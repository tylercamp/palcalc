using System.Collections.Generic;
using GraphSharp.Algorithms.Layout;
using QuickGraph;

namespace GraphSharp.Algorithms.EdgeRouting
{
	public interface IEdgeRoutingAlgorithmFactory<TVertex, TEdge, TGraph>
		where TVertex : class
		where TEdge : IEdge<TVertex>
		where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
	{
		/// <summary>
		/// List of the available algorithms.
		/// </summary>
		IEnumerable<string> AlgorithmTypes { get; }

		IEdgeRoutingAlgorithm<TVertex, TEdge, TGraph> CreateAlgorithm( string newAlgorithmType, ILayoutContext<TVertex, TEdge, TGraph> context, IEdgeRoutingParameters parameters);

		IEdgeRoutingParameters CreateParameters( string algorithmType, IEdgeRoutingParameters oldParameters );

		bool IsValidAlgorithm( string algorithmType );

		string GetAlgorithmType( IEdgeRoutingAlgorithm<TVertex, TEdge, TGraph> algorithm );
	}
}