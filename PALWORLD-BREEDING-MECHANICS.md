# Palworld Breeding Mechanics

This is the maintained reference for the game mechanics PalCalc relies on and the
approximations it uses. Read the [solver overview](PalCalc.Solver/README.md) for
how those mechanics are used to search for breeding paths.

Native behavior is recovered for specific game builds using AI and
[Reverse Engineer Anything (REA)](https://github.com/morluto/rea). The
[research introduction](docres/research/palworld-breeding/README.md) explains the
process, how to run it yourself, and where published evidence is kept. GenDB
extracts the data consumed by these algorithms; see the
[data-generation guide](PalCalc.GenDB/README.md).

## Assessment coverage

The published native findings concern **Steam build 25246127**, Windows x86-64,
executable SHA-256 `e590b5e7bfaa3fea40fab1a02cc72c8fc5fd6f8631ef2308e95ac56c25195837`.
The release label recorded in the previous reference was **v1.0.5.102999**;
the retained native assessments did not independently establish that label.
Inheritance evidence was recovered on 2026-10-16 and its implementation comparison
reviewed on 2026-10-18. Species selection was assessed on 2026-10-18 for the same
executable. These dates do not establish coverage of later game builds.

| Question | Published coverage | PalCalc treatment and limits |
| --- | --- | --- |
| Child species | [2026-10-18 assessment](docres/research/palworld-breeding/assessments/2026-10-18-build-25246127/assessment.md) | Core rank rules agree; roster, recipe overlap, self-pairs, and exact ties need data-equivalence checks |
| Child gender | No complete native assessment published | Uses species gender probabilities from game data; recipe gender constraints are a separate question |
| Child passives | [2026-10-16 assessment](docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/assessment.md) | Ordinary selection and override recovered; filler failures and exhaustion omitted |
| Child attacks | [2026-10-16 assessment](docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/assessment.md) | Final inherited contents modeled; later initialization remains an evidence boundary |
| Child IVs | [2026-10-16 assessment](docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/assessment.md) | Asymmetric source sets modeled; accidental random matches, mutation, and talent bonuses omitted |
| Wild-Pal passives | No complete spawn/capture assessment published | Guaranteed-passive data and random-count assumptions are distinct from a verified generation algorithm |
| Wild-Pal attacks | No complete spawn/capture assessment published | Starting candidates use the current solver model; spawn, learning, and capture effects need separate proof |
| Wild-Pal gender | No complete native assessment published | Uses species gender data; the spawn-time selection process remains to be documented |

Supporting calculations are reviewed when relevant, rather than in every
inheritance assessment: see [egg-production time](#egg-production-time) and
[world-map coordinates](docres/research/palworld-misc/README.md#world-map-coordinates).
Their current source-derived descriptions are not new native assessments.

## Pal Species

### Recovered game behavior

The [species evidence](docres/research/palworld-breeding/assessments/2026-10-18-build-25246127/evidence.md#reconstructed-ordinary-selector)
reconstructs ordinary selection as:

```text
try matching special recipes in table order, including parent genders
if none applies, use the parent's rounded-up mean rank plus the clamped item adjustment
exclude boss/IgnoreCombi rows and characters appearing as special-recipe children
choose the nearest rank, then highest priority; retain table order on an exact tie
use the documented fallback if no candidate remains
```

The simplified rounding formula has an input-domain boundary; see the evidence
for exact arithmetic, recipe fallback, item inputs, and final child-ID replacements.
Mutation/replacement pools were outside the species assessment's scope.

### PalCalc's generated breeding graph

Each Pal species has a breeding power, named `CombiRank` in the game data. Typically
Palworld will take the average of this value from the parents, round up, and select
the Pal with the nearest power.

Some Pals can only be produced by a specific combination of parents. These
special recipes take priority over the normal breeding-power calculation. If there's
any special recipes for producing a child Pal, then it's guaranteed that these are the
_only_ way to breed the child.

_Note: Some special recipes may have gender requirements. In particular, the child of Katress + Wixen may change depending on parent genders._

PalCalc uses this process when generating its breeding database:

> - Visit each possible pairing of Pal species
> - If both parents are the same species, the child is that species
> - If the parents match a special recipe, use that recipe's child
> - Otherwise, we use the standard breeding calculation:
>   1. Average the parents' breeding powers and round up
>   2. Select Pals with the nearest breeding power
>   3. If multiple Pals are equally close, the game's `CombiDuplicatePriority` is used
>    as a tie-breaker (highest value takes priority)

(When selecting Pals with the "nearest breeding power" in the standard calculation, we ignore any Pals with special recipes as children.)

Breeding powers, priorities, and special recipes are read directly from the game
data. PalCalc uses them to generate `breeding.json` and `breedingdb.json`, which
the solver reads when finding possible paths. This file is embedded in
the PalCalc EXE files.

The [functional comparison](docres/research/palworld-breeding/assessments/2026-10-18-build-25246127/assessment.md#functional-comparison)
records remaining conditions: GenDB's roster does not read `IgnoreCombi`, its
same-species shortcut precedes recipe matching, recipe resolution assumes a unique
match, and exact ties add a non-variant preference. The native item rank adjustment
also has no input in the generated graph. These distinctions do not prove an
incorrect live pairing without checking the relevant current data.

## Passive Skills

The original passive model is credited to
[/u/mgxts](https://www.reddit.com/r/Palworld/comments/1af9in7/passive_skill_inheritance_mechanics_in_breeding/).
The completed REA assessment independently recovered the ordinary and item-override
paths, including an apparent random-filler bug. See the
[passive reference and evidence](docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/evidence.md#passive-selection-and-override).

The existing PalCalc data contains these `BP_PalGameSetting` weights:

- `Combi_PassiveInheritNum`: `[4, 3, 2, 1]`
- `Combi_PassiveRandomAddNum`: `[4, 3, 2, 1]`

The native parental-count selector returns its original index **plus one**; the
random-add selector returns its original index. These are requested counts, capped
by available parental candidates and remaining child slots. For ideal weighted
selection, normalizing the repository's weights gives:

| Requested parental count | Chance | Random-add attempts | Chance |
| --- | ---: | --- | ---: |
| 1 passive | 40% | 0 passives | 40% |
| 2 passives | 30% | 1 passive | 30% |
| 3 passives | 20% | 2 passives | 20% |
| 4 passives | 10% | 3 passives | 10% |

PalCalc's ordinary-mode model is:

> 1. Combine the passive lists from both parents and remove duplicates
> 2. Roll the number of passives to inherit (`Combi_PassiveInheritNum`)
> 3. Choose that many passives from the parents' combined list at random
> 4. Roll the number of random passives to add (`Combi_PassiveRandomAddNum`)
> 5. Fill the requested random slots up to the four-passive limit

Passive inheritance happens in two stages: the child first inherits passives
from its parents (steps 1-3), then the game may add random passives (steps 4-5).

PalCalc assumes successful filling with sufficient eligible candidates and ignores
the chance that random additions supply desired passives. The recovered filler
compares compacted weights but subtracts original weights after removals. This
apparent bug can bias choices or consume attempts without a selection. PalCalc
intentionally omits it and pool exhaustion; their frequency with actual game data
was not established. Requested counts are not unconditional game guarantees.

### Passive override items / Special Cakes

A **positive** `PassiveInheritCountOverride=N` replaces **both** ordinary count rolls:

```text
target = min(N, 4)
pool = distinct(parent A passives + parent B passives)
select up to target parental passives without replacement
attempt random filling for target - selected_count
stop; do not perform the ordinary random-add count roll
```

PalCalc's existing Special Cake model uses override four. With at least four
parental candidates the child selects four directly. With fewer, PalCalc models
successful filling to four; the native bug or pool exhaustion can leave the real
child short. The native run verified the override consumer, not Cake05's current
asset value.

For five parental candidates containing two desired passives, override four
selects both with probability `Choose(3,2)/Choose(5,4) = 3/5`. For a pool containing
only the two desired passives, the modeled result contains both plus two random
passives with probability one. A result allowing fewer than four total passives has
probability zero in this override model, even with empty parents.

Search carries normal and Special Cake probabilities separately. Candidate effort
comparisons use ordinary-mode passive probability and omit attack-inheritance odds.
During finalization, attack materialization selects the concrete mode and recalculates
attempts, effort, and cake use with that mode's passive probability and IV/attack odds.
Special Cakes remain part of existing attack solving, rather than a new standalone
passive-only item search.

### Important Details

**a.** The game combines and deduplicates the parents' passives before making its
selection. This means:

- Distribution of passives between parents has no effect - e.g. `2 + 2`, `3 + 1`, `4 + 0` all behave the same
- Have duplicates of a passive between the parents doesn't make it more likely to inherit that passive
- Additional unwanted passives reduce the chance of selecting the desired passives

**b.** Wild Pals can have certain guaranteed passives (e.g. a wild `Gorirat` will always have `Conceited`.) These are not
automatically added to bred children; they only affect inheritance when they are actually present on a parent.

**c.** The "inherited from parents" count is an _upper limit_ on what to inherit. 
If the game rolls three but the parents only have two, the child inherits those two. 

**d.** If any parent has at least one passive, the child will always have at least one passive. The only way to get a child with zero passives is if the parents have zero passives.

For a full walkthrough of the probability calculations, see
[Estimating Breeding Time for Inheriting Passives](PalCalc.Solver/README-BREED-ESTIMATE.md).

## Active/Attack Skills

Unlike passive skills, the game data doesn't specify any inheritance weights for attack skills.
It _does_ provide an `IgnoreRandomInherit` flag for each attack, and we know empirically that
only equipped attack skills are candidates for inheritance. (i.e., we've never seen unequipped
attacks get inherited in any of our samples.)

Based on the breeding samples collected in [issue #71](https://github.com/tylercamp/palcalc/issues/71) and [some tips from The Pal Professor](https://github.com/tylercamp/palcalc/issues/71#issuecomment-5151759038), we also know:

- Palworld uses the same merge+deduplication process as passive skills; having the same attack skill on both parents did _not_ cause the attack skill to be more likely
  - In particular, this implies Palworld does _not_ pick a parent first, and then pick an attack skill. Otherwise we'd expect duplicates to matter more
- Inheritance chance is unaffected by whether a Pal naturally learns the attack by leveling up
- Inherited attacks are added before lv1 attacks
  - In all samples, the lv1 attack appeared at the end of the list of attacks
- By default, only one attack skill may be inherited through normal breeding
- An attack skill already learned by the child at lv1 may be selected, which appears as though no attack was selected
- Pal-exclusive attack skills will never be inherited

The following algorithm would replicate this behavior:

> 1. Combine the parents' equipped attacks and remove duplicates
> 2. Remove attacks marked `IgnoreRandomInherit`
> 3. Select one remaining attack at random, if any are available
> 4. Add all attacks the child species learns at level one
> 5. Remove duplicates from the final list

_Special Cakes use the existing `Cake05` all-attack model (`bInheritAllActiveSkills=true`).
The recovered consumer branch confirms the effect of this flag; this assessment did
not revalidate the item's current asset values._

### Important Details

**a.** If the two parents each have one attack equipped, and only one of them is a desired attack, there's a 50/50 chance of inheriting that attack. (Two attacks to pick from.) But if the other parent equipped an attack with the `IgnoreRandomInherit` flag, the desired attack chance is 100%. (One attack to pick from.)

**b.** Special Cakes cause children to inherit all possible equipped attacks from each parent. More specifically, they cause children to inherit three attacks from each parent. There is no way for a child to inherit more than three attacks from any one parent.

The recovered [attack model](docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/evidence.md#attack-inheritance)
supports this final-inherited-content model. Initial equipment order and the separate
mastered/equipped writes are retained in that reference; they do not establish a new
inheritance-probability discrepancy. Missing database rows are also excluded from the
native eligible pool. Later initialization remains a documented evidence boundary.

## IVs

Palworld calls IVs talents. PalCalc uses HP, Attack (Shot), and Defense. Original
support was based on samples in [issue #22](https://github.com/tylercamp/palcalc/issues/22).
The [REA model and instruction checks](docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/evidence.md#iv-source-selection)
establish the source-selection process below.

### IV source selection

The game makes a weighted selection from `Combi_TalentInheritNum`. Its result is
the **zero-based index of the selected array entry**. “Category 1” in the native
reference means index 1, the **second entry**, rather than one inherited stat.
For the repository's `[3,2,1]` weights, that entry has weight 2. PalCalc stores
the same ordered array and uses the game's zero-based selector indices directly.

| Original selector index | PalCalc weight array index | Parental source sets |
| --- | --- | --- |
| 0 | 0 | HP **or** Attack **or** Defense |
| 1 | 1 | HP+Attack+Defense **or** HP+Defense **or** HP+Attack |
| 2 | 2 | HP+Attack+Defense |

This excerpt shows ordinary source selection. Tuples are ordered
`(HP, Attack, Defense)`; each field in `H` chooses either parent's corresponding
value. The native reference specifies exact RNG operations and subsequent item
bonuses separately.

```text
R = (fresh random HP, fresh random Attack, fresh random Defense)
H = (choose either parent's HP,
     choose either parent's Attack,
     choose either parent's Defense)
selectorIndex = weighted_original_index(Combi_TalentInheritNum)

if selectorIndex == 2:
    result = H
else:
    branch = choose_index(3)
    if selectorIndex == 1:
        result = [
            (H.HP, H.Attack, H.Defense),
            (H.HP, R.Attack, H.Defense),
            (H.HP, H.Attack, R.Defense)
        ][branch]
    else:
        result = [
            (H.HP, R.Attack, R.Defense),
            (R.HP, H.Attack, R.Defense),
            (R.HP, R.Attack, H.Defense)
        ][branch]
```

For each logical stat, the game prepares a fresh random value and a value selected
from either parent. The branch determines which source reaches the child. In the
middle category HP always comes from a parent and one branch inherits all three
stats. Unselected stats use fresh values. An isolated Attack+Defense source set is
impossible; an Attack+Defense **target** can still succeed through an all-three branch.

The native selector returns the original index; sentinel and unexpected categories
use the one-stat fallback in this build. PalCalc supports its existing valid
three-category data: weight array indices 0/1/2 label the original selector entries,
not achieved stat counts.

[`BuildDesiredIVProbabilities`](PalCalc.Model/BreedingMechanics.cs) represents
the parent-sourced sets above as bit masks: HP = 1, Attack = 2, Defense = 4. For
each requested subset it sums, over the three selector indices:

```text
normalized_weight[selectorIndex]
    * number_of_source_sets_containing_the_entire_requested_subset
    / number_of_source_sets_for_that_selector_index
```

This averages branches uniformly for the estimate. It precomputes probabilities
for all eight requested subsets, including the empty subset with probability one.

Using the repository's `[3,2,1]` weights and ideal uniform branches, the conditional
**exact parent-sourced count** probabilities are 50% / 22.22% / 27.78% for one/two/three
stats. These are derived examples, not measured frequencies. The previous
50% / 33.33% / 16.67% interpretation incorrectly treated categories as final counts.

For required subsets, before choosing a qualifying parent's value:

| Desired subset | Conditional selection probability |
| --- | ---: |
| HP | 2/3 |
| Attack, or Defense | 5/9 |
| HP+Attack, or HP+Defense | 7/18 |
| Attack+Defense, or all three | 5/18 |

For each selected stat, PalCalc assumes either parent is chosen independently with
equal chance. One qualifying parent contributes another factor of 1/2; either parent
is sufficient when both qualify. No required IVs gives probability one. The subset
probabilities are computed per database, so custom category weights retain the
asymmetric branch table.

A fresh random value can equal a parent's number; a numerical match does not prove
inheritance. PalCalc estimates parent-sourced target IVs and omits accidental random
matches. Native talent-bonus and mutation branches are retained in the game reference
but are outside this solver correction.


## Gender

PalCalc uses the species gender probabilities scraped into the database by
[GenDB](PalCalc.GenDB/BuildDBProgram.cs). The solver accounts for the probability
of obtaining the required gender and can defer the choice through wildcard
representations; see [gender handling](PalCalc.Solver/README.md#gender).

A recipe's required parent genders constrain species selection. They do not
establish how a child's own gender is drawn. A complete native account of child
and wild-spawn gender selection is not yet published in these assessments.

## Wild Pals

Distinguish initial spawning, level-based learning, capture-time effects, and
what the save file already records. These can produce different passive or attack
collections and should not be inferred from offspring inheritance alone.

[InitialPalBuilder](PalCalc.Solver/Processing/InitialPalBuilder.cs) defines the
solver's current starting-candidate model. Guaranteed passive data, random-passive
count assumptions, and wild attack availability need their own evidence when
reviewing that model. The coverage table above identifies the unpublished areas;
none is implicitly verified by the child-inheritance assessments.

## Egg-production time

This supporting calculation affects effort estimates but is not part of every
inheritance review. The following describes current PalCalc code, not a newly
recovered native timing formula.

```text
time_per_attempt = BreedingTime * 2 * parent_A_time_factor * parent_B_time_factor
```

[GameSettings](PalCalc.Model/GameSettings.cs) defaults `BreedingTime` to five
minutes and doubles it to approximate parents being available only half the time.
That availability allowance is a solver assumption, not a game-code constant.
[ToTimeFactor](PalCalc.Solver/Utils/SolverExtensions.cs) currently uses a factor of
0.5 for a parent with the tracked breeding-speed passive effect, otherwise 1.
With default settings this gives ten minutes per attempt without those effects.

Producing the egg is separate from incubating it. The
[effort calculation](PalCalc.Solver/PalReference/BredPalReferenceEffort.cs) multiplies
production time by expected attempts, then combines it with incubation according
to the configured incubator model. None of this establishes the native treatment
of work, idling, sleep, or multiple speed modifiers.

A targeted timing assessment should trace the farm progress threshold, update rate,
parent and world modifiers, pauses, and the final egg-production event. Record units,
covered modes, build identity, and evidence before replacing these assumptions.
See the [additional-logic review policy](docres/research/palworld-misc/README.md).

## Implementation References

- Child species: [`PalBreedingCalculator.cs`](PalCalc.GenDB/PalBreedingCalculator.cs)
  and [`UniqueBreedComboReader.cs`](PalCalc.GenDB/GameDataReaders/UniqueBreedComboReader.cs)
- Game settings: [`GameSettingReader.cs`](PalCalc.GenDB/GameDataReaders/GameSettingReader.cs)
  and [`BreedingMechanics.cs`](PalCalc.Model/BreedingMechanics.cs)
- Passive estimates: [`Probabilities/Passives.cs`](PalCalc.Solver/Probabilities/Passives.cs)
- IV estimates: [`Probabilities/IVs.cs`](PalCalc.Solver/Probabilities/IVs.cs)
- Attack estimates: [`Probabilities/Attacks.cs`](PalCalc.Solver/Probabilities/Attacks.cs)
- Attack inheritance flags: [`ActiveSkillReader.cs`](PalCalc.GenDB/GameDataReaders/ActiveSkillReader.cs)
