using System.Collections.Generic;
using QuickGraph;

namespace GraphSharp.Algorithms.Layout.Simple.Tree
{
    public partial class SimpleTreeLayoutAlgorithm<TVertex, TEdge, TGraph>
        where TVertex : class
        where TEdge : IEdge<TVertex>
        where TGraph : IBidirectionalGraph<TVertex, TEdge>
    {
        class Layer
        {
            public double Size;
            public double NextPosition;
            public readonly IList<TVertex> Vertices = new List<TVertex>();
            public double LastTranslate;

            public Layer()
            {
                LastTranslate = 0;
            }
        }

        class VertexData
        {
            public TVertex Parent;
            public double Translate;
            public double Position;
        }
    }
}
