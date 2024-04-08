using System.Collections.ObjectModel;
using System.Windows;
using GraphSharp.Algorithms.Layout.Compound;

namespace GraphSharp.Controls
{
    [TemplatePart(Name = PartInnerCanvas, Type = typeof(FrameworkElement))]
    public class CompoundVertexControl : VertexControl, ICompoundVertexControl
    {
        protected const string PartInnerCanvas = "PART_InnerCanvas";

        protected FrameworkElement InnerCanvas;

        private bool _activePositionChangeReaction;

        /// <summary>Gets the control of the inner canvas.</summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // get the control of the inner canvas
            InnerCanvas = Template.FindName(PartInnerCanvas,this) as FrameworkElement ?? this;
        }

        #region Dependency Properties

        public ObservableCollection<VertexControl> Vertices
        {
            get { return (ObservableCollection<VertexControl>)GetValue(VerticesProperty); }
            protected set { SetValue(VerticesPropertyKey, value); }
        }

        public static readonly DependencyProperty VerticesProperty;
        protected static readonly DependencyPropertyKey VerticesPropertyKey =
            DependencyProperty.RegisterReadOnly("Vertices", typeof(ObservableCollection<VertexControl>), typeof(CompoundVertexControl), new UIPropertyMetadata(null));

        public CompoundVertexInnerLayoutType LayoutMode
        {
            get { return (CompoundVertexInnerLayoutType)GetValue(LayoutModeProperty); }
            set { SetValue(LayoutModeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LayoutMode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LayoutModeProperty =
            DependencyProperty.Register("LayoutMode", typeof(CompoundVertexInnerLayoutType), typeof(CompoundVertexControl), new UIPropertyMetadata(CompoundVertexInnerLayoutType.Automatic));

        public bool IsExpanded
        {
            get { return (bool)GetValue(IsExpandedProperty); }
            set { SetValue(IsExpandedProperty, value); }
        }

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register("IsExpanded", typeof(bool), typeof(CompoundVertexControl), new UIPropertyMetadata(true, OnIsExpandedPropertyChanged));

        private static void OnIsExpandedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var compoundVertexControl = (CompoundVertexControl)d;
            if ((bool)e.NewValue)
            {
                compoundVertexControl.RaiseEvent(new RoutedEventArgs(ExpandedEvent, compoundVertexControl));
            }
            else
            {
                compoundVertexControl.RaiseEvent(new RoutedEventArgs(CollapsedEvent, compoundVertexControl));
            }
        }

        public Point InnerCanvasOrigo
        {
            get { return (Point)GetValue(InnerCanvasOrigoProperty); }
            set { SetValue(InnerCanvasOrigoProperty, value); }
        }

        // Using a DependencyProperty as the backing store for InnerCanvasOrigo.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty InnerCanvasOrigoProperty =
            DependencyProperty.Register("InnerCanvasOrigo", typeof(Point), typeof(CompoundVertexControl), new UIPropertyMetadata(new Point()));

        static CompoundVertexControl()
        {
            //readonly DPs
            VerticesProperty = VerticesPropertyKey.DependencyProperty;

            //override the StyleKey Property
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CompoundVertexControl), new FrameworkPropertyMetadata(typeof(CompoundVertexControl)));

            //register a class handler for the GraphCanvas.PositionChanged routed event
            EventManager.RegisterClassHandler(typeof(CompoundVertexControl), GraphCanvas.PositionChangedEvent, new PositionChangedEventHandler(OnPositionChanged));
        }

        private static void OnPositionChanged(object sender, PositionChangedEventArgs args)
        {
            var compoundVertexControl = args.Source as CompoundVertexControl;
            if (compoundVertexControl == null || compoundVertexControl._activePositionChangeReaction)
                return;

            compoundVertexControl._activePositionChangeReaction = true;

            //we are moving the compound vertex itself
            if (compoundVertexControl == args.OriginalSource)
            {
                //move the children with the same amount
                foreach (var childVertexControl in compoundVertexControl.Vertices)
                {
                    GraphCanvas.SetX(childVertexControl, GraphCanvas.GetX(childVertexControl) + args.XChange);
                    GraphCanvas.SetY(childVertexControl, GraphCanvas.GetY(childVertexControl) + args.YChange);
                }
            }
            else
            {
                //we are moving the parent or one of it's child
                var childVertexControl = args.OriginalSource as VertexControl;
                if (childVertexControl == null)
                    return;
                if (compoundVertexControl.Vertices.Contains(childVertexControl))
                {
                    //update the position of all child vertices
                    foreach (var cvc in compoundVertexControl.Vertices)
                    {
                        if (cvc == childVertexControl)
                            continue;
                        var childCenterPos = new Point(cvc.ActualWidth / 2, cvc.ActualHeight / 2);
                        var translatedChildCenterPos = cvc.TranslatePoint(childCenterPos, cvc.RootCanvas);
                        GraphCanvas.SetX(cvc, translatedChildCenterPos.X - cvc.RootCanvas.Translation.X);
                        GraphCanvas.SetY(cvc, translatedChildCenterPos.Y - cvc.RootCanvas.Translation.Y);
                    }
                }
            }

            compoundVertexControl._activePositionChangeReaction = false;
        }

        public CompoundVertexControl()
        {
            Vertices = new ObservableCollection<VertexControl>();
        }

        #endregion

        #region Routed Events
        public static readonly RoutedEvent ExpandedEvent =
            EventManager.RegisterRoutedEvent("Expanded", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CompoundVertexControl));

        public event RoutedEventHandler Expanded
        {
            add { AddHandler(ExpandedEvent, value); }
            remove { RemoveHandler(ExpandedEvent, value); }
        }

        public static readonly RoutedEvent CollapsedEvent =
            EventManager.RegisterRoutedEvent("Collapsed", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CompoundVertexControl));

        public event RoutedEventHandler Collapsed
        {
            add { AddHandler(CollapsedEvent, value); }
            remove { RemoveHandler(CollapsedEvent, value); }
        }
        #endregion

        #region ICompoundVertexControl Members

        /// <summary>Gets the size of the inner canvas control.</summary>
        public Size InnerCanvasSize
        {
            get
            {
                if (InnerCanvas == null)
                    return new Size();

                return new Size(InnerCanvas.ActualWidth, InnerCanvas.ActualHeight);
            }
        }

        /// <summary>Gets the 'borderthickness' of the control around the inner canvas.</summary>
        public Thickness VertexBorderThickness
        {
            get
            {
                var thickness = new Thickness();
                if (InnerCanvas == null)
                    return thickness;

                var innerCanvasPosition = InnerCanvas.TranslatePoint(new Point(), this);
                var innerCanvasSize = InnerCanvasSize;
                var size = new Size(ActualWidth, ActualHeight);

                //calculate the thickness
                thickness.Left = innerCanvasPosition.X;
                thickness.Top = innerCanvasPosition.Y;
                thickness.Right = size.Width - (innerCanvasPosition.X + innerCanvasSize.Width);
                thickness.Bottom = size.Height - (innerCanvasPosition.Y + innerCanvasSize.Height);

                return thickness;
            }
        }

        #endregion
    }
}
