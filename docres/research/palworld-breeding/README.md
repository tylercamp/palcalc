# Palworld mechanics research

This is the record of which mechanics we've checked, against which game versions,
and where to find the conclusions and evidence. For a player-facing explanation
of the rules, start with [Palworld Breeding Mechanics](../../../PALWORLD-BREEDING-MECHANICS.md).

We use [Reverse Engineer Anything (REA)](https://github.com/morluto/rea) to recover
the game's logic. [GenDB](../../../PalCalc.GenDB/README.md) supplies game-data
values such as species ranks, recipes, and skill weights. We need both: finding
the expected values doesn't prove how the game uses them, and recovering the
selection code doesn't establish the values in the current assets.

## Current verification status

**Last complete game-version review: none published.**

**Last mechanics verification update: none under this checklist.**

We're starting a comprehensive baseline review rather than carrying forward the
old, separate investigations as completed coverage. The existing mechanics
explanation and solver calculations remain available, but haven't yet been
revalidated through this checklist. The earlier reports were removed during this
documentation reset; they're available in Git history.

The table below is the main coverage record. Once reports are published, each row
links directly to that mechanic's conclusions and supporting evidence. A partial
review can update individual rows without making the whole version complete.

| Required mechanic | What needs to be established | Last checked game version / date | Status | Conclusions / evidence |
| --- | --- | --- | --- | --- |
| Child species | Recipes and parent genders, rank selection, eligibility, ties, item effects, and later replacements | — | Awaiting baseline review | — |
| Child gender | Ratios, fixed genders, overrides, and final assignment | — | Awaiting baseline review | — |
| Child passives | Parental selection, random additions, duplicates, failed additions, and item effects | — | Awaiting baseline review | — |
| Child attacks | Equipped-parent inheritance, natural and mastered attacks, item effects, and final learned/equipped contents | — | Awaiting baseline review | — |
| Child IVs | Parent and random sources, asymmetric branches, ranges, and item effects | — | Awaiting baseline review | — |
| Wild-Pal passives | Spawn-time contents, guaranteed passives, and capture-time changes | — | Awaiting baseline review | — |
| Wild-Pal attacks | Spawn-time contents, level-based learning, and capture-time changes | — | Awaiting baseline review | — |
| Wild-Pal gender | Spawn-time selection, ratios, fixed genders, and overrides | — | Awaiting baseline review | — |

A standard game-update review checks **all eight mechanics**, including their
relevant ordinary and item modes. Record alternate modes such as mutation
explicitly: trace their effects or explain which conclusions don't cover them.
An excluded mode stays visible as a gap; it doesn't become verified just because
PalCalc currently ignores it. See the [workflow's completion rules](ASSESSMENT-WORKFLOW.md#reviewing-a-game-update).

Use **verified**, **verified unchanged**, **partial**, or **unresolved** for
published findings. The last-checked version/date advances only for the scope
actually supported by evidence; a later unsuccessful check is recorded separately
in that version's report. An unresolved new version doesn't erase an older
verified finding or make it applicable to the new version.

[The changelog](CHANGELOG.md) records completed and partial version reviews, what
changed, and corrections to our understanding. Game version is the primary
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
older check dates don't make that review incomplete.

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

The published report must stand without the original chat or ignored working
files. An independent executable inspection follows the investigation workflow;
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
Use the previous version's report as a baseline and account for every required
mechanic. An unchanged dependency can justify **verified unchanged** without
reconstructing it from scratch, provided the report records what was compared and
why that covers the mechanic. An unchanged executable alone doesn't prove unchanged
assets; relocated code alone doesn't prove a behavior change.

Publish a report for the new game version and update the status table and changelog.
Keep the previous version's conclusions intact. A partial review is useful, but
mustn't be presented as complete verification of the update.

## Published reports

No comprehensive baseline report has been published yet. See the
[changelog](CHANGELOG.md) for review history from this reset onward.

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
| [Changelog](CHANGELOG.md) | Review dates, versions, affected mechanics, and the kind of change |
| `assessments/<game-version>/assessment.md` | Authoritative conclusions for that version and comparison with a named PalCalc baseline |
| `assessments/<game-version>/evidence.md` | Full game model and pseudocode, input provenance, decisive observations, and proof boundaries |
| [Mechanics explanation](../../../PALWORLD-BREEDING-MECHANICS.md) | Player-facing game rules, derived from the research, with links back to status and detail |
| [Limitations](../../../LIMITATIONS.md) | Current modeling omissions, search tradeoffs, and their practical effects |
| [Solver overview](../../../PalCalc.Solver/README.md) and [worked calculations](../../../PalCalc.Solver/README-BREED-ESTIMATE.md) | Current implementation and its estimates |
| [Workflow](ASSESSMENT-WORKFLOW.md), [template](ASSESSMENT-TEMPLATE.md), [REA guide](REA-GUIDE.md) | Review procedure, report structure, and technical investigation methods |
| [Historical discovery references](HISTORICAL-DISCOVERY-REFERENCES.md) | Build-specific search anchors, layout clues, and constants for exploration; not mechanics conclusions or verification coverage |
| [Review skill](palworld-breeding-review/SKILL.md) | Comparing completed findings with PalCalc |
| `tools/` | Reusable helpers and tests; see the [PE helper instructions](REA-GUIDE.md#bundled-offline-pe-helper) |
| `_REA-ANALYSIS/runs/<timestamp>-<id>/` at the repo root | Ignored raw responses, experiments, exports, logs, and working notes |

## When updating these docs

- Accepted findings update the version report, status table, and changelog. Review
  the player explanation and relevant limitations as derived summaries; link each
  summary to the detailed finding rather than copying the full report.
- Solver changes prompt a review of the solver explanation, worked calculations,
  and limitations. They don't establish new game verification or rewrite an older
  report's comparison as though it reviewed today's code.
- A correction to our understanding updates the affected report with a dated
  correction note and changelog entry. Preserve what was previously concluded and
  why it changed; don't describe the correction as a game update without evidence.
- Short explanations may repeat information for their audience. Keep one owner
  for the detailed rule or procedure and review its linked summaries when it changes.
  The player page and limitations page link here rather than maintaining separate
  coverage tables.
