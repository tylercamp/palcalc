using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.Processing.Search;

/// <summary>
/// Stack-local candidate state used to run the early filters before creating
/// a breeding-tree node.
/// </summary>
internal struct CandidateDraft
{
    private readonly GameSettings gameSettings;
    private readonly IPalReference parent1;
    private readonly IPalReference parent2;
    private readonly BreedingOutcome outcome;
    private readonly float ivsProbability;
    private readonly int requiredBreedings;
    private BredPalReference materialized;

    public CandidateDraft(
        GameSettings gameSettings,
        Pal pal,
        IPalReference parent1,
        IPalReference parent2,
        List<PassiveSkill> passives,
        BreedingOutcome outcome,
        IV_Set ivs,
        float ivsProbability,
        int requiredBreedings,
        TimeSpan selfBreedingEffort,
        TimeSpan breedingEffort,
        AttackProfile attackProfile
    )
    {
        this.gameSettings = gameSettings;
        this.parent1 = parent1;
        this.parent2 = parent2;
        this.outcome = outcome;
        this.ivsProbability = ivsProbability;
        this.requiredBreedings = requiredBreedings;
        materialized = null;

        Pal = pal;
        EffectivePassives = passives;
        EffectivePassivesHash = passives.SetHash(passive => passive.InternalName);
        IVs = ivs;
        SelfBreedingEffort = selfBreedingEffort;
        BreedingEffort = breedingEffort;
        TotalCost = parent1.TotalCost + parent2.TotalCost;
        TotalRequiredLevels = parent1.TotalRequiredLevels + parent2.TotalRequiredLevels;
        AttackProfile = attackProfile;
        SelfSpecialCakes = outcome.SpecialCakesFor(requiredBreedings);
        BaseSpecialCakes = parent1.BaseSpecialCakes + parent2.BaseSpecialCakes + SelfSpecialCakes;
    }

    public Pal Pal { get; }
    public PalGender Gender => PalGender.WILDCARD;
    public List<PassiveSkill> EffectivePassives { get; }
    public int EffectivePassivesHash { get; }
    public IV_Set IVs { get; }
    public TimeSpan SelfBreedingEffort { get; }
    public TimeSpan BreedingEffort { get; }
    public int TotalCost { get; }
    public int BaseSpecialCakes { get; }
    private int SelfSpecialCakes { get; }

    public int TotalRequiredLevels { get; }
    public AttackProfile AttackProfile { get; }
    public bool IsMaterialized => materialized is not null;

    public BredPalReference Materialize() => materialized ??= new BredPalReference(
        gameSettings,
        Pal,
        Gender,
        parent1,
        parent2,
        avgRequiredBreedings: requiredBreedings,
        EffectivePassives,
        outcome,
        IVs,
        ivsProbability,
        AttackProfile,
        materializedAttackInheritance: null
    );

    // Note: This should exactly mirror the hash calc for `BredPalReference`, since some
    // tie-breaker calcs use hash at the end
    public override int GetHashCode() => HashCode.Combine(
        nameof(BredPalReference),
        Pal,
        parent1.GetHashCode() ^ parent2.GetHashCode(),
        EffectivePassivesHash,
        HashCode.Combine(BreedingEffort, SelfBreedingEffort, outcome, BaseSpecialCakes, SelfSpecialCakes),
        Gender,
        IVs,
        AttackProfile
    );
}
