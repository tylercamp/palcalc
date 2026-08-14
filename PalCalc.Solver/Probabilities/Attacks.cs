using PalCalc.Model;
using PalCalc.Solver.PalReference;

namespace PalCalc.Solver.Probabilities;

public readonly record struct AttackInheritanceOutcome(
    ActiveSkill ActualAttack,
    ActiveSkill EffectiveAttack,
    float Probability
);

public static class Attacks
{
    // TODO: Extend this outcome model for inherit-all effects: choose up to three
    // equipped attacks per parent, inherit every distinct eligible attack (up to six
    // mastered attacks), and allow later pairings to choose a new equipped loadout.
    public static AttackInheritanceOutcome InheritanceOutcome(
        PalDB db,
        ActiveSkill requiredAttack,
        Pal child,
        IPalReference parent1,
        IPalReference parent2
    )
    {
        if (requiredAttack == null)
            return Fallback(db, child, null, null);

        if (child.Level1AttackInternalIds.Contains(requiredAttack.InternalName))
            return new(requiredAttack, requiredAttack, 1);

        var first = Eligible(parent1.ActualAttack);
        var second = Eligible(parent2.ActualAttack);

        // TODO: Full mastered attack state and exact irrelevant bred identities will
        // allow duplicate irrelevant attacks from different parents to collapse here.
        if (first?.Equals(second) == true)
            second = null;

        var count = (first == null ? 0 : 1) + (second == null ? 0 : 1);
        if (requiredAttack.Equals(first) || requiredAttack.Equals(second))
            return new(requiredAttack, requiredAttack, 1f / count);

        // No desired attacks are available for inheritance, use a generic fallback result

        return Fallback(db, child, first, second);
    }

    private static ActiveSkill Eligible(ActiveSkill attack) =>
        attack?.CanInherit == true ? attack : null;

    private static AttackInheritanceOutcome Fallback(
        PalDB db,
        Pal child,
        ActiveSkill firstEligible,
        ActiveSkill secondEligible
    )
    {
        var neutral = FirstLevel1Attack(db, child, canInherit: false);
        if (neutral != null)
            return new(neutral, null, 1);

        if (
            firstEligible != null ||
            secondEligible != null ||
            FirstLevel1Attack(db, child, canInherit: true) != null
        )
        {
            var random = new RandomActiveSkill();
            return new(random, random, 1);
        }

        return new(null, null, 1);
    }

    private static ActiveSkill FirstLevel1Attack(PalDB db, Pal child, bool canInherit)
    {
        foreach (var id in child.Level1AttackInternalIds)
        {
            foreach (var attack in db.ActiveSkills)
            {
                if (attack.InternalName == id && attack.CanInherit == canInherit)
                    return attack;
            }
        }

        return null;
    }
}
