using ABI.Windows.AI.MachineLearning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PalCalc.UI.ViewModel.SaveSelection
{
    internal class SaveTypeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate SteamTemplate { get; set; }
        public DataTemplate XboxTemplate { get; set; }
        public DataTemplate ManualSaveTemplate { get; set; }
        public DataTemplate VirtualSaveTemplate { get; set; }

        public DataTemplate DefaultTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item == null) return DefaultTemplate;

            if (item is SavesCollectionViewModel scvm) return SelectTemplate(scvm.SaveType, container);
            if (item is SaveGameViewModel2 sgvm) return SelectTemplate(sgvm.Type, container);

            return (SaveType)item switch
            {
                SaveType.Steam => SteamTemplate,
                SaveType.Xbox => XboxTemplate,
                SaveType.LocalFile => ManualSaveTemplate,
                SaveType.Virtual => VirtualSaveTemplate,
                _ => DefaultTemplate
            } ?? DefaultTemplate;
        }
    }
}
