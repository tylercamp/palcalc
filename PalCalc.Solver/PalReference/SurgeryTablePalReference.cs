using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;

namespace PalCalc.Solver.PalReference
{
    public interface ISurgeryOperation
    {
        int GoldCost { get; }
    }

    public class AddPassiveSurgeryOperation(PassiveSkill addedPassive) : ISurgeryOperation
    {
        public PassiveSkill AddedPassive => addedPassive;
        public int GoldCost => addedPassive.SurgeryCost;

        public override int GetHashCode() => HashCode.Combine(nameof(AddPassiveSurgeryOperation), AddedPassive);

        public override string ToString() => $"AddPassive({AddedPassive.InternalName})";
    }

    public class ReplacePassiveSurgeryOperation(PassiveSkill removedPassive, PassiveSkill addedPassive) : ISurgeryOperation
    {
        public PassiveSkill RemovedPassive => removedPassive;
        public PassiveSkill AddedPassive => addedPassive;

        public int GoldCost => AddedPassive.SurgeryCost;

        public override int GetHashCode() => HashCode.Combine(nameof(ReplacePassiveSurgeryOperation), AddedPassive, RemovedPassive);

        public override string ToString() => $"ReplacePassive(rem: {RemovedPassive.InternalName}, add: {AddedPassive.InternalName})";
    }

    public class ChangeGenderSurgeryOperation(PalGender newGender) : ISurgeryOperation
    {
        public PalGender NewGender => newGender;
        public int GoldCost => 0;

        public override int GetHashCode() => HashCode.Combine(nameof(ChangeGenderSurgeryOperation), NewGender);

        public override string ToString() => $"ChangeGender({NewGender})";
    }

    /// <summary>
    /// Represents a pal that has undergone surgery to add or replace a passive skill.
    /// Surgery is instantaneous (no additional time effort) but incurs a monetary
    /// <see cref="PassiveSkill.SurgeryCost"/> that is accumulated in <see cref="TotalCost"/>.
    /// </summary>
    public class SurgeryTablePalReference : IPalReference
    {
        public IPalReference Input { get; }
        public List<ISurgeryOperation> Operations { get; }

        public PalGender Gender { get; }

        public List<PassiveSkill> EffectivePassives { get; }
        public List<PassiveSkill> ActualPassives { get; }

        public int TotalCost { get; }

        private int operationsHash;

        public SurgeryTablePalReference(IPalReference input, List<ISurgeryOperation> operations)
        {
            Input = input;
            Operations = operations;

            EffectivePassives = [.. input.EffectivePassives];
            ActualPassives = [.. input.ActualPassives];

            Gender = input.Gender;
            TotalCost = input.TotalCost;

            operationsHash = operations.SetHash();

#if DEBUG_CHECKS
            if (
                operations.OfType<ReplacePassiveSurgeryOperation>().Count() > 0 &&
                input.ActualPassives.Count + (
                    operations.OfType<AddPassiveSurgeryOperation>().Count() + operations.OfType<ReplacePassiveSurgeryOperation>().Count()
                ) <= GameConstants.MaxTotalPassives
            )
                Debugger.Break();
#endif

            foreach (var op in operations)
            {
                TotalCost += op.GoldCost;

                switch (op)
                {
                    case ChangeGenderSurgeryOperation cgso:
#if DEBUG_CHECKS
                        if (input.Gender == cgso.NewGender) Debugger.Break();
#endif
                        Gender = cgso.NewGender;
                        break;

                    case AddPassiveSurgeryOperation apso:
#if DEBUG_CHECKS
                        if (input.ActualPassives.Contains(apso.AddedPassive)) Debugger.Break();
                        if (input.ActualPassives.Count == GameConstants.MaxTotalPassives) Debugger.Break();
#endif

                        EffectivePassives.Add(apso.AddedPassive);
                        ActualPassives.Add(apso.AddedPassive);
                        break;

                    case ReplacePassiveSurgeryOperation rpso:
                        int removedEffectiveIdx = rpso.RemovedPassive is RandomPassiveSkill ? EffectivePassives.FindIndex(p => p is RandomPassiveSkill) : EffectivePassives.IndexOf(rpso.RemovedPassive);
                        int removedActualIdx = rpso.RemovedPassive is RandomPassiveSkill ? ActualPassives.FindIndex(p => p is RandomPassiveSkill || !EffectivePassives.Contains(p)) : EffectivePassives.IndexOf(rpso.RemovedPassive);

#if DEBUG_CHECKS
                        if (input.ActualPassives.Contains(rpso.AddedPassive)) Debugger.Break();

                        if (removedEffectiveIdx < 0) Debugger.Break();
                        if (removedActualIdx < 0) Debugger.Break();
#endif

                        EffectivePassives.RemoveAt(removedEffectiveIdx);
                        ActualPassives.RemoveAt(removedActualIdx);

                        EffectivePassives.Add(rpso.AddedPassive);
                        ActualPassives.Add(rpso.AddedPassive);
                        break;
                }
            }

#if DEBUG_CHECKS
            if (EffectivePassives.Count > GameConstants.MaxTotalPassives || ActualPassives.Count > GameConstants.MaxTotalPassives)
                Debugger.Break();
#endif

            EffectivePassivesHash = EffectivePassives.Select(p => p.InternalName).SetHash();

            TimeFactor = EffectivePassives.ToTimeFactor();
        }

        // ---------------------------------------------------------------------------------
        // IPalReference implementation (delegate most members to parent)
        // ---------------------------------------------------------------------------------
        public Pal Pal => Input.Pal;
        public int EffectivePassivesHash { get; }
        public IV_Set IVs => Input.IVs;

        public int NumTotalBreedingSteps => Input.NumTotalBreedingSteps;
        public int NumTotalEggs => Input.NumTotalEggs;

        public IPalRefLocation Location => Input.Location;

        public float TimeFactor { get; }
        public TimeSpan BreedingEffort => Input.BreedingEffort;
        public TimeSpan SelfBreedingEffort => Input.SelfBreedingEffort;

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            var newParent = Input.WithGuaranteedGender(db, gender);

            if (ReferenceEquals(newParent, Input)) return this;

            // Composite references may resolve to a pal with fewer (but never more) passives than the original Input
            if (newParent.EffectivePassives.Count < Input.EffectivePassives.Count)
            {
                var newOperations = new List<ISurgeryOperation>();
                int numToSimplify = Input.EffectivePassives.Count - newParent.EffectivePassives.Count;
                foreach (var op in Operations)
                {
                    if (op is ReplacePassiveSurgeryOperation rpso && numToSimplify > 0)
                    {
                        newOperations.Add(new AddPassiveSurgeryOperation(rpso.AddedPassive));
                        --numToSimplify;
                    }
                    else
                    {
                        newOperations.Add(op);
                    }
                }

                return new SurgeryTablePalReference(newParent, newOperations);
            }
            else
            {
                return new SurgeryTablePalReference(newParent, Operations);
            }
        }

        // ---------------------------------------------------------------------------------
        public override string ToString() => $"Surgery on {{{Input}}} : {string.Join("; ", Operations)}";

        public override int GetHashCode() =>
            HashCode.Combine(nameof(SurgeryTablePalReference), Input.GetHashCode(), operationsHash);
    }
}