# PalCalc limitations

PalCalc skips some breeding options to keep searches manageable. That means a
valid plan — or a faster one — can be missed. Its estimates also simplify some game
rules. The sections below explain when the results may differ from the game.

For game rules, see [Palworld Breeding Mechanics](PALWORLD-BREEDING-MECHANICS.md).
The [solver overview](PalCalc.Solver/README.md) explains how PalCalc uses them.
The [research checklist](docres/research/palworld-breeding/README.md#current-verification-status)
records which mechanics and game versions have been reviewed.

## What PalCalc models

The solver looks for a target species with the requested passives, HP/Attack/Defense
IV thresholds, attacks, and gender, using the allowed owned and wild Pals. It
estimates inheritance attempts and effort, with supported items, training, and
surgery options included according to the settings.

PalCalc's estimates cover the selected settings and supported mechanics. They don't
cover every way to obtain the requested Pal.

## Deliberate approximations in estimates

### Random passive selection bug

When Palworld adds random passives during breeding, it uses a pool of eligible
passives and their selection weights. It removes already-selected passives from
a temporary copy of that pool.

After removing candidates, the game mixes the remaining weights with weights
from the original list. This can bias selection or consume an attempt without
adding a passive. We consider this mismatch an apparent bug in Palworld's
inheritance code.

The game can therefore add fewer random passives than PalCalc predicts. We have
[calculated chances for different parent setups](PALWORLD-BREEDING-MECHANICS.md#random-passive-selection-bug),
using the default game data and assumptions about the random rolls. Those
figures haven't been confirmed through gameplay measurements, and how much this
changes PalCalc's breeding-time estimates is still unknown.

This affects ordinary random additions and Special Cakes that rely on random
filling because the parents have fewer than four distinct passives.
It also affects ordinary wild random filling, which uses the same selector.
See [passive selection and its limits](PALWORLD-BREEDING-MECHANICS.md#passive-skills)
for the game rules and supporting evidence.

Fewer unwanted passives can improve inheritance odds in later generations.

**PalCalc assumes random filling succeeds and does not model this behavior in
its probabilities.**

### Desired traits supplied by fresh random values

Breeding can succeed more often than the estimate suggests when a random addition
supplies a desired trait. PalCalc counts desired passives inherited from the
parents, but ignores the chance of gaining them randomly. It also counts qualifying
IVs inherited from parents, ignoring fresh random values that happen to
meet the target. These omissions can overestimate breeding effort.

See the [passive calculation](PalCalc.Solver/README-BREED-ESTIMATE.md) and
[IV calculation](PalCalc.Solver/README.md#iv-probabilities).

### Unknown or irrelevant passive identities

Later breeding can be easier than predicted when both parents share an unwanted
passive. For passives you haven't requested, PalCalc sometimes tracks how many
there are without keeping their names. It can then count a shared passive twice,
making the pool of distinct parental passives appear larger and the desired
inheritance less likely.

See [how the solver represents passives](PalCalc.Solver/README.md#passives).

### Production, capture, and incubation time

Completion times can differ from your actual routine. Egg-production time includes
an assumed allowance for parent availability; PalCalc doesn't simulate all farm
activity. Capture effort and incubation time also depend on the chosen settings.

See [egg-production time](PalCalc.Solver/README.md#egg-production-time)
and [effort estimation](PalCalc.Solver/README.md#estimating-the-effort-of-a-path).

## Search tradeoffs

### Search retains a compact set of attack options and breeding paths

PalCalc uses passive and IV odds to compare paths during search. It works out
the attack odds for specific parent loadouts at the end. A path dropped early
could have turned out faster once those attack odds were included.

The final comparison of effort and cake use considers only surviving paths. It
can't recover discarded candidates, and simplifying the results can remove more
similar paths.
See [path comparisons](PalCalc.Solver/README.md#comparing-paths)
and [finalization](PalCalc.Solver/README.md#8-apply-surgery-and-return-the-results).

### Surgery is deferred, and configured limits bound the search

PalCalc can miss plans that would benefit from surgery between breeding steps.
It applies surgery after the breeding search to keep the number of options
manageable.

A search with no results doesn't establish that the requested Pal is impossible
to obtain. Limits on breeding steps, search iterations, effort, items and allowed
input Pals can all exclude valid plans.

See [search coverage](PalCalc.Solver/README.md#search-coverage-and-limits)
and [finishing results](PalCalc.Solver/README.md#finishing-the-results).

## Unverified and unmodeled behavior

### Wild random passive counts

PalCalc currently assumes that wild Pals are equally likely to receive zero,
one, two, three or four random passives. The game's ordinary wild-generation
weights instead strongly favor one attempted addition. Existing passives, special
spawns and failed selections can also change the final count.

This mismatch can affect the estimated capture effort for wild starting Pals
with a limited number of unwanted passives. The solver's probability logic
hasn't yet been updated to the [recovered wild-passive rules](PALWORLD-BREEDING-MECHANICS.md#random-passives-on-wild-pals).

### Other omitted behavior and verification limits

Inheritance estimates omit mutation and talent bonuses, even though the
[version report](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md#child-ivs)
describes their effects on generated children. A confirmed game mechanic can
therefore still be missing from PalCalc's model.

Capture bonuses and extra-egg effects depend on the target Pal, collection method
and active companions. The [assessment](docres/research/palworld-breeding/assessments/1.0.5.102999/assessment.md)
covers specific target conditions and companion setups. An empty passive slot
alone doesn't guarantee a capture bonus, and the findings don't give an overall
bonus rate across different capture or pickup situations. Rates for setups where
companion ranks or active bonus sources change during the process haven't been
established.

PalCalc's species database hasn't been shown to match every live pairing. Ties,
special recipes and variant parents can affect the result. See the
[species comparison](PalCalc.Solver/README.md#species-database) for the remaining
data checks.

An unverified mechanic isn't necessarily implemented incorrectly; it means the
published evidence hasn't established the full process. Checks are also specific
to a game version. After an update, both game logic and data need checking before
older findings can be carried forward. See the
[research hub](docres/research/palworld-breeding/README.md#check-a-game-update).

## Interpreting results

Effort is an average estimate, not a promised completion time. A child can arrive
on the first attempt or take much longer. The results are useful alternatives from
the retained search space, not an exhaustive list or a guarantee of the fastest
possible real-world plan.

Saved estimates may need a fresh solve after mechanics or settings change. A saved
tree records a previous result; it doesn't itself establish that its estimate uses
the current model.
