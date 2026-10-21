# Estimating Breeding Time for Inheriting Passives

Our ordinary passive model started with
[the work by /u/mgxts](https://www.reddit.com/r/Palworld/comments/1af9in7/passive_skill_inheritance_mechanics_in_breeding/).
The [2026-10-16 assessment](../docres/research/palworld-breeding/README.md) later
recovered the breeding algorithm for Steam build 25246127.

The examples below explain how PalCalc estimates ordinary passive inheritance,
using the probability weights stored in this repository. These are worked
calculations, not new measurements from breeding in the game. See the
[mechanics README](../PALWORLD-BREEDING-MECHANICS.md) for the supporting sources,
known limits of random passive filling, and how IV and attack inheritance fit in.

## Probability References

The main Reddit post includes a table of probabilities for inheriting different numbers of traits from the list of parents. The default values are represented by [`BreedingMechanics`](../PalCalc.Model/BreedingMechanics.cs). Each `PalDB` owns a mechanics instance, so customized databases can supply different probabilities.

PalCalc uses these probabilities:

- Chance of inheriting exactly `N` passives from the parents (`Probability` column in the table from the Reddit post) (`BreedingMechanics.PassiveProbabilityDirect`)
- Chance of inheriting `N` random passives (not represented in any table; pulled from the "RANDOM PASSIVE SKILLS" section of their pseudocode) (`BreedingMechanics.PassiveRandomAddedProbability`)
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

_Note: A random addition could give us one of the desired passives, but PalCalc
ignores that possibility. We also assume random filling succeeds. The game can
fail to add a passive because of a bug in its selection weights or because no
eligible passives remain. We don't yet know how often those failures happen
with actual game data, so we can't say how much they affect the estimates._

### Example - Estimates for Children, Compensating for "At Best"

From the above, we have:

1. A 2% chance of getting exactly the two desired passives
2. A 5.5% chance of getting the two desired passives and one undesired passive

PalCalc will produce a child representing [1]. A 2% chance is roughly 1 in 50 odds, and will require (on average) 50 attempts to accomplish. At 10 minutes per breeding attempt, that gives ~8 hours.

For the child representing [2], keep in mind that case #1 is optimal, and #2 is the fallback. Getting case #1 would be at least as good as getting case #2, though the opposite is not true. Since getting case #1 is at least as good, we can rephrase the question from "probability of getting exactly X" to "probability of getting something _at least as good_ as X".

In that case we can combine the probabilities for [1] and [2], giving us a 7.5% chance of getting a result _at least as good_ as #2. These are roughly 1 in 13 odds, requiring (on average) 13 attempts to accomplish. At 10 minutes per breeding attempt, that gives us ~2 hours.

**This gives us our final result - from these two parents we create two children, one with an ~8 hour estimate, and the other with a ~2 hour estimate.**

(The logic involving `P(Direct)` and `P(Random)` is implemented in `Probabilities.Passives.ProbabilityInheritedTargetPassives`. `CandidateExpander` accumulates these probabilities and assigns them to candidate children.)

## Special Cake override

Special Cakes skip both ordinary count rolls. Instead, the child inherits up to
four distinct passives from the parents, and the game tries to fill any remaining
slots with random passives. This means we don't multiply by
`PassiveRandomAddedProbability` when calculating the cake probability.

More generally, a positive `PassiveInheritCountOverride=N` replaces both count
rolls with the following process:

```text
target = min(N, 4)
pool = distinct(parent A passives + parent B passives)
select up to target parental passives without replacement
attempt random filling for target - selected_count
stop; do not perform the ordinary random-add count roll
```

See the [recovered override branch](../docres/research/palworld-breeding/assessments/2026-10-16-build-25246127/evidence.md#passive-selection-and-override)
for the underlying game evidence. PalCalc's Special Cake calculation uses a target
of four.

PalCalc assumes those filling attempts succeed, so it models a four-passive
child. The game can sometimes finish with fewer; see the
[mechanics README](../PALWORLD-BREEDING-MECHANICS.md) for the known limitations.

For the earlier parents `[Lucky, Brave]` and `[Runner, Brave, Workaholic]`, the
combined list has four unique passives. The cake selects all four, so Lucky and
Runner are guaranteed to be included. If the parents only had Lucky and Runner,
we'd instead model a child with those two passives and two random fillers. Those
extra slots stay on the candidate, since they affect inheritance in later
generations.

For a larger pool, suppose the parents have five unique passives between them,
including two desired passives. The cake selects four parental passives. The
chance of including both desired passives is `Choose(3,2) / Choose(5,4) = 3/5`:
choose the other two inherited passives from the three irrelevant ones, out of
all ways to select four from five.

Because PalCalc assumes successful filling, the override probability is zero for
requested final counts below four, even when the parents have fewer passives or
none at all. This is a property of the estimate, not proof that every real cake
child finishes with four passives.

`MaxBredIrrelevantPassives` limits the ordinary passive options we explore during
search. It doesn't exclude the extra slots required by cakes, and it isn't a
restriction on the final result's passive count. Owned inputs still have their
separate limit on irrelevant passives.

The override is implemented by
`Probabilities.Passives.ProbabilityInheritedTargetPassivesForOverride`.
Ordinary and cake candidates each carry a `BreedingOutcome` with their breeding
method and passive probability. During search, we use that probability and the
IV odds to estimate effort. Ordinary attack-inheritance odds are saved for later.

At finalization, we choose the equipped attacks within the candidate's breeding
method and recalculate attempts, effort, and cake use, including any adjustment
for the required gender. We calculate and cache the costs and exact parent
choices before building the result trees, so we don't have to construct trees
we'll immediately discard.

A slower path that saves cakes can still be useful alongside a faster path that
uses more. We keep those tradeoffs, comparing costs only when the paths have
matching output, inputs, and other resource properties. The configured effort
and cake limits still apply. See the [solver walkthrough](./README.md#8-apply-surgery-and-return-the-results)
for the full comparison rules.

We only consider cakes when the parents can equip and pass on multiple requested
attacks that the child doesn't already learn at level one. We could also use
cakes just to improve passive inheritance, but that would add more options to
the search. We skip those for performance, accepting that we may miss some valid
or better paths.

During search, the compact attack profiles still compare available attacks and
estimated cake costs. Neither breeding method nor cake count is used as an
effective property for grouping candidates.
