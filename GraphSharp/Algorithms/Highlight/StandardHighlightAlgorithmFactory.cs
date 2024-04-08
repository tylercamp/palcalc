using System.Collections.Generic;
using System.Linq;
using QuickGraph;

namespace GraphSharp.Algorithms.Highlight
{
	public class StandardHighlightAlgorithmFactory<TVertex, TEdge, TGraph> : IHighlightAlgorithmFactory<TVertex, TEdge, TGraph>
		where TVertex : class
		where TEdge : IEdge<TVertex>
		where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
	{
		private static readonly string[] highlightModes = { "Simple" };

		public IEnumerable<string> HighlightModes
		{
			get { return highlightModes; }
		}

		public bool IsValidMode( string mode )
		{
			return string.IsNullOrEmpty( mode ) || highlightModes.Contains( mode );
		}

		public IHighlightAlgorithm<TVertex, TEdge, TGraph> CreateAlgorithm(
			string highlightMode,
			IHighlightContext<TVertex, TEdge, TGraph> context,
			IHighlightController<TVertex, TEdge, TGraph> controller,
			IHighlightParameters parameters )
		{
			switch (highlightMode)
			{
				case "Simple":
					return new SimpleHighlightAlgorithm<TVertex, TEdge, TGraph>(controller, parameters);
				default:
					return null;
			}
		}

		public IHighlightParameters CreateParameters( string highlightMode, IHighlightParameters oldParameters )
		{
			switch (highlightMode)
			{
				case "Simple":
					return new HighlightParameterBase();
				default:
					return new HighlightParameterBase();
			}
		}

		public string GetHighlightMode( IHighlightAlgorithm<TVertex, TEdge, TGraph> algorithm )
		{
			if ( algorithm is SimpleHighlightAlgorithm<TVertex, TEdge, TGraph> )
				return "Simple";

			return null;
		}
	}
}