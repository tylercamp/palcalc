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
- [Which versions and mechanics have been checked](#assessment-coverage)
- [PalCalc's limitations and search tradeoffs](LIMITATIONS.md)

Each topic covers the high-level details of a breeding mechanic, with links to more
detailed pseudocode and other reverse-engineered assessments when available.

We investigate the game's behavior for specific builds using AI and
[Reverse Engineer Anything (REA)](https://github.com/morluto/rea). The
[research introduction](docres/research/palworld-breeding/README.md) explains the
process, how to run it yourself, and where published evidence is kept. GenDB
extracts the game data used by these rules; see the
[data-generation guide](PalCalc.GenDB/README.md).

Reverse engineering connects these explanations to the shipped game code and its
inputs. Each finding states the version and paths checked; incomplete paths and
runtime configuration remain explicit in the research reports.

## Assessment coverage

The [research checklist](docres/research/palworld-breeding/README.md#current-verification-status)
records the last checked game version and date for each mechanic, with links to
its conclusions and evidence. The [research changelog](docres/research/palworld-breeding/CHANGELOG.md)
records which rules changed or were confirmed unchanged.

The [1.0.5.102999 baseline assessment](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md)
is **Finished: all eight required mechanics are Verified**. Both the
rare-passive pool binding and spawn-form/settings-to-saved-input bridge are
established for the recorded shipped-base inputs, modes and output boundaries.
It includes ordinary, item/mutation, primary/additional egg, collection/hatch and specified spawn/capture
paths. This is a static baseline, not measured gameplay frequencies or a solver
implementation update. Runtime source/target state and modified world inputs
retain the report's explicit conditions.

The ordinary rules below remain separate from the
[mutation and talent-bonus branches](#mutation-and-talent-bonus-items).
[Egg-production time](#egg-production-time) and
[world-map coordinates](docres/research/palworld-misc/README.md#world-map-coordinates)
are supporting topics, reviewed when needed rather than on every game update.
See [PalCalc's limitations](LIMITATIONS.md) for the approximations used in estimates.

## Pal Species

Each Pal species has a breeding power. The game first checks for a special recipe
matching the parents, including their genders where required. It uses the first
matching recipe in table order.

If no recipe applies, it uses the parents' breeding powers to choose a child. For
normal breeding powers, this works out to averaging the parents' powers and rounding
up, then adding any item rank adjustment. It selects an eligible child with the
nearest power, breaking distance ties by priority and exact ties by table order.

The [version evidence](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#child-species)
records the exact arithmetic and recovered replacements. The shipped first-load
table order is established: PinkCat, PlantSlime and SheepBall win their respective
equal-priority ties. In particular, specified PlantSlime_Flower parents with zero
rank bonus in ordinary nonmutation generation select PlantSlime, or BOSS_PlantSlime
if boss conversion succeeds. Identical saved parent IDs do not universally bypass
selection. This is static model evidence, not a live breeding test or solver fix.
[Conditions and evidence](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#concrete-species-ties-recipes-and-self-pairs).

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

The [1.0.5.102999 passive assessment](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-passives)
establishes ordinary inheritance, item overrides and mutation, including an apparent
bug in random passive selection. The [research checklist](docres/research/palworld-breeding/README.md#current-verification-status)
records the inputs and modes covered for each version.

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

The [pool-construction check](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#passive-ordering-established-construction-and-remaining-input)
establishes initial alignment and reconstructs the shipped first-load candidate
order. With CraftSpeed_up3 already selected, 95 of the 7,355 integer tickets in
the next fill attempt add nothing. Cake05 can therefore miss its four-passive
target when it needs random filling; these ticket counts are not live frequencies.

### Special Cakes

Here, “Special Cake” means Cake05, whose recovered item effect skips both ordinary
count rolls and targets four passives. Other cake items have different effects;
see the [version's item inputs](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#shared-inputs-and-arithmetic).

For Cake05 in ordinary breeding:

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

**b.** The general wild-Pal initializer adds species passives, such as Gorirat's
Conceited, before random additions. These aren't automatically added to bred
children; they affect inheritance when actually present on a parent. Special spawn
and capture paths have their own inputs; see [wild Pals](#wild-pals).

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

Learned availability combines stored mastered attacks with natural moves allowed
by the current level. A natural move therefore need not appear in the raw mastered
list, and equipment trimming alone does not remove its availability. The current
inventory-farm and recorded alternate hatch routes preserve their scoped saved
contents, as detailed in the [version report](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-attacks).

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

Current verification of the exact selection process is tracked in the
[research checklist](docres/research/palworld-breeding/README.md#current-verification-status).
The [solver's IV calculations](PalCalc.Solver/README.md#iv-probabilities) explain
how those rules become probability and effort estimates.

### Mutation and talent-bonus items

The [version report](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-ivs)
distinguishes these generation paths from ordinary inheritance. Cake02 and Cake04
add a separate 1–5 talent bonus to each ordinary offspring stat, clamped at 100.
Mutation instead generates fresh talents in the shipped default range 90–100;
it doesn't run the ordinary parental-talent or item-bonus branch.

Mutation uses a separate rank formula for species selection. Its nearest-rank pool
can include special-recipe children, which ordinary rank selection excludes;
that pool still excludes bosses and rows flagged `IgnoreCombi`.
[Species model](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#child-species).
It also uses different passive selection and attempts an extra Strong attack of
the child's primary element. That attack can already be present,
so the draw doesn't guarantee another distinct attack. Mutation's passive inserts
can displace parental entries, including with a Special Cake. The report's
[full game model](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md)
keeps these branches and their evidence separate. These findings don't mean the
solver estimates include those modes.


## World-egg pickup effects

The 1.0.5.102999 findings identify two effects on the legacy world-egg model's pickup path.
`EggAlphaConversion` can replace the egg item while copying its existing child
save. `EggObtainExtraEgg` prepares an additional egg: its ordinary transform
rerolls some IVs, while its mutation transform generates fresh IVs and can replace
the first eligible `AddMutationPal` and `AddPal`/`Rank > 3` entries, with mutation
membership taking priority during the scan. Each draw excludes the other current
entries; an empty draw preserves the slot, and a draw need not change its ID.
These transforms concern the additional egg, not an unconditional reroll of the
original child. The prepared save is now traced into the granted egg. Gender and attack arrays
remain copied. With the shipped global `bIsStackablePartnerSkillBySameTribe=false`,
contributing non-stackable partner values use the greatest value per `TribeId`,
then combine groups. These rows declare `InvokeInOtomo`, which native conversion
maps to the separate `InOtomo` trigger. Their shipped item constraints are empty.
Item-presence restriction checks and ordinary holder removal are traced. The
declared unchanged NaughtyCat setup reaches the pickup consumer under its stated
conditions; arbitrary changing-party source sets remain supplied inputs. No
unconditional pickup frequency is established.
The [shipped static item audit](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#shipped-item-sources-do-not-add-extra-egg-records)
establishes that the additional item-derived list contributes no extra-egg effect
under the assessed shipped inputs; partner contributions remain a separate question.
See the [scoped evidence](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#pickup-effect-conditions-tribe-grouping-and-activation-masks)
before interpreting asset values as live probabilities.

Collection route matters. The current breeding farm's direct hatch and container
pickup preserve the generated child save. Dropping an existing egg through the
shipped `CommonDropItem3D` route selects a generic dropped-item model: re-picking
it preserves its saved IVs without the legacy egg model's extra-egg roll. The
[route and save-transfer proof](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#world-placement-proof-boundary)
closes Child IV verification for the declared inputs and modes; it does not
establish a universal pickup rule or an unconditional rate for arbitrary parties.

## Gender

Each species has gender probabilities in the game data. A recipe's required
parent genders are separate from the child's own gender: a gender-specific recipe
doesn't by itself explain how the child's gender is drawn.

The [verified child-gender finding](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-gender)
recovers ordinary and mutation generation assignments from the selected child's
`MaleProbability`. Zero and 100 give fixed genders; negative values give None.
Other values are percent thresholds for the random draw. Current inventory-hatch,
legacy world-egg and additional pickup-egg paths preserve that saved value.
Wild-Pal supplied-spawn and captured-storage contracts are established for the
recorded modes. For how PalCalc uses the species
probabilities in estimates, see [gender handling](PalCalc.Solver/README.md#gender).

## Wild Pals

For overall attack availability, use stored mastered attacks plus the current
species' natural moves through the Pal's level. Equipped slots alone do not
define this set. The [wild-attack assessment](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#wild-pal-attacks)
keeps NPC overrides and KingWhale's special capture handling separate. The
three non-natural attacks in KingWhale's battle lists are combat actions; their
shipped initialization/start implementations do not grant them as mastered
attacks. Capture preserves an existing mastered entry but does not supply these
IDs through that action route. Seeing an attack used in combat therefore does
not establish its availability after capture.

A wild Pal's skills can depend on what it receives when spawned, what it learns
with levels, and any changes made during capture. A skill recorded in a save
doesn't tell us which of those steps supplied it.

The [verified wild-Pal findings](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#wild-pal-passives)
recover species-passive ordering, ratio-based initialization and level-based
attack setup. Capture can add passives through synchronization and RainbowPassiveRate,
and rebuilds overfilled equipment from its first three entries. Its ordinary
storage writer preserves the resulting traits. KingWhale has a separate species,
talent and equipment rewrite. The species used at gender/default-passive assignment and later learning/common
initialization must be distinguished when a Unique NPC key selects a different
CharacterID from DT_UniqueNPC. A valid resolved ID reaches the saved species,
while earlier gender/defaults retain their assignment species. An absent key
preserves species. The report records concrete ordinary caller inputs and
keeps effective spawn modes explicit.
The standard World Tree aura spawn branch draws one trait from seven World Tree
passives and appends it after species defaults if a passive slot remains. This
rolled trait is separate from the spawner's temporary WorldTreeAura effect.
[Selection rules and limits](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#world-tree-standard-spawner-passive-input).

The [ordinary rank-1 GuardianDog activation path](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#ordinary-rank-1-guardiandog-active-grant-to-capture-collector)
is connected to the trainer's synchronization collector. Its value 15 is a roll
input per eligible source passive under the stated effect-list and capture-target
conditions, not a guaranteed addition rate for all captures.
The [capture target gate](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#capture-target-fields-player-owner-history-and-group)
checks whether a trainer reference is live, IsPlayer, old-owner history count and group
membership through Dynamic.GroupId. The standard spawner now has a bound None-key input path, native save
installation and actor association. Saved gender is preserved: shared initialization
uses the species after initial form selection, while a supplied/overridden saved
gender reaches captured storage unchanged. See the [wild-gender conclusion](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#wild-pal-gender).
Capture additions still depend on the supplied target/source state. The
[fresh-target argument](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#fresh-pinkcat-capture-target-argument)
now connects construction through actual initialization/capture recipients for
the declared ordinary setup; constructor defaults alone were insufficient.
The capture's own owner-history update occurs after augmentation and cannot
block additions earlier in that same invocation. The unchanged eligible
GuardianDog source also has a completed-reset regrant argument. These results
do not make every captured target eligible or establish a universal addition rate.
See the [research checklist](docres/research/palworld-breeding/README.md#current-verification-status)
for verification status, and the
[solver overview](PalCalc.Solver/README.md#2-build-the-starting-candidates) for
how PalCalc represents available wild Pals.

## Egg-production time

Producing an egg at the breeding farm is separate from hatching it in an
incubator. Both can contribute to the time needed for a breeding path.

The exact farm timing formula, including activity, sleep, and breeding-speed
modifiers, hasn't received a completed game-code assessment here. PalCalc uses a
configurable estimate; its [timing calculation](PalCalc.Solver/README.md#egg-production-time)
and assumptions are documented in the solver README.

## Further reading

- [Solver implementation and calculations](PalCalc.Solver/README.md)
- [Worked passive probability examples](PalCalc.Solver/README-BREED-ESTIMATE.md)
- [PalCalc limitations](LIMITATIONS.md)
- [Research workflow and published evidence](docres/research/palworld-breeding/README.md)
