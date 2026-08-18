using GraphSharp.Controls;
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Solver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PalCalc.UI.View.Main
{
    /// <summary>
    /// Interaction logic for BreedingResultView.xaml
    /// </summary>
    public partial class BreedingResultView : UserControl
    {
        private const double CacheRefreshZoomRatio = 1.25;

        private readonly Queue<VertexControl> pendingCacheRefresh = new();
        private bool isCacheRefreshActive;
        private double pendingZoom;
        private double pendingRenderScale;
        private BitmapScalingMode pendingScalingMode;

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(
                nameof(IsReadOnly),
                typeof(bool),
                typeof(BreedingResultView),
                new PropertyMetadata(false));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty DisplayedResultProperty =
            DependencyProperty.Register(
                nameof(DisplayedResult),
                typeof(BreedingResultViewModel),
                typeof(BreedingResultView),
                new PropertyMetadata(null, DisplayedResultChanged));

        private static void DisplayedResultChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ((BreedingResultView)sender).StopCacheRefresh();
        }

        public BreedingResultViewModel DisplayedResult
        {
            get => (BreedingResultViewModel)GetValue(DisplayedResultProperty);
            set => SetValue(DisplayedResultProperty, value);
        }

        public BreedingResultView()
        {
            InitializeComponent();
            Loaded += BreedingResultView_Loaded;
            Unloaded += BreedingResultView_Unloaded;
        }

        private void BreedingResultView_Loaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering += RefreshCaches;
        }

        private void BreedingResultView_Unloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= RefreshCaches;
            StopCacheRefresh();
        }

        private void GraphZoom_ZoomSettled(object sender, EventArgs e)
        {
            StartCacheRefresh();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);

            if (IsLoaded)
                StartCacheRefresh();
        }

        private void StartCacheRefresh()
        {
            StopCacheRefresh();

            var dpiScale = VisualTreeHelper.GetDpi(GraphLayout).DpiScaleX;
            var qualityScale = Math.Clamp(dpiScale, 1.0, 2.0);

            pendingZoom = GraphZoom.Zoom;
            pendingRenderScale = Math.Clamp(pendingZoom, 0.05, 3.0) * qualityScale;
            pendingScalingMode = dpiScale <= 1.001
                ? BitmapScalingMode.NearestNeighbor
                : BitmapScalingMode.Linear;

            foreach (var vertex in GraphLayout.Children
                         .OfType<VertexControl>()
                         .OrderByDescending(IsOnScreen)
                         .ThenByDescending(CacheScaleDifference))
                pendingCacheRefresh.Enqueue(vertex);

            isCacheRefreshActive = pendingCacheRefresh.Count > 0;
        }

        private void RefreshCaches(object sender, EventArgs e)
        {
            if (pendingZoom > 0 && ZoomRatio(GraphZoom.Zoom, pendingZoom) >= CacheRefreshZoomRatio)
                StartCacheRefresh();

            if (!isCacheRefreshActive)
                return;

            if (pendingCacheRefresh.TryDequeue(out var vertex))
            {
                RefreshCache(vertex);
            }

            if (pendingCacheRefresh.Count == 0)
                StopCacheRefresh();
        }

        private bool IsOnScreen(VertexControl vertex)
        {
            if (!vertex.IsVisible || vertex.ActualWidth <= 0 || vertex.ActualHeight <= 0)
                return false;

            try
            {
                var bounds = vertex.TransformToAncestor(GraphZoom)
                    .TransformBounds(new Rect(vertex.RenderSize));
                return bounds.IntersectsWith(new Rect(GraphZoom.RenderSize));
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private double CacheScaleDifference(VertexControl vertex)
        {
            return vertex.CacheMode is BitmapCache { RenderAtScale: > 0 } cache
                ? ZoomRatio(cache.RenderAtScale, pendingRenderScale)
                : double.PositiveInfinity;
        }

        private static double ZoomRatio(double first, double second)
        {
            return Math.Max(first / second, second / first);
        }

        private void RefreshCache(VertexControl vertex)
        {
            RenderOptions.SetBitmapScalingMode(vertex, pendingScalingMode);

            if (vertex.CacheMode is not BitmapCache cache || cache.IsFrozen)
            {
                cache = new BitmapCache
                {
                    EnableClearType = true,
                    SnapsToDevicePixels = true,
                };
                vertex.CacheMode = cache;
            }

            if (Math.Abs(cache.RenderAtScale - pendingRenderScale) > 0.001)
                cache.RenderAtScale = pendingRenderScale;
        }

        private void StopCacheRefresh()
        {
            isCacheRefreshActive = false;
            pendingCacheRefresh.Clear();
        }
    }
}
