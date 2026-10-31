# Breeding mechanics review notes

Use these distinctions to assess the selected build's evidence. Concrete
algorithms, inputs and worked examples belong in the version report; these notes
do not establish a build's behavior.

## Terms and the functional boundary

- **Attack IV** means the game's Shot talent. Melee is unused for the player-facing estimate in this project. Copying Shot into Melee is not an additional discrepancy; a different source-selection algorithm for HP/Attack/Defense can be.
- **Pal-exclusive attacks** is the README's player-facing description of attacks excluded by `IgnoreRandomInherit`. Finding the flag check does not itself contradict that description. Establish a real mismatch in eligible attacks before listing one.
- **Inherited attacks** means final available inherited contents for this assessment, unless the user asks about equipment. A random first pick, natural-skill ordering or three-slot equipment truncation is irrelevant if all eligible inherited attacks remain available in the end. Do not turn equipment priority into a claimed inheritance loss.
- Separate equipped/mastered writes in a generation helper do not alone establish a final player-visible difference. Later initialization may reconcile collections. If that boundary was not traced, keep it as an unresolved implementation observation.
- Distinct-parent deduplication agrees with the documented pool model; it is not a discrepancy. Native index order matters only when it changes the requested outcome or its probability, rather than exact RNG replay alone.
- A random IV can equal a parental IV. Observed numerical matches do not prove a parent source. Track source labels when evaluating inherited-count claims.
- In a targeted ordinary-mode review, don't use excluded mutation branches to
  weaken or contradict ordinary-mode guarantees. A comprehensive version review
  still needs to account for alternate modes explicitly; these notes don't
  authorize omitting them from the checklist.

## Selection and outcome checks

- Follow a selector's branches before interpreting its category as an achieved
  count. Keep source selection separate from incidental numerical matches.
- Compare the entire override branch with the ordinary process, including which
  steps it replaces. Distinguish requested counts, attempts and successful
  additions; guarantees require sufficient candidates and successful filling.
- Assess the recovered behavior even when it appears buggy. Do not silently
  substitute an intended algorithm or claim developer intent. Demonstrate the
  conditions for a failed selection, distinguishing them from exhausted pools
  or invalid inputs; do not invent a separate failure chance without evidence.
- Separate possible failures from measured gameplay frequency or magnitude.
  Avoid counting multiple symptoms of one cause as independent discrepancies.

## Evidence and reporting scope

- A completed REA model is build-specific. Preserve target identity, evidence locations and the final-write boundary; do not promote static analysis to observed execution.
- Item-specific claims require the item's data provenance. Recovering the consumer branch alone does not verify current item values.
- Keep numeric tables/defaults symbolic unless the task calls for them. Conditional calculations using README weights must be labelled hypothetical, including RNG assumptions.
- Distinguish an algorithm discrepancy from its practical impact. A demonstrated branch can matter structurally even when its frequency has not been evaluated.
- Do not claim the current solver is wrong solely from the mechanics README. A code-accuracy question needs the actual implementation and relevant callers; a documentation comparison does not require a general code audit.
- A useful final review has two lists: estimate-relevant discrepancies, followed by clarifications and unresolved boundaries. Do not mix confirmed differences with agreements, terminology corrections or unrelated details.

## Species, gender, and wild-Pal boundaries

- Ordinary recipe/rank selection alone does not establish live table equivalence
  or mutation/replacement pools. Compare eligibility, self-pairs, first-match recipe
  order, gender coverage, ties and item inputs separately.
- A missing native shortcut is not a demonstrated wrong live result without the
  data conditions that distinguish it. Likewise, source/parser heuristics do not
  establish the native eligible roster.
- Required parent genders in recipes are separate from the child's random gender.
  Spawn-time contents and capture-time changes are separate from inheritance.
  A parsed save proves persisted contents, not their generation history.
