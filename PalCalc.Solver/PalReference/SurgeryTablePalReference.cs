using PalCalc.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Transactions;

namespace PalCalc.Solver.PalReference
{
    // Note: ChangeGenderSurgeryOperation was added initially but later removed. It was used as another way to enforce
    //       gender requirements in the main BreedingBatchSolver loop, but it added too many new unique options
    //       and caused working-set size to blow up. Therefore we won't formally represent gender-change surgery
    //       here, and will instead just use a flag in the solver settings which will affect how specific-gender
    //       restrictions affect the final estimates.

    public interface ISurgeryOperation
    {
        int GoldCost { get; }
    }

    public class AddPassiveSurgeryOperation(PassiveSkill addedPassive) : ISurgeryOperation
    {
        public PassiveSkill AddedPassive => addedPassive;
        public int GoldCost => addedPassive.SurgeryCost;

        public override int GetHashCode() => HashCode.Combine(nameof(AddPassiveSurgeryOperation), AddedPassive);

        public override bool Equals(object obj) => obj?.GetHashCode() == GetHashCode();

        public override string ToString() => $"AddPassive({AddedPassive.Name})";

        private static ConcurrentDictionary<PassiveSkill, AddPassiveSurgeryOperation> cachedOps = [];
        public static AddPassiveSurgeryOperation NewCached(PassiveSkill addedPassive)
        {
#if DEBUG_CHECKS
            if (addedPassive is RandomPassiveSkill)
                Debugger.Break();
#endif

            if (!cachedOps.ContainsKey(addedPassive))
                cachedOps.TryAdd(addedPassive, new AddPassiveSurgeryOperation(addedPassive));

            return cachedOps[addedPassive];
        }
    }

    public class ReplacePassiveSurgeryOperation(PassiveSkill removedPassive, PassiveSkill addedPassive) : ISurgeryOperation
    {
        public PassiveSkill RemovedPassive => removedPassive;
        public PassiveSkill AddedPassive => addedPassive;

        public int GoldCost => AddedPassive.SurgeryCost;

        public override int GetHashCode() => HashCode.Combine(nameof(ReplacePassiveSurgeryOperation), AddedPassive, RemovedPassive);
        public override bool Equals(object obj) => obj?.GetHashCode() == GetHashCode();

        public override string ToString() => $"ReplacePassive(rem: {RemovedPassive.Name}, add: {AddedPassive.Name})";

        private static ConcurrentDictionary<int, ReplacePassiveSurgeryOperation> cachedOps = [];
        public static ReplacePassiveSurgeryOperation NewCached(PassiveSkill removedPassive, PassiveSkill addedPassive)
        {
#if DEBUG_CHECKS
            if (addedPassive is RandomPassiveSkill)
                Debugger.Break();
#endif

            if (removedPassive is RandomPassiveSkill || addedPassive is RandomPassiveSkill)
                return new ReplacePassiveSurgeryOperation(removedPassive, addedPassive); // TODO - is this necessary?

            int SkillHash(PassiveSkill skill) => skill switch
            {
                RandomPassiveSkill => 0,
                _ => removedPassive.GetHashCode()
            };

            int hash = HashCode.Combine(SkillHash(removedPassive), SkillHash(addedPassive));
            if (!cachedOps.ContainsKey(hash))
                cachedOps.TryAdd(hash, new ReplacePassiveSurgeryOperation(removedPassive, addedPassive));

            return cachedOps[hash];
        }
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
        private int inputHash;

        public SurgeryTablePalReference(IPalReference input, List<ISurgeryOperation> rawOperations)
        {
            Input = input;
            Operations = [.. rawOperations];

            EffectivePassives = [.. input.EffectivePassives];
            ActualPassives = [.. input.ActualPassives];

            Gender = input.Gender;
            TotalCost = input.TotalCost;

            NumTotalWildPals = input.NumTotalWildPals;

            operationsHash = Operations.ListSetHash();
            inputHash = Input.GetHashCode();

#if DEBUG_CHECKS
            if (
                Operations.OfType<ReplacePassiveSurgeryOperation>().Count() > 0 &&
                input.ActualPassives.Count + (
                    Operations.OfType<AddPassiveSurgeryOperation>().Count() + Operations.OfType<ReplacePassiveSurgeryOperation>().Count()
                ) <= GameConstants.MaxTotalPassives
            )
                Debugger.Break();
#endif

            foreach (var op in Operations)
            {
                TotalCost += op.GoldCost;

                switch (op)
                {
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
        public int NumTotalWildPals { get; }

        public IPalRefLocation Location => SurgeryRefLocation.Instance;

        public float TimeFactor { get; }
        public TimeSpan BreedingEffort => Input.BreedingEffort;
        public TimeSpan SelfBreedingEffort => Input.SelfBreedingEffort;

        ConcurrentDictionary<PalGender, IPalReference> cachedGenders = null;

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender, bool useReverser)
        {
            cachedGenders ??= [];
            if (cachedGenders.ContainsKey(gender)) return cachedGenders[gender];

            IPalReference result;
            var newParent = Input.WithGuaranteedGender(db, gender, useReverser);

            // Composite references may resolve to a pal with fewer (but never more) passives than the original Input,
            // causing some ReplacePassive operations to lose their "removed" passive and freeing up a space that could
            // be used by an AddPassive operation instead.
            if (newParent.EffectivePassives.Count < Input.EffectivePassives.Count)
            {
                var newOperations = new List<ISurgeryOperation>();
                int numToSimplify = Input.EffectivePassives.Count - newParent.EffectivePassives.Count;
                foreach (var op in Operations)
                {
                    if (op is ReplacePassiveSurgeryOperation rpso && numToSimplify > 0)
                    {
                        newOperations.Add(AddPassiveSurgeryOperation.NewCached(rpso.AddedPassive));
                        --numToSimplify;
                    }
                    else
                    {
                        newOperations.Add(op);
                    }
                }

                result = new SurgeryTablePalReference(newParent, newOperations);            }
            else
            {
                // NewCached takes `ref` since it will modify and try to optimize the list of operations, but `Operations` should have
                // already been optimized, so it should no-op
                var selfOps = Operations;
                result = new SurgeryTablePalReference(newParent, selfOps);
            }

            cachedGenders[gender] = result;

            return result;
        }

        // ---------------------------------------------------------------------------------
        public override string ToString() => $"Surgery on {{{Input}}} : {string.Join("; ", Operations)}";

        public override bool Equals(object obj)
        {
            var asSurgery = obj as SurgeryTablePalReference;
            if (ReferenceEquals(asSurgery, null)) return false;

            return GetHashCode() == obj.GetHashCode();
        }

        public override int GetHashCode() =>
            HashCode.Combine(nameof(SurgeryTablePalReference), inputHash, operationsHash);
    }
}