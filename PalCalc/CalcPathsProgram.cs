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
    /*
     * Encode breeding paths as [p1, p2] [a, b, c, ...] [r]
     * 
     * First two elements are parents, producing a `child`
     * which is bred with `a`
     * whose child is bred with `b`
     * whose child is bred with `c`
     * etc.
     * 
     * and produces `[r]`
     */

    class BreedingPath
    {
        public Pal Parent1 { get; set; }
        public Pal Parent2 { get; set; }
        public Pal Result { get; set; }

        public List<Pal> Intermediates { get; set; }

        public int NumBreeds => 1 + Intermediates.Count;

        public IEnumerable<Pal> Parents
        {
            get
            {
                yield return Parent1;
                yield return Parent2;
            }
        }

        public IEnumerable<Pal> Participants => Parents.Concat(Intermediates);

        public IEnumerable<BreedingResult> AsBreedingResults(PalDB db)
        {
            var initialBreed = db.BreedingByParent[Parent1][Parent2];
            yield return initialBreed;

            var lastBreed = initialBreed;
            foreach (var nextParent in Intermediates)
            {
                lastBreed = db.BreedingByParent[lastBreed.Child][nextParent];
                yield return lastBreed;
            }
        }

        // returns the index of which breeding step the pal get used in
        public int BreedIndexOfParticipant(Pal participant)
        {
            int index = 1;

            if (!Parents.Contains(participant))
            {
                bool didFind = false;
                IEnumerable<Pal> remaining = Participants.Skip(2);
                while (remaining.Any() && !didFind)
                {
                    index += 1;
                    var toCheck = remaining.First();
                    remaining = remaining.Skip(1);

                    didFind = toCheck == participant;
                }

                if (!didFind) throw new Exception("Pal is not a participant of this breeding path");
            }

            return index;
        }
    }

    /**
     * Precalculated set of breeding paths from any parent pal to any child pal, up to a maximum number of steps.
     * 
     * The list of paths between parent and child will be unique in their unordered set of participants, e.g.
     * 
     * [Parent 1 = Pal A, Parent 2 = Pal B, Intermediate 1 = Pal C, Intermediate 2 = Pal D]
     *    
     *    ...will also be used to represent:
     * 
     * [Parent 1 = Pal A, Parent 2 = Pal C, Intermediate 1 = Pal D, Intermediate 2 = Pal B]
     * 
     * (if applicable)
     * 
     * (The specific interim children implied by ordering is irrelevant to the goal of producing a child from a given parent.)
     */
    class BreedingPathsCache
    {
        public int MaxDepth { get; set; }

        // Dict[Parent, Dict[Child, Dict[NumSteps, List[Path]]]]
        public Dictionary<Pal, Dictionary<Pal, Dictionary<int, List<BreedingPath>>>> Paths { get; set; }
    }

    /*
     * Generates a directory structure:
     * 
     * out/
     *   {TargetPal}/
     *      {depth}/
     *          Parent1_Parent2.json // where (Parent1, Parent2) are ordered alphabetically
     */

    internal class CalcPathsProgram
    {
        static void Main(string[] args)
        {
            var sw = Stopwatch.StartNew();

            var db = PalDB.FromJson(File.ReadAllText("db.json"));
            Console.WriteLine("Loaded Pal DB");

            int MAX_PARTICIPANTS = 5; // (4 breeding steps) even just 6 participants (5 steps) would require several trillion permutations

            // generate paths of DEPTH=1
            // TODO

            var lastDepthPaths = new List<List<BreedingPath>>();


            var x = Enumerable.Range(1, 4).ToList().PermutationsUpToN(3).Select(l => l.ToList()).ToList();


            
            //long numPermutations = Math.
            //foreach (var participantsList in db.Pals
            //    .ToList()
            //    .UnorderedPermutations(MAX_PARTICIPANTS)
            //    .Select(l => l.ToList())
            //    .Where(l => l.Count >= 2)
            //    .ToList())
            //{

            //}




            /*
             * Once available paths have been determined, look for all parent pairs which lead to the target
             * pal within the breeding limit. All relevant participants are collected from general participants
             * and children. Begin multi-root iteration:
             * 
             * 1. 
             */
        }
    }
}
