# Palworld Breeding Mechanics

This document covers all Palworld breeding mechanics as used by PalCalc. It's based
on game asset files, manual breeding results, and raw game disassembly.

We try to restrict ourselves to _definitive_ discoveries and not just a "likely" process.

## Pal Species

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

The ordinary breeding-power algorithm is well known in the Palworld community.
The child pal species is always consistent, so it's trivial to check for accuracy
with manual testing. (Mutated eggs ignored here.)

Breeding powers, priorities, and special recipes are read directly from the game
data. PalCalc uses them to generate `breeding.json` and `breedingdb.json`, which
the solver reads when finding possible paths. This file is embedded in
the PalCalc EXE files.

## Passive Skills

Our understanding of Palworld passive inheritance is entirely credited to Reddit
user [/u/mgxts](https://www.reddit.com/r/Palworld/comments/1af9in7/passive_skill_inheritance_mechanics_in_breeding/),
who disassembled the game code to directly inspect Palworld's logic. Nonetheless, there
are data in the game assets to further support those details.

Palworld's `BP_PalGameSetting` asset defines two arrays of weights:

- `Combi_PassiveInheritNum`: `[4, 3, 2, 1]`
- `Combi_PassiveRandomAddNum`: `[4, 3, 2, 1]`

Logically, there's a limit of 4 passives per Pal, and there's 4 entries in each list, so these must relate to the chance of different inheritance counts.

Empirically, we know that a child Pal will always inherit at least 1 passive from its parents. Therefore, since `Combi_PassiveInheritNum` covers 4 outcomes, it must be a set of weights for inheriting 1-4 passives.

Empirically, we also know that it's possible to get children without any random passives. Therefore, since `Combi_PassiveRandomAddNum` also covers 4 outcomes, it must be weights for getting 0-3 extra random passives.

The standard method for weighted-probabilities is to divide each member by the sum of the weights. Both of these sets of weights sum to `10`, so dividing by `10` should give us the actual probability as a normalized percent:

| Inherited from parents | Chance | Randomly added | Chance |
| --- | ---: | --- | ---: |
| 1 passive | 40% | 0 passives | 40% |
| 2 passives | 30% | 1 passive | 30% |
| 3 passives | 20% | 2 passives | 20% |
| 4 passives | 10% | 3 passives | 10% |

This data is valuable but doesn't give us the full picture - how, exactly, are these probabilities
used? Here we rely on the work of `/u/mgxts`:

> 1. Combine the passive lists from both parents and remove duplicates
> 2. Roll the number of passives to inherit (`Combi_PassiveInheritNum`)
> 3. Choose that many passives from the parents' combined list at random
> 4. Roll the number of random passives to add (`Combi_PassiveRandomAddNum`)
> 5. Add random passives until that count or the four-passive limit is reached

Passive inheritance happens in two stages: the child first inherits passives
from its parents (steps 1-3), then the game may add random passives (steps 4-5).

_Note: If Special Cakes are used for breeding, the "roll the number of passives to inherit" check is set at `4`. The game data for `Cake05`, the Special Cake, has this setting: `PassiveInheritCountOverride=4`._

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
[Estimating Breeding Time for Inheriting Passives](./README-BREED-ESTIMATE.md).

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

_Note: If Special Cakes are used for breeding, all equipped attacks will be inherited if possible. The game data for `Cake05`, the Special Cake, has this setting: `bInheritAllActiveSkills=true`._

### Important Details

**a.** If the two parents each have one attack equipped, and only one of them is a desired attack, there's a 50/50 chance of inheriting that attack. (Two attacks to pick from.) But if the other parent equipped an attack with the `IgnoreRandomInherit` flag, the desired attack chance is 100%. (One attack to pick from.)

**b.** Special Cakes cause children to inherit all possible equipped attacks from each parent. More specifically, they cause children to inherit three attacks from each parent. There is no way for a child to inherit more than three attacks from any one parent.

## IVs

There is little verifiable information available on the IV inheritance mechanics. A number
of sources collected data and posed their own opinions, but without access to the data
itself, we can't tell if those opinions are correct.

For PalCalc, the original discussion and sample data are collected in
[issue #22](https://github.com/tylercamp/palcalc/issues/22). (Specific comment
with sample data + processing script [here](https://github.com/tylercamp/palcalc/issues/22#issuecomment-2509009708).)

Palworld refers to IVs as "Talent" in the game data. `BP_PalGameSetting` defines:

- `Combi_TalentInheritNum`: `[3, 2, 1]`

Empirically, we know that a child Pal will always inherit at least 1 IV from its parents.
(Over 190 samples, not a single child was seen with fully random IVs.)
Therefore, since `Combi_TalentInheritNum` covers 3 outcomes, it must be a set of weights
for inheriting 1-3 IVs.

PalCalc interprets these via sum-and-divide like the other `Combi_*` data mentioned in
the Passive Skills section:

| Inherited IVs | Chance |
| --- | ---: |
| 1 | 50% |
| 2 | 33.33% |
| 3 | 16.67% |

The samples have not shown a clear preference for a particular stat, parent,
gender, level, or higher IV. Children may inherit all selected values from one
parent or a mixture from both.

PalCalc assumes the following process:

> 1. Roll the number of IVs to inherit using `Combi_TalentInheritNum`.
> 2. Select that many stats from HP, attack, and defense.
> 3. For each selected stat, choose either parent's value with an equal chance.


## Implementation References

- Child species: [`PalBreedingCalculator.cs`](../PalCalc.GenDB/PalBreedingCalculator.cs)
  and [`UniqueBreedComboReader.cs`](../PalCalc.GenDB/GameDataReaders/UniqueBreedComboReader.cs)
- Game settings: [`GameSettingReader.cs`](../PalCalc.GenDB/GameDataReaders/GameSettingReader.cs)
  and [`BreedingMechanics.cs`](../PalCalc.Model/BreedingMechanics.cs)
- Passive estimates: [`Probabilities/Passives.cs`](./Probabilities/Passives.cs)
- IV estimates: [`Probabilities/IVs.cs`](./Probabilities/IVs.cs)
- Attack estimates: [`Probabilities/Attacks.cs`](./Probabilities/Attacks.cs)
- Attack inheritance flags: [`ActiveSkillReader.cs`](../PalCalc.GenDB/GameDataReaders/ActiveSkillReader.cs)
