using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.PalDerived;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped
{
    public class PalInstanceViewModel(PalInstance inst)
    {
        public PalInstance ModelObject => inst;

        public PalViewModel Pal { get; } = PalViewModel.Make(inst.Pal);

        public PassiveSkillCollectionViewModel PassiveSkills { get; } = new PassiveSkillCollectionViewModel(inst.PassiveSkills.Select(PassiveSkillViewModel.Make));

        public AttackSkillCollectionViewModel EquippedActiveSkills { get; } = new(inst.EquippedActiveSkills.Select(ActiveSkillViewModel.Make));
        public AttackSkillCollectionViewModel ActiveSkills { get; } = new(inst.ActiveSkills.Select(ActiveSkillViewModel.Make));

        public PalGenderViewModel Gender { get; } = PalGenderViewModel.Make(inst.Gender);
    }
}
