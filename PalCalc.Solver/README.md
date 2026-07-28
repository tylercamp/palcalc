# Solver Overview

Pal Calc finds practical breeding paths from the Pals a player already has, or
is willing to capture, to a target Pal with the requested passives, IVs, and
gender.

For example, you might ask for an Anubis with Legend, Earth Emperor, and at
least 90 attack IV. Pal Calc will work through all the possibilities and
return complete breeding trees: which owned or wild Pals to start with, which
parents to breed at each step, and roughly how much effort each path should
take.

The shortest tree is not always the fastest one. A two-step path with poor
inheritance odds may take longer than a three-step path whose children are much
more likely to have the right traits. The solver compares paths by their
estimated effort and keeps a small selection of useful alternatives.

## Concepts at a Glance

- **Target** - The Pal being requested, including its required and optional
  passives, IV thresholds, gender, and any limits placed on the search.
- **Candidate** - A Pal the solver might use in a breeding tree. It can be one
  you own, one you can catch, or a child from an earlier step. Sometimes one
  candidate stands for several actual Pals.
- **Frontier** - The useful candidates found so far. Any Pal in the frontier
  may become a parent in a later breeding step.
- **Effective properties** - The parts of a Pal that matter for the current
  target: species, gender representation, useful passives, and whether each
  requested IV threshold can be met. If two Pals have the same effective
  properties, either one will work in the same future breeding steps.
- **Breeding effort** - An estimate of the work needed to obtain a Pal.
  `BreedingEffort` covers its complete breeding tree, while
  `SelfBreedingEffort` covers only the work introduced by that candidate.
- **Simplification** - Dropping paths that are slower or do not add anything
  new, while keeping the best paths and a few useful alternatives. This is also
  called *pruning*.
- **Completed result** - A Pal that matches the target. It is saved even when
  the solver does not need it as a parent.

After each round, the solver records what changed in the frontier. This change
is called a **frontier delta**. It lets the solver try the new parent pairs
without repeating work it has already done.

## Building a Breeding Tree

Imagine trying to find a breeding tree by hand. You would begin with the Pals
available to you, choose two that could make a useful child, and add that child
to the list of possible parents. You would keep doing this until one of the
children matched your target.

The solver follows the same general process, but it can consider many possible
trees at once.

It first builds a frontier from the owned and wild Pals allowed by the solver
settings. It skips Pals that cannot help with the target. When several owned
Pals would work the same way, it picks one to represent them. It can also
combine matching male and female Pals so either gender is available later.

The solver then breeds the Pals in the frontier. Useful children are added back
to the frontier, so they can become parents in the next round. Children that
already satisfy the target are also saved as completed results.

This repeats until a round produces no useful new Pals, or until the configured
iteration limit is reached.

### What makes a child useful?

A child is useful when it adds a new or better way to reach the target. It may
introduce the right Pal for a later breeding combination, collect useful
passives in one place, carry an IV that can meet a requested threshold, or
provide the gender needed for another pair.

The meaning of "useful" depends on the target. If the request only requires an
attack IV, exact health and defense IVs are ignored when comparing two
candidates. Likewise, a passive that is valuable in general may still be
irrelevant for the final target pal.

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

It does not matter how the desired passives are divided between the parents.
Palworld combines and deduplicates both parents' passives before rolling
inheritance, so a 2/2 split is no better than a 1/3 or 0/4 split.

#### IVs

An IV is relevant when it can satisfy a threshold in the current request.
"Relevant" describes its usefulness for this target, not whether the stat is
generally good.

Some candidates stand for more than one individual Pal, so the solver records
their IVs as ranges. During the search, the important question is usually
whether that range can meet the requested threshold. Exact values can still
help choose between otherwise similar paths.

#### Gender

The solver does not always need to choose a candidate's gender right away. A
wildcard gender means it can choose the required gender later and include the
chance of obtaining that gender in the effort estimate. An opposite-wildcard
simply takes whichever gender is opposite the other parent.

If the player owns equivalent male and female Pals, the solver can combine them
into a composite owned candidate. A later step can use whichever owned Pal has
the required gender instead of breeding another copy just for its gender.

## Estimating the Effort of a Path

Once the solver finds a possible child, it estimates how much work it should
take to produce that child with the required properties.

Suppose a child has an 8% chance of inheriting the passives and IVs needed for
the next step. That works out to about one success per 12 or 13 attempts. The
solver multiplies those average breeding attempts by the configured time per
breed to estimate the time spent making that child.

Depending on the solver settings, effort can include:

- the effort already spent obtaining both parents
- passive and IV inheritance probabilities
- the chance of obtaining the required gender
- breeding and incubation time
- the effort needed to capture a wild Pal
- the number of breeding facilities available

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

See [Passive inheritance estimation](./README-BREED-ESTIMATE.md) for the
probability calculations used by the solver.

## Keeping the Search Manageable

The number of possible breeding trees explodes as the search continues. Every
useful child becomes another possible parent, and every new parent can be
paired with the Pals already found. Keeping every possible way of reaching
every child would massively expand the pool of paths the solver has to search.

Pal Calc simplifies the frontier after each round. If two candidates have the
same effective properties, they are equally useful in future breeding. The
solver compares the paths that produced them and can drop one that clearly
takes longer.

Lower breeding effort is the clearest improvement. If the solver finds the
same effective Pal through a faster path, the slower path can be ignored
entirely.

Effort is not the only useful difference, however. Two paths may take the same
estimated effort while differing in the number of steps, IV quality, cost, Pal
locations, and other practical details. The solver uses those differences to
decide which alternatives are worth keeping instead of simply taking whichever
path it found first.

Completed results are handled separately. A Pal can be a good final answer
even when another equivalent Pal would make a better parent, so the solver
saves results before simplifying the frontier.

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
candidate-selection rules. Pal Calc does not preserve every possible ancestry
for every Pal. It preserves the fastest useful candidates it finds, along with
a limited selection of alternatives that differ in practical ways.

## Finishing the Results

A completed breeding path still needs a final pass before it is returned.

Surgery can add, remove, or change passives at a cost. The solver applies it
after the main breeding search. Considering every possible surgery during
every breeding round could uncover more intermediate combinations, but it
would also multiply the number of paths the solver has to search.

After applying surgery options, the solver checks that each path has the
requested passives and gender, then returns the paths that remain. The UI may
group or reduce those alternatives further so the player is not shown hundreds
of similarly efficient trees. `PalResultGrouping` and `PalResultProperty` are
used for that presentation step, but aren't used in the breeding search itself.

## Detailed Solver Walkthrough

The following is a complete walkthrough of the steps, in order.

### 1. Prepare the solve

1. `BreedingSolver` receives a `BreedingSolverRequest` containing the target
   and settings.
2. The request keeps a normalized copy of the target so later changes by the
   caller cannot affect a running solve.
3. `SolverRunContext` stores the target, settings, breeding mechanics,
   breeding database, run controller, and candidate-selection policy used for
   this run.
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
6. Add the resulting candidates to the initial frontier.

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
6. Calculates the probability and effort of producing each relevant outcome.
7. Produces candidates that may add a new or better path.

Workers use a quick assessment from the selection policy to avoid returning
children that are already known to be unhelpful. This is only a pre-filter;
the frontier performs the complete simplification after collecting the batch.

### 5. Save completed results

`ResultAccumulator` checks each produced candidate before the frontier is
simplified.

Any candidate that satisfies the target is saved as a completed result. This
happens separately because a valid result may not be one of the candidates
worth retaining as a future parent.

Completed results are grouped and simplified independently from the frontier.
Paths with the same breeding effort can be reduced using the result-selection
rules without changing which effort levels have been discovered.

### 6. Simplify and update the frontier

The frontier merges the children from the batch with the candidates already
retained. `DefaultCandidateSelectionPolicy` runs the simplification using the
rules configured by `ResultPruningPolicy`.

1. Group candidates by their effective properties.
2. Treat lower breeding effort as a guaranteed improvement over matching
   candidates.
3. Treat better cost or IVs as possible improvements that still need the full
   simplification pass.
4. Apply the ordered rules from `ResultPruningPolicy`, including effort, steps,
   IV quality, cost, location, reuse, wild Pals, referenced players, variety,
   and the configured result limit.
5. Keep the selected paths for each effective-property group.
6. Produce a `FrontierDelta` containing the candidates added to and removed
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
4. Return the remaining paths as a `BreedingSolverResult`.

The UI may use `PalResultGrouping` and `PalResultProperty` to reduce or group
the returned paths for display. This does not change which candidates were
explored during the solve.

## Implementation Map

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

Further references:

- [Passive inheritance estimation](./README-BREED-ESTIMATE.md)
- [Miscellaneous optimization notes](./README-MISC.md)
- [`PalCalc.Solver.CLI/Program.cs`](../PalCalc.Solver.CLI/Program.cs) contains
  an end-to-end usage example.
