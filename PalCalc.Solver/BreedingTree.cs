using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public interface IBreedingTreeNode
    {
        IPalReference PalRef { get; }
        IEnumerable<IBreedingTreeNode> Children { get; }

        IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth);

        IEnumerable<string> DescriptionLines { get; }
        string Description { get; }
    }

    public class DirectPalNode : IBreedingTreeNode
    {
        public DirectPalNode(IPalReference pref)
        {
            PalRef = pref;
        }

        public string Description => string.Join('\n', DescriptionLines);

        public IPalReference PalRef { get; }
        public IEnumerable<IBreedingTreeNode> Children { get; } = Enumerable.Empty<IBreedingTreeNode>();

        public IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth)
        {
            yield return (this, currentDepth);
        }

        public IEnumerable<String> DescriptionLines
        {
            get
            {
                switch (PalRef)
                {
                    case WildPalReference wild:
                        yield return $"Wild {wild.Pal.Name}";
                        yield return $"{wild.Gender} gender w/ up to {wild.EffectiveTraits.Count} random traits";
                        break;

                    case OwnedPalReference owned:
                        yield return $"Owned {owned.Pal.Name}";
                        yield return $"in {owned.Location}";
                        yield return $"{owned.Gender} w/ {owned.EffectiveTraits.TraitsListToString()}";
                        break;

                    default: throw new NotImplementedException();
                }
            }
        }
    }

    public class CompositePalNode : IBreedingTreeNode
    {
        public CompositePalNode(CompositeOwnedPalReference compositeRef)
        {
            Male = compositeRef.Male;
            Female = compositeRef.Female;

            PalRef = compositeRef;
        }

        public OwnedPalReference Male { get; }
        public OwnedPalReference Female { get; }

        public IPalReference PalRef { get; }

        public IEnumerable<IBreedingTreeNode> Children { get; } = Enumerable.Empty<IBreedingTreeNode>();

        public IEnumerable<string> DescriptionLines => throw new NotImplementedException();

        public string Description => throw new NotImplementedException();

        public IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth)
        {
            yield return (this, currentDepth);
        }
    }

    public class BredPalNode : IBreedingTreeNode
    {
        public BredPalNode(BredPalReference bpr, IBreedingTreeNode parentNode1, IBreedingTreeNode parentNode2)
        {
            PalRef = bpr;
            Children = new List<IBreedingTreeNode>() { parentNode1, parentNode2 };

            ParentNode1 = parentNode1;
            ParentNode2 = parentNode2;
        }

        public string Description => string.Join('\n', DescriptionLines);

        public IBreedingTreeNode ParentNode1 { get; }
        public IBreedingTreeNode ParentNode2 { get; }

        public IPalReference PalRef { get; }

        public IEnumerable<IBreedingTreeNode> Children { get; }

        public IEnumerable<(IBreedingTreeNode, int)> TraversedTopDown(int currentDepth)
        {
            foreach (var n in ParentNode1.TraversedTopDown(currentDepth + 1))
                yield return n;

            yield return (this, currentDepth);

            foreach (var n in ParentNode2.TraversedTopDown(currentDepth + 1))
                yield return n;
        }

        public IEnumerable<string> DescriptionLines
        {
            get
            {
                var asBred = PalRef as BredPalReference;
                yield return $"Bred {asBred.Pal.Name}";
                yield return $"{asBred.Gender} gender w/ {asBred.EffectiveTraits.TraitsListToString()}";
                yield return $"takes ~{asBred.SelfBreedingEffort} for {asBred.AvgRequiredBreedings} breed attempts";
            }
        }
    }

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
