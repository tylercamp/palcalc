using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.View.Utils
{
    /// <summary>
    /// Provides the standard Adonis-themed surface for popup content.
    /// </summary>
    public class AdonisPopupContent : Border
    {
        static AdonisPopupContent()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AdonisPopupContent), new FrameworkPropertyMetadata(typeof(AdonisPopupContent)));
        }
    }
}
