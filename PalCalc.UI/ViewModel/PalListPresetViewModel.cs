using CommunityToolkit.Mvvm.ComponentModel;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{
    public partial class PalListPresetViewModel : ObservableObject, IEditableListItem
    {
        private PalDB db = PalDB.LoadEmbedded();

        public PalListPresetViewModel()
        {
            
        }

        public PalListPresetViewModel(PalListPreset modelObject)
        {
            Name = modelObject.Name;
            Pals = modelObject.PalInternalNames.Select(p => p.InternalToPal(db)).ToList();
        }

        [ObservableProperty]
        private string name;

        public List<Pal> Pals { get; set; }

        public PalListPreset AsModelObject => new PalListPreset() { Name = Name, PalInternalNames = Pals.Select(p => p.InternalName).ToList() };

        public string DeleteConfirmTitle => LocalizationCodes.LC_PAL_LIST_PRESETS_DELETE_TITLE.Bind().Value;
        public string DeleteConfirmMessage => LocalizationCodes.LC_PAL_LIST_PRESETS_DELETE_MSG.Bind(Name).Value;

        public string OverwriteConfirmTitle => LocalizationCodes.LC_PAL_LIST_PRESETS_OVERWRITE_TITLE.Bind().Value;
        public string OverwriteConfirmMessage => LocalizationCodes.LC_PAL_LIST_PRESETS_OVERWRITE_MSG.Bind(Name).Value;

        public string RenamePopupTitle => LocalizationCodes.LC_PAL_LIST_PRESETS_RENAME_TITLE.Bind(Name).Value;
        public string RenamePopupInputLabel => LocalizationCodes.LC_PAL_LIST_PRESETS_NAME.Bind().Value;
    }
}
