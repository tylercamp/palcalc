using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.Model
{
    // Persists window position/size/maximized-state (in WPF DIPs; Aero-snapped bounds are
    // captured because ActualWidth/Left reflect them) and resizable grid column/row sizes,
    // keyed into AppSettings.UILayouts.
    public static class WindowStatePersistence
    {
        // ---- Column widths ----

        private static readonly GridLengthConverter GridConv = new();

        public static List<string> SaveColumns(IEnumerable<ColumnDefinition> cols) =>
            cols.Select(c => GridConv.ConvertToString(c.Width)).ToList();

        public static List<string> SaveRows(IEnumerable<RowDefinition> rows) =>
            rows.Select(r => GridConv.ConvertToString(r.Height)).ToList();

        public static void ApplyColumns(IReadOnlyList<ColumnDefinition> cols, List<string> widths)
        {
            if (widths == null || widths.Count != cols.Count) return;
            for (int i = 0; i < cols.Count; i++)
            {
                try { cols[i].Width = (GridLength)GridConv.ConvertFromString(widths[i]); }
                catch { /* leave XAML default on bad data */ }
            }
        }

        public static void ApplyRows(IReadOnlyList<RowDefinition> rows, List<string> heights)
        {
            if (heights == null || heights.Count != rows.Count) return;
            for (int i = 0; i < rows.Count; i++)
            {
                try { rows[i].Height = (GridLength)GridConv.ConvertFromString(heights[i]); }
                catch { /* leave XAML default on bad data */ }
            }
        }

        // ---- Attach helpers ----

        // Remembers a window's full placement (position + size + maximized, including Aero-snapped
        // bounds) across restarts.
        public static void AttachWindow(Window window, string key)
        {
            window.SourceInitialized += (_, _) =>
            {
                var s = Layout(key);
                if (s == null) return;
                if (s.Width is double w && s.Height is double h) { window.Width = w; window.Height = h; }
                if (s.Left is double l && s.Top is double t)
                {
                    // Override WindowStartupLocation (e.g. CenterOwner) so it can't re-position
                    // the window after this point and discard the restore.
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    window.Left = l;
                    window.Top = t;
                    ClampToScreen(window);
                }
                if (s.Maximized == true) window.WindowState = WindowState.Maximized;
            };
            window.Closing += (_, _) => SaveWindowState(window, key, rememberSize: true);
        }

        public static void AttachColumns(
            FrameworkElement owner, string key, Func<IReadOnlyList<ColumnDefinition>> columns) =>
            AttachGrid(owner, key, columns, null);

        // Persists resizable column widths and/or row heights of a grid owned by `owner`.
        // Saves on both Unloaded and the owning Window's Closing — Unloaded alone does NOT
        // fire on app shutdown, so column/row state would otherwise be lost on close.
        public static void AttachGrid(
            FrameworkElement owner, string key,
            Func<IReadOnlyList<ColumnDefinition>> columns = null,
            Func<IReadOnlyList<RowDefinition>> rows = null)
        {
            void Restore()
            {
                var s = Layout(key);
                if (columns != null) ApplyColumns(columns(), s?.ColumnWidths);
                if (rows != null) ApplyRows(rows(), s?.RowHeights);
            }

            void Save()
            {
                var s = LayoutOrNew(key);
                if (columns != null) s.ColumnWidths = SaveColumns(columns());
                if (rows != null) s.RowHeights = SaveRows(rows());
                Storage.SaveAppSettings(AppSettings.Current);
            }

            bool hookedWindow = false;
            owner.Loaded += (_, _) =>
            {
                Restore();
                if (!hookedWindow && Window.GetWindow(owner) is Window win)
                {
                    win.Closing += (_, _) => Save();
                    hookedWindow = true;
                }
            };
            owner.Unloaded += (_, _) => Save();
        }

        public static void ResetToDefault(Window window, double width, double height)
        {
            window.WindowState = WindowState.Normal;
            window.Width = width;
            window.Height = height;
            var wa = SystemParameters.WorkArea;
            window.Left = wa.Left + (wa.Width - width) / 2;
            window.Top = wa.Top + (wa.Height - height) / 2;
        }

        // ---- Main window (page-aware size) ----

        public const double DefaultMainWidth = 1280;
        public const double DefaultMainHeight = 720;

        // Startup opens on the save-selection screen, which always uses the default size;
        // only position and maximized state are restored here. The remembered size is applied
        // later by ApplySolverSize once the user reaches the solver page.
        public static void RestoreMainWindow(Window window)
        {
            var s = Layout("main");
            window.Width = DefaultMainWidth;
            window.Height = DefaultMainHeight;

            if (s?.Left is double l && s.Top is double t)
            {
                window.Left = l;
                window.Top = t;
                ClampToScreen(window);
            }
            else
            {
                CenterOnWorkArea(window);
            }

            if (s?.Maximized == true)
                window.WindowState = WindowState.Maximized;
        }

        public static void ApplySolverSize(Window window)
        {
            if (window == null || window.WindowState != WindowState.Normal) return;
            var s = Layout("main");
            if (s?.Width is double w && s.Height is double h)
            {
                window.Width = w;
                window.Height = h;
                ClampToScreen(window);
            }
        }

        public static void ApplyDefaultSize(Window window)
        {
            if (window == null || window.WindowState != WindowState.Normal) return;
            window.Width = DefaultMainWidth;
            window.Height = DefaultMainHeight;
            ClampToScreen(window);
        }

        // For the main window, rememberSize is true only on the solver page (save selection keeps
        // the default size). See AppWindowViewModel navigation.
        public static void SaveMainWindow(Window window, bool rememberSize) =>
            SaveWindowState(window, "main", rememberSize);

        // rememberSize=false stores only position + maximized. ActualWidth/Left reflect Aero-snapped
        // bounds while WindowState stays Normal, so snapped size/position is remembered.
        public static void SaveWindowState(Window window, string key, bool rememberSize)
        {
            var s = LayoutOrNew(key);
            if (window.WindowState == WindowState.Maximized)
            {
                s.Maximized = true;
                var rb = window.RestoreBounds;
                if (!rb.IsEmpty)
                {
                    s.Left = rb.Left;
                    s.Top = rb.Top;
                    if (rememberSize) { s.Width = rb.Width; s.Height = rb.Height; }
                }
            }
            else
            {
                s.Maximized = false;
                s.Left = window.Left;
                s.Top = window.Top;
                if (rememberSize) { s.Width = window.ActualWidth; s.Height = window.ActualHeight; }
            }
            Storage.SaveAppSettings(AppSettings.Current);
        }

        private static void CenterOnWorkArea(Window window)
        {
            var wa = SystemParameters.WorkArea;
            window.Left = wa.Left + (wa.Width - window.Width) / 2;
            window.Top = wa.Top + (wa.Height - window.Height) / 2;
        }

        // ponytail: coarse visibility clamp against the virtual screen — enough to rescue an
        // off-screen window after a monitor change, not a full per-monitor placement solver.
        private static void ClampToScreen(Window window)
        {
            double vx = SystemParameters.VirtualScreenLeft, vy = SystemParameters.VirtualScreenTop;
            double vw = SystemParameters.VirtualScreenWidth, vh = SystemParameters.VirtualScreenHeight;
            double width = double.IsNaN(window.Width) ? window.ActualWidth : window.Width;
            double height = double.IsNaN(window.Height) ? window.ActualHeight : window.Height;
            const double margin = 80; // keep at least this much on-screen

            if (window.Left + width < vx + margin) window.Left = vx;
            else if (window.Left > vx + vw - margin) window.Left = vx + vw - width;

            if (window.Top < vy) window.Top = vy;
            else if (window.Top > vy + vh - margin) window.Top = vy + vh - height;
        }

        // ---- Internals ----

        private static WindowLayoutState Layout(string key) =>
            AppSettings.Current.UILayouts.TryGetValue(key, out var v) ? v : null;

        private static WindowLayoutState LayoutOrNew(string key)
        {
            if (!AppSettings.Current.UILayouts.TryGetValue(key, out var v))
                AppSettings.Current.UILayouts[key] = v = new WindowLayoutState();
            return v;
        }
    }
}
