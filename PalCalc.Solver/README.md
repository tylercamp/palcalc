# Solver Overview

PalCalc finds practical breeding paths from the Pals a player already has, or
is willing to capture, to a target Pal with the requested passives, IVs,
attack skills, and gender.

For example, you might ask for an Anubis with Legend, Earth Emperor, and at
least 90 attack IV. PalCalc will work through all the possibilities and
return complete breeding trees: which owned or wild Pals to start with, which
parents to breed at each step, and roughly how much effort each path should
take.

The shortest tree is not always the fastest one. A two-step path with poor
inheritance odds may take longer than a three-step path whose children are much
more likely to have the right traits. The solver compares paths by their
estimated effort and keeps a small selection of useful alternatives.

For the game rules, see [Palworld Breeding Mechanics](../PALWORLD-BREEDING-MECHANICS.md).
For performance and simplicity, PalCalc intentionally ignores or simplifies
certain mechanics. The [limitations page](../LIMITATIONS.md) describes the practical
effects of these choices; this overview explains the relevant implementation
tradeoffs.

Start with [Building a Breeding Tree](#building-a-breeding-tree---the-algorithm)
for the general explanation. The [Detailed Solver Walkthrough](#detailed-solver-walkthrough)
maps the same process to the classes that implement it.

## Concepts at a Glance

- **Target** - The Pal being requested, including its required and optional
  passives, required attacks, IV thresholds, gender, and any limits placed on
  the search.
- **Candidate** - A Pal the solver might use in a breeding tree. It can be one
  you own, one you can catch, or a child from an earlier step. Sometimes one
  candidate stands for several actual Pals.
- **Frontier** - The useful candidates found so far. Any Pal in the frontier
  may become a parent in a later breeding step. This is important - it gives
  us the breeding pairs to check, and acts as a record of the fastest way
  to reach any given Pal.
- **Effective properties** - The parts of a Pal that matter for the current
  target: species, gender, useful passives, and whether each requested IV
  threshold can be met. If two Pals have the same effective
  properties, either one will work in the same future breeding steps.
  (Not seen here: attack skills, and for good reason. This will be mentioned later.)
- **Breeding effort** - The estimated time to obtain a Pal.
  `BreedingEffort` covers its complete breeding tree, while
  `SelfBreedingEffort` covers only the work introduced by that candidate.
- **Simplification** - Dropping paths that are slower or don't add anything
  new, while keeping the best paths and a few useful alternatives. This is also
  called *pruning*.
- **Completed result** - A Pal that matches the target. It is saved even when
  the solver does not need it as a parent. This is also called a *terminal result*.

After each round, the solver records what changed in the frontier. This change
is called a **frontier delta**. It lets the solver try the new parent pairs
without repeating work it has already done.

## Building a Breeding Tree - "The Algorithm"

Imagine trying to find a breeding tree by hand. You'd start with the Pals
available to you, choose two that could make a useful child, and add that child
to the list of possible parents. You keep doing this until one of the
children matched your target.

The solver follows the same general process:

It first builds a frontier from the owned and wild Pals allowed by the solver
settings. It skips Pals that cannot help with the target. When several owned
Pals would work the same way, it picks one to represent them. It can also
combine matching male and female Pals so either gender is available later.

The solver then breeds the Pals in the frontier. **"Useful"** children are added back
to the frontier, so they can become parents in the next round. Children that
already satisfy the target are also saved as completed results.

This repeats until a round produces no useful new Pals, or until the configured
iteration limit is reached.

### What makes a child "useful"?

A child is useful when it adds a new or better way to reach the target. It may
introduce the right Pal for a later breeding combination, collect useful
passives in one place, carry an IV that can meet a requested threshold, or
provide the gender needed for another pair.

The meaning of "useful" depends on the target. If the request only requires an
attack IV, then exact health and defense IVs are ignored when comparing two
candidates. Likewise, a high-tier, valuable passive may still be irrelevant
if it's not one of the passives we wanted in the final target pal.

The solver calls this smaller, target-specific view of a Pal its **effective
properties**. Grouping candidates this way lets it compare paths that will have
the same effect on every future breeding step.

#### Passives

The solver uses two views of a Pal's passives. Its **actual passives** are the
passives it really has. These are used whenever possible when calculating
inheritance odds, matching how Palworld uses the parents' real passives in its
own calculations. Keeping that information also avoids double-counting
passives or checking the wrong inheritance probabilities.

For the broader search, the solver usually condenses those into **effective
passives**. Desired passives keep their names, while irrelevant or unknown
passives are replaced with placeholders. This smaller view makes it much
easier to compare the large number of Pals found during a solve without losing
the actual passive information needed for probability calculations.

**Desired passives** are the required and optional passives requested for the
target. Required passives must appear on the breeding result. Optional passives
are preferred when there is room for them, but are not needed for a valid
result.

The distribution of passives between the parents has no effect on probabilities.
Palworld combines and deduplicates both parents' passives before rolling
inheritance, so a 2/2 split is no better than a 1/3 or 0/4 split.

**What gets tracked:**

1. The list of actual passives, for inheritance probabilities.
2. The list of effective passives, for comparing similar Pals.

#### IVs

An IV is relevant when it can satisfy a threshold in the current request.
"Relevant" describes its usefulness for this target, not whether the stat is
generally good.

Some candidates stand for more than one individual Pal, so the solver records
their IVs as ranges. During the search, the important question is usually
whether that range can meet the requested threshold. Exact values can still
help choose between otherwise similar paths.

**What gets tracked:** Each IV is stored as...

1. A range of values based on the parent IVs.
2. A flag which says whether the IV is "relevant".

#### Attacks

Attack inheritance in Palworld is only based on the attacks _equipped_
by each parent, making it easy to manipulate inheritance probabilities.

There's an extra bonus of "ignore-inherit" attacks, which are exclusive
to certain pals and ignored entirely in the inheritance process. If you
have one parent with an "ignore-inherit" attack, and the other parent has
just 1 normal attack equipped, the child has a 100% chance to inherit the
"1 attack" from that other parent.

As PalCalc considers each possible child, it records the combinations of
attacks which can be made available on that child, based on the parents or
their breeding paths. These records are called **attack profiles**.

For example, the same pair might pass on either of two requested attacks
through ordinary breeding, or both together using a Special Cake. Each breeding
method gets its own candidate and profile. That profile records which attacks
are possible without building a separate tree for every parent loadout.

The final choice of "who equips which attack" is saved for the end. At that
point, the solver selects concrete parent loadouts and includes their attack
inheritance odds in the effort estimate.

**What gets tracked:**

1. Which combinations of requested attacks the child can obtain.
2. Whether a Pal can equip a non-inheritable attack to improve inheritance odds
   when paired with a parent that carries a requested attack.

_Note: The other properties in this list are used as "Effective Properties", but_
_attack skills are treated differently. Attack combinations can become so large_
_that it's hard to compare paths efficiently. Instead, the other properties here_
_are used for grouping, and attack skills are preserved separately._

#### Gender

The solver doesn't always need to choose a candidate's gender right away. A
wildcard gender means it can choose the required gender later and include the
chance of obtaining that gender in the effort estimate. An opposite-wildcard
simply takes whichever gender is opposite the other parent.

If the player owns equivalent male and female Pals, the solver can combine them
into a composite owned candidate. A later step can use whichever owned Pal has
the required gender instead of breeding another copy just for its gender.

**What gets tracked:**

1. The "guaranteed gender" of a given Pal. If the gender can't be guaranteed
   (for example, for a wild Pal), then a "wildcard" gender value is used.

## Estimating the Effort of a Path

Once the solver finds a possible child, it estimates how much work it should
take to produce that child with the required properties.

Suppose a child has an 8% chance of inheriting the passives and IVs needed for
the next step. That works out to about one success per 12 or 13 attempts. The
solver multiplies those average breeding attempts by the configured time per
breed to estimate the time spent making that child.

Depending on the solver settings, effort can include:

- the effort already spent obtaining both parents
- passive, IV, and attack inheritance probabilities
- the chance of obtaining the required gender
- breeding and incubation time
- the effort needed to capture a wild Pal

This is why the fewest breeding steps do not always produce the fastest path.
It is also why an extra irrelevant passive can matter: even when two children
have the same desired passives, their actual passives may give them different
inheritance odds in the next step.

`SelfBreedingEffort` is the work introduced by one candidate, such as the
attempts needed to breed that child. `BreedingEffort` includes that work and
the effort of the complete parent paths leading to it.

The estimate is an average, not a promise. You might get the desired child on
the first egg, or you might need many more attempts than expected. The estimate
gives the solver a consistent way to compare whole breeding trees.

See ["Estimating Breeding Time for Inheriting Passives"](./README-BREED-ESTIMATE.md) for the
probability calculations used by the solver.

### Choosing the Breeding Method

For each pair, the solver considers ordinary breeding and, when the settings
allow it, breeding with a Special Cake. Each method gets its own candidate
because it changes passive inheritance and the attacks the child can obtain.
During search, the solver estimates effort from that method's passive
probability and the IV odds. It includes the odds for specific attack loadouts
when finishing the results.

Ordinary candidates respect the limit on unwanted passives
(`MaxBredIrrelevantPassives`). Special Cakes aim to fill all four passive slots,
so their candidates can exceed that limit. Extra unwanted passives still affect
inheritance in later generations.

_Note: The game's [random passive-selection bug](../LIMITATIONS.md#random-passive-selection-bug)
can leave a child short of four passives. PalCalc assumes the random filling
succeeds when estimating Special Cake outcomes._

The solver's cake usage policy controls when these candidates are introduced.
This is managed by the `SpecialCakePolicy` setting, exposed as the
`Allow Special Cakes for Passives` checkbox in the app.

- `AttackInheritanceOnly` (checkbox disabled) requires multiple requested attacks that the child
  doesn't already learn at level one. The UI uses this policy by default.
- `AttacksAndPassives` (checkbox enabled) also considers cakes without a multiple-attack
  requirement when required or optional passives remain in the breeding search.
  If surgery defers all passive targets, the solver uses the attack-only rule.
  Searching for more cake options may take longer.
- `Never` (not possible through the app) disables cake candidates.

## Keeping the Search Manageable

The number of possible breeding trees explodes as the search continues. Every
useful child becomes another possible parent, and every new parent can be
paired with the Pals already found. Keeping every possible way of reaching
every child would massively expand the pool of paths the solver has to search.

PalCalc simplifies the frontier after each round. If two candidates have the
same effective properties, they are equally useful in future breeding. The
solver compares the paths that produced them and can drop one that clearly
takes longer.

Lower breeding effort is the clearest improvement. If the solver finds the
same effective Pal through a faster path, the slower path can be ignored
entirely.

Effort is not the only useful difference, however. Two paths can have the same
effort while differing in the number of steps, IV quality, cost, Pal
locations, and other practical details. The solver uses those differences to
decide which alternatives are worth keeping instead of simply taking whichever
path it found first.

Completed results are handled separately. A Pal can be a good final answer
even when another equivalent Pal would make a better parent, so the solver
saves results before simplifying the frontier.

### Comparing Paths

Special Cakes add another useful difference between paths. A slower path using
one Special Cake may be worth keeping alongside a faster path using five.
The solver compares both breeding time and cake use, including in searches
that only target passives. To keep the search manageable, it retains the
cheapest and fastest alternatives for each attack combination. Intermediate
compromises may be missed.

When attacks are requested, a replacement must also preserve the attack
combinations the path can provide. The solver postpones choosing parent
loadouts and including their attack inheritance odds until finalization.

These comparisons use the search estimates. A path that looks better at this
stage may become more expensive once the solver resolves its attack choices.

## Search Coverage and Limits

The solver tries every new parent combination among the candidates it retains.
When simplification changes the frontier, only pairs involving those changes
need to be scheduled; combinations between unchanged candidates have already
been considered.

`MaxSolverIterations` puts a hard limit on the number of rounds, but the solver
typically finishes much earlier. Since it only preserves the optimal results
as they are discovered, it eventually and naturally reaches the limit of how
"optimal" the breeding results can be. At that point, every child it can
produce is equivalent to or worse than something it has already kept, so the
frontier stops changing and there are no new parent pairs to try.

"Optimal" therefore means optimal according to the solver's effort model and
candidate-selection rules. PalCalc does not preserve every possible ancestry
for every Pal. It preserves the fastest useful candidates it finds, along with
a limited selection of alternatives that differ in practical ways.

These comparisons use the solver's simplified model and configured limits.
They don't guarantee the fastest possible real-world plan; the
[limitations page](../LIMITATIONS.md) explains which options and outcomes can be missed.

## Finishing the Results

A completed breeding path still needs a final pass before it is returned.

Once the main search finishes, the solver applies surgery options, which can
add, remove, or change passives at a cost. Considering every possible surgery
during every breeding round could uncover more intermediate combinations, but
it would also multiply the number of paths the solver has to search.

Each breeding step already has its breeding method (standard or special cake)
selected by the search process. When attacks
are requested, finalization chooses specific parent loadouts within that method
and includes inheritance odds in the effort and cake estimates.

We can save this detail for the end because attacks are _very_ easy to manipulate.
The general guideline is to assign attacks as late in the tree as possible.

The solver checks that the completed trees have the requested passives,
attacks, and gender before returning the results.

## Detailed Solver Walkthrough

The following maps the search steps to their implementation, in order.

### 1. Prepare the solve

1. `BreedingSolver` receives a `BreedingSolverRequest` containing the target
   and settings.
2. `SolverRunContext` keeps the original `Target` for starting-candidate
   selection and final requirements, and derives a `BreedingTarget` for expansion.
   With **Simplify Surgery Search**, some required passives can be dropped from the
   list and deferred for surgery at the end.
3. When passive cake use is enabled but no desired passives remain in
   `BreedingTarget`, the context uses the attack-only cake policy. It also stores
   the mechanics, breeding database, run controller, and shared
   `ICandidateSelectionPolicy` for the run.
4. `SolverRun` runs the search and finishes the results.

### 2. Build the starting candidates

`InitialPalBuilder` creates the owned and wild candidates that can contribute
to the target.

1. Apply the request's input limits and discard Pals that cannot help reach the
   target.
2. When owned Pals have the same useful properties, choose one to represent
   them.
3. Represent allowed wild Pals, including the effort needed to capture them.
4. Create wildcard gender representations where gender can be resolved later.
5. When the player owns matching male and female Pals, create a composite
   candidate that can supply either gender.
6. Build initial attack profiles from owned mastered attacks and attacks
   available at the minimum wild level. Add training variants when enabled.
7. Add the resulting candidates to the initial frontier.

### 3. Create the frontier and first parent schedule

`SearchFrontier` keeps track of the candidates retained for future breeding,
the pending parent pairs, and the completed results found during the run.

1. `FrontierIndex` groups the initial candidates by
   `EffectivePropertiesKey`.
2. `ResultAccumulator` checks whether any starting candidate already satisfies
   the target.
3. `ParentPairSchedule` creates the initial set of parent combinations.
4. The ordering supplied by the candidate-selection policy determines which
   retained candidates are expanded first.

### 4. Expand the pending parent pairs

`ParallelBatchExecutor` divides the scheduled pairs among workers. Each worker
uses its own `CandidateExpander` and object pools so pair expansion can run in
parallel without sharing temporary objects.

For each pair, `CandidateExpander`:

1. Checks whether the parents are compatible.
2. Applies request limits, such as breeding-step and wild-Pal restrictions,
   that can reject the pair early.
3. Resolves wildcard genders and handles recipes whose child depends on which
   parent is male or female.
4. Finds the child species the pair can produce.
5. Determines the useful passive and IV outcomes for that child, using
   `BreedingTarget` for the passives needed during breeding.
6. Creates ordinary and eligible Special Cake candidates with their own
   `BreedingOutcome`, which records the method and passive probability.
7. Uses `AttackProfileComposer` to record feasible attack outcomes for each
   method. Concrete loadouts and their attack probabilities are deferred.
8. Estimates breeding effort and cake cost, then offers the candidates to
   `CandidatePreFilter`.

`CandidatePreFilter` uses quick policy assessments and a `CandidateCostFrontier`
per effective-property group to retain the cheapest and fastest alternatives
for each exact attack mask, or one comparison group without attack targets.
The frontier performs the complete simplification after collecting the batch.

`CandidateDraft` lets these checks reject a candidate before allocating its
full `BredPalReference`, unless terminal or surgery retention needs that reference.

The cake quantities on a breeding path have distinct roles.

| Property | Role |
| --- | --- |
| `SelfSpecialCakes` | Expected special cakes for this step, including repeated attempts. |
| `ExpectedSpecialCakes` | The current whole-tree count, including parent costs and gender or attack choices as they are resolved. |
| `BaseSpecialCakes` | The minimum special cakes for the whole-tree count, without e.g. gender requirements. |

_Technical note: six requested attacks give 64 possible masks. `AttackProfile`
stores the feasible outcomes in one `ulong` using `1UL << IndividualMask`.
Bit zero represents the valid outcome with none of the requested attacks.
Clearing every bit represents no feasible outcome, and also serves as the
inactive profile when attack solving is disabled._

### 5. Save completed results

Any candidate that satisfies the target is saved as a completed result. This
happens separately because a valid result may not be one of the candidates
worth retaining as a future parent.

`ResultAccumulator` collects candidates that satisfy the original target.
`CandidatePreFilter` also preserves terminal candidates separately from
parent-selection decisions when attacks or passive-cake options are active.
`SurgeryFinalistAccumulator` retains candidates that can meet the target after
allowed surgery, so early pruning doesn't discard those options.

Final grouping and selection happen after post-processing through
`ICandidateSelectionPolicy.SelectFinalResults`.

### 6. Simplify and update the frontier

The frontier merges the children from the batch with the candidates already
retained. `DefaultCandidateSelectionPolicy` runs the simplification using the
rules configured by `ResultPruningPolicy`, then uses `CandidateCostFrontier`
to preserve useful cake/time alternatives and attack coverage.

_Technical note: matching `EffectivePropertiesKey` values put candidates in the
same comparison group. Replacement also depends on costs and, when attacks are
requested, coverage of the represented attack outcomes._

1. Group candidates by their effective properties.
2. Treat lower breeding effort as a guaranteed improvement when the new path
   also covers the incumbent's attack outcomes and uses no more estimated cakes.
3. Treat better cost or IVs as possible improvements that still need the full
   simplification pass.
4. Apply the ordered rules from `ResultPruningPolicy`, including effort, steps,
   IV quality, cost, location, reuse, wild Pals, referenced players, variety,
   and the configured result limit.
5. Keep the selected paths for each effective-property group.
6. Supplement those selections with the cheapest and fastest providers per exact
   attack mask from `CandidateCostFrontier`, or one pair without attack targets.
   This can exceed the ordinary result limit, but doesn't retain every middle
   cake/time tradeoff.
7. Produce a `FrontierDelta` containing the candidates added to and removed
   from the frontier.

`FrontierIndex` is updated from that delta. `ParentPairSchedule` then adds the
new combinations introduced by the change and removes work that is no longer
relevant. Pairs between unchanged candidates are not repeated.

### 7. Repeat or stop

The solver expands the new parent pairs and simplifies the frontier again.
Each successful round can introduce intermediates that make another generation
of breeding possible.

The loop stops when:

- simplification produces no newly useful candidates and no new parent pairs,
- the configured iteration limit is reached, or
- the run is cancelled or stopped because of an error.

### 8. Apply surgery and return the results

`SolverRun` asks `ResultPostProcessor` to apply surgery through the frontier,
including the separately retained surgery finalists, then finalize the completed
candidates.

1. Apply allowed surgery operations and their costs, retaining useful results.
2. Resolve the required gender and check the original target's requirements.
3. For attack targets, `AttackResultMaterializer` evaluates specific parent
   loadouts for their preselected breeding methods and recalculates probabilities,
   effort, and cake use.
4. For attack or passive-cake searches, compare completed alternatives through
   `FinalizationAlternatives` and `FinalizationComparison`. Passive-only cake
   searches use the path costs directly. Attack searches use the evaluated
   costs and reconstruct the selected trees.
5. Reject reconstructed paths that exceed configured limits or fail the target.
6. Ask `ICandidateSelectionPolicy.SelectFinalResults` to group and simplify the
   remaining candidates. The default policy uses its effort groups and, when
   preserving cake options, also separates outcomes and expected cake counts.
7. Return the remaining paths as a `BreedingSolverResult`.

A faster path may need more Special Cakes than a slower one, so we keep both
when neither is clearly better. We can drop an option if another takes no more
effort and uses no more cakes, with an improvement in at least one.

The UI may apply additional grouping and filtering to avoid massive lists of
results, using `PalResultGrouping` and `PalResultProperty` for presentation.

## Mechanics used by the solver

The [mechanics reference](../PALWORLD-BREEDING-MECHANICS.md) explains
the game rules and probabilities. This section explains how PalCalc uses them
in its estimates. The [research checklist](../docres/research/palworld-breeding/README.md#current-verification-status)
records the latest fully verified game version - currently 1.0.5.102999 - and links
to the findings and evidence. Various comments on PalCalc's approximations are collected in
[LIMITATIONS.md](../LIMITATIONS.md).

### Species database

GenDB reads breeding powers, priorities and special recipes from game data
(`CombiRank` and `CombiDuplicatePriority` in `DT_PalMonsterParameter`, and
`DT_PalCombiUnique`). It generates `breeding.json` and `breedingdb.json`, which
are embedded in PalCalc. The solver looks up child species in that database
instead of repeating the [game's selection process](../PALWORLD-BREEDING-MECHANICS.md#pal-species)
for every pair during a solve.

The generated database uses some shortcuts and different tie handling:

- GenDB doesn't read `IgnoreCombi` when building its eligible roster.
- Its same-species shortcut runs before recipe matching.
- Recipe resolution assumes exactly one matching row.
- Exact distance/priority ties add a non-variant preference instead of retaining
  native table order.
- The generated graph has no input for the game's item rank adjustment.

### IV probabilities

The [IV source-selection explanation](../PALWORLD-BREEDING-MECHANICS.md#iv-source-selection)
gives the game's patterns and default weights (`Combi_TalentInheritNum` in
`BP_PalGameSetting`). PalCalc uses those patterns to answer two questions:

1. What is the chance that all requested stats come from the parents?
2. For each of those stats, which parents meet the requested threshold?

For the default `[3, 2, 1]` weights, the first question gives the following
overall probabilities.
(An HP+Attack target counts both the HP+Attack and all-three inheritance patterns.)

| Desired stats | Chance all are taken from parents |
| --- | ---: |
| HP | 2/3 |
| Attack, or Defense | 5/9 |
| HP+Attack, or HP+Defense | 7/18 |
| Attack+Defense, or all three | 5/18 |

For example, say we only want the Attack IV with a threshold of 90. One
parent has 95 and the other has 60. Attack comes from a parent with probability
5/9, and only one of the two parents qualifies, so PalCalc gives this IV
requirement a success chance of `5/9 × 1/2 = 5/18`, or about 27.8%. If both
parents meet the threshold, the chance is 5/9 instead.

For multiple requested stats, each stat with only one qualifying parent adds
another factor of 1/2.

This model omits talent bonuses, mutation, and fresh random IVs that happen to
meet the target. See [desired traits supplied by fresh random values](../LIMITATIONS.md#desired-traits-supplied-by-fresh-random-values).

### Attack inheritance details

The [attack-inheritance rules](../PALWORLD-BREEDING-MECHANICS.md#activeattack-skills)
explain ordinary and Special Cake selection. PalCalc combines and deduplicates
eligible equipped-parent attacks, excluding non-inheritable attacks
(`IgnoreRandomInherit` in `DT_WazaDataTable`) and attacks missing from its database.

PalCalc tracks the achievable combinations
during search and chooses parent loadouts during [finalization](#finishing-the-results).
The [breeding-method policy](#choosing-the-breeding-method) controls when Special
Cake candidates enter the search.

### Egg-production time

PalCalc _estimates_ production time per attempt as follows:

```text
time_per_attempt = BreedingTime * 2 * parent_A_time_factor * parent_B_time_factor
```

[GameSettings](../PalCalc.Model/GameSettings.cs) defaults `BreedingTime` to five
minutes and doubles it to approximate parents being available only half the time (due to day/night cycle).

Producing the egg is separate from incubating it. The
[effort calculation](../PalCalc.Solver/PalReference/BredPalReferenceEffort.cs) multiplies
production time by expected attempts, then combines it with incubation according
to the configured incubator model.


See the [timing research status](../docres/research/palworld-breeding/README.md#supporting-mechanics)
for the scope of completed game-code verification.


## Implementation Map

The selection policy is shared across search and finalization.

```text
BreedingSolver
  -> SolverRunContext
       -> AttackTargetContext
       -> ICandidateSelectionPolicy (DefaultCandidateSelectionPolicy)
  -> SolverRun
       -> InitialPalBuilder
       -> SearchFrontier
            -> FrontierIndex
            -> ParentPairSchedule
            -> ResultAccumulator
            -> ICandidateSelectionPolicy
                 -> CandidateCostFrontier
       -> CandidatePreFilter per iteration
            -> CandidateCostFrontier per effective-property group
            -> SurgeryFinalistAccumulator
       -> ParallelBatchExecutor
            -> CandidateExpander per worker
                 -> worker-local object pools
                 -> AttackProfileComposer
                      -> AttackProfile
                 -> CandidatePreFilter
       -> ResultPostProcessor
            -> AttackResultMaterializer
            -> FinalizationAlternatives / FinalizationComparison
            -> ICandidateSelectionPolicy.SelectFinalResults
  -> BreedingSolverResult
```

Further references:

- [Passive inheritance estimation](./README-BREED-ESTIMATE.md)
- [Miscellaneous optimization notes](./README-MISC.md)
- Child species: [`PalBreedingCalculator.cs`](../PalCalc.GenDB/PalBreedingCalculator.cs)
  and [`UniqueBreedComboReader.cs`](../PalCalc.GenDB/GameDataReaders/UniqueBreedComboReader.cs)
- Game settings: [`GameSettingReader.cs`](../PalCalc.GenDB/GameDataReaders/GameSettingReader.cs)
  and [`BreedingMechanics.cs`](../PalCalc.Model/BreedingMechanics.cs)
- Passive estimates: [`Probabilities/Passives.cs`](./Probabilities/Passives.cs)
- IV estimates: [`Probabilities/IVs.cs`](./Probabilities/IVs.cs)
- Attack estimates: [`Probabilities/Attacks.cs`](./Probabilities/Attacks.cs)
- Attack inheritance flags: [`ActiveSkillReader.cs`](../PalCalc.GenDB/GameDataReaders/ActiveSkillReader.cs)
- [`PalCalc.Solver.CLI/Program.cs`](../PalCalc.Solver.CLI/Program.cs) contains
  an end-to-end usage example.
