# Assessing and updating Palworld mechanics

Start with the [research introduction](README.md). This workflow owns the stages,
outputs, and publication rules. Use the [REA guide](REA-GUIDE.md) for extraction
methods and the [review skill](palworld-breeding-review/SKILL.md) for comparison of
completed evidence. The [template](ASSESSMENT-TEMPLATE.md) provides report outlines.

## Scope and inputs

Record the requested questions and modes, platform, executable SHA-256, assessment
date, and game build/version where known. Name the documented algorithm and code
revision or working tree being compared. Record data provenance separately for
numeric examples; static code recovery does not prove current asset values.

A standard breeding review considers all questions in the
[requirements table](README.md#questions-palcalc-needs-answered): child species,
gender, passives, attacks, and IVs; wild-Pal passives, attacks, and gender. Explicitly
bound ordinary/item/mutation modes and distinguish spawning from capture.
Honor a narrower requested scope and mark omissions rather than implying coverage.

Consider [supporting calculations](../palworld-misc/README.md) at scope selection:
egg-production time and world-map conversion. Record reviewed, carried forward
(with the evidence or reason), or not assessed. Investigate them when changes,
reported symptoms, or the requested scope warrant it; they are not mandatory
native investigations in every breeding review.

## 1. Recover game behavior

Use a fresh `_REA-ANALYSIS/runs/<timestamp>-<id>/` under the repository root, or
resume the same run after interruption. Keep native responses, traces, setup logs,
experiments, and operational notes there. The directory is ignored by Git.

Follow the [REA guide](REA-GUIDE.md), establishing behavior before consulting
PalCalc's probability/calculation assumptions. GenDB readers provide names and
input leads, not proof of native eligibility or branch semantics. Trace the scoped
inputs, decisions, helpers, and final observable writes; report missing proof.

## 2. Reconstruct the full scoped model

Write `evidence.md`: understandable pseudocode for the recovered game process,
input meanings/types/units, relevant modes, and decisive saved excerpts. Include
branches PalCalc intentionally omits. Full means complete within the stated scope,
not a claim to have reverse-engineered every Palworld system.

Separate native observations, arithmetic reconstruction, synthetic examples, and
runtime unknowns. Leave external data symbolic unless extracted or explicitly
assumed; retain the source of any numeric example. Selected evidence must identify
its target, procedure/address or branch, and enough context to interpret the claim.
Raw decompiler output alone is not an effective model.

## 3. Derive the PalCalc model

In `assessment.md`, write the reduced pseudocode and its observable outcomes.
Explain what is retained, omitted, approximated, or unresolved and why it matters
to PalCalc. Retain dependencies that affect possible paths, desired contents,
probability, or expected attempts. An ignored selection identity does not imply
that failed selections or achieved counts are irrelevant.

Keep this proposed model separate from both the recovered game model and the
current implementation. A simplification needs a stated equivalence condition or
an explicit approximation, not an assumption that native details are harmless.

## 4. Compare with the reviewed baseline

Compare the proposed model with the named revision of
[PALWORLD-BREEDING-MECHANICS.md](../../../PALWORLD-BREEDING-MECHANICS.md).
Use the [review skill](palworld-breeding-review/SKILL.md) and its
[notes](palworld-breeding-review/references/review-notes.md).
Assess agreements, discrepancies, deliberate approximations, and unresolved facts.
A documentation discrepancy does not prove the code has the same discrepancy;
inspect relevant implementation when requested or needed to settle that distinction.

For species, compare data dependencies as well as rank arithmetic: recipes and
parent genders, candidate eligibility, self-pairs, tie/table order, item inputs,
and final-ID replacements. Do not claim affected live pairings without establishing
the relevant current data conditions. Apply the same evidence discipline to other
mechanics, including gender, spawning, and capture effects.

## 5. Publish the assessment and maintain the reference

Publish the two files below, using the [template](ASSESSMENT-TEMPLATE.md). Each
material comparison links to a heading in `evidence.md`; both files must stand
without the original chat or ignored run directory. Keep semantic anchors for the
next review alongside decisive excerpts, not just addresses from this build.

When reference updates are included in the task, incorporate accepted findings in
the [maintained mechanics document](../../../PALWORLD-BREEDING-MECHANICS.md).
Keep recovered behavior, PalCalc's model, and remaining implementation differences
explicit. Do not label a difference fixed merely because the documentation changed.
Update its per-mechanic coverage and the [assessment index](README.md#published-assessments).

### Deliverables and Git

```text
docres/research/palworld-breeding/assessments/<date>-build-<id>/
  evidence.md    full scoped game model, inputs, selected evidence, limits
  assessment.md  reduced PalCalc model, baseline comparison, conclusions, checks
```

Use a topic suffix if two scoped reports would otherwise collide. Additional-logic
reports can use the same layout under `docres/research/palworld-misc/assessments/`.
Only `assessment.md` and `evidence.md` are published from each assessment folder;
other generated files are ignored. Small necessary numeric snapshots can be embedded
in `evidence.md`. No game binaries, toolchains, full query history, or operating
handoff is required in the published report. Reusable helpers belong in `tools/`
with a meaningful check; run-specific scripts stay in the ignored run directory.

## 6. Implement only when requested

An assessment does not itself authorize solver or database changes. If requested,
inspect the shared calculation and its callers using repository CodeGraph
instructions. Add focused checks distinguishing old and recovered behavior,
including affected combinations of species, passives, attacks, IVs, gender, and
cake limits. Record the implementation revision and tests actually run.

Preserve saved breeding plans, selected results, checked-node progress, targets,
and settings. Explain older estimates that may need a fresh solve; do not clear
player data to force recalculation. Keep implementation explanations in the
[solver overview](../../../PalCalc.Solver/README.md) and worked calculations in
[the probability example](../../../PalCalc.Solver/README-BREED-ESTIMATE.md).

## Reviewing a later game build

Recheck the executable identity and relevant asset dependencies. Relocate code
using field names, diagnostics, and call relationships, rather than old addresses.
Record changed, equivalent, carried-forward, or unresolved behavior per mechanic.
An unchanged executable does not prove unchanged data, and relocated code does not
prove a behavior change. Carry forward conclusions only with their evidence and
scope limitations. Update the current coverage without overwriting the historical
build evidence or silently widening an older assessment's claims.
