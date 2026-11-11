# Palworld Breeding Mechanics

This page explains the breeding rules that matter when planning a Pal, along with
PalCalc's approximations. Read the [solver overview](PalCalc.Solver/README.md) to see
how those mechanics are used to search for breeding paths.

For farm-bred eggs, the child's species, gender, IVs, passives and inherited
attacks are determined when the egg is produced. See the
[generation-to-hatch evidence](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#shared-child-save-boundary)
and the separate [world-egg pickup effects](#world-egg-pickup-effects) for the
collection-time exceptions.

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

## Assessment coverage

The [research checklist](docres/research/palworld-breeding/README.md#current-verification-status)
records the last checked game version and date for each mechanic, with links to
its conclusions and evidence. The [research changelog](docres/research/palworld-breeding/CHANGELOG.md)
records changes to checked versions, game mechanics and assessment scope.

## Pal Species

Each Pal species has a breeding power (`CombiRank` in `DT_PalMonsterParameter`).
The game first checks for a special recipe (`DT_PalCombiUnique`) matching the
parents, including their genders where required. It uses the first matching
recipe in table order.

If no recipe applies, it uses the parents' breeding powers to choose a child. For
normal breeding powers, this means: take the average of the parents' powers, round
up, then adding any item rank adjustment. It selects an eligible child with the
nearest power, breaking distance ties by breeding priority (`CombiDuplicatePriority`
in `DT_PalMonsterParameter`), and if there is still a tie, Palworld picks the option
which appears first in the game files.

For ordinary breeding, the steps are:

> 1. Check special recipes, including parent-gender requirements. If one matches,
>    use its child species and skip steps 2–4.
> 2. Average the parents' breeding powers, round up, and add any item power adjustment.
> 3. Exclude special-recipe children, bosses and other ineligible species from the candidates.
> 4. Select the nearest breeding power. Break distance ties by higher breeding
>    priority, then use the first entry in table order if priorities also tie.
> 5. Roll for Alpha conversion. If it succeeds and the selected species has an
>    Alpha form, use that form.

Breeding two identical variants doesn't always preserve that variant. For example,
without mutation or an item power adjustment, two flower-crowned Gumoss select
ordinary Gumoss, which can then become an Alpha.
[Selection rules and exceptions](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-species).

_Note: Some special recipes have gender requirements. In particular, the child of
Katress + Wixen may change depending on parent genders._

To see how PalCalc builds its list of breeding pairs, see the
[solver's species database explanation](PalCalc.Solver/README.md#species-database).

## Passive Skills

PalCalc's original passive-inheritance model was based on
[/u/mgxts's reverse engineering](https://www.reddit.com/r/Palworld/comments/1af9in7/passive_skill_inheritance_mechanics_in_breeding/).
The explanation below is supported by our
[game-code analysis of Palworld 1.0.5.102999](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-passives).
The original two-stage process still matches ordinary inheritance; the current
findings also establish Special Cake behavior and the random-selection bug
described below.

The first roll asks for one to four passives from the parents. If the parents
have fewer unique passives than requested, the child inherits the available ones.
The second roll asks for zero to three random additions, limited by the remaining
slots on the child. The default weights (`Combi_PassiveInheritNum` and
`Combi_PassiveRandomAddNum` in `BP_PalGameSetting`, both `[4, 3, 2, 1]`) give the
following chances for those rolls:

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

### Random-passive selection bug

An apparent bug in the game can cause it to skip a random passive it tried to add,
leaving the child with fewer passives. It can also change which random passives
are more likely to be chosen. The
[passive assessment](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-passives)
explains the bug and gives an example using the current game data.

For nonmutated eggs, the table below shows the **calculated chance per egg that
at least one random addition fails**:

| Unique passives between the parents | Ordinary breeding | Special Cake |
| --- | ---: | ---: |
| Zero | ~0.0053% | ~0.0323% |
| Four | ~0.0013% - ~1.36%, depending on the passives | 0%: no random filling |

Allowing other combinations of parental passives raises the highest calculated
chances to about **1.6% for ordinary breeding** (roughly 1 in 60 eggs) and
**3.9% with Special Cakes** (roughly 1 in 26). Both cases use parents with two
unique passives between them. Which passives the parents have matters as well
as how many random additions the game tries.

These calculations use the default game data, treat the random rolls as
independent, and assume each possible lottery number is equally likely.
The [full calculation](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#nominal-per-egg-fill-failure-probabilities)
records the assumptions and examples. These rates also don't tell us how far
PalCalc's breeding-time estimates might differ; see the
[limitations page](LIMITATIONS.md#random-passive-selection-bug).

### Special Cakes

Special Cakes (in-game name `Cake05`, with `PassiveInheritCountOverride=4` in
`DA_BreedingItemEffectData`) will replace both passive-count rolls with a target of
four passives.
Other breeding items have different effects; see the
[item-effect table](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#shared-inputs-and-arithmetic).

When using Special Cakes:

- If the parents have at least four unique passives between them, the child
  inherits four of those passives.
- If the parents have fewer than four, the child inherits all of them and the
  game tries to fill the remaining slots with random passives.

For example, if the parents only have Lucky and Runner between them, the child
inherits both and the game attempts to add two random passives.

The random-selection bug described above can leave the child short of four
passives when random filling is needed. [Mutation](#mutation-and-talent-bonus-items)
can also change which passives the child receives.

For the calculations and how cakes are used in solver search, see the
[solver overview](PalCalc.Solver/README.md#choosing-the-breeding-method) and
[Special Cake probability example](PalCalc.Solver/README-BREED-ESTIMATE.md#special-cake-override).

### Important Details

**a.** The game combines and deduplicates the parents' passives before making its
selection. This means:

- Distribution of passives between parents has no effect - e.g. `2 + 2`, `3 + 1`, `4 + 0` all behave the same
- Having the same passive on both parents doesn't make it more likely to be inherited
- Additional unwanted passives reduce the chance of selecting the desired passives

**b.** Wild Pals can start with species-specific passives, such as Gorirat's
Conceited, before random additions. These aren't automatically added to bred
children; they affect inheritance when actually present on a parent. Special spawn
and capture paths have their own inputs; see [wild Pals](#wild-pals).

**c.** The "inherited from parents" count is an _upper limit_ on what to inherit. 
If the game rolls three but the parents only have two, the child inherits those two. 

**d.** If any parent has at least one passive, the child will always have at least one passive. The only way to get a child with zero passives is if the parents have zero passives.

For a full walkthrough of the probability calculations, see
[Estimating Breeding Time for Inheriting Passives](PalCalc.Solver/README-BREED-ESTIMATE.md).

## Active/Attack Skills

Only the parents' equipped attacks are considered for inheritance. Non-inheritable
attacks (`IgnoreRandomInherit` in `DT_WazaDataTable`), including Pal-exclusive
attacks, are left out of the selection entirely.

For ordinary breeding, the game uses this process:

> 1. Combine the parents' equipped attacks and remove duplicates
> 2. Remove non-inheritable attacks
> 3. Select one remaining attack at random, if any are available
> 4. Add all attacks the child species learns at level one
> 5. Remove duplicates from the final list

Having the same attack equipped on both parents doesn't make it more likely to
be inherited. The selected attack can also be one the child naturally learns at
level one, in which case inheritance is still possible but adds no new attack to
its final available moves.

With a Special Cake (`bInheritAllActiveSkills=true` in `DA_BreedingItemEffectData`),
the child inherits all eligible equipped attacks from both parents, rather than
just one. Since only three attacks can be equipped by any pal, each parent can
contribute at most three equipped attacks.

### Important Details

**a.** If the two parents each have one attack equipped, and only one of them is a desired attack, there's a 50/50 chance of inheriting that attack. (Two attacks to pick from.) But if the other parent equips a non-inheritable attack, the desired attack chance is 100%. (One attack to pick from.)

Available attacks include inherited moves and the species' natural moves
(`DT_WazaMasterLevel`) through the Pal's current level.
See the [attack assessment](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-attacks)
for the inheritance rules and checked hatch routes.

## IVs

IVs are the underlying HP, Attack, and Defense values, called talents in the game
data. The [IV assessment](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-ivs)
establishes the inheritance process below.

### IV source selection

The game doesn't simply roll how many IVs to inherit and then choose that many
stats. It chooses between three groups of inheritance patterns. In the middle
group, HP always comes from a parent, and in another group, all three stats are inherited.
That means asking for HP can give different odds from asking for Attack or Defense.

The default weights `[3, 2, 1]` (`Combi_TalentInheritNum` in `BP_PalGameSetting`)
choose between these groups, giving the calculated chances below.
See the
[recorded game inputs](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#shared-inputs-and-arithmetic).

| Selection group | Calculated chance | Possible stats taken from parents |
| --- | ---: | --- |
| First | 50% | HP **or** Attack **or** Defense |
| Second | 33% | HP+Attack+Defense **or** HP+Defense **or** HP+Attack |
| Third | 16% | HP+Attack+Defense |

The first two groups have multiple sub-options, which Palworld chooses at random.
Each inherited stat has a separate 50/50 choice between
the parents' values. Stats not taken from parents get fresh random values from 0–100.

Current verification of the exact selection process is tracked in the
[research checklist](docres/research/palworld-breeding/README.md#current-verification-status).
The [solver's IV calculations](PalCalc.Solver/README.md#iv-probabilities) explain
how those rules become probability and effort estimates.

### Mutation and talent-bonus items

Some [breeding items](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#shared-inputs-and-arithmetic)
(Mushroom Cake - `Cake02`, and `Cake04` - Extravagant Vegetable Cake, in `DA_BreedingItemEffectData`) add a separate 1–5 bonus
to each ordinary offspring IV, capped at 100, _after_ the child IV is selected.
Mutation instead gives fresh IVs in the default range 90–100 (`Combi_MutationMinTalent=90` in the native game
settings), _skipping_ both parental IV inheritance and the item bonus.

Mutation also attempts an extra attack of the child's primary element, which may
duplicate one the child already has. Species and passives use separate selection
rules from ordinary breeding.

#### Mutation species

Mutation skips special recipes and uses a randomized breeding-power calculation:

1. Calculate a target power using the lower parental breeding power, the
   difference between the parents' powers, and a random adjustment.
2. Select the nearest eligible species, using the same priority and table-order
   tie breakers as ordinary selection. Special-recipe children are allowed in
   this pool; bosses and other ineligible species are still excluded.
3. Use the selected species' Alpha form if one is available. This replaces the
   ordinary Alpha-conversion roll.

The [mutation species model](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#child-species)
gives the exact arithmetic and settings behind the target power.

#### Mutation passives

Without a passive-count override, the game:

1. Draws one mutation passive and one high-rank random passive, removing duplicates.
2. Randomly selects distinct parental passives not already present until the
   child has four passives or the parental pool is exhausted.
3. Attempts ordinary random filling for any remaining slots. The usual
   [selection bug](#random-passive-selection-bug) can leave a shortfall.

The mutation pool uses `AddMutationPal`; the high-rank random pool uses
`AddPal=true` and `Rank>3` (both in `DT_PassiveSkill_Main`). These draws use the
pool's selection weights (`LotteryWeight`).

With a Special Cake, the game first builds the ordinary cake outcome. It then
draws a high-rank passive absent from that outcome, followed by a mutation passive
absent from both the outcome and the new high-rank choice. Any successful mutation
draw goes first, followed by any successful high-rank draw and then the cake
outcome; the list is trimmed to four. A draw with no eligible candidate adds
nothing. These new entries can therefore displace parental passives.
See the [full passive model](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#child-passives)
for the selection and exclusion rules.

PalCalc's estimates omit mutation and IV bonuses. The
[version report](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-ivs)
links the detailed rules for these modes.

## World-egg pickup effects

On the [world-egg pickup route covered by the report](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-ivs),
partner effects can turn an egg into an Alpha egg (`EggAlphaConversion`) or grant
an additional egg (`EggObtainExtraEgg`; partner-effect rows in `DT_PassiveSkill_Main`).
Alpha conversion keeps the existing child's traits. The additional egg can have
rerolled IVs and, for mutation eggs, changed passives; its gender and attacks
remain copied. These changes to the additional egg don't reroll the original child.

Collection method matters. The breeding farm's direct hatch and container pickup
keep the generated child's traits. In the checked drop-and-pickup route, an
existing egg also keeps its saved IVs and doesn't receive the extra-egg roll above.

Bonuses depend on the active companions and their eligibility. The report gives
the [effect conditions and combination rules](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#pickup-effect-conditions-tribe-grouping-and-activation-masks).

## Gender

Each species has gender probabilities in the game data (`MaleProbability` in
`DT_PalMonsterParameter`).

Ordinary and mutated children use the selected species' gender ratio. Fixed or
absent genders occur on special encounter forms: for example, raid Bellanoir
variants are configured female-only, tower Shadowbeak forms male-only, and police
Rayhound/Nitewing forms genderless. These settings belong to those special forms,
not their ordinary species. The [special-encounter snapshot](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#special-encounter-gender-rows)
lists the relevant game-data rows.

See the [gender assessment](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-gender)
for the game rules, and [gender handling](PalCalc.Solver/README.md#gender) for
PalCalc's estimates.

## Wild Pals

A wild Pal's skills can depend on what it receives when spawned, what it learns
with levels, and any changes made during capture.

- **Passives:** Wild Pals normally receive species-specific passives before random
  additions. Special spawns can supply other traits; World Tree aura spawns, for
  example, can add one World Tree passive if a slot remains.
- **Capture bonuses:** Effects such as Yakumo's passive sharing (`GuardianDog` in
  `DT_PartnerSkillParameter`) can add passives during capture. An empty slot alone
  doesn't guarantee a bonus: the target and active companion must meet the
  effect's conditions.
- **Attacks:** Available moves include learned attacks and natural moves through
  the Pal's current level.
  Seeing a boss use an attack in combat doesn't guarantee it will be available
  after capture; Panthalus has special handling covered in the report.
- **Gender:** Wild Pals normally use their species' ratio, though special spawns
  can supply a different gender. The checked capture routes preserve the assigned
  gender.

### Random passives on wild Pals

When ordinary wild random filling is enabled, the game starts with species and
spawn-supplied passives, then rolls how many random additions to attempt. This
uses different count weights from breeding (`PassiveSkillAppendNumWeights` in
the native game settings):

| Random-add attempts | Calculated chance |
| --- | ---: |
| 0 | 10% |
| 1 | 76% |
| 2 | 10% |
| 3 | 3% |
| 4 | 1% |

These chances normalize the default weights `[10, 76, 10, 3, 1]`. Attempts are
capped by the remaining slots, so this isn't a table of final passive counts.
Special spawns and rare additions have their own rules.

The count roll differs, but wild generation uses the same ordinary random-passive
pool and selection helper as breeding (`AddPal` in `DT_PassiveSkill_Main`). The
[selection bug](#random-passive-selection-bug) therefore applies here too: an
attempt can fail to add a passive.

These weights come from the game code and are not visible in the game's assets.
See the [initialization evidence](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#wild-pal-passives)
for their source and the conditions which control random filling. PalCalc's current
estimate still uses a [simplified wild-passive distribution](LIMITATIONS.md#wild-random-passive-counts).

### Yakumo passive order

**Yakumo's passive order can matter when slots are limited.** The game checks
eligible passives in stored order, using the same per-check rate for each, and
stops when the captured Pal reaches four passives. For example, if the captured
Pal already has three passives, the first successful Yakumo passive fills its
last slot; later passives aren't checked. Earlier passives get priority for the
remaining space, rather than a larger per-roll bonus. With enough room for every
eligible passive, this slot competition disappears.
[Order and four-slot rules](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#capture-source-traversal-order).

The version report covers [wild passives and capture conditions](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#wild-pal-passives),
[available attacks and special encounters](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#wild-pal-attacks),
and [gender](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#wild-pal-gender).
See the
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
