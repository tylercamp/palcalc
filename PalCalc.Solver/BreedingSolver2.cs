using PalCalc.Model;
using PalCalc.Solver.FImpl;
using PalCalc.Solver.FImpl.AttrId;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using ZLinq;

namespace PalCalc.Solver
{
    public class BreedingSolver2(BreedingSolverSettings settings)
    {
        private PalBreedingDB breedingdb = PalBreedingDB.LoadEmbedded(settings.DB);

        /*
         * The original impl. did an all-pairs calculation. It constantly compared costs and pruned
         * inefficient results to keep the runtime manageable. It had a broad calculation phase, followed
         * by a consolidation phase.
         * 
         * This new impl. has a more "directed" process:
         * 
         * 1. BFS on pal attributes, prioritizes finding new ways to produce pals
         * 2. 
         */

        private List<(FPal, FPal)> GatherRelevantPalPairs(Pal target)
        {
            var result = new HashSet<(Pal, Pal)>();
            var visited = new HashSet<Pal>();
            var toCheck = new Stack<Pal>();
            toCheck.Push(target);

            while (toCheck.TryPop(out var pal))
            {
                foreach (var (parent1, others) in breedingdb.BreedingByChild[pal])
                {
                    if (!visited.Contains(parent1.Pal)) toCheck.Push(parent1.Pal);
                    foreach (var parent2 in others)
                    {
                        if (!visited.Contains(parent2.Pal)) toCheck.Push(parent2.Pal);

                        var p1 = parent1.Pal;
                        var p2 = parent2.Pal;

                        if (p1.Id.CompareTo(p2.Id) > 0)
                            (p1, p2) = (p2, p1);

                        result.Add((p1, p2));
                    }
                }
            }

            return result.Select(p => (new FPal(p.Item1), new FPal(p.Item2))).ToList();
        }

        private void InsertOwnedPaths(Dictionary<FPalSpec, HashSet<FPath>> paths, PalSpecifier spec)
        {
            foreach (var inst in settings.OwnedPals.Where(inst => inst.PassiveSkills.Except(spec.DesiredPassives.ModelObjects).Count() <= settings.MaxInputIrrelevantPassives))
                paths.TryAdd(FPalSpec.FromInstance(settings.DB, spec, inst), [FPath.Owned]);

            foreach (var g in paths.Keys.ToList().GroupBy(k => (k.Pal, k.IVs, k.Passives.ToDesiredSet(spec.DesiredPassives))))
            {
                var male = g.Where(p => p.Gender == FGender.Male);
                var female = g.Where(p => p.Gender == FGender.Female);

                if (male.Any() && female.Any())
                {
                    var maleMinRandom = male.MinBy(p => p.Passives.CountRandom);
                    var femaleMinRandom = female.MinBy(p => p.Passives.CountRandom);

                    var refSpec = maleMinRandom.Passives.CountRandom < femaleMinRandom.Passives.CountRandom
                        ? femaleMinRandom
                        : maleMinRandom;

                    var combinedSpec = new FPalSpec(
                        Pal: refSpec.Pal,
                        Gender: FGender.Wildcard,
                        Passives: refSpec.Passives,
                        IVs: FIVSet.Merge(maleMinRandom.IVs, femaleMinRandom.IVs)
                    );

                    paths.Add(combinedSpec, [FPath.Owned]);
                }
            }
        }

        private void InsertCapturePaths(Dictionary<FPalSpec, HashSet<FPath>> paths, PalSpecifier spec)
        {
            if (settings.MaxWildPals == 0) return;

            var existingPals = paths.Keys.Select(k => k.Pal).Distinct().ToList();
            var toAdd = settings.DB.Pals.Intersect(settings.AllowedWildPals).Select(p => new FPal(p)).Except(existingPals);

            foreach (var pal in toAdd)
            {
                var guaranteedPassives = FPassiveSet.FromModel(settings.DB, pal.ModelObject(settings.DB).GuaranteedPassiveSkills(settings.DB).ToList());
                var guaranteedPassiveSpec = FPassiveSpec.FromMatch(spec.DesiredPassives, guaranteedPassives);

                for (int numRandom = guaranteedPassiveSpec.CountRandom; numRandom <= Math.Min(GameConstants.MaxTotalPassives, settings.MaxInputIrrelevantPassives); numRandom++)
                {
                    var extraRandom = guaranteedPassiveSpec.CountRandom - numRandom;
                    var curPassives = guaranteedPassives.Concat(FPassiveSet.RepeatRandom(extraRandom));
                    var curPassiveSpec = FPassiveSpec.FromMatch(spec.DesiredPassives, curPassives);

                    var newSpec = new FPalSpec(
                        Pal: pal,
                        Gender: FGender.Wildcard,
                        Passives: curPassiveSpec,
                        IVs: FIVSet.AllRandom
                    );

                    HashSet<FPath> newSpecPaths;
                    if (paths.TryGetValue(newSpec, out HashSet<FPath> value))
                        newSpecPaths = value;
                    else
                    {
                        newSpecPaths = [];
                        paths.Add(newSpec, newSpecPaths);
                    }

                    newSpecPaths.Add(FPath.Captured);
                }
            }
        }

        public List<IPalReference> SolveFor(PalSpecifier spec, SolverStateController controller)
        {
            // acts as the WorkingSet
            var discoveredPaths = new Dictionary<FPalSpec, HashSet<FPath>>();

            InsertOwnedPaths(discoveredPaths, spec);
            InsertCapturePaths(discoveredPaths, spec);

            var relevantPalPairs = GatherRelevantPalPairs(spec.Pal);

            bool didChange = false;
            do
            {
                // Breeding pass
                foreach (var pair in relevantPalPairs)
                {
                    var allp1 = discoveredPaths.Keys.Where(k => k.Pal == pair.Item1);
                    var allp2 = discoveredPaths.Keys.Where(k => k.Pal == pair.Item2);

                    var pairs = allp1
                        .AsValueEnumerable()
                        .SelectMany(
                            p1 => allp2.AsValueEnumerable().Select(p2 => (p1, p2))
                        )
                        .Where(pair => pair.p1.Gender.IsCompatible(pair.p2.Gender));

                    foreach (var (p1, p2) in pairs)
                    {
                        var finalIVs = FIVSet.Merge(p1.IVs, p2.IVs);

                        var p1Passives = p1.Passives.ToFilteredSet(spec.DesiredPassives);
                        var p2Passives = p2.Passives.ToFilteredSet(spec.DesiredPassives);

                        var parentPassives = p1Passives.Concat(p2Passives);

                        var availableRequiredPassives = parentPassives.Intersect(spec.RequiredPassives);
                        var availableOptionalPassives = parentPassives.Intersect(spec.OptionalPassives);



                    }
                }

                // Surgery pass

            } while (didChange);
        }
    }
}
