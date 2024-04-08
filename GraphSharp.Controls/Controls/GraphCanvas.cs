using System;
using System.Windows;
using System.Windows.Controls;
using GraphSharp.Controls.Animations;
using GraphSharp.Controls.Transitions;

namespace GraphSharp.Controls
{
    public class GraphCanvas : Panel
    {
        #region Attached Dependency Property registrations
        public static readonly DependencyProperty XProperty =
            DependencyProperty.RegisterAttached("X", typeof(double), typeof(GraphCanvas),
                                                 new FrameworkPropertyMetadata(double.NaN,
                                                                                FrameworkPropertyMetadataOptions.AffectsMeasure |
                                                                                FrameworkPropertyMetadataOptions.AffectsArrange |
                                                                                FrameworkPropertyMetadataOptions.AffectsRender |
                                                                                FrameworkPropertyMetadataOptions.AffectsParentMeasure |
                                                                                FrameworkPropertyMetadataOptions.AffectsParentArrange |
                                                                                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                                                                                XPropertyChanged));

        private static void XPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var xChange = (double)e.NewValue - (double)e.OldValue;
            PositionChanged(d, xChange, 0);
        }


        public static readonly DependencyProperty YProperty =
            DependencyProperty.RegisterAttached("Y", typeof(double), typeof(GraphCanvas),
                                                 new FrameworkPropertyMetadata(double.NaN,
                                                                                FrameworkPropertyMetadataOptions.AffectsMeasure |
                                                                                FrameworkPropertyMetadataOptions.AffectsArrange |
                                                                                FrameworkPropertyMetadataOptions.AffectsRender |
                                                                                FrameworkPropertyMetadataOptions.AffectsParentMeasure |
                                                                                FrameworkPropertyMetadataOptions.AffectsParentArrange |
                                                                                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                                                                                YPropertyChanged));

        private static void YPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var yChange = (double)e.NewValue - (double)e.OldValue;
            PositionChanged(d, 0, yChange);
        }

        private static void PositionChanged(DependencyObject d, double xChange, double yChange)
        {
            var e = d as UIElement;
            if (e != null)
                e.RaiseEvent(new PositionChangedEventArgs(PositionChangedEvent, e, xChange, yChange));
        }

        #endregion

        #region Attached Properties
        [AttachedPropertyBrowsableForChildren]
        public static double GetX(DependencyObject obj)
        {
            return (double)obj.GetValue(XProperty);
        }

        public static void SetX(DependencyObject obj, double value)
        {
            obj.SetValue(XProperty, value);
        }

        [AttachedPropertyBrowsableForChildren]
        public static double GetY(DependencyObject obj)
        {
            return (double)obj.GetValue(YProperty);
        }

        public static void SetY(DependencyObject obj, double value)
        {
            obj.SetValue(YProperty, value);
        }

        #endregion

        #region Attached Routed Events
        public static readonly RoutedEvent PositionChangedEvent =
            EventManager.RegisterRoutedEvent("PositionChanged", RoutingStrategy.Bubble, typeof(PositionChangedEventHandler), typeof(GraphCanvas));
        public static void AddPositionChangedHandler(DependencyObject d, RoutedEventHandler handler)
        {
            var e = d as UIElement;
            if (e != null)
                e.AddHandler(PositionChangedEvent, handler);
        }

        public static void RemovePositionChangedHandler(DependencyObject d, RoutedEventHandler handler)
        {
            var e = d as UIElement;
            if (e != null)
                e.RemoveHandler(PositionChangedEvent, handler);
        }
        #endregion

        #region Measure & Arrange

        /// <summary>The position of the topLeft corner of the most top-left vertex.</summary>
        private Point _topLeft;

        /// <summary>The position of the bottom right corner of the most bottom-right vertex.</summary>
        private Point _bottomRight;

        /// <summary>Arranges the size of the control.</summary>
        /// <param name="arrangeSize">The arranged size of the control.</param>
        /// <returns>The size of the control.</returns>
        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var translate = new Vector(-_topLeft.X, -_topLeft.Y);
            Vector graphSize = (_bottomRight - _topLeft);

            if (double.IsNaN(graphSize.X) || double.IsNaN(graphSize.Y) ||
                 double.IsInfinity(graphSize.X) || double.IsInfinity(graphSize.Y))
                translate = new Vector(0, 0);

            Translation = translate;

            graphSize = InternalChildren.Count > 0
                            ? new Vector(double.NegativeInfinity, double.NegativeInfinity)
                            : new Vector(0, 0);

            //translate with the topLeft
            foreach (UIElement child in InternalChildren)
            {
                double x = GetX(child);
                double y = GetY(child);
                if (double.IsNaN(x) || double.IsNaN(y))
                {
                    //not a vertex, set the coordinates of the top-left corner
                    x = double.IsNaN(x) ? translate.X : x;
                    y = double.IsNaN(y) ? translate.Y : y;
                }
                else
                {
                    //this is a vertex
                    x += translate.X;
                    y += translate.Y;

                    //get the top-left corner
                    x -= child.DesiredSize.Width * 0.5;
                    y -= child.DesiredSize.Height * 0.5;
                }
                child.Arrange(new Rect(new Point(x, y), child.DesiredSize));

                graphSize.X = Math.Max(0, Math.Max(graphSize.X, x + child.DesiredSize.Width));
                graphSize.Y = Math.Max(0, Math.Max(graphSize.Y, y + child.DesiredSize.Height));
            }

            return new Size(graphSize.X, graphSize.Y);
        }

        /// <summary>Overridden measure. It calculates a size where all of of the vertices are visible.</summary>
        /// <param name="constraint">The size constraint.</param>
        /// <returns>The calculated size.</returns>
        protected override Size MeasureOverride(Size constraint)
        {
            _topLeft = new Point(double.PositiveInfinity, double.PositiveInfinity);
            _bottomRight = new Point(double.NegativeInfinity, double.NegativeInfinity);

            foreach (UIElement child in InternalChildren)
            {
                //measure the child
                child.Measure(constraint);

                //get the position of the vertex
                double left = GetX(child);
                double top = GetY(child);

                double halfWidth = child.DesiredSize.Width * 0.5;
                double halfHeight = child.DesiredSize.Height * 0.5;

                if (double.IsNaN(left) || double.IsNaN(top))
                {
                    left = halfWidth;
                    top = halfHeight;
                }

                //get the top left corner point
                _topLeft.X = Math.Min(_topLeft.X, left - halfWidth - Origo.X);
                _topLeft.Y = Math.Min(_topLeft.Y, top - halfHeight - Origo.Y);

                //calculate the bottom right corner point
                _bottomRight.X = Math.Max(_bottomRight.X, left + halfWidth - Origo.X);
                _bottomRight.Y = Math.Max(_bottomRight.Y, top + halfHeight - Origo.Y);
            }

            var graphSize = (Size)(_bottomRight - _topLeft);
            graphSize.Width = Math.Max(0, graphSize.Width);
            graphSize.Height = Math.Max(0, graphSize.Height);

            if (double.IsNaN(graphSize.Width) || double.IsNaN(graphSize.Height) ||
                 double.IsInfinity(graphSize.Width) || double.IsInfinity(graphSize.Height))
                return new Size(0, 0);

            return graphSize;
        }
        #endregion

        #region DP - Animation length

        /// <summary>
        /// Gets or sets the length of the animation.
        /// If the length of the animations is 0:0:0.000, 
        /// there won't be any animations.
        /// 
        /// Default value is 500 ms.
        /// </summary>
        public TimeSpan AnimationLength
        {
            get { return (TimeSpan)GetValue(AnimationLengthProperty); }
            set { SetValue(AnimationLengthProperty, value); }
        }

        public static readonly DependencyProperty AnimationLengthProperty =
            DependencyProperty.Register("AnimationLength", typeof(TimeSpan), typeof(GraphCanvas), new UIPropertyMetadata(new TimeSpan(0, 0, 0, 0, 500)));
        #endregion

        #region DP - CreationAnimation

        /// <summary>
        /// Gets or sets the animation controller for the 'Control Creation' animation.
        /// </summary>
        public ITransition CreationTransition
        {
            get { return (ITransition)GetValue(CreationTransitionProperty); }
            set { SetValue(CreationTransitionProperty, value); }
        }

        public static readonly DependencyProperty CreationTransitionProperty =
            DependencyProperty.Register("CreationTransition", typeof(ITransition), typeof(GraphCanvas), new UIPropertyMetadata(new FadeInTransition()));

        #endregion

        #region IsAnimationEnabled

        /// <summary>
        /// If this property is true, and the other animation disabler 
        /// properties are also true, the animation is enabled.
        /// If this is false, the animations will be disabled.
        /// </summary>
        public bool IsAnimationEnabled
        {
            get { return (bool)GetValue(IsAnimationEnabledProperty); }
            set { SetValue(IsAnimationEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsAnimationEnabledProperty =
            DependencyProperty.Register("IsAnimationEnabled", typeof(bool), typeof(GraphCanvas), new UIPropertyMetadata(true));

        #endregion

        #region DP - MoveAnimation

        /// <summary>
        /// Gets or sets the animation controller for the 'Control Moving' animation.
        /// </summary>
        public IAnimation MoveAnimation
        {
            get { return (IAnimation)GetValue(MoveAnimationProperty); }
            set { SetValue(MoveAnimationProperty, value); }
        }

        public static readonly DependencyProperty MoveAnimationProperty =
            DependencyProperty.Register("MoveAnimation", typeof(IAnimation), typeof(GraphCanvas), new UIPropertyMetadata(new SimpleMoveAnimation()));

        #endregion

        #region DP - DestructionAnimation

        /// <summary>
        /// Gets or sets the transition controller for the 'Control Descruction' animation.
        /// </summary>
        public ITransition DestructionTransition
        {
            get { return (ITransition)GetValue(DestructionTransitionProperty); }
            set { SetValue(DestructionTransitionProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DestructionAnimation.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DestructionTransitionProperty =
            DependencyProperty.Register("DestructionTransition", typeof(ITransition), typeof(GraphCanvas), new UIPropertyMetadata(new FadeOutTransition()));


        #endregion

        #region DP - Origo

        /// <summary>
        /// Gets or sets the virtual origo of the canvas.
        /// </summary>
        public Point Origo
        {
            get { return (Point)GetValue(OrigoProperty); }
            set { SetValue(OrigoProperty, value); }
        }

        public static readonly DependencyProperty OrigoProperty =
            DependencyProperty.Register("Origo", typeof(Point), typeof(GraphCanvas),
                new FrameworkPropertyMetadata(
                    new Point(),
                    FrameworkPropertyMetadataOptions.AffectsMeasure |
                    FrameworkPropertyMetadataOptions.AffectsArrange |
                    FrameworkPropertyMetadataOptions.AffectsRender |
                    FrameworkPropertyMetadataOptions.AffectsParentMeasure |
                    FrameworkPropertyMetadataOptions.AffectsParentArrange));

        #endregion

        public Vector Translation
        {
            get { return (Vector)GetValue(TranslationProperty); }
            protected set { SetValue(TranslationPropertyKey, value); }
        }

        public static readonly DependencyProperty TranslationProperty;
        protected static readonly DependencyPropertyKey TranslationPropertyKey =
            DependencyProperty.RegisterReadOnly("Translation", typeof(Vector), typeof(GraphCanvas), new UIPropertyMetadata(new Vector()));

        static GraphCanvas()
        {
            TranslationProperty = TranslationPropertyKey.DependencyProperty;
        }

        /// <summary>The layout process will be initialized with the current vertex positions.
        /// </summary>
        public virtual void ContinueLayout()
        {
        }

        /// <summary>The layout process will be started without initial vertex positions.</summary>
        public virtual void Relayout()
        {
        }

        private IAnimationContext _animationContext;

        /// <summary>Gets the context of the animation.</summary>
        public virtual IAnimationContext AnimationContext
        {
            get
            {
                if (_animationContext == null)
                    _animationContext = new AnimationContext(this);

                return _animationContext;
            }
        }

        /// <summary>Gets whether the animation could be run, or not.</summary>
        public virtual bool CanAnimate
        {
            get
            {
                return IsAnimationEnabled;
            }
        }

        /// <summary>Does a transition for the control which has been already added to this container.</summary>
        /// <param name="control">The control which has been added.</param>
        protected virtual void RunCreationTransition(Control control)
        {
            if (CreationTransition == null)
                return;

            CreationTransition.Run(AnimationContext, control, AnimationLength);
        }

        /// <summary>Animates the position of the given control to the given positions.</summary>
        /// <param name="control">The control which should be moved.</param>
        /// <param name="x">The new horizontal position of the control.</param>
        /// <param name="y">The new vertical position of the control.</param>
        protected virtual void RunMoveAnimation(Control control, double x, double y)
        {
            if (MoveAnimation != null && CanAnimate)
                //animate to the given position
                MoveAnimation.Animate(AnimationContext, control, x, y, AnimationLength);
            else
            {
                //cannot animate, only the given positions
                SetX(control, x);
                SetY(control, y);
            }
        }

        /// <summary>Transites a control which gonna' be removed from this container.</summary>
        /// <param name="control">The control which will be removed.</param>
        /// <param name="dontRemoveAfter">If it's true, the control won't be removed
        /// automatically from this container's Children.</param>
        protected virtual void RunDestructionTransition(Control control, bool dontRemoveAfter)
        {
            if (DestructionTransition == null)
            {
                if (!dontRemoveAfter)
                {
                    Children.Remove(control);
                }
            }
            else
            {
                if (dontRemoveAfter)
                    DestructionTransition.Run(AnimationContext, control, AnimationLength);
                else
                    DestructionTransition.Run(AnimationContext, control, AnimationLength, Children.Remove);
            }
        }
    }
}
