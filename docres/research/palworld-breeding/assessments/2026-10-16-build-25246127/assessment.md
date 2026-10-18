# Inheritance assessment: 2026-10-16, Steam build 25246127

Scope: Windows x86-64 inheritance through generated child save-parameter writes.
Executable SHA-256:
`e590b5e7bfaa3fea40fab1a02cc72c8fc5fd6f8631ef2308e95ac56c25195837`.
Release version/date were not recorded.

Comparison: the [current mechanics README](../../../../../PALWORLD-BREEDING-MECHANICS.md)
and PalCalc's `rea-mechanics` working tree at `7691b05` plus local changes,
reviewed on 2026-10-18. The discovered game behavior comes from the completed
2026-10-16 investigation.

## Discovered mechanics

- **IVs:** The middle category always includes HP and can inherit all three stats.
  Equal-sized requested subsets can therefore have different probabilities.
  See the [source table and instructions](evidence.md#iv-source-selection).
- **Passives:** Parents' passives are combined and deduplicated before selection.
  A positive override replaces both ordinary count rolls, selects up to
  `min(N,4)` parental passives, and attempts to fill the shortfall.
  See [selection and override](evidence.md#passive-selection-and-override).
- **Random filler:** Comparison uses compacted weights while subtraction uses the
  original array. This apparent bug can bias selection or consume an attempt
  without adding a passive. See the [instructions and counterexample](evidence.md#random-passive-filler).
- **Attacks:** Eligible equipped-parent attacks are deduplicated; normal inheritance
  selects one, while the all-attack item flag preserves all eligible inherited IDs.
  Initial equipment is capped separately. See [attack inheritance](evidence.md#attack-inheritance).

## PalCalc-relevant model

The [game model and selected evidence](evidence.md) retain native source selection,
count semantics, filler failures, and attack collections within the stated scope.
The reduced ordinary/item model used for this comparison is:

```text
IV success = chance the native source set contains every requested stat
             * chance each chosen parent supplies a qualifying value
passive pool = distinct passives actually present on either parent
ordinary passives = capped parental selection followed by modeled random additions
positive override = capped target selection, then modeled filling to the target
attack pool = distinct eligible equipped-parent attacks
ordinary attacks = choose one from that pool, then include natural level-one attacks
all-attack mode = retain the eligible inherited contents, with equipment handled separately
```

The projection omits accidental random IV matches and random passives supplying
desired traits. It assumes successful passive filling rather than modeling the
native weight mismatch or exhaustion. Those omissions can affect actual success
or achieved counts; they are deliberate approximations, not native guarantees.
Native talent bonuses, mutation, wild initialization/capture, and child gender
are not established by this reduced inheritance model. Numeric examples retain
the data provenance and assumptions in [the evidence](evidence.md#example-inputs-and-limits).

## Functional comparison with current PalCalc

| Mechanic | Current model versus discovered behavior | Effect on estimates |
| --- | --- | --- |
| IV source sets | Current subset table matches the recovered branches under ideal uniform draws; the older symmetric-count assumption was corrected | Requested stat identities affect success odds; accidental fresh-random matches remain excluded |
| Ordinary parental passives | Merge/deduplication and parental selection agree | Random additions supplying desired passives do not contribute to modeled success |
| Passive override | Current override replaces both count rolls correctly; filling is assumed successful | Modeled final counts can be optimistic when native filling falls short |
| Random passive filler | Native weight mismatch and pool exhaustion are deliberately omitted | Bias and failed additions are not estimated; practical frequency remains unknown |
| Attacks | Current final-inherited-content model agrees with the recovered pool/item behavior | No new inheritance-probability discrepancy was established; equipment ordering alone is not one |

Relevant implementation: [IV source sets](../../../../../PalCalc.Model/BreedingMechanics.cs),
[parental IV choices](../../../../../PalCalc.Solver/Probabilities/IVs.cs),
[passive probabilities](../../../../../PalCalc.Solver/Probabilities/Passives.cs), and
[attack probabilities](../../../../../PalCalc.Solver/Probabilities/Attacks.cs).
Search comparisons retain ordinary-mode passive effort;
[final materialization](../../../../../PalCalc.Solver/Processing/Attacks/AttackResultMaterializer.cs)
recomputes mode-specific attempts, effort, and cake use. Saved plans and player
progress are preserved; older saved estimates may require a fresh solve.

## Breadcrumbs and limits

Start from `UPalBreedingUtility::GenerateChildSaveParameter` and follow the
[entry points and named fields](evidence.md#entry-points-and-fields).
Attack IV means Shot; the Melee copy is not another logical IV. Fresh random
values can coincide with parental IVs. Requested passive counts need not be achieved.

[Example inputs and limits](evidence.md#example-inputs-and-limits) distinguish the
repository weights and Cake05 model from verified native behavior. Live item data,
RNG independence/frequencies, pool order, and exhaustive downstream alias effects
remain unverified. Recovered mutation and talent-bonus paths are outside this
solver correction.

## Checks

The implementation review on 2026-10-18 passed 13 model, 284 solver, and 61 UI tests.
[IV regressions](../../../../../PalCalc.Solver.Tests/Probabilities/IVProbabilitiesTests.cs),
[override tests](../../../../../PalCalc.Solver.Tests/Probabilities/SpecialCakePassiveProbabilitiesTests.cs),
[solver cases](../../../../../PalCalc.Solver.Tests/AttackInheritanceSolverTests.cs), and
[persistence tests](../../../../../PalCalc.UI.Tests/PersistenceTests.cs) cover the affected behavior.
Synthetic reconstruction checks passed during recovery; these checks do not
execute the game. Use the [workflow](../../ASSESSMENT-WORKFLOW.md) for a new review.
