using PalCalc.Model;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Probabilities;

namespace PalCalc.Solver.Tests.Probabilities;

[TestClass]
public class IVProbabilitiesTests
{
    static readonly int[] CurrentGameWeights = [3, 2, 1];

    private static readonly (bool HP, bool Attack, bool Defense)[] StatSelections =
    [
        (false, false, false),
        (true, false, false),
        (false, true, false),
        (false, false, true),
        (true, true, false),
        (true, false, true),
        (false, true, true),
        (true, true, true),
    ];

    // Hand-derived expectations; source sets and the general formula are documented in:
    // ../../PalCalc.Solver/README-PALWORLD-MECHANICS.md#iv-source-selection
    //
    // P(required subset) = sum(selector weight / total weight * fraction of its branches containing the subset)
    //
    // With CurrentGameWeights = [3, 2, 1], selector probabilities are 3/6, 2/6, and 1/6:
    //   No requirements:         1
    //   HP:                      3/6 * 1/3 + 2/6 * 3/3 + 1/6 = 2/3
    //   Attack or Defense:       3/6 * 1/3 + 2/6 * 2/3 + 1/6 = 5/9
    //   HP+Attack / HP+Defense:              2/6 * 2/3 + 1/6 = 7/18
    //   Attack+Defense / all:                2/6 * 1/3 + 1/6 = 5/18
    // Extra inherited stats are allowed. Both parents meet each required IV, so parent choice adds no penalty.
    [DataTestMethod]
    [DataRow(false, false, false, 1, 1)] // No requirements
    [DataRow(true, false, false, 2, 3)] // HP
    [DataRow(false, true, false, 5, 9)] // Attack
    [DataRow(false, false, true, 5, 9)] // Defense
    [DataRow(true, true, false, 7, 18)] // HP + Attack
    [DataRow(true, false, true, 7, 18)] // HP + Defense
    [DataRow(false, true, true, 5, 18)] // Attack + Defense must use the all-three branch
    [DataRow(true, true, true, 5, 18)] // All three
    public void ReferenceModelMatchesDocumentedSubsetProbabilities(
        bool hp, bool attack, bool defense, int numerator, int denominator)
    {
        var required = (hp, attack, defense);
        Assert.AreEqual(numerator / (float)denominator,
            ReferenceProbability(CurrentGameWeights, required, required, required), 0.000001f);
    }

    [DataTestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    public void ProbabilityHelpersMatchReferenceGameBranches(bool hp, bool attack, bool defense)
    {
        var required = (HP: hp, Attack: attack, Defense: defense);

        // Inheritance weights shipped with the game are `[3, 2, 1]` (at time of writing)
        // Also test with other weights which (1) test each selection separately and (2) use a different
        //   set of weights, to check for consistency

        int[][] weightSets = [
            CurrentGameWeights,
            [1, 0, 0],
            [0, 1, 0],
            [0, 0, 1],
            [1, 4, 7]
        ];

        foreach (var weights in weightSets)
        {
            var mechanics = new BreedingMechanics(
                ivInheritanceWeights: weights,
                passiveInheritanceWeights: new() { [1] = 1 },
                passiveRandomWeights: new() { [0] = 1 }
            );

            var selectionProbability = ReferenceProbability(weights, required, required, required);
            Assert.AreEqual(
                expected: selectionProbability,
                actual: mechanics.ProbabilityOfInheritingDesiredIVs(hp, attack, defense),
                delta: 0.000001f
            );

            foreach (var parentA in StatSelections)
                foreach (var parentB in StatSelections)
                {
                    // Each required stat must be available from at least one parent.
                    if ((parentA.HP || parentB.HP) != hp ||
                        (parentA.Attack || parentB.Attack) != attack ||
                        (parentA.Defense || parentB.Defense) != defense) continue;

                    var expected = ReferenceProbability(weights, required, parentA, parentB);
                    foreach (var randomIrrelevant in new[] { false, true })
                        Assert.AreEqual(
                            expected: expected,
                            actual: IVs.ProbabilityInheritedTargetIVs(mechanics, Parent(parentA, randomIrrelevant), Parent(parentB, randomIrrelevant)),
                            delta: 0.000001f,
                            message: $"Target {required}; parents {parentA}/{parentB}; weights {string.Join(',', weights)}"
                        );
                }
        }
    }

    // Uses the reverse-engineered, reference IV inheritance impl. to calculate inheritance probability
    // for the given weights, required IVs, and presence of IVs on parents
    private static float ReferenceProbability(
        int[] weights,
        (bool HP, bool Attack, bool Defense) required,
        (bool HP, bool Attack, bool Defense) parentA,
        (bool HP, bool Attack, bool Defense) parentB)
    {
        double probability = 0;
        for (var selectorIndex = 0; selectorIndex < weights.Length; selectorIndex++)
        {
            var branchCount = selectorIndex == 2 ? 1 : 3;
            var successes = 0;
            for (var branch = 0; branch < branchCount; branch++)
                foreach (var fromParentA in StatSelections)
                {
                    var child = ReferenceChildIVs(parentA, parentB, fromParentA, selectorIndex, branch);
                    if ((!required.HP || child.HP) &&
                        (!required.Attack || child.Attack) &&
                        (!required.Defense || child.Defense)) successes++;
                }

            probability += weights[selectorIndex] / (double)weights.Sum() *
                successes / (branchCount * StatSelections.Length);
        }
        return (float)probability;
    }

    // Reference impl. which matches the Palworld IV calculation (almost) exactly; namely, see:
    // README-PALWORLD-MECHANICS.md#iv-source-selection
    //
    // `true` inputs/output indicates whether the value meets the target.
    private static (bool HP, bool Attack, bool Defense) ReferenceChildIVs(
        (bool HP, bool Attack, bool Defense) parentA,
        (bool HP, bool Attack, bool Defense) parentB,
        (bool HP, bool Attack, bool Defense) fromParentA,
        int selectorIndex,
        int branch)
    {
        // (fresh random matches are excluded from PalCalc's estimate, so R never meets a required IV)
        var R = (HP: false, Attack: false, Defense: false);
        var H = (
            HP: fromParentA.HP ? parentA.HP : parentB.HP,
            Attack: fromParentA.Attack ? parentA.Attack : parentB.Attack,
            Defense: fromParentA.Defense ? parentA.Defense : parentB.Defense);

        if (selectorIndex == 2)
            return H;

        if (selectorIndex == 1)
            return branch switch
            {
                0 => (H.HP, H.Attack, H.Defense),
                1 => (H.HP, R.Attack, H.Defense),
                2 => (H.HP, H.Attack, R.Defense),
                _ => throw new ArgumentOutOfRangeException(nameof(branch)),
            };

        return branch switch
        {
            0 => (H.HP, R.Attack, R.Defense),
            1 => (R.HP, H.Attack, R.Defense),
            2 => (R.HP, R.Attack, H.Defense),
            _ => throw new ArgumentOutOfRangeException(nameof(branch)),
        };
    }

    private static IV_Set Parent((bool HP, bool Attack, bool Defense) relevant, bool randomIrrelevant)
    {
        IV_Value Value(bool isRelevant) => isRelevant
            ? new IV_Value(true, 100, 100)
            : randomIrrelevant ? IV_Value.Random : new IV_Value(false, 50, 50);
        return new IV_Set(Value(relevant.HP), Value(relevant.Attack), Value(relevant.Defense));
    }
}
