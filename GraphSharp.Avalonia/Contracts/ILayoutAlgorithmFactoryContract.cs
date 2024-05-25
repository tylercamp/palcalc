using System.Collections.Generic;
using System.Diagnostics.Contracts;
using GraphSharp.Algorithms.Layout;
using QuickGraph;
using System.Linq;

namespace GraphSharp.Contracts
{
    [ContractClassFor( typeof( ILayoutAlgorithmFactory<,,> ) )]
    public class ILayoutAlgorithmFactoryContract<TVertex, TEdge, TGraph> : ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>
        where TVertex : class
        where TEdge : IEdge<TVertex>
        where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
    {
        #region ILayoutAlgorithmFactory<TVertex,TEdge,TGraph> Members

        IEnumerable<string> ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>.AlgorithmTypes
        {
            get
            {
                Contract.Ensures( Contract.Result<IEnumerable<string>>() != null );

                return default( IEnumerable<string> );
            }
        }

        ILayoutAlgorithm<TVertex, TEdge, TGraph> ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>.CreateAlgorithm( string newAlgorithmType, ILayoutContext<TVertex, TEdge, TGraph> context, ILayoutParameters parameters )
        {
            Contract.Requires( newAlgorithmType != null );
            var laf = (ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>)this;
            Contract.Requires( laf.AlgorithmTypes.Contains( newAlgorithmType ) );
            Contract.Requires( context.Sizes != null );

            return default( ILayoutAlgorithm<TVertex, TEdge, TGraph> );
        }

        ILayoutParameters ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>.CreateParameters( string algorithmType, ILayoutParameters oldParameters )
        {
            Contract.Requires( algorithmType != null );
            var laf = (ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>)this;
            Contract.Requires( laf.AlgorithmTypes.Contains( algorithmType ) );

            return default( ILayoutParameters );
        }

        bool ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>.IsValidAlgorithm( string algorithmType )
        {
            Contract.Requires( algorithmType != null );
            var laf = (ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>)this;
            Contract.Ensures( Contract.Result<bool>() == laf.AlgorithmTypes.Contains( algorithmType ) );

            return false;
        }

        string ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>.GetAlgorithmType( ILayoutAlgorithm<TVertex, TEdge, TGraph> algorithm )
        {
            Contract.Requires( algorithm != null );
            var laf = (ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>)this;
            Contract.Ensures( Contract.Result<string>() == null || laf.AlgorithmTypes.Contains( Contract.Result<string>() ) );

            return default( string );
        }

        bool ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>.NeedEdgeRouting( string algorithmType )
        {
            return default( bool );
        }

        bool ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>.NeedOverlapRemoval( string algorithmType )
        {
            return default( bool );
        }

        #endregion
    }
}
