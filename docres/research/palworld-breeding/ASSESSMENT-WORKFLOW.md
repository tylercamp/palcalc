# Assessing and updating Palworld mechanics

Start with the [mechanics checklist and current status](README.md#current-verification-status).
This workflow defines what a review establishes and publishes. Use the
[REA guide](REA-GUIDE.md) for technical investigation and the
[template](ASSESSMENT-TEMPLATE.md) for the version report. The
[review skill](palworld-breeding-review/SKILL.md) compares completed evidence with
PalCalc; it doesn't start another investigation.

## Scope and version identity

A baseline or standard game-update review accounts for all eight required
mechanics in the checklist. A targeted task can investigate one mechanic without
rechecking the others, but doesn't count as a complete version review.

Record the **Palworld game version** first, with its source, platform, review date,
and installed configuration. Also record the executable SHA-256, Steam build ID
where applicable, and the identities of relevant assets and decoding inputs.
These identify the actual files checked; they don't replace the game version.
Don't infer a release label from an old report or treat Steam's build ID as the
game's version number.

If the game version can't be established, record it as unknown and use
`assessments/unknown-version-<platform>-<build-or-hash>/` provisionally. Continue
useful investigation, but leave the game-version checklist pending. Once the
version is confirmed, move the report to its version folder and repair links.

Name the previous version report, mechanics-document revision, and code revision
or working tree being compared. Separately record the source of numeric inputs;
understanding executable logic doesn't establish current asset values.

For every mechanic, record ordinary, item, mutation, spawn, capture, or other
relevant modes and the final observable boundary reached. An excluded mode is a
visible limitation, not implicit coverage. Supporting mechanics such as map
conversion and breeding time are optional unless requested or implicated by a
change or reported problem.

## 1. Recover game behavior

Use a fresh `_REA-ANALYSIS/runs/<timestamp>-<id>/` under the repository root, or
resume the same run after interruption. Keep raw responses, traces, setup logs,
experiments, and operational notes there; the directory is ignored by Git.

Follow the [REA guide](REA-GUIDE.md), establishing behavior before consulting
PalCalc's probability assumptions. GenDB readers provide names and input leads,
not proof of native eligibility or branch semantics. Trace inputs, decisions,
helpers, and final observable writes. Record missing proof rather than guessing.
For a later version, use the dependency comparison below to avoid repeating work
already justified as unchanged.

## 2. Reconstruct the game model

Write a section per mechanic in `evidence.md`: full readable pseudocode, input
meanings/types/units, modes, and decisive saved excerpts. Include branches PalCalc
intentionally omits. Full means complete within the stated scope, not every
Palworld system. Partial recovery stays labelled partial.

Separate native observations, arithmetic reconstruction, synthetic examples, and
live game observations. Keep external data symbolic unless verified or explicitly
assumed. Each material claim needs its target, procedure/address or semantic
anchor, relevant branch, and enough surrounding context to check the reasoning.
Raw decompiler output alone isn't an effective model.

Record the code and data dependencies needed to check this mechanic next time.
Include the shared helpers, caller branches, relevant defaults/overrides, input
tables, and later writes that could change the conclusion. Old addresses are
breadcrumbs, not a dependency comparison.

A symbolic algorithm doesn't verify current item values, species eligibility, or
selection weights. Check the inputs needed to establish the claimed rule or
probability; leave that part of the mechanic partial when its data isn't verified.

## 3. State the conclusions and derive the PalCalc model

In each mechanic's `assessment.md` section, state the game rule, covered modes,
remaining unknowns, and links to the decisive evidence. This is the version's
conclusion even if PalCalc doesn't implement the rule.

Then give the reduced model proposed for PalCalc, separately from both the full
game model and the current code. Explain what is retained, omitted, approximated,
or unresolved and why. Keep dependencies that affect possible paths, desired
contents, probability, or expected attempts. Ignoring a random passive's identity
doesn't mean failed selections or achieved counts can also be ignored.

For each simplification, state when it gives the same result or label it an
approximation. Don't assume a detail is harmless because PalCalc currently omits it.

## 4. Compare with the recorded baselines

Compare game behavior with the previous version's report, where one exists, and
the reduced PalCalc model with the named document/code baseline. These are
different comparisons: a newly discovered error in our model isn't proof that
the game changed.

Use the [review skill](palworld-breeding-review/SKILL.md) and its
[notes](palworld-breeding-review/references/review-notes.md). Assess agreements,
discrepancies, deliberate approximations, and unresolved facts. A documentation
discrepancy doesn't prove the code has the same discrepancy; inspect relevant
implementation when requested or needed to settle that distinction.

For species, compare recipes and genders, candidate eligibility, self-pairs,
ties/table order, item inputs, and final replacements as well as rank arithmetic.
Don't claim affected live pairings without establishing the current data
conditions. Apply the same discipline to other mechanics, distinguishing
initialization, spawning, capture, and final persisted contents.

## 5. Publish and update the references

Keep one report per game version, with stable sections for every required
mechanic in both files. Use the [template](ASSESSMENT-TEMPLATE.md).

```text
docres/research/palworld-breeding/assessments/<game-version>/
  assessment.md   conclusions, version changes, PalCalc comparison, checks
  evidence.md     game pseudocode, input provenance, selected supporting evidence
```

Targeted work updates that version's relevant sections, preserving other findings
and their review dates. On the first targeted review of a version, include the
other checklist rows as not checked. For different platform/build variants within
one release, use explicitly scoped sections or a folder suffix when conclusions
would otherwise be ambiguous. Don't silently combine different executable or
asset identities into one verified target.

Only `assessment.md` and `evidence.md` are published from each assessment folder;
other generated files are ignored. Both files must stand without the original
chat, native session, or ignored run directory. Embed small necessary numeric
snapshots and retain semantic discovery anchors alongside decisive excerpts.
Reusable helpers belong in `tools/` with a meaningful check; run-specific scripts
stay in the ignored directory. Don't publish game binaries or toolchains.

Update the hub's mechanic rows and [changelog](CHANGELOG.md), recording which
mechanics changed and whether this is a game change, newly verified behavior,
confirmed unchanged behavior, or correction to our understanding. A failed or
partial check belongs in the report too. Keep an older last-verified version
visible when the new version isn't established.

When reference updates are included in the task, review the
[player explanation](../../../PALWORLD-BREEDING-MECHANICS.md) and
[limitations](../../../LIMITATIONS.md) against accepted findings. Keep solver
details in the solver docs, link summaries back to conclusions/evidence, and
don't label an implementation difference fixed merely because docs changed.
See [document responsibilities](README.md#where-information-lives).

## 6. Implement only when requested

An assessment doesn't itself authorize solver or database changes. If requested,
inspect the shared calculation and callers using repository CodeGraph instructions.
Add focused checks distinguishing old and recovered behavior, including affected
combinations of species, passives, attacks, IVs, gender, and cake limits. Record
the implementation revision and tests actually run.

Preserve saved breeding plans, selected results, checked-node progress, targets,
and settings. Explain which older estimates may need a fresh solve; don't clear
player data to force recalculation. Review the
[solver overview](../../../PalCalc.Solver/README.md),
[worked calculations](../../../PalCalc.Solver/README-BREED-ESTIMATE.md), and
[limitations](../../../LIMITATIONS.md) when the implementation changes.

## Reviewing a game update

Create the new version report from the checklist, using the previous report as
the comparison baseline. Each required mechanic needs a disposition:

- **Verified:** recovered and checked for this version, with conclusions and evidence.
- **Verified unchanged:** the previous rule still applies, supported by a recorded
  comparison of all relevant code and data dependencies.
- **Partial:** some questions or modes are established, with the remaining gaps named.
- **Unresolved:** the available evidence doesn't establish the rule for this version.

For **verified unchanged**, identify the earlier pseudocode/conclusions and record
what was compared: code bodies and branches, shared helpers/callers/final writes,
relevant tables, defaults, and item inputs. Explain why the comparison covers the
mechanic. Matching an entire dependency's hash can avoid reanalysis; a changed
hash requires determining whether the change affects this mechanic. A matching
top-level function alone doesn't establish unchanged callees or assets. Different
addresses don't establish different behavior. Retain enough comparison evidence
to reproduce the conclusion without ignored caches.

An update review is complete only when every required mechanic is verified or
verified unchanged for the declared platform/configuration and its relevant modes.
Partial or unresolved mechanics keep the overall review incomplete. List exclusions
prominently; a review limited to ordinary breeding mustn't appear as full coverage
of item or alternate behavior. Supporting mechanics aren't part of this gate.

The first comprehensive baseline has no previous complete report to inherit:
establish its rules and evidence directly. Old hypotheses and maintained summaries
can guide discovery, but aren't substitutes for the missing proof.

Preserve previous version reports. Amend a published conclusion only with a dated
correction explaining what changed in our understanding; record it in the changelog
without inventing a historical game change.
