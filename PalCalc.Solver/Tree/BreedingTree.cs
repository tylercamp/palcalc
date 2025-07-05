using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tree
{
    public class BreedingTree
    {
        IBreedingTreeNode BuildNode(IPalReference pref)
        {
            switch (pref)
            {
                case BredPalReference bpr:
                    return new BredPalNode(bpr, BuildNode(bpr.Parent1), BuildNode(bpr.Parent2));

                case WildPalReference:
                case OwnedPalReference:
                    return new DirectPalNode(pref);

                case CompositeOwnedPalReference copr:
                    return new CompositePalNode(copr);

                case SurgeryTablePalReference stpr:
                    return new SurgeryResultNode(stpr, BuildNode(stpr.Input));

                default: throw new NotImplementedException();
            }
        }

        public BreedingTree(IPalReference finalPal)
        {
            Root = BuildNode(finalPal);
        }

        public IBreedingTreeNode Root { get; }

        public IEnumerable<(IBreedingTreeNode, int)> AllNodes => Root.TraversedTopDown(0);

        public void Print()
        {
            /*
             * Node
             * Description
             *       \
             *              Node
             *              Description
             *       /
             * Node
             * Description
             */

            var maxDescriptionLengthByDepth = AllNodes.GroupBy(n => n.Item2).ToDictionary(g => g.Key, g => g.Max(p => p.Item1.DescriptionLines.Max(l => l.Length)));
            var maxDepth = maxDescriptionLengthByDepth.Keys.Max();

            var indentationByDepth = Enumerable
                .Range(0, maxDepth + 1)
                .ToDictionary(
                    depth => depth,
                    depth =>
                    {
                        if (depth == maxDepth) return 0;

                        var priorDepthsLengths = Enumerable.Range(1, maxDepth - depth).Select(depthOffset => 1 + maxDescriptionLengthByDepth[depth + depthOffset]).ToList();
                        return priorDepthsLengths.Sum();
                    }
                );

            int? prevDepth = null;
            foreach (var (node, depth) in AllNodes)
            {
                var indentation = new string(' ', indentationByDepth[depth]);

                if (prevDepth != null)
                {
                    if (prevDepth > depth)
                        Console.WriteLine("{0}\\", indentation);
                    else if (prevDepth < depth)
                        Console.WriteLine("{0}/", new string(' ', indentationByDepth[prevDepth.Value]));
                }

                foreach (var line in node.DescriptionLines)
                    Console.WriteLine("{0} {1}", indentation, line);

                prevDepth = depth;
            }
        }
    }
}
