# Researching Palworld breeding mechanics

PalCalc needs a reliable understanding of the game rules behind breeding paths and
their success probabilities. An AI agent can use
[Reverse Engineer Anything (REA)](https://github.com/morluto/rea) to investigate a
specific Palworld executable and compare its behavior with PalCalc.

For the current rules, approximations, and assessment coverage, read
[PALWORLD-BREEDING-MECHANICS.md](../../../PALWORLD-BREEDING-MECHANICS.md).
This folder explains how to investigate, review, and maintain that understanding.

## Data and logic

[GenDB](../../../PalCalc.GenDB/README.md) tells us what values are stored in the
game files, such as species stats, breeding ranks, recipes, skill weights, and
icons. Reverse engineering tells us how the game uses those values: what it
selects, filters, calculates, and saves.

We need both. Finding the expected weights doesn't prove how they're selected,
and understanding the selection code doesn't prove which values the current
assets contain. We also need to check that PalCalc considers the same eligible
Pals and recipes as the game.

## Questions PalCalc needs answered

| Area | Questions |
| --- | --- |
| Child species | Which recipe or rank rule chooses the child, including genders, eligibility, ties, and later replacements? |
| Child gender | How is the child's gender selected, and which data or overrides influence it? |
| Child passives | How are parental and random passives selected, including item overrides and failed additions? |
| Child attacks | How are equipped, inherited, natural, and mastered attacks assembled? |
| Child IVs | How are parental and fresh values selected, including asymmetric branches and relevant overrides? |
| Wild-Pal passives | What is assigned at spawn, what is guaranteed, and what can change on capture? |
| Wild-Pal attacks | What is assigned at spawn, learned with level, or changed on capture? |
| Wild-Pal gender | How is gender selected when a wild Pal is spawned? |

These are the questions a complete review needs to cover; they haven't all been
answered yet. The [coverage table](../../../PALWORLD-BREEDING-MECHANICS.md#assessment-coverage)
shows the published findings and remaining gaps. A smaller review can focus on
selected questions, but should say which modes it covers: ordinary breeding,
items, mutation, spawning, or capture.

## From game behavior to PalCalc

```mermaid
flowchart LR
    A[Recover game behavior] --> B[Reconstruct full scoped pseudocode]
    B --> C[Derive the PalCalc model]
    C --> D[Compare with docs and relevant code]
    D --> E[Update the maintained reference]
    E --> F[Implement and test when requested]
```

The full model describes the part of the game being investigated, including
details PalCalc doesn't model. The reduced model describes the rules we use for
PalCalc and explains what we leave out. For example, we might ignore which random
passive is selected while still accounting for how many are added. If selections
can fail, ignoring those failures is an approximation that needs to be stated.

The [workflow](ASSESSMENT-WORKFLOW.md) explains what to produce at each stage.
The [REA guide](REA-GUIDE.md) explains how to investigate the executable.
The [review skill](palworld-breeding-review/SKILL.md) compares findings from a
completed investigation. It doesn't start another investigation or authorize
implementation changes.

## Run an assessment yourself

You'll need the game files, a writable checkout, and an AI environment with REA's
native-analysis tools and a compatible Ghidra/Java installation. The
[setup instructions](REA-GUIDE.md#ghidra-prerequisite-gate) explain how to check
those tools and reuse an existing installation with the required authorization.
GenDB and mapping prerequisites are only needed if the investigation requires
asset extraction.

1. Make the game files and checkout available, then check the required tools
   using the setup instructions above.
2. Give the agent the executable or installed-game path, desired scope, and the
   mechanics document/code revision to compare. Follow the
   [assessment workflow](ASSESSMENT-WORKFLOW.md). Unknown version metadata should
   be recorded as unknown, not inferred from an older report.
3. Review `evidence.md` and `assessment.md`. They should be understandable without
   the original chat, native session, or ignored working directory.

For example, from a checkout with the required tools available:

```text
Follow docres/research/palworld-breeding/ASSESSMENT-WORKFLOW.md to assess the
installed Palworld build. Examine child species, gender, passives, attacks,
and IVs, plus wild-Pal initialization and capture effects. Compare with
PALWORLD-BREEDING-MECHANICS.md at the current revision. Report any scope gaps.
Publish the evidence and assessment; do not implement changes in this run.
```

To review existing findings without starting REA, supply the relevant assessment
folder to [palworld-breeding-review](palworld-breeding-review/SKILL.md). Use this
repository copy of the skill and its notes; an older installed copy may have a
narrower scope.

## Where information lives

| Location | Purpose |
| --- | --- |
| [Current mechanics](../../../PALWORLD-BREEDING-MECHANICS.md) | Maintained rules, PalCalc approximations, coverage and supporting calculations |
| [Workflow](ASSESSMENT-WORKFLOW.md) | Investigation, reconstruction, comparison, publication, and update process |
| [REA guide](REA-GUIDE.md) | Setup, tracing checklists, technical methods and troubleshooting |
| [Report template](ASSESSMENT-TEMPLATE.md) | Outlines for the two published assessment files |
| `assessments/<date>-build-<id>/evidence.md` | Recovered scoped game model and selected supporting evidence |
| `assessments/<date>-build-<id>/assessment.md` | Model used for the PalCalc comparison, reviewed baseline, consequences, and checks |
| `tools/` | Reusable helpers and their tests; see the [PE helper instructions](REA-GUIDE.md#bundled-offline-pe-helper) |
| `_REA-ANALYSIS/runs/<timestamp>-<id>/` at the repo root | Ignored raw responses, experiments, exports, logs and working notes |

## Additional game logic

REA can investigate a scoped game calculation outside inheritance using the same
process. [Additional game logic](../palworld-misc/README.md) documents world-map
coordinate conversion and links the supporting
[egg-production-time model](../../../PALWORLD-BREEDING-MECHANICS.md#egg-production-time).
These topics receive targeted reviews when relevant. A standard breeding review
records whether each needs work, is carried forward, or was not assessed; it does
not require both investigations on every run.

## Published assessments

| Date | Build | Scope |
| --- | --- | --- |
| 2026-10-18 | 25246127 | [Offspring species selection and comparison](assessments/2026-10-18-build-25246127/assessment.md) |
| 2026-10-16 | 25246127 | [IVs, passives, and attack inheritance](assessments/2026-10-16-build-25246127/assessment.md) |

For another build, start from the workflow and semantic discovery anchors. Earlier
addresses and findings are leads, not proof of unchanged behavior.
