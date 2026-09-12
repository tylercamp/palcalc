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

A detailed explanation of Palworld breeding mechanics and calculations are
described [here](./README-PALWORLD-MECHANICS.md).

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

As PalCalc considers each possible child, it builds up a list of desired
attacks that can be achieved somewhere in the parents' breeding tree
(or on the parents themselves.)

While IVs and passives are tracked closely through the breeding process,
PalCalc just holds a loose collection of details for attacks as they
pass through each step. The final choice of "who equips which attack" is
saved for the very end.

**What gets tracked:**

1. A rough list of possible attacks for the child
2. Some info on how those attacks are distributed up the tree.

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

1. The "guaranteed gender" of a given Pal. If the gender can't be guaranteed (namely for "owned pals"), then a "wildcard" gender value is used.

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

### Comparing Attack Paths

PalCalc doesn't track each unique way to manage attack skills. For
each Pal it only records the attacks available up the chain, and some extra
information:

- The available pairings of desired attacks between the parents
- The number of Special Cakes required for each pairing
- Whether an "ignore-inherit" attack is available

Breeding effort is excluded here on purpose because it adds a lot of
tracking overhead. Instead, attack options are compared by the number of
Special Cakes required. A path which needs fewer Special Cakes
will _often_ require less breeding attempts than other approaches, making
it a semi-accurate stand-in for direct breeding effort. These cakes are
also high-level and expensive, making them even more important for comparisons.

_Technical note: the "available pairings" data is stored in a 64-bit `ulong`_
_bitfield for general comparisons, and an array of 8-bit `byte` bitfields for_
_associating arrangements with cake costs. With 6 order-independent attack slots,_
_there are only 6 bits needed to represent available attacks. There are 64 (`2^6`)_
_possible arrangements, conveniently letting us represent combined state in_
_a single `ulong` via `1 << IndividualMask`._

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

## Finishing the Results

A completed breeding path still needs a final pass before it is returned.

Surgery can add, remove, or change passives at a cost. The solver applies it
after the main breeding search. Considering every possible surgery during
every breeding round could uncover more intermediate combinations, but it
would also multiply the number of paths the solver has to search.

After applying surgery options, the solver filters for paths which have the
requested passives, attacks, and gender. The PalCalc.UI app will further
group or reduce those alternatives so the player isn't shown hundreds of
similar trees. `PalResultGrouping` and `PalResultProperty` are used for that
presentation step, but aren't used in the breeding search itself.

The solver ends by resolving the real attack skills needed on parent
and child Pals. The earlier search process just tracks the presence and
general distribution of attack skills. This simple approach gives great
performance improvements.

We can save this detail for the end because attacks are _very_ easy to
manipulate. Results with attack data are resolved by a simple rule:
"Attack inheritance should appear as late in the tree as possible."

## Detailed Solver Walkthrough

The following is a complete walkthrough of the steps, in order.

### 1. Prepare the solve

1. `BreedingSolver` receives a `BreedingSolverRequest` containing the target
   and settings.
2. `SolverRunContext` stores the target, settings, breeding mechanics,
   breeding database, run controller, and candidate-selection policy used for
   this run.
3. `SolverRun` runs the search and finishes the results.

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
6. Build initial attack profiles from owned mastered attacks and wild level-1 attacks.
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
5. Determines the useful passive and IV outcomes for that child.
6. Combines the parent attack profiles and adjusts probabilities.
7. Produces candidates that may add a new or better path.

Workers use a quick assessment from the selection policy to avoid returning
children that are already known to be unhelpful. This is only a pre-filter;
the frontier performs the complete simplification after collecting the batch.

### 5. Save completed results

Any candidate that satisfies the target is saved as a completed result. This
happens separately because a valid result may not be one of the candidates
worth retaining as a future parent.

Completed results are grouped and simplified independently from the frontier.
Paths with the same breeding effort can be reduced using the result-selection
rules without changing which effort levels have been discovered.

### 6. Simplify and update the frontier

The frontier merges the children from the batch with the candidates already
retained. `DefaultCandidateSelectionPolicy` runs the simplification using the
rules configured by `ResultPruningPolicy`. Some special handling is needed
to preserve and compare `AttackProfiles` since they're not part of the
effective properties.

1. Group candidates by their effective properties.
2. Treat lower breeding effort as a guaranteed improvement over matching
   candidates.
3. Treat better cost or IVs as possible improvements that still need the full
   simplification pass.
4. Apply the ordered rules from `ResultPruningPolicy`, including effort, steps,
   IV quality, cost, location, reuse, wild Pals, referenced players, variety,
   and the configured result limit.
5. Keep the selected paths for each effective-property group.
6. In each effective-property group, check for any `AttackProfiles` which were
   entirely removed by pruning. Restore the lowest-cost candidates for each
   missing `AttackProfile`.
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

`ResultPostProcessor` handles the completed paths collected during the search.

1. Apply the allowed surgery operations and their costs.
2. Check the final required and optional passive rules.
3. Check that the result has the requested gender.
4. Check the result for an `AttackProfile` which meets the attack requirements and
   Special Cake limit.
5. Reconstruct the necessary attack inheritance modes and parent loadouts.
6. Recompute exact probability, effort, and cake totals, and re-verify
   that the result still meets the final requirements.
7. Return the remaining paths as a `BreedingSolverResult`.

The UI may use `PalResultGrouping` and `PalResultProperty` to reduce or group
the returned paths for display. This does not change which candidates were
explored during the solve.

## Implementation Map

```text
BreedingSolver
  -> SolverRunContext
       -> AttackTargetContext
  -> SolverRun
       -> InitialPalBuilder
       -> SearchFrontier
            -> FrontierIndex
            -> ParentPairSchedule
            -> ResultAccumulator
            -> DefaultCandidateSelectionPolicy
       -> ParallelBatchExecutor
            -> CandidateExpander per worker
                 -> worker-local object pools
                 -> AttackProfileComposer
                      -> AttackProfile / AttackProfileEntry
                      -> AttackProfileReducer
       -> ResultPostProcessor
            -> AttackResultMaterializer
  -> BreedingSolverResult
```

Further references:

- [Passive inheritance estimation](./README-BREED-ESTIMATE.md)
- [Miscellaneous optimization notes](./README-MISC.md)
- [`PalCalc.Solver.CLI/Program.cs`](../PalCalc.Solver.CLI/Program.cs) contains
  an end-to-end usage example.
