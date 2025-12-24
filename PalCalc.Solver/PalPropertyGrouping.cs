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
    public static class PalProperty
    {
        public delegate int GroupIdFn(IPalReference p);

        public static GroupIdFn Pal = p => p.Pal.Id.GetHashCode();
        public static GroupIdFn Gender = p => (int)p.Gender;
        public static GroupIdFn WildPalCount = p => p.NumTotalWildPals;
        public static GroupIdFn NumBreedingSteps = p => p.NumTotalBreedingSteps;
        public static GroupIdFn EffectivePassives = p => p.EffectivePassivesHash;
        public static GroupIdFn RelevantPassives = p => p.ActualPassives.Intersect(p.EffectivePassives).SetHash();
        public static GroupIdFn ActualPassives = p => p.ActualPassives.SetHash();
        public static GroupIdFn TotalEffort = p => p.BreedingEffort.GetHashCode();
        public static GroupIdFn LocationType = p => p.Location.GetType().GetHashCode();
        public static GroupIdFn IvRelevance = p => HashCode.Combine(p.IVs.HP.IsRelevant, p.IVs.Attack.IsRelevant, p.IVs.Defense.IsRelevant);
        public static GroupIdFn IvExact = p => HashCode.Combine(p.IVs.HP, p.IVs.Attack, p.IVs.Defense);
        public static GroupIdFn GoldCost = p => p.TotalCost;

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

    public class PalPropertyGrouping(PalProperty.GroupIdFn groupIdFn)
    {
        private Dictionary<int, List<IPalReference>> content = new Dictionary<int, List<IPalReference>>();

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

        public void FilterAll(PruningRulesBuilder prb, CancellationToken token)
        {
            var pruner = prb.BuildAggregate(token);
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

        public PalPropertyGrouping BuildNew(PalProperty.GroupIdFn newIdFn)
        {
            var res = new PalPropertyGrouping(newIdFn);
            foreach (var r in All)
                res.Add(r);
            return res;
        }
    }
}
