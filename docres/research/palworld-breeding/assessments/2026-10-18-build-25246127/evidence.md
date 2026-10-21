# Selected species evidence: Steam build 25246127

Reviewed 2026-10-18. Windows x86-64 executable SHA-256:
`e590b5e7bfaa3fea40fab1a02cc72c8fc5fd6f8631ef2308e95ac56c25195837`.
The installed Steam manifest identifies build 25246127. Native excerpts below come
from successful retained REA/Ghidra 12.1.4 records for these exact bytes, using
analysis-profile digest `257f6477151b0bf0c2966cf83235460526e053202d0bc4ac8fa9604af3729044`.
They are decompiler/instruction observations, not original source or live execution.
Descriptive names in the reconstruction are analyst labels. Raw records and checks
remain in `_REA-ANALYSIS/runs/20261018-species-01/`.

## Inputs and identity

| Input | Evidence and meaning |
| --- | --- |
| `CombiRank` | Character-row signed int32 at `+0x100`; descriptor `0x1473ef470`, field-offset member `+0x38` |
| `CombiDuplicatePriority` | Character-row signed int32 at `+0x104`; descriptor `0x1473ef4b0`, field-offset member `+0x38` |
| `IsBoss` | Character-row bool byte at `+0xcc`; descriptor `0x1473eec80`, setter `0x140f197d0` |
| `IgnoreCombi` | Character-row bool byte at `+0x108`; descriptor `0x1473ef4f0`, setter `0x140e68230` |
| Recipe row | `ParentTribeA/B`: uint16 at `+0x8/+0xc`; gender bytes at `+0xa/+0xe`; `ChildCharacterID`: 8-byte name at `+0x10` |
| Optional item adjustment | `CombiRankBonus`: signed int32 at item-effect `+0xc`; absent means zero; descriptor `0x1473512b0`; current item values not re-extracted |
| Candidate/recipe order | Input from native table traversal; current row inventory/order not extracted |

The four character-field names/offsets were independently checked against the
current PE. The bool setters are respectively:

```text
IsBoss:      c6 81 cc 00 00 00 01 c3   MOV byte ptr [RCX+0xcc],1; RET
IgnoreCombi: c6 81 08 01 00 00 01 c3   MOV byte ptr [RCX+0x108],1; RET
```

The item-field name was also independently resolved from the current PE:
descriptor `0x1473512b0` points to ASCII `CombiRankBonus` at `0x147351628`;
its property-generation flags at descriptor `+0x18` are `3` (int32), and its
field-offset member at descriptor `+0x38` is `0xc`. This matches the species
consumer's item-effect read. The retained export of
`Pal/Content/Pal/DataAsset/MapObject/Breeding/DA_BreedingItemEffectData`,
`ItemEffectMap`, contains `CombiRankBonus = 0` for Cake02, Cake03, Cake04 and
Cake05. That export identifies the effect but no positive-bonus item; it was not
freshly extracted, so it does not establish all current item values or availability.

Recipe field labels/layout are retained reflection reconstruction, corroborated
by matching accesses in the selector and the original GenDB parser field names.
The native selector obtains 16-bit parent identifiers through `0x142f7fc70`,
compares them with the recipe tribe fields, and obtains genders through
`0x142f7b1d0`. Current tribe-to-species mapping is a separate data question.
Parent character rows are looked up through `0x142f79ad0` and `0x142e721f0`;
missing parent rows contribute rank zero in this path.

Source-data leads: `DT_PalMonsterParameter` and `DT_PalCombiUnique`, as named in
[AssetPaths.cs](../../../../../PalCalc.GenDB/GameDataReaders/AssetPaths.cs).
Names and parser behavior are leads, not proof of the live table contents.

## Recipe selection

Selector `0x142e70570`, REA record
`ev_1dd305e7a8b2687006498757e9c18ccf691dfd8774258d98fac7f42a48e8f717`,
contains diagnostic `UPalCombiMonsterParameter::FindChildCharacterID`.
It collects rows matching the two parent tribes in either order. Selected native
excerpts, with unrelated table lookup/allocation code omitted:

```c
if (((short)puVar17[1] == sStack_1c8) &&
    (*(short *)((longlong)puVar17 + 0xc) == sVar10)) bVar4 = true;
if (((short)puVar17[1] == sVar10) &&
    (*(short *)((longlong)puVar17 + 0xc) == sStack_1c8)) bVar5 = true;
if ((bVar4) || (bVar5)) { /* append this recipe row */ }
// In the subsequent gender-check loop:
if ((bVar5) && (bVar6)) {
    *puStack_1b8 = puVar18[2];
    /* cleanup, then return; later rows are not considered */
}
```

Each zero gender byte is unrestricted. The slot checks first compare its tribe to
parent A, otherwise parent B, and test the selected parent's gender. For distinct
parent tribes this implements matching either parent order. For equal tribes,
both slots can select parent A; do not silently replace this with bijective
parent assignment when assessing unusual recipe rows.

Rows are considered in traversal order, including unrestricted rows. There is no
general "gender-specific recipe first" step. The native code also remembers an
unrestricted row as a fallback; in well-formed matching rows its unrestricted
checks already succeed. If no applicable recipe is returned, it reaches rank
selection. No explicit same-species return is present in this function.

## Rank arithmetic

Assembly record
`ev_9a6be5b7fce791ffdf96d87507f8603a26de395095951189bffa46da01bb9c8c`
for `0x142e70570` contains:

```text
0x142e70a19: ADD EAX, EBX
0x142e70a1b: MOVD XMM0, EAX
0x142e70a1f: CVTDQ2PS XMM0, XMM0
0x142e70a22: MOVSS XMM1, dword ptr [0x14676d208]
0x142e70a2a: SUBSS XMM1, XMM0
0x142e70a2e: CVTSS2SI EDX, XMM1
0x142e70a32: SAR EDX, 0x1
0x142e70a43: MOV ECX, dword ptr [RCX + 0xc]
0x142e70a46: TEST ECX, ECX
0x142e70a48: JNS 0x142e70a4e
0x142e70a4a: JMP 0x142e70a58
0x142e70a4c: MOV ECX, ESI
0x142e70a4e: MOV ESI, 0xa
0x142e70a53: CMP ECX, ESI
0x142e70a55: CMOVC ESI, ECX
0x142e70a58: SUB ESI, EDX
0x142e70a5a: MOV R9B, 0x1
0x142e70a5d: MOV R8D, ESI
0x142e70a6b: CALL 0x142e713c0
```

`ESI` starts at zero; a negative item value skips the cap assignment and keeps
zero. Positive values are capped at ten. The constant at `0x14676d208` is bytes
`00 00 00 bf` (`-0.5f`), also supported by REA byte-read record
`ev_16893764bb38deae694164b57f321c981db2d6940f7ce46d1179300df33659b0`.

The literal operation is an int32 sum converted to float32, subtraction from
`-0.5f`, conversion using the current MXCSR rounding mode, arithmetic right shift,
then subtraction from the item adjustment. Under default nearest-even rounding
and normal small nonnegative integer ranks:

```text
target_rank = ceil((parent_rank_A + parent_rank_B) / 2)
              + clamp(optional_item_integer, 0, 10)
```

For an even sum `2k`, nearest-even converts `-2k-0.5` to `-2k`. For an odd sum
`2k+1`, it converts `-2k-1.5` to `-2k-2`. The signed shift and subtraction therefore
give `k` and `k+1` respectively. This is not a proof for changed MXCSR modes,
int32 overflow, or ranks too large for exact float32 representation.

## Eligible candidates and ties

Nearest-rank routine `0x142e713c0`, REA record
`ev_60ca11e6c75540b73dc28550fb44c46473e3970ae629a375ec54cefb206de165`:

```c
if (((*(char *)(uVar12 + 0xcc) == '\0') &&
     (*(char *)(uVar12 + 0x108) == '\0')) &&
    ((cStack_1b8 == '\0' ||
      (cVar5 = FUN_142e6b2b0(lStack_188,uVar3), cVar5 == '\0')))) {
    iVar7 = *(int *)(uVar12 + 0x100) - iStack_190;
    /* take absolute distance; reset the tie list on a strictly nearer row;
       append equal-distance rows in table encounter order */
}
// For more than one nearest candidate:
iVar7 = -999;
/* iterate tied rows */
iVar6 = *(int *)(lVar10 + 0x104);
if (iVar7 < iVar6) {
    *param_2 = uVar3;
    iVar7 = iVar6;
}
// If the chosen character ID is still empty:
FUN_1434e5700(&puStack_180,L"SheepBall",1);
```

The ordinary caller passes the exclusion flag as one. Exclusion routine
`0x142e6b2b0`, record
`ev_f5975a806606b10345e6fa3b9b40ed17e899f322060f110c042781bbef06c3c1`,
traverses the complete unique-recipe table and tests `puVar8[2] == param_2`:
membership as a child is sufficient, irrespective of the current parents/genders.

Distance is minimized first, then priority maximized; strict priority comparison
preserves the first tied row. The implementation initializes best distance to
999999 and priority to -999. A simplified unlimited-distance/maximum-priority
description assumes ordinary data within those bounds; retain the native sentinels
when assessing pathological inputs.

## Reconstructed ordinary selector

This intentionally leaves ranks, table contents and order symbolic:

```text
matching_recipes = rows whose parent tribes match the input pair in either order
for recipe in matching_recipes, in table order:
    if both tribe/gender slot checks accept the parents:
        return recipe.ChildCharacterID
if the native unrestricted-row fallback exists:
    return that child

target = rounded_up_parent_mean + clamp(optional_item_integer, 0, 10)
eligible = character rows where !IsBoss && !IgnoreCombi
           && character ID is not any unique-recipe child
nearest = eligible rows with minimum abs(CombiRank - target)
child = highest CombiDuplicatePriority in nearest; retain first on an exact tie
return child, or SheepBall if selection is empty
```

The arithmetic domain and native sentinels above apply. Same-species behavior is
an outcome of these rules and data in this traced path, not an extra proven rule.

## Final child boundary and verification examples

Ordinary orchestrator `0x142e73970`, record
`ev_8115ba3e817f957c73045c6a1dfad95705bae8bf6729271fce0bc6b25f3804f5`,
calls `0x142e70570`, can replace its result through a random branch calling
`0x142e71f20`, then writes the child character ID before invoking inheritance:

```c
FUN_142e70570(lVar4,&uStack_f8,param_3,param_4);
/* random comparison and possible FUN_142e71f20 replacement */
*param_6 = uVar5;
FUN_142e909b0(lVar4,param_3,param_4,param_6);
FUN_142e91200(lVar4,param_3,param_4,param_6);
FUN_142e8f8a0(lVar4,param_3,param_4,param_6);
```

The replacement pool and alternate branch (`0x142e72ea0`) were not reassessed;
this report verifies the ordinary selector, not all possible mutated offspring.

Synthetic examples used to distinguish interpretations (not actual game rows):

| Inputs | Expected consequence |
| --- | --- |
| Rank sums 200 and 201, zero adjustment | Targets 100 and 101 |
| Item input -3 or 99 | Adjustments 0 or 10 |
| Candidates rank 100/102, target 101, priorities 1/2 | Rank-102 candidate wins |
| Equal distance and priority, reverse candidate order | Winner reverses; no variant preference is implied |
| Closest row is boss, ignored, or a recipe child | Exclude it before selecting nearest |
| Unrestricted recipe before an applicable specific recipe | First unrestricted recipe wins |
| Recipe pair reversed, genders follow the parents | Same applicable recipe still matches |
| Only recipe has incompatible genders | Continue to ordinary rank selection |
| Same-species parents, another equally ranked eligible row has greater priority | The shortcut is not equivalent under these hypothetical data conditions |
| No eligible candidates | Native fallback is SheepBall |

All local synthetic checks passed, including rounding equivalence for sums 0..4096.
They do not prove current roster equivalence, current item values, live table order,
or game runtime rounding state. The fresh native startup attempt timed out; these
claims use retained successful records plus fresh PE identity/field/constant checks.
