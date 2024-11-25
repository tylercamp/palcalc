using PalCalc.Model;
using PalCalc.UI.Localization;
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

        public PalGenderViewModel Gender { get; } = PalGenderViewModel.Make(inst.Gender);
    }
}
