using System.Collections.Generic;
using QuickGraph;
using System.Windows;

namespace GraphSharp.Algorithms.Layout.Contextual
{
    public class ContextualLayoutContext<TVertex, TEdge, TGraph> : LayoutContext<TVertex, TEdge, TGraph>
        where TEdge : IEdge<TVertex>
        where TGraph : IVertexAndEdgeListGraph<TVertex, TEdge>
    {
        public TVertex SelectedVertex { get; private set; }

        public ContextualLayoutContext( TGraph graph, TVertex selectedVertex, IDictionary<TVertex, Point> positions, IDictionary<TVertex, Size> sizes )
            : base( graph, positions, sizes, LayoutMode.Simple )
        {
            SelectedVertex = selectedVertex;
        }
    }
}