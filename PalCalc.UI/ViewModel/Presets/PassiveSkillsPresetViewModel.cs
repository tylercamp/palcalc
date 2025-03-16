using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.View.Utils;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Presets
{
    public partial class PassiveSkillsPresetViewModel : ObservableObject, IEditableListItem
    {
        public PassiveSkillsPresetViewModel(PassiveSkillsPreset content)
        {
            ModelObject = content;
            Label = new HardCodedText(content.Name);

            var db = PalDB.LoadEmbedded();
            RequiredPassives = new([
                content.Passive1InternalName.InternalToStandardPassive(db),
                content.Passive2InternalName.InternalToStandardPassive(db),
                content.Passive3InternalName.InternalToStandardPassive(db),
                content.Passive4InternalName.InternalToStandardPassive(db),
            ]);

            OptionalPassives = new([
                content.OptionalPassive1InternalName.InternalToStandardPassive(db),
                content.OptionalPassive2InternalName.InternalToStandardPassive(db),
                content.OptionalPassive3InternalName.InternalToStandardPassive(db),
                content.OptionalPassive4InternalName.InternalToStandardPassive(db),
            ]);
        }

        public PassiveSkillsPreset ModelObject { get; }

        public PalSpecifierPassiveSkillCollectionViewModel RequiredPassives { get; }
        public PalSpecifierPassiveSkillCollectionViewModel OptionalPassives { get; }

        [ObservableProperty]
        private ILocalizedText label;

        public string Name => Label.Value;
        public string DeleteConfirmTitle => LocalizationCodes.LC_TRAITS_PRESETS_DELETE_TITLE.Bind().Value;
        public string DeleteConfirmMessage => LocalizationCodes.LC_TRAITS_PRESETS_DELETE_MSG.Bind(Label).Value;
        public string OverwriteConfirmTitle => LocalizationCodes.LC_TRAITS_PRESETS_OVERWRITE_TITLE.Bind().Value;
        public string OverwriteConfirmMessage => LocalizationCodes.LC_TRAITS_PRESETS_OVERWRITE_MSG.Bind(Label).Value;
        public string RenamePopupTitle => LocalizationCodes.LC_TRAITS_PRESETS_RENAME_TITLE.Bind(Label).Value;
        public string RenamePopupInputLabel => LocalizationCodes.LC_TRAITS_PRESETS_NAME.Bind().Value;

        public void ApplyTo(PalSpecifierViewModel spec)
        {
            spec.RequiredPassives.CopyFrom(RequiredPassives);
            spec.OptionalPassives.CopyFrom(OptionalPassives);
        }
    }
}
