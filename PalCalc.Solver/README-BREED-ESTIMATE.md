# Estimating Breeding Time for Inheriting Passives

Thanks to [the work by /u/mgxts](https://www.reddit.com/r/Palworld/comments/1af9in7/passive_skill_inheritance_mechanics_in_breeding/), we have detailed information on the _likely_ breeding process for inheriting passive skills. Note that:

1. The author mentions that this calculation has not been 100% verified.
2. At the time of writing, this reverse-engineering is ~8 months old, and has not been updated since it was first posted. (There is an [open issue](https://github.com/tylercamp/palcalc/issues/7) in this repo for any potential updates.)
3. This is [based on a disassembly of the Palworld code](https://www.reddit.com/r/Palworld/comments/1af9in7/comment/koexa09/?utm_source=share&utm_medium=web3x&utm_name=web3xcss&utm_term=1&utm_content=share_button), and while the author emphasizes it's not 100% confirmed, any work based on disassembly is likely to be more accurate than efforts based purely on statistics.
4. Assuming that the disassembly by /u/mgxts was accurate in the first place, and that there haven't been any updates in recent versions, there's still room for error in my own implementation of this calculation. There is an [open issue](https://github.com/tylercamp/palcalc/issues/8) for confirming this.

Nonetheless, the work by /u/mgxts is the best information we have at the moment. We welcome any additional information on any of the above points in the [Issues](https://github.com/tylercamp/palcalc/issues) section of this repo.

## Probability References

The main Reddit post includes a table of probabilities for inheriting different numbers of traits from the list of parents. These are reflected in [`GameConstants.cs`](../PalCalc.Model/GameConstants.cs).

Pal Calc uses these probabilities:

- Chance of inheriting exactly `N` passives from the parents (`Probability` column in the table from the Reddit post) (`GameConstants.PassiveProbabilityDirect`)
- Chance of inheriting `N` random passives (not represented in any table; pulled from the "RANDOM PASSIVE SKILLS" section of their pseudocode) (`PassiveRandomAddedProbability`)
  - The `Combi_PassiveRandomAddNum` contains 4 elements, and `select_weighted_random_index` can return `[0, Size-1]`, i.e. from 0 - 3. This means it's impossible to inherit 4 random passives.

## Calculating Probabilities

Once the breeding process has discovered and finalized two parents, at least one child will be created.

For example, let's consider:

- Target skills: Lucky, Runner, Swift
- Parent #1 skills: Lucky, Brave
- Parent #2 skills: Runner, Brave, Workaholic
- Allow up to 1 irrelevant passive

We first produce the list of possible passives and desired passives from these parents:

- Available desired passives: `[Lucky, Runner]` (2 passives)
- All available passives: `[Lucky, Brave, Runner, Workaholic]` (4 unique passives)

We then calculate probabilities for potential outcomes of children which:

- Have all desired passives from the parents (2)
- Have exactly N total passive skills

Since there are two desired passives, and we allow at most one irrelevant passive, we calculate for:

- A child with exactly two total passives
- A child with exactly three total passives

In either case, we calculate the total probability of achieving the specific total passive account _while still inheriting the desired passives from the parents._ This involves two separate probabilities - one for inheriting those desired passives (`P(Direct)`), and another for inheriting a specific amount of random passives (`P(Random)`).

### Example - Two Total

For the child with exactly two total passives, `P(Direct)` is based on the probability of inheriting that many passives (30%), and the probability of the 2 passives being selected from combined list of 4 unique passives from the parents.

Since there's only one specific combination we want, the probability of getting `[Lucky, Runner]` from the combined list of `[Lucky, Brave, Runner, Workaholic]` is `1 / (numTotal Choose numDesired)`, or `1 / (4 Choose 2)`, which works out to `1 / 6` or ~16%.

The final `P(Direct)` in this case just combines these two probabilities - 30% to inherit two, and 16% for those to be the passives we want, giving us `0.3 * 0.16 = 0.048` - a probability of ~5%.

For `P(Random)` we only need to consider the chance of getting zero random passives. This is a simple 40% chance.

Now that we have `P(Direct) = 5%` and `P(Random) = 40%`, we combine these: `0.05 * 0.4 = 0.02` - **a probability of ~2% for this exact result.**

### Example - Three Total

For the child with exactly three total passives, the final chance is based on the probability of _either_:

1. Inheriting exactly 2 passives from the parents, both being desired passives, _and_ getting exactly 1 random passive
2. Inheriting exactly 3 passives from the parents, including _at least_ the two desired passives, and getting exactly zero random passives

`P(Direct)` for [1] is the same as the "Two Total" example above, ~5%. `P(Random)`, the chance of exactly 1 random passive, is 30%. The combined probability for [1] is then `0.05 * 0.3 = 0.015`, or a 1.5% chance.

`P(Direct)` for [2] is different - here we have the same requirement of two specific passives, but one additional irrelevant passive. We need:

- The number of combinations when selecting the one irrelevant passive from the list of any irrelevant passives - `(numTotal - numDesired) Choose numIrrelevant`, `(4 - 2) Choose 1` - 2 combinations
- The number of combinations when choosing exactly 3 passives from the total list of passives - `numTotal Choose numInherited`, `4 Choose 3` - 4 combinations

The probability of choosing 3 passives from the list of 4, where those three will at least contain the two desired passives, works out to `2 / 4`, or ~50%. We combine this with the base probability of inheriting exactly 3 passives from the parents - 20% - to get `0.2 * 0.5 = 0.1`, or a 10% chance. This is the `P(Direct)` for this case.

`P(Random)` for [2] is the same as the "Two Total" example above, ~40%. The combined probability for [2] is then `0.1 * 0.4 = 0.04`, or a 4% chance.

Finally, we have a 1.5% chance for case [1], and a 4% chance for case [2]. Since _either_ of these would give us the desired outcome (the two desired passives and one undesired), we just add them for **a total 5.5% chance.**

_Note: It's possible for the randomly passive to be one of the desired passives, or one of the passives held by the parents. This is ignored since it only slightly affects the final probabilities._

### Example - Estimates for Children, Compensating for "At Best"

From the above, we have:

1. A 2% chance of getting exactly the two desired passives
2. A 5.5% chance of getting the two desired passives and one undesired passive

Pal Calc will produce a child representing [1]. A 2% chance is roughly 1 in 50 odds, and will require (on average) 50 attempts to accomplish. At 10 minutes per breeding attempt, that gives ~8 hours.

For the child representing [2], keep in mind that case #1 is optimal, and #2 is the fallback. Getting case #1 would be at least as good as getting case #2, though the opposite is not true. Since getting case #1 is at least as good, we can rephrase the question from "probability of getting exactly X" to "probability of getting something _at least as good_ as X".

In that case we can combine the probabilities for [1] and [2], giving us a 7.5% chance of getting a result _at least as good_ as #2. These are roughly 1 in 13 odds, requiring (on average) 13 attempts to accomplish. At 10 minutes per breeding attempt, that gives us ~2 hours.

**This gives us our final result - from these two parents we create two children, one with an ~8 hour estimate, and the other with a ~2 hour estimate.**

(The logic involving `P(Direct)` and `P(Random)` are implemented in `BreedingSolver.ProbabilityInheritedTargetPassives`. The logic for accumulating and assigning these probabilities is in `BreedingSolver.SolveFor`.)