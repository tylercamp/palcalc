---
name: palworld-breeding-review
description: "Assess PalCalc breeding paths, inheritance estimates, and scoped supporting logic against completed REA findings. Use for high-level accuracy comparisons, separating estimate-relevant discrepancies from terminology and implementation details. Does not conduct reverse engineering or implement fixes."
---

# Palworld Breeding Review

Compare the process assumed by PalCalc with the recovered game behavior. Focus on
differences that affect possible breeding paths, desired contents, probabilities,
expected attempts, or the requested supporting calculation. Use the user's scope;
omit mutation unless requested.

Read [review-notes.md](references/review-notes.md) before assessing findings. It records distinctions established in prior reviews and worked examples, not permanent claims about future game builds.

## Start from completed evidence

Resolve the repository root and the supplied assessment or REA run. Start with its report and cited evidence; confirm the build/executable identity and scope. Read detailed models or input contracts only where needed to settle a claim. Older runs may keep these under `_REA-ANALYSIS/runs/<run>/`; do not assume the newest directory is the intended one. Manifests, startup logs, and full query histories are not prerequisites for review.

Use `PALWORLD-BREEDING-MECHANICS.md` as the default documented algorithm, or the document the user supplies. The README describes PalCalc's assumed process; a discrepancy with it does not prove the implementation currently has the same discrepancy. Inspect implementation only when requested or needed to resolve that distinction, using the repository's CodeGraph instructions.

REA is already complete. Do not open a binary, launch/import Ghidra, install tooling, or rerun discovery. Resolve a decisive question from retained evidence when available. Otherwise state the missing proof and its consequence; do not turn an incomplete finding into a confirmed difference. Use [REA-GUIDE.md](../REA-GUIDE.md) only to understand an existing investigation's scope and methods, not to restart its workflow.

## Derive the scoped PalCalc model

Start from the full game pseudocode for the reviewed scope in `evidence.md`.
Write pseudocode for the proposed PalCalc model in `assessment.md`. Explain what
it leaves out, where it approximates the game, and when a simplification gives
the same result. Keep dependencies that affect possible paths, final contents,
probability, or expected effort; don't turn unresolved details into guarantees.

Cover the requested child species/gender/passive/attack/IV and wild initialization
or capture questions. For species, test recipe and gender precedence, roster,
self-pair assumptions, arithmetic/item inputs, ties/table order, and final-ID
replacements. Matching the average alone does not prove graph equivalence; name
live mismatches only when their data conditions are established. For gender and
wild Pals, distinguish recipe constraints, spawning, learning, capture, and saved
contents. Supporting logic is reviewed only when scoped, not on every run.

## Assess functional accuracy

For each candidate difference:

1. State what the documentation assumes and what the evidence establishes, with a resolvable source location for each. Consult saved instructions or pseudocode if a summarized finding cannot settle a branch, source label, ordering, or final write.
2. Ask whether it changes the final inherited contents or their probabilities under valid inputs. Exclude naming differences, unused fields and initial equipment ordering when final inheritance is the user's concern.
3. Trace the consequence far enough to distinguish a cause from its symptom. A failed selection caused by a weight-array mismatch is not automatically a separate random-failure mechanic. Do not infer developer intent from observed behavior.
4. Identify the relevant conditions: ordinary versus item override, candidate availability, count/index interpretation, eligibility, duplicate removal and requested-versus-achieved counts. Do not label an invariant violation or exhausted pool as an ordinary probability branch.
5. Distinguish confirmed differences, conditional consequences and unresolved observations. Quantify only when needed and supported. Keep weights symbolic by default; if using documented values hypothetically, state those assumptions and separate parent-sourced values from accidental numerical matches.

Do not infer a probability change merely because two routines execute in different orders. For shared RNG or native quantization effects, explain their relevance and required assumptions before including them as an estimate discrepancy. Do not claim exact frequencies or independence from static evidence.

## Deliver two separate lists

First give concise bullets of **discrepancies relevant to the scoped PalCalc behavior**. Each should identify the assumption, recovered difference and consequence. Lead with the material differences, not a function-by-function trace. Do not call the list action items or propose implementation changes unless asked.

Then give **notes for future reviews**: terminology, equivalences, scope exclusions, evidence boundaries and conditional assumptions that prevent future reviewers from rediscovering false discrepancies. Move implementation observations without a demonstrated estimate effect here. Avoid repeating the first list.

For each material conclusion, link the decisive evidence and give the procedure/address, field name, diagnostic, or short call path another agent can use for comparison. Include key unresolved checks without implying that current data tables or runtime behavior were tested. Keep setup and operational provenance out of the report. If no material difference is established, say so.

Return the assessment in chat by default; save or modify repository documents only when requested. For a published assessment, use the compact [assessment template](../ASSESSMENT-TEMPLATE.md): `evidence.md` contains the full scoped game model and selected excerpts; `assessment.md` contains the reduced PalCalc model and functional comparison, naming its document/code baseline. Cover agreements and remaining differences as well as corrected older assumptions. Keep implementation separate. Update the maintained reference only when requested, following the [workflow](../ASSESSMENT-WORKFLOW.md); preserve saved plans and progress.
