using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.View.Utils
{
    // (ty chatgpt)

    public class MapPanel : Panel
    {
        // Attached properties to indicate the position in the map's coordinate space
        public static double GetMapNormX(DependencyObject obj) => (double)obj.GetValue(MapNormXProperty);
        public static void SetMapNormX(DependencyObject obj, double value) => obj.SetValue(MapNormXProperty, value);

        public static readonly DependencyProperty MapNormXProperty =
            DependencyProperty.RegisterAttached("MapNormX", typeof(double), typeof(MapPanel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsArrange));

        public static double GetMapNormY(DependencyObject obj) => (double)obj.GetValue(MapNormYProperty);
        public static void SetMapNormY(DependencyObject obj, double value) => obj.SetValue(MapNormYProperty, value);

        public static readonly DependencyProperty MapNormYProperty =
            DependencyProperty.RegisterAttached("MapNormY", typeof(double), typeof(MapPanel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsArrange));

        // The parent MapViewer will bind its BackgroundImage here
        public ImageSource ImageSource
        {
            get => (ImageSource)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(
                nameof(ImageSource),
                typeof(ImageSource),
                typeof(MapPanel),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

        // We'll maintain a single ImageDrawing for performance (optional)
        private ImageDrawing _imageDrawing;

        protected override Size MeasureOverride(Size availableSize)
        {
            // Measure children (no constraints).
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }

            // Ignore the image's natural pixel size for the panel's layout footprint; 
            // instead, fill the parent’s available size (prevents “white borders”).
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var imageSize = Size.Empty;

            // Let the background image fill the panel's entire rect
            // (its "natural" size from Measure will become finalSize).
            if (ImageSource is BitmapSource bmp)
            {
                // Let the parent know the real image size for clamp logic:
                imageSize = new Size(bmp.PixelWidth, bmp.PixelHeight);
                if (this.TemplatedParent is MapViewer viewer)
                {
                    viewer.SetImageNaturalSize(imageSize);
                }
            }

            // Position each child based on MapX/MapY
            foreach (UIElement child in InternalChildren)
            {
                double x = GetMapNormX(child) * imageSize.Width;
                double y = GetMapNormY(child) * imageSize.Height;

                x *= finalSize.Width / imageSize.Width;
                y *= finalSize.Height / imageSize.Height;

                // We measure child as its own DesiredSize
                double childWidth = child.DesiredSize.Width;
                double childHeight = child.DesiredSize.Height;

                // Arrange at (x, y)
                child.Arrange(new Rect(new Point(x - childWidth/2, y - childHeight/2), new Size(childWidth, childHeight)));
            }

            return finalSize;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (ImageSource != null)
            {
                // If it's a BitmapSource, draw at 0,0 with its natural size
                var rect = new Rect(0, 0, RenderSize.Width, RenderSize.Height);
                dc.DrawImage(ImageSource, rect);
            }
        }
    }
}