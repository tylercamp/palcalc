using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    /// <summary>
    /// Groups results for presentation screens. The solver uses
    /// <see cref="EffectivePropertiesKey"/> instead when deciding which
    /// candidates are interchangeable for future breeding.
    /// </summary>
    public static class PalResultProperty
    {
        public delegate int GroupIdFn(IPalReference p);

        public static GroupIdFn Pal { get; } =
            reference => reference.Pal.Id.GetHashCode();
        public static GroupIdFn Gender { get; } =
            reference => (int)reference.Gender;
        public static GroupIdFn WildPalCount { get; } =
            reference => reference.NumTotalWildPals;
        public static GroupIdFn NumBreedingSteps { get; } =
            reference => reference.NumTotalBreedingSteps;
        public static GroupIdFn EffectivePassives { get; } =
            reference => reference.EffectivePassivesHash;
        public static GroupIdFn RelevantPassives { get; } =
            reference => reference.ActualPassives
                .Intersect(reference.EffectivePassives)
                .SetHash();
        public static GroupIdFn ActualPassives { get; } =
            reference => reference.ActualPassives.SetHash();
        public static GroupIdFn TotalEffort { get; } =
            reference => reference.BreedingEffort.GetHashCode();
        public static GroupIdFn LocationType { get; } =
            reference => reference.Location.GetType().GetHashCode();
        public static GroupIdFn IvRelevance { get; } =
            reference => HashCode.Combine(
                reference.IVs.HP.IsRelevant,
                reference.IVs.Attack.IsRelevant,
                reference.IVs.Defense.IsRelevant
            );
        public static GroupIdFn IvExact { get; } =
            reference => HashCode.Combine(
                reference.IVs.HP,
                reference.IVs.Attack,
                reference.IVs.Defense
            );
        public static GroupIdFn GoldCost { get; } =
            reference => reference.TotalCost;

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
    /// Presentation utility for reducing result clutter after a solve.
    /// This is not used as solver frontier identity.
    /// </summary>
    public sealed class PalResultGrouping(
        PalResultProperty.GroupIdFn groupIdFn
    )
    {
        private readonly Dictionary<int, List<IPalReference>> content =
            [];

        public void Add(IPalReference p)
        {
            var groupId = groupIdFn(p);
            if (!content.TryGetValue(groupId, out var group))
            {
                group = [];
                content.Add(groupId, group);
            }

            if (!group.Contains(p)) group.Add(p);
        }

        public void AddRange(IEnumerable<IPalReference> items)
        {
            foreach (var i in items) Add(i);
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
            foreach (var r in All)
                res.Add(r);
            return res;
        }
    }
}
