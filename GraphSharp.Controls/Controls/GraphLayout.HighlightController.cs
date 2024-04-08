using System.Collections.Generic;
using System.Linq;
using GraphSharp.Algorithms.Highlight;
using QuickGraph;

namespace GraphSharp.Controls
{
	public partial class GraphLayout<TVertex, TEdge, TGraph> : IHighlightController<TVertex, TEdge, TGraph>
		where TVertex : class
		where TEdge : IEdge<TVertex>
		where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
	{
		#region IHighlightController<TVertex,TEdge,TGraph> Members
		private readonly IDictionary<TVertex, object> _highlightedVertices = new Dictionary<TVertex, object>();
		private readonly IDictionary<TVertex, object> _semiHighlightedVertices = new Dictionary<TVertex, object>();
		private readonly IDictionary<TEdge, object> _highlightedEdges = new Dictionary<TEdge, object>();
		private readonly IDictionary<TEdge, object> _semiHighlightedEdges = new Dictionary<TEdge, object>();

		public IEnumerable<TVertex> HighlightedVertices
		{
			get { return _highlightedVertices.Keys.ToArray(); }
		}

		public IEnumerable<TVertex> SemiHighlightedVertices
		{
			get { return _semiHighlightedVertices.Keys.ToArray(); }
		}

		public IEnumerable<TEdge> HighlightedEdges
		{
			get { return _highlightedEdges.Keys.ToArray(); }
		}

		public IEnumerable<TEdge> SemiHighlightedEdges
		{
			get { return _semiHighlightedEdges.Keys.ToArray(); }
		}

		public bool IsHighlightedVertex( TVertex vertex )
		{
			return _highlightedVertices.ContainsKey( vertex );
		}

		public bool IsHighlightedVertex( TVertex vertex, out object highlightInfo )
		{
			return _highlightedVertices.TryGetValue( vertex, out highlightInfo );
		}

		public bool IsSemiHighlightedVertex( TVertex vertex )
		{
			return _semiHighlightedVertices.ContainsKey( vertex );
		}

		public bool IsSemiHighlightedVertex( TVertex vertex, out object semiHighlightInfo )
		{
			return _semiHighlightedVertices.TryGetValue( vertex, out semiHighlightInfo );
		}

		public bool IsHighlightedEdge( TEdge edge )
		{
			return _highlightedEdges.ContainsKey( edge );
		}

		public bool IsHighlightedEdge( TEdge edge, out object highlightInfo )
		{
			return _highlightedEdges.TryGetValue( edge, out highlightInfo );
		}

		public bool IsSemiHighlightedEdge( TEdge edge )
		{
			return _semiHighlightedEdges.ContainsKey( edge );
		}

		public bool IsSemiHighlightedEdge( TEdge edge, out object semiHighlightInfo )
		{
			return _semiHighlightedEdges.TryGetValue( edge, out semiHighlightInfo );
		}

		public void HighlightVertex( TVertex vertex, object highlightInfo )
		{
			_highlightedVertices[vertex] = highlightInfo;
			VertexControl vc;
			if ( VertexControls.TryGetValue( vertex, out vc ) )
			{
				GraphElementBehaviour.SetIsHighlighted( vc, true );
				GraphElementBehaviour.SetHighlightInfo( vc, highlightInfo );
			}
		}

		public void SemiHighlightVertex( TVertex vertex, object semiHighlightInfo )
		{
			_semiHighlightedVertices[vertex] = semiHighlightInfo;
			VertexControl vc;
			if ( VertexControls.TryGetValue( vertex, out vc ) )
			{
				GraphElementBehaviour.SetIsSemiHighlighted( vc, true );
				GraphElementBehaviour.SetSemiHighlightInfo( vc, semiHighlightInfo );
			}
		}

		public void HighlightEdge( TEdge edge, object highlightInfo )
		{
			_highlightedEdges[edge] = highlightInfo;
			EdgeControl ec;
			if ( EdgeControls.TryGetValue( edge, out ec ) )
			{
				GraphElementBehaviour.SetIsHighlighted( ec, true );
				GraphElementBehaviour.SetHighlightInfo( ec, highlightInfo );
			}
		}

		public void SemiHighlightEdge( TEdge edge, object semiHighlightInfo )
		{
			_semiHighlightedEdges[edge] = semiHighlightInfo;
			EdgeControl ec;
			if ( EdgeControls.TryGetValue( edge, out ec ) )
			{
				GraphElementBehaviour.SetIsSemiHighlighted( ec, true );
				GraphElementBehaviour.SetSemiHighlightInfo( ec, semiHighlightInfo );
			}
		}

		public void RemoveHighlightFromVertex( TVertex vertex )
		{
			_highlightedVertices.Remove( vertex );
			VertexControl vc;
			if ( VertexControls.TryGetValue( vertex, out vc ) )
			{
				GraphElementBehaviour.SetIsHighlighted( vc, false );
				GraphElementBehaviour.SetHighlightInfo( vc, null );
			}
		}

		public void RemoveSemiHighlightFromVertex( TVertex vertex )
		{
			_semiHighlightedVertices.Remove( vertex );
			VertexControl vc;
			if ( VertexControls.TryGetValue( vertex, out vc ) )
			{
				GraphElementBehaviour.SetIsSemiHighlighted( vc, false );
				GraphElementBehaviour.SetSemiHighlightInfo( vc, null );
			}
		}

		public void RemoveHighlightFromEdge( TEdge edge )
		{
			_highlightedEdges.Remove( edge );
			EdgeControl ec;
			if ( EdgeControls.TryGetValue( edge, out ec ) )
			{
				GraphElementBehaviour.SetIsHighlighted( ec, false );
				GraphElementBehaviour.SetHighlightInfo( ec, null );
			}
		}

		public void RemoveSemiHighlightFromEdge( TEdge edge )
		{
			_semiHighlightedEdges.Remove( edge );
			EdgeControl ec;
			if ( EdgeControls.TryGetValue( edge, out ec ) )
			{
				GraphElementBehaviour.SetIsSemiHighlighted( ec, false );
				GraphElementBehaviour.SetSemiHighlightInfo( ec, null );
			}
		}

		#endregion

		protected void SetHighlightProperties(TVertex vertex, VertexControl presenter)
		{
			object highlightInfo;
			if ( IsHighlightedVertex( vertex, out highlightInfo ) )
			{
				GraphElementBehaviour.SetIsHighlighted( presenter, true );
				GraphElementBehaviour.SetHighlightInfo( presenter, highlightInfo );
			}

			object semiHighlightInfo;
			if ( IsSemiHighlightedVertex( vertex, out semiHighlightInfo ) )
			{
				GraphElementBehaviour.SetIsSemiHighlighted( presenter, true );
				GraphElementBehaviour.SetSemiHighlightInfo( presenter, semiHighlightInfo );
			}
		}

	    protected void SetHighlightProperties( TEdge edge, EdgeControl edgeControl )
		{
			object highlightInfo;
			if (IsHighlightedEdge(edge, out highlightInfo))
			{
				GraphElementBehaviour.SetIsHighlighted( edgeControl, true );
				GraphElementBehaviour.SetHighlightInfo( edgeControl, highlightInfo );
			}

			object semiHighlightInfo;
			if ( IsSemiHighlightedEdge( edge, out semiHighlightInfo ) )
			{
				GraphElementBehaviour.SetIsSemiHighlighted( edgeControl, true );
				GraphElementBehaviour.SetSemiHighlightInfo( edgeControl, semiHighlightInfo );
			}
		}
	}
}