using System.Collections.Generic;
using System.Diagnostics.Contracts;
using GraphSharp.Algorithms.Layout;
using QuickGraph;
using System.Windows;

namespace GraphSharp.Contracts
{
    [ContractClassFor( typeof( ILayoutContext<,,> ) )]
    public class ILayoutContextContract<TVertex, TEdge, TGraph> : ILayoutContext<TVertex, TEdge, TGraph>
        where TEdge : IEdge<TVertex>
        where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
    {
        [ContractInvariantMethod]
        protected void Invariants()
        {
            var lc = (ILayoutContext<TVertex, TEdge, TGraph>)this;
            Contract.Invariant( lc.Positions != null );
            Contract.Invariant( lc.Graph != null );
            Contract.Invariant( lc.Sizes != null );
        }

        #region ILayoutContext<TVertex,TEdge,TGraph> Members

        IDictionary<TVertex, Point> ILayoutContext<TVertex, TEdge, TGraph>.Positions
        {
            get { return default( IDictionary<TVertex, Point> ); }
        }

        IDictionary<TVertex, Size> ILayoutContext<TVertex, TEdge, TGraph>.Sizes
        {
            get { return default( IDictionary<TVertex, Size> ); }
        }

        TGraph ILayoutContext<TVertex, TEdge, TGraph>.Graph
        {
            get { return default( TGraph ); }
        }

        LayoutMode ILayoutContext<TVertex, TEdge, TGraph>.Mode
        {
            get { return default( LayoutMode ); }
        }

        #endregion
    }
}
