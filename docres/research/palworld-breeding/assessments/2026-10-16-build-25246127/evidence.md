# Selected inheritance evidence: Steam build 25246127

Target: Windows x86-64 shipping executable, SHA-256
`e590b5e7bfaa3fea40fab1a02cc72c8fc5fd6f8631ef2308e95ac56c25195837`.
Recovered on 2026-10-16 with REA/Ghidra. The snippets below are selected from the
completed investigation; pseudocode and source labels are analyst reconstructions.
Addresses apply to this build. This file contains the evidence needed by the
[report](assessment.md); raw query envelopes and working notes remain local.

## Entry points and fields

Search for `UPalBreedingUtility::GenerateChildSaveParameter` or
`PalBreedingUtility.cpp`. The diagnostic-led entry at `0x142de98f0` calls the
orchestrator at `0x142e73970`, which dispatches ordinary talents, attacks, and
passives to `0x142e909b0`, `0x142e91200`, and `0x142e8f8a0` respectively.

| Semantic anchor | Location in this build |
| --- | --- |
| `Combi_TalentInheritNum` | Settings + `0x1d38`, ordered float32 weights |
| `Combi_PassiveInheritNum` / `Combi_PassiveRandomAddNum` | Settings + `0x1d48` / `0x1d58` |
| `PassiveInheritCountOverride` / `bInheritAllActiveSkills` | Item effect + `0x18` (int32) / `0x14` (bool) |
| `Talent_HP`, `Talent_Shot`, `Talent_Defense` | Parent + `0x450`, `0x452`, `0x453`; child save + `0x80`, `0x82`, `0x83` |
| `EquipWaza` / `MasteredWaza` | Child save + `0x58` / `0x68`, unsigned 16-bit attack-ID arrays |
| `PassiveSkillList` | Parent + `0x460`; child save + `0x90`, 8-byte name identities |

These layouts were correlated with saved field descriptors and consumers. The
parent's save region is inline at +`0x3d0`. Use the semantic names and caller chain
to relocate code in another build before reusing its offsets.

## IV source selection

Ordinary helper: `0x142e909b0`. The weighted selector at `0x142c93b70` returns the
original array index, retaining indices when nonpositive weights are filtered.
Its callback `0x142c9d380` loads a float32 weight. The selector index is not a final
inherited-stat count.

Let `H` contain values chosen separately from either parent for HP, Shot, and
Defense, and `R` contain fresh random values. The recovered source table is:

| Selector index | Possible parent-sourced sets |
| --- | --- |
| 0 | HP; Shot; Defense |
| 1 | HP+Shot+Defense; HP+Defense; HP+Shot |
| 2 | HP+Shot+Defense |

Sentinel/unexpected indices take the one-stat fallback. In the middle category,
the branch selector `j` is 0, 1, or 2. Before this excerpt, `R12D`, `EBX`, `EDI`
hold the fresh values; `[RBP+0x6f]`, `R15D`, `R14D` hold parental HP, Shot, Defense.
Selected native instructions, from the saved ordinary-helper assembly:

```text
0x142e90c99: CMP ESI, 0x1
0x142e90c9c: JNZ 0x142e90cd1
0x142e90c9e: CMP EAX, ECX
0x142e90ca0: CMOVL ECX, EAX
0x142e90ca3: MOV EAX, R14D
0x142e90ca6: TEST ECX, ECX
0x142e90ca8: CMOVNZ EAX, EDI
0x142e90cab: MOV EDI, EAX
0x142e90cad: MOV EAX, R15D
0x142e90cb0: CMOVNZ EAX, EBX
0x142e90cb3: MOV EBX, EAX
0x142e90cb5: CMP ECX, ESI
0x142e90cb7: JNZ 0x142e90cc5
0x142e90cb9: MOV EDI, R14D
0x142e90cbc: MOV R12D, dword ptr [RBP + 0x6f]
0x142e90cc0: JMP 0x142e90c0e
0x142e90cc5: MOV EBX, R15D
0x142e90cc8: MOV R12D, dword ptr [RBP + 0x6f]
0x142e90ccc: JMP 0x142e90c0e
```

For `j=0`, both conditional moves retain parental Shot/Defense and the final path
restores parental HP: all three are inherited. Both exits restore parental HP.
The final stores at `0x142e90e03/e0a/e10/e16` write HP, Shot into both Melee and
Shot, then Defense. A fresh value matching a parent numerically is not proof of
inheritance.

## Passive selection and override

Helper `0x142e8f8a0` combines the parents' passive IDs, keeps first occurrences,
and samples without replacement. Ordinary inheritance uses the selected index
of `Combi_PassiveInheritNum` plus one, then the unadjusted index of
`Combi_PassiveRandomAddNum` for filling remaining slots.

The positive override is a separate branch. Selected decompiler excerpts below
omit allocation/copying details; the ellipses mark omitted code:

```text
if ((*param_5 != 0) && (iVar11 = *(int *)(*param_5 + 0x18), 0 < iVar11)) {
    ...
    iVar10 = 4;
    if (iVar11 < 4) {
      iVar10 = iVar11;
    }
    ...
    if ((int)uVar17 < iVar10) {
      ...
      uStack_e4 = (longlong *)CONCAT44(iVar10 - uVar17,(undefined4)uStack_e4);
      ...
      FUN_143150be0(uVar6,param_4,&plStack_f0,&plStack_c0);
      ...
    }
    ...
    goto LAB_142e902b3;
}
```

`iVar10` is the capped target; `uVar17` is the parental selection's output count.
The filler receives their difference. The jump goes to final list assignment,
past both ordinary count selectors. Reconstructed behavior:

```text
target = min(positive_override, 4)
out = sample_without_replacement(distinct(parentA + parentB), up_to=target)
out += random_fill(excluding=out, attempts=target - out.count)
write child.PassiveSkillList = out
```

Neither parental selection stage filters passive rows by rank or eligibility.
Filling uses the passive manager's eligible pool and can fall short.

## Random passive filler

Helper `0x143150be0` copies manager weights, removes excluded keys and copied
weights, and reduces the total. After removals, `R12` is the mutable copied array;
`R13` is the manager pointer, whose +`0x400` array remains uncompacted.
The saved assembly contains:

```text
0x143150d15: IDIV EBX
...
0x143150d26: CMP EDX, dword ptr [R12 + RSI*0x4]
0x143150d2a: JL 0x143150e6a
0x143150d30: MOV RAX, qword ptr [R13 + 0x400]
0x143150d37: SUB EDX, dword ptr [RAX + RSI*0x4]
...
0x143150f53: SUB EBX, dword ptr [R12 + RSI*0x4]
```

Comparison uses the copied weight; failed comparison subtracts the original weight
at the same index. Selection/removal subtracts the copied weight from the total.
The attempt counter advances at `0x143150fd4..fe3` whether a passive was appended
or not. Empty pools stop filling; a zero total with remaining keys reaches native
division by zero rather than a defined probability branch.

A synthetic counterexample: excluding the first entry from original weights
`[1,3,5]` leaves copied weights `[3,5]`. Ticket 7 fails comparison with 3, subtracts
original weight 1, then fails comparison of 6 with 5. Correct aligned subtraction
would leave 4 and select that entry. This shows possible failure, not its frequency
with live game data.

## Attack inheritance

The ordinary helper `0x142e91200` reads equipped parent arrays through
`0x142f7aca0`. The inherited pool is deduplicated. Predicate `0x143339fb0` excludes
missing rows and rows whose +`0x0b` flag is set (`IgnoreRandomInherit`). Its saved
decompiler body returns the row byte when lookup succeeds, otherwise 1.
Call-site assembly confirms the current ID is passed to that predicate in both
ordinary and all-attack item loops. Representative reads/writes are:

```text
0x142e912b0: MOVZX EBX, word ptr [R12]
0x142e9131c: MOV word ptr [RSI + RDI*0x2], BX
0x142e9142b: MOVZX EBX, word ptr [RBX + RAX*0x2]
```

Reconstructed ordinary/item behavior:

```text
pool = distinct(eligible(parentA.EquipWaza + parentB.EquipWaza))
inherited = one_random_choice(pool) if pool is nonempty else []
combined = distinct(inherited + child_level_one_attacks)
if item.bInheritAllActiveSkills:
    inherited = distinct(inherited + pool)
    combined = distinct(combined + pool)
child.MasteredWaza = inherited
child.EquipWaza = first_three(combined)
```

The initial random draw still occurs with the all-attack flag; it affects ordering.
Natural learning uses `0x1433344f0`, callback `0x14332d990`, and a single species
replacement lookup `0x1432de870`. Final inherited availability and initial equipment
ordering are separate comparison questions.

## Example inputs and limits

The investigation's numeric examples used repository database v28 values:
IV selector weights `[3,2,1]`, parental passive weights `[4,3,2,1]`, and random-add
weights `[4,3,2,1]`. See the [repository database](../../../../../PalCalc.Model/db.json).
PalCalc's existing Cake05 model uses passive override 4 and all-attack inheritance.
These settings/item values were not independently re-scraped for this executable.

PalCalc's derived probabilities assume ideal uniform branches and independent
parent choices. Native code uses masked `rand`, float32 scaling, and modulo; static
recovery does not establish measured frequencies or RNG independence. Live pool
order, actual filler failure frequency, and exhaustive downstream alias effects
remain unverified. Common initialization `0x142e73be0` shows no direct overwrite of
these inheritance fields, but nested side effects and eventual live save behavior
were not exhaustively audited. Mutation and talent-bonus branches were recovered
separately and are outside the solver correction described in the report.
