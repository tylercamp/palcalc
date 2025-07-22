using PalCalc.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;

namespace PalCalc.Solver.PalReference
{
    public interface ISurgeryOperation
    {
        int GoldCost { get; }
        int NumGenderReversers { get; }
    }

    public class AddPassiveSurgeryOperation(PassiveSkill addedPassive) : ISurgeryOperation
    {
        public PassiveSkill AddedPassive => addedPassive;
        public int GoldCost => addedPassive.SurgeryCost;
        public int NumGenderReversers => 0;

        public override int GetHashCode() => HashCode.Combine(nameof(AddPassiveSurgeryOperation), AddedPassive);

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
        public int NumGenderReversers => 0;

        public override int GetHashCode() => HashCode.Combine(nameof(ReplacePassiveSurgeryOperation), AddedPassive, RemovedPassive);

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

    public class ChangeGenderSurgeryOperation(PalGender newGender) : ISurgeryOperation
    {
        public PalGender NewGender => newGender;
        public int GoldCost => 0;
        public int NumGenderReversers => 1;

        public override int GetHashCode() => HashCode.Combine(nameof(ChangeGenderSurgeryOperation), NewGender);

        public override string ToString() => $"ChangeGender({NewGender})";

        private static ConcurrentDictionary<PalGender, ChangeGenderSurgeryOperation> cachedOps = [];
        public static ChangeGenderSurgeryOperation NewCached(PalGender newGender)
        {
            if (!cachedOps.ContainsKey(newGender))
                cachedOps.TryAdd(newGender, new ChangeGenderSurgeryOperation(newGender));

            return cachedOps[newGender];
        }
    }

    public interface ISurgeryCachingPalReference
    {
        ConcurrentDictionary<int, IPalReference> SurgeryResultCache { get; }
    }

    /// <summary>
    /// Represents a pal that has undergone surgery to add or replace a passive skill.
    /// Surgery is instantaneous (no additional time effort) but incurs a monetary
    /// <see cref="PassiveSkill.SurgeryCost"/> that is accumulated in <see cref="TotalCost"/>.
    /// </summary>
    public class SurgeryTablePalReference : IPalReference, ISurgeryCachingPalReference
    {
        public IPalReference Input { get; }
        public List<ISurgeryOperation> Operations { get; }

        public PalGender Gender { get; }

        public List<PassiveSkill> EffectivePassives { get; }
        public List<PassiveSkill> ActualPassives { get; }

        public int TotalCost { get; }

        private int operationsHash;

        // primarily handles multiple gender-change ops, takes the last gender-change op
        // (`ref` param isn't functionally required, but meant to indicate that the list contents will change)
        private static void SimplifyOperations(IPalReference input, ref List<ISurgeryOperation> ops)
        {
            ChangeGenderSurgeryOperation lastGenderOp = null;

            foreach (var op in ops)
            {
                if (op is ChangeGenderSurgeryOperation cgso)
                    lastGenderOp = cgso;
            }

            if (lastGenderOp != null)
            {
                for (int i = 0; i < ops.Count; i++)
                {
                    if (ops[i] is ChangeGenderSurgeryOperation cgso)
                    {
                        if (cgso != lastGenderOp || input.Gender == cgso.NewGender)
                        {
                            ops.RemoveAt(i);
                            --i;
                        }
                    }
                }
            }
        }

        public SurgeryTablePalReference(IPalReference input, ref List<ISurgeryOperation> rawOperations)
        {
            SimplifyOperations(input, ref rawOperations);

            Input = input;
            Operations = rawOperations;

            EffectivePassives = [.. input.EffectivePassives];
            ActualPassives = [.. input.ActualPassives];

            Gender = input.Gender;
            TotalCost = input.TotalCost;

            operationsHash = Operations.ListSetHash();

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

            NumTotalSurgerySteps = 1 + Input.NumTotalSurgerySteps;
            NumTotalGenderReversers = Operations.Sum(op => op.NumGenderReversers) + Input.NumTotalGenderReversers;
        }

        // ---------------------------------------------------------------------------------
        // IPalReference implementation (delegate most members to parent)
        // ---------------------------------------------------------------------------------
        public Pal Pal => Input.Pal;
        public int EffectivePassivesHash { get; }
        public IV_Set IVs => Input.IVs;

        public int NumTotalBreedingSteps => Input.NumTotalBreedingSteps;
        public int NumTotalEggs => Input.NumTotalEggs;
        public int NumTotalSurgerySteps { get; }
        public int NumTotalGenderReversers { get; }

        public IPalRefLocation Location => SurgeryRefLocation.Instance;

        public float TimeFactor { get; }
        public TimeSpan BreedingEffort => Input.BreedingEffort;
        public TimeSpan SelfBreedingEffort => Input.SelfBreedingEffort;

        private ConcurrentDictionary<int, IPalReference> surgeryResultCache = null;
        public ConcurrentDictionary<int, IPalReference> SurgeryResultCache => surgeryResultCache ??= new();

        ConcurrentDictionary<PalGender, IPalReference> cachedGenders = null;

        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender)
        {
            cachedGenders ??= [];
            if (cachedGenders.ContainsKey(gender)) return cachedGenders[gender];

            IPalReference result;
            var newParent = Input.WithGuaranteedGender(db, gender);

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

                result = SurgeryTablePalReference.NewCached(newParent, ref newOperations);
            }
            else
            {
                // NewCached takes `ref` since it will modify and try to optimize the list of operations, but `Operations` should have
                // already been optimized, so it should no-op
                var selfOps = Operations;
                result = SurgeryTablePalReference.NewCached(newParent, ref selfOps);
            }

            cachedGenders[gender] = result;

            return result;
        }

        public static IPalReference EnforceGender(IPalReference r, PalGender gender)
        {
            if (r.Gender == gender) return r;

#if DEBUG && DEBUG_CHECKS
            // this is *technically* OK, but there's no valid reason for this to happen, which suggests
            // something went wrong elsewhere
            if (gender == PalGender.WILDCARD) Debugger.Break();
#endif

            List<ISurgeryOperation> newOps;

            if (r is SurgeryTablePalReference stpr)
            {
                newOps = new List<ISurgeryOperation>(stpr.Operations.Count + 1);
                newOps.AddRange(stpr.Operations);
                newOps.Add(ChangeGenderSurgeryOperation.NewCached(gender));
                return NewCached(stpr.Input, ref newOps);
            }
            else
            {
                newOps = new List<ISurgeryOperation>(1);
                newOps.Add(ChangeGenderSurgeryOperation.NewCached(gender));
                return NewCached(r, ref newOps);
            }
        }

        public static IPalReference NewCached(IPalReference r, ref List<ISurgeryOperation> rawOperations)
        {
            if (r is ISurgeryCachingPalReference cachingRef)
            {
                SimplifyOperations(r, ref rawOperations);
                var opsHash = rawOperations.ListSetHash();

                var cache = cachingRef.SurgeryResultCache;
                if (!cache.ContainsKey(opsHash))
                    cache.TryAdd(opsHash, rawOperations.Count == 0 ? r : new SurgeryTablePalReference(r, ref rawOperations));

                return cache[opsHash];
            }
            else
            {
                return new SurgeryTablePalReference(r, ref rawOperations);
            }
        }

        // ---------------------------------------------------------------------------------
        public override string ToString() => $"Surgery on {{{Input}}} : {string.Join("; ", Operations)}";

        public override int GetHashCode() =>
            HashCode.Combine(nameof(SurgeryTablePalReference), Input.GetHashCode(), operationsHash);
    }
}