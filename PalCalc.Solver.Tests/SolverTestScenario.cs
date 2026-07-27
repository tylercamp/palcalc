using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing;
using PalCalc.Solver.ResultPruning;

namespace PalCalc.Solver.Tests;

internal static class SolverTestScenario
{
    private static int nextInstanceId;

    public static PalDB DB { get; } = PalDB.LoadEmbedded();

    public static PalInstance Owned(
        string palName,
        PalGender gender,
        IEnumerable<PassiveSkill>? passives = null,
        int ivHp = 0,
        int ivAttack = 0,
        int ivDefense = 0
    )
    {
        var instanceIndex = Interlocked.Increment(ref nextInstanceId);

        return new PalInstance
        {
            InstanceId = $"solver-test-{instanceIndex}",
            OwnerPlayerId = "solver-test-player",
            Pal = palName.ToPal(DB),
            Gender = gender,
            PassiveSkills = passives?.ToList() ?? [],
            Location = new PalLocation
            {
                ContainerId = "solver-test-palbox",
                Type = LocationType.Palbox,
                Index = instanceIndex,
            },
            IV_HP = ivHp,
            IV_Shot = ivAttack,
            IV_Defense = ivDefense,
            ActiveSkills = [],
            EquippedActiveSkills = [],
        };
    }

    public static ConfiguredSolver Solver(
        IEnumerable<PalInstance> ownedPals,
        int maxBreedingSteps = 4,
        int maxSolverIterations = 4,
        int maxWildPals = 0,
        int maxBredIrrelevantPassives = 0,
        TimeSpan? maxEffort = null,
        int maxThreads = 1,
        GameSettings? gameSettings = null,
        int maxSurgeryCost = 0,
        IEnumerable<PassiveSkill>? allowedSurgeryPassives = null,
        IEnumerable<Pal>? allowedWildPals = null,
        IEnumerable<Pal>? bannedBredPals = null
    ) =>
        new(
            solver: new BreedingSolver(),
            settings: new BreedingSolverSettings(
                db: DB,
                breedingDB: PalBreedingDB.LoadEmbedded(DB),
                gameSettings: gameSettings ?? new GameSettings(),
                ownedPals: ownedPals.ToList(),
                resultPruning: ResultPruningPolicy.Default,
                maxBreedingSteps: maxBreedingSteps,
                maxSolverIterations: maxSolverIterations,
                maxWildPals: maxWildPals,
                allowedWildPals: allowedWildPals?.ToList() ?? [],
                bannedBredPals: bannedBredPals?.ToList() ?? [],
                maxInputIrrelevantPassives: GameConstants.MaxTotalPassives,
                maxBredIrrelevantPassives: maxBredIrrelevantPassives,
                maxEffort: maxEffort ?? TimeSpan.MaxValue,
                maxThreads: maxThreads,
                maxSurgeryCost: maxSurgeryCost,
                allowedSurgeryPassives: allowedSurgeryPassives?.ToList() ?? [],
                useGenderReversers: false
            )
        );

    public static List<IPalReference> Solve(
        ConfiguredSolver solver,
        string targetPal,
        IEnumerable<PassiveSkill>? requiredPassives = null,
        IEnumerable<PassiveSkill>? optionalPassives = null,
        PalGender requiredGender = PalGender.WILDCARD,
        int ivHp = 0,
        int ivAttack = 0,
        int ivDefense = 0
    ) =>
        solver.Solver.Solve(
            new BreedingSolverRequest(
                new PalSpecifier
                {
                    Pal = targetPal.ToPal(DB),
                    RequiredPassives = requiredPassives?.ToList() ?? [],
                    OptionalPassives = optionalPassives?.ToList() ?? [],
                    RequiredGender = requiredGender,
                    IV_HP = ivHp,
                    IV_Attack = ivAttack,
                    IV_Defense = ivDefense,
                },
                solver.Settings
            ),
            new SolverStateController(CancellationToken.None)
        ).Results.ToList();

    internal sealed class ConfiguredSolver(
        BreedingSolver solver,
        BreedingSolverSettings settings
    )
    {
        public BreedingSolver Solver { get; } = solver;
        public BreedingSolverSettings Settings { get; } = settings;

        public event Action<SolverStatus> StatusUpdated
        {
            add => Solver.StatusUpdated += value;
            remove => Solver.StatusUpdated -= value;
        }
    }

    public static IReadOnlyList<ResultSignature> Signatures(IEnumerable<IPalReference> results) =>
        results
            .Select(ResultSignature.From)
            .Distinct()
            .OrderBy(r => r.PalInternalName, StringComparer.Ordinal)
            .ThenBy(r => r.Gender)
            .ThenBy(r => r.Passives, StringComparer.Ordinal)
            .ThenBy(r => r.IVs.HP.Min)
            .ThenBy(r => r.IVs.Attack.Min)
            .ThenBy(r => r.IVs.Defense.Min)
            .ThenBy(r => r.EffortTicks)
            .ThenBy(r => r.GoldCost)
            .ThenBy(r => r.BreedingSteps)
            .ThenBy(r => r.WildPals)
            .ToList();

    internal readonly record struct ResultSignature(
        string PalInternalName,
        PalGender Gender,
        string Passives,
        IV_Set IVs,
        long EffortTicks,
        int GoldCost,
        int BreedingSteps,
        int WildPals
    )
    {
        public static ResultSignature From(IPalReference result) =>
            new(
                PalInternalName: result.Pal.InternalName,
                Gender: result.Gender,
                Passives: string.Join(
                    "|",
                    result.EffectivePassives
                        .GroupBy(p => p.InternalName)
                        .OrderBy(g => g.Key, StringComparer.Ordinal)
                        .Select(g => $"{g.Key}:{g.Count()}")
                ),
                IVs: result.IVs,
                EffortTicks: result.BreedingEffort.Ticks,
                GoldCost: result.TotalCost,
                BreedingSteps: result.NumTotalBreedingSteps,
                WildPals: result.NumTotalWildPals
            );
    }
}
