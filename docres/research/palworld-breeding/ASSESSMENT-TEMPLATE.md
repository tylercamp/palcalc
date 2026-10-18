# Assessment: <date>, <game build>, <topic>

Scope: <questions and modes; platform, executable SHA-256, build/version if known>.
Compared with: <mechanics document and code revision or working tree>.
Data inputs: <verified source or explicit assumptions behind numeric examples>.
Coverage: <reviewed, carried forward, or not assessed for each requested mechanic>.
Supporting logic: <egg-production time and map conversion disposition, if relevant>.

## Recovered game behavior

Summarize the full scoped process and link its pseudocode, input contracts, and
decisive native evidence in `evidence.md`. Distinguish native observations from
synthetic reconstruction and game observations. State untraced final writes,
excluded modes, and any missing proof.

## PalCalc-relevant model

Give understandable reduced pseudocode. Explain which game details are retained,
omitted, approximated, or unresolved and why. Identify conditions needed for a
simplification to preserve possible paths, desired outcomes, probabilities, and
expected attempts. Do not treat unknowns as proven equivalences.

## Functional comparison

| Mechanic | Reviewed baseline | Recovered/reduced model | Consequence and status | Evidence |
| --- | --- | --- | --- | --- |
| <topic> | <document/code reference> | <agreement or difference> | <effect, approximation, or unresolved condition> | <evidence.md heading> |

Separate documentation discrepancies from demonstrated implementation differences,
and corrected older assumptions from differences still remaining. For species,
cover recipes/genders, roster, rounding/item inputs, self-pairs, ties/table order,
and final child-ID replacements. For wild Pals distinguish spawn, learning, capture,
and persisted contents; parent-gender recipe checks are not child-gender generation.

## Breadcrumbs and limits

Record fields, diagnostics, short call paths, and build-specific addresses needed
to find the logic again. Link the evidence for each material conclusion. Identify
uncertainties and the next useful check; keep setup logs and session history local.

## Changes and checks

Record documentation updates, and implementation/code tests only if requested.
List checks actually performed and their results. Label native inspection,
synthetic checks, and live game observations separately. Preserve saved plans and
progress; report how older estimates are handled without deleting player data.

---

# Companion evidence.md outline

- **Identity and scope:** game build/platform, executable hash, input provenance,
  covered modes, observation/reuse dates, and explicit boundaries.
- **Full scoped pseudocode:** named inputs, units/types, branch conditions,
  ordering/filtering, overrides, failure behavior, and final observable writes.
- **Selected evidence:** decisive excerpts with procedure/address or semantic
  anchors and enough context to support the pseudocode and comparison.
- **Examples and checks:** distinguishing inputs, symbolic versus verified data,
  expected outcomes and the checks actually run.
- **Limits:** unverified assets/defaults, omitted modes, unresolved finalization,
  and anything that could change the conclusion.

Use a separate heading per mechanic when the scope spans several. The full game
model belongs here; its reduced PalCalc interpretation belongs in `assessment.md`.
