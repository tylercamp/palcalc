using System.Collections.Generic;
using System.Windows;
using QuickGraph;
using GraphSharp.Algorithms.Layout.Simple.Tree;

namespace GraphSharp.Algorithms.Layout.Contextual
{
    public enum DoubleTreeVertexType
    {
        Backward,
        Forward,
        Center
    }

    public class DoubleTreeLayoutAlgorithm<TVertex, TEdge, TGraph> : ParameterizedLayoutAlgorithmBase<TVertex, TEdge, TGraph, DoubleTreeVertexType, object, DoubleTreeLayoutParameters>
        where TVertex : class
        where TEdge : IEdge<TVertex>
        where TGraph : IBidirectionalGraph<TVertex, TEdge>
    {
        readonly TVertex root;
        readonly IDictionary<TVertex, Size> vertexSizes;

        protected override DoubleTreeLayoutParameters DefaultParameters
        {
            get { return new DoubleTreeLayoutParameters(); }
        }

        public DoubleTreeLayoutAlgorithm( TGraph visitedGraph, IDictionary<TVertex, Point> vertexPositions, IDictionary<TVertex, Size> vertexSizes, DoubleTreeLayoutParameters oldParameters, TVertex selectedVertex )
            : base( visitedGraph, vertexPositions, oldParameters )
        {
            root = selectedVertex;
            this.vertexSizes = ( vertexSizes ?? new Dictionary<TVertex, Size>() );
        }

        protected override void InternalCompute()
        {
            //
            // Separate the two sides
            //
            HashSet<TVertex> side1, side2;
            SeparateSides( VisitedGraph, root, out side1, out side2 );

            #region Build the temporary graph for the two sides

            //
            // The IN side
            //
            //on the IN side we should reverse the edges
            var graph1 = new BidirectionalGraph<TVertex, Edge<TVertex>>();
            graph1.AddVertexRange( side1 );
            foreach ( var v in side1 )
            {
                vertexInfos[v] = DoubleTreeVertexType.Backward;
                foreach ( var e in VisitedGraph.InEdges( v ) )
                {
                    if ( !side1.Contains( e.Source ) || e.Source.Equals( e.Target ) )
                        continue;

                    //reverse the edge
                    graph1.AddEdge( new Edge<TVertex>( e.Target, e.Source ) );
                }
            }

            //
            // The OUT side
            //
            var graph2 = new BidirectionalGraph<TVertex, TEdge>();
            graph2.AddVertexRange( side2 );
            foreach ( var v in side2 )
            {
                vertexInfos[v] = DoubleTreeVertexType.Forward;
                foreach ( var e in VisitedGraph.OutEdges( v ) )
                {
                    if ( !side2.Contains( e.Target ) || e.Source.Equals( e.Target ) )
                        continue;

                    //simply add the edge
                    graph2.AddEdge( e );
                }
            }

            vertexInfos[root] = DoubleTreeVertexType.Center;
            #endregion

            LayoutDirection side2Direction = Parameters.Direction;
            LayoutDirection side1Direction = Parameters.Direction;
            switch ( side2Direction )
            {
                case LayoutDirection.BottomToTop:
                    side1Direction = LayoutDirection.TopToBottom;
                    break;
                case LayoutDirection.LeftToRight:
                    side1Direction = LayoutDirection.RightToLeft;
                    break;
                case LayoutDirection.RightToLeft:
                    side1Direction = LayoutDirection.LeftToRight;
                    break;
                case LayoutDirection.TopToBottom:
                    side1Direction = LayoutDirection.BottomToTop;
                    break;
            }

            //
            // SimpleTree layout on the two side
            //
            var side1LayoutAlg = new SimpleTreeLayoutAlgorithm<TVertex, Edge<TVertex>, BidirectionalGraph<TVertex, Edge<TVertex>>>(
                graph1, VertexPositions, vertexSizes,
                new SimpleTreeLayoutParameters
                    {
                        LayerGap = Parameters.LayerGap,
                        VertexGap = Parameters.VertexGap,
                        Direction = side1Direction,
                        SpanningTreeGeneration = SpanningTreeGeneration.BFS
                    } );
            var side2LayoutAlg = new SimpleTreeLayoutAlgorithm<TVertex, TEdge, BidirectionalGraph<TVertex, TEdge>>(
                graph2, VertexPositions, vertexSizes,
                new SimpleTreeLayoutParameters
                    {
                        LayerGap = Parameters.LayerGap,
                        VertexGap = Parameters.VertexGap,
                        Direction = side2Direction,
                        SpanningTreeGeneration = SpanningTreeGeneration.BFS
                    } );

            side1LayoutAlg.Compute();
            side2LayoutAlg.Compute();

            //
            // Merge the layouts
            //
            var side2Translate = side1LayoutAlg.VertexPositions[root] - side2LayoutAlg.VertexPositions[root];
            foreach ( var v in side1 )
                VertexPositions[v] = side1LayoutAlg.VertexPositions[v];

            foreach ( var v in side2 )
                VertexPositions[v] = side2LayoutAlg.VertexPositions[v] + side2Translate;
            NormalizePositions();
        }

        /// <summary>
        /// Separates the points of the graph according to the given <paramref name="selectedVertex"/>.
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="selectedVertex"></param>
        /// <param name="side1"></param>
        /// <param name="side2"></param>
        public static void SeparateSides( TGraph graph, TVertex selectedVertex, out HashSet<TVertex> side1, out HashSet<TVertex> side2 )
        {
            var visitedVertices = new HashSet<TVertex> { selectedVertex };

            //create the set of the side 1 and 2
            side1 = new HashSet<TVertex> { selectedVertex };
            side2 = new HashSet<TVertex> { selectedVertex };

            var queue1 = new Queue<TVertex>();
            var queue2 = new Queue<TVertex>();
            queue1.Enqueue( selectedVertex );
            queue2.Enqueue( selectedVertex );

            do
            {
                //get the next layer of vertices on the IN side
                for ( int i = 0, n = queue1.Count; i < n; i++ )
                {
                    var vertex = queue1.Dequeue();
                    foreach ( var edge in graph.InEdges( vertex ) )
                    {
                        if ( ( graph.ContainsVertex( edge.Source ) && visitedVertices.Add( edge.Source ) ) || vertex.Equals( selectedVertex ) )
                        {
                            queue1.Enqueue( edge.Source );
                            side1.Add( edge.Source );
                        }
                    }
                }

                //get the next layer of vertices on the OUT side
                for ( int i = 0, n = queue2.Count; i < n; i++ )
                {
                    var vertex = queue2.Dequeue();
                    foreach ( var edge in graph.OutEdges( vertex ) )
                    {
                        if ( ( graph.ContainsVertex( edge.Target ) && visitedVertices.Add( edge.Target ) ) || vertex.Equals( selectedVertex ) )
                        {
                            queue2.Enqueue( edge.Target );
                            side2.Add( edge.Target );
                        }
                    }
                }
            } while ( queue1.Count > 0 || queue2.Count > 0 );
            //we got the 2 sides
        }
    }
}