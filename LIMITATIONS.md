# PalCalc limitations

PalCalc estimates useful breeding paths without simulating every detail of the
game or keeping every possible breeding tree. This page collects the current
modeling omissions and search tradeoffs, and explains what they mean for results.

For the game rules and detailed calculations, start with
[Palworld Breeding Mechanics](PALWORLD-BREEDING-MECHANICS.md). The
[research checklist](docres/research/palworld-breeding/README.md#current-verification-status)
is the main record of which mechanics and game versions have been checked. A
comprehensive baseline is pending after the research documentation reset. The
[solver overview](PalCalc.Solver/README.md) explains how the current implementation
uses those rules.

## What PalCalc models

A solve looks for a target species with the requested passives, HP/Attack/Defense
IV thresholds, attacks, and gender, using the allowed owned and wild Pals. It
estimates inheritance attempts and effort, with supported items, training, and
surgery options included according to the settings.

That doesn't mean every game mechanic has been independently verified. It also
doesn't mean the solver explores every way to satisfy those requirements. The
sections below distinguish deliberate simplifications from areas that still need
investigation.

## Deliberate approximations in estimates

### Random passive filling

PalCalc assumes the requested random additions can be filled successfully. The
recovered game code has an apparent weight-array bug that can bias selection or
use an attempt without adding a passive. Filling also stops if eligible candidates
run out. PalCalc doesn't model those failures; their frequency with actual game
data hasn't been established.

This matters especially for Special Cakes: the game targets four passives, but
with fewer than four parental candidates it relies on random filling. PalCalc
models a four-passive result. See [passive selection and its limits](PALWORLD-BREEDING-MECHANICS.md#passive-skills)
for the detailed explanation and evidence.

### Desired traits supplied by fresh random values

The passive estimate counts desired passives inherited from the parents, ignoring
the chance that random additions supply them. Likewise, the IV estimate counts
qualifying parent-sourced values, ignoring fresh random values that happen to meet
the target. These omissions can miss successful outcomes and overestimate effort.

See the [passive calculation](PalCalc.Solver/README-BREED-ESTIMATE.md) and
[IV calculation](PalCalc.Solver/README.md#iv-probabilities).

### Unknown or irrelevant passive identities

Bred candidates can represent irrelevant passives with placeholders. These keep
the passive slot counts, but lose named overlap with another parent's irrelevant
passives. If both parents would really share one of those passives, the combined
pool can be smaller than the solver estimates, improving inheritance odds. Losing
that overlap can therefore overestimate later breeding effort.

See [candidate generation](PalCalc.Solver/Processing/CandidateExpander.cs) and
[the solver's passive representations](PalCalc.Solver/README.md#passives).

### Production, capture, and incubation time

Time estimates use configurable models, not a full simulation of farm activity or
the player's routine. In particular, egg-production time includes an assumed
allowance for parent availability. Incubation and capture effort also depend on
the configured model and settings.

See [egg-production time](PalCalc.Solver/README.md#egg-production-time)
and [effort estimation](PalCalc.Solver/README.md#estimating-the-effort-of-a-path).

## Search tradeoffs

### Cakes are considered for attack inheritance

We only introduce Special Cake outcomes when needed to inherit multiple requested
attacks that the child doesn't already learn at level one. Searching for cakes
solely to improve passive inheritance could find additional valid or better paths,
but we intentionally skip those options for performance.

The ordinary passive-variant limit, `MaxBredIrrelevantPassives`, also reduces the
options explored. It isn't a hard restriction on final passive counts: mandatory
cake fillers can exceed it. Those fillers still affect later inheritance.
See [Special Cake planning](PalCalc.Solver/README-BREED-ESTIMATE.md#special-cake-override).

### Search retains a compact set of attack options and breeding paths

During search, each exact requested-attack combination keeps its minimum estimated
cake cost rather than every effort/cake alternative. Other candidate comparisons
also retain a limited set of useful paths. This saves substantial work, but a
faster path that needs more cakes can be lost before finalization.

Finalization compares effort/cake tradeoffs within the surviving trees. It can't
recover discarded candidates, and result simplification can further reduce similar
paths. See [attack-path comparisons](PalCalc.Solver/README.md#comparing-attack-paths)
and [finalization](PalCalc.Solver/README.md#8-apply-surgery-and-return-the-results).

### Surgery is deferred, and configured limits bound the search

Surgery is applied after breeding search, rather than at every intermediate step.
That avoids multiplying the search space, but can miss useful intermediate surgery
combinations. Breeding-step, iteration, effort, item, input, and other configured
limits can also exclude paths. A solve with no results doesn't establish that the
requested Pal is impossible to obtain in the game.

See [search coverage](PalCalc.Solver/README.md#search-coverage-and-limits)
and [finishing results](PalCalc.Solver/README.md#finishing-the-results).

## Unverified and unmodeled behavior

An unverified mechanic isn't necessarily implemented incorrectly. It means the
published evidence hasn't established the full process. Conversely, some recovered
behavior is intentionally omitted from the estimate.

The [research checklist](docres/research/palworld-breeding/README.md#current-verification-status)
records game-version verification and gaps, including child gender and wild-Pal
generation. The inheritance estimates described there omit mutation and talent-bonus
branches. The [species comparison](PalCalc.Solver/README.md#species-database)
also records data conditions still needing checks before claiming that PalCalc's
generated graph matches every live pairing.

Executable logic and asset values need separate checks. An assessment of an older
build doesn't establish behavior after an update, even if familiar field names or
example values remain unchanged. For reassessment, use the
[research hub](docres/research/palworld-breeding/README.md#check-a-game-update).

## Interpreting results

Effort is an average estimate, not a promised completion time. A child can arrive
on the first attempt or take much longer. The results are useful alternatives from
the retained search space, not an exhaustive list or a guarantee of the fastest
possible real-world plan.

Saved estimates may need a fresh solve after mechanics or settings change. A saved
tree records a previous result; it doesn't itself establish that its estimate uses
the current model.
