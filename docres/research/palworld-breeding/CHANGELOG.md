# Mechanics verification history

This records game-version reviews and changes in our understanding of the
mechanics. The [research hub](README.md#current-verification-status) is the current
status record; each version's assessment and evidence hold the detailed findings.

## 2026-10-21: documentation reset, baseline pending

The earlier incremental reports have been cleared for a comprehensive baseline.
They remain in Git history. No game version has been fully verified under the new
checklist, and this documentation reset isn't a mechanics verification.

## Recording a review

Add entries newest first, headed by **game version and review date**. Link the
version report and list the affected mechanics under the applicable categories:

- **Game behavior changed:** a supported difference between two checked versions.
- **Verified unchanged:** checked code/data dependencies support the earlier rule.
- **Newly verified:** behavior established for the first time; this doesn't imply
  it was introduced by this game version.
- **Understanding corrected:** our earlier conclusion or model was wrong or
  incomplete; explain the correction without claiming the game changed.
- **Still unresolved:** missing proof or partial coverage, with its practical limits.

State whether the overall version review is complete or partial. Record platform
and supporting build/hash identities in the linked report. A later correction or
targeted review gets its own dated entry and updates that version's sections;
don't rewrite the history as though it was known at the original review date.
