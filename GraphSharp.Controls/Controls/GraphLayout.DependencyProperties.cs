using System;
using System.Windows;
using GraphSharp.Algorithms.EdgeRouting;
using GraphSharp.Algorithms.Highlight;
using GraphSharp.Algorithms.Layout;
using GraphSharp.Algorithms.OverlapRemoval;
using QuickGraph;

namespace GraphSharp.Controls
{
    public partial class GraphLayout<TVertex, TEdge, TGraph>
            where TVertex : class
            where TEdge : IEdge<TVertex>
            where TGraph : class, IBidirectionalGraph<TVertex, TEdge>
    {
        #region Dependency Property registrations

        public static readonly DependencyProperty AnimationDisablerVertexCountProperty =
                DependencyProperty.Register("AnimationDisablerVertexCount", typeof (int), typeof (GraphLayout<TVertex, TEdge, TGraph>), new UIPropertyMetadata(200));


        public static readonly DependencyProperty AnimationDisablerEdgeCountProperty =
                DependencyProperty.Register("AnimationDisablerEdgeCount", typeof (int), typeof (GraphLayout<TVertex, TEdge, TGraph>), new UIPropertyMetadata(500));

        //computing and status
        public static readonly DependencyProperty AsyncComputeProperty = DependencyProperty.Register("AsyncCompute", typeof (bool), typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                                                                                     new UIPropertyMetadata(false));

        public static readonly DependencyProperty EdgeRoutingAlgorithmFactoryProperty = DependencyProperty.Register("EdgeRoutingAlgorithmFactory",
                                                                                                                    typeof (IEdgeRoutingAlgorithmFactory<TVertex, TEdge, TGraph>),
                                                                                                                    typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                                                                                                    new PropertyMetadata(
                                                                                                                            new StandardEdgeRoutingAlgorithmFactory
                                                                                                                                    <TVertex, TEdge, TGraph>(), null,
                                                                                                                            EdgeRoutingAlgorithmFactoryCoerce));

        public static readonly DependencyProperty EdgeRoutingAlgorithmProperty;

        protected static readonly DependencyPropertyKey EdgeRoutingAlgorithmPropertyKey =
                DependencyProperty.RegisterReadOnly("EdgeRoutingAlgorithm",
                                                    typeof (IEdgeRoutingAlgorithm<TVertex, TEdge, TGraph>),
                                                    typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                                    new UIPropertyMetadata(null));

        public static readonly DependencyProperty EdgeRoutingAlgorithmTypeProperty =
                DependencyProperty.Register("EdgeRoutingAlgorithmType", typeof (string),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                            new PropertyMetadata(string.Empty, EdgeRoutingAlgorithmTypePropertyChanged));

        public static readonly DependencyProperty EdgeRoutingConstraintProperty =
                DependencyProperty.Register("EdgeRoutingConstraint", typeof (AlgorithmConstraints),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                            new PropertyMetadata(AlgorithmConstraints.Automatic,
                                                                 EdgeRoutingConstraintPropertyChanged));

        public static readonly DependencyProperty EdgeRoutingParametersProperty =
                DependencyProperty.Register("EdgeRoutingParameters", typeof (IEdgeRoutingParameters),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>), new PropertyMetadata(null));

        public static readonly DependencyProperty GraphProperty = DependencyProperty.Register("Graph", typeof (TGraph),
                                                                                              typeof (
                                                                                                      GraphLayout
                                                                                                      <TVertex, TEdge,
                                                                                                      TGraph>),
                                                                                              new FrameworkPropertyMetadata
                                                                                                      (null,
                                                                                                       FrameworkPropertyMetadataOptions
                                                                                                               .AffectsRender,
                                                                                                       GraphPropertyChanged));

        public static readonly DependencyProperty HighlightAlgorithmFactoryProperty =
                DependencyProperty.Register("HighlightAlgorithmFactory",
                                            typeof (IHighlightAlgorithmFactory<TVertex, TEdge, TGraph>),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                            new PropertyMetadata(
                                                    new StandardHighlightAlgorithmFactory<TVertex, TEdge, TGraph>(),
                                                    HighlightAlgorithmFactoryPropertyChanged, HighlightAlgorithmFactoryCoerce));

        public static readonly DependencyProperty HighlightAlgorithmProperty;

        protected static readonly DependencyPropertyKey HighlightAlgorithmPropertyKey =
                DependencyProperty.RegisterReadOnly("HighlightAlgorithm",
                                                    typeof (IHighlightAlgorithm<TVertex, TEdge, TGraph>),
                                                    typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                                    new UIPropertyMetadata(null, HighlightAlgorithmPropertyChanged));

        public static readonly DependencyProperty HighlightAlgorithmTypeProperty =
                DependencyProperty.Register("HighlightAlgorithmType", typeof (string),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                            new PropertyMetadata(string.Empty, HighlightAlgorithmTypePropertyChanged,
                                                                 HighlightAlgorithmTypeCoerce));

        public static readonly DependencyProperty HighlightParametersProperty =
                DependencyProperty.Register("HighlightParameters", typeof (IHighlightParameters),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                            new PropertyMetadata(null, null, HighlightParametersCoerce));

        //algorithm factories
        public static readonly DependencyProperty LayoutAlgorithmFactoryProperty =
                DependencyProperty.Register("LayoutAlgorithmFactory",
                                            typeof (ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                            new PropertyMetadata(
                                                    new StandardLayoutAlgorithmFactory<TVertex, TEdge, TGraph>(), null,
                                                    LayoutAlgorithmFactoryCoerce));

        public static readonly DependencyProperty LayoutAlgorithmProperty;

        protected static readonly DependencyPropertyKey LayoutAlgorithmPropertyKey =
                DependencyProperty.RegisterReadOnly("LayoutAlgorithm", typeof (ILayoutAlgorithm<TVertex, TEdge, TGraph>),
                                                    typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                                    new UIPropertyMetadata(null));

        public static readonly DependencyProperty LayoutAlgorithmTypeProperty =
                DependencyProperty.Register("LayoutAlgorithmType", typeof (string),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                            new PropertyMetadata(string.Empty, LayoutAlgorithmTypePropertyChanged));

        public static readonly DependencyProperty LayoutModeProperty = DependencyProperty.Register("LayoutMode",
                                                                                                   typeof (LayoutMode),
                                                                                                   typeof (
                                                                                                           GraphLayout
                                                                                                           <TVertex, TEdge,
                                                                                                           TGraph>),
                                                                                                   new PropertyMetadata(
                                                                                                           LayoutMode.
                                                                                                                   Automatic,
                                                                                                           LayoutModePropertyChanged,
                                                                                                           LayoutModeCoerce));

        //algorithm parameters
        public static readonly DependencyProperty LayoutParametersProperty =
                DependencyProperty.Register("LayoutParameters", typeof (ILayoutParameters),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>), new PropertyMetadata(null));

        public static readonly DependencyProperty LayoutStateProperty;

        protected static readonly DependencyPropertyKey LayoutStatePropertyKey =
                DependencyProperty.RegisterReadOnly("LayoutState", typeof (LayoutState<TVertex, TEdge>),
                                                    typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                                    new UIPropertyMetadata(null));

        public static readonly DependencyProperty LayoutStatusPercentProperty;

        protected static readonly DependencyPropertyKey LayoutStatusPercentPropertyKey =
                DependencyProperty.RegisterReadOnly("LayoutStatusPercent", typeof (double),
                                                    typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                                    new UIPropertyMetadata(0.0));

        public static readonly DependencyProperty OverlapRemovalAlgorithmFactoryProperty =
                DependencyProperty.Register("OverlapRemovalAlgorithmFactory",
                                            typeof (IOverlapRemovalAlgorithmFactory<TVertex>),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                            new PropertyMetadata(new StandardOverlapRemovalAlgorithmFactory<TVertex>(), null,
                                                                 OverlapRemovalAlgorithmFactoryCoerce));

        public static readonly DependencyProperty OverlapRemovalAlgorithmProperty;

        protected static readonly DependencyPropertyKey OverlapRemovalAlgorithmPropertyKey =
                DependencyProperty.RegisterReadOnly("OverlapRemovalAlgorithm", typeof (IOverlapRemovalAlgorithm<TVertex>),
                                                    typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                                    new UIPropertyMetadata(null));

        public static readonly DependencyProperty OverlapRemovalAlgorithmTypeProperty =
                DependencyProperty.Register("OverlapRemovalAlgorithmType", typeof (string),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                            new PropertyMetadata(string.Empty, OverlapRemovalAlgorithmTypePropertyChanged));

        public static readonly DependencyProperty OverlapRemovalConstraintProperty =
                DependencyProperty.Register("OverlapRemovalConstraint", typeof (AlgorithmConstraints),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                            new PropertyMetadata(AlgorithmConstraints.Automatic,
                                                                 OverlapRemovalConstraintPropertyChanged));

        public static readonly DependencyProperty OverlapRemovalParametersProperty =
                DependencyProperty.Register("OverlapRemovalParameters", typeof (IOverlapRemovalParameters),
                                            typeof (GraphLayout<TVertex, TEdge, TGraph>), new PropertyMetadata(null));

        public static readonly DependencyProperty ShowAllStatesProperty = DependencyProperty.Register("ShowAllStates",
                                                                                                      typeof (bool),
                                                                                                      typeof (
                                                                                                              GraphLayout
                                                                                                              <TVertex,
                                                                                                              TEdge, TGraph>
                                                                                                              ),
                                                                                                      new UIPropertyMetadata
                                                                                                              (false));

        public static readonly DependencyProperty StateCountProperty;

        protected static readonly DependencyPropertyKey StateCountPropertyKey =
                DependencyProperty.RegisterReadOnly("StateCount", typeof (int), typeof (GraphLayout<TVertex, TEdge, TGraph>),
                                                    new UIPropertyMetadata(0, StateCountPropertyChanged));

        public static readonly DependencyProperty StateIndexProperty = DependencyProperty.Register("StateIndex",
                                                                                                   typeof (int),
                                                                                                   typeof (
                                                                                                           GraphLayout
                                                                                                           <TVertex, TEdge,
                                                                                                           TGraph>),
                                                                                                   new UIPropertyMetadata
                                                                                                           (0,
                                                                                                            StateIndexPropertyChanged,
                                                                                                            StateIndexCoerce));

        /// <summary>
        ///     Static Constructor. At this time it's only task to initialize the readonly Dependency Properties.
        /// </summary>
        static GraphLayout()
        {
            //initialize the readonly dependency properties
            StateCountProperty = StateCountPropertyKey.DependencyProperty;
            LayoutStatusPercentProperty = LayoutStatusPercentPropertyKey.DependencyProperty;
            LayoutAlgorithmProperty = LayoutAlgorithmPropertyKey.DependencyProperty;
            OverlapRemovalAlgorithmProperty = OverlapRemovalAlgorithmPropertyKey.DependencyProperty;
            EdgeRoutingAlgorithmProperty = EdgeRoutingAlgorithmPropertyKey.DependencyProperty;
            LayoutStateProperty = LayoutStatePropertyKey.DependencyProperty;
            HighlightAlgorithmProperty = HighlightAlgorithmPropertyKey.DependencyProperty;
        }

        public GraphLayout()
        {
            AddHandler(GraphElementBehaviour.HighlightTriggeredEvent,
                       new HighlightTriggerEventHandler(HighlightTriggerEventHandler));
        }

        /// <summary>
        ///     If the graph has more vertex than this count, the animation will be disabled
        ///     automatically.
        /// </summary>
        public int AnimationDisablerVertexCount
        {
            get { return (int) GetValue(AnimationDisablerVertexCountProperty); }
            set { SetValue(AnimationDisablerVertexCountProperty, value); }
        }

        /// <summary>
        ///     If the graph has more edge than this count, the animations will be
        ///     disabled automatically.
        /// </summary>
        public int AnimationDisablerEdgeCount
        {
            get { return (int) GetValue(AnimationDisablerEdgeCountProperty); }
            set { SetValue(AnimationDisablerEdgeCountProperty, value); }
        }

        public override bool CanAnimate
        {
            get
            {
                return base.CanAnimate
                       && AnimationLength > new TimeSpan(0, 0, 0, 0, 0)
                       && Graph != null
                       && (AnimationDisablerVertexCount < 0 || Graph.VertexCount < AnimationDisablerVertexCount)
                       && (AnimationDisablerEdgeCount < 0 || Graph.EdgeCount < AnimationDisablerEdgeCount);
            }
        }

        public void HighlightTriggerEventHandler(object sender, HighlightTriggeredEventArgs args)
        {
            if (Graph == null || HighlightAlgorithm == null)
                return;

            if (args.OriginalSource is VertexControl)
            {
                var vc = (VertexControl) args.OriginalSource;
                var vertex = vc.Vertex as TVertex;
                if (vertex == null || !Graph.ContainsVertex(vertex))
                    return;

                if (args.IsPositiveTrigger)
                    HighlightAlgorithm.OnVertexHighlighting(vertex);
                else
                    HighlightAlgorithm.OnVertexHighlightRemoving(vertex);
            }
            else if (args.OriginalSource is EdgeControl)
            {
                var ec = (EdgeControl) args.OriginalSource;
                TEdge edge = default(TEdge);
                try
                {
                    edge = (TEdge) ec.Edge;
                }
                catch
                {
                }
                if (Equals(edge, default(TEdge)) || !Graph.ContainsEdge(edge))
                    return;

                if (args.IsPositiveTrigger)
                    HighlightAlgorithm.OnEdgeHighlighting(edge);
                else
                    HighlightAlgorithm.OnEdgeHighlightRemoving(edge);
            }
        }

        #endregion

        #region Dependency Property Callbacks

        /// <summary>
        ///     Property Change Callback of the <code>StateIndex</code> dependency property. It call the animation,
        ///     so the layout will animate from one state to the another.
        /// </summary>
        protected static void StateIndexPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var gl = obj as GraphLayout<TVertex, TEdge, TGraph>;
            if (gl == null) return;

            var p = (int) e.NewValue;
            gl.ChangeState(p);
        }

        /// <summary>
        ///     Coerce Callback of the <code>StateIndex</code> dependency property.
        /// </summary>
        /// <returns>It coerces the value of the <see cref="StateIndex" /> between 0 and (<see cref="StateCount" />-1).</returns>
        protected static object StateIndexCoerce(DependencyObject obj, object stateIndex)
        {
            var gl = obj as GraphLayout<TVertex, TEdge, TGraph>;
            var p = (int) stateIndex;

            if (gl == null) return p;
            if (p < 0 || gl.LayoutStates.Count == 0) return 0;
            if (p > gl.LayoutStates.Count - 1) return gl.LayoutStates.Count - 1;
            return p;
        }

        /// <summary>
        ///     Property Change Callback of the <see cref="StateCount" /> dependency property.
        ///     It coerces the <see cref="StateIndex" />.
        /// </summary>
        protected static void StateCountPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var gl = obj as GraphLayout<TVertex, TEdge, TGraph>;
            if (gl != null)
                gl.CoerceValue(StateIndexProperty);
        }

        protected static void LayoutModePropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;
            gl.OnRelayoutInduction(false);
        }

        /// <summary>
        ///     Coerce callback of the <see cref="LayoutMode" /> dependency property.
        /// </summary>
        private static object LayoutModeCoerce(DependencyObject obj, object newValue)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;
            if (gl.Graph == null)
                return LayoutMode.Automatic;

            return newValue;
        }

        /// <summary>
        ///     It's called when the Graph dependency property changed.
        /// </summary>
        /// <param name="obj">The GraphLayout instance which Graph dependency property changed.</param>
        /// <param name="e">OldValue & NewValue</param>
        protected static void GraphPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;

            var g = e.NewValue as TGraph;
            if (g == null)
            {
                gl.LayoutMode = LayoutMode.Automatic;
                return;
            }

            gl.OnRelayoutInduction(true);
        }

        private void OnRelayoutInduction(bool tryKeepControls)
        {
            if (HighlightAlgorithm != null)
                HighlightAlgorithm.ResetHighlight();

            //recreate the graph elements
            RecreateGraphElements(tryKeepControls);

            //do the layout process again
            Relayout();
        }

        protected static object LayoutAlgorithmFactoryCoerce(DependencyObject obj, object newValue)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;
            return newValue ?? gl.LayoutAlgorithmFactory;
        }

        protected static object EdgeRoutingAlgorithmFactoryCoerce(DependencyObject obj, object newValue)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;
            return newValue ?? gl.EdgeRoutingAlgorithmFactory;
        }

        protected static object OverlapRemovalAlgorithmFactoryCoerce(DependencyObject obj, object newValue)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;
            return newValue ?? gl.OverlapRemovalAlgorithmFactory;
        }

        protected static object HighlightAlgorithmFactoryCoerce(DependencyObject obj, object newValue)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;
            return newValue ?? gl.HighlightAlgorithmFactory;
        }

        private static void HighlightAlgorithmFactoryPropertyChanged(DependencyObject obj,
                                                                     DependencyPropertyChangedEventArgs e)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;

            string highlightMethod = gl.HighlightAlgorithmType;
            gl.HighlightAlgorithmType = null;
            gl.HighlightAlgorithmType = highlightMethod;
        }

        protected static void LayoutAlgorithmTypePropertyChanged(DependencyObject obj,
                                                                 DependencyPropertyChangedEventArgs e)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;
            string newAlgoType = e.NewValue == null ? string.Empty : e.NewValue.ToString();

            //regenerate parameters
            gl.LayoutParameters = gl.LayoutAlgorithmFactory.CreateParameters(newAlgoType, gl.LayoutParameters);

            if (gl.Graph != null)
            {
                gl.Relayout();
            }
        }

        protected static void EdgeRoutingAlgorithmTypePropertyChanged(DependencyObject obj,
                                                                      DependencyPropertyChangedEventArgs e)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;

            string newAlgoType = e.NewValue == null ? string.Empty : e.NewValue.ToString();

            //regenerate parameters
            gl.EdgeRoutingParameters = gl.EdgeRoutingAlgorithmFactory.CreateParameters(newAlgoType,
                                                                                       gl.EdgeRoutingParameters);

            if (gl.Graph != null)
            {
                gl.RecalculateEdgeRouting();
            }
        }

        protected static void EdgeRoutingConstraintPropertyChanged(DependencyObject obj,
                                                                   DependencyPropertyChangedEventArgs e)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;

            //regenerate parameters
            if (gl.Graph != null)
                gl.RecalculateEdgeRouting();
        }

        protected static void OverlapRemovalAlgorithmTypePropertyChanged(DependencyObject obj,
                                                                         DependencyPropertyChangedEventArgs e)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;

            string newAlgoType = e.NewValue == null ? string.Empty : e.NewValue.ToString();

            //regenerate parameters
            gl.OverlapRemovalParameters = gl.OverlapRemovalAlgorithmFactory.CreateParameters(newAlgoType,
                                                                                             gl.OverlapRemovalParameters);

            if (gl.Graph != null)
            {
                gl.RecalculateOverlapRemoval();
            }
        }

        protected static void OverlapRemovalConstraintPropertyChanged(DependencyObject obj,
                                                                      DependencyPropertyChangedEventArgs e)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;

            //regenerate parameters
            if (gl.Graph != null)
                gl.RecalculateOverlapRemoval();
        }

        private static object HighlightAlgorithmTypeCoerce(DependencyObject obj, object newValue)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;

            if (!gl.HighlightAlgorithmFactory.IsValidMode(newValue as string))
                return null;

            return newValue;
        }

        protected static void HighlightAlgorithmTypePropertyChanged(DependencyObject obj,
                                                                    DependencyPropertyChangedEventArgs e)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;

            string newAlgoType = e.NewValue == null ? string.Empty : e.NewValue.ToString();

            //regenerate algorithm, parameters
            IHighlightParameters parameters = gl.HighlightAlgorithmFactory.CreateParameters(newAlgoType, gl.HighlightParameters);
            gl.HighlightAlgorithm = gl.HighlightAlgorithmFactory.CreateAlgorithm(newAlgoType,
                                                                                 gl.CreateHighlightContext(), gl,
                                                                                 parameters);
        }

        private static void HighlightAlgorithmPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var algo = e.NewValue as IHighlightAlgorithm<TVertex, TEdge, TGraph>;
            if (algo != null)
                algo.ResetHighlight();
        }

        private static object HighlightParametersCoerce(DependencyObject obj, object newValue)
        {
            var gl = (GraphLayout<TVertex, TEdge, TGraph>) obj;

            if (gl.HighlightAlgorithm != null)
            {
                gl.HighlightAlgorithm.TrySetParameters(newValue as IHighlightParameters);
                return gl.HighlightAlgorithm.Parameters;
            }
            return null;
        }

        #endregion

        #region Dependency Properties

        /// <summary>If this property is <code>true</code> the algorithm will be executed on a separate thread.</summary>
        public bool AsyncCompute
        {
            get { return (bool) GetValue(AsyncComputeProperty); }
            set { SetValue(AsyncComputeProperty, value); }
        }

        /// <summary>Dependency property. Gets or sets the layout mode.</summary>
        public LayoutMode LayoutMode
        {
            get { return (LayoutMode) GetValue(LayoutModeProperty); }
            set { SetValue(LayoutModeProperty, value); }
        }

        /// <summary>Gets the status of the layout process in percent. Readonly Dependency Property.</summary>
        public double LayoutStatusPercent
        {
            get { return (double) GetValue(LayoutStatusPercentProperty); }
            protected set { SetValue(LayoutStatusPercentPropertyKey, value); }
        }

        /// <summary>
        ///     If <code>true</code> all state of the layout process will be stored, so you can "replay" the layout, otherwise only the start and end states
        ///     will be stored. Dependency Property.
        /// </summary>
        public bool ShowAllStates
        {
            get { return (bool) GetValue(ShowAllStatesProperty); }
            set { SetValue(ShowAllStatesProperty, value); }
        }

        /// <summary>Gets or sets the index of the actually shown layout state. Dependency Property.</summary>
        public int StateIndex
        {
            get { return (int) GetValue(StateIndexProperty); }
            set { SetValue(StateIndexProperty, value); }
        }

        /// <summary>Gets the number of the layout states. Readonly Dependency Property.</summary>
        public int StateCount
        {
            get { return (int) GetValue(StateCountProperty); }
            protected set { SetValue(StateCountPropertyKey, value); }
        }

        public LayoutState<TVertex, TEdge> LayoutState
        {
            get { return (LayoutState<TVertex, TEdge>) GetValue(LayoutStateProperty); }
            protected set { SetValue(LayoutStatePropertyKey, value); }
        }

        /// <summary>The algorithm which have been used/is running/will be executed. Dependency Property.</summary>
        public ILayoutAlgorithm<TVertex, TEdge, TGraph> LayoutAlgorithm
        {
            get { return (ILayoutAlgorithm<TVertex, TEdge, TGraph>) GetValue(LayoutAlgorithmProperty); }
            protected set { SetValue(LayoutAlgorithmPropertyKey, value); }
        }

        /// <summary>
        ///     The algorithm used for the edge routing. (It can be the same object as the LayoutAlgorithm, in this case the EdgeRoutingAlgorithm won't be run
        ///     separately, the edge routing should be calculated in the LayoutAlgorithm).
        /// </summary>
        public IEdgeRoutingAlgorithm<TVertex, TEdge, TGraph> EdgeRoutingAlgorithm
        {
            get { return (IEdgeRoutingAlgorithm<TVertex, TEdge, TGraph>) GetValue(EdgeRoutingAlgorithmProperty); }
            protected set { SetValue(EdgeRoutingAlgorithmPropertyKey, value); }
        }

        public IOverlapRemovalAlgorithm<TVertex> OverlapRemovalAlgorithm
        {
            get { return (IOverlapRemovalAlgorithm<TVertex>) GetValue(OverlapRemovalAlgorithmProperty); }
            protected set { SetValue(OverlapRemovalAlgorithmPropertyKey, value); }
        }

        public IHighlightAlgorithm<TVertex, TEdge, TGraph> HighlightAlgorithm
        {
            get { return (IHighlightAlgorithm<TVertex, TEdge, TGraph>) GetValue(HighlightAlgorithmProperty); }
            protected set { SetValue(HighlightAlgorithmPropertyKey, value); }
        }

        public ILayoutAlgorithmFactory<TVertex, TEdge, TGraph> LayoutAlgorithmFactory
        {
            get { return (ILayoutAlgorithmFactory<TVertex, TEdge, TGraph>) GetValue(LayoutAlgorithmFactoryProperty); }
            set { SetValue(LayoutAlgorithmFactoryProperty, value); }
        }

        public string LayoutAlgorithmType
        {
            get { return (string) GetValue(LayoutAlgorithmTypeProperty); }
            set { SetValue(LayoutAlgorithmTypeProperty, value); }
        }

        public IEdgeRoutingAlgorithmFactory<TVertex, TEdge, TGraph> EdgeRoutingAlgorithmFactory
        {
            get
            {
                return
                        (IEdgeRoutingAlgorithmFactory<TVertex, TEdge, TGraph>) GetValue(EdgeRoutingAlgorithmFactoryProperty);
            }
            set { SetValue(EdgeRoutingAlgorithmFactoryProperty, value); }
        }

        public string EdgeRoutingAlgorithmType
        {
            get { return (string) GetValue(EdgeRoutingAlgorithmTypeProperty); }
            set { SetValue(EdgeRoutingAlgorithmTypeProperty, value); }
        }

        public IOverlapRemovalAlgorithmFactory<TVertex> OverlapRemovalAlgorithmFactory
        {
            get { return (IOverlapRemovalAlgorithmFactory<TVertex>) GetValue(OverlapRemovalAlgorithmFactoryProperty); }
            set { SetValue(OverlapRemovalAlgorithmFactoryProperty, value); }
        }

        public string OverlapRemovalAlgorithmType
        {
            get { return (string) GetValue(OverlapRemovalAlgorithmTypeProperty); }
            set { SetValue(OverlapRemovalAlgorithmTypeProperty, value); }
        }

        public IHighlightAlgorithmFactory<TVertex, TEdge, TGraph> HighlightAlgorithmFactory
        {
            get { return (IHighlightAlgorithmFactory<TVertex, TEdge, TGraph>) GetValue(HighlightAlgorithmFactoryProperty); }
            set { SetValue(HighlightAlgorithmFactoryProperty, value); }
        }

        public string HighlightAlgorithmType
        {
            get { return (string) GetValue(HighlightAlgorithmTypeProperty); }
            set { SetValue(HighlightAlgorithmTypeProperty, value); }
        }

        public AlgorithmConstraints EdgeRoutingConstraint
        {
            get { return (AlgorithmConstraints) GetValue(EdgeRoutingConstraintProperty); }
            set { SetValue(EdgeRoutingConstraintProperty, value); }
        }

        public AlgorithmConstraints OverlapRemovalConstraint
        {
            get { return (AlgorithmConstraints) GetValue(OverlapRemovalConstraintProperty); }
            set { SetValue(OverlapRemovalConstraintProperty, value); }
        }

        public ILayoutParameters LayoutParameters
        {
            get { return (ILayoutParameters) GetValue(LayoutParametersProperty); }
            set { SetValue(LayoutParametersProperty, value); }
        }

        public IEdgeRoutingParameters EdgeRoutingParameters
        {
            get { return (IEdgeRoutingParameters) GetValue(EdgeRoutingParametersProperty); }
            set { SetValue(EdgeRoutingParametersProperty, value); }
        }

        public IOverlapRemovalParameters OverlapRemovalParameters
        {
            get { return (IOverlapRemovalParameters) GetValue(OverlapRemovalParametersProperty); }
            set { SetValue(OverlapRemovalParametersProperty, value); }
        }

        public IHighlightParameters HighlightParameters
        {
            get { return (IHighlightParameters) GetValue(HighlightParametersProperty); }
            set { SetValue(HighlightParametersProperty, value); }
        }

        /// <summary>The graph we want to show. Dependency Property./// </summary>
        public TGraph Graph
        {
            get { return (TGraph) GetValue(GraphProperty); }
            set { SetValue(GraphProperty, value); }
        }

        #endregion
    }
}