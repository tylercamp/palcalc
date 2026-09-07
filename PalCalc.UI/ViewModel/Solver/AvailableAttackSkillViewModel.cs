using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.Localization;
using System;

namespace PalCalc.UI.ViewModel.Solver
{
    public enum AttackSkillAvailability
    {
        Available,
        NotInheritable,
        NotKnownByPals
    }

    public class AvailableAttackSkillViewModel(ActiveSkillViewModel vm, AttackSkillAvailability availability)
    {
        public ActiveSkillViewModel Attack => vm;
        public AttackSkillAvailability Availability => availability;
        public ILocalizedText WarningText { get; } = availability switch
        {
            AttackSkillAvailability.Available => null,
            AttackSkillAvailability.NotInheritable => LocalizationCodes.LC_ATTACK_SKILL_NOT_INHERITABLE.Bind(),
            AttackSkillAvailability.NotKnownByPals => LocalizationCodes.LC_ATTACK_SKILL_NOT_KNOWN.Bind(),
            _ => throw new ArgumentOutOfRangeException(nameof(availability), availability, null)
        };

        public bool IsAvailable => Availability == AttackSkillAvailability.Available;
    }
}
