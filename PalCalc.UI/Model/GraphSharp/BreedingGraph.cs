using GraphSharp;
using PalCalc.Solver;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model
{
    public class BreedingEdge : TypedEdge<IBreedingTreeNode>
    {
        public BreedingEdge(IBreedingTreeNode parent, IBreedingTreeNode child) : base(parent, child, EdgeTypes.Hierarchical) { }

        public override string ToString() => "";
    }

    public class BreedingGraph : HierarchicalGraph<IBreedingTreeNode, BreedingEdge>
    {
        public BreedingGraph(BreedingTree tree)
        {
            Tree = tree;
        }

        public BreedingTree Tree { get; private set; }

        public static BreedingGraph FromPalReference(IPalReference palRef)
        {
            var tree = new BreedingTree(palRef);
            var result = new BreedingGraph(tree);

            result.AddVertexRange(tree.AllNodes.Select(p => p.Item1));

            // breeding tree is upside down relative to breeding direction
            foreach (var (child, _) in tree.AllNodes)
                foreach (var parent in child.Children)
                    result.AddEdge(new BreedingEdge(parent: parent, child: child));

            return result;
            
        }
    }
}
