using PalCalc.UI.ViewModel.Inspector.Details;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PalCalc.UI.ViewModel.Inspector.Search.Grid
{
    public class ContainerGridSlotEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                null => false,
                // empty slot should never be interactable
                ContainerGridEmptySlotViewModel => false,
                // "new pal" slot should always be interactable regardless of filters
                ContainerGridNewPalSlotViewModel => true,
                // custom pal slot is always enabled if it hasn't been properly configured yet
                // (otherwise, use default match logic)
                ContainerGridCustomPalSlotViewModel vm => !vm.PalInstance.IsValid || vm.Matches,
                // normal pals are enabled if they match
                ContainerGridPalSlotViewModel vm => vm.Matches,
                _ => throw new NotImplementedException(targetType.Name)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
