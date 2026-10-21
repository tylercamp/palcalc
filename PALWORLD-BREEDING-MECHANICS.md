# Palworld Breeding Mechanics

This is the reference for game mechanics that PalCalc relies on, and the
approximations it uses. Read the [solver overview](PalCalc.Solver/README.md) for
how those mechanics are used to search for breeding paths.

Choose a topic:

- [Child species](#pal-species)
- [Passive inheritance](#passive-skills), including [Special Cakes](#special-cakes)
- [Attack inheritance](#activeattack-skills)
- [IV inheritance](#ivs)
- [Gender](#gender) and [wild Pals](#wild-pals)
- [Which builds and mechanics have been checked](#assessment-coverage)
- [PalCalc's limitations and search tradeoffs](LIMITATIONS.md)

Each topic covers the high-level details of a breeding mechanic, with links to more
detailed pseudocode and other reverse-engineered assessments when available.

We investigate the game's behavior for specific builds using AI and
[Reverse Engineer Anything (REA)](https://github.com/morluto/rea). The
[research introduction](docres/research/palworld-breeding/README.md) explains the
process, how to run it yourself, and where published evidence is kept. GenDB
extracts the game data used by these rules; see the
[data-generation guide](PalCalc.GenDB/README.md).

By **reverse engineering** the Palworld game code we can understand _exactly_ how
things work in-game. This is as accurate as it gets, second only to direct explanations
by Pocket Pair, Inc. themselves.

## Assessment coverage

These findings were checked against **Steam build 25246127**. Some parts of
breeding, including child gender and wild-Pal generation, still need separate
investigation. The table below shows what has been checked and where PalCalc uses
an approximation. See [build and research details](#build-and-research-details)
for the executable identity, review dates, and version-label limitations.
Coverage of a named build doesn't establish compatibility with a later update,
and checking executable logic doesn't by itself verify the current asset values.

| Question | Published coverage | What was checked |
| --- | --- | --- |
| Child species | [2026-10-18 assessment](docres/research/palworld-breeding/assessments/2026-10-18-build-25246127/assessment.md) | Ordinary recipe and breeding-power selection; current recipe and species lists weren't fully checked |
| Child gender | Not fully investigated | Recipe gender requirements don't explain how the child's own gender is chosen |
| Child passives | [2026-10-16 assessment](docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/assessment.md) | Selection from parents, random additions, and item overrides; random filling can fall short |
| Child attacks | [2026-10-16 assessment](docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/assessment.md) | Selection from equipped parental attacks and the all-attack item effect; later initialization wasn't fully traced |
| Child IVs | [2026-10-16 assessment](docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/assessment.md) | Which stats come from parents or fresh random values; mutation and talent bonuses aren't covered by the explanation below |
| Wild-Pal passives | Not fully investigated | Initial spawning and capture-time changes need separate checks |
| Wild-Pal attacks | Not fully investigated | Initial spawning, level-based learning, and capture-time changes need separate checks |
| Wild-Pal gender | Not fully investigated | The spawn-time selection process remains to be documented |

Other relevant topics include [egg-production time](#egg-production-time) and
[world-map coordinates](docres/research/palworld-misc/README.md#world-map-coordinates).
The current timing and map calculations haven't been independently verified
against the game's code. See [PalCalc's limitations](LIMITATIONS.md) for the
approximations used in estimates.

## Pal Species

Each Pal species has a breeding power. The game first checks for a special recipe
matching the parents, including their genders where required. It uses the first
matching recipe in table order.

If no recipe applies, it uses the parents' breeding powers to choose a child. For
normal breeding powers, this works out to averaging the parents' powers and rounding
up, then adding any item rank adjustment. It selects an eligible child with the
nearest power, breaking distance ties by priority and exact ties by table order.

The [species evidence](docres/research/palworld-breeding/assessments/2026-10-18-build-25246127/evidence.md#reconstructed-ordinary-selector)
explains the exact arithmetic and where that simplification stops being valid.
Mutation and later replacement pools were outside this assessment's scope.

Pals that appear as special-recipe children are excluded from the ordinary
breeding-power selection. Bosses and other species flagged as ineligible are
excluded too.

_Note: Some special recipes have gender requirements. In particular, the child of
Katress + Wixen may change depending on parent genders._

For how PalCalc builds its list of breeding pairs, see the
[solver's species database explanation](PalCalc.Solver/README.md#species-database).

## Passive Skills

The original passive inheritance model is credited to
[/u/mgxts](https://www.reddit.com/r/Palworld/comments/1af9in7/passive_skill_inheritance_mechanics_in_breeding/).

The completed REA assessment independently checked ordinary inheritance and item
effects, including an apparent bug in random passive selection. See the
[passive reference and evidence](docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/evidence.md#passive-selection-and-override).

The first roll asks for one to four passives from the parents. If the parents
have fewer unique passives than requested, the child inherits the available ones.
The second roll asks for zero to three random additions, limited by the remaining
slots on the child. Using the game-data weights recorded in PalCalc gives the
following chances for those count rolls. These are calculated probabilities,
not measured breeding frequencies:

| Requested parental count | Chance | Random-add attempts | Chance |
| --- | ---: | --- | ---: |
| 1 passive | 40% | 0 passives | 40% |
| 2 passives | 30% | 1 passive | 30% |
| 3 passives | 20% | 2 passives | 20% |
| 4 passives | 10% | 3 passives | 10% |

The ordinary process is:

> 1. Combine the passive lists from both parents and remove duplicates
> 2. Roll the number of passives to inherit
> 3. Choose that many passives from the parents' combined list at random
> 4. Roll the number of random passives to add
> 5. Attempt those random additions, up to the four-passive limit

Passive inheritance happens in two stages: the child first inherits passives
from its parents (steps 1-3), then the game may add random passives (steps 4-5).

The game can sometimes finish with fewer passives than requested because of a
selection bug or because no eligible passives remain. We don't yet know how often
that happens with actual game data. The [limitations page](LIMITATIONS.md#random-passive-filling)
explains how this affects PalCalc's estimates.

### Special Cakes

Special Cakes skip both ordinary count rolls and target four passives:

- If the parents have at least four unique passives between them, the child
  inherits four of those passives.
- If the parents have fewer than four, the child inherits all of them and the
  game tries to fill the remaining slots with random passives.

For example, if the parents only have Lucky and Runner between them, the child
inherits both and the game attempts to add two random passives.

Note: The game's selection bug or a lack of eligible passives can leave the real child short.

For the calculations and how cakes are used in solver search, see the
[solver overview](PalCalc.Solver/README.md#finishing-the-results) and
[Special Cake probability example](PalCalc.Solver/README-BREED-ESTIMATE.md#special-cake-override).

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

Only the parents' equipped attacks are considered for inheritance. Some attacks
are marked as non-inheritable and are left out of the selection entirely.

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
> 2. Remove non-inheritable attacks
> 3. Select one remaining attack at random, if any are available
> 4. Add all attacks the child species learns at level one
> 5. Remove duplicates from the final list

With a Special Cake, the child inherits all eligible equipped attacks from both
parents, rather than just one. Each parent can contribute at most three equipped
attacks. The child's three equipment slots don't mean additional inherited attacks
are lost; learned attacks and currently equipped attacks are different things.

### Important Details

**a.** If the two parents each have one attack equipped, and only one of them is a desired attack, there's a 50/50 chance of inheriting that attack. (Two attacks to pick from.) But if the other parent equips a non-inheritable attack, the desired attack chance is 100%. (One attack to pick from.)

The recovered [attack model](docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/evidence.md#attack-inheritance)
records the detailed selection and saved attack lists. Later initialization wasn't
fully traced; the report explains that boundary and the item-data assumptions.

## IVs

IVs are the underlying HP, Attack, and Defense values, called talents in the game
data. Early findings came from samples in
[issue #22](https://github.com/tylercamp/palcalc/issues/22); the later REA assessment
recovered the selection process below.

### IV source selection

The game doesn't simply roll how many IVs to inherit and then choose that many
stats. It chooses between three groups of inheritance patterns. In the middle
group, HP always comes from a parent, and one pattern inherits all three stats.
That means asking for HP can give different odds from asking for Attack or Defense.

The table below shows those groups. They are choices between patterns, rather
than simply choices of how many stats to inherit.

| Selection group | Possible stats taken from parents |
| --- | --- |
| First | HP **or** Attack **or** Defense |
| Second | HP+Attack+Defense **or** HP+Defense **or** HP+Attack |
| Third | HP+Attack+Defense |

Each inherited stat can come from either parent. Stats not taken from parents get
fresh random values. A random value can happen to match a parent's IV, but that
doesn't mean it was inherited.

For the exact selection process, see the [recovered IV pseudocode and evidence](docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/evidence.md#iv-source-selection).
The [solver's IV calculations](PalCalc.Solver/README.md#iv-probabilities) explain
how those rules become probability and effort estimates.


## Gender

Each species has gender probabilities in the game data. A recipe's required
parent genders are separate from the child's own gender: a gender-specific recipe
doesn't by itself explain how the child's gender is drawn.

A complete investigation of child and wild-spawn gender selection hasn't yet
been published in these assessments. For how PalCalc uses the species
probabilities in estimates, see [gender handling](PalCalc.Solver/README.md#gender).

## Wild Pals

A wild Pal's skills can depend on what it receives when spawned, what it learns
with levels, and any changes made during capture. A skill recorded in a save
doesn't tell us which of those steps supplied it.

These processes haven't received a complete assessment here. The breeding
findings above don't establish the rules for wild Pals. See the
[coverage table](#assessment-coverage) for what has been investigated, and the
[solver overview](PalCalc.Solver/README.md#2-build-the-starting-candidates) for
how PalCalc represents available wild Pals.

## Egg-production time

Producing an egg at the breeding farm is separate from hatching it in an
incubator. Both can contribute to the time needed for a breeding path.

The exact farm timing formula, including activity, sleep, and breeding-speed
modifiers, hasn't received a completed game-code assessment here. PalCalc uses a
configurable estimate; its [timing calculation](PalCalc.Solver/README.md#egg-production-time)
and assumptions are documented in the solver README.

## Build and research details

<details>
<summary>Executable identity and research dates</summary>

The published game-code findings concern Steam build 25246127, Windows x86-64,
executable SHA-256 `e590b5e7bfaa3fea40fab1a02cc72c8fc5fd6f8631ef2308e95ac56c25195837`.
The release label recorded in the previous reference was **v1.0.5.102999**;
the retained assessments did not independently establish that label.

Inheritance evidence was recovered on 2026-10-16 and its implementation comparison
reviewed on 2026-10-18. Species selection was assessed on 2026-10-18 for the same
executable. These dates do not establish coverage of later game builds.

</details>

## Further reading

- [Solver implementation and calculations](PalCalc.Solver/README.md)
- [Worked passive probability examples](PalCalc.Solver/README-BREED-ESTIMATE.md)
- [PalCalc limitations](LIMITATIONS.md)
- [Research workflow and published evidence](docres/research/palworld-breeding/README.md)
