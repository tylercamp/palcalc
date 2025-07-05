using GraphSharp.Algorithms;
using GraphSharp.Algorithms.Layout;
using PalCalc.Solver;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.GraphSharp
{
    internal class SpecificLayoutAlgorithmFactory : ILayoutAlgorithmFactory<IBreedingTreeNodeViewModel, BreedingEdge, BreedingGraph>
    {
        public SpecificLayoutAlgorithmFactory()
        {
            AlgorithmTypes = new List<string>() { "Specific" };
        }

        public IEnumerable<string> AlgorithmTypes { get; private set; }

        public ILayoutAlgorithm<IBreedingTreeNodeViewModel, BreedingEdge, BreedingGraph> CreateAlgorithm(string newAlgorithmType, ILayoutContext<IBreedingTreeNodeViewModel, BreedingEdge, BreedingGraph> context, ILayoutParameters parameters)
        {
            return new BreedingTreeLayoutAlgorithm(
                context.Graph,
                context.Positions,
                context.Sizes,
                parameters as BreedingTreeLayoutAlgorithmParameters
            );
        }

        public ILayoutParameters CreateParameters(string algorithmType, ILayoutParameters oldParameters)
        {
            return oldParameters.CreateNewParameter<BreedingTreeLayoutAlgorithmParameters>();
        }

        public string GetAlgorithmType(ILayoutAlgorithm<IBreedingTreeNodeViewModel, BreedingEdge, BreedingGraph> algorithm)
        {
            return "Custom";
        }

        public bool IsValidAlgorithm(string algorithmType) => true;

        public bool NeedEdgeRouting(string algorithmType) => true;

        public bool NeedOverlapRemoval(string algorithmType) => false;
    }
}
