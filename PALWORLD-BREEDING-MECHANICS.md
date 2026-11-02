# Palworld Breeding Mechanics

This page explains the breeding rules that matter when planning a Pal, along with
PalCalc's approximations. Read the [solver overview](PalCalc.Solver/README.md) for
how those mechanics are used to search for breeding paths.

For farm-bred eggs, the child's species, gender, IVs, passives and inherited
attacks are determined when the egg is produced. The checked hatch routes use
that stored child rather than rerolling inheritance. See the
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

The linked version reports contain the detailed findings, evidence and conditions.
Their game-code analysis doesn't measure how often every outcome occurs in play,
or establish that PalCalc models every rule.
See [PalCalc's limitations](LIMITATIONS.md) for the approximations used in estimates.

## Pal Species

Each Pal species has a breeding power (`CombiRank` in `DT_PalMonsterParameter`).
The game first checks for a special recipe (`DT_PalCombiUnique`) matching the
parents, including their genders where required. It uses the first matching
recipe in table order.

If no recipe applies, it uses the parents' breeding powers to choose a child. For
normal breeding powers, this works out to averaging the parents' powers and rounding
up, then adding any item rank adjustment. It selects an eligible child with the
nearest power, breaking distance ties by breeding priority (`CombiDuplicatePriority`
in `DT_PalMonsterParameter`) and exact ties by table order.

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

For how PalCalc builds its list of breeding pairs, see the
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
slots on the child. The default count weights (`Combi_PassiveInheritNum` and
`Combi_PassiveRandomAddNum` in `BP_PalGameSetting`, both `[4, 3, 2, 1]`) give the
following chances for those rolls. These are calculated probabilities, not
measured breeding frequencies:

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

An apparent bug in random selection can bias which passives are added or leave
the child with fewer passives than requested. Its frequency in gameplay hasn't
been measured. The [limitations page](LIMITATIONS.md#random-passive-selection-bug)
explains how this affects PalCalc's estimates.

The [passive assessment](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-passives)
links the recovered rules and a worked example of this bug using current game data.

### Special Cakes

Special Cakes (`Cake05`, with `PassiveInheritCountOverride=4` in
`DA_BreedingItemEffectData`) replace both ordinary count rolls with a target of
four passives.
Other breeding items have different effects; see the
[item-effect table](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#shared-inputs-and-arithmetic).

In ordinary breeding:

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
[solver overview](PalCalc.Solver/README.md#finishing-the-results) and
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
level one, in which case inheritance adds no new attack to its available moves.

With a Special Cake (`bInheritAllActiveSkills=true` in `DA_BreedingItemEffectData`),
the child inherits all eligible equipped attacks from both parents, rather than
just one. Each parent can contribute at most three equipped attacks. The child's
three equipment slots don't mean additional inherited attacks are lost; learned
attacks and currently equipped attacks are different things.

### Important Details

**a.** If the two parents each have one attack equipped, and only one of them is a desired attack, there's a 50/50 chance of inheriting that attack. (Two attacks to pick from.) But if the other parent equips a non-inheritable attack, the desired attack chance is 100%. (One attack to pick from.)

Available attacks include inherited moves and the species' natural moves
(`DT_WazaMasterLevel`) through the Pal's current level. Only three can be equipped
at once. See the
[attack assessment](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-attacks)
for the inheritance rules and checked hatch routes.

## IVs

IVs are the underlying HP, Attack, and Defense values, called talents in the game
data. The [IV assessment](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-ivs)
establishes the inheritance process below.

### IV source selection

The game doesn't simply roll how many IVs to inherit and then choose that many
stats. It chooses between three groups of inheritance patterns. In the middle
group, HP always comes from a parent, and one pattern inherits all three stats.
That means asking for HP can give different odds from asking for Attack or Defense.

The default weights `[3, 2, 1]` (`Combi_TalentInheritNum` in `BP_PalGameSetting`)
choose between these groups, giving the nominal calculated chances below.
They select a group of patterns, rather than simply how many stats to inherit.
See the
[recorded game inputs](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#shared-inputs-and-arithmetic).

| Selection group | Nominal chance | Possible stats taken from parents |
| --- | ---: | --- |
| First | 50% | HP **or** Attack **or** Defense |
| Second | 33⅓% | HP+Attack+Defense **or** HP+Defense **or** HP+Attack |
| Third | 16⅔% | HP+Attack+Defense |

Within either of the first two groups, the game chooses one of its three patterns
with equal nominal chances. Each parent-sourced stat has its own nominal
50/50 choice between the two parents' values. Stats not taken from parents get
fresh random values from 0–100, inclusive. A random value can happen to match a
parent's IV, but that doesn't mean it was inherited. These are calculated
probabilities, not measured gameplay frequencies.

Current verification of the exact selection process is tracked in the
[research checklist](docres/research/palworld-breeding/README.md#current-verification-status).
The [solver's IV calculations](PalCalc.Solver/README.md#iv-probabilities) explain
how those rules become probability and effort estimates.

### Mutation and talent-bonus items

Some [breeding items](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#shared-inputs-and-arithmetic)
(`Cake02` and `Cake04` in `DA_BreedingItemEffectData`) add a separate 1–5 bonus
to each ordinary offspring IV, capped at 100. Mutation instead gives fresh IVs
in the default range 90–100 (`Combi_MutationMinTalent=90` in the native game
settings), skipping both parental IV inheritance and the item bonus.

Mutation also changes species and passive selection. Its species pool can include
special-recipe children, but still excludes bosses and other ineligible species.
Mutation passives can displace parental passives, even with a Special Cake. It
also attempts an extra attack of the child's primary element, which may duplicate
one the child already has.

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
These findings don't establish that every egg pickup behaves the same way.

Bonuses depend on the active companions and their eligibility. The report gives
the [effect conditions and combination rules](docres/research/palworld-breeding/assessments/1.0.5.102999/evidence.md#pickup-effect-conditions-tribe-grouping-and-activation-masks);
it doesn't establish an overall bonus rate for arbitrary or changing parties.

## Gender

Each species has gender probabilities in the game data (`MaleProbability` in
`DT_PalMonsterParameter`). A recipe's required parent genders are separate from
the child's own gender: a gender-specific recipe doesn't by itself explain how
the child's gender is drawn.

Ordinary and mutated children use the selected species' gender ratio; some species
have a fixed gender or no gender. The checked hatch and extra-egg routes preserve
the assigned gender. See the [gender assessment](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-gender)
for the game rules, and [gender handling](PalCalc.Solver/README.md#gender) for
PalCalc's estimates.

## Wild Pals

A wild Pal's skills can depend on what it receives when spawned, what it learns
with levels, and any changes made during capture.

- **Passives:** Ordinary generation adds species-specific passives before random
  filling. Special spawns can supply other traits; World Tree aura spawns, for
  example, can add one World Tree passive if a slot remains.
- **Capture bonuses:** Effects such as Yakumo's passive sharing (`GuardianDog` in
  `DT_PartnerSkillParameter`) can add passives during capture. An empty slot alone
  doesn't guarantee a bonus: the target and active companion must meet the
  effect's conditions. The findings don't establish a bonus rate for every capture.
- **Attacks:** Available moves include learned attacks and natural moves through
  the Pal's current level. They aren't limited to the three equipped moves.
  Seeing a boss use an attack in combat doesn't guarantee it will be available
  after capture; Panthalus has special handling covered in the report.
- **Gender:** Wild Pals normally use their species' ratio, though special spawns
  can supply a different gender. The checked capture routes preserve the assigned
  gender.

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
