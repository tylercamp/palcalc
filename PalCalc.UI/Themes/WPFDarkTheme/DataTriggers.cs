using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Theme.WPF.Themes;

namespace Theme.WPF.Themes
{
    public class CustomThemeTrigger : DataTrigger
    {
        public CustomThemeTrigger()
        {
            var odp = new ObjectDataProvider()
            {
                ObjectType = typeof(ThemesController),
                MethodName = "get_IsCustomTheme"
            };

            Binding = new Binding()
            {
                Source = odp,
                Mode = BindingMode.OneWay
            };

            Value = true;
        }
    }

    public class NativeThemeTrigger : DataTrigger
    {
        public NativeThemeTrigger()
        {
            Binding = new Binding()
            {
                Source = typeof(ThemesController),
                Path = new PropertyPath(nameof(ThemesController.IsNativeTheme))
            };

            Value = true;
        }
    }
}
