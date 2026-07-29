using PalCalc.UI.Model;
using PalCalc.UI.Model.Service;
using Serilog;
using System;
using System.ComponentModel;
using System.Windows;

namespace PalCalc.UI.View.Behaviors
{
    public enum WindowPlacementMode
    {
        Full,
        SizeAndState,
        Size,
    }

    public static class WindowPlacementBehavior
    {
        private const int CurrentSettingsVersion = 2;
        private static readonly ILogger logger = Log.ForContext(typeof(WindowPlacementBehavior));

        public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
            "Key",
            typeof(string),
            typeof(WindowPlacementBehavior),
            new PropertyMetadata(null, KeyChanged)
        );

        public static readonly DependencyProperty ModeProperty = DependencyProperty.RegisterAttached(
            "Mode",
            typeof(WindowPlacementMode),
            typeof(WindowPlacementBehavior),
            new PropertyMetadata(WindowPlacementMode.Full)
        );

        private static readonly DependencyProperty LastNonMinimizedStateProperty =
            DependencyProperty.RegisterAttached(
                "LastNonMinimizedState",
                typeof(WindowState),
                typeof(WindowPlacementBehavior),
                new PropertyMetadata(WindowState.Normal)
            );

        public static string GetKey(DependencyObject obj) => (string)obj.GetValue(KeyProperty);

        public static void SetKey(DependencyObject obj, string value) => obj.SetValue(KeyProperty, value);

        public static WindowPlacementMode GetMode(DependencyObject obj) =>
            (WindowPlacementMode)obj.GetValue(ModeProperty);

        public static void SetMode(DependencyObject obj, WindowPlacementMode value) =>
            obj.SetValue(ModeProperty, value);

        private static void KeyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            if (obj is not Window window)
            {
                // (don't throw an exception - VS XAML Preview doesn't provide a real `Window`)
                logger.Warning($"{nameof(WindowPlacementBehavior)} can only be applied to a Window, not a {obj.GetType().FullName}");
                return;
            }

            if (!string.IsNullOrWhiteSpace(e.OldValue as string))
            {
                window.SourceInitialized -= Window_SourceInitialized;
                window.Closing -= Window_Closing;
                window.StateChanged -= Window_StateChanged;
            }

            if (!string.IsNullOrWhiteSpace(e.NewValue as string))
            {
                if (window.WindowState != WindowState.Minimized)
                    window.SetValue(LastNonMinimizedStateProperty, window.WindowState);

                window.SourceInitialized += Window_SourceInitialized;
                window.Closing += Window_Closing;
                window.StateChanged += Window_StateChanged;
            }
        }

        private static void Window_StateChanged(object sender, EventArgs e)
        {
            var window = (Window)sender;
            if (window.WindowState != WindowState.Minimized)
                window.SetValue(LastNonMinimizedStateProperty, window.WindowState);
        }

        private static void Window_SourceInitialized(object sender, EventArgs e)
        {
            var window = (Window)sender;
            var key = GetKey(window);

            if (!UILayoutStore.TryGetWindowPlacement(key, out var savedPlacement) ||
                !UILayoutValidation.IsValidWindowPlacement(savedPlacement, CurrentSettingsVersion))
            {
                return;
            }

            var mode = GetMode(window);
            var restoreMaximized =
                savedPlacement.IsMaximized &&
                mode is WindowPlacementMode.Full or WindowPlacementMode.SizeAndState;

            window.Width = savedPlacement.Right - savedPlacement.Left;
            window.Height = savedPlacement.Bottom - savedPlacement.Top;

            if (mode == WindowPlacementMode.Full)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = savedPlacement.Left;
                window.Top = savedPlacement.Top;
            }

            if (restoreMaximized)
                window.WindowState = WindowState.Maximized;
        }

        private static void Window_Closing(object sender, CancelEventArgs e)
        {
            var window = (Window)sender;
            var bounds = window.RestoreBounds;
            if (bounds.IsEmpty ||
                !double.IsFinite(bounds.Left) ||
                !double.IsFinite(bounds.Top) ||
                !double.IsFinite(bounds.Width) ||
                !double.IsFinite(bounds.Height))
            {
                logger.Warning("Unable to capture window placement for {WindowLayoutKey}", GetKey(window));
                return;
            }

            var savedPlacement = new WindowPlacementSettings
            {
                Version = CurrentSettingsVersion,
                Left = (int)Math.Round(bounds.Left),
                Top = (int)Math.Round(bounds.Top),
                Right = (int)Math.Round(bounds.Right),
                Bottom = (int)Math.Round(bounds.Bottom),
                IsMaximized =
                    (WindowState)window.GetValue(LastNonMinimizedStateProperty) ==
                    WindowState.Maximized,
            };

            if (UILayoutValidation.IsValidWindowPlacement(savedPlacement, CurrentSettingsVersion))
                UILayoutStore.SaveWindowPlacement(GetKey(window), savedPlacement);
        }
    }
}
