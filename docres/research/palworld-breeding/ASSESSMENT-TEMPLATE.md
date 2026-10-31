# Game-version report template

Copy the assessment outline into `assessments/<game-version>/assessment.md` and
its companion outline into `evidence.md`. Replace placeholders and instructional
text with findings. Keep all eight mechanic headings in both files, using the
same names so links remain stable. A targeted review leaves other rows explicitly
not checked, preserving any findings already published for that version.

Use the [workflow](ASSESSMENT-WORKFLOW.md) for completion and publication rules.
The outlines below organize the report; they aren't evidence that a review happened.
Define the questions before discovery, then keep these sections current on
continuation. Consolidate answers and evidence rather than appending a history
of research passes. Temporary topic notes, draft models, and continuation
checkpoints belong under `_REA-ANALYSIS/txt/<investigation-id>/`; generated output
and exports belong under `_REA-ANALYSIS/runs/<run-id>/`.

## assessment.md outline

```markdown
# Palworld <game-version>: mechanics assessment

- Game version and source: <release label, how it was established>
- Platform/configuration: <platform, distribution, relevant options/mods>
- Supporting identity: <build ID, executable SHA-256, relevant assets/mappings>
- Review dates: <first published, last updated; dates also recorded per mechanic>
- Previous game-version report: <link, or no prior baseline>
- Compared PalCalc baseline: <mechanics document and code revision/working tree>
- Investigation state: <ongoing or finished; reason for stopping if finished>
- Verification coverage: <full or partial for the declared questions/modes; gaps>

## Review summary

| Mechanic | Status | Checked date | Modes covered / gaps | Change from previous version | Conclusions | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Child species | <status> | <date> | <scope> | <classification> | [Conclusions](#child-species) | [Evidence](evidence.md#child-species) |
| Child gender | <status> | <date> | <scope> | <classification> | [Conclusions](#child-gender) | [Evidence](evidence.md#child-gender) |
| Child passives | <status> | <date> | <scope> | <classification> | [Conclusions](#child-passives) | [Evidence](evidence.md#child-passives) |
| Child attacks | <status> | <date> | <scope> | <classification> | [Conclusions](#child-attacks) | [Evidence](evidence.md#child-attacks) |
| Child IVs | <status> | <date> | <scope> | <classification> | [Conclusions](#child-ivs) | [Evidence](evidence.md#child-ivs) |
| Wild-Pal passives | <status> | <date> | <scope> | <classification> | [Conclusions](#wild-pal-passives) | [Evidence](evidence.md#wild-pal-passives) |
| Wild-Pal attacks | <status> | <date> | <scope> | <classification> | [Conclusions](#wild-pal-attacks) | [Evidence](evidence.md#wild-pal-attacks) |
| Wild-Pal gender | <status> | <date> | <scope> | <classification> | [Conclusions](#wild-pal-gender) | [Evidence](evidence.md#wild-pal-gender) |

## Child species

<Use the per-mechanic outline below.>

## Child gender

<Use the per-mechanic outline below.>

## Child passives

<Use the per-mechanic outline below.>

## Child attacks

<Use the per-mechanic outline below.>

## Child IVs

<Use the per-mechanic outline below.>

## Wild-Pal passives

<Use the per-mechanic outline below.>

## Wild-Pal attacks

<Use the per-mechanic outline below.>

## Wild-Pal gender

<Use the per-mechanic outline below.>

## Checks and reference updates

<Checks actually performed, results, documentation changes, and implementation
checks only if requested. Distinguish native inspection, synthetic calculations,
PalCalc tests, and live observations. Record a dated note for any later correction.>
```

### Per-mechanic assessment outline

For each heading, give:

- **Questions and boundaries:** inputs, modes, output being explained, evidence
  needed to connect the calculation to that output, and external conditions left
  as inputs. Follow the workflow's scope rules when new material branches appear.
- **Status, date, and scope:** verified, verified unchanged, partial, unresolved,
  or not checked. Within partial coverage, say which answers are established and
  which remain conditional; don't add a grade for every function or treat a
  finished investigation as fully verified.
- **Game rule:** a definitive answer where evidence permits, with links to the
  full pseudocode and decisive observations. Keep uncertainty explicit.
- **Version comparison:** changed game behavior, verified unchanged, newly
  verified behavior, correction to our understanding, or unresolved. If unchanged,
  link the prior conclusion and the dependency comparison proving it still applies.
- **PalCalc model:** reduced pseudocode, retained dependencies, omissions and
  approximations, and when simplifications preserve outcomes and probabilities.
  State the precision of probability claims, verified versus symbolic inputs,
  and any assumptions about draws, pool order, or activation.
- **Baseline comparison:** agreement or discrepancy with the named document/code
  revision, consequences for paths or estimates, and unresolved data conditions.
- **Remaining gaps:** which question each gap affects and its consequence for the
  answer. If further tracing is warranted, state the missing fact and stopping
  boundary under the workflow's dependency rule; otherwise explain why it remains
  a conditional input or follow-up. Don't describe a documentation correction as
  an implemented solver fix.

For species, include recipes/genders, eligibility, rounding/item inputs, self-pairs,
ties/table order, and final replacements. For wild Pals, distinguish spawn, learning,
capture, and saved contents; recipe gender checks aren't child-gender generation.
Supporting mechanics can be added after the required sections when investigated.

## Companion evidence.md outline

```markdown
# Palworld <game-version>: mechanics evidence

## Identity and methods

<Game version and source; platform/build/hash; asset and decoding-input identities;
review dates; tools/methods; original observations versus reused evidence. Record
the provenance of numeric inputs and the boundaries of static analysis.>

## Child species

<Use the per-mechanic evidence outline below.>

## Child gender

<Use the per-mechanic evidence outline below.>

## Child passives

<Use the per-mechanic evidence outline below.>

## Child attacks

<Use the per-mechanic evidence outline below.>

## Child IVs

<Use the per-mechanic evidence outline below.>

## Wild-Pal passives

<Use the per-mechanic evidence outline below.>

## Wild-Pal attacks

<Use the per-mechanic evidence outline below.>

## Wild-Pal gender

<Use the per-mechanic evidence outline below.>
```

### Per-mechanic evidence outline

Promote established material from temporary notes using the workflow's
[publication boundary](ASSESSMENT-WORKFLOW.md#what-belongs-in-published-evidence).
Each section should support a current assessment conclusion or material
limitation. Partial coverage can still have publishable established parts;
investigation history and speculative models stay in `txt/`.

- **Inputs and scope:** names, types, units, provenance, covered modes and final
  observable boundary, matching the assessment's declared questions. State not
  checked where applicable.
- **Full game pseudocode:** branches, ordering/filtering, overrides, failures,
  caps, and final writes, including behavior omitted from PalCalc's model.
- **Selected evidence:** decisive native/data excerpts, procedure/address or
  semantic anchors, and enough context to check each material conclusion.
- **Dependencies and version comparison:** relevant callers, helpers, defaults,
  input tables, item settings, and later writes that can change the scoped answer.
  Record the justified boundary, not every transitive dependency. For verified
  unchanged behavior, link the earlier model and retain the comparison supporting reuse; don't paste
  the same pseudocode into every version without explaining what was checked.
- **Examples and checks:** distinguishing inputs and actual results; separate
  symbolic or assumed values from verified current data and live observations.
  Label nominal probabilities and exact executable details separately; neither
  establishes measured gameplay frequencies.
- **Limits:** unresolved branches/assets/modes or untraced final writes. Raw query
  history stays under `runs/`, and exploratory reasoning/operating notes under
  `txt/`; published citations and reasoning must stand alone.

Keep a single current explanation for each path. Retain decisive excerpts and
useful citation anchors when consolidating; record meaningful corrections with
dates, without leaving superseded conclusions interleaved with current ones.
Preserve the uncommitted report under `txt/` before a substantial cleanup. Update
incoming links when replacing obsolete headings rather than keeping every phase
heading or unused evidence ID in the published file.
