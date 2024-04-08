using System.Collections.Generic;
using QuickGraph.Algorithms;
using QuickGraph;
using System.Windows;

namespace GraphSharp.Algorithms.Layout
{
	public delegate void LayoutIterationEndedEventHandler<TVertex, TEdge>( object sender, ILayoutIterationEventArgs<TVertex> e )
		where TVertex : class
		where TEdge : IEdge<TVertex>;

	public delegate void LayoutIterationEndedEventHandler<TVertex, TEdge, TVertexInfo, TEdgeInfo>( object sender, ILayoutInfoIterationEventArgs<TVertex, TEdge, TVertexInfo, TEdgeInfo> e )
		where TVertex : class
		where TEdge : IEdge<TVertex>;

    /// <summary>
    /// Reports the progress of the layout.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="percent">The status of the progress in percent.</param>
    public delegate void ProgressChangedEventHandler(object sender, double percent);

	public interface ILayoutAlgorithm<TVertex, TEdge, TGraph, TVertexInfo, TEdgeInfo> : ILayoutAlgorithm<TVertex, TEdge, TGraph>
		where TVertex : class
		where TEdge : IEdge<TVertex>
		where TGraph : IVertexAndEdgeListGraph<TVertex, TEdge>
	{
		/// <summary>
		/// Extra informations, calculated by the layout
		/// </summary>
		IDictionary<TVertex, TVertexInfo> VertexInfos { get; }

		/// <summary>
		/// Extra informations, calculated by the layout
		/// </summary>
		IDictionary<TEdge, TEdgeInfo> EdgeInfos { get; }

		new event LayoutIterationEndedEventHandler<TVertex, TEdge, TVertexInfo, TEdgeInfo> IterationEnded;
	}

	public interface ILayoutAlgorithm<TVertex, TEdge, TGraph> : IAlgorithm<TGraph>
		where TVertex : class
		where TEdge : IEdge<TVertex>
		where TGraph : IVertexAndEdgeListGraph<TVertex, TEdge>
	{
		IDictionary<TVertex, Point> VertexPositions { get; }

		/// <summary>
		/// Returns with the extra layout information of the vertex (or null).
		/// </summary>
		object GetVertexInfo( TVertex vertex );

		/// <summary>
		/// Returns with the extra layout information of the edge (or null).
		/// </summary>
		object GetEdgeInfo( TEdge edge );

		event LayoutIterationEndedEventHandler<TVertex, TEdge> IterationEnded;

	    event ProgressChangedEventHandler ProgressChanged;
	}
}