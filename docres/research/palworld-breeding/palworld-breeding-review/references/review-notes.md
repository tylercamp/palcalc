# Breeding mechanics review notes

These distinctions came from a completed Palworld inheritance review. Apply the reasoning to the selected build's evidence; do not assume an old finding is still present. The examples below are useful hypotheses and counterexamples, not substitutes for native proof.

## Terms and the functional boundary

- **Attack IV** means the game's Shot talent. Melee is unused for the player-facing estimate in this project. Copying Shot into Melee is not an additional discrepancy; a different source-selection algorithm for HP/Attack/Defense can be.
- **Pal-exclusive attacks** is the README's player-facing description of attacks excluded by `IgnoreRandomInherit`. Finding the flag check does not itself contradict that description. Establish a real mismatch in eligible attacks before listing one.
- **Inherited attacks** means final available inherited contents for this assessment, unless the user asks about equipment. A random first pick, natural-skill ordering or three-slot equipment truncation is irrelevant if all eligible inherited attacks remain available in the end. Do not turn equipment priority into a claimed inheritance loss.
- Separate equipped/mastered writes in a generation helper do not alone establish a final player-visible difference. Later initialization may reconcile collections. If that boundary was not traced, keep it as an unresolved implementation observation.
- Distinct-parent deduplication agrees with the documented pool model; it is not a discrepancy. Native index order matters only when it changes the requested outcome or its probability, rather than exact RNG replay alone.
- A random IV can equal a parental IV. Observed numerical matches do not prove a parent source. Track source labels when evaluating inherited-count claims.
- Unless requested, leave mutation out. Do not use excluded mutation branches to weaken or contradict ordinary-mode guarantees.

## Findings that can affect estimates

### Asymmetric IV selection

Do not read a weighted selector's index as the achieved count without following its branches. In the reviewed native model, category 1 (the nominal two-stat category) had these possible parent-source sets:

```text
{HP, Attack, Defense}
{HP, Defense}
{HP, Attack}
```

This differs from uniformly choosing two of three stats: HP is always parent-sourced in that category, and one branch takes all three. Verify the table against the selected run's decisive instructions. When this behavior is present, simply normalizing category weights as probabilities of exactly one/two/three inherited stats gives the wrong model. Selection of either parent's value remains a separate step.

### Passive weight mismatch and failed attempts

The reviewed filler compared the ticket against a compacted weight vector but subtracted entries from the original vector after candidates were removed. Preserve the distinction between the arrays; do not silently rewrite it into conventional weighted selection.

To assess failure reachability, first consider the corrected/invariant case: nonnegative remaining weights, a positive total equal to their sum, aligned keys and weights, and no arithmetic overflow. A ticket in `[0,total-1]`, compared and reduced using that same vector, must select a positive-weight entry. Thus counting attempts is not independently a random-failure mechanic under those conditions.

The mismatch can break that guarantee. For example, original weights `[1,3,5]` become `[3,5]` after excluding the first entry. With ticket 7, comparing against 3 then subtracting original weight 1 leaves 6, which also fails the comparison against 5. Correct subtraction would leave 4 and select the second remaining entry. This demonstrates possible behavior, not its frequency with actual game data.

Distinguish this regular-loop behavior from exhausted candidates (which stop filling), malformed caches, missing rows, zero totals and overflow. Call the mismatch an apparent bug or implementation quirk, without claiming developer intent. Its biased outcomes and possible shortfalls belong under one cause; avoid double-counting failed attempts as a separate discrepancy. Check actual live weights/order before claiming practical frequency or magnitude.

### Passive override semantics

Compare the entire item branch with the ordinary process. In the reviewed model, a positive `PassiveInheritCountOverride=N` replaced both ordinary count rolls. It selected up to `min(N,4)` distinct parental passives, then attempted random filling for any shortfall.

- Override 4 with at least four distinct parental passives directly produces four in the ordinary override path.
- With fewer parental candidates, the branch targets four but relies on filling; an unqualified always-four guarantee requires proof of successful filling and sufficient candidates.
- An override other than four does not automatically target four.
- Without the weight mismatch and with valid, sufficient pools, do not invent an independent chance for a weighted fill attempt to fail.

An item-specific claim such as Cake05 having override 4 or all-attack inheritance enabled requires that item's data provenance. Recovering the consumer branch alone does not verify current item values.

## Evidence and reporting scope

- A completed REA model is build-specific. Preserve target identity, evidence locations and the final-write boundary; do not promote static analysis to observed execution.
- Keep numeric tables/defaults symbolic unless the task calls for them. Conditional calculations using README weights must be labelled hypothetical, including RNG assumptions.
- Distinguish an algorithm discrepancy from its practical impact. A demonstrated branch can matter structurally even when its frequency has not been evaluated.
- Do not claim the current solver is wrong solely from the mechanics README. A code-accuracy question needs the actual implementation and relevant callers; a documentation comparison does not require a general code audit.
- A useful final review has two lists: estimate-relevant discrepancies, followed by clarifications and unresolved boundaries. Do not mix confirmed differences with agreements, terminology corrections or unrelated details.

## Species, gender, and wild-Pal boundaries

- The 2026-10-18 species evidence covers ordinary recipe/rank selection, not live
  table equivalence or mutation/replacement pools. Compare eligibility, self-pairs,
  first-match recipe order, gender coverage, ties, and item inputs separately.
- A missing native shortcut is not a demonstrated wrong live result without the
  data conditions that distinguish it. Likewise, source/parser heuristics do not
  establish the native eligible roster.
- Required parent genders in recipes are separate from the child's random gender.
  Spawn-time contents and capture-time changes are separate from inheritance.
  A parsed save proves persisted contents, not their generation history.
- Map conversion and egg-production time are optional scoped reviews. Distinguish
  calibrated/assumed PalCalc formulas from recovered native behavior, and
  production from incubation or availability allowances.
