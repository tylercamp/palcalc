using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PalCalc.UI.View
{
    public class CachingContentPresenter : FrameworkElement
    {
        private readonly Dictionary<object, UIElement> _renderedViews = new();
        private object _currentContent;
        private UIElement _currentView;

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register(
                nameof(Content),
                typeof(object),
                typeof(CachingContentPresenter),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnContentChanged));

        public static readonly DependencyProperty ContentTemplateProperty =
            DependencyProperty.Register(
                nameof(ContentTemplate),
                typeof(DataTemplate),
                typeof(CachingContentPresenter),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, OnContentTemplateChanged));

        public object Content
        {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public DataTemplate ContentTemplate
        {
            get => (DataTemplate)GetValue(ContentTemplateProperty);
            set => SetValue(ContentTemplateProperty, value);
        }

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var presenter = (CachingContentPresenter)d;
            presenter.OnContentChanged(e.OldValue, e.NewValue);
        }

        protected virtual void OnContentChanged(object oldContent, object newContent)
        {
            if (_currentContent == newContent)
                return;

            _currentContent = newContent;
            InvalidateMeasure();
            InvalidateVisual();
        }

        private static void OnContentTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as CachingContentPresenter).OnContentTemplateChanged(e.OldValue, e.NewValue);
        }

        protected virtual void OnContentTemplateChanged(object oldTemplate, object newTemplate)
        {
            ClearAllCache();

            InvalidateMeasure();
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_currentContent == null)
                return new Size(0, 0);

            var renderedContent = GetOrCreateRenderedContent(_currentContent);
            renderedContent.Measure(availableSize);
            return renderedContent.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_currentContent == null)
                return finalSize;

            var renderedContent = GetOrCreateRenderedContent(_currentContent);
            renderedContent.Arrange(new Rect(finalSize));
            return finalSize;
        }

        protected override int VisualChildrenCount => _currentContent != null && _renderedViews.ContainsKey(_currentContent) ? 1 : 0;

        protected override Visual GetVisualChild(int index)
        {
            if (_currentContent == null || !_renderedViews.ContainsKey(_currentContent))
                throw new ArgumentOutOfRangeException(nameof(index));

            return _renderedViews[_currentContent];
        }

        private UIElement GetOrCreateRenderedContent(object content)
        {
            if (content == null || ContentTemplate == null)
                return null;

            if (!_renderedViews.TryGetValue(content, out var renderedView))
            {
                // Create a new view if not already cached
                var view = ContentTemplate.LoadContent() as UIElement;

                if (view is FrameworkElement frameworkElement)
                {
                    frameworkElement.DataContext = content;
                }

                if (view != null)
                {
                    _renderedViews[content] = view;
                }

                renderedView = view;
            }

            if (_currentView != renderedView)
            {
                if (_currentView != null)
                    RemoveVisualChild(_currentView);

                AddVisualChild(renderedView);
                _currentView = renderedView;
            }
            return renderedView;
        }

        public void ClearAllCache()
        {
            foreach (var view in _renderedViews.Values)
            {
                RemoveVisualChild(view);
            }

            _renderedViews.Clear();
        }
    }
}
