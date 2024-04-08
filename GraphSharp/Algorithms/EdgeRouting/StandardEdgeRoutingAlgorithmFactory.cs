using System.Collections.Generic;
using System.Linq;
using GraphSharp.Algorithms.Layout;
using QuickGraph;

namespace GraphSharp.Algorithms.EdgeRouting
{
    public class StandardEdgeRoutingAlgorithmFactory<TVertex, TEdge, TGraph> : IEdgeRoutingAlgorithmFactory<TVertex, TEdge, TGraph>
            where TVertex : class
            where TEdge : IEdge<TVertex>
            where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
    {
        protected static readonly string[] algorithmTypes = {};

        public IEnumerable<string> AlgorithmTypes
        {
            get { return algorithmTypes; }
        }

        public IEdgeRoutingAlgorithm<TVertex, TEdge, TGraph> CreateAlgorithm(string newAlgorithmType, ILayoutContext<TVertex, TEdge, TGraph> context,
                                                                             IEdgeRoutingParameters parameters)
        {
            return null;
        }

        public IEdgeRoutingParameters CreateParameters(string algorithmType, IEdgeRoutingParameters oldParameters)
        {
            return null;
        }

        public bool IsValidAlgorithm(string algorithmType)
        {
            return AlgorithmTypes.Any(at => at == algorithmType);
        }

        public string GetAlgorithmType(IEdgeRoutingAlgorithm<TVertex, TEdge, TGraph> algorithm)
        {
            return string.Empty;
        }
    }
}