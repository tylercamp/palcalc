# PalCalc.Solver

`PalCalc.Solver` finds practical breeding paths from the Pals a player has or
can capture to a target Pal with requested passives, IV thresholds, and gender.
It compares complete paths by their estimated breeding effort and retains a
small selection of useful alternatives.

For example, a request can ask for Anubis with Legend, Earth Emperor, and at
least 90 attack IV. The result describes which owned or wild Pals to start
with, which parents to breed at each step, and the estimated effort of reaching
the requested Pal.

## How a solve works

1. Build the starting candidates from allowed owned and wild Pals.
2. Pair candidates that have not previously been tried together.
3. Keep children that can still contribute to the target.
4. Simplify the working candidates and schedule the newly useful parent pairs.
5. Stop when a pass finds no new useful candidates or reaches the configured
   iteration limit.
6. Apply surgery options and final result constraints.

The solver calls its working collection of useful candidates the **frontier**.
When the frontier changes, only parent combinations introduced by that change
need to be explored. A **frontier delta** records the candidates added and
removed by one simplification pass.

## Essential vocabulary

### Candidates and effective properties

A **candidate** is an owned, wild, bred, composite, or surgery-based Pal that
can appear in a breeding path.

Candidates are grouped by the **effective properties** that affect future
breeding for the current target:

- Pal species and variant
- Gender representation
- Effective passives
- Whether each IV can satisfy its target threshold

Effort, cost, location, and breeding history are not effective properties.
Candidates that share effective properties can be used interchangeably in
future breeding, so the simplification rules decide which paths among them are
worth retaining.

### Passives

- **Actual passives** are known to be present on the represented Pal. They are
  used when calculating inheritance probability.
- **Desired passives** are all required and optional passives requested by the
  target.
- **Effective passives** retain the desired passives and replace other
  passives with placeholders. This is the smaller representation used while
  searching for the current target.
- A **random passive placeholder** means that a passive is irrelevant or not
  known precisely in the solver model. It does not necessarily mean that the
  game literally chose a random passive in that breeding history.

Required passives must be present for a result to satisfy the target. Optional
passives are preferred when room is available but are not required.

### IVs

An IV is **relevant** when it can satisfy the threshold requested for the
current target. This describes relevance to the request, not the general value
of that stat.

Some references hold an IV range because they represent more than one concrete
Pal. Choosing a specific gender or owned instance can later narrow that range.

### Gender representations

A wildcard gender means the solver can choose the required gender later and
includes the probability of obtaining it in the effort estimate. An
opposite-wildcard represents whichever gender is opposite another parent.
Gender reversers can change the cost of making that guarantee.

A **composite owned candidate** represents having both male and female owned
copies with the same effective properties. A later breeding step can select
the required concrete copy without breeding another Pal solely for gender.

### Breeding effort

Breeding effort estimates the work needed to produce the complete breeding
path, not just its final step. Depending on settings, it accounts for parent
effort, breeding and inheritance probabilities, incubation, capture effort,
gender probability, and breeding facilities.

`BreedingEffort` covers the complete path. `SelfBreedingEffort` covers only the
work introduced by that reference.

### Surgery

Surgery is the game mechanic that adds, removes, or changes passives at a cost.
The solver applies surgery after the main breeding search. Considering every
surgery option during every breeding step would model more combinations but
would substantially increase search cost.

## Public API

A solve is defined by a `BreedingSolverRequest`, executed by
`BreedingSolver`, and returned as a read-only `BreedingSolverResult`.

```csharp
var settings = new BreedingSolverSettings(
    db: db,
    breedingDB: breedingDB,
    gameSettings: gameSettings,
    ownedPals: ownedPals,
    resultPruning: ResultPruningPolicy.Default,
    maxBreedingSteps: 8,
    maxSolverIterations: 8,
    maxWildPals: 2,
    allowedWildPals: db.Pals,
    bannedBredPals: [],
    maxInputIrrelevantPassives: 2,
    maxBredIrrelevantPassives: 1,
    maxEffort: TimeSpan.FromDays(7),
    maxThreads: 0,
    maxSurgeryCost: 0,
    allowedSurgeryPassives: [],
    useGenderReversers: false
);

var request = new BreedingSolverRequest(
    new PalSpecifier
    {
        Pal = targetPal,
        RequiredPassives = requiredPassives,
        OptionalPassives = optionalPassives,
        IV_HP = 90,
    },
    settings
);

using var cancellation = new CancellationTokenSource();
var controller = new SolverStateController(cancellation.Token);
var solver = new BreedingSolver();

solver.StatusUpdated += status =>
{
    // Status objects are immutable snapshots.
};

var result = solver.Solve(request, controller);
if (!result.IsCanceled)
{
    foreach (var palReference in result.Results)
    {
        // Display or inspect the breeding path.
    }
}
```

The request normalizes and captures its target without mutating the caller's
`PalSpecifier`. `BreedingSolverSettings` copies mutable configuration
collections. Database and model objects are treated as shared, read-only data
for the duration of a run.

`SolverStateController.Pause()` and `Resume()` use a lock-free flag.
Cancellation is supplied through its constructor. Cancellation may leave
partial results in `BreedingSolverResult`, but normal consumers discard them
when `IsCanceled` is true.

## Architecture

One solve has the following flow:

```text
BreedingSolver
  -> SolverRunContext
  -> SolverRun
       -> InitialPalBuilder
       -> SearchFrontier
            -> FrontierIndex
            -> ParentPairSchedule
            -> ResultAccumulator
       -> ParallelBatchExecutor
            -> CandidateExpander per worker
                 -> worker-local object pools
       -> ResultPostProcessor
  -> BreedingSolverResult
```

### Run-scoped data

`SolverRunContext` captures the request data used throughout one solve,
including `PalDB`, `PalBreedingDB`, and the current `BreedingMechanics`.
Changing database mechanics affects later solves without changing one already
in progress. Injecting the breeding database also allows callers to supply
custom breeding combinations.

### Starting candidates

`InitialPalBuilder` creates owned and wild candidates that can reach the target.
It applies input limits, chooses representative save instances, and combines
suitable male and female owned Pals into composite candidates.

### Frontier and parent scheduling

`SearchFrontier` owns the candidates retained for future breeding, the pending
parent pairs, and completed results. `FrontierIndex` groups candidates using
`EffectivePropertiesKey`.

Each iteration expands the pending parent pairs, records completed results,
runs the full simplification pass, and updates the pair schedule from the
resulting frontier delta. The search stops early when the frontier no longer
changes.

### Parallel candidate expansion

`ParallelBatchExecutor` divides parent pairs among workers and reports progress
and errors. Each worker has a `CandidateExpander` and worker-local object pools.

`CandidateExpander` checks whether parents are compatible and within request
limits, resolves gender-specific recipes, determines possible children,
calculates inheritance probability, and emits promising candidates.

Workers apply a quick pre-filter to reduce obviously unhelpful candidates. The
frontier later applies the complete ordered simplification rules. Object pools
remain worker-local because profiling showed that allocations and shared-pool
contention added substantial overhead in this path.

### Candidate simplification

`DefaultCandidateSelectionPolicy` coordinates:

- effective-property grouping;
- the worker-side quick pre-filter;
- the full simplification pass;
- the order in which retained candidates are bred;
- grouping completed results with the same breeding effort.

Lower breeding effort is a guaranteed improvement and can immediately mark
matching frontier candidates as outdated. Better cost or IVs make a candidate
a potential improvement, but the full simplification pass decides whether its
complete breeding path is retained.

`ResultPruningPolicy` configures that full pass. The default rules consider
effort, steps, IV quality, cost, location, reuse, wild Pals, referenced players,
variety, and a final result limit. `MinimumReusePruning` currently favors paths
that reuse the same Pal references across multiple steps.

### Completed results and post-processing

`ResultAccumulator` records terminal candidates before frontier
simplification, because a completed result need not remain useful as a future
parent. It simplifies results separately within groups that have the same
breeding effort.

`ResultPostProcessor` then applies surgery and final passive and gender
constraints. The UI may reduce alternatives further to avoid presenting
hundreds of similarly efficient paths. `PalResultGrouping` and
`PalResultProperty` serve that presentation purpose only; they do not affect
solver correctness.

## Search coverage and limitations

Within its retained-candidate model, the solver schedules all parent
combinations introduced by frontier changes and normally stops well before
`MaxSolverIterations` because no new useful candidates are found. For the
default optimization goal of breeding effort, this normally explores all
useful optimized effective-property groups.

The result remains subject to intentional modeling assumptions:

- Probability models estimate game behavior.
- Some bred candidates represent irrelevant passives approximately.
- Surgery is applied once after the breeding search.
- Only a limited set of equally efficient breeding paths is retained for each
  effective-property group.
- A low `MaxSolverIterations` can stop the search before the frontier settles.

These assumptions can affect probability accuracy or which equivalent path is
shown. The solver does not arbitrarily cap the number of Pal-property groups
considered during each pass.

## Additional details

- [Passive inheritance estimation](./README-BREED-ESTIMATE.md)
- [Miscellaneous optimization notes](./README-MISC.md)
- `PalCalc.Solver.CLI/Program.cs` contains an end-to-end usage example.
