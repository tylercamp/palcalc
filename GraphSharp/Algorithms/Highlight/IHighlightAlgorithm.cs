using QuickGraph;

namespace GraphSharp.Algorithms.Highlight
{
	public interface IHighlightAlgorithm<TVertex, TEdge, TGraph>
		where TVertex : class
		where TEdge : IEdge<TVertex>
		where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
	{
		IHighlightParameters Parameters { get; }

		/// <summary>
		/// Reset the whole highlight.
		/// </summary>
		void ResetHighlight();

		bool OnVertexHighlighting( TVertex vertex );
		bool OnVertexHighlightRemoving( TVertex vertex );
		bool OnEdgeHighlighting( TEdge edge );
		bool OnEdgeHighlightRemoving( TEdge edge );

		bool IsParametersSettable(IHighlightParameters parameters);
		bool TrySetParameters(IHighlightParameters parameters);
	}
}