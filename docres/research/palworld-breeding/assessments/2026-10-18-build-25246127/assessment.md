# Offspring species assessment: 2026-10-18, Steam build 25246127

Scope: ordinary offspring species selection, recipe matching, rank selection and
the item-adjustment consumer in the Windows x86-64 shipping executable. SHA-256:
`e590b5e7bfaa3fea40fab1a02cc72c8fc5fd6f8631ef2308e95ac56c25195837`.
Mutation/replacement species pools and live asset-table contents are outside scope.

Compared with the working-tree [species description](../../../../../PALWORLD-BREEDING-MECHANICS.md#pal-species),
[GenDB calculator](../../../../../PalCalc.GenDB/PalBreedingCalculator.cs),
[character reader](../../../../../PalCalc.GenDB/GameDataReaders/PalReader.cs), and
[recipe reader](../../../../../PalCalc.GenDB/GameDataReaders/UniqueBreedComboReader.cs)
based on commit `7691b05ab05a69f400b76d993ddd31cea790099b`. Implementation statements
below describe that reviewed baseline, not later changes to the linked files.
No solver, database-generation, or saved-plan behavior was changed by this assessment.

## Conclusions

- Species selection is a separate mechanic that belongs in every complete breeding
  assessment. It determines which paths exist, independently of inherited-trait odds.
- The traced ordinary selector checks special recipes before nearest-rank selection.
  It matches tribes in either parent order, applies gender requirements and returns
  the first matching row. Table order matters when recipes overlap.
- With normal small nonnegative ranks, the usual nearest-even floating-point
  environment and no item adjustment, the target is the parents' mean rounded up.
  The item consumer adds `CombiRankBonus`, an integer clamped to 0..10. The exact instruction sequence
  and the assumptions behind this simplification are retained in [the evidence](evidence.md#rank-arithmetic).
- The ordinary candidate scan excludes `IsBoss`, `IgnoreCombi`, and every character
  appearing as a recipe child. It chooses minimum rank distance, then highest
  `CombiDuplicatePriority`; an exact priority tie keeps the first encountered row.
  An empty selection falls back to `SheepBall`.
- An unconditional same-species shortcut was not found in the traced selector or
  its ordinary orchestrator. PalCalc's shortcut needs a data-equivalence check;
  absence of that shortcut alone does not prove any existing self-pair is wrong.

See [the reconstructed algorithm and selected native excerpts](evidence.md).

## PalCalc-relevant model

The [full scoped selector](evidence.md#reconstructed-ordinary-selector) explains
native arithmetic, recipe fallback, and rank selection. For PalCalc, the relevant
result is the child-ID mapping for each parent species/gender pair and supported
item/mode input:

```text
apply native recipe matching and precedence using the input tables
otherwise select the nearest eligible rank using the documented rounding and item input
resolve distance ties by priority, then table encounter order; preserve the fallback
return the final ordinary child ID within this assessment's replacement boundary
```

Native representation details can be simplified only when they preserve that
mapping over the valid data domain. Recipe rows/order, candidate eligibility,
parent genders, self-pairs, and exact ties remain relevant even though the ordinary
result is deterministic. Comparing all generated pairings would require current
asset tables; matching this symbolic algorithm alone is not graph validation.
Mutation/replacement pools remain outside scope, and current item values are not
established. The implementation comparison below records the remaining distinctions.

## Functional comparison

### Relevant game inputs

| Game reference | Owner/source and role | Supporting evidence |
| --- | --- | --- |
| `CombiRank` | Character row in `DT_PalMonsterParameter`; int32 breeding rank used for the parent mean and child distance | [Field identity](evidence.md#inputs-and-identity), [arithmetic](evidence.md#rank-arithmetic) |
| `CombiDuplicatePriority` | Character row; int32 priority, highest wins among equally near candidates | [Field identity](evidence.md#inputs-and-identity), [tie-breaking](evidence.md#eligible-candidates-and-ties) |
| `IsBoss`, `IgnoreCombi` | Character-row bool flags excluding candidates from ordinary rank selection | [Fields and setters](evidence.md#inputs-and-identity), [candidate scan](evidence.md#eligible-candidates-and-ties) |
| `ParentTribeA/B`, `ParentGenderA/B`, `ChildCharacterID` | Recipe rows in `DT_PalCombiUnique`; parent matching, gender restrictions, selected child and global recipe-child exclusion | [Input layout](evidence.md#inputs-and-identity), [recipe selection](evidence.md#recipe-selection) |
| `CombiRankBonus` | Item effect in `DA_BreedingItemEffectData.ItemEffectMap`; int32 addition to target rank, clamped to 0..10 | [Named field and retained cake entries](evidence.md#inputs-and-identity), [consumer](evidence.md#rank-arithmetic) |

These references identify the inputs and their roles. Current table contents and
item availability were not freshly extracted; the retained Cake02 through Cake05 entries
all set `CombiRankBonus` to zero.

### Agreements

PalCalc's rounded-up mean, nearest-rank selection, descending priority and global
recipe-child exclusion agree with the recovered ordinary rules under their stated
input conditions. Recipe parent reversal and unrestricted gender are also represented.
These are algorithm agreements, not an exhaustive verification of `breeding.json`.

### Differences and conditions affecting breeding paths

| Area | Comparison and possible consequence |
| --- | --- |
| Candidate eligibility | The native scan explicitly checks `IsBoss` and `IgnoreCombi`. GenDB filters bosses and uses additional roster heuristics, but does not read `IgnoreCombi`. Different eligible rosters can select different children; the current table rows must be checked before naming affected pairs. |
| Same-species shortcut | PalCalc returns the parent species before checking recipes. The traced native path instead performs recipe/rank selection. Equivalence depends on recipe contents, eligibility, ranks and priorities for self-pairs. |
| Recipe resolution | Native returns the first matching row and falls through to rank selection when no applicable row remains. PalCalc requires exactly one matching recipe once a species pair has any recipe. Overlap or incomplete gender coverage can therefore change behavior or make generation fail; current rows were not enumerated here. |
| Equal priority | Native retains table encounter order. PalCalc adds a non-variant preference after distance and priority. A difference requires tied candidates and a conflicting row order. |
| Item rank adjustment | Native reads `CombiRankBonus` at item-effect offset `+0xc`, clamps it to 0..10 and adds it to the target rank. PalCalc's species graph has no such input. The field name/offset are verified against the reviewed executable. Retained Cake02 through Cake05 entries all have zero; no positive-bonus item or specific item-induced mismatch is established. Item values/availability for the reviewed build were not re-extracted. |

These are demonstrated algorithm distinctions or conditional consequences, not a
claim that current ordinary breeding results are incorrect. Establish relevant
data conditions before deciding whether implementation changes are needed.

## Breadcrumbs for the next review

Use `UPalCombiMonsterParameter::FindChildCharacterID`,
`UPalDatabaseCharacterParameter::FindNearestCombiRank`, and
`ContainChildCharacterID_InUniqueTable` as semantic anchors. The relevant functions
in this build are `0x142e70570`, `0x142e713c0`, and `0x142e6b2b0`.
The ordinary orchestrator at `0x142e73970` stores the selected character ID and can
replace it through a separate random branch before inheritance.

Retain the character/recipe table identities, original field types, table order,
parent tribe/ID mapping, item input and final-save boundary in future assessments.
Do not assume recipe specificity wins, that a tribe is always a species, or that
self-pair behavior follows from the average alone. Full assessments must explicitly
include or exclude the separate replacement/mutation path.

## Checks

- Reused eight successful retained REA/Ghidra 12.1.4 records after checking every
  record's executable hash against the installed executable and Steam manifest.
  Selected supporting excerpts are committed in [evidence.md](evidence.md); full
  envelopes remain in the ignored investigation directory.
- Independently reread the current PE's `CombiRank` and `CombiDuplicatePriority`
  descriptors, `IsBoss`/`IgnoreCombi` setter bytes, and the `-0.5f` constant.
- Resolved `CombiRankBonus` to its int32 descriptor and `+0xc` field offset in
  the current PE; checked the four zero-valued cake entries in the retained asset export.
- Synthetic checks covered 4,097 rank sums, item clamps, nearest-rank/priority ties,
  row-order reversal, exclusions, recipe order/parent reversal/gender mismatch,
  self-pair counterexample conditions, and empty-candidate fallback. All passed.
- The reference reconstruction received an independent saved-trace review.
- A fresh Ghidra query did not finish within the connected client's startup
  deadline; it contributes no new native observation to these findings.
- No live breeding, current table extraction, complete parent-pair matrix comparison,
  or production-code test suite was performed. Asset values remain symbolic.
