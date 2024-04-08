using System.Diagnostics.Contracts;
using System.Linq;
using QuickGraph;
using GraphSharp.Algorithms.Layout;
using GraphSharp.Algorithms.Layout.Contextual;

namespace GraphSharp.Contracts
{
    [ContractClassFor( typeof( IContextualLayoutAlgorithmFactory<,,> ) )]
    public class IContextualLayoutAlgorithmFactoryContract<TVertex, TEdge, TGraph> : ILayoutAlgorithmFactoryContract<TVertex, TEdge, TGraph>, IContextualLayoutAlgorithmFactory<TVertex, TEdge, TGraph>
        where TVertex : class
        where TEdge : IEdge<TVertex>
        where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
    {

        #region ILayoutAlgorithmFactory<TVertex,TEdge,TGraph> Members

        ILayoutAlgorithm<TVertex, TEdge, TGraph> ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>.CreateAlgorithm( string newAlgorithmType, ILayoutContext<TVertex, TEdge, TGraph> context, ILayoutParameters parameters )
        {
            Contract.Requires( newAlgorithmType != null );
            Contract.Requires( context as ContextualLayoutContext<TVertex, TEdge, TGraph> != null );

            var laf = (ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>)this;
            Contract.Requires( laf.AlgorithmTypes.Contains( newAlgorithmType ) );

            return default( ILayoutAlgorithm<TVertex, TEdge, TGraph> );
        }

        #endregion
    }
}
