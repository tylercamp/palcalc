namespace PalCalc.Solver.PalReference.Properties;

public enum BreedingMethod
{
    Ordinary,
    SpecialCake,
}

/// <summary>The method and passive probability of one candidate outcome.</summary>
/// <param name="PassivesProbability">
/// Method-specific probability of satisfying the candidate's passive
/// requirements. For ordinary breeding, this accumulates acceptable results
/// up to the represented passive count, rather than describing exactly
/// the represented passive list. IV and attack probabilities are separate.
/// </param>
public readonly record struct BreedingOutcome(BreedingMethod Method, float PassivesProbability)
{
    public bool Allows(AttackInheritanceMode mode) => Method switch
    {
        BreedingMethod.Ordinary => mode is AttackInheritanceMode.Baseline or AttackInheritanceMode.Normal,
        BreedingMethod.SpecialCake => mode == AttackInheritanceMode.InheritAll,
        _ => throw new ArgumentOutOfRangeException(nameof(Method)),
    };

    public int SpecialCakesFor(int attempts) => Method == BreedingMethod.SpecialCake ? attempts : 0;
}
