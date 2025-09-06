using GraphSharp.Algorithms.Layout;
using GraphSharp.Algorithms.Layout.Simple.Tree;
using GraphSharp.Controls;
using PalCalc.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.GraphSharp
{
    public class BreedingGraphLayout : GraphLayout<IBreedingTreeNodeViewModel, BreedingEdge, BreedingGraph>
    {
        public BreedingGraphLayout()
        {
            LayoutAlgorithmType = "Tree";
            LayoutParameters = new SimpleTreeLayoutParameters()
            {
                Direction = LayoutDirection.TopToBottom,
                SpanningTreeGeneration = SpanningTreeGeneration.DFS,
                WidthPerHeight = 100
            };

            LayoutAlgorithmFactory = new SpecificLayoutAlgorithmFactory();
        }
    }
}
