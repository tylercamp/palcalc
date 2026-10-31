# Code comments

Write inline comments like a helpful note to another developer: casual, direct,
and technically precise. Explain why we chose an approach, what assumption makes
it valid, or what tradeoff we accept. Use "we," contractions, and concrete
language where natural. Match the surrounding capitalization and formatting.

Preserve useful existing explanations and phrasing, including constraints and
accuracy caveats. Keep XML documentation somewhat more formal, focused on purpose,
workflow, and caller-visible limitations. For comment reviews, follow
[docs-refinement](.opencode/skills/docs-refinement/SKILL.md) for fuller guidance
and examples.

# Mechanics research

For new REA analysis or comparisons after a game update, start with the
[mechanics checklist and status](docres/research/palworld-breeding/README.md#current-verification-status),
then read the [assessment workflow](docres/research/palworld-breeding/ASSESSMENT-WORKFLOW.md)
and relevant sections of the [REA guide](docres/research/palworld-breeding/REA-GUIDE.md).
A standard game-update review accounts for every required mechanic. Follow the
workflow's scope, further-tracing, and completion rules: define questions and
output boundaries, justify new dependencies, and distinguish investigation state
from verification coverage. A finished investigation can have documented gaps;
it mustn't imply full version verification. Map conversion and breeding time are
supporting topics, not required update checks.

Record game version as the primary identity, with build IDs/hashes as supporting
metadata. Publish conclusions and evidence by game version and mechanic, update
the checklist and changelog, and distinguish game changes from corrections to our
understanding. Don't assume a skill will be discovered automatically: the workflow
and guide above are the required starting points for investigation.

For reviewing completed findings, explicitly read the repository's
[review skill](docres/research/palworld-breeding/palworld-breeding-review/SKILL.md)
and its [notes](docres/research/palworld-breeding/palworld-breeding-review/references/review-notes.md).
This is a comparison of retained evidence, not authorization to start another
native investigation. The [research hub](docres/research/palworld-breeding/README.md)
explains the task routes, published reports, and document relationships.
