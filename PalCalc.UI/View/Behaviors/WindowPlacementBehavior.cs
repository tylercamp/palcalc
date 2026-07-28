using PalCalc.UI.Model;
using PalCalc.UI.Model.Service;
using Serilog;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

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
        private const int CurrentSettingsVersion = 1;
        private const int SwShowNormal = 1;
        private const int SwShowMinimized = 2;
        private const int SwShowMaximized = 3;
        private const int WpfRestoreToMaximized = 0x0002;
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
            }

            if (!string.IsNullOrWhiteSpace(e.NewValue as string))
            {
                window.SourceInitialized += Window_SourceInitialized;
                window.Closing += Window_Closing;
            }
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
            var placement = new NativeWindowPlacement
            {
                Length = Marshal.SizeOf<NativeWindowPlacement>(),
                Flags = 0,
                ShowCommand = restoreMaximized ? SwShowMaximized : SwShowNormal,
                NormalPosition = new NativeRect
                {
                    Left = savedPlacement.Left,
                    Top = savedPlacement.Top,
                    Right = savedPlacement.Right,
                    Bottom = savedPlacement.Bottom,
                },
            };

            var handle = new WindowInteropHelper(window).Handle;
            if (mode != WindowPlacementMode.Full &&
                !TryKeepCurrentPosition(handle, ref placement))
            {
                logger.Warning("Unable to restore window size for {WindowLayoutKey}", key);
                return;
            }

            if (!SetWindowPlacement(handle, ref placement))
                logger.Warning("Unable to restore window placement for {WindowLayoutKey}", key);
        }

        private static bool TryKeepCurrentPosition(IntPtr handle, ref NativeWindowPlacement placement)
        {
            var currentPlacement = new NativeWindowPlacement
            {
                Length = Marshal.SizeOf<NativeWindowPlacement>(),
            };

            if (!GetWindowPlacement(handle, ref currentPlacement))
                return false;

            var width = placement.NormalPosition.Right - placement.NormalPosition.Left;
            var height = placement.NormalPosition.Bottom - placement.NormalPosition.Top;
            var horizontalCenter = (long)currentPlacement.NormalPosition.Left +
                                   currentPlacement.NormalPosition.Right;
            var verticalCenter = (long)currentPlacement.NormalPosition.Top +
                                 currentPlacement.NormalPosition.Bottom;

            var left = horizontalCenter / 2 - width / 2;
            var top = verticalCenter / 2 - height / 2;

            left = Math.Clamp(left, int.MinValue, (long)int.MaxValue - width);
            top = Math.Clamp(top, int.MinValue, (long)int.MaxValue - height);

            placement.NormalPosition = new NativeRect
            {
                Left = (int)left,
                Top = (int)top,
                Right = (int)left + width,
                Bottom = (int)top + height,
            };
            return true;
        }

        private static void Window_Closing(object sender, CancelEventArgs e)
        {
            var window = (Window)sender;
            var handle = new WindowInteropHelper(window).Handle;

            var placement = new NativeWindowPlacement
            {
                Length = Marshal.SizeOf<NativeWindowPlacement>(),
            };

            if (!GetWindowPlacement(handle, ref placement))
            {
                logger.Warning("Unable to capture window placement for {WindowLayoutKey}", GetKey(window));
                return;
            }

            var isMaximized =
                placement.ShowCommand == SwShowMaximized ||
                (placement.ShowCommand == SwShowMinimized &&
                 (placement.Flags & WpfRestoreToMaximized) != 0);

            var savedPlacement = new WindowPlacementSettings
            {
                Version = CurrentSettingsVersion,
                Left = placement.NormalPosition.Left,
                Top = placement.NormalPosition.Top,
                Right = placement.NormalPosition.Right,
                Bottom = placement.NormalPosition.Bottom,
                IsMaximized = isMaximized,
            };

            if (UILayoutValidation.IsValidWindowPlacement(savedPlacement, CurrentSettingsVersion))
                UILayoutStore.SaveWindowPlacement(GetKey(window), savedPlacement);
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowPlacement(IntPtr windowHandle, ref NativeWindowPlacement placement);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPlacement(IntPtr windowHandle, [In] ref NativeWindowPlacement placement);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeWindowPlacement
        {
            public int Length;
            public int Flags;
            public int ShowCommand;
            public NativePoint MinPosition;
            public NativePoint MaxPosition;
            public NativeRect NormalPosition;
        }
    }
}
