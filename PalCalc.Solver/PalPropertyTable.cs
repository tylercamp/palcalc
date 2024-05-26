using PalCalc.Model;
using PalCalc.Solver.PalReference;
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
        public static GroupIdFn WildPalCount = p => p.NumWildPalParticipants();
        public static GroupIdFn NumBreedingSteps = p => p.NumTotalBreedingSteps;
        public static GroupIdFn EffectiveTraits = p => p.EffectiveTraitsHash;
        public static GroupIdFn ActualTraits = p => p.ActualTraits.SetHash();
        public static GroupIdFn TotalEffort = p => p.BreedingEffort.GetHashCode();

        public static GroupIdFn Combine(params GroupIdFn[] fns) => p =>
        {
            int groupId = 0;
            foreach (var fn in fns) groupId = HashCode.Combine(groupId, fn(p));
            return groupId;
        };
    }

    public class PalPropertyTable(PalProperty.GroupIdFn groupIdFn)
    {
        private Dictionary<int, List<IPalReference>> content = new Dictionary<int, List<IPalReference>>();
        public void Add(IPalReference p)
        {
            var groupId = groupIdFn(p);
            var group = content.GetValueOrElse(groupId, new List<IPalReference>());
            content.TryAdd(groupId, group);

            if (!group.Contains(p)) group.Add(p);
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

        public void Filter(int key, FilterFunc filterFn)
        {
            var group = content.GetValueOrDefault(key);
            if (group == null) return;

            var toKeep = filterFn(group);
            group.RemoveAll(r => !toKeep.Contains(r));
            content[key] = filterFn(group).ToList();
        }

        public void Filter(IPalReference key, FilterFunc filterFn) => Filter(groupIdFn(key), filterFn);

        public PalPropertyTable BuildNew(PalProperty.GroupIdFn newIdFn)
        {
            var res = new PalPropertyTable(newIdFn);
            foreach (var r in All)
                res.Add(r);
            return res;
        }
    }
}
