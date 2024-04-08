using System.Collections.Generic;
using System.Linq;
using GraphSharp.Controls;
using QuickGraph;

namespace GraphSharp.Serialization
{
    public abstract class GraphInfo
    {}

    public class GraphInfo<TVertex, TVertexInfo, TEdge, TEdgeInfo, TGraph> : GraphInfo
        where TVertex : class 
        where TVertexInfo : VertexInfo<TVertex>, new()
        where TEdge : class, IEdge<TVertex>
        where TEdgeInfo : EdgeInfo<TEdge>, new()
        where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
    {
        public VertexInfoCollection Verteces { get; } = new VertexInfoCollection();

        public EdgeInfoCollection Edges { get; } = new EdgeInfoCollection();

        public static TGraphInfo Create<TGraphInfo>(GraphCanvas layout)
            where TGraphInfo : GraphInfo<TVertex, TVertexInfo, TEdge, TEdgeInfo, TGraph>, new()
        {
            TGraphInfo graph = new TGraphInfo();

            Dictionary<TVertex, VertexInfo<TVertex>> vertecesSet = new Dictionary<TVertex, VertexInfo<TVertex>>();

            foreach (VertexControl control in layout.Children.OfType<VertexControl>())
            {
                TVertexInfo info = new TVertexInfo
                {
                    ID = graph.Verteces.Count,
                    Vertex = control.Vertex as TVertex,
                    Height = control.ActualHeight,
                    Width = control.ActualWidth,
                    X = GraphCanvas.GetX(control),
                    Y = GraphCanvas.GetY(control)
                };
                graph.Verteces.Add(info);

                vertecesSet.Add(info.Vertex, info);
            }

            foreach (EdgeControl control in layout.Children.OfType<EdgeControl>())
            {
                TEdgeInfo info = new TEdgeInfo
                {
                    ID = graph.Edges.Count, 

                    SourceID = GetId(control.Source.Vertex as TVertex, vertecesSet),
                    TargetID = GetId(control.Target.Vertex as TVertex, vertecesSet)
                };
                graph.Edges.Add(info);
            }

            return graph;
        }

        private static int GetId(TVertex vertex, Dictionary<TVertex, VertexInfo<TVertex>> vertecesSet)
        {
            if (vertecesSet.TryGetValue(vertex, out VertexInfo<TVertex> info))
            {
                return info.ID;
            }

            return -1;
        }

//        var g = new BidirectionalGraph<object, IEdge<object>>();
//        var vertices = new object[] { "S", "A", "M", "P", "L", "E" };
//        var edges = new IEdge<object>[] {
//            new Edge<object>(vertices[0], vertices[1]),
//            new Edge<object>(vertices[1], vertices[2]),
//            new Edge<object>(vertices[1], vertices[3]),
//            new Edge<object>(vertices[3], vertices[4]),
//            new Edge<object>(vertices[0], vertices[4]),
//            new Edge<object>(vertices[4], vertices[5])
//        };
//        g.AddVerticesAndEdgeRange(edges);
//        OverlapRemovalAlgorithmType = "FSA";
//        LayoutAlgorithmType = "FR";
//        Graph = g;
    }
}