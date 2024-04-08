using System.Collections.Generic;
using QuickGraph;

namespace GraphSharp.Algorithms.Layout
{
    public interface ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>
        where TVertex : class
        where TEdge : IEdge<TVertex>
        where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
    {
        IEnumerable<string> AlgorithmTypes { get; }

        /// <summary>
        /// Creates a new algorithm specified by the newAlgorithmType parameter.
        /// </summary>
        /// <param name="newAlgorithmType">The identifier of the new layout algorithm.</param>
        /// <param name="context"></param>
        /// <param name="parameters"></param>
        /// <returns>The new layout algorithm object.</returns>
        ILayoutAlgorithm<TVertex, TEdge, TGraph> CreateAlgorithm( string newAlgorithmType, ILayoutContext<TVertex, TEdge, TGraph> context, ILayoutParameters parameters );

        ILayoutParameters CreateParameters( string algorithmType, ILayoutParameters oldParameters );

        bool IsValidAlgorithm( string algorithmType );

        /// <summary>
        /// Gets the type of the algorithm (usually the name of the algorithm which identifies the algorithm).
        /// </summary>
        /// <param name="algorithm">The layout algorithm object.</param>
        /// <returns>The identified of the algorithm.</returns>
        string GetAlgorithmType( ILayoutAlgorithm<TVertex, TEdge, TGraph> algorithm );

        bool NeedEdgeRouting( string algorithmType );

        bool NeedOverlapRemoval( string algorithmType );
    }
}