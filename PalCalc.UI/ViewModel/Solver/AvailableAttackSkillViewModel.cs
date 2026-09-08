using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.Localization;
using PalCalc.Model;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.UI.ViewModel.Solver
{
    public class AvailableAttackSkillViewModel
    {
        public AvailableAttackSkillViewModel(ActiveSkillViewModel vm, AttackSkillAvailabilityInfo availability)
        {
            Attack = vm;
            Availability = availability;

            if (!availability.IsAvailable)
            {
                var warnings = !availability.CanInherit
                    ? [LocalizationCodes.LC_ATTACK_SKILL_NOT_INHERITABLE.Bind()]
                    : new List<ILocalizedText>
                    {
                        OwnedWarning(availability.Owned),
                        WildWarning(availability.Wild),
                    }.SkipNull().ToList();

                WarningText = Translator.JoinNewline.Bind(warnings);
            }
        }

        private static ILocalizedText OwnedWarning(OwnedSkillAvailabilityInfo availability)
        {
            if (availability.RequiresSameTypeWarning)
                return LocalizationCodes.LC_ATTACK_SKILL_REQUIRES_SAME_TYPE.Bind();
            if (!availability.HasCarrier)
                return LocalizationCodes.LC_ATTACK_SKILL_NOT_KNOWN.Bind();
            if (!availability.IsWithinBreedingLimit)
                return LocalizationCodes.LC_ATTACK_SKILL_NOT_REACHABLE.Bind();

            return null;
        }

        private static ILocalizedText WildWarning(WildSkillAvailabilityInfo availability)
        {
            if (availability.IsAvailable)
                return null;
            if (!availability.HasCarrier)
                return LocalizationCodes.LC_ATTACK_SKILL_WILD_NOT_KNOWN.Bind();

            return LocalizationCodes.LC_ATTACK_SKILL_WILD_DISABLED.Bind();
        }

        public ActiveSkillViewModel Attack { get; }
        public AttackSkillAvailabilityInfo Availability { get; }
        public ILocalizedText WarningText { get; }

        public bool CanInherit => Availability.CanInherit;
        public bool IsAvailable => Availability.IsAvailable;
    }
}
