using PalCalc.model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc
{
    internal class FindPerms2Program
    {
        static void Main(string[] args)
        {
            var sw = Stopwatch.StartNew();

            var db = PalDB.FromJson(File.ReadAllText("db.json"));
            Console.WriteLine("Loaded Pal DB");
            var savedInstances = PalInstance.JsonToList(db, File.ReadAllText("savegame.json"));
            Console.WriteLine("Loaded save game");

            // !!! CONFIG !!!
            var MAX_WILD_PALS = 0;
            var MAX_BREEDING_STEPS = 5; // MAX VALUE OF 5; ANY HIGHER WILL USE ALL MEMORY (though this depends on which pal you're targetting)

            var targetInstance = new PalInstance
            {
                Pal = "Suzaku".ToPal(db),
                Gender = PalGender.WILDCARD,
                Traits = new List<Trait> { "Swift".ToTrait(db), "Runner".ToTrait(db), "Nimble".ToTrait(db) },
                Location = null
            };

            /*
             * Given the set of available pals with traits:
             * 
             * 1. For each available (pal, gender) pair, and for each set of instance traits as a subset of the desired traits (e.g. all male lamballs with "runner", all with "runner and swift", etc.),
             *    pick the instance with the fewest total traits
             *
             *    (These are the instances we'll use for graph building)
             * 
             * 2. Find all paths from each pal with a desired trait(s) to the final target pal
             *   2.1. Filter edges to "valid" breedings based on available pals in save; require (node,edge) pairs with opposite
             *        gender
             *   2.2. Allow a limited number of traitless wildcard nodes (i.e. "we can use this path, but you'll need to catch these extra Pals")
             *   2.3. Limit to some maximum number of edges via "search limit"(*2) (breadth-first search of up to N nodes)
             * 
             * 4. Build a dictionary where, for any pal with a specific gender and set of traits, we can find the set of paths to that instance.
             * 
             */

            var relevantPals = PalCalcUtils.RelevantInstancesForTraits(db, savedInstances, targetInstance.Traits);

            Console.WriteLine(
                "Using {0}/{1} pals as relevant inputs with traits:\n- {2}",
                relevantPals.Count,
                savedInstances.Count,
                string.Join("\n- ",
                    relevantPals
                        .OrderBy(p => p.Pal.Name)
                        .ThenBy(p => p.Gender)
                        .ThenBy(p => string.Join(" ", p.Traits.OrderBy(t => t.Name)))
                )
            );

            // `relevantPals` is now a list of all captured Pal types, where multiple of the same pal
            // may be included if they have different genders and/or different matching subsets of
            // the desired traits

            List<IPalReference> availablePalsInstances = new List<IPalReference>(relevantPals.Select(i => new OwnedPalReference(i)));
            if (MAX_WILD_PALS > 0)
            {
                availablePalsInstances.AddRange(db.Pals.Where(p => !relevantPals.Any(i => i.Pal == p)).Select(p => new WildcardPalReference(p)));
            }

            Console.WriteLine("Using {0} pals for graph search:\n- {1}", availablePalsInstances.Count, string.Join("\n- ", availablePalsInstances));

            var palDistances = PalCalcUtils.CalcMinDistances(db);
            var availableTraitsByPal = db.Pals.ToDictionary(p => p, p => availablePalsInstances.Where(inst => inst.Pal == p).SelectMany(inst => inst.Traits).Distinct().ToList());



            Console.WriteLine("Took {0}", TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds));
        }
    }
}
