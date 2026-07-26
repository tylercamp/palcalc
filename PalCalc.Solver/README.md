# PalCalc.Solver

`PalCalc.Solver` searches the breeding graph for efficient ways to produce a
target pal with requested passives, IV thresholds, and gender. It starts with
owned and optionally wild pals, repeatedly expands useful parent pairs, and
stops when the retained search frontier reaches a fixed point or the configured
iteration limit is reached.

The primary objective is currently estimated breeding effort. The secondary
result-pruning pipeline preserves a small, ordered set of alternatives for each
equivalent breeding state.

## Public API

A solve is defined by a `BreedingSolverRequest`, executed by
`BreedingSolver`, and returns a read-only `BreedingSolverResult`.

```csharp
var settings = new BreedingSolverSettings(
    db: db,
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
collections. `GameSettings` and model objects are treated as shared,
effectively read-only objects for the duration of a run.

`SolverStateController.Pause()` and `Resume()` use a lock-free flag. Cancellation
is supplied through its constructor. Cancellation may leave partial results in
`BreedingSolverResult`, but normal consumers discard them when
`IsCanceled` is true.

## Run-scoped mechanics

Every `PalDB` owns an immutable `BreedingMechanics` value containing:

- IV inheritance probabilities
- Direct and random passive probabilities
- Wild passive-count probabilities
- Capture-effort inputs
- Mechanics-derived IV probability tables

`SolverRunContext` captures the database's current mechanics when a solve
starts. Replacing `PalDB.BreedingMechanics` therefore affects later solves
without changing a run already in progress. Multiple customized databases can
coexist in one process.

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

### `InitialPalBuilder`

Builds the initial owned and wild candidates. It filters unreachable inputs,
applies irrelevant-passive limits, selects representative save instances, and
creates composite opposite-gender owned references.

Owned candidates are reduced with structural pal, passive, IV-relevance, and
gender keys. Hash-based presentation grouping is not used for solver identity.

### `SearchFrontier`

Owns the retained breeding states and is the authoritative single writer.
`FrontierIndex` uses `BreedingStateKey`, which contains structural pal identity,
gender, an order-independent passive multiset, and IV-relevance flags.

Each iteration:

1. Expands the pending parent-pair schedule.
2. Collects terminal candidates independently of whether they remain useful as
   parents.
3. Selects retained alternatives for each structural state.
4. Produces a `FrontierDelta` of added and removed candidates.
5. Advances the incremental pair schedule using that delta.

When a merge adds or removes nothing, the frontier is at a fixed point and the
search ends early.

### `ParallelBatchExecutor` and `CandidateExpander`

The executor owns thread creation, work chunking, progress aggregation, and
worker exception propagation. Each worker creates one `CandidateExpander` and
one `ObjectPoolFactory`.

The expander performs the hot-loop breeding work:

- Reject incompatible or over-limit parents.
- Check whether a possible child can still reach the target.
- Resolve wildcard genders, including gender-specific breeding recipes.
- Merge relevant IV state.
- Calculate passive and IV inheritance probabilities from the run's mechanics.
- Emit candidates that pass cheap admission checks.

Pools remain worker-local because profiling showed that pooling materially
reduces allocation and GC overhead in this path.

### Candidate selection and result pruning

The internal candidate-selection policy coordinates:

- Structural state identity
- Cheap early comparisons
- Frontier admission hints
- Authoritative alternative selection
- Expansion ordering
- Terminal result tiers

Breeding effort is the current primary objective. Strict improvement in effort
may immediately obsolete the old frontier state. Cost and IV comparisons can
admit a promising candidate but do not immediately obsolete alternatives,
because the full secondary pipeline may prefer a different path.

`ResultPruningPolicy` configures that relatively expensive secondary pipeline.
It remains outside the parallel candidate hot loop. The default rules consider
effort, steps, IV quality, cost, location, reuse, wild pals, referenced players,
variety, and a final result limit.

`MinimumReusePruning` retains its current historical behavior.

### `ResultAccumulator` and `ResultPostProcessor`

Terminal candidates are captured before frontier selection because an optimal
result need not remain useful as a future parent. Results are retained
separately for each primary-effort tier.

After the breeding search, `ResultPostProcessor` performs the deliberately
post-search surgery pass and applies final irrelevant-passive and gender
constraints.

The UI may reduce the returned alternatives further to avoid presenting
hundreds of similarly efficient paths. `PalResultGrouping` and
`PalResultProperty` exist for this presentation-level purpose only; they are
not part of frontier identity or solver correctness.

## Search coverage and constraints

Within the retained-state model, the solver schedules all parent combinations
introduced by frontier deltas and normally reaches a fixed point well before
`MaxSolverIterations`. For the default primary metric, breeding effort, this is
normally an exhaustive search of useful optimized states.

The result is still subject to intentional modeling assumptions and secondary
alternative pruning:

- Probability models are estimates of game behavior.
- Irrelevant passives are represented approximately in some bred states.
- Surgery is applied once after the breeding search.
- Only a limited set of equally primary-efficient provenance alternatives is
  retained per structural state.
- An explicitly low `MaxSolverIterations` can stop before convergence.

These affect probability accuracy or which equivalent path is shown; they do
not turn the main loop into a beam search over arbitrary pal states.

## Additional details

- [Passive inheritance estimation](./README-BREED-ESTIMATE.md)
- [Miscellaneous optimization notes](./README-MISC.md)
- `PalCalc.Solver.CLI/Program.cs` contains an end-to-end usage example.
