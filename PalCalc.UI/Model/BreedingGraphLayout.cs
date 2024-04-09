using GraphSharp.Algorithms.Layout.Simple.Tree;
using GraphSharp.Controls;
using PalCalc.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model
{
    internal class BreedingGraphLayout : GraphLayout<IBreedingTreeNode, BreedingEdge, BreedingGraph>
    {
        public BreedingGraphLayout()
        {
            LayoutAlgorithmType = "Tree";
            LayoutParameters = new SimpleTreeLayoutParameters()
            {
                Direction = GraphSharp.Algorithms.Layout.LayoutDirection.TopToBottom,
                SpanningTreeGeneration = SpanningTreeGeneration.DFS,
                WidthPerHeight = 100
            };

            LayoutAlgorithmFactory = new SpecificLayoutAlgorithmFactory();
        }
    }
}
