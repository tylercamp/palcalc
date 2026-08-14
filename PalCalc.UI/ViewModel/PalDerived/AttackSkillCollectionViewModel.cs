using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.UI.ViewModel.PalDerived
{
    public class AttackSkillCollectionViewModel
    {
        public AttackSkillCollectionViewModel(IEnumerable<ActiveSkillViewModel> attacks)
        {
            Attacks = attacks.ToList();
            Description = Translator.Join.Bind(Attacks.Select(t => t.Name));
        }

        public List<ActiveSkillViewModel> Attacks { get; }

        public ILocalizedText Description { get; }
    }
}
