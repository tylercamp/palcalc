namespace PalCalc.Solver.Probabilities;

public static class Attacks
{
    /// <summary>
    /// <para>
    ///     Given:
    /// 
    ///     <list type="number">
    ///         <item>A goal of inheriting a specific target attack</item>
    ///         <item>Two parents which both have exactly 1 attack equipped</item>
    ///         <item>Info on which parents have the target attack and/or noop/non-inheritable attacks</item>
    ///     </list>
    /// 
    ///     ... calculates the probability of the child having the specific target attack.
    /// </para>
    /// 
    /// <para>
    ///     In normal attack inheritance (i.e., no special cakes) Palworld combines and deduplicates
    ///     the list of *equipped* attacks from the parents, removes any non-inheritable attacks,
    ///     and picks a random attack from the list.
    /// </para>
    /// 
    /// <para>
    ///     If one parent has a noop/non-inheritable attack, it's excluded completely from the
    ///     list, giving the other parent's attack a 100% chance to inherit.
    /// </para>
    /// </summary>
    public static float ProbabilityInheritedTargetAttack(
        bool parent1HasTarget,
        bool parent2HasTarget,
        bool parent1HasNoopAttack,
        bool parent2HasNoopAttack
    )
    {
        /*
         * Extra note: The child always gets its Lv1 attack and one attack from the parents. If the inherited
         * attack is the same as the child's Lv1 attack, the inheritance has no effect, and the child just
         * gets its Lv1 attack alone.
         * 
         * This has no effect on the overall solver or probability calc for attacks. It's mentioned here
         * for completeness. Since we can manually swap out the equipped attacks on a child, it doesn't
         * matter whether it gets one, two, or fifty final attacks, so long as it gets to *desired* attack.
         */

        /*
         * Extra extra note: Usually, the only way to get a child with >1 desired attack is through special cakes,
         * which force inheritance of up-to-three from each parent. Otherwise inheritance just gives the child 1 extra attack.
         * 
         * But if the child inherits a desired attack, *and* the child's lv1 attack is also desired, we effectively
         * get 2 desired attacks from a single breeding result without needing to use special cakes.
         * 
         * This detail should be handled by the caller, and is ignored in this function. Here we only care
         * about the *inherited* attack.
         */

        // Neither parent has what we want, no desired attack to inherit
        if (!parent1HasTarget && !parent2HasTarget)
            return 0;

        // If only 1 parent has the desired attack, and the other parent has a normal inherited attack, it's
        // a 50/50 chance to get what we want. Otherwise the desired result is guaranteed.
        return (parent1HasTarget && parent2HasTarget) ||
            (parent1HasTarget && parent2HasNoopAttack) ||
            (parent2HasTarget && parent1HasNoopAttack)
                ? 1
                : 0.5f;
    }
}
