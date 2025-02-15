using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PalCalc.UI.View
{
    /// <summary>
    /// Interaction logic for LoadingSaveFileModal.xaml
    /// </summary>
    public partial class LoadingSaveFileModal : AdonisWindow
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public LoadingSaveFileModal()
        {
            InitializeComponent();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            // hackfix
            // style is set to `None` in XAML and set to the desired value here, avoids flickering when the
            // window appears. (doesn't work for the About window)
            WindowStyle = WindowStyle.ToolWindow;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
        }

        /// <summary>
        /// Presents this window as a dialog modal and begins the provided work `fn`.
        /// 
        /// The dialog closes automatically when `fn` completes.
        /// </summary>
        public T ShowDialogDuring<T>(Func<T> fn)
        {
            var isDesignMode = DesignerProperties.GetIsInDesignMode(new DependencyObject());
            if (isDesignMode) return fn();

            T res = default;
            Exception ex = null;

            SourceInitialized += (sender, e) =>
            {
                var modalHandle = new WindowInteropHelper(this).Handle;

                Task.Run(async () =>
                {
                    // (slight delay to give priority to WPF UI thread so it can render a bit quicker, hopefully reducing any window flickering)
                    await Task.Delay(100);

                    try { res = fn(); }
                    catch (Exception e) { ex = e; }
                    finally {
                        // WM_CLOSE
                        SendMessage(modalHandle, 0x0010, 0, 0);
                    }
                });
            };

            ShowDialog();

            if (ex != null) throw new Exception("Exception occurred during dialog action", ex);

            return res;
        }

        public void ShowDialogDuring(Action fn) => ShowDialogDuring(() =>
        {
            fn();
            return 0;
        });
    }
}
