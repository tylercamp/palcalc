using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows;
using GraphSharp.Algorithms.EdgeRouting;
using GraphSharp.Algorithms.Highlight;
using GraphSharp.Algorithms.Layout;
using GraphSharp.Algorithms.Layout.Compound;
using GraphSharp.Algorithms.OverlapRemoval;
using QuickGraph;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace GraphSharp.Controls
{
    public class GraphLayout : GraphLayout<object, IEdge<object>, IBidirectionalGraph<object, IEdge<object>>>
    {
        public GraphLayout()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                var g = new BidirectionalGraph<object, IEdge<object>>();
                var vertices = new object[] { "S", "A", "M", "P", "L", "E" };
                var edges = new IEdge<object>[] {
                    new Edge<object>(vertices[0], vertices[1]),
                    new Edge<object>(vertices[1], vertices[2]),
                    new Edge<object>(vertices[1], vertices[3]),
                    new Edge<object>(vertices[3], vertices[4]),
                    new Edge<object>(vertices[0], vertices[4]),
                    new Edge<object>(vertices[4], vertices[5])
                };
                g.AddVerticesAndEdgeRange(edges);
                OverlapRemovalAlgorithmType = "FSA";
                LayoutAlgorithmType = "FR";
                Graph = g;
            }
        }
    }

    /// <summary>THE layout control. Support layout, edge routing and overlap removal algorithms, with multiple layout states.</summary>
    /// <typeparam name="TVertex">Type of the vertices.</typeparam>
    /// <typeparam name="TEdge">Type of the edges.</typeparam>
    /// <typeparam name="TGraph">Type of the graph.</typeparam>
    public partial class GraphLayout<TVertex, TEdge, TGraph>
        where TVertex : class
        where TEdge : IEdge<TVertex>
        where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
    {
        protected readonly Dictionary<TEdge, EdgeControl> EdgeControls = new Dictionary<TEdge, EdgeControl>();
        private readonly Queue<TEdge> _edgesAdded = new Queue<TEdge>();
        private readonly Queue<TEdge> _edgesRemoved = new Queue<TEdge>();
        protected List<LayoutState<TVertex, TEdge>> LayoutStates { get; }  = new List<LayoutState<TVertex, TEdge>>();
        private readonly TimeSpan _notificationLayoutDelay = TimeSpan.FromMilliseconds(5);
        private readonly object _notificationSyncRoot = new object();
        protected readonly Dictionary<TVertex, VertexControl> VertexControls = new Dictionary<TVertex, VertexControl>();
        private readonly Queue<TVertex> _verticesAdded = new Queue<TVertex>();
        private readonly Queue<TVertex> _verticesRemoved = new Queue<TVertex>();
        private readonly Stopwatch _stopWatch = new Stopwatch();
        private DateTime _lastNotificationTimestamp = DateTime.Now;

        protected IDictionary<TVertex, SizeF> Sizes;
        protected BackgroundWorker Worker;

        protected IDictionary<TVertex, SizeF> VertexSizes
        {
            get
            {
                if (Sizes == null)
                {
                    InvalidateMeasure();
                    UpdateLayout();
                    Sizes = new Dictionary<TVertex, SizeF>();
                    foreach (var kvp in VertexControls)
                    {
                        var size = kvp.Value.DesiredSize;
                        Sizes.Add(kvp.Key, new SizeF(
                                                 (double.IsNaN(size.Width) ? 0 : (float)size.Width),
                                                 (double.IsNaN(size.Height) ? 0 : (float)size.Height)));
                    }
                }
                return Sizes;
            }
        }


        #region Layout

        protected Algorithms.Layout.LayoutMode ActualLayoutMode
        {
            get
            {
                if (LayoutMode == LayoutMode.Compound || LayoutMode == LayoutMode.Automatic && Graph is ICompoundGraph<TVertex, TEdge>)
                    return Algorithms.Layout.LayoutMode.Compound;

                return Algorithms.Layout.LayoutMode.Simple;
            }
        }

        protected bool IsCompoundMode
        {
            get { return ActualLayoutMode == Algorithms.Layout.LayoutMode.Compound; }
        }

        protected virtual bool CanLayout
        {
            get { return true; }
        }

        public override void ContinueLayout()
        {
            Layout(true);
        }

        public override void Relayout()
        {
            //clear the states before
            LayoutStates.Clear();
            Layout(false);
        }

        public void CancelLayout()
        {
            if (Worker != null && Worker.IsBusy && Worker.WorkerSupportsCancellation)
                Worker.CancelAsync();
        }

        public void RecalculateEdgeRouting()
        {
            foreach (var state in LayoutStates)
                state.RouteInfos = RouteEdges(state.OverlapRemovedPositions, GetLatestVertexSizes());
            ChangeState(StateIndex);
        }

        public void RecalculateOverlapRemoval()
        {
            foreach (var state in LayoutStates)
                state.OverlapRemovedPositions = OverlapRemoval(state.Positions, GetLatestVertexSizes());
            ChangeState(StateIndex);
        }

        protected virtual ILayoutContext<TVertex, TEdge, TGraph> CreateLayoutContext(
            IDictionary<TVertex, Point> positions, IDictionary<TVertex, Size> sizes)
        {
            if (!CanLayout)
                return null;

            if (ActualLayoutMode == Algorithms.Layout.LayoutMode.Simple)
                return new LayoutContext<TVertex, TEdge, TGraph>(Graph, positions, sizes, ActualLayoutMode);

            var borders = (from kvp in VertexControls
                           where kvp.Value is CompoundVertexControl
                           select kvp).ToDictionary(
                    kvp => kvp.Key,
                    kvp => ((CompoundVertexControl)kvp.Value).VertexBorderThickness);

            var layoutTypes = (from kvp in VertexControls
                               where kvp.Value is CompoundVertexControl
                               select kvp).ToDictionary(
                kvp => kvp.Key,
                kvp => ((CompoundVertexControl)kvp.Value).LayoutMode);

            return new CompoundLayoutContext<TVertex, TEdge, TGraph>(Graph, positions, sizes, ActualLayoutMode, borders, layoutTypes);
        }

        protected IHighlightContext<TVertex, TEdge, TGraph> CreateHighlightContext()
        {
            return new HighlightContext<TVertex, TEdge, TGraph>(Graph);
        }

        protected IOverlapRemovalContext<TVertex> CreateOverlapRemovalContext(IDictionary<TVertex, Point> positions, IDictionary<TVertex, Size> sizes)
        {
            var rectangles = new Dictionary<TVertex, Rect>();
            foreach (var vertex in Graph.Vertices)
            {
                Point position;
                Size size;
                if (!positions.TryGetValue(vertex, out position) || !sizes.TryGetValue(vertex, out size))
                    continue;

                rectangles[vertex] =
                    new Rect(
                        position.X - size.Width * (float)0.5,
                        position.Y - size.Height * (float)0.5,
                        size.Width,
                        size.Height);
            }

            return new OverlapRemovalContext<TVertex>(rectangles);
        }

        protected void Layout(bool continueLayout)
        {
            if (Graph == null || Graph.VertexCount == 0 || !LayoutAlgorithmFactory.IsValidAlgorithm(LayoutAlgorithmType) || !CanLayout)
                return; //no graph to layout, or wrong layout algorithm

            UpdateLayout();
            if (!IsLoaded)
            {
                RoutedEventHandler handler = null;
                handler = (s, e) =>
                {
                    Layout(continueLayout);
                    var gl = (GraphLayout<TVertex, TEdge, TGraph>)e.Source;
                    gl.Loaded -= handler;
                };
                Loaded += handler;
                return;
            }

            //get the actual positions if we want to continue the layout
            IDictionary<TVertex, Point> oldPositions = GetOldVertexPositions(continueLayout);
            IDictionary<TVertex, Size> oldSizes = GetLatestVertexSizes();

            //create the context
            var layoutContext = CreateLayoutContext(oldPositions, oldSizes);

            //create the layout algorithm using the factory
            LayoutAlgorithm = LayoutAlgorithmFactory.CreateAlgorithm(LayoutAlgorithmType, layoutContext,
                                                                      LayoutParameters);

            if (AsyncCompute)
            {
                //Asynchronous computing - progress report & anything else
                //if there's a running progress than cancel that
                CancelLayout();

                Worker = new BackgroundWorker
                              {
                                  WorkerSupportsCancellation = true,
                                  WorkerReportsProgress = true
                              };

                //run the algorithm on a background thread
                Worker.DoWork += ((sender, e) =>
                                       {
                                           var worker = (BackgroundWorker)sender;
                                           var argument = (AsyncThreadArgument)e.Argument;
                                           if (argument.ShowAllStates)
                                               argument.Algorithm.IterationEnded +=
                                                   ((s, args) =>
                                                        {
                                                            var iterArgs = args;
                                                            if (iterArgs != null)
                                                            {
                                                                worker.ReportProgress(
                                                                    (int)Math.Round(iterArgs.StatusInPercent), iterArgs);
                                                                iterArgs.Abort = worker.CancellationPending;
                                                            }
                                                        });
                                           else
                                               argument.Algorithm.ProgressChanged +=
                                                   ((s, percent) => worker.ReportProgress((int)Math.Round(percent)));
                                           argument.Algorithm.Compute();
                                       });

                //progress changed if an iteration ended
                Worker.ProgressChanged +=
                    ((s, e) =>
                         {
                             if (e.UserState == null)
                                 LayoutStatusPercent = e.ProgressPercentage;
                             else
                                 OnLayoutIterationFinished(e.UserState as ILayoutIterationEventArgs<TVertex>);
                         });

                //background thread finished if the iteration ended
                Worker.RunWorkerCompleted += ((s, e) =>
                                                    {
                                                        OnLayoutFinished();
                                                        Worker = null;
                                                    });

                OnLayoutStarted();
                Worker.RunWorkerAsync(new AsyncThreadArgument
                                           {
                                               Algorithm = LayoutAlgorithm,
                                               ShowAllStates = ShowAllStates
                                           });
            }
            else
            {
                //Syncronous computing - no progress report
                LayoutAlgorithm.Started += ((s, e) => OnLayoutStarted());
                if (ShowAllStates)
                    LayoutAlgorithm.IterationEnded += ((s, e) => OnLayoutIterationFinished(e));
                LayoutAlgorithm.Finished += ((s, e) => OnLayoutFinished());

                LayoutAlgorithm.Compute();
            }
        }

        private IDictionary<TVertex, Point> GetOldVertexPositions(bool continueLayout)
        {
            if (ActualLayoutMode == Algorithms.Layout.LayoutMode.Simple)
                return continueLayout ? GetLatestVertexPositions() : null;

            return GetRelativePositions(continueLayout);
        }

        private IDictionary<TVertex, Point> GetLatestVertexPositions()
        {
            IDictionary<TVertex, Point> vertexPositions = new Dictionary<TVertex, Point>(VertexControls.Count);

            //go through the vertex presenters and get the actual layoutpositions
            if (ActualLayoutMode == Algorithms.Layout.LayoutMode.Simple)
            {
                foreach (var vc in VertexControls)
                {
                    var x = GetX(vc.Value);
                    var y = GetY(vc.Value);
                    vertexPositions[vc.Key] =
                        new Point(
                            double.IsNaN(x) ? 0.0 : x,
                            double.IsNaN(y) ? 0.0 : y);
                }
            }
            else
            {
                var topLeft = new Point(0, 0);
                foreach (var vc in VertexControls)
                {
                    Point position = vc.Value.TranslatePoint(topLeft, this);
                    position.X += vc.Value.ActualWidth / 2;
                    position.Y += vc.Value.ActualHeight / 2;
                    vertexPositions[vc.Key] = position;
                }
            }

            return vertexPositions;
        }

        private static Point GetRelativePosition(VertexControl vc, UIElement relativeTo)
        {
            return vc.TranslatePoint(new Point(vc.ActualWidth / 2.0, vc.ActualHeight / 2.0), relativeTo);
        }

        private Point GetRelativePosition(VertexControl vc)
        {
            return GetRelativePosition(vc, this);
        }

        private IDictionary<TVertex, Point> GetRelativePositions(bool continueLayout)
        {
            //if layout is continued it gets the relative position of every vertex
            //otherwise it's gets it only for the vertices with fixed parents
            var posDict = new Dictionary<TVertex, Point>(VertexControls.Count);
            if (continueLayout)
            {
                foreach (var kvp in VertexControls)
                {
                    posDict[kvp.Key] = GetRelativePosition(kvp.Value);
                }
            }
            else
            {
                foreach (var kvp in VertexControls)
                {
                    if (!(kvp.Value is CompoundVertexControl))
                        continue;

                    var cvc = (CompoundVertexControl)kvp.Value;
                    foreach (var vc in cvc.Vertices)
                    {
                        posDict[(TVertex)vc.Vertex] = GetRelativePosition(vc, cvc);
                    }
                }
            }
            return posDict;
        }

        private IDictionary<TVertex, Size> GetLatestVertexSizes()
        {
            if (!IsMeasureValid)
                Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            IDictionary<TVertex, Size> vertexSizes =
                new Dictionary<TVertex, Size>(VertexControls.Count);

            //go through the vertex presenters and get the actual layoutpositions
            foreach (var vc in VertexControls)
                vertexSizes[vc.Key] = new Size(vc.Value.ActualWidth, vc.Value.ActualHeight);

            return vertexSizes;
        }

        protected void OnLayoutStarted()
        {
            _stopWatch.Reset();
            _stopWatch.Start();
            LayoutStatusPercent = 0.0;
        }

        protected void OnLayoutIterationFinished(ILayoutIterationEventArgs<TVertex> iterArgs)
        {
            if (iterArgs == null || iterArgs.VertexPositions == null)
            {
                OnLayoutIterationFinished(LayoutAlgorithm.VertexPositions, null);
                SetLayoutInformations();
            }
            else
            {
                //TODO compound layout
                OnLayoutIterationFinished(iterArgs.VertexPositions, iterArgs.Message);
                LayoutStatusPercent = iterArgs.StatusInPercent;
                SetLayoutInformations(iterArgs as ILayoutInfoIterationEventArgs<TVertex, TEdge>);
            }
        }

        protected void OnLayoutIterationFinished(
            IDictionary<TVertex, Point> vertexPositions,
            string message)
        {
            var sizes = GetLatestVertexSizes();
            var overlapRemovedPositions = OverlapRemoval(vertexPositions, sizes);
            var edgeRoutingInfos = RouteEdges(overlapRemovedPositions, sizes);

            var state = new LayoutState<TVertex, TEdge>(
                vertexPositions,
                overlapRemovedPositions,
                edgeRoutingInfos,
                _stopWatch.Elapsed,
                LayoutStates.Count,
                (message ?? string.Empty));

            LayoutStates.Add(state);
            StateCount = LayoutStates.Count;
        }

        protected virtual void OnLayoutFinished()
        {
            _stopWatch.Stop();
            OnLayoutIterationFinished(null);
            StateIndex = StateCount - 1;

            //animating to the finish state
            if (StateIndex == 0)
                ChangeState(StateIndex);

            LayoutStatusPercent = 100;
        }

        private void SetLayoutInformations(ILayoutInfoIterationEventArgs<TVertex, TEdge> iterArgs)
        {
            if (iterArgs == null)
                return;

            foreach (var kvp in VertexControls)
            {
                var vertex = kvp.Key;
                var control = kvp.Value;

                GraphElementBehaviour.SetLayoutInfo(control, iterArgs.GetVertexInfo(vertex));
            }

            foreach (var kvp in EdgeControls)
            {
                var edge = kvp.Key;
                var control = kvp.Value;

                GraphElementBehaviour.SetLayoutInfo(control, iterArgs.GetEdgeInfo(edge));
            }
        }

        private void SetLayoutInformations()
        {
            if (LayoutAlgorithm == null)
                return;

            foreach (var kvp in VertexControls)
            {
                var vertex = kvp.Key;
                var control = kvp.Value;

                GraphElementBehaviour.SetLayoutInfo(control, LayoutAlgorithm.GetVertexInfo(vertex));
            }

            foreach (var kvp in EdgeControls)
            {
                var edge = kvp.Key;
                var control = kvp.Value;

                GraphElementBehaviour.SetLayoutInfo(control, LayoutAlgorithm.GetEdgeInfo(edge));
            }
        }

        protected IDictionary<TVertex, Point> OverlapRemoval(IDictionary<TVertex, Point> positions,
                                                              IDictionary<TVertex, Size> sizes)
        {
            if (positions == null || sizes == null)
                return positions; //not valid positions or sizes

            bool isValidAlgorithm = OverlapRemovalAlgorithmFactory.IsValidAlgorithm(OverlapRemovalAlgorithmType);
            if (OverlapRemovalConstraint == AlgorithmConstraints.Skip
                 ||
                 (OverlapRemovalConstraint == AlgorithmConstraints.Automatic &&
                   (!LayoutAlgorithmFactory.NeedOverlapRemoval(LayoutAlgorithmType) || !isValidAlgorithm))
                 || (OverlapRemovalConstraint == AlgorithmConstraints.Must && !isValidAlgorithm))
                return positions;

            //create the algorithm parameters based on the old parameters
            OverlapRemovalParameters = OverlapRemovalAlgorithmFactory.CreateParameters(OverlapRemovalAlgorithmType,
                                                                                        OverlapRemovalParameters);

            //create the context - rectangles, ...
            var context = CreateOverlapRemovalContext(positions, sizes);

            //create the concreate algorithm
            OverlapRemovalAlgorithm = OverlapRemovalAlgorithmFactory.CreateAlgorithm(OverlapRemovalAlgorithmType,
                                                                                      context, OverlapRemovalParameters);
            if (OverlapRemovalAlgorithm != null)
            {
                OverlapRemovalAlgorithm.Compute();

                var result = new Dictionary<TVertex, Point>();
                foreach (var res in OverlapRemovalAlgorithm.Rectangles)
                {
                    result[res.Key] = new Point(
                        (res.Value.Left + res.Value.Size.Width * 0.5),
                        (res.Value.Top + res.Value.Size.Height * 0.5));
                }

                return result;
            }

            return positions;
        }

        /// <summary>
        /// This method runs the proper edge routing algorithm.
        /// </summary>
        /// <param name="positions">The vertex positions.</param>
        /// <param name="sizes">The sizes of the vertices.</param>
        /// <returns>The routes of the edges.</returns>
        protected IDictionary<TEdge, Point[]> RouteEdges(IDictionary<TVertex, Point> positions,
                                                          IDictionary<TVertex, Size> sizes)
        {
            IEdgeRoutingAlgorithm<TVertex, TEdge, TGraph> algorithm = null;
            bool isValidAlgorithmType = EdgeRoutingAlgorithmFactory.IsValidAlgorithm(EdgeRoutingAlgorithmType);

            if (EdgeRoutingConstraint == AlgorithmConstraints.Must && isValidAlgorithmType)
            {
                //an EdgeRouting algorithm must be used
                EdgeRoutingParameters = EdgeRoutingAlgorithmFactory.CreateParameters(EdgeRoutingAlgorithmType,
                                                                                      EdgeRoutingParameters);

                var context = CreateLayoutContext(positions, sizes);

                algorithm = EdgeRoutingAlgorithmFactory.CreateAlgorithm(EdgeRoutingAlgorithmType, context,
                                                                         EdgeRoutingParameters);
                algorithm.Compute();
            }
            else if (EdgeRoutingConstraint == AlgorithmConstraints.Automatic)
            {
                if (!LayoutAlgorithmFactory.NeedEdgeRouting(LayoutAlgorithmType) &&
                     LayoutAlgorithm is IEdgeRoutingAlgorithm<TVertex, TEdge, TGraph>)
                    //the layout algorithm routes the edges
                    algorithm = LayoutAlgorithm as IEdgeRoutingAlgorithm<TVertex, TEdge, TGraph>;
                else if (isValidAlgorithmType)
                {
                    //the selected EdgeRouting algorithm will route the edges
                    EdgeRoutingParameters = EdgeRoutingAlgorithmFactory.CreateParameters(EdgeRoutingAlgorithmType,
                                                                                          EdgeRoutingParameters);
                    var context = CreateLayoutContext(positions, sizes);
                    algorithm = EdgeRoutingAlgorithmFactory.CreateAlgorithm(EdgeRoutingAlgorithmType, context,
                                                                             EdgeRoutingParameters);
                    algorithm.Compute();
                }
            }

            if (algorithm == null)
                return null;

            //route the edges
            return algorithm.EdgeRoutes;
        }

        /// <summary>
        /// Changes which layout state should be shown.
        /// </summary>
        /// <param name="stateIndex">The index of the shown layout state.</param>
        protected void ChangeState(int stateIndex)
        {
			if (stateIndex >= LayoutStates.Count) return;
			var activeState = LayoutStates[stateIndex];

            LayoutState = activeState;

            var positions = activeState.OverlapRemovedPositions;

            //Animate the vertices
            if (positions != null)
            {
                foreach (var v in Graph.Vertices)
                {
                    VertexControl vp;
                    if (!VertexControls.TryGetValue(v, out vp))
                        continue;

                    Point pos;
                    if (positions.TryGetValue(v, out pos))
                        RunMoveAnimation(vp, pos.X, pos.Y);
                }
            }

            //Change the edge routes
            if (activeState.RouteInfos != null)
            {
                foreach (var e in Graph.Edges)
                {
                    EdgeControl ec;
                    if (!EdgeControls.TryGetValue(e, out ec))
                        continue;

                    Point[] routePoints;
                    activeState.RouteInfos.TryGetValue(e, out routePoints);
                    ec.RoutePoints = routePoints;
                }
            }
        }

        public void RefreshHighlight()
        {
            //TODO doit
        }

        private class AsyncThreadArgument
        {
            public ILayoutAlgorithm<TVertex, TEdge, TGraph> Algorithm;
            public bool ShowAllStates;
        }

        #endregion
    }
}