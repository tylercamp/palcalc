using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;

namespace PalCalc.UI.View
{
    /// <summary>A hyperlink that opens its Uri in the default browser.</summary>
    public class Weblink : Hyperlink
    {
        public static readonly DependencyProperty UriProperty = NavigateUriProperty;

        public Uri Uri
        {
            get => (Uri)GetValue(UriProperty);
            set => SetValue(UriProperty, value);
        }

        public Weblink()
        {
            SetResourceReference(StyleProperty, typeof(Hyperlink));
            RequestNavigate += (_, e) =>
            {
                e.Handled = true;
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            };
        }
    }
}
