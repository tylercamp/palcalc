using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Threading;

namespace PalCalc.UI.ViewModel.Behaviors
{
    // https://stackoverflow.com/questions/28095177/wpf-expander-when-gridsplitter-used-to-manually-resize-row-row-does-not-prope/28095967
    class ExpanderGridRowResetBehavior : Behavior<Expander>
    {
        private DispatcherTimer _minHeightApplyTimer;
        private Grid _parentGrid;
        public int TargetGridRowIndex { get; set; }
        public double? ExpandedMinHeight { get; set; }

        // delay (in seconds) before applying the min-height after expanding; needed since the default expander
        // template has an animation
        public double ExpandedMinHeightDelay { get; set; } = 0.2 + 0.1;

        protected override void OnAttached()
        {
            AssociatedObject.Expanded += AssociatedObject_Expanded;
            AssociatedObject.Collapsed += AssociatedObject_Collapsed;
            FindParentGrid();

            AssociatedObject.VerticalAlignment = VerticalAlignment.Stretch;
            if (AssociatedObject.IsExpanded)
            {
                _parentGrid.RowDefinitions[TargetGridRowIndex].MinHeight = ExpandedMinHeight ?? 0;
                AssociatedObject.VerticalContentAlignment = VerticalAlignment.Stretch;
            }
        }

        private void FindParentGrid()
        {
            DependencyObject parent = LogicalTreeHelper.GetParent(AssociatedObject);
            while (parent != null)
            {
                if (parent is Grid)
                {
                    _parentGrid = parent as Grid;
                    return;
                }
                parent = LogicalTreeHelper.GetParent(AssociatedObject);
            }
        }

        void AssociatedObject_Collapsed(object sender, RoutedEventArgs e)
        {
            AssociatedObject.VerticalContentAlignment = VerticalAlignment.Top;
            _parentGrid.RowDefinitions[TargetGridRowIndex].Height = GridLength.Auto;

            if (_minHeightApplyTimer != null)
            {
                _minHeightApplyTimer.Tick -= _minHeightApplyTimer_Tick;
                _minHeightApplyTimer.Stop();
                _minHeightApplyTimer = null;
            }
            _parentGrid.RowDefinitions[TargetGridRowIndex].MinHeight = 0;
        }

        void AssociatedObject_Expanded(object sender, RoutedEventArgs e)
        {
            _parentGrid.RowDefinitions[TargetGridRowIndex].Height = GridLength.Auto;

            if (ExpandedMinHeight != null)
            {
                _minHeightApplyTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher);
                _minHeightApplyTimer.Tick += _minHeightApplyTimer_Tick;
                _minHeightApplyTimer.Interval = TimeSpan.FromSeconds(ExpandedMinHeightDelay);
                _minHeightApplyTimer.Start();
            }
        }

        private void _minHeightApplyTimer_Tick(object sender, EventArgs e)
        {
            var defaultLength = new GridLength(1, GridUnitType.Star);

            // GridSplitter resizing on Auto converts to a plain pixel size instead of star-lengths, which breaks MinHeight constraint
            //
            // reapply star-lengths so MinHeight is still obeyed
            var hasStarLengths = false;
            for (int i = 0; i < _parentGrid.RowDefinitions.Count; i++)
            {
                var def = _parentGrid.RowDefinitions[i];

                if (i == TargetGridRowIndex)
                {
                    // 2nd one is the main case; 1st handles "Jobs" panel edge-case

                    if (i == _parentGrid.RowDefinitions.Count - 1 && !hasStarLengths)
                        _parentGrid.RowDefinitions[i].Height = GridLength.Auto;
                    else
                        _parentGrid.RowDefinitions[i].Height = new GridLength(def.ActualHeight, GridUnitType.Star);
                }
                else if (def.Height.IsStar && def.Height != defaultLength)
                {
                    _parentGrid.RowDefinitions[i].Height = new GridLength(def.ActualHeight, GridUnitType.Star);
                    hasStarLengths = true;
                }
            }

            _parentGrid.RowDefinitions[TargetGridRowIndex].MinHeight = ExpandedMinHeight ?? 0;

            _minHeightApplyTimer.Stop();
            _minHeightApplyTimer.Tick -= _minHeightApplyTimer_Tick;
            _minHeightApplyTimer = null;

            Dispatcher.BeginInvoke(() => AssociatedObject.VerticalContentAlignment = VerticalAlignment.Stretch, DispatcherPriority.ApplicationIdle);
        }
    }
}
