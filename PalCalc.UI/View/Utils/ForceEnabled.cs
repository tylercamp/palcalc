using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.View.Utils
{
    public class ForceEnabled : ContentControl
    {
        static ForceEnabled()
        {
            IsEnabledProperty.OverrideMetadata(
                typeof(ForceEnabled),
                new UIPropertyMetadata(
                    defaultValue: true,
                    propertyChangedCallback: (_, __) => { },
                    coerceValueCallback: (_, __) => true)
            );
        }
    }
}
