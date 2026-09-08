using PalCalc.Model;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.UI.ViewModel.Solver
{
    public class AvailablePassiveSkillViewModel
    {
        public AvailablePassiveSkillViewModel(PassiveSkillViewModel vm, PassiveSkillAvailabilityInfo availability)
        {
            var warnings = availability.IsAvailable
                ? []
                : new List<ILocalizedText>
                {
                    OwnedWarning(availability.Owned),
                    WildWarning(availability.Wild),
                    SurgeryWarning(availability.Surgery),
                }.SkipNull().ToList();

            Passive = vm;
            Availability = availability;
            WarningText = Translator.JoinNewline.Bind(warnings);
        }

        private static ILocalizedText OwnedWarning(OwnedSkillAvailabilityInfo availability)
        {
            if (availability.RequiresSameTypeWarning)
                return LocalizationCodes.LC_PASSIVE_SKILL_REQUIRES_SAME_TYPE.Bind();
            if (!availability.HasCarrier)
                return LocalizationCodes.LC_PASSIVE_SKILL_NOT_KNOWN.Bind();
            if (!availability.IsWithinBreedingLimit)
                return LocalizationCodes.LC_PASSIVE_SKILL_NOT_REACHABLE.Bind();

            return null;
        }

        private static ILocalizedText WildWarning(WildSkillAvailabilityInfo availability)
        {
            if (availability == null || availability.IsAvailable)
                return null;
            if (!availability.HasCarrier)
                return LocalizationCodes.LC_PASSIVE_SKILL_WILD_NOT_KNOWN.Bind();

            return LocalizationCodes.LC_PASSIVE_SKILL_WILD_DISABLED.Bind();
        }

        private static ILocalizedText SurgeryWarning(SurgeryPassiveAvailabilityInfo availability)
        {
            if (availability == null || availability.IsAvailable)
                return null;
            if (!availability.IsEnabled)
                return LocalizationCodes.LC_PASSIVE_SKILL_SURGERY_DISABLED.Bind();

            return LocalizationCodes.LC_PASSIVE_SKILL_SURGERY_UNAFFORDABLE.Bind();
        }

        public PassiveSkillViewModel Passive { get; }
        public PassiveSkillAvailabilityInfo Availability { get; }
        public ILocalizedText WarningText { get; }
        public bool IsAvailable => Availability.IsAvailable;
    }
}
