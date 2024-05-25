using QuickGraph;

namespace GraphSharp.Algorithms.Highlight
{
	public class SimpleHighlightAlgorithm<TVertex, TEdge, TGraph> : HighlightAlgorithmBase<TVertex, TEdge, TGraph, IHighlightParameters>
		where TVertex : class
		where TEdge : IEdge<TVertex>
		where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
	{
		public SimpleHighlightAlgorithm(
			IHighlightController<TVertex, TEdge, TGraph> controller,
			IHighlightParameters parameters )
			: base( controller, parameters )
		{
		}

		private void ClearSemiHighlights()
		{
			foreach ( var vertex in Controller.SemiHighlightedVertices )
				Controller.RemoveSemiHighlightFromVertex( vertex );

			foreach ( var edge in Controller.SemiHighlightedEdges )
				Controller.RemoveSemiHighlightFromEdge( edge );
		}

		private void ClearAllHighlights()
		{
			ClearSemiHighlights();

			foreach ( var vertex in Controller.HighlightedVertices )
				Controller.RemoveHighlightFromVertex( vertex );

			foreach ( var edge in Controller.HighlightedEdges )
				Controller.RemoveHighlightFromEdge( edge );
		}

		/// <summary>
		/// Resets the semi-highlights according to the actually
		/// highlighted vertices/edges.
		/// 
		/// This method should be called if the graph changed, 
		/// or the highlights should be resetted.
		/// </summary>
		public override void ResetHighlight()
		{
			ClearAllHighlights();
		}

		public override bool OnVertexHighlighting( TVertex vertex )
		{
			ClearAllHighlights();

			if ( vertex == null || !Controller.Graph.ContainsVertex( vertex ) )
				return false;

			//semi-highlight the in-edges, and the neighbours on their other side
			foreach ( var edge in Controller.Graph.InEdges( vertex ) )
			{
				Controller.SemiHighlightEdge( edge, "InEdge" );
				if ( edge.Source == vertex || Controller.IsHighlightedVertex( edge.Source ) )
					continue;

				Controller.SemiHighlightVertex( edge.Source, "Source" );
			}

			//semi-highlight the out-edges
			foreach ( var edge in Controller.Graph.OutEdges( vertex ) )
			{
				Controller.SemiHighlightEdge( edge, "OutEdge" );
				if ( edge.Target == vertex || Controller.IsHighlightedVertex( edge.Target ) )
					continue;

				Controller.SemiHighlightVertex( edge.Target, "Target" );
			}
			Controller.HighlightVertex( vertex, "None" );
			return true;
		}

		public override bool OnVertexHighlightRemoving( TVertex vertex )
		{
			ClearAllHighlights();
			return true;
		}

		public override bool OnEdgeHighlighting( TEdge edge )
		{
			ClearAllHighlights();

			//highlight the source and the target
			if ( Equals( edge, default( TEdge ) ) || !Controller.Graph.ContainsEdge( edge ) )
				return false;

			Controller.HighlightEdge( edge, null );
			Controller.SemiHighlightVertex( edge.Source, "Source" );
			Controller.SemiHighlightVertex( edge.Target, "Target" );
			return true;
		}

		public override bool OnEdgeHighlightRemoving( TEdge edge )
		{
			ClearAllHighlights();
			return true;
		}
	}
}