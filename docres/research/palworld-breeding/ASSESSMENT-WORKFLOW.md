# Assessing and updating Palworld mechanics

Start with the [mechanics checklist and current status](README.md#current-verification-status).
This workflow defines what a review establishes and publishes. Use the
[REA guide](REA-GUIDE.md) for technical investigation and the
[template](ASSESSMENT-TEMPLATE.md) for the version report. The
[review skill](palworld-breeding-review/SKILL.md) compares completed evidence with
PalCalc; it doesn't start another investigation.

Scope, decisions about further tracing, and completion are defined here. The hub
lists the mechanic questions; the guide explains how to investigate them, and
the template helps present the answers without creating additional requirements.

## Scope and version identity

A baseline or standard game-update review accounts for all eight required
mechanics in the checklist. A targeted task can investigate one mechanic without
rechecking the others, but doesn't establish full version verification.

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

### Define the questions and boundaries

Before native discovery, turn each selected checklist topic into a small set of
questions. Record these in its assessment section, together with:

- Inputs and modes: for example, specified parents, breeding item, mutation mode,
  or the active effects supplied to a capture calculation.
- The output being explained: selected child ID, saved traits, learned attacks,
  or success probability under those inputs.
- The evidence needed to connect the calculation to that output, including later
  transformations that could change it.
- External conditions left as inputs, and what remains unknown about them.

Use the user's scope and the hub's mechanic questions to choose these boundaries.
Account for relevant ordinary, item, mutation, spawn, and capture behavior; don't
exclude a mode just because PalCalc omits it. A new material branch can require
revising the questions, with the reason recorded. Don't narrow them afterward
merely to turn a partial answer into a verified one.

A calculation given an active-effect set and the gameplay conditions producing
that set are separate questions. Establish the interface and relevant consumers
before treating activation as an external input. State which results are
conditional; an unknown activation rate prevents an unconditional probability
claim. Likewise, unknown pool order can't be dismissed as external when it changes
the probability being claimed.

The output boundary should explain player-relevant contents, not stop at an
arbitrary helper return. It needn't reconstruct unrelated bookkeeping or every
event in an effect's lifecycle once the required answer is supported.

## Deciding what to investigate next

Before following another dependency, state briefly:

1. Which unanswered mechanics question could this change?
2. What specific fact or observation would settle it?
3. Where will this trace stop?

Continue when it can change a required answer or resolve an identified uncertainty.
Otherwise record it as a conditional input or follow-up, explaining the limitation
on the answer. A suspicious function or another caller alone isn't a reason to
expand the investigation. Shared helpers still need checking when their behavior
can change the result; this rule doesn't justify assuming they are harmless.

On continuation, first reassess the remaining questions against retained evidence.
Reuse supported answers and plan only the missing observations, rather than
starting another general discovery pass. Keep the current questions and next
useful checks in the private checkpoint; don't maintain a second public task log.

### Probability claims

Distinguish the recovered selection rules, probabilities under stated inputs and
assumptions, exact executable details, and measured gameplay frequencies. Choose
the precision needed for the question instead of treating exact RNG replay as a
default requirement. A nominal uniform-draw calculation must be labelled as such.

Recover numeric inputs when they are needed to establish a claimed probability;
otherwise retain them symbolically with their provenance and limits. Investigate
ordering, finite-grid draws, or shared RNG dependencies when they can materially
change that answer. For example, a mismatched passive-weight array can make pool
order relevant to success, not just replay. Don't simplify that effect away to
finish the review. Static reconstruction alone doesn't establish live frequencies
or RNG independence.

## 1. Recover game behavior

Use a fresh `_REA-ANALYSIS/runs/<timestamp>-<id>/` under the repository root, or
resume the same run after interruption. Keep script output, raw responses, traces,
logs, exports and snapshots there. Use `_REA-ANALYSIS/txt/<investigation-id>/`
for temporary Markdown documentation: topic investigations, hypotheses, draft
models, rejected leads, and continuation notes. Both locations are ignored by Git.
Working notes should identify the relevant run paths and artifact identities so
another work period can resume without reconstructing the original conversation.

Create these directories as needed. Existing working notes can be migrated when
useful; preserve their contents and update continuation pointers rather than
moving files an active script or session depends on.

Follow the [REA guide](REA-GUIDE.md), establishing behavior before consulting
PalCalc's probability assumptions. GenDB readers provide names and input leads,
not proof of native eligibility or branch semantics. Trace inputs, decisions,
helpers, and final observable writes. Record missing proof rather than guessing.
For a later version, use the dependency comparison below to avoid repeating work
already justified as unchanged.

## 2. Reconstruct the game model

Develop the model in temporary topic notes under `txt/`. Promote established
parts into `evidence.md` as coherent sections per mechanic or shared process:
readable pseudocode, input meanings/types/units, modes, and decisive saved excerpts.
Include branches PalCalc intentionally omits. Full means complete within the
stated scope, not every Palworld system. A partial mechanic can have established
parts worth publishing; keep its unresolved boundaries explicit.

### What belongs in published evidence

Each section of `evidence.md` should support a current conclusion or material
limitation in `assessment.md`. Keep the game model and the observations needed
to check that reasoning, including evidence for why an answer is conditional.
Shared processes can support several mechanics without repeating their traces.

Keep derivations that explain mechanics decisions. For supporting machinery that
preserves inputs or results, retain the conclusion, its conditions and a few useful
verification landmarks (such as named functions/classes, distinctive fields,
assets or build-specific addresses). Self-contained evidence means readers can
check or revisit the scoped conclusion without private notes; it does not require
deriving every supporting subsystem. Do not turn a bounded preservation result
into a universal guarantee or omit a transformation that can change the answer.

Hypotheses, candidate lists, failed lookups, intermediate reconstructions and
step-by-step discovery history belong in `txt/`. A negative finding can still be
published when it establishes a conclusion or limitation; the search diary behind
it usually isn't needed. Don't wait for an entire mechanic to become Verified
before publishing its established parts, or remove a real uncertainty to shorten
the file. There is no line limit: relevance and sufficient support determine
what stays, rather than the amount of work spent finding it.

Separate native observations, arithmetic reconstruction, synthetic examples, and
live game observations. Keep external data symbolic unless verified or explicitly
assumed. Each material claim needs its target, procedure/address or semantic
anchor, relevant branch, and enough surrounding context to check the reasoning.
Raw decompiler output alone isn't an effective model.

Record the code and data dependencies needed to check this mechanic next time.
Include the shared helpers, caller branches, relevant defaults/overrides, input
tables, and later writes that could change the scoped conclusion. Apply the
dependency decision above rather than enumerating every transitive subsystem.
Old addresses are breadcrumbs, not a dependency comparison.

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

After each continuation, consolidate the report around the current understanding:

- `assessment.md` gives current answers, coverage, and remaining gaps per mechanic.
- `evidence.md` gives the current scoped model and the decisive observations that
  support the assessment's conclusions and limitations. Preserve useful excerpts
  when merging earlier sections; it isn't a transcript of the investigation.
- The changelog records changes to checked versions, observed game mechanics and
  assessment scope. Detailed findings and dated evidence corrections stay in the
  version report; research and publication checkpoints do not need changelog entries.
- Temporary topic notes and the private checkpoint under `txt/` hold exploratory
  reasoning, operational history, session state, and next steps, with references
  to generated artifacts under `runs/`.

Replace superseded explanations instead of appending another account of the same
path. Keep corrections traceable through dated notes and Git history; don't erase
decisive evidence or leave contradictory old conclusions mixed with current ones.
Before a substantial cleanup of an uncommitted report, save a local copy under
`txt/` so useful material and correction context aren't lost. Move exploration
notes there and promote the consolidated argument into the published files.
Keep citation anchors where useful or update their incoming links; preserving
every historical heading or evidence ID isn't a publication requirement. Retain
the IDs and excerpts supporting published claims, along with necessary correction
notes. Published reasoning and citations must stand without either ignored folder.

Update the hub's mechanic rows. Add a short [changelog](CHANGELOG.md) entry when
the latest checked version, an observed game mechanic or assessment scope changes.
Distinguish a supported game change from newly recovered behavior or a correction
to our understanding in the version report. Failed or partial checks belong there
too. Keep an older last-verified version visible when the new version isn't established.

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

## Investigation state and verification coverage

Record investigation state separately from what has been verified:

- **Ongoing:** further work on the declared questions is planned.
- **Finished:** the answers and remaining gaps have been delivered, with no further
  work planned in this investigation. Say explicitly when it finished with gaps.

A turn, phase, or token-budget boundary ends a work period, not the investigation.
Keep the investigation Ongoing when further work on its declared questions remains
planned. Finishing with gaps is a separate decision: explain why the remaining
questions are deferred and why that delivery boundary is appropriate for the
user's scope. A list of missing evidence, or a request to stop after one phase,
does not supply that explanation. Record the actual reason; don't invent a
retrospective justification for an earlier stop.

Each mechanic also needs a coverage disposition:

- **Verified:** recovered and checked for this version, with conclusions and evidence.
- **Verified unchanged:** the previous rule still applies, supported by a recorded
  comparison of all relevant code and data dependencies.
- **Partial:** some questions or modes are established, with the remaining gaps named.
- **Unresolved:** the available evidence doesn't establish the rule for this version.
- **Not checked:** the question wasn't investigated, as in a targeted task.

Verification applies to the recorded questions, inputs, modes, and output boundary.
Within a partial mechanic, state which answers are established: for example,
calculation verified, activation partial, probability conditional on the supplied
effect set. Use a short explanation where needed, not a status for every function.
A conditional calculation doesn't verify the broader gameplay claim.

The investigation can finish with gaps when those gaps and their consequences are
explained, retained evidence has been assessed, and a reason for stopping is
recorded under the separate completion decision above. Explain why further
investigation is deferred, not just what remains unknown. Identify what further
observation would be needed; don't treat each
unknown as an instruction to keep tracing indefinitely. Stopping doesn't upgrade
coverage. Full version verification still requires all required questions and
modes to be supported; a finished report with missing answers remains partial.

When continuing an existing report under these instructions, reassess its scope
and gaps before querying the binary. Don't upgrade grades just because the process
changed. A grade change needs an evidence-based explanation of the supported claim.

## Reviewing a game update

Create the new version report from the checklist, using the previous report as
the comparison baseline. Compare the same questions, inputs, modes, and output
boundaries; explain any change in scope. Use the coverage dispositions above.

For **verified unchanged**, identify the earlier pseudocode/conclusions and record
what was compared: code bodies and branches, shared helpers/callers/final writes,
relevant tables, defaults, and item inputs. Explain why the comparison covers the
mechanic. Matching an entire dependency's hash can avoid reanalysis; a changed
hash requires determining whether the change affects this mechanic. A matching
top-level function alone doesn't establish unchanged callees or assets. Different
addresses don't establish different behavior. Retain enough comparison evidence
to reproduce the conclusion without ignored caches.

Full version verification requires every required mechanic to be verified or
verified unchanged for the declared platform/configuration and required modes.
An investigation can finish with partial or unresolved coverage under the rules
above. List exclusions prominently; a review limited to ordinary breeding mustn't
appear as full coverage of item or alternate behavior. Supporting mechanics aren't
part of the full-verification requirement.

The first comprehensive baseline has no previous complete report to inherit:
establish its rules and evidence directly. Old hypotheses and maintained summaries
can guide discovery, but aren't substitutes for the missing proof.

Preserve previous version reports. Amend a published conclusion only with a dated
correction in that report explaining what changed in our understanding. Correct
the changelog if its checked-version, game-change or scope record is affected;
don't invent a historical game change.
