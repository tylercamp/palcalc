# Historical discovery references

These are shortcuts for finding code and data during a new investigation. They
were collected during the 2026-10-16 and 2026-10-18 investigations of the Windows
x86-64 executable identified below:

- Steam build ID: `25246127`
- Executable SHA-256: `e590b5e7bfaa3fea40fab1a02cc72c8fc5fd6f8631ef2308e95ac56c25195837`
- Game version: not independently established in those reports

Use the [REA guide](REA-GUIDE.md) for the investigation procedure. These notes
contain exploration leads only: they don't establish current mechanics, supply
an algorithm to copy, or count toward the [verification checklist](README.md#current-verification-status).
Addresses are preferred-image virtual addresses from that executable, not live
ASLR addresses. Offsets and analyst labels need fresh corroboration before use,
even when a familiar string or function shape appears in another build.

## Entry points and nearby routines

Start with named diagnostics, then follow references and callers. Use the old
addresses to orient yourself only after checking the target identity and mapping.
Routine roles below are labels from the earlier investigation, not recovered
source signatures.

| Search anchor or investigation label | Old address | Exploration tip |
| --- | --- | --- |
| `UPalBreedingUtility::GenerateChildSaveParameter`, `PalBreedingUtility.cpp` | `0x142de98f0` | Follow the diagnostic-led entry toward the orchestration code |
| Child-generation orchestrator | `0x142e73970` | Follow calls toward species, talent, attack, and passive routines, and inspect the final child writes |
| `UPalCombiMonsterParameter::FindChildCharacterID` | `0x142e70570` | Start tracing recipe-table accesses and rank arithmetic here |
| `UPalDatabaseCharacterParameter::FindNearestCombiRank` | `0x142e713c0` | Look for character-row filtering, distance comparisons, and tie handling |
| `ContainChildCharacterID_InUniqueTable` | `0x142e6b2b0` | Look for recipe-child membership checks |
| IV/talent helper | `0x142e909b0` | Follow settings-array selection through each branch to saved talent fields |
| Attack helper | `0x142e91200` | Follow parental arrays, row lookups, and child array writes |
| Passive helper | `0x142e8f8a0` | Look for both count settings, item inputs, and calls into filling |
| Random passive filler | `0x143150be0` | Track keys and each weight array separately through removal and selection |
| Weighted selector / float-weight callback | `0x142c93b70` / `0x142c9d380` | Trace the callback and returned index rather than assigning meaning from a setting's name |
| Common child initialization | `0x142e73be0` | Inspect this and later callers when checking for subsequent field overwrites |
| Other species-generation paths | `0x142e71f20`, `0x142e72ea0` | Leads for following replacement and alternate branches beyond the main selector |

Useful neighboring calls from the earlier traces:

- Parent character-row lookups: `0x142f79ad0`, `0x142e721f0`.
- Parent identifier and gender accessors: `0x142f7fc70`, `0x142f7b1d0`.
- Equipped-parent attack accessor: `0x142f7aca0`.
- Attack-row predicate: `0x143339fb0`; inspect its missing-row path as well as flags.
- Natural-attack lookup, callback, and species replacement lookup:
  `0x1433344f0`, `0x14332d990`, `0x1432de870`.

## Field-layout leads

The owner matters: a parent object offset isn't interchangeable with a child-save
offset. Establish the current owner, type, and accesses through reflection and
consumers before applying any of these old layouts.

| Field / owner from the old trace | Old offset or layout |
| --- | --- |
| `Combi_TalentInheritNum` / settings | `+0x1d38`, ordered float32 weights |
| `Combi_PassiveInheritNum`, `Combi_PassiveRandomAddNum` / settings | `+0x1d48`, `+0x1d58` |
| `CombiRankBonus` / item effect | `+0xc`, signed int32 |
| `bInheritAllActiveSkills`, `PassiveInheritCountOverride` / item effect | `+0x14` bool, `+0x18` int32 |
| Parent's inline save region | `+0x3d0` |
| `Talent_HP`, `Talent_Shot`, `Talent_Defense` / parent object | `+0x450`, `+0x452`, `+0x453` |
| Same talent fields / child save | `+0x80`, `+0x82`, `+0x83` |
| `PassiveSkillList` / parent object, child save | `+0x460`, `+0x90`; 8-byte name identities |
| `EquipWaza`, `MasteredWaza` / child save | `+0x58`, `+0x68`; unsigned 16-bit attack-ID arrays |
| `IgnoreRandomInherit` / attack row | Flag byte at `+0x0b` |
| `IsBoss`, `IgnoreCombi` / character row | Bool bytes at `+0xcc`, `+0x108` |
| `CombiRank`, `CombiDuplicatePriority` / character row | Signed int32 at `+0x100`, `+0x104` |
| `ParentTribeA/B` / recipe row | uint16 at `+0x8`, `+0xc` |
| `ParentGenderA/B`, `ChildCharacterID` / recipe row | Gender bytes at `+0xa`, `+0xe`; 8-byte name at `+0x10` |

For reflection-layout discovery, the earlier trace used descriptors
`0x1473ef470` (`CombiRank`), `0x1473ef4b0` (`CombiDuplicatePriority`),
`0x1473eec80` (`IsBoss`), and `0x1473ef4f0` (`IgnoreCombi`). Descriptor member
`+0x38` was a field-offset lead. The bool setter routines were `0x140f197d0` and
`0x140e68230`; short setter bodies can help corroborate a suspected layout.

The old `CombiRankBonus` descriptor was at `0x1473512b0`, with its ASCII name at
`0x147351628`. Descriptor members `+0x18` and `+0x38` were useful type/offset
leads. Don't confuse those descriptor-relative members with the item-effect
field's offset.

## Constants and instruction landmarks

These values help locate or distinguish calculations. Re-read their bytes and
follow their consumers; matching a literal alone doesn't identify a mechanic.

| Lead | Where to look and what to inspect |
| --- | --- |
| `-0.5f`, bytes `00 00 00 bf` | Old address `0x14676d208`, referenced by the species selector near `0x142e70a22`; inspect surrounding conversions, rounding state, signed shifts, and operation order |
| Integer literals `0` and `10` near `CombiRankBonus` | Species selector around `0x142e70a43..0x142e70a58`; follow sign tests, comparisons, and conditional moves |
| `999999`, `-999`, and string `SheepBall` | Search the nearest-rank routine and its string references for initialization and fallback paths |
| Talent conditional moves | Old helper around `0x142e90c99..0x142e90ccc`; track register sources independently through each exit |
| Talent stores | Old addresses `0x142e90e03`, `0x142e90e0a`, `0x142e90e10`, `0x142e90e16`; useful endpoints for tracing source values |
| Passive weight reads | Old filler around `0x143150d26..0x143150d37`; distinguish the copied array in `R12` from the manager array accessed through `R13+0x400` |
| Passive removal and loop progress | Old filler around `0x143150f53` and `0x143150fd4..0x143150fe3`; follow total updates, append paths, and attempt-counter changes |
| Attack array reads/writes | Old addresses `0x142e912b0`, `0x142e9131c`, `0x142e9142b`; inspect element widths and source/destination collections |

The existing guide's [asset-path and field-name seeds](REA-GUIDE.md#discovery-anchors-from-palcalc)
remain the preferred starting point for another executable. In particular,
`DA_BreedingItemEffectData.ItemEffectMap`, `DT_PalMonsterParameter`, and
`DT_PalCombiUnique` are useful asset search terms. A consumer-field match doesn't
establish which values those assets currently contain.

## Maintaining these tips

Keep additions limited to locating and inspecting the relevant code or data:
search names, call paths, layout clues, literals, and useful instruction landmarks.
Give each set of addresses/offsets its executable identity. Put recovered rules,
pseudocode, evidence supporting conclusions, and verification status in the
version reports defined by the [workflow](ASSESSMENT-WORKFLOW.md).

These leads were extracted from the reports retained in Git commit `6e45912`, under
`assessments/2026-10-16-build-25246127/` and
`assessments/2026-10-18-build-25246127/`. That provenance identifies where the tips
came from; it doesn't reinstate those reports as a verified baseline.
