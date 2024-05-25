using QuickGraph;

namespace GraphSharp.Algorithms.Highlight
{
	public class HighlightContext<TVertex, TEdge, TGraph> : IHighlightContext<TVertex, TEdge, TGraph>
		where TVertex : class 
		where TEdge : IEdge<TVertex>
		where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
	{
		public TGraph Graph
		{
			get; private set;
		}

		public HighlightContext(TGraph graph)
		{
			this.Graph = graph;
		}
	}
}