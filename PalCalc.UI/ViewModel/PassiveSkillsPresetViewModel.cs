using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel
{
    public interface IPassiveSkillsPresetViewModel
    {
        string Label { get; }
    }

    public partial class PassiveSkillsPresetViewModel : ObservableObject, IPassiveSkillsPresetViewModel
    {
        public PassiveSkillsPresetViewModel(PassiveSkillsPreset content)
        {
            ModelObject = content;
            Label = content.Name;

            var db = PalDB.LoadEmbedded();
            RequiredPassive1 = PassiveSkillViewModel.Make(content.Passive1InternalName.InternalToPassive(db));
            RequiredPassive2 = PassiveSkillViewModel.Make(content.Passive2InternalName.InternalToPassive(db));
            RequiredPassive3 = PassiveSkillViewModel.Make(content.Passive3InternalName.InternalToPassive(db));
            RequiredPassive4 = PassiveSkillViewModel.Make(content.Passive4InternalName.InternalToPassive(db));

            OptionalPassive1 = PassiveSkillViewModel.Make(content.OptionalPassive1InternalName.InternalToPassive(db));
            OptionalPassive2 = PassiveSkillViewModel.Make(content.OptionalPassive2InternalName.InternalToPassive(db));
            OptionalPassive3 = PassiveSkillViewModel.Make(content.OptionalPassive3InternalName.InternalToPassive(db));
            OptionalPassive4 = PassiveSkillViewModel.Make(content.OptionalPassive4InternalName.InternalToPassive(db));
        }

        public PassiveSkillsPreset ModelObject { get; }

        public PassiveSkillViewModel RequiredPassive1 { get; }
        public PassiveSkillViewModel RequiredPassive2 { get; }
        public PassiveSkillViewModel RequiredPassive3 { get; }
        public PassiveSkillViewModel RequiredPassive4 { get; }

        public PassiveSkillViewModel OptionalPassive1 { get; }
        public PassiveSkillViewModel OptionalPassive2 { get; }
        public PassiveSkillViewModel OptionalPassive3 { get; }
        public PassiveSkillViewModel OptionalPassive4 { get; }

        [ObservableProperty]
        private string label;

        public void ApplyTo(PalSpecifierViewModel spec)
        {
            spec.Passive1 = RequiredPassive1;
            spec.Passive2 = RequiredPassive2;
            spec.Passive3 = RequiredPassive3;
            spec.Passive4 = RequiredPassive4;

            spec.OptionalPassive1 = OptionalPassive1;
            spec.OptionalPassive2 = OptionalPassive2;
            spec.OptionalPassive3 = OptionalPassive3;
            spec.OptionalPassive4 = OptionalPassive4;
        }
    }

    public class NewPassiveSkillsPresetViewModel : IPassiveSkillsPresetViewModel
    {
        public string Label { get; private set; }

        public static NewPassiveSkillsPresetViewModel Instance { get; } = new NewPassiveSkillsPresetViewModel() { Label = "Save passives as new preset..." };
        private NewPassiveSkillsPresetViewModel() { }
    }
}
