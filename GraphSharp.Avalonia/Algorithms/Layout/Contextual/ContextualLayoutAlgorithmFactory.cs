using System.Collections.Generic;
using System.Linq;
using QuickGraph;
using GraphSharp.Algorithms.Layout.Simple.Tree;

namespace GraphSharp.Algorithms.Layout.Contextual
{
	public class ContextualLayoutAlgorithmFactory<TVertex, TEdge, TGraph> : IContextualLayoutAlgorithmFactory<TVertex, TEdge, TGraph>
		where TVertex : class
		where TEdge : IEdge<TVertex>
		where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
	{
		private readonly string[] algorithmTypes = new[] { "DoubleTree", "BalloonTree" };

		public IEnumerable<string> AlgorithmTypes
		{
			get { return algorithmTypes; }
		}

		public ILayoutAlgorithm<TVertex, TEdge, TGraph> CreateAlgorithm( string newAlgorithmType, ILayoutContext<TVertex, TEdge, TGraph> context, ILayoutParameters parameters )
		{
		    var layoutContext = context as ContextualLayoutContext<TVertex, TEdge, TGraph>;

			switch ( newAlgorithmType )
			{
				case "DoubleTree":
					return new DoubleTreeLayoutAlgorithm<TVertex, TEdge, TGraph>( layoutContext.Graph, layoutContext.Positions, layoutContext.Sizes, parameters as DoubleTreeLayoutParameters, layoutContext.SelectedVertex );
				case "BalloonTree":
					return new BalloonTreeLayoutAlgorithm<TVertex, TEdge, TGraph>( layoutContext.Graph, layoutContext.Positions, layoutContext.Sizes, parameters as BalloonTreeLayoutParameters, layoutContext.SelectedVertex );
				default:
					return null;
			}
		}

		public ILayoutParameters CreateParameters( string algorithmType, ILayoutParameters oldParameters )
		{
			switch ( algorithmType )
			{
				case "DoubleTree":
					return !( oldParameters is DoubleTreeLayoutParameters ) ? new DoubleTreeLayoutParameters() : (DoubleTreeLayoutParameters)( oldParameters as DoubleTreeLayoutParameters ).Clone();
				case "BaloonTree":
					return !( oldParameters is BalloonTreeLayoutParameters ) ? new BalloonTreeLayoutParameters() : (BalloonTreeLayoutParameters)( oldParameters as BalloonTreeLayoutParameters ).Clone();
				default:
					return null;
			}
		}

		public string GetAlgorithmType( ILayoutAlgorithm<TVertex, TEdge, TGraph> algorithm )
		{
			if ( algorithm is DoubleTreeLayoutAlgorithm<TVertex, TEdge, TGraph> )
				return "DoubleTree";
		
            if ( algorithm is BalloonTreeLayoutAlgorithm<TVertex, TEdge, TGraph> )
		        return "BalloonTree";
		    
            return string.Empty;
		}

		public bool IsValidAlgorithm( string algorithmType )
		{
			return ( AlgorithmTypes.Contains( algorithmType ) );
		}

		public bool NeedEdgeRouting( string algorithmType )
		{
			return true;
		}

		public bool NeedOverlapRemoval( string algorithmType )
		{
			return false;
		}
	}
}