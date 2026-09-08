using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.UI.ViewModel.Solver
{
    /// <param name="HasCarrier">Whether any enabled Pal has the skill</param>
    /// <param name="HasCompatibleCarrier">Whether any breeding-compatible Pal has the skill (see `TargetRequiresSameType`)</param>
    /// <param name="IsWithinBreedingLimit">Whether a Pal with the skill can be reached within the breeding-steps limit</param>
    /// <param name="TargetRequiresSameType">Whether the target Pal requires same-type parents, e.g. Jetragon can only be bred from other Jetragons</param>
    public record OwnedSkillAvailabilityInfo(
        bool HasCarrier,
        bool HasCompatibleCarrier,
        bool IsWithinBreedingLimit,
        bool TargetRequiresSameType
    )
    {
        public bool IsAvailable => HasCarrier && HasCompatibleCarrier && IsWithinBreedingLimit;

        public bool RequiresSameTypeWarning => !IsAvailable
            && (TargetRequiresSameType || HasCarrier && !HasCompatibleCarrier);

        public static OwnedSkillAvailabilityInfo FromCarriers(
            IEnumerable<Pal> carriers,
            Func<Pal, int> breedingSteps,
            int maxBreedingSteps,
            bool targetRequiresSameType
        )
        {
            var carrierSteps = carriers?.Select(breedingSteps).ToList() ?? [];
            var hasCarrier = carrierSteps.Count > 0;
            var minSteps = hasCarrier
                ? carrierSteps.Min()
                : PalBreedingDB.NotReachableBreedingSteps;
            var hasCompatibleCarrier = minSteps < PalBreedingDB.NotReachableBreedingSteps;

            return new(
                hasCarrier,
                hasCompatibleCarrier,
                hasCompatibleCarrier && minSteps <= maxBreedingSteps,
                targetRequiresSameType
            );
        }
    }

    public record WildSkillAvailabilityInfo(bool HasCarrier, bool IsEnabled)
    {
        public bool IsAvailable => HasCarrier && IsEnabled;
    }

    public record SurgeryPassiveAvailabilityInfo(bool IsEnabled, bool IsAffordable)
    {
        public bool IsAvailable => IsEnabled && IsAffordable;
    }

    public record AttackSkillAvailabilityInfo(
        bool CanInherit,
        OwnedSkillAvailabilityInfo Owned,
        WildSkillAvailabilityInfo Wild
    )
    {
        public bool IsAvailable => CanInherit && (Owned.IsAvailable || Wild.IsAvailable);
    }

    public record PassiveSkillAvailabilityInfo(
        OwnedSkillAvailabilityInfo Owned,
        WildSkillAvailabilityInfo Wild,
        SurgeryPassiveAvailabilityInfo Surgery
    )
    {
        public bool IsAvailable => Owned.IsAvailable
            || Wild?.IsAvailable == true
            || Surgery?.IsAvailable == true;
    }
}
