using QuickGraph;

namespace GraphSharp.Algorithms.Highlight
{
	public interface IHighlightContext<TVertex, TEdge, TGraph>
		where TEdge : IEdge<TVertex>
		where TGraph : IVertexAndEdgeListGraph<TVertex, TEdge>
	{
		TGraph Graph { get; }
	}
}