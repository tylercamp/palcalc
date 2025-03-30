using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector
{
    public class SaveInspectorWindowViewModel
    {
        private static SaveInspectorWindowViewModel designerInstance = null;
        public static SaveInspectorWindowViewModel DesignerInstance => designerInstance ??= new SaveInspectorWindowViewModel(null, SaveGameViewModel.DesignerInstance, GameSettings.Defaults);

        public SaveGameViewModel DisplayedSave { get; }

        public SearchViewModel Search { get; }
        public SaveDetailsViewModel Details { get; }

        public ILocalizedText WindowTitle { get; }

        public SaveInspectorWindowViewModel(ISavesLocationViewModel slvm, SaveGameViewModel sgvm, GameSettings settings)
        {
            DisplayedSave = sgvm;

            Search = new SearchViewModel(sgvm, settings);
            Details = new SaveDetailsViewModel(slvm.Value, sgvm.CachedValue);

            WindowTitle = LocalizationCodes.LC_SAVEWINDOW_TITLE.Bind(sgvm.Label);
        }
    }
}
