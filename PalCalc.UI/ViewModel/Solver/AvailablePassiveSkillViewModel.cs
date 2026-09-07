using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.UI.ViewModel.Solver
{
    public class AvailablePassiveSkillViewModel
    {
        public AvailablePassiveSkillViewModel(PassiveSkillViewModel vm, List<ILocalizedText> warnings)
        {
            warnings = warnings.SkipNull().ToList();

            Passive = vm;
            WarningText = Translator.JoinNewline.Bind(warnings);
            IsAvailable = warnings.Count == 0;
        }

        public PassiveSkillViewModel Passive { get; }
        public ILocalizedText WarningText { get; }
        public bool IsAvailable { get; }
    }
}
