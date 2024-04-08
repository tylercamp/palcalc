using System.Collections.Generic;
using QuickGraph;

namespace GraphSharp.Algorithms.Highlight
{
	public interface IHighlightAlgorithmFactory<TVertex, TEdge, TGraph>
		where TVertex : class
		where TEdge : IEdge<TVertex>
		where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
	{
		IEnumerable<string> HighlightModes { get; }
		bool IsValidMode( string mode );

		IHighlightAlgorithm<TVertex, TEdge, TGraph> CreateAlgorithm(
			string highlightMode,
			IHighlightContext<TVertex, TEdge, TGraph> context,
			IHighlightController<TVertex, TEdge, TGraph> controller,
			IHighlightParameters parameters );

		IHighlightParameters CreateParameters( string highlightMode, IHighlightParameters oldParameters );

		string GetHighlightMode( IHighlightAlgorithm<TVertex, TEdge, TGraph> algorithm );
	}
}