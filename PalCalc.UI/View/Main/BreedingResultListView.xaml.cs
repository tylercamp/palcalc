using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Solver;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace PalCalc.UI.View.Main
{
    /// <summary>
    /// Interaction logic for BreedingResultListView.xaml
    /// </summary>
    public partial class BreedingResultListView : ListView
    {
        private Dictionary<string, GridViewColumn> columnsByKey = new();
        private List<string> originalColumnOrder = new();
        private bool isLoadingSettings = false;
        private bool isApplyingUserPreferences = false;

        public BreedingResultListView()
        {
            InitializeComponent();

            SetResourceReference(StyleProperty, typeof(ListView));

            Wpf.Util.GridViewSort.ApplySort(Items, nameof(BreedingResultViewModel.TimeEstimate));

            Loaded += BreedingResultListView_Loaded;
            DataContextChanged += BreedingResultListView_DataContextChanged;
        }

        private void BreedingResultListView_Loaded(object sender, RoutedEventArgs e)
        {
            if (View is GridView gridView)
            {
                InitializeColumns(gridView);
                LoadColumnSettings();
                AttachColumnHeaderContextMenu(gridView);
            }
        }

        private void BreedingResultListView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;

            if (e.NewValue is INotifyPropertyChanged newVm)
                newVm.PropertyChanged += ViewModel_PropertyChanged;

            Dispatcher.BeginInvoke(UpdateAllColumnWidths, DispatcherPriority.Loaded);
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // When Results change, the ViewModel recalculates all width properties
            // We need to update column widths after that happens
            if (e.PropertyName == nameof(BreedingResultListViewModel.Results))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateAllColumnWidths();
                }), DispatcherPriority.Loaded);
            }
        }

        private BreedingResultListColumnSettings GetOrCreateColumnSettings()
        {
            if (AppSettings.Current == null) return null;

            AppSettings.Current.BreedingResultListColumns ??= new BreedingResultListColumnSettings();
            return AppSettings.Current.BreedingResultListColumns;
        }

        private void UpdateAllColumnWidths()
        {
            if (isApplyingUserPreferences || View is not GridView gridView)
                return;

            var settings = GetOrCreateColumnSettings();
            if (settings == null) return;

            var vm = DataContext as BreedingResultListViewModel;

            isApplyingUserPreferences = true;
            try
            {
                foreach (var kvp in columnsByKey)
                {
                    var columnKey = kvp.Key;
                    var column = kvp.Value;

                    // Check if user has a preference for this column
                    if (settings.ColumnVisibility.TryGetValue(columnKey, out var userPreference) && !userPreference)
                    {
                        // User preference takes precedence
                        column.Width = 0;
                    }
                    else if (vm != null)
                    {
                        // No user preference - use ViewModel's calculated width
                        var vmWidth = GetViewModelWidthForColumn(vm, columnKey);
                        column.Width = vmWidth;
                    }
                }
            }
            finally
            {
                isApplyingUserPreferences = false;
            }
        }

        private double GetViewModelWidthForColumn(BreedingResultListViewModel vm, string columnKey)
        {
            return columnKey switch
            {
                "TimeEstimate" => vm.EffortWidth,
                "NumBreedingSteps" => vm.NumStepsWidth,
                "NumEggs" => vm.NumEggsWidth,
                "EffectivePassives" => vm.PassiveSkillsWidth,
                "InputLocations" => vm.LocationsWidth,
                "IV_HP" or "IV_Attack" or "IV_Defense" or "IV_Average" => vm.IVsWidth,
                _ => double.NaN // Default to auto-width
            };
        }

        private void InitializeColumns(GridView gridView)
        {
            columnsByKey.Clear();
            originalColumnOrder.Clear();

            var columns = gridView.Columns.ToList();
            for (int i = 0; i < columns.Count; i++)
            {
                var column = columns[i];
                var key = Wpf.Util.GridViewSort.GetPropertyName(column);
                columnsByKey[key] = column;
                originalColumnOrder.Add(key);

                // Clear bindings - we'll manage column widths directly
                BindingOperations.ClearBinding(column, GridViewColumn.WidthProperty);
            }
        }

        private void AttachColumnHeaderContextMenu(GridView gridView)
        {
            var headerStyle = new Style(typeof(GridViewColumnHeader), FindResource(typeof(GridViewColumnHeader)) as Style);

            headerStyle.Setters.Add(new EventSetter(GridViewColumnHeader.MouseRightButtonUpEvent,
                new MouseButtonEventHandler(ColumnHeader_RightClick)));

            gridView.ColumnHeaderContainerStyle = headerStyle;
        }

        private void ColumnHeader_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not GridViewColumnHeader || View is not GridView gridView)
                return;

            var contextMenu = new ContextMenu();

            foreach (var (columnKey, column) in columnsByKey.OrderBy(kvp => gridView.Columns.IndexOf(kvp.Value)))
            {
                var isVisible = double.IsNaN(column.Width) || column.Width > 0;

                var menuItem = new MenuItem
                {
                    Header = column.Header?.ToString(),
                    IsCheckable = true,
                    IsChecked = isVisible
                };

                menuItem.Click += (s, args) => ToggleColumnVisibility(columnKey, column, !isVisible);
                contextMenu.Items.Add(menuItem);
            }

            contextMenu.Items.Add(new Separator());

            var resetMenuItem = new MenuItem { Header = LocalizationCodes.LC_RESULT_COLUMNS_RESET.Bind().Value };
            resetMenuItem.Click += (s, args) => ResetAllColumns();
            contextMenu.Items.Add(resetMenuItem);

            contextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void ResetAllColumns()
        {
            if (View is not GridView gridView) return;

            var settings = GetOrCreateColumnSettings();
            if (settings == null) return;

            var vm = DataContext as BreedingResultListViewModel;

            settings.ColumnVisibility.Clear();
            settings.ColumnOrder.Clear();

            isApplyingUserPreferences = true;
            isLoadingSettings = true;
            try
            {
                // Restore original column order with all columns visible
                gridView.Columns.Clear();
                foreach (var key in originalColumnOrder)
                {
                    if (columnsByKey.TryGetValue(key, out var column))
                    {
                        gridView.Columns.Add(column);
                        // Force all columns visible (stock/default state)
                        column.Width = vm != null ? GetViewModelWidthForColumn(vm, Wpf.Util.GridViewSort.GetPropertyName(column)) : double.NaN;
                    }
                }
            }
            finally
            {
                isApplyingUserPreferences = false;
                isLoadingSettings = false;
            }

            Storage.SaveAppSettings(AppSettings.Current);
        }

        private void ToggleColumnVisibility(string columnKey, GridViewColumn column, bool isVisible)
        {
            var settings = GetOrCreateColumnSettings();
            if (settings == null) return;

            // Store user preference
            settings.ColumnVisibility[columnKey] = isVisible;

            // Set width directly
            column.Width = isVisible ? double.NaN : 0;

            // Save immediately (bypassing guards since this is a direct user action)
            Storage.SaveAppSettings(AppSettings.Current);
        }

        private void LoadColumnSettings()
        {
            if (View is not GridView gridView) return;

            var settings = GetOrCreateColumnSettings();
            if (settings == null) return;

            isLoadingSettings = true;
            try
            {
                // Apply user visibility preferences (and ViewModel widths for columns without preferences)
                UpdateAllColumnWidths();

                // Restore column order
                if (settings.ColumnOrder.Count > 0)
                {
                    var orderedColumns = settings.ColumnOrder
                        .Where(key => columnsByKey.ContainsKey(key))
                        .Select(key => columnsByKey[key])
                        .ToList();

                    var remainingColumns = gridView.Columns
                        .Cast<GridViewColumn>()
                        .Where(col => !orderedColumns.Contains(col))
                        .ToList();

                    gridView.Columns.Clear();

                    foreach (var col in orderedColumns)
                    {
                        gridView.Columns.Add(col);
                    }

                    foreach (var col in remainingColumns)
                    {
                        gridView.Columns.Add(col);
                    }
                }
            }
            finally
            {
                isLoadingSettings = false;
            }

            AttachColumnReorderHandler(gridView);
        }

        private void AttachColumnReorderHandler(GridView gridView)
        {
            ((INotifyCollectionChanged)gridView.Columns).CollectionChanged +=
                (s, e) =>
                {
                    if (!isLoadingSettings && !isApplyingUserPreferences)
                    {
                        SaveColumnSettings();
                    }
                };
        }

        private void SaveColumnSettings()
        {
            if (isLoadingSettings || isApplyingUserPreferences || View is not GridView gridView)
                return;

            var settings = GetOrCreateColumnSettings();
            if (settings == null) return;

            // Save column order
            settings.ColumnOrder.Clear();
            foreach (GridViewColumn column in gridView.Columns)
            {
                var key = columnsByKey.FirstOrDefault(kvp => kvp.Value == column).Key;
                if (key != null)
                {
                    settings.ColumnOrder.Add(key);
                }
            }

            Storage.SaveAppSettings(AppSettings.Current);
        }
    }
}
