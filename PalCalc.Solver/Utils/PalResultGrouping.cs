using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.ResultPruning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Utils
{
    public static class PalResultProperty
    {
        public delegate int GroupIdFn(IPalReference p);

        public static GroupIdFn Pal { get; } = reference => reference.Pal.Id.GetHashCode();
        public static GroupIdFn Gender { get; } = reference => (int)reference.Gender;
        public static GroupIdFn WildPalCount { get; } = reference => reference.NumTotalWildPals;
        public static GroupIdFn NumBreedingSteps { get; } = reference => reference.NumTotalBreedingSteps;
        public static GroupIdFn EffectivePassives { get; } = reference => reference.EffectivePassivesHash;
        public static GroupIdFn RelevantPassives { get; } = reference => reference.ActualPassives.Intersect(reference.EffectivePassives).SetHash();
        public static GroupIdFn ActualPassives { get; } = reference => reference.ActualPassives.SetHash();
        public static GroupIdFn TotalEffort { get; } = reference => reference.BreedingEffort.GetHashCode();
        public static GroupIdFn LocationType { get; } = reference => reference.Location.GetType().GetHashCode();
        public static GroupIdFn IvRelevance { get; } = reference => HashCode.Combine(reference.IVs.HP.IsRelevant, reference.IVs.Attack.IsRelevant, reference.IVs.Defense.IsRelevant);
        public static GroupIdFn IvExact { get; } = reference => HashCode.Combine(reference.IVs.HP, reference.IVs.Attack, reference.IVs.Defense);
        public static GroupIdFn GoldCost { get; } = reference => reference.TotalCost;

        public static GroupIdFn SpecialCakes { get; } = reference => reference.ExpectedSpecialCakes;

        /// <summary>
        /// Creates a grouping function for the distinct location types in a reference and its ancestry.
        /// Use a new function for each grouping pass so the parent cache stays local to that pass.
        /// </summary>
        public static GroupIdFn CreateAncestryLocationTypeGrouping()
        {
            Type[] locationTypes =
            [
                typeof(OwnedRefLocation), typeof(CapturedRefLocation), typeof(BredRefLocation),
                typeof(SurgeryRefLocation), typeof(CompositeRefLocation)
            ];
            var hashes = new int[1 << locationTypes.Length];
            for (var mask = 1; mask < hashes.Length; mask++)
            {
                var types = locationTypes.Where((_, index) => (mask & (1 << index)) != 0);
                hashes[mask] = types.SetHash();
            }

            // Shared parents only need their location types computed once. Use identity because
            // two references can compare equal without sharing the same ancestry.
            var parentMasks = new Dictionary<IPalReference, int>(ReferenceEqualityComparer.Instance);
            return reference =>
            {
                var mask = LocationMask(reference);
                return mask == 0
                    ? reference.AllReferences().Select(r => r.Location.GetType()).Distinct().SetHash()
                    : hashes[mask];
            };

            int ParentMask(IPalReference parent)
            {
                if (!parentMasks.TryGetValue(parent, out var mask))
                    parentMasks.Add(parent, mask = LocationMask(parent));
                return mask;
            }

            int LocationMask(IPalReference reference)
            {
                var index = Array.IndexOf(locationTypes, reference.Location.GetType());
                if (index < 0) return 0;

                var ownMask = 1 << index;
                switch (reference)
                {
                    case BredPalReference bred:
                        var first = ParentMask(bred.Parent1);
                        var second = ParentMask(bred.Parent2);
                        return first == 0 || second == 0 ? 0 : ownMask | first | second;
                    case SurgeryTablePalReference surgery:
                        var input = ParentMask(surgery.Input);
                        return input == 0 ? 0 : ownMask | input;
                    default:
                        return ownMask;
                }
            }
        }

        /// <summary>
        /// Makes a grouping function based on the result of applying `mainFn` to all
        /// elements (i.e. children and self) of a provided pal reference.
        /// </summary>
        public static GroupIdFn Recursive(GroupIdFn mainFn) => p =>
            p.AllReferences().Select(i => mainFn(i)).SetHash();

        public static GroupIdFn RecursiveWhere(GroupIdFn mainFn, Func<IPalReference, bool> filter) => p =>
            p.AllReferences().Where(filter).Select(i => mainFn(i)).SetHash();

        /// <summary>
        /// Makes a grouping function as a combination of the provided functions.
        /// </summary>
        public static GroupIdFn Combine(params GroupIdFn[] fns) => p =>
        {
            int groupId = 0;
            foreach (var fn in fns) groupId = HashCode.Combine(groupId, fn(p));
            return groupId;
        };
    }

    /// <summary>
    /// Helper for grouping pals by various properties. Not meant for internal solver
    /// use (it uses `EffectivePropertyKey`), but makes it easier to do additional
    /// pruning of results from the solver.
    /// </summary>
    public sealed class PalResultGrouping(PalResultProperty.GroupIdFn groupIdFn)
    {
        private readonly Dictionary<int, List<IPalReference>> content = [];

        public void Add(IPalReference p)
        {
            var group = GetOrAddGroup(groupIdFn(p));
            if (!group.Contains(p)) group.Add(p);
        }

        private List<IPalReference> GetOrAddGroup(int groupId)
        {
            if (!content.TryGetValue(groupId, out var group))
            {
                group = [];
                content.Add(groupId, group);
            }
            return group;
        }

        public void AddRange(IEnumerable<IPalReference> items)
        {
            // Keep the lists in first-seen order for pruning, and only index membership during bulk insertion.
            var seenByGroup = new Dictionary<int, HashSet<IPalReference>>();
            foreach (var item in items)
            {
                var groupId = groupIdFn(item);
                var group = GetOrAddGroup(groupId);
                if (!seenByGroup.TryGetValue(groupId, out var seen))
                    seenByGroup.Add(groupId, seen = group.ToHashSet());

                if (seen.Add(item)) group.Add(item);
            }
        }

        public void Remove(IPalReference p) => content.GetValueOrDefault(groupIdFn(p))?.Remove(p);

        public IReadOnlyList<IPalReference> this[IPalReference r] => content.GetValueOrDefault(groupIdFn(r));
        public IReadOnlyList<IPalReference> this[int groupId] => content.GetValueOrDefault(groupId);

        public IEnumerable<IPalReference> All => content.SelectMany(kvp => kvp.Value);

        public int TotalCount => content.Sum(kvp => kvp.Value.Count);

        public delegate IEnumerable<IPalReference> FilterFunc(IEnumerable<IPalReference> input);
        public void FilterAll(FilterFunc filterFn)
        {
            foreach (var group in content.Keys)
                content[group] = filterFn(content[group]).ToList();
        }

        public void FilterAll(ResultPruningPolicy policy, CancellationToken token)
        {
            var pruner = policy.Create(token);
            foreach (var group in content.Keys.TakeWhile(_ => !token.IsCancellationRequested))
                content[group] = pruner.Apply(content[group], new CachedResultData(content[group])).ToList();
        }

        public void Filter(int key, FilterFunc filterFn)
        {
            var group = content.GetValueOrDefault(key);
            if (group == null) return;

            content[key] = filterFn(group).ToList();
        }

        public void Filter(IPalReference key, FilterFunc filterFn) => Filter(groupIdFn(key), filterFn);

        public PalResultGrouping BuildNew(PalResultProperty.GroupIdFn newIdFn)
        {
            var res = new PalResultGrouping(newIdFn);
            res.AddRange(All);
            return res;
        }
    }
}
