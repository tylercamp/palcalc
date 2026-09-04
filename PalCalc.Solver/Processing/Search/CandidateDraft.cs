using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.Processing.Search;

/// <summary>
/// Stack-local candidate state used to run the early filters before creating
/// the retained profile array and breeding-tree node.
/// </summary>
internal struct CandidateDraft
{
    private readonly GameSettings gameSettings;
    private readonly IPalReference parent1;
    private readonly IPalReference parent2;
    private readonly float passivesProbability;
    private readonly float ivsProbability;
    private BredPalReference materialized;

    public CandidateDraft(
        GameSettings gameSettings,
        Pal pal,
        IPalReference parent1,
        IPalReference parent2,
        List<PassiveSkill> passives,
        float passivesProbability,
        IV_Set ivs,
        float ivsProbability,
        TimeSpan selfBreedingEffort,
        TimeSpan breedingEffort,
        PreparedAttackProfile attackProfile
    )
    {
        this.gameSettings = gameSettings;
        this.parent1 = parent1;
        this.parent2 = parent2;
        this.passivesProbability = passivesProbability;
        this.ivsProbability = ivsProbability;
        materialized = null;

        Pal = pal;
        EffectivePassives = passives;
        EffectivePassivesHash = passives.SetHash(passive => passive.InternalName);
        IVs = ivs;
        SelfBreedingEffort = selfBreedingEffort;
        BreedingEffort = breedingEffort;
        TotalCost = parent1.TotalCost + parent2.TotalCost;
        AttackProfile = attackProfile;
    }

    public Pal Pal { get; }
    public PalGender Gender => PalGender.WILDCARD;
    public List<PassiveSkill> EffectivePassives { get; }
    public int EffectivePassivesHash { get; }
    public IV_Set IVs { get; }
    public TimeSpan SelfBreedingEffort { get; }
    public TimeSpan BreedingEffort { get; }
    public int TotalCost { get; }
    public PreparedAttackProfile AttackProfile { get; }
    public bool IsMaterialized => materialized is not null;

    public BredPalReference Materialize() => materialized ??= new BredPalReference(
        gameSettings,
        Pal,
        parent1,
        parent2,
        EffectivePassives,
        passivesProbability,
        IVs,
        ivsProbability,
        AttackProfile.Materialize(),
        materializedAttackInheritance: null,
        avgRequiredBreedings: null,
        Gender
    );

    public override int GetHashCode() => HashCode.Combine(
        nameof(BredPalReference),
        Pal,
        parent1.GetHashCode() ^ parent2.GetHashCode(),
        EffectivePassivesHash,
        HashCode.Combine(BreedingEffort, SelfBreedingEffort),
        Gender,
        IVs,
        AttackProfile
    );
}
