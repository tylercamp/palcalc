using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    class SavesCollectionTemplateSelector : DataTemplateSelector
    {
        public DataTemplate SteamTemplate { get; set; }
        public DataTemplate XboxTemplate { get; set; }
        public DataTemplate ManualTemplate { get; set; }
        public DataTemplate FakeSavesTemplate { get; set; }

        public DataTemplate DefaultTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return item switch
            {
                SteamSavesCollectionViewModel => SteamTemplate,
                XboxSavesCollectionViewModel => XboxTemplate,
                ManualSavesCollectionViewModel => ManualTemplate,
                FakeSavesCollectionViewModel => FakeSavesTemplate,
                _ => null
            } ?? DefaultTemplate;
        }
    }
}
