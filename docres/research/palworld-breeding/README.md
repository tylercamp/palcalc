# Palworld mechanics research

This is the record of which mechanics we've checked, against which game versions,
and where to find the conclusions and evidence. For a player-facing explanation
of the rules, start with [Palworld Breeding Mechanics](../../../PALWORLD-BREEDING-MECHANICS.md).

We use [Reverse Engineer Anything (REA)](https://github.com/morluto/rea) to recover
the game's logic. [GenDB](../../../PalCalc.GenDB/README.md) supplies game-data
values such as species ranks, recipes, and skill weights. Both are needed for a
sufficient view of in-game behavior.

## Current verification status

**Last fully verified game version: 1.0.5.102999, 2026-10-29.**

**Latest assessment: eight Verified. Investigation state: Finished.** This
establishes the first verified baseline for the shipped Windows Steam configuration.

The [assessment](assessments/1.0.5.102999/assessment.md) gives current rules,
conditions and the PalCalc model comparison; [evidence](assessments/1.0.5.102999/evidence.md)
contains the scoped game model, current inputs and decisive observations.
Runtime source membership/order, world state and options retain their explicit
input contracts; static verification does not establish measured gameplay
frequencies or a universal capture-addition guarantee. The assessment records
current findings and limits; the [changelog](CHANGELOG.md) tracks checked
versions, observed game changes and assessment scope.

The table below is the main coverage record, linking each mechanic to its
conclusions and supporting evidence. A targeted review can update individual
rows without establishing full version verification.

| Required mechanic | Questions to answer for declared inputs and modes | Last checked game version / date | Status | Conclusions / evidence |
| --- | --- | --- | --- | --- |
| Child species | Which child ID results from the given parents, recipe/rank inputs and mode, including item effects and later replacements? | 1.0.5.102999 / 2026-10-27 | Verified | [Conclusions](assessments/1.0.5.102999/assessment.md#child-species) / [Evidence](assessments/1.0.5.102999/evidence.md#child-species) |
| Child gender | How is the selected child's saved gender assigned, including ratios, fixed genders and overrides? | 1.0.5.102999 / 2026-10-22 | Verified | [Conclusions](assessments/1.0.5.102999/assessment.md#child-gender) / [Evidence](assessments/1.0.5.102999/evidence.md#child-gender) |
| Child passives | Which passives can reach the child and with what selection/count rules, including items, mutation, failures and later changes? | 1.0.5.102999 / 2026-10-27 | Verified | [Conclusions](assessments/1.0.5.102999/assessment.md#child-passives) / [Evidence](assessments/1.0.5.102999/evidence.md#child-passives) |
| Child attacks | Which parental and natural attacks are available to the child, and how do item/mutation effects and equipment limits affect them? | 1.0.5.102999 / 2026-10-22 | Verified | [Conclusions](assessments/1.0.5.102999/assessment.md#child-attacks) / [Evidence](assessments/1.0.5.102999/evidence.md#child-attacks) |
| Child IVs | Which values supply each saved IV, including source selection, random ranges, items, mutation and later changes? | 1.0.5.102999 / 2026-10-26 | Verified | [Conclusions](assessments/1.0.5.102999/assessment.md#child-ivs) / [Evidence](assessments/1.0.5.102999/evidence.md#child-ivs); [item-derived extra-egg exclusion established](assessments/1.0.5.102999/evidence.md#shipped-item-sources-do-not-add-extra-egg-records) |
| Wild-Pal passives | Which passives result from the specified spawn inputs, and how can capture effects change the saved list? | 1.0.5.102999 / 2026-10-29 | Verified | [Conclusions](assessments/1.0.5.102999/assessment.md#wild-pal-passives) / [Evidence](assessments/1.0.5.102999/evidence.md#wild-pal-passives) |
| Wild-Pal attacks | Which attacks are available overall for the specified spawn/level inputs, and how can capture change that set? | 1.0.5.102999 / 2026-10-29 | Verified | [Conclusions](assessments/1.0.5.102999/assessment.md#wild-pal-attacks) / [Evidence](assessments/1.0.5.102999/evidence.md#wild-pal-attacks) |
| Wild-Pal gender | How do the resolved species and spawn overrides determine saved gender, and does capture change it? | 1.0.5.102999 / 2026-10-24 | Verified | [Conclusions](assessments/1.0.5.102999/assessment.md#wild-pal-gender) / [Evidence](assessments/1.0.5.102999/evidence.md#wild-pal-gender) |

A standard game-update review accounts for **all of the above**. These questions
are used to identify explicit inputs, modes and output boundaries under the
[workflow's scope rules](ASSESSMENT-WORKFLOW.md#define-the-questions-and-boundaries).
Reports also record excluded modes and their effect on coverage.

The workflow defines [coverage grades and investigation state](ASSESSMENT-WORKFLOW.md#investigation-state-and-verification-coverage).
A finished investigation can have partial coverage. Within a partial mechanic,
the report explains which calculations, activation conditions, and probability
claims are established. The last-checked version/date advances only for the scope
supported by evidence; record a later unsuccessful check separately rather than
implying the older finding applies to the new version.

[The changelog](CHANGELOG.md) records changes to checked versions, observed game
mechanics and assessment scope. Game version is the primary
identifier; platform, Steam build ID, executable hash, and asset identities
distinguish the installation actually checked. If the version is unknown, say so
and keep the review provisional rather than guessing a release label.

### Supporting mechanics

| Mechanic | Current explanation | Last checked game version / date | Status |
| --- | --- | --- | --- |
| World-map coordinates | [Coordinate transforms and calibration](../palworld-misc/README.md#world-map-coordinates) | — | No published native verification |
| Egg-production time | [PalCalc's timing model](../../../PalCalc.Solver/README.md#egg-production-time) | — | No published native verification |

These get targeted reviews when a change, reported problem, or explicit request
calls for one. They aren't required for a standard game-update review, and their
older check dates don't prevent full verification of the required mechanics.

## Choose a starting point

| Task | Start here |
| --- | --- |
| Understand the game rules or see whether they're current | [Mechanics explanation](../../../PALWORLD-BREEDING-MECHANICS.md), then the [status table](#current-verification-status) |
| Verify a committed finding | [Review existing findings](#review-existing-findings) |
| Investigate a new mechanic or fill a gap | [Run an assessment yourself](#run-an-assessment-yourself) |
| Check a new game version | [Check a game update](#check-a-game-update) |
| Read detailed game pseudocode | Follow a mechanic's evidence link in the status table; [report layout](#published-reports) |
| Maintain these docs | [Where information lives](#where-information-lives) and [update dependencies](#when-updating-these-docs) |
| Perform an analysis as an agent | Read the [workflow](ASSESSMENT-WORKFLOW.md), then the relevant [REA guide](REA-GUIDE.md) sections; the [review skill](palworld-breeding-review/SKILL.md) applies after evidence recovery |

## Review existing findings

Open the mechanic's conclusions in `assessment.md`, then its full game pseudocode
and selected observations in `evidence.md`. Both belong to one game-version report.

1. Check the game version, platform, executable and asset identities, review date,
   covered modes, and compared PalCalc revision. A conclusion about ordinary
   breeding doesn't automatically cover mutation, spawning, or capture.
2. Check the reasoning against the cited evidence. The repository's
   [review skill](palworld-breeding-review/SKILL.md) and its
   [notes](palworld-breeding-review/references/review-notes.md) explain the comparison
   and distinctions worth preserving. Those notes aren't current-version proof.
3. Reproduce calculations using the recorded inputs and assumptions. Synthetic
   checks test the reconstructed calculation; PalCalc tests check its implementation.
   Neither independently verifies the recovered instructions or runs the game.

The published report must stand without the original chat, ignored working files,
or other investigation passes. References to prior reports may use only accepted
reviews, identified by version and scope. Omit rejected or intermediate review
history and local repository state such as staged, unstaged or unrelated changes. An independent executable inspection follows the investigation workflow;
reviewing completed evidence doesn't itself authorize a new native session.

## Run an assessment yourself

Use the [workflow](ASSESSMENT-WORKFLOW.md) and [report template](ASSESSMENT-TEMPLATE.md).
You'll need the game files, a writable checkout, REA's native-analysis tools, and
a compatible Ghidra/Java installation. See the [setup instructions](REA-GUIDE.md#ghidra-prerequisite-gate).
Asset-extraction prerequisites are needed only for the relevant data checks.

A targeted investigation can cover one mechanic. Add its conclusions and evidence
to the report for that game version, preserving other mechanics' coverage and
review dates. If it introduces a new mechanic, add it to the checklist and say
whether future update reviews must cover it or it belongs with supporting topics.

For example:

```text
Follow docres/research/palworld-breeding/ASSESSMENT-WORKFLOW.md to investigate
child gender in the installed Palworld version. Record the game version and
supporting executable/data identities. Update that mechanic in the version's
assessment and evidence, marking the other checklist entries as not checked by
this investigation. Compare with the current mechanics document and report gaps.
Do not implement solver changes.
```

## Check a game update

Follow [Reviewing a game update](ASSESSMENT-WORKFLOW.md#reviewing-a-game-update).
Use the previous accepted version report as a baseline and account for every required
mechanic. An unchanged dependency can justify **verified unchanged** without
reconstructing it from scratch, provided the report records what was compared and
why that covers the mechanic. An unchanged executable alone doesn't prove unchanged
assets; relocated code alone doesn't prove a behavior change.

Publish a report for the new game version and update the status table and changelog.
Keep the previous version's conclusions intact. A partial review is useful, but
mustn't be presented as full verification of the update. Before another native
pass, apply the workflow's [further-tracing rule](ASSESSMENT-WORKFLOW.md#deciding-what-to-investigate-next)
to the remaining gaps using retained evidence first.

## Published reports

- [1.0.5.102999 assessment](assessments/1.0.5.102999/assessment.md) and
  [evidence](assessments/1.0.5.102999/evidence.md), Windows Steam build 25246127,
  reviewed 2026-10-21–29. See the
  [current coverage table](#current-verification-status) and mechanic sections
  for scope and conditions.

See the [changelog](CHANGELOG.md) for review history.

```text
assessments/<game-version>/
  assessment.md   conclusions per mechanic, changes, PalCalc comparison, checks
  evidence.md     full game pseudocode, inputs, selected native/data evidence
```

Use stable headings for the eight mechanics in both files, as shown in the
[template](ASSESSMENT-TEMPLATE.md). Dates belong in the report, rather than naming
a separate report for each investigation. Platform/build variants of the same
release must have distinct, explicitly scoped sections or folders; one platform's
findings don't verify another. See the workflow for provisional unknown versions.

## Where information lives

| Location | Main responsibility |
| --- | --- |
| This README | Current mechanic checklist, verification status, and routes into conclusions/evidence |
| [Changelog](CHANGELOG.md) | Changes to checked versions, observed game mechanics and assessment scope |
| `assessments/<game-version>/assessment.md` | Authoritative conclusions for that version and comparison with a named PalCalc baseline |
| `assessments/<game-version>/evidence.md` | Consolidated scoped game model, input provenance, and decisive support for assessment conclusions and material limitations |
| [Mechanics explanation](../../../PALWORLD-BREEDING-MECHANICS.md) | Player-facing game rules, derived from the research, with links back to status and detail |
| [Limitations](../../../LIMITATIONS.md) | Current modeling omissions, search tradeoffs, and their practical effects |
| [Solver overview](../../../PalCalc.Solver/README.md) and [worked calculations](../../../PalCalc.Solver/README-BREED-ESTIMATE.md) | Current implementation and its estimates |
| [Workflow](ASSESSMENT-WORKFLOW.md) | Scope, further-tracing decisions, probability claims, completion, and publication rules |
| [Template](ASSESSMENT-TEMPLATE.md), [REA guide](REA-GUIDE.md) | Report structure and technical investigation methods, following the workflow's boundaries |
| [Historical discovery references](HISTORICAL-DISCOVERY-REFERENCES.md) | Build-specific search anchors, layout clues, and constants for exploration; not mechanics conclusions or verification coverage |
| [Review skill](palworld-breeding-review/SKILL.md) | Comparing completed findings with PalCalc |
| `tools/` | Reusable helpers and tests; see the [PE helper instructions](REA-GUIDE.md#bundled-offline-pe-helper) |
| `_REA-ANALYSIS/runs/<timestamp>-<id>/` at the repo root | Ignored scripts, raw responses, generated output, exports, logs, and snapshots |
| `_REA-ANALYSIS/txt/<investigation-id>/` at the repo root | Ignored temporary documentation: topic investigations, hypotheses, draft models, and continuation notes |

## When updating these docs

- Accepted findings update the version report and status table. Update the
  changelog when checked versions, observed game mechanics or assessment scope
  change. Review the player explanation and relevant limitations as derived
  summaries; link each summary to the detailed finding.
- Solver changes prompt a review of the solver explanation, worked calculations,
  and limitations. They don't establish new game verification or rewrite an older
  report's comparison as though it reviewed today's code.
- A correction to an accepted finding states the supported result and its evidence
  in the affected report. Link the accepted baseline when explaining a changed
  conclusion; keep intermediate or rejected reviews in private notes. Correct the
  changelog if its version, game-change or scope record is affected. Don't describe
  a changed interpretation as a game update without evidence.
- Short explanations may repeat information for their audience. Keep one owner
  for the detailed rule or procedure and review its linked summaries when it changes.
  The player page and limitations page link here rather than maintaining separate
  coverage tables.
- Consolidate continued work into current mechanic sections under the workflow's
  publication rules. Keep the hub's narrative short and link to the coverage table
  and report rather than repeating status counts or each research pass.
- Develop investigations in temporary `txt/` notes and promote established
  material under the [publication boundary](ASSESSMENT-WORKFLOW.md#what-belongs-in-published-evidence).
  Keep supporting evidence for material uncertainties public, without copying the
  entire search history into `evidence.md`.
