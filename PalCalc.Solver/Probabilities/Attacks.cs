namespace PalCalc.Solver.Probabilities;

public static class Attacks
{
    /// <summary>
    /// Calculates the probability of inheriting one target attack after choosing
    /// the best legal one-attack loadout for each parent.
    ///
    /// Equipped parent attacks are deduplicated, attacks which cannot be
    /// inherited are excluded, and one remaining attack is selected uniformly.
    /// A parent without the target can avoid adding an irrelevant attack to the
    /// pool only when it can equip a non-inheritable attack instead.
    /// </summary>
    public static float ProbabilityInheritedTargetAttack(
        bool parent1HasTarget,
        bool parent2HasTarget,
        bool parent1HasNeutralAttack,
        bool parent2HasNeutralAttack
    )
    {
        if (!parent1HasTarget && !parent2HasTarget)
            return 0;

        return parent1HasTarget && parent2HasTarget ||
            parent1HasTarget && parent2HasNeutralAttack ||
            parent2HasTarget && parent1HasNeutralAttack
                ? 1
                : 0.5f;
    }
}
