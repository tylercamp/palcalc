using PalCalc.UI.Model;
using PalCalc.UI.Model.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace PalCalc.UI.View.Behaviors
{
    public static class GridLayoutBehavior
    {
        public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
            "Key",
            typeof(string),
            typeof(GridLayoutBehavior),
            new PropertyMetadata(null, KeyChanged)
        );

        public static readonly DependencyProperty VersionProperty = DependencyProperty.RegisterAttached(
            "Version",
            typeof(int),
            typeof(GridLayoutBehavior),
            new PropertyMetadata(1)
        );

        public static readonly DependencyProperty PrimaryColumnProperty = DependencyProperty.RegisterAttached(
            "PrimaryColumn",
            typeof(int),
            typeof(GridLayoutBehavior),
            new PropertyMetadata(-1)
        );

        public static readonly DependencyProperty PrimaryRowProperty = DependencyProperty.RegisterAttached(
            "PrimaryRow",
            typeof(int),
            typeof(GridLayoutBehavior),
            new PropertyMetadata(-1)
        );

        public static readonly DependencyProperty PrimaryMinimumProperty = DependencyProperty.RegisterAttached(
            "PrimaryMinimum",
            typeof(double),
            typeof(GridLayoutBehavior),
            new PropertyMetadata(0d)
        );

        private static readonly DependencyProperty HasRestoredProperty =
            DependencyProperty.RegisterAttached(
                "HasRestored",
                typeof(bool),
                typeof(GridLayoutBehavior),
                new PropertyMetadata(false)
            );

        public static string GetKey(DependencyObject obj) => (string)obj.GetValue(KeyProperty);
        public static void SetKey(DependencyObject obj, string value) => obj.SetValue(KeyProperty, value);

        public static int GetVersion(DependencyObject obj) => (int)obj.GetValue(VersionProperty);
        public static void SetVersion(DependencyObject obj, int value) => obj.SetValue(VersionProperty, value);

        public static int GetPrimaryColumn(DependencyObject obj) =>
            (int)obj.GetValue(PrimaryColumnProperty);
        public static void SetPrimaryColumn(DependencyObject obj, int value) =>
            obj.SetValue(PrimaryColumnProperty, value);

        public static int GetPrimaryRow(DependencyObject obj) => (int)obj.GetValue(PrimaryRowProperty);
        public static void SetPrimaryRow(DependencyObject obj, int value) =>
            obj.SetValue(PrimaryRowProperty, value);

        public static double GetPrimaryMinimum(DependencyObject obj) =>
            (double)obj.GetValue(PrimaryMinimumProperty);
        public static void SetPrimaryMinimum(DependencyObject obj, double value) =>
            obj.SetValue(PrimaryMinimumProperty, value);

        private static bool GetHasRestored(DependencyObject obj) =>
            (bool)obj.GetValue(HasRestoredProperty);
        private static void SetHasRestored(DependencyObject obj, bool value) =>
            obj.SetValue(HasRestoredProperty, value);

        private static void KeyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            if (obj is not Grid grid)
                throw new InvalidOperationException($"{nameof(GridLayoutBehavior)} can only be applied to a Grid");

            if (!string.IsNullOrWhiteSpace(e.OldValue as string))
            {
                grid.Loaded -= Grid_Loaded;
                grid.RemoveHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(Grid_DragCompleted));
                SetHasRestored(grid, false);
            }

            if (!string.IsNullOrWhiteSpace(e.NewValue as string))
            {
                grid.Loaded += Grid_Loaded;
                grid.AddHandler(
                    Thumb.DragCompletedEvent,
                    new DragCompletedEventHandler(Grid_DragCompleted)
                );
            }
        }

        private static void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            var grid = (Grid)sender;
            if (GetHasRestored(grid))
                return;

            SetHasRestored(grid, true);
            grid.Dispatcher.BeginInvoke(
                () =>
                {
                    if (!TryRestore(grid))
                        SetHasRestored(grid, false);
                },
                DispatcherPriority.Loaded
            );
        }

        private static bool TryRestore(Grid grid)
        {
            if (grid.ActualWidth <= 0 ||
                grid.ActualHeight <= 0 ||
                !UILayoutStore.TryGetGridLayout(GetKey(grid), out var saved) ||
                saved.Version != GetVersion(grid))
            {
                return false;
            }

            var primaryMinimum = GetPrimaryMinimum(grid);
            var columnConstraints = grid.ColumnDefinitions
                .Select(definition => new GridDefinitionConstraints(
                    definition.MinWidth,
                    definition.MaxWidth,
                    definition.ActualWidth
                ))
                .ToList();
            var rowConstraints = grid.RowDefinitions
                .Select(definition => new GridDefinitionConstraints(
                    definition.MinHeight,
                    definition.MaxHeight,
                    definition.ActualHeight
                ))
                .ToList();

            if (!UILayoutValidation.TryNormalizeGridLengths(
                    saved.Columns,
                    columnConstraints,
                    grid.ActualWidth,
                    GetPrimaryColumn(grid),
                    primaryMinimum,
                    out var columns
                ) ||
                !UILayoutValidation.TryNormalizeGridLengths(
                    saved.Rows,
                    rowConstraints,
                    grid.ActualHeight,
                    GetPrimaryRow(grid),
                    primaryMinimum,
                    out var rows
                ))
            {
                return false;
            }

            for (var i = 0; i < columns.Count; i++)
            {
                if (columns[i].Unit != LayoutGridUnit.Auto)
                    grid.ColumnDefinitions[i].Width = ToGridLength(columns[i]);
            }

            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].Unit != LayoutGridUnit.Auto)
                    grid.RowDefinitions[i].Height = ToGridLength(rows[i]);
            }

            return true;
        }

        private static void Grid_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            var grid = (Grid)sender;
            if (e.OriginalSource is not GridSplitter splitter ||
                FindNearestPersistedGrid(splitter) != grid)
            {
                return;
            }

            UILayoutStore.SaveGridLayout(
                GetKey(grid),
                new GridLayoutSettings
                {
                    Version = GetVersion(grid),
                    Columns = grid.ColumnDefinitions
                        .Select(definition => FromGridLength(definition.Width))
                        .ToList(),
                    Rows = grid.RowDefinitions
                        .Select(definition => FromGridLength(definition.Height))
                        .ToList(),
                }
            );
        }

        private static Grid FindNearestPersistedGrid(DependencyObject child)
        {
            var current = child;
            while (current != null)
            {
                current = VisualTreeHelper.GetParent(current);
                if (current is Grid grid && !string.IsNullOrWhiteSpace(GetKey(grid)))
                    return grid;
            }

            return null;
        }

        private static GridLengthSettings FromGridLength(GridLength length) => new()
        {
            Unit = length.GridUnitType switch
            {
                GridUnitType.Auto => LayoutGridUnit.Auto,
                GridUnitType.Pixel => LayoutGridUnit.Pixel,
                GridUnitType.Star => LayoutGridUnit.Star,
                _ => throw new ArgumentOutOfRangeException(),
            },
            Value = length.Value,
        };

        private static GridLength ToGridLength(GridLengthSettings length) =>
            new(
                length.Value,
                length.Unit switch
                {
                    LayoutGridUnit.Pixel => GridUnitType.Pixel,
                    LayoutGridUnit.Star => GridUnitType.Star,
                    _ => throw new ArgumentOutOfRangeException(),
                }
            );
    }
}
