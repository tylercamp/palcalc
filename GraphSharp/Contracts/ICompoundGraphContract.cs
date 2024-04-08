using System.Collections.Generic;
using System.Diagnostics.Contracts;
using QuickGraph;

namespace GraphSharp.Contracts
{
    /// <summary>
    /// Contract class for the ICompoundGraph interface.
    /// </summary>
    /// <typeparam name="TVertex">
    /// Type of the vertex.
    /// </typeparam>
    /// <typeparam name="TEdge">
    /// Type of the edge.
    /// </typeparam>
    [ContractClassFor( typeof( ICompoundGraph<,> ) )]
    internal sealed class ICompoundGraphContract<TVertex, TEdge>
        : BidirectionalGraph<TVertex, TEdge>, ICompoundGraph<TVertex, TEdge>
        where TEdge : IEdge<TVertex>
    {
        #region ICompoundGraph<TVertex,TEdge> Members

        [Pure]
        bool ICompoundGraph<TVertex, TEdge>.AddChildVertex( TVertex parent, TVertex child )
        {
            ICompoundGraph<TVertex, TEdge> ithis = this;
            Contract.Requires( !parent.Equals( default( TVertex ) ) );
            Contract.Requires( ithis.ContainsVertex( parent ) );

            return default( bool );
        }

        [Pure]
        int ICompoundGraph<TVertex, TEdge>.AddChildVertexRange( TVertex parent, IEnumerable<TVertex> children )
        {
            ICompoundGraph<TVertex, TEdge> ithis = this;
            Contract.Requires( !parent.Equals( default( TVertex ) ) );
            Contract.Requires( ithis.ContainsVertex( parent ) );
            Contract.Ensures( Contract.Result<int>() >= 0 );

            return default( int );
        }

        [Pure]
        TVertex ICompoundGraph<TVertex, TEdge>.GetParent( TVertex vertex )
        {
            ICompoundGraph<TVertex, TEdge> ithis = this;
            Contract.Requires( !vertex.Equals( default( TVertex ) ) );
            Contract.Requires( ithis.ContainsVertex( vertex ) );
            //TODO this is a bug in the MS Contract, i think -- solve it
            //Contract.Ensures( Contract.Result<TVertex>().Equals( default( TVertex ) ) || ithis.ContainsVertex( Contract.Result<TVertex>() ) );

            return default( TVertex );
        }

        [Pure]
        bool ICompoundGraph<TVertex, TEdge>.IsChildVertex( TVertex vertex )
        {
            ICompoundGraph<TVertex, TEdge> ithis = this;
            Contract.Requires( !vertex.Equals( default( TVertex ) ) );
            Contract.Requires( ithis.ContainsVertex( vertex ) );

            return default( bool );
        }

        [Pure]
        IEnumerable<TVertex> ICompoundGraph<TVertex, TEdge>.GetChildrenVertices( TVertex vertex )
        {
            ICompoundGraph<TVertex, TEdge> ithis = this;
            Contract.Requires( !vertex.Equals( default( TVertex ) ) );
            Contract.Requires( ithis.ContainsVertex( vertex ) );

            return new List<TVertex>();
        }

        [Pure]
        int ICompoundGraph<TVertex, TEdge>.GetChildrenCount( TVertex vertex )
        {
            ICompoundGraph<TVertex, TEdge> ithis = this;
            Contract.Requires( ithis.ContainsVertex( vertex ) );
            Contract.Ensures( Contract.Result<int>() >= 0 );

            return default( int );
        }

        [Pure]
        bool ICompoundGraph<TVertex, TEdge>.IsCompoundVertex( TVertex vertex )
        {
            Contract.Requires( vertex != null );
            Contract.Requires( ContainsVertex( vertex ) );

            return default( bool );
        }

        IEnumerable<TVertex> ICompoundGraph<TVertex, TEdge>.CompoundVertices
        {
            get
            {
                return null;
            }
        }

        IEnumerable<TVertex> ICompoundGraph<TVertex, TEdge>.SimpleVertices
        {
            get
            {
                return null;
            }
        }

        #endregion
    }
}
