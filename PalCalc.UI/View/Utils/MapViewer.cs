﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using System.Globalization;
using System.Windows.Data;
using Serilog;

namespace PalCalc.UI.View.Utils
{
    // (ty chatgpt)

    public class InverseDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double d = (double)value;
            return (Math.Abs(d) < 0.000001) ? 0.0 : 1.0 / d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// A custom control that displays a map image (BackgroundImage)
    /// and items laid out in the map's coordinate space.
    /// Supports panning & zooming with bounds-checking.
    /// </summary>
    public class MapViewer : ItemsControl
    {
        private const string PartContainer = "PART_Container";

        // The container that will hold the background and items
        private FrameworkElement _container;

        // We'll use separate transforms: Scale, then Translate.
        private readonly ScaleTransform _scaleTransform = new ScaleTransform(1.0, 1.0);
        private readonly TranslateTransform _translateTransform = new TranslateTransform(0, 0);
        private TransformGroup _transformGroup;

        // Keep track of mouse-dragging state
        private Point _lastMousePosition;
        private bool _isPanning;

        // We store the "natural" width/height of the map image as soon as it's known
        private Size _imageNaturalSize = Size.Empty;

        internal ScaleTransform ViewScaleTransform => _scaleTransform;

        #region Dependency Properties

        /// <summary>
        /// The map (image) to be displayed as the background.
        /// </summary>
        public ImageSource BackgroundImage
        {
            get => (ImageSource)GetValue(BackgroundImageProperty);
            set => SetValue(BackgroundImageProperty, value);
        }

        public static readonly DependencyProperty BackgroundImageProperty =
            DependencyProperty.Register(
                nameof(BackgroundImage),
                typeof(ImageSource),
                typeof(MapViewer),
                new PropertyMetadata(null, OnBackgroundImageChanged));

        private static void OnBackgroundImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // We might need to update bounding or measure
            var control = (MapViewer)d;
            control.UpdateLayout(); // or InvalidateVisual();
        }

        /// <summary>
        /// An optional property to set the ItemTemplate for all items.
        /// Alternatively, you could use ItemTemplateSelector or custom logic.
        /// </summary>
        new public DataTemplate ItemTemplate
        {
            get => (DataTemplate)GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        new public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register(
                nameof(ItemTemplate),
                typeof(DataTemplate),
                typeof(MapViewer),
                new PropertyMetadata(null));

        public double InitialZoom
        {
            get => (double)GetValue(InitialZoomProperty);
            set => SetValue(InitialZoomProperty, value);
        }

        public static readonly DependencyProperty InitialZoomProperty =
            DependencyProperty.Register(nameof(InitialZoom), typeof(double), typeof(MapViewer),
                new PropertyMetadata(1.0, OnInitialPropertyChanged));

        private static void OnInitialPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MapViewer viewer)
            {
                viewer.ApplyInitialState();
            }
        }

        public double InitialX
        {
            get => (double)GetValue(InitialXProperty);
            set => SetValue(InitialXProperty, value);
        }

        public static readonly DependencyProperty InitialXProperty =
            DependencyProperty.Register(nameof(InitialX), typeof(double), typeof(MapViewer),
                new PropertyMetadata(0.0, OnInitialPropertyChanged));

        public double InitialY
        {
            get => (double)GetValue(InitialYProperty);
            set => SetValue(InitialYProperty, value);
        }

        public static readonly DependencyProperty InitialYProperty =
            DependencyProperty.Register(nameof(InitialY), typeof(double), typeof(MapViewer),
                new PropertyMetadata(0.0, OnInitialPropertyChanged));

        #endregion

        private void ApplyInitialState()
        {
            // Make sure we have a valid container & image size
            if (_container == null || _container.ActualWidth == 0 || _container.ActualHeight == 0 || _imageNaturalSize.IsEmpty) return;

            // Clamp the zoom to a desired range
            double zoom = Math.Max(0.01, Math.Min(InitialZoom, 10.0));
            _scaleTransform.ScaleX = zoom;
            _scaleTransform.ScaleY = zoom;

            double scaleX = _scaleTransform.ScaleX * (_container.ActualWidth / _imageNaturalSize.Width);
            double scaleY = _scaleTransform.ScaleY * (_container.ActualHeight / _imageNaturalSize.Height);

            double mapWidth = _imageNaturalSize.Width * scaleX;
            double mapHeight = _imageNaturalSize.Height * scaleY;
            double viewportWidth = _container.ActualWidth;
            double viewportHeight = _container.ActualHeight;

            _translateTransform.X = viewportWidth / 2 - (InitialX * mapWidth);
            _translateTransform.Y = viewportHeight / 2 - (InitialY * mapHeight);

            ClampTranslation();
        }


        static MapViewer()
        {
            // By default, this control looks for its Style in Themes/Generic.xaml
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(MapViewer),
                new FrameworkPropertyMetadata(typeof(MapViewer)));
        }

        public MapViewer()
        {
            // Create transform group: scale, then translate
            _transformGroup = new TransformGroup();
            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);

            ClipToBounds = true;

            // We'll handle mouse events for panning/zooming
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Hook up mouse events on the main container once loaded
            if (_container != null)
            {
                _container.MouseWheel += OnMouseWheel;
                _container.MouseLeftButtonDown += OnMouseLeftButtonDown;
                _container.MouseLeftButtonUp += OnMouseLeftButtonUp;
                _container.MouseMove += OnMouseMove;
                _container.MouseLeave += OnMouseLeave;
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // PART_Container is the root element in the control template
            _container = GetTemplateChild(PartContainer) as FrameworkElement;
            if (_container != null)
            {
                ApplyInitialState();
                _container.RenderTransform = _transformGroup;
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            // We want each item to be a ContentPresenter with a specific Panel
            return new ContentPresenter();
        }

        protected override bool IsItemItsOwnContainerOverride(object item)
        {
            // If it's already a UIElement, we won't wrap it again
            return item is UIElement;
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            if (element is ContentPresenter presenter)
            {
                // (Optional) apply your existing template if needed
                presenter.ContentTemplate = ItemTemplate;

                // Create a ScaleTransform that inverts the parent's current scale
                var st = new ScaleTransform();
                var group = new TransformGroup();
                group.Children.Add(st);

                // Example binding (assuming you create the converter in code or XAML resource).
                // This binds to the viewer’s _scaleTransform.ScaleX/ScaleY.
                // Replace "local:InverseDoubleConverter" with the converter resource you’ve defined.
                BindingOperations.SetBinding(
                    st,
                    ScaleTransform.ScaleXProperty,
                    new Binding("ScaleX")
                    {
                        Source = _scaleTransform,
                        Converter = new InverseDoubleConverter()
                    });

                BindingOperations.SetBinding(
                    st,
                    ScaleTransform.ScaleYProperty,
                    new Binding("ScaleY")
                    {
                        Source = _scaleTransform,
                        Converter = new InverseDoubleConverter()
                    });

                // Assign the RenderTransform to the container
                presenter.RenderTransform = group;

                // If you'd like items anchored by their center, also set:
                presenter.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }


        #region Zoom/Pan Logic

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            if (_container == null || _imageNaturalSize.IsEmpty)
                return;

            // Zoom in or out
            const double zoomFactor = 1.2;
            double oldScale = _scaleTransform.ScaleX;
            double newScale = (e.Delta > 0)
                ? oldScale * zoomFactor
                : oldScale / zoomFactor;

            // Limit min/max scale if desired
            double maxScale = Math.Min(
                _imageNaturalSize.Width / _container.ActualWidth,
                _imageNaturalSize.Height / _container.ActualHeight
            );
            newScale = Math.Max(1, Math.Min(newScale, maxScale));

            // 1) Get mouse pos in the *parent's* coordinate space (e.g. MapViewer)
            //    Instead of _container, so we're not affected by the container's own transform.
            var parent = VisualTreeHelper.GetParent(_container) as FrameworkElement;
            if (parent == null) return;

            Point mousePosParent = e.GetPosition(parent);

            // 2) Convert mousePosParent into the map coordinate system
            //    by using the container's current transform to parent, then invert it.
            var currentMatrix = _container.TransformToAncestor(parent);
            Point mapPos = currentMatrix.Inverse.Transform(mousePosParent);

            // 3) Update the scale
            _scaleTransform.ScaleX = newScale;
            _scaleTransform.ScaleY = newScale;

            // 4) Get the new transform after scaling, then transform the same map point forward
            var newMatrix = _container.TransformToAncestor(parent);
            Point newMousePos = newMatrix.Transform(mapPos);

            // 5) Offset the container so the mouse remains in the same on-screen position
            _translateTransform.X += (mousePosParent.X - newMousePos.X);
            _translateTransform.Y += (mousePosParent.Y - newMousePos.Y);

            ClampTranslation();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_container == null) return;

            _isPanning = true;
            _lastMousePosition = e.GetPosition(this);
            _container.CaptureMouse();

            Cursor = Cursors.SizeAll;

            e.Handled = true;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_container == null) return;

            _isPanning = false;
            _container.ReleaseMouseCapture();

            Cursor = null;

            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning || _container == null) return;

            var currentPos = e.GetPosition(this);
            var dx = currentPos.X - _lastMousePosition.X;
            var dy = currentPos.Y - _lastMousePosition.Y;

            _lastMousePosition = currentPos;

            // Move
            _translateTransform.X += dx;
            _translateTransform.Y += dy;

            InvalidateVisual();
            ClampTranslation();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            // If the mouse leaves the control while panning,
            // end the panning operation
            if (_isPanning && _container != null)
            {
                _isPanning = false;
                _container.ReleaseMouseCapture();
            }
        }

        private static ILogger logger = Log.ForContext<MapViewer>();

        /// <summary>
        /// Prevents the user from panning the map outside the control’s visible area.
        /// (i.e., clamps the translation to keep the map image fully in view)
        /// </summary>
        private void ClampTranslation()
        {
            if (_container == null || _imageNaturalSize.IsEmpty)
                return;

            double scaleX = _scaleTransform.ScaleX * (_container.ActualWidth / _imageNaturalSize.Width);
            double scaleY = _scaleTransform.ScaleY * (_container.ActualHeight / _imageNaturalSize.Height); // if it's uniform, both are same

            double mapWidth = _imageNaturalSize.Width * scaleX;
            double mapHeight = _imageNaturalSize.Height * scaleY;

            double viewportWidth = _container.ActualWidth;
            double viewportHeight = _container.ActualHeight;

            // If the map is smaller than the viewport, center it
            // Otherwise, clamp so we don't drag outside
            if (mapWidth <= viewportWidth)
            {
                // center horizontally
                _translateTransform.X = (viewportWidth - mapWidth) / 2.0;
            }
            else
            {
                double minX = viewportWidth - mapWidth;
                double maxX = 0;
                _translateTransform.X = Math.Min(Math.Max(_translateTransform.X, minX), maxX);
            }

            if (mapHeight <= viewportHeight)
            {
                // center vertically
                _translateTransform.Y = (viewportHeight - mapHeight) / 2.0;
            }
            else
            {
                double minY = viewportHeight - mapHeight;
                double maxY = 0;
                _translateTransform.Y = Math.Min(Math.Max(_translateTransform.Y, minY), maxY);
            }
        }

        #endregion

        /// <summary>
        /// Called from the MapPanel or background image once its size is known,
        /// so we can clamp properly.
        /// </summary>
        internal void SetImageNaturalSize(Size size)
        {
            if (!size.IsEmpty && size != _imageNaturalSize)
            {
                _imageNaturalSize = size;
                ApplyInitialState();
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            ApplyInitialState();
        }

    }
}
