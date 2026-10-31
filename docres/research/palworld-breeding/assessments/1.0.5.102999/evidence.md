# Palworld 1.0.5.102999: mechanics evidence

## Identity and methods

Review dates: 2026-10-21–29. Matching identity checks: executable October 29, base
archive October 28, mappings/decoder October 25. This baseline covers the Windows x86-64
Steam executable and shipped base archive. The shipped `Pal/Config/DefaultGame.ini`,
section `/Script/EngineSettings.GeneralProjectSettings`, contains:

```ini
ProjectName=Pal
ProjectVersion=1.0.5.102999
```

This establishes the packaged project version; no title-screen version was
observed. Supporting installation identities:

| Input | Identity |
| --- | --- |
| Steam app / installed build | Palworld, 1623730 / 25246127 |
| Executable | `Pal/Binaries/Win64/Palworld-Win64-Shipping.exe`, 161,802,312 bytes |
| Executable SHA-256 | `e590b5e7bfaa3fea40fab1a02cc72c8fc5fd6f8631ef2308e95ac56c25195837` |
| Preferred image base | `0x140000000` (addresses below are static preferred VAs) |
| Base archive | `Pal/Content/Paks/Pal-Windows.pak`, 39,254,065,482 bytes |
| Archive SHA-256 | `bf0e5a4e11e925e0a222ad8f6ff19afc344c2a1570f79c7f9a0f54152061973b` |
| `Mappings.usmap` SHA-256 | `30df115851cea62dcbd63172607820c8cac0102d09a8308d2f5ce6938754e4f1` |
| CUE4Parse DLL SHA-256 | `98154fafa9ba4f2d0ae9ffe9e18de9fc1242bd4507ed711bbf86234cd46947ef` |
| Oodle DLL SHA-256 | `cba19529d0a3b5ec9c630e95652af01e123ae29a34a8a5f7507f5bcf23d9e82b` |

The only top-level archive file was `Pal-Windows.pak`. A separate installed
`~mods/Instant Breed Farm with Berries_P.pak` (12,008 bytes) was not mounted or
executed. Conclusions concern shipped base behavior; effective modded behavior,
live world settings, and player saves were not sampled.

Tools: REA 6.5.0, Ghidra 12.1.4, JDK 25.0.4.1; CUE4Parse with
`EGame.GAME_UE5_1`, local mappings and Oodle. The reduced Ghidra profile disables
Windows x86 PE RTTI Analyzer, Function ID, x86 Constant Reference Analyzer, Create
Address Tables, Stack, Decompiler Switch Analysis and Decompiler Parameter ID.
Types, computed calls, and decompiler parameter inference therefore need explicit
corroboration. Pseudocode uses analyst variable names, not recovered source.

Native REA observations are bound to this executable by their subject SHA-256.
Asset snapshots use the archive, mappings and decoder identified above; executable
identity alone does not establish asset contents. Evidence IDs identify observations,
with decisive excerpts and input snapshots included in this report.

### Shared inputs and arithmetic

Fresh serialized settings export:
`Pal/Content/Pal/Blueprint/System/BP_PalGameSetting`,
`Default__BP_PalGameSetting_C`:

```text
Combi_TalentInheritNum     = float32[3, 2, 1]
Combi_PassiveInheritNum    = float32[4, 3, 2, 1]
Combi_PassiveRandomAddNum  = float32[4, 3, 2, 1]
Combi_BossPalRate          = float32(0.05)
```

Fresh `DA_BreedingItemEffectData.ItemEffectMap`:

| Item | TalentBonusMin..Max | MutationRateBonusPercent | CombiRankBonus | BreedCount | bInheritAllActiveSkills | PassiveInheritCountOverride |
| --- | --- | ---: | ---: | ---: | --- | ---: |
| Cake02 | 1..5 | 0 | 0 | 1 | false | 0 |
| Cake03 | 0..0 | 0 | 0 | 2 | false | 0 |
| Cake04 | 1..5 | 2 | 0 | 1 | false | 0 |
| Cake05 | 0..0 | 0 | 0 | 1 | true | 4 |

Fresh composite table snapshots: `DT_PalMonsterParameter` 753 rows,
`DT_PalCombiUnique` 258 rows, `DT_PassiveSkill_Main` 1,905 rows,
`DT_WazaDataTable` 385 rows, `DT_WazaMasterLevel` 5,772 rows. These are table
contents, not counts of playable species. Composite exports include merged rows;
their runtime enumeration order is a separate input.

Use `f(x)` for IEEE float32 rounding, `r = rand() & 32767`, and
`c = float32(bits 0x38000100)` = 0.000030518509447574615. The observed
clamped index is:

```text
Index(n):                       # called only with a nonempty pool
    return min(trunc(f(f(f(r) * f(n)) * c)), n - 1)

WeightedIndex(weights):          # 0x142c93b70; callback 0x142c9d380
    retain positive weights with their original indices
    total = float32 sum of retained weights
    ticket = f(f(f(r) * c) * total)
    return first original index whose cumulative weight >= ticket
    return -1 if no entry qualifies
```

RNG formulas describe instructions, not
measured independence or live frequencies. Synthetic enumeration of all 32,768
scalar inputs gives `[13107,9830,6554,3277]` for `[4,3,2,1]` and
`[16384,10922,5462]` for `[3,2,1]`. Nominal normalized percentages are useful
approximations, not exact finite-grid probabilities.

## Child species

Scope: ordinary recipe/rank selector, item rank input, boss-form replacement,
and mutation selector through the generation save's CharacterID. The current inventory-farm hatch/live-save path is established in the
[shared boundary](#shared-child-save-boundary); the legacy delivery, pickup and extra-egg paths are connected below; the shipped first-load order is established below. Supplied pickup effects remain inputs; arbitrary activation frequencies are not inferred.

Retained native evidence: selector `0x142e70570`,
`ev_1dd305e7a8b2687006498757e9c18ccf691dfd8774258d98fac7f42a48e8f717`;
nearest-rank `0x142e713c0`,
`ev_60ca11e6c75540b73dc28550fb44c46473e3970ae629a375ec54cefb206de165`;
orchestrator `0x142e73970`,
`ev_8115ba3e817f957c73045c6a1dfad95705bae8bf6729271fce0bc6b25f3804f5`;
mutation `0x142e72ea0`,
`ev_c26d4b12ca58a8b37fac89f9d03a457a91ba83e3c3b1347535302c6645149006`.

```text
FindChild(A, B, effect):
    rows = recipe rows matching (A.Tribe,B.Tribe) in either order
    for row in rows, in enumerated order:
        each recipe side checks the gender of the parent with its Tribe
        a None gender requirement accepts any gender
        if both sides pass: return row.ChildCharacterID
    # The function also retains an unrestricted-row fallback. Such a row
    # already passes the preceding tests for valid matching tribes.
    rankA = character lookup(A.CharacterID).CombiRank, or 0 if absent
    rankB = character lookup(B.CharacterID).CombiRank, or 0 if absent
    bonus = clamp(effect.CombiRankBonus if effect exists else 0, 0, 10)
    sum = signed_int32(rankA + rankB)
    target = bonus - (CVTSS2SI(f(f(-0.5) - f(sum))) arithmetic_shift_right 1)
    return Nearest(target, excludeRecipeChildren=true)

Nearest(target, excludeRecipeChildren):
    result = None; bestDistance = 999999; tied = []
    for (id,row) in character table enumeration:
        skip if row.IsBoss or row.IgnoreCombi
        skip if excludeRecipeChildren and id occurs as a child in ANY recipe
        distance = signed absolute value(row.CombiRank - target)
        if distance < bestDistance:
            result=id; bestDistance=distance; tied=[id]
        else if distance == bestDistance: tied.append(id)
    if tied.count > 1:
        bestPriority = -999
        for id in tied:
            priority = lookup(id).CombiDuplicatePriority, or 0 if absent
            if priority > bestPriority: result=id; bestPriority=priority
    return result if not None else SheepBall

ordinary generation:
    id = FindChild(A,B,effect)
    if f(r*c) < settings.Combi_BossPalRate:
        replacement = FindRarePalNameByNomalName(id)
        if replacement != None: id = replacement
    child.CharacterID = id

mutation generation:
    a,b = parent ranks (lookup failure gives 0)
    m = min(a,b); d = abs(a-b)
    Q(x) = CVTSS2SI(f(x + 0.5)) arithmetic_shift_right 1
    n = max(Q(f(f(m)+f(m))*MutationRandomCoefficient),1)
    target = max(Index(n) + Q(f(f(m)+f(m))*MutationRankCoefficient)
                 + Q(f(f(d)+f(d))*MutationRankDiffPenalty) + 1, 1)
    id = Nearest(target, excludeRecipeChildren=false)
    if FindRarePalNameByNomalName(id) != None: id = that replacement
    child.CharacterID = id
```

`CVTSS2SI` obeys the processor rounding state; the ordinary ceiling-average
reduction assumes round-to-nearest-even, representable sums and no overflow.
The actual instructions at `0x142e70a19..0x142e70a32` are `ADD`, `CVTDQ2PS`,
load `-0.5f`, `SUBSS`, `CVTSS2SI`, `SAR 1`. The item clamp follows at
`0x142e70a34..0x142e70a58`. Synthetic sums 1..9999 match `ceil(sum/2)`.
This is not a proof for arbitrary int32 values or altered rounding modes.

The rare-form helper `0x142e71f20` scans rows for matching Tribe, IsBoss set,
IsTowerBoss/IsRaidBoss clear, and `BattleBGM != Predator`. It returns the first
qualifying row, not a name-prefix transformation. Fresh native observation
`ev_b8c9f846a783e30681bbf724d9a358c3ef5ab5170068c142eca99bfaa7218bb3`
tests bytes `+0xcd`, `+0xce`, `+0xd0 != 11`, Tribe `+0x2a` and IsBoss `+0xcc`.
Fresh reflection descriptor `0x1473eeeb0` binds BattleBGM to `+0xd0`;
enum record `0x1477c60f0` binds `EPalBattleBGMType::Predator` to 11.
Bool setters `0x141607b50` / `0x141607b60` write 1 to `+0xcd` / `+0xce`,
binding IsTowerBoss / IsRaidBoss. Current inventory-farm copies preserve the
selected ID; the legacy object-selection/delivery and pickup connections are established below.

Fresh data has two gender-constrained recipes: row 80, CatMage male + FoxMage
female → FoxMage_Dark; row 81, CatMage female + FoxMage male → CatMage_Fire.
Applying the recovered ordinary eligibility predicate to the fresh table gives
186 candidate rows. Equal `(rank,priority)` groups include SheepBall /
Quest_Farmer03_SheepBall, PinkCat / Quest_Farmer03_PinkCat, and PlantSlime /
PlantSlime_Flower. Thus table ordering and downstream replacement are material
dependencies; these observations alone do not establish a wrong live PalCalc pair.

Dependencies for later comparison: parent ID/Tribe/gender getters, recipe and
character lookup/iteration, global recipe-child membership `0x142e6b2b0`, item
effects, nearest-rank filtering/ties, boss replacement, mutation settings,
generation callers, and all later CharacterID writes.

### Species ordering: first surviving priority tie

The shipped first-load input is bound below; arbitrary runtime table edits are
outside that order guarantee. The nearest-rank body `0x142e713c0`,
`ev_60ca11e6c75540b73dc28550fb44c46473e3970ae629a375ec54cefb206de165`,
obtains character row names with `0x1451f68c0(characterTable, &names)` and
visits that returned array sequentially. It appends all equal-best-distance IDs
to the tie array in visitation order. Its decisive final selection is:

```text
bestPriority = -999
for id in ties_in_visitation_order:
    row = 142e721f0(database,id)
    priority = row ? int32(row+0x104) : 0
    if bestPriority < priority:
        result = id
        bestPriority = priority
```

Equal priority does not replace the earlier winner. No additional random draw,
lexical ID comparison or secondary sort appears in this tie-selection loop.
For tied candidates with a unique highest priority, their relative order cannot
change this winner; for several highest-priority candidates it can. The retained
table snapshot contains equal `(rank,priority)` groups already listed above,
including PlantSlime/PlantSlime_Flower and quest variants. That is not proof that
a specific parental pairing reaches such a tie or that those candidates have
identical final boss replacements.

Recipe selection `0x142e70570` obtains row names from the database's `+0x140`
table with the same `1451f68c0` helper, filters matching Tribe pairs into an array
in that order, then visits that array sequentially. Rare-form `142e71f20` obtains
names from the same character-table member `+0xd8` used by nearest-rank. Thus the
three consumers share the established row-name contract, with distinct tables
and predicates. Fresh selector/nearest observations reproduce their original
Evidence IDs above; recipe-child membership `142e6b2b0`,
`ev_f5975a806606b10345e6fa3b9b40ed17e899f322060f110c042781bbef06c3c1`,
also traverses recipe row names and tests `row.ChildCharacterID == queriedID`.

### Shipped species table binding and order

**Established 2026-10-27, Windows Steam base configuration, first load.** The
question is which ordered tables reach these consumers, not a universal order
after external table edits. The existing declared recipe/character inputs,
ordinary/item/mutation modes and saved-CharacterID output are unchanged.

The shipped `BP_PalGameInstance` default sets `DatabaseCharacterParameterClass`
to `BP_PalDatabaseCharacterParameter_C`. Its default sets:

```text
MonsterParameterDataTable = /Game/Pal/DataTable/Character/DT_PalMonsterParameter
PalCombiUniqueDataTable   = /Game/Pal/DataTable/Character/DT_PalCombiUnique
```

The database Blueprint derives directly from `PalDatabaseCharacterParameter`,
has no script functions in the decoded export, and supplies these table references.
The native class getter `1428de070` names that class, registers size `0x1b0` and
constructor `1428ded20`; the latter installs vtable `1473f8438`. The game-instance
construction branch in `142f1c2e0` checks the configured class at `+0x598`, creates
it through `142f11a50`, validates its class and stores the result at `+0x5a0`.
Accessor `1432df330` returns that `+0x5a0` database to the recipe selector.

Selected native bindings (descriptor names are ASCII pointers; each reflected
member offset is the uint32 at descriptor `+56`):

| Member / observation | Descriptor or procedure | Decisive value / Evidence |
| --- | --- | --- |
| `DatabaseCharacterParameterClass` | `147477980`, name `1474794e8` | `+0x598`; `ev_2c298a1ad19adfb801010e65efee9e1b1455ae4342415f4dccea1e88689a5fe2` |
| `MonsterParameterDataTable` | `1473f6370`, name `1473f6df8` | `+0xd8`; `ev_d6d04aea2450f01d1a8c760db1d868d5edce37b8e0eba756dda62c2d93d6d34d` |
| `PalCombiUniqueDataTable` | `1473f6780`, name `1473f6f68` | `+0x140`; `ev_af7b1d73cff6d55833642ffedc7471a1b7241e7012cc7f66055944af9b1048ee` |
| Named database class | `1428de070` | `ev_b61359434eb860c9578d619408136616ac507656056af0934a06409d5e4b1902` |
| Database constructor | `1428ded20` | `ev_69bda10e7b2923e09c203d44a75041e97a239e7bd3ed47ad1479c38b19b689f4` |
| Configured creation and owner store | `142f1c2e0` | `ev_21fb0fb0ca9f65af8fde73db310b81d66a6f2234abd2562073a621cc346e3408` |
| Database accessor | `1432df330` | `ev_3480e7408e8bf98a63f35927f0861b7ba56628b92b2e5796f5ca9990093172f8` |

Fresh extraction with the identified CUE4Parse/Oodle/mapping/helper pipeline gives:

| Decoded export | Type and relevant contents | JSON SHA-256 |
| --- | --- | --- |
| `DT_PalMonsterParameter` | CompositeDataTable, 753 rows; sole parent `DT_PalMonsterParameter_Common`; RowStruct `PalCharacterParameterDatabaseRow` | `b8fce52f84af279b5494a6de87ea6966aa412a5b00e555e0730a3b3ae5ad8599` |
| `DT_PalMonsterParameter_Common` | Plain DataTable, same RowStruct and identical 753 keyed rows **and key order** | `c17fe9072a640eea2461c5aa4933ba778c38a0514e5cad5c1a2ecfa117f4a2db` |
| `DT_PalCombiUnique` | Plain DataTable, 258 unique row names; RowStruct `PalCombiUniqueDatabaseRow` | `60798f86e4ce614f90202cce13f6d1f92c9116175a88ee16dc7bb0d72c429993` |
| Database class default | The two table references above | `3bdd5460cd07c66f3a23fd57f1cfd30e5c054c75839b886aa9d37f31c08b9517` |
| Game-instance class default | The configured database Blueprint above | `78942b1698dacf4f621e4092f7e413ef38e3fdad8f81fb08b2a762449ea4a75e` |

The character and recipe exports, and both class defaults, match their retained
copies byte-for-byte. Character keys are also unique ignoring case. This is a
fresh asset check, not an assumption that old numeric data is current.

The [native load and decoder bridge](#shipped-composite-first-load-order) applies
to these identified classes: `1451f82c0` reads serialized rows in sequence;
reservation `140eaeab0` consumes fresh slots in ascending order, and insertion
`1412d0f90` preserves first-seen distinct keys. Rehash changes links, not slots.
For this single-parent composite, `1451e5aa0` / `1451eb250` clear and rebuild in
parent slot order through `1451f22a0`; the own and parent serialized orders agree.
`GetRowNames1451f68c0` / `1412d17b0` then traverse occupied slots ascending.
The same identified decoder ordinary DataTable branch and JSON writer retain
that serialized order. This closes the input-to-consumer argument; it does not
infer native order from JSON enumeration alone. Later external mutations of the
tables are outside the declared unmodified first-load configuration.

### Concrete species ties, recipes and self-pairs

Applying the recovered eligibility predicates gives **186 ordinary** and **263
mutation** candidates. Mutation keeps the recipe children excluded by ordinary
nearest-rank selection. Across valid Tribe-pair/gender combinations (including
None and both parent orientations), the 258 current recipes have **no overlapping
matches with different child IDs**. Their first-match rule remains part of the
model, but recipe row order does not distinguish outputs in these current inputs.
The two gender-constrained recipes remain CatMage male/FoxMage female →
FoxMage_Dark and CatMage female/FoxMage male → CatMage_Fire.

These are the complete equal-highest-priority nearest-rank ties in a synthetic
target check of integers 1–10,000. The target range is a model check, not a claim
about the mutation RNG's reachable range. Indices refer to the 753-row character
table before filtering; all six rows are nonboss and `IgnoreCombi=false` and are
not recipe children.

| Candidate IDs in visitation order (zero-based row indices) | Rank / priority | Ordinary target interval, inclusive | Mutation target interval, inclusive | Winner |
| --- | --- | --- | --- | --- |
| PinkCat (399), Quest_Farmer03_PinkCat (681) | 2760 / 276000 | 2750–2764 | 2755–2764 | PinkCat |
| PlantSlime (474), PlantSlime_Flower (475) | 2950 / 295000 | 2945–2954 | 2945–2954 | PlantSlime |
| SheepBall (378), Quest_Farmer03_SheepBall (680) | 3050 / 305000 | 3045–3054 | 3045–3054 | SheepBall |

For checking those interval edges, the adjacent eligible ranks/priorities are
FlyingManta 2740/274000, Bastet_Ice 2750/275000 (recipe child, mutation only),
NegativeOctopus 2770/277000, WizardOwl 2940/294000, Penguin 2960/296000,
Bastet 3040/304000 and CuteFox 3060/306000. At equal distance the higher priority
wins, explaining the asymmetric inclusive interval endpoints. An independent
`(distance, -priority, visitation index)` minimization agrees with the recovered
loop for all checked targets in both modes. This checks the reconstruction; the
native branches and ordered inputs above establish its premises.

The rare-form predicate has only two multi-row Tribe matches in this table:

| Tribe | Qualifying IDs in visitation order (row indices) | Result |
| --- | --- | --- |
| PlantSlime | BOSS_PlantSlime (147), BOSS_PlantSlime_Flower (148) | BOSS_PlantSlime |
| WhiteAlienDragon | BOSS_WhiteAlienDragon (213), SUMMON_WhiteAlienDragon_MAX (564) | BOSS_WhiteAlienDragon |

All four rows have IsBoss=true, IsTowerBoss=false, IsRaidBoss=false and
BattleBGM other than Predator. `IgnoreCombi` does not enter this replacement
predicate. Both PlantSlime variants share Tribe PlantSlime, so either maps to
the same first rare form. Mutation applies that replacement whenever available;
ordinary generation requires its separately recovered boss-roll branch.

**Distinguishing specified-parent case:** take two saved parents with
CharacterID `PlantSlime_Flower`, either opposite gender order, ordinary nonmutation
generation, and CombiRankBonus=0. Both parent ranks are 2950 and both Tribes are
PlantSlime; no current same-Tribe recipe matches. The normal target is 2950.
PlantSlime and PlantSlime_Flower tie at distance zero and priority 295000, so the
saved selection is **PlantSlime**. If the ordinary boss-conversion branch succeeds,
the saved ID is **BOSS_PlantSlime** instead. Neither branch selects the Flower ID.
The retained [save/pickup/hatch transfer](#shared-child-save-boundary) preserves
that generated CharacterID; alpha pickup conversion preserves a nonempty child
save, and extra-save transformation does not replace its CharacterID. This is a
conditional consequence for the specified saved parents, not a live breeding test
or proof of how a player obtains those parents. The two Quest_Farmer03 self-pairs
likewise select their base IDs under those ordinary no-bonus conditions.

This resolves the October 23 exact-ID/order limitation for the declared shipped
tables. Arbitrary table edits, modified rounding modes, unconditional effect-source
frequencies and exact joint RNG replay remain unclaimed. No solver implementation
was inspected or changed to turn this finding into a claimed code fix.


## Child gender

Scope: ordinary helper and mutation's inline assignment through child-save
gender byte `+0x10`. Parent recipe gender checks are a different operation.

Retained helper `0x142e89200`,
`ev_074da2527500a01a41bfe608b6a3bbca95e865f4eb8040f0c636f18de79c932e`:

```text
Gender(id):
    row = FindCharacter(id)
    if row absent or row.MaleProbability < 0: return None (0)
    p = row.MaleProbability                    # signed int32, percent
    if p == 100: return Male (1)
    if p == 0: return Female (2)
    x = f(f(rand() & 32767) * float32(0.003051851...))
    return Female if p <= x else Male
```

The helper's decisive branches are `p<0`, `p==100`, `p==0`, then
`return (p <= float(r)*0.003051851) + 1`. Ordinary orchestration calls this
after selecting the final generation species and stores its result in
`param_6+2` (an eight-byte-pointer expression, byte offset `0x10`). Mutation
contains the same test inline, then calls common initialization `0x142e73be0`.
That common initializer does not itself assign this gender byte in the retained
body. The hatch and pickup models below establish preservation in the declared routes.

Fresh `MaleProbability` values include 0, 100 and -1, plus ordinary percentages
such as 50. Ratios are species data, not recipe gender requirements. Fresh reflection descriptor `0x1473ef410` corroborates MaleProbability at row
`+0xfc`, observation `ev_3bdf538054cdcd6381a2bb773640a0485dfd40f9c63a319e13079a6da3212b85`.
The shared current-farm save path preserves the assigned gender. The legacy
egg and extra-egg grant models below establish preservation too;
child gender is Verified for the declared base-game routes.

Dependencies: character row identity/offset/type, gender enum identity, exact
threshold constant/instructions, ordinary and mutation assignment, spawn/hatch
callers, overrides and later saved gender writes.

## Child passives

Scope: ordinary and positive-override generation, random filling, mutation
composition, and generation writes to `PassiveSkillList` (`+0x90`).

Retained native evidence: ordinary `0x142e8f8a0`,
`ev_58814c40caab02d1a05917d5b85933e552673bd80ac3d4c3f9115148a15aacdd`;
filler `0x143150be0`,
`ev_c877379cee7b1b03bdf4f8dd4e46009e4404df735c885d921770616b49b6fb94`;
pool initialization `0x1431538a0`,
`ev_b6512d87ba3ec4be885c3508889c3618358a62c6780c412d93c8f61972bb0468`;
mutation body cited under species.

```text
OrdinaryPassives(A,B,effect):
    pool = ordered_unique(A.PassiveSkillList + B.PassiveSkillList)
    result = []
    if effect.PassiveInheritCountOverride > 0:
        target = min(effect.PassiveInheritCountOverride,4)
        take up to target pool entries using Index and removal
        result += RandomFill(exclude=result, attempts=target-result.count)
    else:
        target = WeightedIndex(Combi_PassiveInheritNum)+1
        take up to target pool entries using Index and removal
        if result.count < 4:
            k = WeightedIndex(Combi_PassiveRandomAddNum)
            if k >= 0: result += RandomFill(result,min(k,4-result.count))
    child.PassiveSkillList = result

RandomFill(exclude,attempts):
    keys = manager.AddPal_keys_in_runtime_order
    local = copy(manager.original_weights); total = manager.original_total
    remove excluded keys and corresponding LOCAL weights; subtract their weights
    result=[]
    repeat attempts times:
        if keys empty: stop
        ticket = rand() % total
        for i in local indices:
            if ticket < local[i]:
                result.add_unique(keys[i])
                total -= local[i]; remove keys[i],local[i]
                break
            ticket -= manager.original_weights[i] # different, unfiltered vector
        # Failure to select consumes this attempt too.
    return result

MutationPassives(A,B,effect):
    if effect.PassiveInheritCountOverride <= 0:
        result = unique(nonempty draw from AddMutationPal,
                        nonempty draw from AddPal-and-Rank>3)
        pool = ordered_unique(A.passives+B.passives) minus result
        take distinct parents using Index/removal until four or pool exhausted
        result += RandomFill(result,4-result.count)
    else:
        result = OrdinaryPassives(A,B,effect)
        rankChoice = draw AddPal-and-Rank>3 excluding result
        mutationChoice = draw AddMutationPal excluding result and rankChoice
        prepend rankChoice if present; prepend mutationChoice if present
        truncate result to four
    child.PassiveSkillList=result
```

Mutation/rank draws use the distinct selector chain `0x1431509b0` /
`0x1431509f0` → `0x143150a80`; exclusions are skipped in their original index
space. Do not transfer the ordinary filler's mismatch to those draws.
The ordinary filler compares through a local vector but subtracts from
`*(param_1+0x400)+i*4`. After removals these can differ. Synthetic original
weights `[1,3,5]`, excluding the first key, ticket 7: compare 3, subtract original
1, then fail against 5. Conventional subtraction would select the second key.
This demonstrates reachability, not its live frequency or manager order.

Fresh AddPal pool: 85 rows, 73 weight 100 and 12 weight 5 (total 7360).
AddMutationPal: five rows of weight 100. Pool construction separately creates
AddRarePal and AddWorldTreePal pools; those flags aren't requirements for the
ordinary pool. Parent inheritance doesn't apply AddPal or LotteryWeight filters.
Cake05's override 4 bypasses both count rolls. Four distinct parental candidates
therefore fill its ordinary branch directly; fewer candidates rely on random
attempts and can leave a shortfall. Mutation prepends can displace parent entries.

Dependencies: parent/save array layouts and FName identity, all count selectors,
manager construction/key order, filtered/original vectors, integer lottery,
item override, mutation selectors/composition, callers and later passive writes.

### Passive ordering: established construction and remaining input

Native container/load checks and the identified decoder establish initial
alignment and the ordered shipped first-load pools. The post-removal filler
mismatch has a concrete current-data witness below. Later table edits and modded
inputs are outside this unmodified first-load claim.

The shipped manager CDO selects DT_PassiveSkill_Main, a CompositeDataTable with
one parent, DT_PassiveSkill_Main_Common. Both decoded dictionaries have the same
1,905 keyed rows. AddPal admits 85: 73 weight-100 and 12 weight-5 entries, total
7,360. Equal decoded contents do not prove equal runtime ordering.

Initializer 0x1431538a0 visits row names returned by 0x1451f68c0. Its original
accepted-row operations remain:

```text
143140d40(manager+0x118, ..., rowNameAndRow)   # insert ordinary-pool entry
manager.total_at_0x3f8 += row.LotteryWeight
i = manager.weight_count_at_0x408++
manager.weights_at_0x400[i] = row.LotteryWeight
```

0x1451f68c0 calls 0x1412d17b0 on table +0x30. The latter walks allocated bits
in ascending sparse-slot order, reads FName keys from 0x18-byte row-map entries,
and appends each first-seen key to the result. Its temporary seen-key set removes
duplicates without sorting the output. Row order is therefore the existing
row-map element order, not alphabetical order or hash-bucket traversal.

Pool insertion 0x143140d40 allocates the next element index when its free count
(+0x34) is zero, otherwise reuses a free slot. Copy 0x14313ef20 stores the FName
at entry +0 and copies row.LotteryWeight (+0x0c) to pool-entry +0x14. Entries have
stride 0x80. Growth 0x140e89550 reallocates that element buffer; rehash
0x142eed790 rebuilds buckets and entry links +0x78/+0x7c without moving entries.
Duplicate handler 0x14316ebd0 replaces an existing equal-FName entry and frees the
new temporary slot. This branch cannot reorder distinct first-population keys:
the row-name helper already returned unique names.

Pool key extraction 0x143141690 also walks allocated slots in ascending order,
appending first-seen FNames with a temporary seen-key set. It does not enumerate
hash chains or sort the keys. Thus, starting with empty pools and weight arrays:

```text
accepted rows = filter(GetRowNames(table), pool predicate)
for row in accepted rows:
    append a distinct pool entry at next sparse slot
    append row.LotteryWeight to that pool's weight vector
keys = pool entries in ascending allocated-slot order
# keys[i] and weights[i] describe the SAME accepted row before removals.
# Rehashing changes bucket links, not this key/weight alignment.
```

The concrete owner initializer 0x142f1c2e0 passes its configured manager class
(+0x5c8) to NewObject helper 0x142f11a50, stores the object at +0x5d0 and calls
0x1431538a0. Getter 0x142a67310 names PalPassiveSkillManager and selects
constructor thunk 0x142a67ba0; bytes at thunk +0x0e jump to 0x142a67cf0.
That constructor zeros the ordinary pool's element/free counts and weight/total
storage (+0x118 map, +0x3f8 total, +0x400 weights), and the other pool storage.
This supports the fresh first-population contract; it is not a claim about
reinitializing an already populated or externally changed manager.

The same insertion/key-extraction contract applies to the initializer's rank>3
and mutation pools. Their distinct selector 0x143150a80 skips excluded keys in
the original index space and uses the corresponding original weight there.
The ordinary filler's different-vector subtraction must not be transferred to
those selectors.

Order matters even when initial alignment is established. The filler
`0x143150be0`,
`ev_c877379cee7b1b03bdf4f8dd4e46009e4404df735c885d921770616b49b6fb94`,
compares compacted weights but subtracts original-index weights after exclusions.
Two synthetic aligned arrangements of the **current multiset** (73 weights of
100, 12 weights of 5) illustrate the sensitivity:

| Assumed original order; exclude its first entry | Remaining total | Successful integer tickets | Failed integer tickets |
| --- | ---: | ---: | ---: |
| 73×100, then 12×5 | 7260 | 7260 | 0 |
| 12×5, then 73×100 | 7355 | 7260 | 95 |

For each case, enumerate every ticket `0..total-1` for one attempted addition,
compare `ticket < local[i]`, otherwise subtract `original[i]`. These are invented
orders/exclusions with shipped weight values, not observed runtime snapshots or
named-parent examples. The ticket counts are not measured frequencies; `rand()%total`
also need not distribute tickets uniformly. They demonstrate that order can change
achieved count, not just which named passive is selected. They do not quantify ordinary live failure incidence; an order-independent
integer-ticket bound is derived below.


#### Shipped composite first-load order

The ordinary, rank>3 and mutation candidate orders below are reconstructed for
the unmodified shipped tables at first load. This is a static load-path/data
reconstruction, not a runtime snapshot or a claim about later table edits.

Class getter 0x1451c4d50 names CompositeDataTable; constructor 0x1451c7870 installs
vtable 0x1480dd048 after DataTable construction. Parent getter 0x1451f65d0 names
DataTable; its registered constructor 0x1451efde0 installs 0x1468b80c8. Property
descriptor 0x1480d2010 names ParentTables and binds +0xb0. The shipped composite
has exactly one parent, DT_PassiveSkill_Main_Common, with the same row-struct path.
Both freshly re-extracted serialized row dictionaries have 1,905 equal keys,
values and key order. This equality is used with the load contracts below, rather
than being treated as an ordering proof on its own.

The ordinary DataTable archive path 0x1452079a0, on the archive loading branch,
invokes virtual +0x2e0 to empty the table and 0x1451f82c0 to load rows. The latter
reads the serialized count, reserves the row map, and repeatedly reads a FName,
allocates/deserializes its row, then inserts it through 0x1412d0f90. The row map
is table +0x30. Its getters at slots +0x2c8/+0x2d0 share 0x140ea7140, whose bytes
`48 8d 41 30 c3` return that address.

Reservation 0x140eaeab0 creates free slots from the highest new index down to the
lowest, pushing each onto the free list. A fresh count-N reservation therefore
has free-list order 0,1,...,N-1. Insertion 0x1412d0f90 consumes that head; new
keys consequently occupy ascending slots in serialized order. Duplicate keys
replace the old slot and free the temporary slot. Rehash 0x140c90fa0 changes
buckets and entry links, not element positions. Thus first-seen key order is
preserved, including when reservation occurred before the loop.

Composite slot +0xd0 is 0x1451e5aa0. It sets flag +0xd0 bit 0 on the loading
branch, delegates to DataTable serialization, ensures the parents are loaded,
and invokes 0x1451eb250 under its reentrancy guard. Slot +0xc8 at 0x1451e5e00
adapts its archive argument and jumps to that same body. Slot +0xa0 at
0x1451dfd60 clears the loading bit before its base call. Composite empty-table
slot +0x2e0, 0x1451d6830, retains ParentTables while the loading flag is set and
then calls 0x1451f57e0 to clear row storage.

The checked noncyclic rebuild 0x1451eb250 clears the destination row map via
0x1451f57e0 and visits ParentTables in array order. For a parent with matching
row struct it walks the parent's allocated row slots in ascending order and
copies each non-null row through 0x1451f22a0. Assembly 0x1451ebbbf loads its row
pointer into R8, 0x1451ebbc9 loads the key into RDX, and 0x1451ebbcd puts the
destination in RCX before the call at 0x1451ebbd0. The decompiler's two-argument
summary of that call is not used to omit the row data.

0x1451f22a0 removes an existing destination key through virtual +0x2c0
(0x145203da0), allocates/copies the supplied row, and inserts through +0x2b8
(0x1451f23f0 -> 0x1412d0f90). After a clear, this single-parent table has unique
keys, so copying preserves parent slot order. General multiparent duplicate
precedence or runtime parent-edit behavior is not substituted for this setup.
The composite's own serialized order and its sole parent's order agree here;
retaining the loaded rows and the checked rebuild therefore give the same
ordered input. The fresh manager subsequently filters it without reordering,
as established above.

```text
common row payload -> DataTable reserve/free-list -> first-seen serialized key order
composite single-parent rebuild -> clear -> copy those parent keys in slot order
GetRowNames -> accepted rows in that order
fresh manager insertion + weight append -> aligned ordered candidate pools
```

The decoder/output bridge was checked against the identified CUE4Parse.dll,
not assumed from JSON property order. Its UDataTable.Deserialize method token
0x0600158e has IL SHA-256
`f071d3624ee0781b65e7871270e110c7cfe18631f0b5a2f15c152de9c8fcb57d`.
The helper supplies GAME_UE5_1=83,951,616, excluding the two special game branches
68,812,828 and 68,878,360. On its ordinary path the IL reads count at 0x125,
creates the dictionary at 0x12d, reads each FName at 0x142, assigns the row at
0x173, and loops at 0x181 to 0x13c. WriteJson token 0x0600158f passes RowMap
to JsonSerializer.Serialize without a sort. A 1,905-key fixture through the exact
serializer assembly, including replacing an existing key, reproduced first-
insertion order under .NET 10.0.11. Fresh extraction of both tables reproduced
the retained file hashes and order. The fixture checks the decoder/output
pipeline; native slot preservation is established separately above.

| Decoding input | SHA-256 |
| --- | --- |
| DT_PassiveSkill_Main-DT_PassiveSkill_Main.json | `ce22ba86cf0a461289f380d9bb12dcbc6b73a003b217bd678ad37cf76d472eab` |
| DT_PassiveSkill_Main_Common-DT_PassiveSkill_Main_Common.json | `a405785eae0c0e246f248dc9c246db024213bab48c1e87053f873af6dcf7cb91` |
| CUE4Parse.dll | `98154fafa9ba4f2d0ae9ffe9e18de9fc1242bd4507ed711bbf86234cd46947ef` |
| Newtonsoft.Json.dll | `a28c251dfe36d881e9e2462e171441b8b0ec156fe3f452602c9149b1b9efe05b` |

The ordinary pool's complete ordered snapshot is below (zero-based indices).
The twelve weight-5 rows are exactly its rank>3 subpool, all Rank=4, in this
same relative order. Total ordinary weight is 7,360; rank>3 total is 60.

| Index | Passive key | LotteryWeight |
| ---: | --- | ---: |
| 0 | CraftSpeed_up3 | 5 |
| 1 | CraftSpeed_up2 | 100 |
| 2 | CraftSpeed_up1 | 100 |
| 3 | CraftSpeed_down1 | 100 |
| 4 | CraftSpeed_down2 | 100 |
| 5 | Deffence_up3 | 5 |
| 6 | Deffence_up2 | 100 |
| 7 | Deffence_up1 | 100 |
| 8 | Deffence_down1 | 100 |
| 9 | Deffence_down2 | 100 |
| 10 | Noukin | 100 |
| 11 | PAL_ALLAttack_up3 | 5 |
| 12 | PAL_ALLAttack_up2 | 100 |
| 13 | PAL_ALLAttack_up1 | 100 |
| 14 | PAL_ALLAttack_down1 | 100 |
| 15 | PAL_ALLAttack_down2 | 100 |
| 16 | PAL_rude | 100 |
| 17 | PAL_conceited | 100 |
| 18 | PAL_sadist | 100 |
| 19 | PAL_masochist | 100 |
| 20 | PAL_CorporateSlave | 100 |
| 21 | PAL_oraora | 100 |
| 22 | TrainerATK_UP_1 | 100 |
| 23 | TrainerDEF_UP_1 | 100 |
| 24 | TrainerWorkSpeed_UP_1 | 100 |
| 25 | TrainerMining_up1 | 100 |
| 26 | TrainerLogging_up1 | 100 |
| 27 | ElementResist_Normal_1_PAL | 100 |
| 28 | ElementResist_Fire_1_PAL | 100 |
| 29 | ElementResist_Aqua_1_PAL | 100 |
| 30 | ElementResist_Thunder_1_PAL | 100 |
| 31 | ElementResist_Leaf_1_PAL | 100 |
| 32 | ElementResist_Ice_1_PAL | 100 |
| 33 | ElementResist_Earth_1_PAL | 100 |
| 34 | ElementResist_Dark_1_PAL | 100 |
| 35 | ElementResist_Dragon_1_PAL | 100 |
| 36 | ElementBoost_Normal_1_PAL | 100 |
| 37 | ElementBoost_Fire_1_PAL | 100 |
| 38 | ElementBoost_Aqua_1_PAL | 100 |
| 39 | ElementBoost_Thunder_1_PAL | 100 |
| 40 | ElementBoost_Leaf_1_PAL | 100 |
| 41 | ElementBoost_Ice_1_PAL | 100 |
| 42 | ElementBoost_Earth_1_PAL | 100 |
| 43 | ElementBoost_Dark_1_PAL | 100 |
| 44 | ElementBoost_Dragon_1_PAL | 100 |
| 45 | PAL_FullStomach_Up_1 | 100 |
| 46 | PAL_FullStomach_Up_2 | 100 |
| 47 | PAL_FullStomach_Down_1 | 100 |
| 48 | PAL_FullStomach_Down_2 | 100 |
| 49 | PAL_FullStomach_Down_3 | 5 |
| 50 | PAL_Sanity_Up_1 | 100 |
| 51 | PAL_Sanity_Up_2 | 100 |
| 52 | PAL_Sanity_Down_1 | 100 |
| 53 | PAL_Sanity_Down_2 | 100 |
| 54 | PAL_Sanity_Down_3 | 5 |
| 55 | MoveSpeed_up_1 | 100 |
| 56 | MoveSpeed_up_2 | 100 |
| 57 | MoveSpeed_up_3 | 5 |
| 58 | NonKilling | 100 |
| 59 | Nocturnal | 100 |
| 60 | Test_PalEgg_HatchingSpeed_Up | 100 |
| 61 | CoolTimeReduction_Up_1 | 100 |
| 62 | CoolTimeReduction_Up_2 | 100 |
| 63 | CoolTimeReduction_Down_1 | 100 |
| 64 | Stamina_Up_1 | 100 |
| 65 | Stamina_Up_2 | 100 |
| 66 | Stamina_Up_3 | 5 |
| 67 | Stamina_Down_1 | 100 |
| 68 | SalePrice_Up_1 | 100 |
| 69 | SalePrice_Up_2 | 100 |
| 70 | SalePrice_Down_1 | 100 |
| 71 | Vampire | 5 |
| 72 | SwimSpeed_up_1 | 100 |
| 73 | SwimSpeed_up_2 | 100 |
| 74 | SwimSpeed_up_3 | 5 |
| 75 | Deffence_up2_2 | 100 |
| 76 | NightOwl | 100 |
| 77 | SelfDeathAddItemDrop_up_2 | 100 |
| 78 | SelfDeathAddItemDrop_up_3 | 5 |
| 79 | WorkSuitabilityAddRank_MonsterFarm_1 | 100 |
| 80 | WorkSuitabilityAddRank_MonsterFarm_2 | 5 |
| 81 | RideJumpCount_Increase1 | 5 |
| 82 | PlayerSP_DecreaseRate_Passive | 100 |
| 83 | AutoHPRegeneRate_Passive | 100 |
| 84 | ReloadSpeedUp_Passive | 100 |

AddMutationPal is a separate ordered pool, total weight 500:

| Index | Passive key | LotteryWeight |
| ---: | --- | ---: |
| 0 | MutationPal_Babysitter | 100 |
| 1 | MutationPal_Mutant | 100 |
| 2 | MutationPal_Immortal | 100 |
| 3 | MutationPal_ExplosionResist | 100 |
| 4 | RideJumpCount_Increase2 | 100 |

#### Concrete current-data fill shortfall

For the first fill attempt with CraftSpeed_up3 already selected, removing that
ordinary-pool key leaves total 7,355. Applying the signed-ticket interval model
to the recovered current order gives:

| Outcome | Integer ticket interval | Ticket count |
| --- | --- | ---: |
| CraftSpeed_up1 (row weight 100) | [100,105) | 5 |
| Deffence_up2 (row weight 100) | [310,505) | 195 |
| WorkSuitabilityAddRank_MonsterFarm_2 (row weight 5) | empty | 0 |
| No selection | [7260,7355) | 95 |

The complete candidate intervals were checked against direct enumeration of all
7,355 tickets. Conventional weighting would assign the first two named rows 100
tickets each and have no failed ticket. This establishes material bias and a
shortfall witness using current ordered inputs. It is not a uniformly
sampled-ticket or live-frequency claim.

For example, take ordinary nonmutation generation with Cake05's override 4 and
a parental union containing only CraftSpeed_up3. That passive is selected, then
three fill attempts are requested. A first ticket in [7260,7355) adds nothing;
the two remaining attempts can add at most two passives, so the generated list
has at most three. The established ordinary save/hatch transfer preserves that
list. No extra-egg/source-activation assumption is needed for this primary-egg
witness. No RNG independence or exact seeded encounter claim follows.

| Decisive load/order observation | Evidence ID |
| --- | --- |
| Composite class / constructor | `ev_de85710175576368b10b465a51873daf833ee9724a8a635c616da1aaf7cf8c7b`, `ev_173b0e4f44b599b1cb598ffa7845a3a109b75f62ddcfcc55e7b5f5dbce6e5200` |
| DataTable class / constructor | `ev_d9d53b444e4c998959b37b84708aca590b381e1f6a52fc9fc8a1c97068391c9e`, `ev_0410ba766d66f4cf51c849e9affc1c6ba81884bce0e9f5074c320cf3bd977abe` |
| Composite archive body / adapter / loading-bit clear | `ev_0d017a7d74d422368f0e5ee4c3241b952801237f9e77482b8eeb4287e53af13c`, `ev_b2673b8033e35e585c6849eafeea26ac1525e2806e4c4419ff2c1a8c6311c2d2`, `ev_43b9d25562a1c2cb5fbdc59e1c97df139cb880072bf9c73febe8557c4bc1e9a4` |
| ParentTables field / row-map getter | `ev_f94572608b346dde07f404e2528d489ba5e510e650e66d875a23539b423b8692`, `ev_95a3573739d8d7f30ff5cee5b8a2a7283cd915b6579fd0c5c404c3aaf9e45cb9` |
| Composite empty / rebuild / argument forwarding | `ev_3d1de3e78a7de98aad9c58827b43bcb9fcac78e561ca6950b189db91db93f811`, `ev_1121be2cee3c2fac4673b34e2222be50181b962c49e11d7d778002a9828308de`, `ev_6cf5086b3ed96b4bbafb109374a59ad047bc566c8e57504880a22699790124f1` |
| DataTable load / reserve / rehash | `ev_cc5f1209f3a21e4d2247220e96c7d23593cb00058e8033926363af86e3f5a9e5`, `ev_65503cc6837b374fe9f1926edd92099ec95c775999bba84002b5f0e21f71dd2d`, `ev_14a743f8b0de5e266654042798aecfdc7adc3fd7fc954d84184134179b37cb6f` |
| Row copying / insertion / removal / clear | `ev_74da9e550a36c21eb66aa35d219929ea9d6c0335178b42e559df8e412ae7b37e`, `ev_9e3007611eddb57a1408006646d5e3e662848a4da54791e62406cb413c6b2bca`, `ev_c51769e31a8f9b657e6fe5f0c41489ecff8e945d997b6291f978ca3cc5b1915c`, `ev_23bd63fff479f3a3333a1096df8cd8bdf29af9dc2f2858e065677ca45d1bc286`, `ev_cf3399592c0ade4a71a3e7e60a7664a7f8fc2605ad5fb3bbaadf822a3ce1787c` |
| Decoder method bodies and call edges | `ev_3570417d9bd6681b5663b744f88e8f4d088c57435c8fad5f9ae6f88bd8ee852e` |

#### Exact ticket intervals and a bound without fixed row order

Fresh filler 0x143150be0 confirms the signed comparison `(int)ticket < local[i]`
and subtraction from manager +0x400 at the original index. Removal compacts local
keys/weights together and reduces the total. With valid aligned initial data,
let O be original weights, L the surviving weights and T=sum(L). For one attempt:

```text
q[i] = sum(O[0:i]) + L[i]
c[-1] = 0
c[i] = min(T, max(c[i-1], q[i]))
integer tickets selecting candidate i = [c[i-1], c[i])
failed integer tickets = T - c[last]
```

This follows because a ticket reaching i has residual t-sum(O[0:i]); prior
acceptance thresholds determine which tickets can reach it. It models the
mismatched subtraction rather than replacing it with conventional weighting.
It also captures zero-width candidate intervals and bias even when no ticket
fails. For the shipped weights, if ell removed entries have weight 5, failures
are at most 95*ell. To see this, the last candidate alone bounds failures by
`max(0, sum(L[:-1])-sum(O[:len(L)-1]))`. Replacing deleted prefix entries by later
survivors cannot increase the sum for a removed 100; each removed 5 can increase
it by at most 95. Hence removing only weight-100 entries causes no failed integer
tickets, although identities can still be biased.

A child filler attempt under the recovered four-passive cap has at most three
removed ordinary-pool entries, counting initial exclusions and previous successful
additions. Therefore at most 285 integer tickets fail in one such valid attempt.
An invented order of 12x5 then 73x100, excluding its first three entries, attains
that bound: T=7,345, 7,060 successful and 285 failed tickets. This is a bound on
candidate selection, not an observed per-child failure rate or a claim that the
game uses this order. Modulo draws need not make tickets equally likely.

The interval formula matches exhaustive ticket enumeration for 260 five-entry
permutation/exclusion cases and the full 85-entry examples above. These synthetic
checks validate the reconstruction's arithmetic, separately from native evidence.

The row-map/fresh-manager contracts, decoder bridge and ordered snapshots establish
the shipped first-load inputs for the declared per-egg saved-content calculation.
Later table edits, unconditional source activation, live frequencies and joint RNG
independence remain unclaimed. Future version checks must compare native load/
construction dependencies as well as table values.

| Decisive observation | Evidence ID |
| --- | --- |
| Row-name wrapper / row-map walk | `ev_bd0ec64011aac57d1955d761abd65a8de8fa888c69f4f97a69bc17455e754ae2`, `ev_e0a5f3328b2e1dc7e1b1beac44139d5838ed446d3265721d791164e22a8df758` |
| Pool insertion / row copy | `ev_0d64e5ffa7bbe91735dbeaf1eb918d2b986e372d8ea9a5e833639e8c5a64143d`, `ev_558b3d87ebf759ab5f8538a55087b56f378db0bd51b1f8fb8c0f0ecbfc41f83e` |
| Duplicate resolution / rehash / growth | `ev_1762ffeda7320f21181e67bf15d56b385e70a4ea96efd1f5b02de128f4b185ed`, `ev_9a2a93126405f9765c880735c718e9cf0c82731e5ae6d755dea83b69ab3de8ab`, `ev_79bebadf53000d55b57a7fe69885abc0fc26bcf388055f3b532d8da9606ce27e` |
| Pool key extraction / signed filler | `ev_00eab53a1a17d17f0e69d4fb007af199a7df6ef306e96b98691013c9eede665e`, `ev_c877379cee7b1b03bdf4f8dd4e46009e4404df735c885d921770616b49b6fb94` |
| Owner initialization / creation / manager class | `ev_39414662eb7132c3f87ba803fd593609fd863f96b5b9a5411496dc10be67b268`, `ev_9bbb31c5dca692bc87dcfe49ed1f5b47ab734bedfba74004a569124224a6f7ba`, `ev_3eb6eee1ff13a1810748b6fa1ddc936aac2d5c52930b4b3861563ad82ec671f7` |
| Constructor thunk bytes / body | `ev_3940899b09b3908a1f21e3d9335ca42f5eef25c380f4ff02404deefab345292f`, `ev_207ace8aad1044a3658319f6adf027eba048edbedecb7aa126411b9066740744` |


## Child attacks

Scope: equipped-parent inheritance, level-one lookup, item override and mutation
grant through generation's `EquipWaza` and `MasteredWaza` writes. Derived learned availability is established below; the shared current-farm
hatch path preserves both stored arrays. The legacy and extra-egg models below
establish preservation through those paths; child attacks are Verified.

Retained native evidence: `0x142e91200`,
`ev_7b460f49258830decb1a0302368a7fbc5e7dbc876741febb543d74af7121a49a`;
parent getter `0x142f7aca0`,
`ev_cd86b158243fe0cf7b7bfe0f975252662c833ec46896ba028771132aa3e51b21`;
filter `0x143339fb0`,
`ev_afa6126adc15e48d7f1af3cc62a37d4feea125107a57ce239c736f8b2882518f`;
mutation extra `0x142e66ae0`,
`ev_253f30ca2b47e5bf0745e1fdd39c02091ef8ba342c8c2db370ef220e84e39110`.

```text
eligible(w) = FindWaza(w) exists and not row.IgnoreRandomInherit
pool = ordered_unique(eligible(A.EquipWaza + B.EquipWaza))
mastered=[]; starting=[]
if pool nonempty:
    w=pool[Index(pool.count)]; mastered.add_unique(w); starting.add_unique(w)
for (w,level) in child's masterable map:
    if level==1 and w!=None: starting.add_unique(w)
if effect.bInheritAllActiveSkills:
    for w in A.EquipWaza followed by B.EquipWaza:
        if eligible(w): mastered.add_unique(w); starting.add_unique(w)
child.MasteredWaza=mastered
child.EquipWaza=starting.first(3)

if mutation:
    element = character(child.CharacterID).ElementType1, or None if absent
    extraPool = table entries where Element==element and Strength==Strong
                and not IgnoreRandomInherit and not DisabledData
    if extraPool nonempty:
        w=extraPool[Index(extraPool.count)]
        child.MasteredWaza.add_unique(w)
        if w not equipped:
            append w if fewer than three equipped, else replace last slot
```

The ordinary filter returns blocked for a missing row or `row+0xb != 0`.
It does not separately test DisabledData; all nine currently disabled rows are
already excluded by IgnoreRandomInherit. Fresh data has 103 inheritable and 282
excluded WazaTypes. There are 30 eligible Strong mutation rows; no duplicate
WazaType rows in the fresh table.

`0x1433344f0` and callback `0x14332d990` resolve learning-row PalId through
`PalReplaceSetting.CharacterIDReplacer` (`0x1432de870`), compare against child
CharacterID, and insert WazaID→Level, replacing an existing key's value.
Fresh packaged `DefaultGame.ini` (41,219 bytes, SHA-256
`77995f1902973c7534747cbae4b38334902858e49aa844d9af34c9d6e525add8`)
contains this configured input at lines 144–146:

```ini
[/Script/Pal.PalReplaceSetting]
CharacterIDReplacer=((Gorrila,Gorilla))
ItemIDReplacer=()
```

The map therefore isn't empty after shipped configuration is applied. Fresh map
descriptor `0x147714d10` binds CharacterIDReplacer to object `+0x28` and has
PropertyFlags `0x0010000000004001` (including Config). The class registration
block `0x147714e70` names config `Game`. A bounded inventory/search of all 87
`.ini` members of the base archive found this as the only map configuration;
external user settings and runtime changes remain outside the declared scope.
The current learning table has no Gorrila PalId and eight Gorilla rows, so this
shipped alias leaves every current learning-row ID unchanged. An empty native
constructor alone would not have established that result.

The 5,772 fresh learning rows contain 712 PalId values, each with one level-one
row and no duplicate `(PalId,WazaID)` pair. The natural move is written to starting
equipment here; that alone doesn't prove its final learned-list membership.
Stored mastered getter `0x142f7c7d0` returns the distinct mastered array.

### Derived learned availability

Fresh `0x142f7ad10`,
`ev_4f97e55a3b1bf3594fb0e8abdb61a405b2a0d875dd8ce2ddd57e74bd6ba7ac7d`,
resolves the distinction between stored MasteredWaza and learned availability:

```text
AvailableWaza(individual):
    result = ordered_unique(individual.save.MasteredWaza)
    map = GetMasterableWaza_Level(individual.save.CharacterID)
    for (w, learnLevel) in map enumeration:
        if signed_int32(learnLevel) <= unsigned_byte(individual.save.Level):
            result.add_unique(w)
    return result

HasMasteredWaza(individual,w):
    if w==None: return false
    return w in AvailableWaza(individual)
```

The available-list routine reads object `+0x438` (stored mastered), `+0x3d0`
(species) and `+0x3f0` (level), and performs explicit duplicate checks. It doesn't
read EquipWaza. Fresh HasMasteredWaza body `0x142f80c80`,
`ev_17944e033bd9a9ba766a2b5bc628cb4c734e339051683a882952fc7479fabb1c`,
calls that routine before membership testing. Consequently natural level-one
moves are available to a level-one child even when generation put them only in
EquipWaza. Equipment truncation doesn't remove that derived availability.
Final field transfer and special mode rewrites remain separate questions.

Dependencies: equipped getter, Waza lookup/predicate and enum identities,
learning table/callback/replacement map, array insertion and truncation, mutation
pool/grant, generation and hatch callers, later learned/equipped writes.

## Child IVs

Scope: HP, Shot (player-facing Attack), Defense, Melee copy, item talent bonus,
mutation fresh values, and generation save writes. Inputs are integer talents
in 0..100; invalid external configurations remain outside the established range.

Retained native helper `0x142e909b0`,
`ev_f230bd78907aaf19d65069134befc1ab830e5775f08b1394c31827af4b1488ca`,
with its retained assembly; mutation body cited under species.

```text
ordinary:
    randomHP,randomAttack,randomDefense = three fresh draws in inclusive 0..100
    sourceHP = either parent's HP, with one clamped two-way draw
    sourceAttack = either parent's Shot, with a separate two-way draw
    sourceDefense = either parent's Defense, with a separate two-way draw
    category=WeightedIndex(Combi_TalentInheritNum)
    if category==2: take all three parent sources
    else:
        branch=Index(3)
        if category==1:
            branch0: parent HP,Attack,Defense
            branch1: parent HP,Defense; random Attack
            branch2: parent HP,Attack; random Defense
        else: # includes invalid selector, not a guaranteed inherited-count label
            branch0: parent HP only
            branch1: parent Attack only
            branch2: parent Defense only
    if effect exists and TalentBonusMax>0:
        independently add TalentBonusMin + Index(Max-Min+1) to each selected value
        clamp each result to 0..100
    write HP,Shot,Defense; copy Shot into Melee

mutation:
    m=settings.Combi_MutationMinTalent
    independently draw m+Index(101-m) for Attack, HP, Defense
    write HP,Shot,Defense; copy Shot into Melee
    # Does not call ordinary talent inheritance or its item-bonus branch.
```

The ordinary initial random-draw constant is float32 bytes `94 01 4a 3b` at
`0x1478be234`, used with integer truncation and a 100 clamp. Parent coin constant
`00 01 80 38` is at `0x147024d70`; branch constant `80 01 c0 38` at
`0x1478be230`. Do not replace differently ordered float32 operations when
reproducing exact scalar bins. The asymmetric middle category keeps HP's source
register across all three branches. Final stores are byte offsets `+0x80`,
`+0x82`, copy to `+0x81`, and `+0x83`.

Retained native defaults assembly `0x142f04880`,
`ev_b27fa5a4ba2982f1a3e94ba892a7860210b87bd18a2b01d5c0432168fd4581a9`:

```text
0x142f06bca: MOV [RDI+0x1dd8], 0x3c23d70a  # MutationRate ≈ .01
0x142f06bd4: MOV [RDI+0x1ddc], 0x3f000000  # RankCoefficient .5
0x142f06bde: MOV [RDI+0x1de0], 0x3ecccccd  # RankDiffPenalty .4
0x142f06be8: MOV qword [RDI+0x1de4], 0x3dcccccd # RandomCoefficient .1
0x142f06bfa: MOV word [RDI+0x1df0], 0x035a # MinTalent 90; InitialRank 3
```

The fresh settings export has no serialized mutation-property overrides. These
are shipped native defaults, not sampled live settings. Trigger helper
`0x142dfcdb0`, retained dossier
`ev_074cfffbb20147cc07ecafd38d8f11e4478ea97784bb2971f5284bbcbc9e1c75`,
adds the caller's item bonus fraction to Combi_MutationRate, clamps to 0..1,
and uses strict `f(r*c)<chance`. Farm callers `0x143021510` / `0x14309d1d0`
convert MutationRateBonusPercent by multiplying .01 once. The latter calls the
roll and child generator within its BreedCount loop. Separate draws don't prove
statistical independence. Ordinary Cake02/Cake04 bonuses are 1..5; mutation
uses fresh default 90..100 talents instead. Category weights do not describe
probabilities of exactly one, two and three inherited talents.

Dependencies: talent identities and widths, RNG helpers/constants, count and
category branches, item values/bonus gate/clamps, mutation defaults and overrides,
mode decision/callers, multiple-offspring loop, final talent writes.

## Shared child-save boundary

Successful native delivery preserves the generated child save except at the
explicit legacy pickup transforms below. This is static field/identity-transfer
evidence for the declared shipped routes, not a live-game or disk-save test.

```text
generated child save
    current farm -> dynamic egg save -> pending hatch record -> live child save
    legacy farm -> InitialCharacter extra -> PalEggModel -> dynamic egg save
current inventory egg -> CommonDropItem3D / DropItemModel
    -> generic pickup: existing dynamic identity and child save
legacy PalEggModel pickup -> optional alpha item replacement
    -> optional extra egg with transformed talents/mutation passives
inventory identity -> pending hatch record -> copied live child save
```

Save assignment `0x1428bf910` copies CharacterID (`+0`), Gender (`+0x10`),
HP/Melee/Shot/Defense (`+0x80..83`), EquipWaza (`+0x58`), MasteredWaza (`+0x68`)
and PassiveSkillList (`+0x90`). Attack/passive copies retain counts, order and
duplicates; they do not select or reroll entries. Copy construction `0x1428a3090`
and move assignment `0x142ea1d40` preserve the same scoped contents.

```text
SetEggSave(eggData, save):
    SaveCopy(eggData+0x78, save)

EggSave(eggData):
    output = initialized empty save
    if eggData exists:
        SaveCopy(output, eggData+0x78)
        if output.CharacterID == None:
            output = generic generation using egg/settings inputs
    return output

CollectHatched(slot or all):
    require valid player, egg data and enough empty storage slots
    if pending child save is empty:
        populate it from EggSave(record.eggData); continue without granting yet
    on actual grant, require inventory egg identity matches the pending record
    submit copied pending save(s) for creation; consume record(s) and egg(s)

RequestCreate(player, save):
    require successful creation/registration guards
    construct individual; InstallSave(individual+0x3d0, save)
    register under the requested/generated identity and complete the grant
```

Installation `0x142f81a70` copies the supplied save before nickname/resource/runtime
initialization. For these nonplayer saves its recovered post-copy writes are
disjoint from species, gender, attack arrays, talents and passives. Registration
`0x142e37dc0` writes identity fields outside those traits. This establishes the
bounded live-server installation, without extending it to every later operation.

Useful copy/hatch landmarks:

| Boundary | Procedure / observation |
| --- | --- |
| Save assignment | `1428bf910`, `ev_ab5a3118a929b2d1c679f6179c2186f763e2eb532e6fc03196c5d0c0b6405cf1` |
| Attack/passive array copies | `140b85470` (`count*2`), `140d37250` (`count*8`) |
| Deposit and preserving egg setter | `143087230` -> `142ef21b0` (`eggData+0x78`) |
| Existing-save/fallback decision | `143035620` |
| Single/bulk collection | `ObtainHatchedCharacter_ServerInternal`, `14303c070` -> `143191f60` / `1431920b0` |
| Creation and installation | `142e3ec90` -> `142c6aac0` -> `142f81a70` |

### Current-farm direct hatch and world-pickup separation

`PalMapObjectMultiHatchingEggWithBreedModel` derives from
`PalMapObjectMultiHatchingEggModel` and `PalMapObjectHatchingEggModelBase`.
Its generation/deposit and inherited pending-hatch producer use the **same item
container**, module field `+0x78`. Direct hatching therefore consumes the deposited
save without the legacy world-pickup hooks. Successful deposit, matching identity,
valid pending record and successful creation remain required.

Verification landmarks are class getter `1429e73d0`, generation `14309d1d0`,
deposit `143087230`, pending-record producer `143029620`, and container accessors
`143067b70` / `143083060`. The inherited collection exec `1429d42e0` reaches
`14303c070`. This class/container binding, rather than a shared helper name alone,
connects the current producer to the hatch model above.

#### Container pickup into inventory

`UPalMapObjectItemContainerModule::PickupItemToInventory_ServerInternal`
(`143073760`, named by its diagnostic) validates the requested occupied slot,
static item and recipient inventory, then transfers the whole slot through
`143197690` -> `143197380`. Successful transfer retains the existing dynamic item
identity and attached child save. This API does not dispatch world-egg pre/post
hooks or perform a talent draw. The claim concerns that native API, not every
possible interface, Blueprint callback or later drop.

#### World-placement proof boundary

For the shipped successful drop/re-pickup route, the effective map-object manager
selects `StaticData.DropItem3DMapObjectId=CommonDropItem3D`. The matching row and
`BP_MapObject_CommonDropItem3D_C` default select native
`PalMapObjectDropItemModel`, derived from `PalMapObjectPickableItemModelBase`.
The checked cooked/native candidate bindings establish this selection for the
declared base configuration; custom worlds or substituted managers are not covered.

```text
RequestDrop_ToServer -> validate source slots/quantities
    -> transfer requested egg into a temporary one-slot container
    -> DropItemInitializeExtra carries that NEW container's ID
    -> spawn CommonDropItem3D / DropItemModel
    -> initialize model: transfer egg into its own container
    -> completion removes the now-empty temporary container
generic pickup -> transfer existing egg identity into recipient inventory
```

Both successful transfers preserve the complete dynamic item record, not just
its static item name. Model initialization precedes temporary-container cleanup.
The concrete model's pre/post-pickup hooks are no-ops, so **this route** preserves
the generated talents and does not add the legacy extra-egg roll. Failed operations
are not guaranteed to return an egg; no universal world-object bypass follows.

| Landmark | Role |
| --- | --- |
| `RequestDrop_ToServer`, exec `142a22e60` -> `142c80f20` | Actual drop RPC |
| `142fa6050` / `142fa62b0` | Temporary-container transfer and typed drop extra |
| Manager `StaticData+8` (`manager+0x540`) | FName `DropItem3DMapObjectId`, not a class pointer |
| `BP_PalMapObjectManager_C`, `BP_MapObject_CommonDropItem3D_C` | Effective selector and `ConcreteModelClass` inputs |
| `14303fbb0` -> `142fbade0` | Concrete initialization/container transfer |
| `142fc07a0`, `142fbfd20`, `142fc65c0` | Full item-record planning/commit |
| `14305b3d7` before `14305b5c4` | Initialization before completion/cleanup |
| `1430a1fd0`; model slots `+0x448/+0x450 = 140b81320` | Inherited pickup; no-op hooks |

### Legacy world-egg metadata consumer

The separate legacy producer `1430216b0` copies its generated save into
`PalMapObjectModelInitializeExtraParameterInitialCharacter`, embedded save `+0x28`,
through callback `143004c20`. The selected `PalMapObjectPalEggModel` consumer
`14308f010` checks that typed extra and the dynamic egg-data class, then calls the
same preserving `SetEggSave` setter. A nonempty saved child therefore remains the
generation result rather than being regenerated from egg metadata at hatch.

### Legacy egg object selection and delivery

Request selector `1430cc2f0` reads the ordinary `PalEggMapObjectIdMap` at settings
`+0x1d80`, or overrides with `PalEggMapObjectId_WorldTree` (`+0x1dd0`, mode 1)
or `PalEggMapObjectId_Mutation` (`+0x1de8`, mode 2). None yields an empty request.
Ordinary selector helper `142e77700` reads character-row bytes `+0x50/+0x51`.

The shipped settings/map-object rows select these eleven IDs:
`PalEgg`, `PalEgg_Fire`, `PalEgg_Water`, `PalEgg_Leaf`, `PalEgg_Electricity`,
`PalEgg_Ice`, `PalEgg_Earth`, `PalEgg_Dark`, `PalEgg_Dragon`, `PalEgg_WorldTree`
and `PalEgg_MutationPal`. Their actor classes inherit
`BP_MapObject_PickupItem_PalEgg_Base_C`, whose `ConcreteModelClass` is
`PalMapObjectPalEggModel`, without a selected child override. The main/Common
map-object tables agree; `_EnemyCamp` supplies no conflicting egg rows.

#### Request and callback delivery

Successful asynchronous delivery retains the same typed extras and embedded save
through request copying, soft-class loading and concrete-model initialization.
Missing rows, a set row gate, invalid requests/classes or failed construction take
failure completion. Useful landmarks are row loader `1430644f0`, load callback
`14305a670`, factory `143064840`, `ConcreteModelClass` at actor CDO `+0x2f8`,
and egg initializer `143098380` -> `14308f010`. Request copies preserve the extra
UObject pointers; no recovered delivery step edits their embedded child save.

#### Legacy egg registration, pickup and hatch identity

Registration and successful inventory transfer retain a nonzero dynamic identity
and its child save. Empty-identity normalization is a distinct creation branch.
The shared planner/slot commit copies the full identity; pickup notifications
containing only static names/counts are not the actual transferred record.
The incubator resolves that identity back to dynamic egg data and builds its
pending save; moved/replaced identities are detected before collection.

Landmarks: `14308d990` and `CreateContainerInServer` (`143063eb0`) register the
item; `142eeb720` distinguishes existing/empty identities; `142fc65c0` commits
the full slot identity. `RequestPickup_ServerInternal` reaches `1430a1fd0`,
which runs the **legacy** hooks around transfer. `1430380e0` resolves hatch input,
`143029620` creates the pending record, and `143047070` detects identity changes.
The transformations at those hooks are the consequential exceptions below.

#### Pickup effects and additional eggs

The egg table's pre/post-pickup slots `+0x448` and `+0x450` select `0x143093be0`
and `0x1430938f0`
(`ev_aa42fd1577c1e25f5112ee2d2bad2d576cfd50ac4601fd091e4f7b2e54f33d2c`,
`ev_b18bce01629e1ba583a875f74d41d592a1c929c7325dc936218b05f69e56f9fd`).
The former clears caches, then calls alpha conversion `0x1430aa530` and extra-egg
cache setup `0x143088820`. These are material trait/multiplicity branches.

The shipped enum name/value pairs at `0x147691360` and `0x147691370` bind
`EggAlphaConversion=245 (0xf5)` and `EggObtainExtraEgg=246 (0xf6)`; their names
are at `0x147694990` and `0x1476949c0`. Fresh main/common passive tables agree on:

| Row family, suffixes `1..5` | EffectValue1 | Target / declared invocation |
| --- | --- | --- |
| `EggAlphaConversion_SakuraSaurus_` | `[35,37,39,41,45]` | `ToTrainer`, `InvokeInOtomo=true` |
| `EggAlphaConversion_SakuraSaurus_Water_` | `[45,47,49,51,55]` | Same |
| `EggObtainExtraEgg_PartnerSkill_NaughtyCat_` | `[50,55,60,65,75]` | Same |

These declarations do not establish live activation, aggregation or odds.
`InvokeActiveOtomo=false` on these rows is distinct from `InvokeInOtomo=true`.

```text
BeforePickup(model, player, container):
    clear model's conversion and extra-egg caches
    AlphaConversion(player, first occupied egg slot)
    PrepareExtraEggs(player, container)               # observes post-conversion data
    # Caller then transfers the container's current item identities to inventory.

AlphaConversion(player, slot):                        # 1430aa530
    require active effect EggAlphaConversion
    chance = aggregate effect value - 100
    require chance > 0 and chance > (rand15 * 100/32767)
    require valid non-boss egg metadata species and a rare-form candidate
    choose candidate's egg item, preserving old WorldTree/mutation item mode
    newEgg = create item for candidate metadata species
    if oldEgg.savedChild.CharacterID is nonempty:
        SetEggSave(newEgg, deepCopy(oldEgg.savedChild))
    replace world slot with newEgg, quantity 1; release old dynamic-item entry

PrepareExtraEggs(player, container):                   # 143088820
    require active effect EggObtainExtraEgg
    chance = aggregate effect value - 100
    require chance > 0 and FloatPercentRoll(chance)
    for each valid occupied dynamic egg slot:
        cache metadata species, item ID, deepCopy(savedChild), mutation-item flag

AfterPickup(model, player):                           # 1430938f0
    emit cached conversion notification if set
    for each cached extra egg:
        transformed = TransformExtraSave(cache.savedChild, cache.mutationItem)
        grant one cached egg item using a scoped creation context with transformed
    clear caches
```

Alpha conversion body `ev_10a5e25815969752f8ae9f63541cee8130fdee8ba04d036a60307ce1f223528e`
copies a nonempty saved child wholesale. The new egg metadata's species can
therefore differ from that copied save's CharacterID at this boundary; the hook
does not itself rewrite that copied CharacterID. This is not a claimed live
species bug or a measured reachable pairing. Its rare-form helper is the same
Tribe/boss/first-match selector already recorded, and the boss exclusion reads
`IsBoss` (`0x142e79680`,
`ev_964175a4a7eb155f643df5c2622f267cb3575b5cd748cd28fc093e6f2ee8d9e7`).
Extra-cache body is `ev_5a2c264ebcb375d4f187512f63991c7993df1b84468fc8ba474218d13ee4db6f`;
assembly `ev_785fa5eb8370ce27a40a791d2ac317a22743021f0372f8340af7e974b3eebcad`
proves `SUBSS XMM0,100` immediately before `0x143309c60`, binding its percent input.

Extra grant `0x142dfee20`
(`ev_09680f3a1ddc20e80cea79375fa77d3076ca236ed2793bc6e4098d4dc51d1f7c`)
calls transform `0x142de9d60`
(`ev_c62b59e7fe7300d53380dbaa1e3f4a8dd8abf5b054342386bf8593ca8a22393b`):

```text
TransformExtraSave(source, mutationItem):
    out = deepCopy(source)
    if not mutationItem:                             # 142e90e50
        fresh HP, Shot, Defense = independent 0..100 draws
        category = WeightedIndex(Combi_TalentInheritNum)  # shipped [3,2,1]
        j = clamped rand15 index in 0..2, unless category == 2
        keep source values at:
            category 0: j0={HP}, j1={Shot}, j2={Defense}
            category 1: j0={Shot,Defense}, j1={HP,Defense}, j2={HP,Shot}
            category 2: {HP,Shot,Defense}
        use fresh candidates for the remaining stats; Melee = Shot
    else:
        all HP, Shot, Defense = fresh Combi_MutationMinTalent..100 draws
        Melee = Shot
        scan for mutation-pool and rank-pool slots, giving the first test priority
        for each found slot, in A then B order:
            select replacement using a copy of the other current passives
            replace only if selection returns a non-None ID
    return out
```

The ordinary transform is `ev_1741f7d5a5731802cf3631f6bb8dfde44ac0b4c34c76068ed9a1d33ea0f01ae4`.
Its source subsets differ from the asymmetric ordinary two-parent breeding rule:
the source here is one existing saved child. Mutation minimum is
`Combi_MutationMinTalent` (default `90`). Native descriptors
`0x14747c5a0` and `0x14747cab0` bind the transform's `+0x1d38` and `+0x1df0`
reads to those original setting names. The mutation passive predicates/selectors
(`0x143156320`, `0x1431566b0`, `0x1431509b0`, `0x1431509f0`) bind to the concrete pools in the following subsection.
Assembly `ev_27f6a410f1424272f421e7251eb8a5dcc4a9e20b45d13320641bd4602f0bd050`
resolves a missing decompiler argument: RDX retains the source save while RCX
receives the output before `SaveCopy` at `0x142de9d94`.

The following creation-hook evidence connects this prepared save to the final
extra egg. Gender and attacks are preserved by both transforms; IVs and mutation
passives follow the distinct branches above. Effect occurrence remains conditional
on contributing records; it is not a measured pickup frequency.

### Extra-egg save application and mutation selectors

Recovered 2026-10-22: prepared-save application and mutation-pool identities are established. The executable/profile match the identity above.
It does not close the complete pickup-effect activation lifecycle.

`0x142dfee20` transforms the cached save once, then registers a scoped creation
hook with `0x142dd0b50` before calling inventory grant `0x14317abc0`.
Registration captures the prepared-save pointer at delegate `+0x18` and appends
the delegate to the dynamic-item subsystem's `+0x80` list. Its table is
`0x1478b8a60`; entry `+0x50` is `0x142de8f60`. The registration body is
`ev_1e3be0246d4d5ff8c7f45c933002cd84216258fb3615a4fe208201bb972ca8f4`.

The grant's container path `0x142fbc7c0` and explicit-slot path
`0x142fbc2a0` → `0x142fbc2f0` both call `0x142eeb2e0` to make dynamic items.
That calls `0x142ed7d70` → `0x142ed7720`. The last body constructs the item's
configured dynamic-data class, calls its initializer at virtual `+0x2b0`, sets
egg metadata species `+0x70`, then traverses the creation hooks newest first.
The hook call uses virtual `+0x50` with that newly created object. Only after
these calls does it register the dynamic object in the subsystem map.

Selected evidence:

| Boundary | Procedure | Evidence |
| --- | --- | --- |
| Inventory grant and fallback | `0x14317abc0` | `ev_dd9152d0f789035719f4370f88e7f28880b6c2c40e100f3ba747a341bf9edc6f` |
| Container creation/commit | `0x142fbc7c0` | `ev_40147cebd7ff560d74d835d1b16a0b935d59e3afa12caaf3a4a4ffe293a46e78` |
| Explicit slot creation/commit | `0x142fbc2f0` | `ev_13476091479e089de33b12312d4f8fca2034fe1c4e177324b3ef6a65fe11dd34` |
| Item requests to dynamic creation | `0x142eeb2e0` | `ev_404a2c57d2f2d51add30ec452bb98542c36627cc018d9f541110171f188dd4ac` |
| Create each dynamic item | `0x142ed7d70` | `ev_eeefc845f0d99f8bb77d7c3a11d5e8ecbce3c04260da096b9891dee94ea619ee` |
| Initialization, hooks, registration | `0x142ed7720` | `ev_a4c9775e306b1083e38c9d9f6cb7b26405bc0d87910361a6b03fea7f8dc24444` |
| Egg hook's preserving setter | `0x142de8f60` | `ev_bbdad458499343027246c85f2770ac60cfecb19d26af4263ab46596782c8f6f9` |

```text
GrantExtraEgg(cache):
    prepared = TransformExtraSave(cache.savedChild, cache.mutationItem)
    register scoped on-create hook capturing prepared
    grant cached static egg item, quantity 1, with cached metadata species
        either inventory insertion route:
            create dynamic object, initialize, set metadata species
            invoke registered on-create hooks, newest first
                our hook, if object is PalDynamicPalEggItemDataBase:
                    SetEggSave(object, prepared)  # SaveCopy into object +0x78
            register dynamic object and return its identity
            plan insertion; on success commit full item identity to slot
        if insertion fails:
            create dynamic item(s) through the same hook-bearing creation path
            pass resulting item identities to the drop path
    remove scoped hook; destroy prepared temporary
```

The grant does not call `TransformExtraSave` again on insertion failure; another
creation receives the same prepared save. Drop placement itself is not claimed
as a live observation. Successful inventory insertion rejoins the full-ID commit
and hatch-record paths above. The hook's class check and setter preserve species
in the child save, gender, both attack arrays and all other copied fields.
This is static final-save evidence, not a live pickup/hatch test or modded result.

The mutation predicates use these pools:

- `0x143156320` (`ev_a3ccad25773b24b8ef65f6003f1db720f69cdccd3d8e756d730387cfaa349e01`)
  checks membership in manager map `+0x398`: `AddMutationPal`.
- `0x1431566b0` (`ev_0e79b523d74a69df2b97cd337c6a66072a7f29f9e35b3e23c98632e54bd7f867`)
  checks map `+0x2f8`: `AddPal` with `Rank > 3`.
- Initializer `0x1431538a0`,
  `ev_b6512d87ba3ec4be885c3508889c3618358a62c6780c412d93c8f61972bb0468`,
  builds these maps from the already bound row flags and `LotteryWeight`.
  Selectors `0x1431509b0` / `0x1431509f0` pass their matching original weights
  and totals to `0x143150a80`,
  `ev_a286449d2fb844fa5612405f43f45cafba7b578ef9220e5a208f7b7a77815c58`.

Refinement of the preceding transform's passive loop:

```text
mutationSlot = rankSlot = absent
for i, passive in current saved list:
    if mutationSlot absent and passive in AddMutationPal:
        mutationSlot = i
    else if rankSlot absent and passive in (AddPal and Rank>3):
        rankSlot = i
for slot,pool in [(mutationSlot, AddMutationPal), (rankSlot, AddPal-and-Rank>3)]:
    if slot exists:
        excluded = copy of CURRENT list with this slot removed
        candidate = original-index weighted draw(pool, excluded)
        if candidate != None: current[slot] = candidate
```

Mutation membership takes priority when both tests could match while its slot
is still absent. The second draw sees any first replacement. A slot's original
passive is not itself excluded unless it occurs elsewhere, so a draw need not
produce a different ID. Failed/empty selection preserves that slot. These are
the exclusion-aware selectors, not the ordinary filler's compacted-weight bug.
Exact named-skill probabilities still depend on manager enumeration and inputs.

Pickup effect checking is also narrowed. `0x142cae6b0`,
`ev_36879f61de6e5f835ed046d9c66f31d7aa7eba8f520f64b568625b1f2b4a8053`,
searches registered effect records; flag 1 additionally obtains records through
`0x142cac280`, skipping component-excluded effect types. Thus the extra-egg hook
uses an additional source that alpha conversion's flag-0 check does not.
`0x142cab8d0` snapshots the component's effect records and, for flag 1, appends
that additional list before applying the effect filter/calculation.

`0x14314f840` applies the resulting aggregate as either `base + aggregate` or
`base * (1 + aggregate/100)`, according to the manager's effect-type list.
With base 100, both give `100 + aggregate`; both pickup hooks subtract 100.
The aggregate is not necessarily a simple sum: `0x143151a60` includes a
max-only type list and conditional grouping by record key `+0x30`, retaining
the greatest value per group. Filter `0x142c9d1d0` also tests record `+0x88`
and calls `0x142caf350` on record conditions. The condition identities and grouping key are bound in the next subsection. Ordinary restriction/removal is recovered after that. The named unchanged setups are connected below; arbitrary source membership remains an input, without an unconditional rate.

Calculation evidence: `ev_90e6f4a25e14c6540ab414dd683a4ccb26d409c9acfd597c7475431508b49046`
(`0x14314f840`), `ev_c37463cd702028fc5c766ea4ed60db57a2b96bd4ee411f095bc20d097f72c936`
(`0x143151a60`), `ev_d6fe7864a002f04f97a5fe6541d1cd8d35db59351159edd1ffd19c421f48f9d7`
(`0x142cab8d0`), `ev_a451cf827dcbb8d5f955a568e98a3d6e81d46595996b6218404443240c6c090f`
(`0x142c9d1d0`), and `ev_87232406905c38add956e1002cc3c426b06db83b14bcec494ad4c7b1aedae642`
(`0x142cac280`). Outer aggregation decompilation warns about a different effect's
jump-table branch. Assembly `ev_7433419079214444ff9a77465a73bbe190e849f6346aafd1bdf7c5b1e0554471`
proves effects `0xf5/0xf6` skip it: `effect-0xc9 > 7` branches directly to
the return at `0x142cab026`; the special additions for effects 3/4 are skipped too.

Both pickup transforms and final extra-save application preserve gender and attack
arrays, whether or not activation succeeds. Unknown effect occurrence rates do
not change that field-preservation result for the declared egg routes.

### Pickup-effect conditions, Tribe grouping and activation masks

Condition/grouping fields and activation-mask conversion establish the
calculation under supplied effects. The named unchanged setups are connected in
the shared partner model; arbitrary unlock/transition/disable/removal lifecycles
and measured live pickup probabilities are not established.

The `0x98`-byte effect record's reflected fields bind as follows:

| Field | Record offset | Decisive descriptor/setter |
| --- | --- | --- |
| `IsPartnerSkill` | `+0x28` | `0x14769adc0`; `0x140d202d0`: `c6 41 28 01 c3` |
| `IsStackablePartnerSkillBySameTribe` | `+0x29` | `0x14769ae10`; `0x1412c6fc0`: `c6 41 29 01 c3` |
| `PartnerSkillParams` | `+0x30` | `0x14769ae60`, struct getter `0x142a666a0` |
| `SourcePassiveSkillRowName` | `+0x90` | `0x14769aeb0` |

Getter metadata `0x148f74960` names the `0x60`-byte
`PalPassivePartnerSkillInvocationParams` and points to its seven property
descriptors at `0x14769aaa8`. Within that struct, `TribeId` is `+0x0`
(`0x14769a8e0`), `ItemParam` is `+0x8` (`0x14769a930`) and `WorkType` is
`+0x58` (`0x14769aa60`). The aggregator's effect-record key `+0x30` is therefore
`TribeId`; the filter's `+0x38` is `ItemParam`, and `+0x88` is `WorkType`.
These are explicit descriptor relationships, not names inferred from adjacency.

Global `bIsStackablePartnerSkillBySameTribe` is setting `+0x1460`:
descriptor `0x147490d20` names it, setter `0x1429261e0` has bytes
`c6 81 60 14 00 00 01 c3`. The retained constructor assembly
`ev_b27fa5a4ba2982f1a3e94ba892a7860210b87bd18a2b01d5c0432168fd4581a9`
has `0x142f065e1: MOV word ptr [RDI+0x1460],0x0`. The shipped settings blueprint
has no serialized override of this property. Runtime/world configuration was not
sampled, so the following default model keeps that global input explicit.

```text
Aggregate(type, contributing records):                  # 143151a60
    highest = 0; ungrouped = 0; groups = map<TribeId,value>()
    for record in source order:
        if record.Type != type or invocation filter rejects it: continue
        if type is manager.highestOnly:
            highest = max(highest, record.Value)
        else if !record.IsPartnerSkill
             or record.IsStackablePartnerSkillBySameTribe
             or settings.bIsStackablePartnerSkillBySameTribe:
            ungrouped += record.Value
        else:
            groups[record.PartnerSkillParams.TribeId] =
                max(existing group value, record.Value) # insert first value if absent
    return highest + ungrouped + sum(groups.values)
```

The base-archive snapshot includes `DT_PassiveSkillEffectCondition`
(51 rows) and `BP_PalPassiveSkillManager`'s class default object. That object
selects this condition table and `DT_PassiveSkill_Main`. Neither
`EggAlphaConversion` nor `EggObtainExtraEgg` occurs among the condition-table
keys, so neither is inserted into its `bIsHighestOnly` / `bIsFixedValue` lists
by initializer `0x1431538a0`. Their base-100 percentage calculation still yields
`100 + Aggregate`, followed by the hook's subtraction of 100. Under the shipped
false global setting and false row stacking flag, contributing partner values
are grouped by Tribe and use a maximum within each group. This does not prove
which sources are contributing at a particular gameplay moment.

| Fresh decoded export | SHA-256 |
| --- | --- |
| `DT_PassiveSkillEffectCondition` | `99d227a6c2eda070f26c6cd3524114e15f7c69fdf194d83d52dbec6ff5bc3392` |
| `BP_PalPassiveSkillManager`, `Default__BP_PalPassiveSkillManager_C` | `4a1b2e2d2f3870af2634f062fed06fd522765472e381947bd6716614ab20a8cc` |

The package paths are
`Pal/Content/Pal/DataTable/PassiveSkill/DT_PassiveSkillEffectCondition` and
`Pal/Content/Pal/Blueprint/System/BP_PalPassiveSkillManager`. These export hashes
identify decoded inputs; the archive and decoder identities above identify their shipped
source. The partner table exports use the same matching archive. All six ordinary/BOSS
definitions for `NaughtyCat`, `SakuraSaurus` and `SakuraSaurus_Water`, across all five
ranks, have `WorkType=None` and empty item/weapon constraints. Their restriction item
names are respectively `SkillUnlock_NaughtyCat`, `SkillUnlock_SakuraSaurus`, and
`SkillUnlock_SakuraSaurus_Water`. A restriction's presence is not proof of its
enforcement. All associated egg-effect rows declare `InvokeInOtomo=true`, other Invoke
flags false, and `IsStackablePartnerSkillBySameTribe=false`.

Item predicate `0x142caf350`,
`ev_e43b48d3694f9c01ef5f36d9d40700610d2630d367ab1c2b104ce5717a3ec353`,
has this scoped model, with `wanted` the record's ItemParam and `actual` the
query context:

```text
if actual has an item ID and its FIRST ID is in wanted.ExcludedItemIds: reject
ok = (wanted.ItemTypeA==None or wanted.ItemTypeA==actual.ItemTypeA)
  and (wanted.ItemTypeB==None or wanted.ItemTypeB==actual.ItemTypeB)
if wanted.ItemIds nonempty:
    ok = actual has an ID and its FIRST ID is in wanted.ItemIds
else if actual.bIsContainNoItemId:
    ok = true
if wanted.WeaponTypes nonempty and actual.WeaponType not in wanted.WeaponTypes:
    reject
if wanted.bMeleeOnly and not IsMelee(actual.WeaponType): ok = false
return ok
```

Explicit ID matching can override the earlier type result; this is not an AND
of every apparent filter field. The body does not separately compare the
wanted single `WeaponType` or wanted `bIsContainNoItemId` fields. For the empty
constraints in these shipped pickup definitions, this predicate returns true;
`WorkType=None` also bypasses the separate work-type comparison. That closes
these concrete filters without assuming the whole lifecycle is active.

The flag-1 additional source `0x142cac280` uses `0x1432f0f80`,
`ev_64bf6e753e257699ad3ec95f7a44ad2da735ac07e243dd22a91500eb82bcb683`.
It resolves the actor by instance ID, reads item data associated with its
`PalShooterComponent`, iterates inventory category 4 with per-static-item
deduplication, and obtains another equipped-item list through `0x143181a20`.
It appends item passive fields at `+0x12c`, `+0x134`, `+0x13c`, `+0x144`.
This is item-derived data, not a collection of every party Pal's partner skill.
Getter `0x142ae61a0` references the wide literal `PalShooterComponent` at
`0x147742c3a`. Exact inventory-category and equipped-list gameplay names remain
unbound here. Manager expansion `0x14314fae0`,
`ev_316ed9a6bd670dedc64c4a2022f82171cbc7cbce285c2ccf0817fb7c34dbc386`,
then visits the row's four TargetType/EffectType/EffectValue slots through the
already recovered target filter. Alpha conversion's flag-0 check does not add
this source list; extra-egg's flag-1 check does.

Activation conversion uses the native definition builder. `0x143119760` identifies itself as
`UPalPartnerSkillPassiveSkill::ConvertPassiveSkills` and copies each rank's
skill parameters, looks up that passive row, then ORs its Invoke flags into the
copied definition's trigger mask:

| Row flag / row-byte offset | Added bit / enum name |
| --- | --- |
| `InvokeActiveOtomo` / `+0x39` | `0x1` / `ActiveOtomo` |
| `InvokeWorker` / `+0x3a` | `0x2` / `Worker` |
| `InvokeRiding` / `+0x3b` | `0x4` / `Ride` |
| `InvokeReserve` / `+0x3c` | `0x8` / `Reserve` |
| `InvokeInOtomo` / `+0x3d` | `0x10` / `InOtomo` |
| `InvokeInBaseCamp` / `+0x3f` | `0x20` / `InBaseCamp` |
| `InvokeAlways` / `+0x3e` | `0x40` / `Always` |

Bool setters bind those row offsets (`c6 41 <offset> 01 c3`); the
`EPalPassiveTriggerType` name/value records are `0x1476950f0..0x147695150`,
stride `0x10`. For example, record `0x147695130` is
`08 52 69 47 01 00 00 00 10 00 00 00 00 00 00 00`, binding the named
InOtomo string at `0x147695208` to `0x10`.
Conversion evidence is `ev_a0b645925cc0ae90cc63d7e0c6ff6fa4bc4b66164b024b2ed96350906896896f`;
assembly `ev_f4e40343821ab0beeeb2e113b10978b6d7f36cb15d4c3cb33e85fbb3b33f117b`
includes `0x1431198d3: OR EAX,0x10`. The method preserves copied parameters;
this is a flag-to-bit contribution, not proof about every possible input mask.

Named-record lookup/copy `0x14311dbb0`
(`ev_3a6f6bda06b3c813928903fd58091303c3d94a4840c3f15b25d8def354a646eb`)
is separate from the definition builder.
It supplies the definition consumed by `0x143110fd0`,
`ev_3bfb9d6a1652fb989dee2c0f57985960fc71dfe1248fb54754cb573fe1a66baf`:

```text
AssignForTrigger(source, eventFlags, triggerType):
    require flags, live owner, passive component and rank definitions
    reject if component disable predicate is true
    rankIndex = clamp(source rank - 1, 0, rankDefinitions.count - 1)
    for each skill in that rank:
        skip existing states except the eligible state-3 retry
        definition = lookup/copy named definition
        require IsAssignablePassiveSkill(definition, triggerType)
        require source restriction predicate
        store/update state and copied invocation parameters
        invoke 143115000(source, skillName)  # reaches trainer component grant
```

The preserved `IsAssignablePassiveSkill` body rejects an empty mask or absent
trigger bit. Its `InOtomo` case calls `0x143305dd0`, which obtains
`PalCharacterParameterComponent` and calls `0x142c1c970`; that helper tests a
live non-null trainer reference at component `+0x148`.
Evidence: `ev_118d5aede6d2ceb05f5b4669396b4e7e1c7804020c5e685211d2a80c3ec9e3aa`
and `ev_abebf6ccb3bbd15f215f0191c71d336210c0387127a45055116a709563b997ae`.
Entry `0x14312b4b0` supplies trigger `0x10` to this dispatcher,
`ev_28f86e56060dddcef5cf659ae6edfd8f861823f2c2782bc38ec65273cb050fb0`;
`0x14312b280` supplies `0x1` and setup `0x143125da0` supplies `0x40`.
This distinguishes the paths; ordinary holder removal is recovered below, while the named unchanged setups are connected below and arbitrary event ownership remains outside those contracts. The same mask conversion also binds the capture
synchronization rows' `InvokeActiveOtomo` declaration to bit `0x1`.

**Calculation boundary:** condition identities, default same-Tribe grouping,
condition-table membership and Invoke-to-mask conversion are established.
The following section supplies restriction and ordinary removal evidence.
Unknown setup-specific source membership limits gameplay activation claims, not
the calculation given contributing records. See the finite candidate boundaries
in the assessment rather than treating every lifecycle event as required work.

#### Shipped item sources do not add extra-egg records

**Established 2026-10-26.** For the selected shipped static item data, the flag-1
additional item list contributes no `EggObtainExtraEgg` records. This is a bounded
data and default-constructor result, not a claim that the flag-1 interface ignores
items or that arbitrary modified item data cannot contribute.

The collector's first two item paths use the **first** pointer returned by
`0x142fac3f0`. That pointer is the static row resolved through `0x1432651f0`;
the separate second pointer comes from dynamic-item lookup `0x142eddde0`.
The equipped-ID path uses `0x142fadd00`, which reaches the same static resolver.
Thus the four reads at `+0x12c`, `+0x134`, `+0x13c`, `+0x144` concern static
`PassiveSkill`, `PassiveSkill2`, `PassiveSkill3`, `PassiveSkill4`, rather than
random fields of a mutable egg save. Their native descriptors are respectively
`0x147785120`, `0x147785160`, `0x1477851a0`, `0x1477851e0`.
Resolver evidence: `ev_2bb6b9070dc05c205a62afcf52ce7da5defefe76eb2728f851fd151193813fb5`.

The shipped GameInstance CDO selects `BP_PalItemIDManager_C`; its CDO's
`StaticItemDataAsset` references
`Pal/Content/Pal/DataAsset/Item/DA_StaticItemDataAsset`. Manager resolution
`0x1432e7c20` reads GameInstance `+0x528`, named `ItemIDManager` by descriptor
`0x147477520`; `ev_4aff1fef6d0fb05a20b7de2784d3baef425f2b7d8e9e174141dfbb430d0ca062`.
Descriptor `0x14750da50` binds `StaticItemDataAsset` to manager `+0x28`.
Setup `0x142faeb30` constructs `PalStaticItemDataTable` at manager `+0x160`
and passes the selected asset to `0x141766150`, which stores it at wrapper
`+0x28`. Row lookup then reads the asset's `StaticItemDataMap` at `+0x30`,
bound by descriptor `0x147783ec0`. Setup/store evidence:
`ev_f6be3a93465af5d4b064898ef2f1433f6e9809d54ed57f57d952d189b29a8382`,
`ev_679762fe8cfbfda88498bf6ad0c35039e5a1823b528cfcabb3b4a84ded4d9c8a`.
This binding uses the item **ID** manager, distinct from the container manager.

The following is the extraction check, not a game-executed selection algorithm:

```text
extraRows = all passive-table rows with EggObtainExtraEgg in any effect slot
           # exactly NaughtyCat partner rows 1..5; values 50,55,60,65,75
for each nonnull object in selected StaticItemDataMap:
    values = its four static PassiveSkill fields
             # absent serialized fields retain checked native None defaults
    assert no value names any extraRows entry
therefore expanding any subset selected by the item collector adds no f6 record
```

Fresh extraction found 2,467 map entries: one null `None` entry and 2,466
referenced item objects. Every object reference was resolved; all 937 explicitly
serialized passive fields name rows in the current 1,905-row passive table.
Slots 1–4 have 560, 192, 120 and 65 explicit values respectively. None names any
of the five extra-egg rows. The nine referenced native classes are:

| Item class | Objects |
| --- | ---: |
| `PalStaticItemDataBase` | 1,252 |
| `PalStaticArmorItemData` | 515 |
| `PalStaticWeaponItemData` | 400 |
| `PalStaticConsumeItemData` | 286 |
| `PalStaticItem_TreasureMap` | 5 |
| `PalStaticItem_GrantTechnologyPt` | 4 |
| `PalStaticItem_PlayerLamp` | 2 |
| `PalStaticItem_Homeward` | 1 |
| `PalStaticItem_WorldTreeHolyWater` | 1 |

Base constructor `0x142b0cd80` explicitly zeroes all four FName fields;
`ev_13b6e8b44caddf2d2079fbf92549036c00520f47ed65da0503d3a11f9a5d3550`.
Instructions corroborate the four stores at `0x142b0ce6c..0x142b0ce81`
from zeroed RSI; `ev_7d90c13d70c50b9a2d29c40bc438ac77f0b67e2dafc4d1bd30b8b64385cb130d`.
The class-registration and constructor audit covers all nine referenced types:
their seven distinct derived constructor bodies call this base and only change
vtable or later fields (`+0x178` onward). They do not replace the four defaults.
Derived constructor entries are `0x142b0ccc0`, `0x142b0f400`, `0x142b0f500`,
`0x142b0f580`, `0x142b0f5c0`, `0x142b0f600`, `0x142b0f640`.
Decisive constructor observations:
`ev_e2bcb4ed70826ff5fdaae0c3164a2083205e9dddfa9b454e215f297963e12c51`,
`ev_9d8a19a5b1bd43b116a826bceda348964784d06ccb67d51dad7bcd85788c7c41`,
`ev_170eed8d0642dddf42ffb81f095d9d4bc19cfc9e7b93e32321a7dd708cb60eb2`,
`ev_4acf2b4ae2be39fc83cf1f6742b273268e6e3f3126c5fbf75036e462d7865b76`,
`ev_bd279ae7b5378f18c244492e937eb1de53f14a2bc28e6021c0c9da836910f459`,
`ev_6221e8a74f4dd364b651d35d06ed5e56d97248efe738d79a18c902e2a51cf27e`,
`ev_cf91579a6983746bcaab49539548b2f7aca8b96bd4d76f79e17b1dfbba8d2179`.

Decoded export SHA-256 identities:

- Selected asset: `98afe074f058ecee9153072c265bb19747f687339c3de781225875f8dff45859`.
- Item ID manager CDO: `78e580bc1ca63fe41236855c3b7165be028d812fe09f575778976f268a5d057f`.
- GameInstance CDO: `78942b1698dacf4f621e4092f7e413ef38e3fdad8f81fb08b2a762449ea4a75e`.
- Passive table: `ce22ba86cf0a461289f380d9bb12dcbc6b73a003b217bd678ad37cf76d472eab`.

The base archive was freshly rehashed on October 26 and matches the report's
identity. The spreadsheet-style `DT_ItemDataTable` also has no extra-egg
references, but that absence alone is not this proof: the selected asset map,
native defaults and static lookup are the decisive inputs. Runtime replacement
of these shipped static inputs and mods remain outside the declared setup.
Partner-record acceptance/grouping and route applicability remain separate;
this result does not establish an unconditional extra-egg frequency.
Retained descriptor bytes support the field bindings above:
`ev_cb13c7087fe6f4548846bdf2e4a185e66e56a155e3a968b74196dc41e234e605`,
`ev_5c3c53da3d138a4e76ec35ae28709272fa6c960611a0b866228bb765b267284c`,
`ev_2d0a2328c7713f15b68dff3419874b79546b7b78e5266ab1f4915f96ff2d07a9`,
`ev_c1574dc399c2eca212ba62e04fa881eab74bb12b2b056a5fd82ff3271542228e`.


#### Unchanged ordinary NaughtyCat rank mixtures

The fixed rank-1 grant proof extends to **fixed initial ranks 1–5** for ordinary
NaughtyCat sources satisfying the same setup conditions. This is not a claim about rank
changes after grant or arbitrary party activation. Each source must have completed the
retained initialization/grant path, belong to the pickup player, have
`SkillUnlock_NaughtyCat`, retain valid ordinary party identity and have no intervening
holder, ownership, restriction or disable changes. The shipped non-stacking setting and
the non-excluding source lists apply. The pickup must use the known world-egg hook.

The current `DT_PartnerSkillParameter` has five one-entry rank definitions for
`NaughtyCat`. Their entire `Parameters` objects are identical: zero delay,
WorkType None, empty item/weapon and other-companion constraints, and no extra
rank-specific source condition. Every entry names its corresponding
`EggObtainExtraEgg_PartnerSkill_NaughtyCat_1..5` row. All five rows declare only
`InvokeInOtomo=true` and `IsStackablePartnerSkillBySameTribe=false`; their values
are **50, 55, 60, 65, 75**. The native assignment's already recovered
`clamp(rank-1, 0, definitionCount-1)` selects the definition; all subsequent
grant, acceptance and cache edges are the shared rank-1 edges. This equality of
the actual rank inputs, rather than presumed symmetry, supports the extension.
The BOSS table definitions match, but no additional BOSS activation claim follows.

The ordinary character row binds `Tribe=EPalTribeID::NaughtyCat`. Consequently,
the established same-Tribe maximum aggregation reduces this setup to:

```text
values = [50,55,60,65,75]
R = fixed ranks of initialized ordinary NaughtyCat sources meeting the grant contract
extraEggInput = 0 if R is empty else max(values[r-1] for r in R)
# This is the nominal percent input to the known pickup consumer.
# The shipped item-derived list adds no extra-egg record.
```

Two rank-1 sources give 50, rather than 100. Ranks 1 and 5 give 75; ranks 2, 3
and 4 give 65. These are arithmetic reconstructions under the stated source
contract, not observations that every Pal in an arbitrary party contributes.
They do not measure live frequencies or establish independent repeated rolls.
Neither ordinary source count nor visitation order changes a maximum within
this one Tribe. A global stacking override would change that calculation and
remains an explicit external setting.

Decoded rank-definition export SHA-256:
`ffc19de94741fb4f61a837e631564924d540894e2f16299ac7cd2c05d7b60e0d`.
The passive and character tables retain their reported archive/decoder provenance.
Assignment evidence is
`ev_3bfb9d6a1652fb989dee2c0f57985960fc71dfe1248fb54754cb573fe1a66baf`;
the shared acceptance/cache proof is in
[NaughtyCat contribution at pickup](#rank-1-naughtycat-contribution-at-pickup),
and the arithmetic uses the aggregation model above. This closes the fixed-rank
ordinary source calculation. Unrestricted query-time source changes remain
outside this fixed setup. The world-placement handoff is established above;
its generic model does not use this extra-egg consumer.

## Shared partner-effect model

These established rules support the pickup and capture calculations under supplied
effects, and the setup-specific NaughtyCat/GuardianDog conclusions. The declared
unchanged NaughtyCat setup uses the source-list binding below; arbitrary source
combinations remain separate. No measured gameplay frequencies are implied.

### Shipped partner-table selection and accessor initialization

Shipped `DefaultEngine.ini` selects `BP_PalGameInstance_C`, whose
`MasterDataTablesClass=BP_PalMasterDataTables_C` selects
`PartnerSkillParameterDataTable=DT_PartnerSkillParameter`. Native setup binds
that actual table to the typed accessor; an empty native constructor is not
evidence of an empty effective table. Runtime overrides remain outside this base
configuration. Landmarks: game-instance setup `142f1c2e0`, accessor setup
`1430f3d60`, table store `141766150`, typed lookup `1430d4880`.

### Partner initialization and runtime inputs

`LoadPartnerSkillParameterFromDataTable` (`142caff10`) looks up the owner's
CharacterID and copies `RestrictionItems` and rank `PassiveSkills` from the typed
row into `PalPartnerSkillParameterComponent`. An absent owner/row follows the
diagnostic branch, without those successful-row copies.

Component setup `142cb1ca0` and handler initialization `143125da0` convert the
definitions, cache the bound item restriction, register callbacks and directly
invoke InOtomo entry `14312b4b0`. That call is outside the separate Always guard,
but retains its own authority, owner, assignment and disable checks; no later
companion event is needed to reach it. [Invoke masks and filters](#pickup-effect-conditions-tribe-grouping-and-activation-masks)
remain part of the grant decision.

### Partner-effect restrictions and removal

The bound `IsRestrictedByItems` predicate (`142cafbf0`) is:

```text
if restriction list empty: return false
resolve source's trainer, or optional actor; if neither available: return false
if special trainer ownership check rejects, or inventory unavailable: return false
return true if any required static item lacks a matching nonempty, positive-quantity slot
```

This is item presence, not proof of technology purchase or an unconditional lock.
Unresolvable delegate invocation (`143129380`) returns false. The cached handler
restriction byte `+0x39` causes state 4 instead of a grant; it is distinct from
the Active guard at `+0x38` and the component disable registry.

Ordinary authority-side holder removal (`14312e610`) removes invocation flags
through `1431254b0` and owner/trainer prepass `143135f30`. Component removal
`142ca0780` deletes accepted source records only when no flags remain, then
rebuilds their effect view (`142ca1a10`). A record with other flags can survive.
This reaches contributing storage, rather than merely unhooking callbacks.
Special recipients and all possible disable writers are not covered by this
ordinary removal result.

### Authority-side trainer grant and source-record state

Authority predicate `143306f40` tests `PalGameState.Role == ROLE_Authority` (3),
not an anonymous gameplay mode. For a matching rank definition:

```text
Grant143115000(handler, skill):
    require definition and handler state
    if cached item restriction: state=4; return
    if state != 2 and DelayTime > 0: initialize delayed state=2; return
    state=1
    requests = copy flags/parameters, partner marker and source Tribe
    grant to source unless bNotAssignSelf
    grant to validated trainer through 142c97620 -> 142cbd740
```

State 1 alone does not establish acceptance: expansion, target modes, conditions
and type exclusions still apply. The trainer wrapper supplies mode arguments
4=0, 5=0, 6=1; it appends accepted records under the source actor's weak key in
trainer component `+0xf0`, then synchronously rebuilds the effect view. The
wrapper does not take the grant's old-record replacement branch.

### NaughtyCat additional source gates and delayed update

Shipped NaughtyCat rank definitions have `DelayTime=0`; the associated passive
rows have `AddInvokeTriggerType_1=None` and `_2=None`. Checked parameter
initialization/copy and `ConvertPassiveSkills` (`143119760`) therefore yield a
zero additional-condition mask. Source predicate `14314a2e0` accepts that mask
for a live source; trigger, restriction, disable and target checks remain separate.

Delayed update `14313d370` accumulates elapsed time only for state-2 entries and
invokes the grant when their delay expires. It does not reevaluate or revoke
active records in its shown path. This supplies no periodic expiry rule for the
zero-delay NaughtyCat setup, and no conclusion about all other lifecycle events.

### Partner restriction, disable, holder and rank transitions

Inventory callback `OnUpdateInventoryContainer` (`142cb3ae0`) reaches restriction
refresh `143132a90`: becoming restricted marks non-state-3 entries as state 4
and removes the seven selected source flags; becoming unrestricted retries
state-4 grants. Records retaining other flags survive. Retries still require
acceptance. Component `OnInactivatedAsOtomoHolder` reaches `14312e610`.

`OnChangeDisablePassiveSkill` -> `14312ca60` retains positional booleans (their
semantic parameter names are unestablished):

| Second boolean | First boolean | Action |
| --- | --- | --- |
| false | false | Retry trigger paths, including InOtomo |
| false | true | Remove/deactivate, including InOtomo and seven-flag cleanup |
| true | either | Remove then retry through `1431133d0` |

`OnUpdateCharacterRank` -> `142cb3a60` updates active-skill parameters through
`142cbaba0`; that callback does not establish immediate replacement of an accepted
passive record. Dispatch selects the current rank, but refresh of an already
granted record after rank change is not proved here. The setup-specific results
below require unchanged initial rank; they are not arbitrary transition models.

### Rank-1 NaughtyCat contribution at pickup

For shipped base defaults, take an initialized ordinary non-BOSS rank-1 NaughtyCat
with valid trainer/party assignment, `SkillUnlock_NaughtyCat` present, no other
contributing effects or active disable entries, and successful ordinary
nonmutation **legacy world-egg** pickup. No rank, ownership, holder, restriction
or disable change intervenes. The trainer recipient must be the pickup player.

Under those conditions, initialization's InOtomo dispatch, zero delay/additional
conditions, trainer acceptance and cache publication connect the sole accepted
rank-1 value to the pickup consumer. It supplies a nominal **50-percent roll
input**, not a measured extra-egg frequency. The fixed-rank mixture extension is
[above](#unchanged-ordinary-naughtycat-rank-mixtures); generic farm-egg drop pickup
does not use this consumer.

#### Trainer acceptance and target filtering

Expansion filter `143143bc0` accepts no-override ToTrainer (2) only when all three
request mode bytes `+8/+9/+10` are zero, effect type is nonzero and that type is
absent from the source's effective `DisablePassiveTypes`. Wrapper modes `+8/+9`
are zero. Grant `142cbd740` computes `+10` by comparing the trainer recipient
with the actor resolved from the holder's activated companion slot. The valid
ordinary identities below make that comparison false. An absent resolved actor
also leaves it zero. “No active disable entries” does not itself settle this
separate type-exclusion list.

#### Scalar-cache publication

Accepted source records in `+0xf0` rebuild the `+0x140` view; `142cb54b0` flattens
those records into locked scalar cache `+0x168/+0x170` without a new acceptance
or rank test. Scalar reader `142cab8d0` copies that cache under the same lock.
Presence check `142cae6b0` reads the record view. Extra eggs use this scalar
aggregation, whereas capture synchronization uses the separate source-key
collector `142caca50`.

#### Spawned-slot contract and exclusion initialization

The holder event is `GetSpawnedOtomoID`, returning `ActivatedOtomoSlotID` in
`BP_OtomoPalHolderComponent`; its inherited default is -1. An out-of-range/absent
slot resolves no actor. Nondefault indices use the identity contract below.
Native `PalPassiveSkillComponent` and the selected NaughtyCat/MonsterBase
templates supply an empty `DisablePassiveTypes` default. Initialization
`142cb23f0` binds rank updates without populating that list. Arena application
can replace it; the effective shipped lists are bound below.

#### Ordinary player-controller holder binding

Shipped `BP_PalGamemode_C.PlayerControllerClass=BP_PalPlayerController_C`.
That controller constructs `BP_OtomoPalHolderComponent_C` through its
`SimpleConstructionScript` (`SCS_Node_2`), without overriding the inherited
template. This binds the selected holder behavior within this route; arbitrary
map game-mode overrides or modified controllers are not covered.

#### Ordinary nondefault-slot identity

`ActivateCurrentOtomo` sends the same selected index to `ActivateOtomo` and
`SetActivateOtomoID_ToALL`; `GetSpawnedOtomoID` returns that activated index.
`SpawnOtomo` and its callback resolve the slot's individual/actor independently
of the holder controller's player pawn. For valid ordinary party data the
companion Pal and trainer recipient are distinct entities. Thus both spawned
and unspawned cases clear mode byte `+10`; the conclusion does not require
index -1. Crafted/debug aliases and other controller/party contracts are outside
this inference. Native landmarks: `142c8fb20` -> `142f92290` for the slot actor,
`142cbd740` for the comparison and `143143bc0` for ToTrainer acceptance.

#### Arena exclusion values and egg-effect consumers

`EggAlphaConversion=0xf5` uses `1430aa530`; `EggObtainExtraEgg=0xf6` uses
`143088820` with flag **1**, including item-derived additional effects. The
enum records are `147691360` / `147691370`; the [pickup model](#pickup-effects-and-additional-eggs)
and extra-save transform retain their distinct behavior.

Shipped `BP_PalArenaWorldSubsystem` supplies primary `DisablePassiveTypes={0x46}`
(NonKilling), and sub-Pal list `{0x0d..0x14,0x78}` (eight element effects and
ReloadSpeedUp). Native enum records bind those values. Neither list nor their
union contains extra egg `0xf6` or capture synchronization `0x55`. Enabled copy
and disabled empty-list application therefore exclude neither effect.

#### Effective arena subsystem and shipped-world binding

Shipped `BP_PalWorldSettings_C.ArenaWorldSubsystemClass` references
`BP_PalArenaWorldSubsystem_C`. The checked cooked registry and native registration
family identify no competing shipped derived candidate. Native base
`PalArenaWorldSubsystem` has `bShouldCreate=false`; the configured data-only
Blueprint enables creation and supplies the lists above. Collection lookup uses
an exact registered class, otherwise the first registered derived instance.

If lookup is null/dead, arena application leaves the source's inherited empty
list unchanged. If the configured BP is instantiated, either copied lists or
the disabled empty input still omit `0xf6/0x55`. Creation and the copy toggle
therefore do not reject those effects **in the declared unchanged base setup**.
No unconditional creation or custom-world/subclass guarantee follows.

Useful landmarks: world-settings `ArenaWorldSubsystemClass` descriptor
`1478a27f0` (`+0x520`); creation predicate `14338f1b0`; collection insertion
`1457f4ed0`; exact/derived lookup `1457fc0f0`; application `142d5a5e0` and
`DisablePassiveTypes` setter `142cba340`. World-settings CDO SHA-256:
`eddb782d4a032c7c8bca88c602d345e02b35679f2c240dcb0eb51f144900286a`;
arena CDO: `7ee6b18d3af7fbcc10f9548fe997268f7ebc857ae4da2c367d374066a4122d36`.

#### Selected grant and cache observations

| Conclusion | Verification landmark / retained observation |
| --- | --- |
| Typed source row reaches initialization | `142caff10`, `ev_14082613cbdf078ba5a88563ac8f1058f245bc7c6b557722689923b306d09413` |
| Authority-side dispatch/grant | `14312b4b0`, `143115000`, `142c97620` |
| Trainer source-key record and target modes | `142cbd740`, `ev_e7618f8be5ddd1b1c2a7ed4bcf5aee6882fd77dedf172ec478423ca0dc366c8d` |
| ToTrainer/type acceptance | `143143bc0`, `ev_c1a93a069de3ae2f375bfcac1a92085043c885583c05ed7c8d8fcc3797f9237e` |
| Synchronous scalar publication | `142cb54b0`, `ev_9e36b2a17aa58d29410d2595403f5b5e27b8d7014d23cc3bc441fa0ccd2ec26b` |
| Extra-egg consumer | `143088820`, `ev_5a2c264ebcb375d4f187512f63991c7993df1b84468fc8ba474218d13ee4db6f` |

## Wild-Pal passives

**Verified for declared assignment/rare inputs, updated 2026-10-29.** Target/source
capture contracts and the [rare current-input link](#rare-passive-draw-and-current-pool)
are established. Form selection is documented separately under the wild-attack
producer boundary; an enable flag is not itself the resulting rare flag. Fresh native initializer `0x1432e6070`,
`ev_06a0c4c0404e03ccfee8f18e7230feeda2c6ff9339e82d3b9b14c95cd2bd31c1`,
and guaranteed-passive helper `0x142e7bab0`,
`ev_797472e07df22ef317119d7129ad1adb1b73ed0d95e3923b982d562ae37e099c`:

```text
initialize output; apply the form-selection prefix below
append non-None character PassiveSkill1..4 in field order
append caller passives while count < 4; no deduplication at this step
if resulting rare flag: draw manager pool +0x168; append if nonempty and absent
    # rare draw has no four-slot append guard; named AddRarePal binding below
unless caller skips random filling or special-character predicates apply:
    if output byte +0xb0 is clear:
        k = WeightedIndex(settings.PassiveSkillAppendNumWeights)
        attempts = 0 if k<0 else min(k,4-currentCount)
        append RandomFill(existing,attempts)
finish common initialization
```

The special predicates read IsTowerBoss / IsRaidBoss at character bytes
`+0xcd`/`+0xce`; the same reflection/setter bindings are recorded under child
species. Upstream caller overrides retain the explicit supplied-input contracts below. RandomFill is the same helper
with the filtered/original-vector mismatch described above. Fresh descriptor reads
bind PassiveSkill1 to row `+0x168` and PassiveSkillAppendNumWeights to settings
`+0x1400`: `ev_57c52a99dcf6a4cf27ddf12595bf41322753afe39a06959c045f40c473f05701`
and `ev_89f595a5212cd16738f74abc719241ba4c335ae85c649091e9cfa1f239c282b1`.
The latter array isn't serialized in the fresh Blueprint export. Its recovered
native defaults are **[10, 76, 10, 3, 1]**, selecting indices 0..4 without a +1
adjustment. These are attempted additions, capped by remaining slots; the filler
can still fail. Gorilla's fresh PassiveSkill1 is `PAL_rude`.

The retained constructor `0x142f04880`,
`ev_b27fa5a4ba2982f1a3e94ba892a7860210b87bd18a2b01d5c0432168fd4581a9`,
allocates five floats for settings `+0x1400`. Its decisive stores are:

```text
0x142f06558: LEA RBX,[RDI+0x1400]
0x142f06563: MOVDQA XMM6,[0x1474951e0]
0x142f0656b: MOV dword [RBP-0x29],0x3f800000
0x142f06575: MOV dword [RBX+8],5
0x142f06587: CALL 0x140bfe8c0
0x142f06593: MOVUPS [RCX],XMM6
0x142f06599: MOV [RCX+0x10],EAX  # fifth float, from [RBP-0x29]
```

Fresh executable bytes at VA `0x1474951e0`, file offset `0x74935e0`, are
`00002041 00009842 00002041 00004040`: float32 `[10,76,10,3]`.
The fifth value `0x3f800000` is 1. The fresh Blueprint class directly inherits
`Class'PalGameSetting'` from `/Script/Pal`; no intermediate Blueprint parent
overrides this array. This establishes shipped inherited defaults, not sampled
runtime/world configuration. Synthetic enumeration of the 32,768 scalar inputs
to the recovered WeightedIndex gives `[3277,24903,3277,983,328]` for indices
0..4. This tests the reconstructed selector, not live spawn frequencies.

### Rare passive draw and current pool

The general
initializer's resulting rare flag invokes `0x143150a30`, then appends a nonempty
result if absent. This append has **no four-slot guard**; the cap on preceding
caller passives must not be transferred to it. A form-enable flag alone does not
mean this draw occurs. The [form-selection prefix](#wild-form-selection-and-saved-level-handoff)
establishes that separate producer contract.

The consumer and pool construction use the named field binding below:

```text
BuildRareDrawPool(manager, row):              # 1431538a0
    if row.AddRarePal:                       # byte43; field binding below
        Insert(manager.pool_168, row.key, row)
        manager.total_410 += row.LotteryWeight_at_0c
        manager.weights_418.append(row.LotteryWeight_at_0c)

DrawRareAddition(manager):                   # 143150a30
    return 143150a80(manager, weights_418, pool_168, total_410, exclusions=[])

Select(weights, pool, total, exclusions):     # 143150a80
    if weights.empty: return None
    keys = ascending occupied pool slots
    total -= sum(weights[i] for excluded keys[i])
    if total < 1: return None
    ticket = rand() % total
    for i in original key/weight index space:
        if keys[i] in exclusions: continue
        if ticket < weights[i]: return keys[i]
        ticket -= weights[i]
    return None
```

The selector subtracts the **same original vector** it compares. Unlike the
ordinary filler, it does not compact one vector and subtract another.
`143150a30` constructs two zero qwords for an empty exclusion array and calls:

```text
143150a80(manager, output, manager+418, manager+168, *(manager+410), empty)
```

`1431538a0` checks `*(char *)(row+0x43)`, inserts into `manager+0x168`, adds
`*(int *)(row+0x0c)` to `manager+0x410`, and appends that value to `+0x418`.
The shared fresh-manager initialization and key/weight alignment argument above
applies to this pool too. Decisive observations:

| Observation | Evidence ID |
| --- | --- |
| General initializer and append, `1432e6070` | `ev_06a0c4c0404e03ccfee8f18e7230feeda2c6ff9339e82d3b9b14c95cd2bd31c1` |
| Rare-pool consumer, `143150a30` | `ev_11726d1f399dfdaf36d12540adcaf3472962c081d8aa7fdd2fa59cf946b39a33` |
| Original-index selector, `143150a80` | `ev_a286449d2fb844fa5612405f43f45cafba7b578ef9220e5a208f7b7a77815c58` |
| Pool initializer, `1431538a0` | `ev_b6512d87ba3ec4be885c3508889c3618358a62c6780c412d93c8f61972bb0468` |

The configured main/composite-parent table chain is established in
[passive ordering](#passive-ordering-established-construction-and-remaining-input).
The retained decoded Main and Common dictionaries agree across all 1,905 rows.
Their complete **decoded `AddRarePal=true`** candidate snapshot is:

| Key | Rank | LotteryWeight | AddPal |
| --- | ---: | ---: | --- |
| Rare | 4 | 100 | false |

| Decoded input | SHA-256 |
| --- | --- |
| `DT_PassiveSkill_Main-DT_PassiveSkill_Main.json` | `ce22ba86cf0a461289f380d9bb12dcbc6b73a003b217bd678ad37cf76d472eab` |
| `DT_PassiveSkill_Main_Common-DT_PassiveSkill_Main_Common.json` | `a405785eae0c0e246f248dc9c246db024213bab48c1e87053f873af6dcf7cb91` |

These exports use the recorded shipped archive/mappings/decoder identities.
Native byte `+0x43` is bound to `AddRarePal`; the fresh pool is the positive-weight
singleton above: total100, tickets0–99, result `Rare`. Ordering cannot affect a single candidate, and no
uniform-RNG assumption is needed for that identity. Duplicate suppression at the
caller can still prevent another append of `Rare`.

**Native field binding:** the property descriptor at `0x1476967f0` begins
with name pointer `0x147696ee8`; its ASCII bytes decode to `AddRarePal`.
Descriptor `+0x48` points to boolean setter `0x140f52230`, whose complete body is
`c6414301c3`: `MOV byte [RCX+0x43],1; RET`. The property pointer list at
`0x147696d38` contains this descriptor immediately before the independently
bound World Tree descriptor `0x147696840`. The proof uses the named descriptor
and actual setter, not inference from adjacent field names.

| Fresh observation | Evidence ID |
| --- | --- |
| AddRarePal descriptor, `1476967f0`, 80 bytes | `ev_dbd19d1171b8808758a52fea8aaa2313ff99ec93fd928915c613a2c12170fac2` |
| AddRarePal ASCII name, `147696ee8` | `ev_10c126aaa90f2de1b3a521a23b833f0576216fb62366d0715c005c5708e86fa7` |
| Setter `140f52230`, five complete bytes | `ev_6296b095f6cb9dbec9a7f218b60802b89cd4a9e5c1709b48a71087ab7115877f` |
| Shared property pointer-list entries | `ev_db15f8dfb04444d3db41bfb193f339b7d76e4ab449d976fa18bcf48113f483ef` |

The native reads match the recorded executable SHA-256 and reduced analysis profile;
their bytes were also compared with the executable's PE file mappings.
Given the resulting rare flag and valid fresh manager/table inputs, the draw
returns `Rare`; duplicate suppression can prevent a second copy. Its append has
no four-slot guard. Form enable/selection remains a separate producer contract,
and no unconditional rare-spawn frequency is claimed.

### World Tree standard-spawner passive input

**Established 2026-10-27.** This closes the computed caller-list input for the
already scoped `BP_PalSpawner_Standard.Spawn` World Tree aura branch. Species,
level and the branch invocation remain supplied inputs; it does not establish
how often a world chooses that branch. The common initializer and saved-list
boundary remain the ones above.

Freshly re-extracted Blueprint function `GetRandomWorldTreePassiveSkill` has
eleven decoded expressions. Expressions 0–2 obtain/check the passive manager;
4 invokes `PalPassiveSkillManager.GetRandomWorldTreePalSkill` with no arguments;
5–6 build and return a **one-element** Name array. The invalid-manager branch
at expression 8 returns an empty array. It does not filter a None result from
the successful-manager branch.

Native registration pair `14769eae0` binds name pointer `14769d6f0`
(`GetRandomWorldTreePalSkill`) to exec `142a6a450`. That wrapper calls selector
`143151030`, which supplies an empty exclusion list to `143150a80`:

```text
GetRandomWorldTreePalSkill(manager):
    return DrawOriginalIndex(
        weights=manager+0x448, pool=manager+0x348,
        total=manager.int32_at_0x440, excluded=[])

DrawOriginalIndex(weights, pool, total, excluded):
    if weights.count == 0 or total_after_exclusions <= 0: return None
    keys = pool keys in occupied-slot order
    ticket = rand() % total_after_exclusions
    for original index i, key in keys:
        skip excluded key
        if ticket < weights[i]: return key
        ticket -= weights[i]
    return None

SpawnerWorldTreeCallerList(self):
    if passive manager invalid: return []
    return [manager.GetRandomWorldTreePalSkill()]

common initialization:
    saved = character PassiveSkill1..4, omitting None, in field order
    for name in callerList, while saved.count < 4:
        saved.append(name)                 # no deduplication or None check here
    continue the separately recovered random-fill stage
```

Manager initialization `1431538a0` inserts a row into `+0x348` only when copied-row
byte `+0x44` is true **and Rank equals 5**; it appends LotteryWeight to `+0x448`
and adds it to total `+0x440`. The AddWorldTreePal descriptor at `147696840`
names `147696ef8`; its setter `140b9a380` has bytes `c6 41 44 01 c3`, binding
the flag to byte `+0x44`. The established [first-load table and pool insertion
proof](#shipped-composite-first-load-order) supplies aligned order for this map
as well: same insertion/key helper, distinct current keys, no exclusions in this
selector. The ordinary filler's compacted/original-weight mismatch is not used
by this initial World Tree draw.

The current main/Common tables agree and yield this complete ordered pool:

| ID | Rank | LotteryWeight | Integer tickets, inclusive | AddPal |
| --- | --- | --- | --- | --- |
| WorldTree_ATK | 5 | 5 | 0–4 | false |
| WorldTree_DEF | 5 | 5 | 5–9 | false |
| WorldTree_CraftSpeed | 5 | 5 | 10–14 | false |
| WorldTree_FullStomach | 5 | 5 | 15–19 | false |
| WorldTree_Sanity | 5 | 5 | 20–24 | false |
| WorldTree_MoveSpeed | 5 | 5 | 25–29 | false |
| WorldTree_ATK_DEF | 5 | 5 | 30–34 | false |

Total is 35. Every valid ticket 0–34 selects an entry; the current fresh pool
therefore supplies one non-None Name to the Blueprint array. Each entry has five
tickets, giving nominal 1/7 under a uniform-ticket assumption. The executable
uses `rand()%35`, not a uniform-ticket primitive: enumerating all 32,768 scalar
values 0–32767 gives counts **[4685,4683,4680,4680,4680,4680,4680]** in the order
above. These are conditional scalar-model counts, not measured gameplay rates,
independence or World Tree spawn frequency. Empty/malformed pools retain the
None fallback; a valid manager then returns `[None]`, not `[]`.

The addition may be discarded if the character defaults already fill four slots.
Otherwise it is appended before random filling. Since these seven rows all have
AddPal=false, they are not ordinary random-fill candidates and their presence
alone does not remove an ordinary weight. Later ordinary additions can still
reach the already documented filler mismatch. The same AddPal requirement also
prevents these rows from being transferred as synchronization candidates through
the recovered capture filter. No universal final passive count is inferred.

#### Aura effects and capture callback are separate from the rolled trait

The shipped spawner default `WorldTreeAuraPassiveSkillNames=[WorldTreeAura]`
supplies a separate runtime effect grant. Decoded `EnableWorldTreeAura` expression
20 invokes `PassiveSkillComponent.AddSkillFromSpawner(Self, names)`;
`OnCapturedWorldTreeAuraPal` expression 6 invokes `ClearSkill(Self)` and then
removes its own capture delegate. These are not calls to clear the saved Name list.

Native AddSkillFromSpawner exec `142a65000` reaches `142c97660` → `142cbd520`
→ the retained source-grant routine `142cbd740`. It expands Name requests into
effects keyed by the supplied spawner actor. ClearSkill exec `142a651f0` reaches
`142ca05e0`, whose selected mutation is:

```text
snapshot component effect aggregates
key = WeakObjectPtr(spawner)
remove key from component maps +0xf0 and +0x1c8
rebuild aggregates; notify effect-type removals/value changes
```

The inspected body and change-notification helper `142ca0c10` operate on runtime
effect records and effect-change delegates; they contain no direct saved
PassiveSkillList erase. This identifies the callback's shown mutation and avoids
mistaking its name for saved-trait deletion. It does not claim that every possible
delegate listener or arbitrary capture setup is harmless. The existing capture
target/source predicates and saved-storage model still govern the final answer.
The separate WorldTreeAura row has AddWorldTreePal=false and AddPal=false; it
is not an eighth entry in the rolled pool.

Decisive native evidence:

| Observation | Evidence |
| --- | --- |
| Reflected selector registration | `ev_6e5741669f66ce210d568487a93599dfa90ca4b365d0075ef3d69023b901886a` |
| Selector exec `142a6a450` | `ev_61362519020464084b7ccaf59e6479aa1f018afee898751924a8aca34a06ec5c` |
| World Tree selector `143151030` | `ev_b00099528a66323c9b1a92b152e8bc0765e2a4bfb871d7e27c456e63b1c4813e` |
| Original-index draw `143150a80` | `ev_a286449d2fb844fa5612405f43f45cafba7b578ef9220e5a208f7b7a77815c58` |
| Pool construction `1431538a0` | `ev_b6512d87ba3ec4be885c3508889c3618358a62c6780c412d93c8f61972bb0468` |
| AddWorldTreePal descriptor / setter | `ev_905ef24a2a7d01159c6ed830d72ad70f28ac6077ccc1d1dc70c7f709b88a4fab` / `ev_c64f153646c8dca6b861104c2770bac380fd1f2d288a4252dceca807803ee9d3` |
| AddSkillFromSpawner exec / request expansion | `ev_c804526f024eeef2a72ff7835b483b2d326ac888d7a32a39502c8f77e81fb248` / `ev_adec933738088d09ca62cb38a2e7ad954ba3ae6a99abd518e30bc29d8b76201c` |
| ClearSkill exec / map removal | `ev_e7ecdee4cb27829917654969f30c1ff5e34e506cf4bba37ee8a05347871e1a91` / `ev_094f5bbca3779839e52bb71c466e7ee4c85c44d0bb363d671bd01cd0a8b7a91e` |
| Effect-change notification | `ev_24814d5deaad31b60fecfe26210675cf92a3210d57392ab5e90061f177255042` |

Fresh extraction preserves the retained script/table bytes. JSON SHA-256:

| Artifact | SHA-256 |
| --- | --- |
| GetRandomWorldTreePassiveSkill script | `8175a440a2e3cf2ebdc74304fc2349ac188f03cf1297b36c7ce6618a62573659` |
| EnableWorldTreeAura script | `03bbbe4657ad59a6fc1469d36942b281384423eed69395ff513c0ad51c500fb3` |
| OnCapturedWorldTreeAuraPal script | `dc51bbb459207106ca54a174f9a0a6599789d771885090785cb3273dc3bcbfd3` |
| Spawn script | `981374f9d209ad2497d74bd8450f3d4183bf866853f5c81b80cbb74fba66a74e` |
| DT_PassiveSkill_Main | `ce22ba86cf0a461289f380d9bb12dcbc6b73a003b217bd678ad37cf76d472eab` |
| DT_PassiveSkill_Main_Common | `a405785eae0c0e246f248dc9c246db024213bab48c1e87053f873af6dcf7cb91` |

Checks cover valid/invalid manager arrays, all 35 tickets, the conditional 15-bit
scalar enumeration, a full four-slot default list, duplicates and the None fallback.
No game execution or player save was used. Target/source conditions are given in
the capture sections below; caller-specific inputs and unclaimed activation
frequencies remain explicit.

### Capture additions

The reflected PalCaptureSuccess wrapper reaches `0x14330e480`,
`ev_28d20aad0c5a10662e2c15f3ee107d9e5c3a13f3fdf3aa8fb868eeb0b9d4e471`.
It invokes `0x142cc1810`,
`ev_a7c5e213dc1c3c9b8b2b11f37daf681a4d4ad84152f44d0456a79e52dd61b2a0`:

```text
collect source effects of type SyncroPassiveWhenCapture (0x55)
for each source, while existingCount+pendingCount < 4:
    for each source passive, under the same cap:
        require passive-row lookup and row.AddPal
        require row.EffectType1 != SyncroPassiveWhenCapture
        skip names in captured Pal's existing list
        if IntegerPercentRoll(trunc(sourceEffectValue)): pending.append(passive)
append pending entries through the passive-write helper
```

Fresh executable name/value record at VA `0x147690960`, file offset `0x768ed60`,
contains qwords `0x1476924a0, 0x55`; the pointer names
`EPalPassiveSkillEffectType::SyncroPassiveWhenCapture`. Its neighboring records
bind FarmCropGrowupSpeed=0x54 and ActiveSkillCoolTime_Decrease=0x56.
The EffectType1 descriptor at `0x147695d30` names the field and stores offset
`0x19` at descriptor `+0x38`. The AddPal descriptor at `0x1476967a0` points
to bool setter `0x140e66560`, whose bytes `c6 41 42 01 c3` decode to
`MOV byte [RCX+0x42],1; RET`. These independently bind the helper's copied-row
tests. Only EffectType1 is tested; this is not an exclusion over every effect slot.

No deduplication against *pending* entries is visible. Five freshly extracted
SyncroPassiveWhenCapture partner rows have values 15/18/21/24/30 and target
ToTrainer. Source activation/membership is still a separate proof obligation.
Collector `0x142caca50`,
`ev_639c49590e6759d8d48019ade4ec168a999d9b88852439637c925b2559e8495d`,
returns the first matching effect per source in its registered effect map; this
does not prove every party member contributes an active source.

Percent helper `0x143309cc0`,
`ev_eebe9d0fc015866ce20bcdf7003f41f4a3511bf63b19fc1bd98f724d8c944042`,
uses `min(trunc(float32(r)*0.003051851...),99) < integerPercent`.
Append helper `0x142f733b0`,
`ev_b0f6193db79159bad65c5f7c010fdc88668b0be51ab07673b2d7cd75b65bccb2`,
checks row lookup and the four-slot gate before writing object `+0x460`.
A separate capture branch checks no rank>3 ordinary-pool passive and fewer than
four slots, then obtains the player's RainbowPassiveRate relic effect. Fresh enum
record `0x14740a880` pairs name pointer `0x14740aa18` with numeric value 11.
Assembly of capture core `0x14330e480`,
`ev_7b1dce575b49fefe1a4b5558b410658bc222df6eb675f137d47ce1917d7c7dbe`,
closes its argument flow:

```text
0x14330e893: MOV DL,0xb
0x14330e895: CALL 0x143190470   # RainbowPassiveRate value returned in XMM0
0x14330e89a: COMISS XMM0,XMM9  # zero
0x14330e89e: JBE skip
0x14330e8a0: CALL 0x143309c60   # same XMM0 percent input
```

The float-percent helper rejects nonpositive rates, accepts rates at least 100,
and otherwise tests `float32(r)*0.003051851... <= rate`. On success the branch
draws from AddPal-and-Rank>3 and appends a nonempty result. The player's relic
progress/value remains a conditional input; no player data was sampled.
The shared storage section below establishes ordinary transfer and the connected KingWhale callback. Other material upstream contracts remain scoped gaps. Dependencies include caller modes, native count defaults, pools,
source activation/order, percent roll, append helper, relic branch and storage.

### Capture augmentation: predicate and low-byte boundary

Retained capture-core assembly
`ev_7b1dce575b49fefe1a4b5558b410658bc222df6eb675f137d47ce1917d7c7dbe`
establishes the gate before synchronization/relic additions:

```text
14330e71a MOV RCX,RDI
14330e71d CALL 143307e60
14330e722 TEST AL,AL
14330e724 JZ 14330e91e       # skip additions
```

Predicate `0x143307e60`,
`ev_a6193099ced41b95e96bb328c9eae84313ec0628f3f04b2c83571755377b366b`,
returns a meaningful low byte; decompiled upper return bits are not a boolean:

```text
if actor invalid: return low_byte_false
parameterComponent = actor's parameter component
if live parameterComponent and 142c1c970(parameterComponent): return false
system = 1432e52e0(actor)
if system invalid: return false
return not low_byte(142f4c4e0(system,actor))
```

`0x142c1c970`,
`ev_abebf6ccb3bbd15f215f0191c71d336210c0387127a45055116a709563b997ae`,
tests the live trainer reference at parameter-component `+0x148`.
The other helper `0x142f4c4e0`,
`ev_716d94cea24d9f4cf526b79c3722f942b88bcd7b912d819d20e3208b408424e1`,
returns false for an invalid actor or missing resolved individual. For a valid
individual it returns true if a guarded GroupId-keyed lookup finds a live map
entry, `OldOwnerPlayerUIds.Num != 0`, or `IsPlayer` is true. The names and widths
are bound below. A valid PalGroupManager is required by the outer predicate;
missing individual resolution is not used as an ordinary-setup shortcut.

#### Capture target fields: player, owner history and group

World lookup `1432e52e0` resolves **PalGroupManager**. The target parameter
component's `IndividualHandle` (`+0x170`) resolves the installed
`PalIndividualCharacterParameter`; its save begins at live `+0x3d0`.

| Live offset | Saved member | Verification landmark |
| --- | --- | --- |
| `+0x480` | `IsPlayer`, save `+0xb0` | Descriptor `1474e96d0`, setter `1411562c0` |
| `+0x4a8` | `OldOwnerPlayerUIds.Num`, save `+0xd8` | Array descriptor `1474e9810`, base save `+0xd0` |
| `+0x6f8..704` | `Dynamic.GroupId`, save `+0x328` | Dynamic descriptor `1474eac00`, nested Guid descriptor `1474e8230` |

```text
for a valid resolved target individual:
    require no live trainer in target parameter component(+0x148)
    require a valid PalGroupManager
    if nonzero GroupId has a matching LIVE entry in the guarded manager map(+0xf8): reject
    if OldOwnerPlayerUIds.Num != 0 or IsPlayer: reject
    otherwise permit synchronization/relic additions under their filters and cap
```

The group lookup retains the occupancy guard (`manager+0x100 != +0x12c`), full
four-dword key and live-entry check. The history test reads **Num**, not capacity:
an allocated empty array does not fail it. The outer predicate's missing-individual
fallback is recorded above, but is not used to prove the ordinary setup.

Constructor `142847780` supplies false IsPlayer, empty history and zero GroupId.
Defaults alone do not prove state at capture; the joined setup below includes
later installation, group assignment and concrete pre-gate dispatches. Decisive
inner-gate widths: `142f4c5ab CMP dword [RBX+4a8],0`, then
`142f4c5b4 CMP byte [RBX+480],0`. Retained observation:
`ev_53081e989839cdac517418b882da4211eb74bccf48a5040aff8665cfae6939dc`.

#### Ordinary-target construction and pre-gate dispatch boundary

The common initializer's supplied Guid is written to save `+0xc0/+0xc8`,
**not Dynamic.GroupId** at `+0x328/+0x330`. The actual parameter-component
constructor `142c08730` initializes trainer `+0x148=null`. Binder `142c78ca0`
writes the handle/parameter at component `+0x170/+0x178`; numeric refresh
`142c2c950` is not a trainer assignment. The selected capture order is:

```text
14330e480(captor, target):
    remove target from player-key battle tracking (142dc5be0)
    if tested NPC class and equipment.count > 3:
        trim to first three through AddUnique; emit equipment notifications
    broadcast target.OnCapturedDelegate(captor)
    resolve target's installed individual
    if 143307e60(target) returns nonzero AL:
        apply synchronization and relic additions
    update ownership/history and store the individual
```

Those notifications precede augmentation. Neither construction defaults nor
disjoint direct stores justify omitting their actual recipients. The following
contracts support the [joined target conclusion](#fresh-pinkcat-capture-target-argument)
without asserting arbitrary listeners harmless.

#### Selected ordinary callbacks and trainer assignment

The selected target ancestry is `BP_PinkCat_C` -> `BP_MonsterBase_C` -> native
`PalMonsterCharacter`; PinkCat has no decoded function children or parameter
template overrides. Standard-spawner defaults select
`BP_MonsterAIController_Wild_C` and native `PalAICombatModule_Wild`.

**Standard callback guards.** `BP_PalSpawner_Standard` binds OnCaptured only when
**both** its SpawnerType and the actor's reported type are in `{2,4,8}`. The
selected supplied `SpawnerType=0` therefore excludes this binding regardless of
the actor getter. When bound, its boss-save action additionally requires actor
type 2, and its server defeat action actor type 2/4. A World Tree aura has a
separate binding; non-aura excludes it. Other supplied modes retain these guards.

Stored type `+0x8dd` is not always the reported type: getter `142cd4b90` first
tests `+0x8db -> 8`, `+0x8d9 -> 5`, `+0x8da -> 6`, then the low-byte live-individual
predicate `142cd6b80 -> 1`, before returning the stored byte. Setter `142ce4310`
changes only that byte. Constructor zeros do not settle every later getter input.

**Group assignment.** Earlier AddGroupCharacter is independent of the OnCaptured
guard. A PinkCat-only request has `Organization=None` (row `+0x53`, enum zero).
The group request keeps these distinct modes:

```text
if supplied nonempty name resolves an existing group: reuse its identifier
else if organization != 0: use the organization group's identifier
else if spawner already has its created group: reuse its stored identifier
else: request a new kind-1 group; on success save its identifier in the spawner
```

Kind 1 creates `PalGroupNeutral`. Registration `142f40200` inserts it into the
general map `+0xa0` but returns before the Organization/GuildBase-only `+0xf8`
rejection map. Successful membership writes its Guid to the individual's saved
Dynamic.GroupId through `142f45d60` -> `142f8bf70`; missing membership backing
state can skip that write. A **nonzero Neutral GroupId therefore does not itself
cause GuildBase-map rejection**. Named-group reuse, other organizations and later
reassignment require their own supplied state. The earlier `143234290` association
is PalSquad tracking, distinct from this proven membership write.

Landmarks: `RequestCreateGroup` -> `1430ffbb0` / `1430ffcb0`, kind factory
`142f4d1b0`, completion `1430e2760`, AddGroupCharacter `1430dd8d0`, registrar
`142f40200`, and GroupId setter `142f8bf70`. The decisive Neutral/GuildBase
registration observation is
`ev_6282294d76f089925c801c6b94edf408e09f4d079adf8b65c252048ad6c80a0a`.

**Controller and trainer recipients.** Ordinary Wild setup `142d31070` binds
OnBattleFinish_forDelegate (`142d29140`), whose shown writes remove battle
tracking and clear the module weak reference. It does not bind KingWhale's
different OnCaptured setup (`142d30810`). Shared diagnostic strings do not make
these implementations interchangeable.

The real trainer setter `SetTrainer` -> `142c2c6c0` writes parameter-component
`+0x148`. Known `SetTrainerForOtomo` (`142c8be70`) invocation from the holder's
spawn callback receives the **companion**, not the independent wild target.
Its other weak-reference store at `+0x150` is a different field.

The other checked setter route `142f259e0` belongs to **PalFunnelCharacter**.
It copies a source handle/parameter into the normally constructed Funnel's own
component and assigns that component's trainer. A null source trainer excludes
an earlier event-binding block, not this later setter. Source resolution
`142f19630` requires a valid Funnel manager, tries its first matching association,
then the receiver's identifier fallback; runtime associations remain inputs.
Under distinct normally constructed Funnel/wild components, sharing a parameter
does not alias their trainer-pointer storage. The setter's immediate notification
targets `PalInteractiveObjectSphereComponent`; arbitrary further listeners are
not covered by that direct-write conclusion.

Funnel receiver `wchar_t* +0x318` means byte **+0x630**, its own
CharacterParameterComponent, not the independent individual's actor pointer at byte
`+0x318`. This identifies the direct recipient; no universal alias-exclusion claim
follows. Landmarks: shared actor constructor `142e34a00`, Funnel constructor
`14291d970`, setter route `142f259e0`.

#### Capture ownership history is recorded after augmentation

The same invocation's later ownership write cannot reject its earlier gate:

```text
14330e480: OnCaptured -> predicate/additions -> 14330ed50(captor, handle)
    -> 142e47c30 -> 142e4c410 -> 142f851b0(same individual, ownerGuid)
```

`142f851b0` updates owner metadata, bounds history and appends a GUID if absent.
With initially empty history, its first entry is recorded **after** augmentation.
A future gate with preserved nonempty history rejects additions, but preservation
through every release/transfer/re-capture mode is not established. This ordering
does not substitute for proving the starting state or pre-gate callbacks.

#### Neutral group handle notification

If its known handle binding is enabled, `PalGroupNeutral.OnUpdateIndividualActor`
dispatches through GroupBase exec `141ac03c0`, Neutral virtual `+0x320`, to
`140b81320` (**RET 0**). That specific callback cannot alter eligibility fields.
The handle event is `OnUpdateIndividualActorDelegate` (`+0x28`); this no-op result
does not cover other subscribers or the individual's separate GroupId events.

#### Individual GroupId notifications and account-bound subscriber

GroupId setter `142f8bf70` broadcasts individual `OnUpdateGroupIdDelegate`
(`+0x1c8`) and `OnUpdateGroupIdWithParameterDelegate` (`+0x1d8`). The checked
OnlineManager binder `1431347d0`, reached through `143166a60` / `14311e990`,
gets its subject from `PalPlayerManager.PlayerAccountMap` and that account's
`IndividualHandle`. For distinct player-account/fresh-wild individuals, it binds
the **account individual**, not the wild target. Handler `143132850` updates
online/group-info caches and notifies controllers. This does not establish every
subscriber, arbitrary handle aliases or harmlessness of unrelated network callbacks.

#### Capture equipment notifications: supplied-count boundary

Fresh ordinary generation passes `collectAll=true` to SetupAttacks, so it is not
capped at three equipment entries. For nonrare/no-explicit-override PinkCat,
fresh empty arrays and unchanged generated equipment, the complete natural
learning snapshot is:

| Level | WazaID |
| --- | --- |
| 1 | Unique_PinkCat_CatPunch |
| 7 | AirCanon |
| 15 | MudShot |
| 22 | PowerShot |
| 30 | WindCutter |
| 40 | SeedMachinegun |
| 50 | HyperBeam |
| 70 | HolyBlast |

Thus levels 1–21 have at most three generated entries; level 22 reaches four.
Capture trim `14310b010` then clears equipment and attempts AddUnique for its
first three entries, emitting individual `OnAddEquipWazaDelegate` (`+0x748`) and
`OnChangeEquipWazaDelegate` (`+0x758`) before OnCaptured/augmentation. Freshness
alone cannot exclude that branch. Actual query-time count controls it in other
supplied states. Equipment trimming does not reduce derived overall availability.

#### Equipment-notification consumers and riding-state boundary

The concrete add-equipment callback `LoadWazaActionClass` (`142cd8a20`) writes
the actor's **StaticCharacterParameterComponent** action cache, through deferred
completion `142cc8770` or a fallback entry. This is distinct from the actor's
CharacterParameterComponent and saved gate fields.

The change-equipment consumer `PalActiveSkillSlot.OnChangeEquipWaza`
(`142d02430`) reconciles slot/skill state. The recovered creation/binding path
is conditional on `PalRideMarkerComponent.SyncActiveSkill` or nonzero riding mode
(`142cbb3e0`, creator `142cbff50`). Its generic existence does not establish a
subscription on the selected fresh wild target. Nested skill notifications and
arbitrary extra subscribers are not assumed harmless.

#### Early removal: player-key receiver and battle-state notifications

`142dc5be0` removes the captured target from tracked arrays keyed by
**PalPlayerCharacter**. Emptying an array updates that **player's** battle flag;
`1431648e0` removes the target identifier from the player's **PlayerBattleSituation**
(`+0xe08`) and conditionally emits rank/BGM/near-enemy notifications. These are
player/tracking records, not the captured Pal's GroupId or PalGroupManager's
GuildBase map. Useful landmarks are `ChangeBattleModeFlag_ToAll` -> `14314a180`
and `OnBattleRankDownDelegate` / `OnChangeBGMTypeDelegate` / `OnChangeNearEnemyDelegate`.
Direct receiver/write disjointness does not establish all listeners harmless;
the consequential partner reset listener is modeled separately below.

#### Installed-individual helpers and arena-snapshot guard

Binder helpers `142f8c350`, `142f8c2b0` and `142f904f0` write location, a separate
state byte and an actor-value cache, outside the eligibility fields. Snapshot
restoration `142f8a430` is guarded by **ArenaRestoreParameter.bValid**, save
`+0x270` / live `+0x640`. Fresh construction sets it false, so unchanged fresh
state returns before restoration or notifications. With a supplied valid snapshot,
the branch restores HP, food/sanity/sickness state and may notify status/food
listeners; it is not assumed absent or harmless for every later save. No arena
world-subsystem presence alone implies a valid individual snapshot.

### Installed actor initialization dispatch

Binder `142c78ca0` associates the same installed individual/actor, updates the
small state/cache fields above, and dispatches pending initialization by a copied
stage map, sorted by stage key, before the native Monster override. Pending
callbacks remain an explicit API input; the selected standard route supplies
none, as described next. NPC stage 1 registers MasterWazaSetup; stage 2
`OnCompletedInitParam` (`1430f7e30`) handles guarded health/status/collision and
component refresh. The actual status and damage recipients are distinct from the
captor's passive source registry. This contract alone is not every target's
capture-time eligibility proof.

### Standard creation callback is separate from pending actor initialization

For standard-spawner `SpawnNewCharacter`, wrapper `142e53e20` -> `142e54050`
supplies an **empty pending actor-initialization delegate** and empty optional
parameter callback. The spawner's Spawn Delegate is a separate completion value.
It runs through readiness/completion **after actor binding**, then performs the
group/controller/setup sequence above. This is established from the supplied
arguments, not assumed harmless callback behavior. Other creation APIs can still
supply pending initialization callbacks. Landmarks: `142c8d180` pending insertion,
`142c60fd0` binder-before-readiness, `142c8db70` readiness, `142e36ae0` completion.

### RideMarker construction and selected component declarations

Native `PalRideMarkerComponent` constructor `142c95220` sets zero mode and null
SkillSlot. Zero-mode `142cbb3e0` returns before slot creation/binding; later
SyncActiveSkill is a separate possible input. The checked PinkCat/MonsterBase
serialized component declarations and selected native constructors do not create
a RideMarker. This is a construction/selected-asset boundary, not a runtime
component census or proof that dynamic creation is impossible. It supplies no
fresh-target riding subscription merely from the generic slot callback's existence.

### Final ordinary-target initialization virtuals and eligibility

The selected `PalMonsterCharacter` table `14760fe98` binds the last two concrete
initialization dispatches:

| Slot / body | Scoped contract |
| --- | --- |
| `A30 -> 1430f6cf0` | Trainer/group/history predicate and optional local-player query; no selected-target assignment. |
| `688 -> 144f0c2e0` | Engine Actor **ForceNetUpdate**; named/default driver scheduling and conditional dormancy handling. |

ForceNetUpdate's `NetDriverName` / `NetDormancy` are actor fields, not the parameter
component's trainer pointer. Its recovered NetDriver and PalReplicationGraph
routes update actor-keyed network records and notify network-owned lists; they
do not resolve the saved individual or assign its eligibility/passive fields.
Useful landmarks: ForceNetUpdate native pair `14800b910`, NetDriver handler
`1454953b0`, graph handler `1417a4d60`. Arbitrary replacement drivers, custom
listeners and modified world configuration are not covered by this boundary.

### Fresh PinkCat capture target argument

The declared setup is a fresh **PinkCat-only standard-spawner request**,
supplied SpawnerType 0, non-aura, newly created no-organization Neutral group,
valid manager/individual, entering first capture without external ownership/group
reassignment or component replacement. Under the recovered native routes:

| Stage | Joined result |
| --- | --- |
| Fresh save/install | False IsPlayer and empty history reach the installed individual; supplied initializer Guid is not GroupId. |
| Trainer/identity | Trainer starts null; binding connects the same individual. Known holder/Funnel setters have distinct component recipients. |
| Group | Neutral membership may write a nonzero GroupId but does not register it in the GuildBase rejection map; its known handle callback is no-op. The online subscriber's account subject is distinct. |
| Initialization | Fresh snapshot is invalid; selected NPC stages, callbacks and final virtuals have the bounded contracts above. SpawnNewCharacter supplies no pending actor-init delegate. |
| Pre-gate capture | Removal writes player battle tracking; equipment notifications have concrete cache/conditional slot recipients. SpawnerType 0 and non-aura exclude the selected spawner capture handlers; ordinary Wild does not register KingWhale's handler. |
| Gate and output | Trainer remains null, IsPlayer false, history Num zero; Neutral GroupId causes no GuildBase rejection. The target passes the named augmentation gate, then uses the separately conditioned source/filter/roll/cap/storage model. |

The binding's facial update (`142f92ed0` -> `PalFacialComponent` ->
`142c2a3d0`, `PalFacial_MainModule`) writes facial/module state, distinct from
those gate fields. It is a verification landmark for this setup, not a universal
rendering/helper guarantee.

This is a static state-transfer result, not an effective placed-spawner
distribution or a claim that every first capture is eligible. Named-group reuse,
other organizations, supplied callbacks, changed source/world state and actual
registry positions retain their explicit inputs and predicates. The unchanged
sole-source GuardianDog grant and completed-reset result below connect the named
source without asserting arbitrary live party membership.

### Battle-mode source reset is scheduled, not invoked during registration

The pre-gate player battle-mode notification can reach a partner handler bound
by InOtomo entry `14312b4b0`. Its actual listener `14312c7f0` registers a weak
method delegate for reset `1431133d0` with the timer manager. Registration
`145830bd0` validates/copies the delegate, stores a timer record (`145813500`)
and inserts its handle in the heap (`14580b740`); **it does not invoke reset
inline**. The distinction matters because reset removes/retries partner grants.

If no timer processing or other source-changing event intervenes before
augmentation in the same synchronous capture, this scheduled reset does not
alter that query's supplied source set. It does not prove no prior queued reset,
that reset never executes, or that other listeners preserve every source input.
An earlier reset that has completed has the more specific result below.

Useful verification landmarks are the actual listener/reset pair above,
`143024970` (delegate builder), and the timer registration/heap helpers. Retained
listener observation: `ev_97506f011348f96f0741fb630c3f8903fcc8f8ee234e251487c8b72b7288dd06`;
scheduler: `ev_63b24ccaa698a045f6aa4f2c0d05d374f3a3b81cb6cf8e6db30fd6d257a981fa`.
The distinct OnChangeBattleMode_ForOtomoBattleSpeed listener changes movement
factors in its shown path; a shared diagnostic does not identify it as reset.

### Completed partner reset and unchanged GuardianDog regrant

**2026-10-28.** A completed reset has a more specific answer than an unknown
future timer event. For the declared ordinary sole-source, active rank-1
GuardianDog, retain the same valid owner/trainer/recipient, converted rank
definition, unlock/restriction/disable conditions and shipped world exclusions.
Assume the reset reaches completion and the query is not reentered partway
through its removal/retry sequence. Under these conditions its ActiveOtomo retry
reaches the already established accepted type55/value15 grant again.

```text
Reset1431133d0(handler):
    remove prior invocation paths, including Active removal14312e460
    clear accepted effects for the collected invocation flags via143135b10
    Retry143110840(handler)

ActiveRemoval14312e460(handler), on authority:
    remove common invocation flag via143125880
    RemoveFlags1431254b0(handler, [Active flag from14940bd00])

RemoveFlags1431254b0(handler, flags):
    143135f30(handler, flags)                 # remove matching accepted effects
    for each occupied handler state at88:
        remove every matching flag from state.invocationFlags
        if state.invocationFlags is empty: collect state key
    delete each collected state with1431353f0

Retry143110840(handler):
    run Worker2, Ride4 and Reserve8 entries
    run Active14312b280(handler)              # flag1
    run InOtomo10 and Always40 entries
```

The global14940bd00 is the same invocation flag supplied by Active entry14312b280
and removed by14312e460. Its other direct user143135b10 includes it in the
all-path accepted-effect clear. By contrast14312eed0 removes Worker-related
flags14940bcf8/14940bd20; it is not the Active entry or its matching remover.
This distinction follows the actual globals/calls, not function-address proximity.

The converted GuardianDog synchronization definition has trigger mask1 only.
The pre-Active retries therefore cannot assign that definition through2/4/8.
The Ride retry's extra14313d0a0 ->143132730 reads movement-effect information and
writes the named **PartnerSkill_MoveSpeed_Ground** modifier through142bfb5e0;
the inspected bodies do not change the handler guard byte38. No
generic movement or callback lifecycle claim is needed for that direct boundary.

After Active's sole invocation flag is removed, its flag-only handler-state entry
is deleted. The existing state1 skip in143110fd0 therefore cannot by itself
suppress this retry. Active14312b280 supplies the same flag and trigger1 again.
Its assignment predicate143127a90 requires a clear handler byte38 and a live
trainer, along with the retained additional-condition/restriction checks. It does
not use the player's battle-mode byte as a substitute for those inputs. With the
declared unchanged conditions, the dispatcher selects rank1, inserts the new
state and invokes143115000. The already established GuardianDog request,
trainer acceptance and world-exclusion contracts then restore type55/value15
under that same source actor's weak key. InOtomo10/Always40 do not match this
definition's mask and cannot supply a second invocation of it.

**What this settles:** an earlier queued reset that has fully completed is not, by
itself, a reason to leave this unchanged sole-source answer unknown. Before the new
scheduled reset runs, the prior accepted record remains; after a complete eligible
reset, the accepted contribution is restored. Reinserted sparse-map position can matter
with multiple competing sources, but cannot change which grantor supplies the declared
sole contribution. This is a conditional state-transfer result, not a live scheduling
measurement or a guarantee about a query reentered during reset, changed
restriction/disable inputs, failed validity gates, changed recipients, or arbitrary
other registered sources. Those cases retain the general query-time input model. Target
eligibility is established for the declared setup in the target model above; other
supplied target/source states retain the general predicate model.

| Decisive observation | Evidence |
| --- | --- |
| Reset / retry convergence / Active entry | `ev_b7d7936d3a170dd8b4307e489130a5147434ff3745dfc1a9eb7468b75c9bab87` / `ev_83575715c6ee86a03df4f2255c63b07bfc7b733f0c9218f1ab8eb80d12cee9f6` / `ev_becd6a95bd1f94aec2e8e444e79d3cc10781bfcfb520f8abb6b0720c5ec10d49` |
| Active removal / all-path accepted-effect clear | `ev_bc0b3e118198cef0e172473b1c3bee86a5c841bfa19f1320934eeb93ab9fdc20` / `ev_df75689f44d6dfefdf01e39465c962fabd8ba4cdb2529309ee845bbd8961750c` |
| Common removal / flag removal / state deletion | `ev_90934178b0e5016163d85814719aa7da299b66cc1894070e5de0f69be7299875` / `ev_c8f0c328c1a484a2136cc88ea9038b230f4a6c3c17a22925f5a1e1b50821a9e6` / `ev_ea0f93f894f86fe3dd86bf7e74d0b7b42976af768fef8fd36fb55ab17b5931df` |
| Removal argument assembly | `ev_9e419c3dcc86a3fd368c831127d7049131bddf9ff580c63c0f6a9e710b190274` |
| Assignment state/mask gate / trigger predicate | `ev_3bfb9d6a1652fb989dee2c0f57985960fc71dfe1248fb54754cb573fe1a66baf` / `ev_9c34fd44838d70f95118eb7483781f43ca5a4ef1ad32ba0b8c08e67daceae7c3` |
| Worker / Ride / Reserve retry entries | `ev_107054494a8f1117a8710a281d10eac68a952b65118e5acf878366dda632586f` / `ev_9a329591288fb97d6e43c5952ede75a8f58583c507008724c0b90402685259f4` / `ev_319044620a4cd9849c3b18ecf01864df00e2f13433f42cf33101a4b5e70af061` |
| Ride movement refresh / modifier recipient | `ev_5ec95d39123530b96f5ccbe615c80f9317c5e248506b855647c20823f97b837a` / `ev_b0cad1eefb3af356776507edbde8b694c3f1c792844aaf1585f43002b1b9307c` |

### Capture source identity and activation inputs

The captor's PassiveSkillComponent is actor `+0x680`. Its source map at `+0xf0`
is keyed by the grantor actor's weak reference, not party slot or passive name.
Fresh grant routine `0x142cbd740`,
`ev_e7618f8be5ddd1b1c2a7ed4bcf5aee6882fd77dedf172ec478423ca0dc366c8d`,
populates it as follows:

```text
expand each passive request into accepted effect records
entry = FindOrAdd(component.sources, WeakObjectPtr(grantorActor))
if caller requests replacement: clear entry.effects
append accepted effects to entry.effects
rebuild aggregated skill information; notify changes
```

Manager expansion `0x14314fae0` visits all four row effect slots. Target filter
`0x143143bc0`,
`ev_c1a93a069de3ae2f375bfcac1a92085043c885583c05ed7c8d8fcc3797f9237e`,
rejects None and disabled effect types; an explicit target override requires a
matching slot target. Without that override, ToTrainer requires all three request
mode bytes `+8/+9/+10` clear. Self-only and generic mode-flag-one grants therefore
do not establish player capture-source activation. Recovered applicators
`0x143115000` and `0x143147390` grant applicable effects to a validated trainer
PalPlayerCharacter under their own state/mode conditions. The collector then takes
the first matching synchronization effect per registered grantor; capture validates
that live grantor as a Pal actor before reading its stored passives.

Fresh `DT_PartnerSkillParameter` rows GuardianDog and BOSS_GuardianDog reference
SyncroPassiveWhenCapture_PartnerSkill_1..5 in rank order, with prerequisite
SkillUnlock_GuardianDog. The corresponding passive rows have ToTrainer values
15/18/21/24/30 and InvokeActiveOtomo=true; InvokeWorker, InvokeRiding,
InvokeReserve, InvokeInOtomo, InvokeAlways and InvokeInBaseCamp are false.
IsStackablePartnerSkillBySameTribe is false. These are shipped activation
declarations, not proof that every party member contributes.

Native `UPalPartnerSkillPassiveSkill::IsAssignablePassiveSkill`, `0x143127a90`,
`ev_9c34fd44838d70f95118eb7483781f43ca5a4ef1ad32ba0b8c08e67daceae7c3`,
tests an activation mask and event-specific conditions. Conversion of InvokeActiveOtomo to trigger bit 1 and ordinary restriction/removal are established in the shared effect sections. The ordinary GuardianDog active invocation is connected below; the five asset values alone still do not establish unconditional live capture probabilities.

#### Capture source traversal order

**2026-10-28 ordering result.** Collector 142caca50 reads the source map at
component +0xf0 using its occupied-slot bitset. Its initial scan chooses the least
set bit; after a source it clears that bit from the remaining-word mask and calls
140b8c710. That helper picks the least remaining bit, advances to the next
32-slot word when necessary, and returns the end index when exhausted. Therefore
sources are appended to the collector output in **ascending occupied registry-slot
index**, skipping holes. Within each source it selects the first matching effect
in the stored effect array. Consumer 142cc1810 visits those output records in
that order and visits each source's saved +0x460 passive array in stored order.
It does not sort either list by party position, species or passive name.

This is a runtime-dependent order rule, not a missing canonical static ordering.
The occupied positions and their associated grantors at query time remain inputs.
For the declared sole-contributor GuardianDog setup, source ordering cannot change
which grantor is first. For broader setups, membership and position can still
change the resulting captured list through the four-slot cap.

A conditional algorithm witness makes that consequence explicit: start with three
captured passives, supply two valid source records with distinct eligible names X
and Y absent from that list, and let each source value be 15. With the first tested
integer-percent draw equal to zero, it succeeds. If X's source is first, X fills
the last slot; reversing the source records makes Y fill it. The second record is
then skipped by the cap. This is a synthetic branch witness under supplied source
records, not evidence that two such active sources occur in the declared setup or
a measured probability claim. No source-state distribution is inferred.

| Decisive ordering observation | Evidence |
| --- | --- |
| Sparse occupied-slot traversal and first matching record | `ev_639c49590e6759d8d48019ade4ec168a999d9b88852439637c925b2559e8495d` |
| Next occupied index, 140b8c710 | `ev_7616bd7789eafad694d316d68d8bcb5be1a116cb1ee51709638b3c89b47e3010` |
| Ordered source/passive consumption and cap | `ev_a7c5e213dc1c3c9b8b2b11f37daf681a4d4ad84152f44d0456a79e52dd61b2a0` |

### Ordinary rank-1 GuardianDog: active grant to capture collector

**Question and setup.** For one valid ordinary rank-1 GuardianDog activated through
the shipped player holder, does its synchronization record reach the same trainer's
capture collector? The authority-side handler is initialized from the shipped
partner table, `SkillUnlock_GuardianDog` is present, party data and trainer are
valid, and no ownership/rank/unlock/restriction/disable change intervenes. There
are no other contributing sources. Effective type-exclusion lists must not contain
`SyncroPassiveWhenCapture` (`0x55`). The inherited component default and the
[effective shipped-world arena binding](#effective-arena-subsystem-and-shipped-world-binding)
establish that neither decoded arena list excludes it for this setup. The output here is accepted source membership and its capture
use, not a universal success rate for every catchable target.

The fresh base package `Pal/Content/Pal/Blueprint/Character/Monster/PalActorBP/GuardianDog/BP_GuardianDog`
confirms `BP_GuardianDog_C` extends `BP_MonsterBase_C`. Its passive component
subobject templates `Default__BP_MonsterBase_C:PassiveSkillComponent` without
serialized overrides; the base component serializes SkillStatusMap only. The
native empty exclusion default therefore applies before runtime setters.
The rank-1 partner definition has zero delay, WorkType None, empty item/map/tribe/
element/other-companion constraints, AssignOthers false and FloatValue1 zero.
Its passive row is ToTrainer, SyncroPassiveWhenCapture, value 15,
InvokeActiveOtomo true, other Invoke flags false, and additional trigger types None.

The retained `BP_OtomoPalHolderComponent:ActivateOtomo` script supplies the missing
ordinary invocation edge. Its selected slot's individual resolves to `tempPal`
(the [ordinary slot contract](#ordinary-nondefault-slot-identity)); expressions
35/38 get that actor's `PalPartnerSkillParameterComponent`, 36/37 test validity,
and 39 invoke `OnActivatedAsPartner`. This is a decoded call on the companion's
component, not inference from the event name alone.

| Native registration pair | Name pointer / text | Exec entry / target |
| --- | --- | --- |
| `0x147687a40` | `0x147682b00`, OnActivatedAsPartner | `0x142a5d5a0` → `0x142cb0c90` |
| `0x14768cfe0` | `0x147688928`, OnActivatedAsOtomo | `0x142a61dc0` → `0x14312b280` |

Both wrappers unpack the execution frame then tail-jump. Their exact bytes are:

```text
142a5d5a0: 488b42204533c04885c0410f95c04c03c04c894220e9d6362500
142a61dc0: 488b42204533c04885c0410f95c04c03c04c894220e9a6946c00
```

Recovered `0x142cb0c90` validates handler `+0x230`, clears handler byte `+0x38`,
then calls `0x143130c70` and `0x14312b280`. The former performs previous-flag
removal through `0x143125880` / `0x1431254b0` and removes a worker delegate;
`0x143125880` also clears `+0x71/+0x72`. These shown reset/removal bodies do not
restore `+0x38`. The active entry dispatches trigger `1` on authority. The cleared
byte satisfies the trigger-1 guard in `IsAssignablePassiveSkill` for
this activation path, subject to its valid-trainer and definition checks. The
reflected wrapper and Kismet invocation connect the component callback.

```text
shipped holder.ActivateOtomo(valid selected companion):
    component = companion.GetComponentByClass(PalPartnerSkillParameterComponent)
    component.OnActivatedAsPartner()
        handler[0x38] = 0
        remove previous invocation flags / worker callback
        dispatch handler with ActiveOtomo trigger 1

shared dispatcher, for the rank-1 GuardianDog definition:
    require authority, valid owner/trainer, unlock, assignment and disable gates
    require converted trigger mask includes 1 and row conditions pass
    issue immediate trainer grant through 143115000 -> 142c97620
    expand/filter through 142cbd740 -> 14314fae0 -> 143143bc0
    append accepted (type=0x55, value=15) under companion weak-actor key
        in trainer passive-component source map +0xf0

capture by that same trainer, while the record remains accepted:
    require captured-target predicate 143307e60 returns nonzero AL
    collector 142caca50 reads first matching 0x55 record per source
    for eligible source passives while total captured/passive additions < 4:
        apply the existing integer-percent roll with input 15
        append successful eligible additions through the saved-list writer
```

**Acceptance is not inferred from trigger bit 1.** Trainer wrapper target modes
`+8/+9` are zero; `+10` is separately computed by comparing the trainer recipient
with the selected companion actor. The established valid ordinary party identity
makes that comparison false. With no `0x55` exclusion, ToTrainer filtering accepts
the record. GuardianDog's row has no extra source-condition gates and no delay.
The accepted-record map and source-key collector are already established in
[capture source identity](#capture-source-identity-and-activation-inputs); the scalar
pickup cache is not the synchronization collector's source.

**Supported answer and limits.** This closes ordinary active invocation and
connects the rank-1 grant to capture consumption under the declared conditions.
Value 15 is the nominal integer-percent input for each eligible source passive,
not a 15-percent guarantee of an added passive on every capture. The target must
pass [the separate low-byte predicate](#capture-augmentation-predicate-and-low-byte-boundary);
fewer than four passives alone does not prove eligibility. Source row lookup,
AddPal, EffectType1 exclusion, existing-passive duplicates, source order and the
cap remain as recovered in [capture additions](#capture-additions). No live
frequency or universal active-party/catchable-target assertion is added.

The shipped-world exclusion model applies to this effect as well as NaughtyCat's: the
configured Blueprint supplies primary list {0x46} and companion list {0x0d..0x14, 0x78};
native enum records bind those values. Neither the enabled primary/companion union nor
the disabled empty-list branch contains 0x55. GuardianDog's inherited component has no
serialized exclusion override, and its initialization does not add one. Thus effective
arena selection does not exclude this effect in the unchanged declared shipped-default
setup. Custom overrides or intervening restriction changes remain outside it, as already
declared.

The remaining setup condition is target-predicate state, together with keeping
the declared accepted source unchanged until the query. Source order is irrelevant
to the sole-contributor case; broader runtime source membership/positions remain
conditional as described above. General rank/removal branches and an all-writer
audit are outside this fixed setup. The target/source model supports the
declared Wild-Pal passive result within this source contract.

| Decisive observation | Evidence ID |
| --- | --- |
| Component active callback, `0x142cb0c90` | `ev_ac43c72432da3eaafecfb46f97a6c22c8a8caab359af67a5f94fce11d5f193d4` |
| Callback references including reflected wrapper | `ev_08f86268e72f8624f0bd8d48956410d1ad245b282f40f7ab2aed7caa360d347d` |
| Pre-dispatch removal, `0x143130c70` | `ev_1edeac8613c17515e756c5f36a5e51eaef618b70b6b37913af9b4a6354f40735` |
| Reset helper, `0x143125880` | `ev_90934178b0e5016163d85814719aa7da299b66cc1894070e5de0f69be7299875` |
| Prior-flag removal, `0x1431254b0` | `ev_c8f0c328c1a484a2136cc88ea9038b230f4a6c3c17a22925f5a1e1b50821a9e6` |
| Trigger-1 dispatch entry, `0x14312b280` | `ev_becd6a95bd1f94aec2e8e444e79d3cc10781bfcfb520f8abb6b0720c5ec10d49` |

Decoded artifacts use the recorded base archive/mappings/decoder identities:

| Input | SHA-256 |
| --- | --- |
| `BP_GuardianDog-BP_GuardianDog_C.json` | `8ae1b77ae4a5a5b49d3ee167b4da027b4ee33547cf8c14c193798c01c5a17757` |
| `BP_GuardianDog-PassiveSkillComponent.json` | `9f221a249f4b0d853aa115f988031de72c375cb45029e34df0c918c217740a5e` |
| `BP_OtomoPalHolderComponent-ActivateOtomo-script.json` | `7aa3486a44b6abc3739334ac8d9aef611822a212c6335f6e4b86c25025f4072f` |

### Special capture input: KingWhale

Fresh module package
`Pal/Content/Pal/Blueprint/Controller/Monster/BP_AICombatModule_KingWhale_Wild`
has direct native parent PalAICombatModule_KingWhale_Wild and CDO inputs:

```text
CaptureReplaceSourceCharacterID = BOSS_KingWhale
CaptureReplaceTargetCharacterID = BOSS_KingWhale_otomo
```

The shipped `BP_MonsterAIController_Wild_KingWhale` CDO selects that module as
CombatModuleClass. Fresh property descriptors `0x1472cbd50` / `0x1472cbd90`
bind the inputs to module `+0x260` / `+0x268`; registration pair `0x1472cb360`
binds OnCaptured_ServerInternal to generated UFunction getter `0x1428258d0`.
Actual native registration maps that name to exec `0x142829990`, which calls
`0x142d29190`. This establishes a
specific capture-path dependency, not the consumer's final writes by itself.

The source character row has PassiveSkill1=Legend and IsTowerBoss=true; the
target has PassiveSkill1=None and IsTowerBoss=false. Both have MaleProbability=50,
Tribe=KingWhale, CombiRank=20, and matching eight natural learning rows. Matching
ratios and moves do not prove preservation of saved contents or the module's
phase-specific attack overrides. The callback and its hookup are:

```text
OnCaptured_KingWhale(module,pal,captor):
    require server, valid player captor and saved parameter object
    if nonempty configured source/target and saved.CharacterID == source:
        saved.CharacterID = target
        minimum = settings.int32_at_0x128  # original field name not bound here
        for each of HP,Shot,Melee,Defense independently:
            saved.talent = minimum + Index(101-minimum)
        InstallSave(parameter, copy(saved))
        clamp current HP to its recomputed maximum
        clear equipment
        equip the first three entries of AvailableWaza(parameter)
    if module's shared-award flag:
        snapshot converted save and request copies for other eligible participants
```

Native callback `0x142d29190`,
`ev_0bbd5d4986bba409edf04b4c577f73b9de48d869e2391396215d55264df74e2c`,
writes species and all four talents, then copies the existing save through
InstallSave. It does not reroll gender or reapply the target row's guaranteed
passives; an already stored Legend is retained by that copy. The shared-award
eligibility/flag is a separate conditional input, not a claim that every nearby
player receives a Pal.

Setup `0x142d30810`,
`ev_49eb3c285d58f09ab0a2ecc73e8ccb81e2233f04c8fe0dfbc3a7920d9d9b1829`,
registers the module's OnCaptured_ServerInternal delegate on owner actor `+0x778`.
Capture core broadcasts that delegate after the NPC equipment trim and before
its synchronization/relic passive additions. The callback is thus connected to
capture, rather than inferred solely from a configured class or method name.

## Wild-Pal attacks

**Verified, updated 2026-10-29; overall available attacks are the requested output.**
The calculation uses the
[spawn-form/settings bridge](#wild-form-selection-and-saved-level-handoff).
Equipment choice is retained below only to explain data flow and possible writes
to availability inputs. Initializer `0x1432e6070` copies nonempty explicit
attack overrides into both saved arrays; otherwise it calls `0x1432ed8a0`,
`ev_fba26c4790603de3538e3cfa4e16c17a7c435d81f71f758d5b9ac90daa164a74`:

```text
SetupAttacks(speciesForMoves, requestedLevel, collectAll, rare, mastered, equip):
    if context/subsystem/attack database invalid: return
    map = GetMasterableWaza_Level(speciesForMoves)
    if map empty:
        weapon = helper_142e7db80(speciesForMoves)
        w = Human_Punch if weapon in {None,MeleeWeapon,Katana,GiantClub} else Weapon_Use
        mastered.append(w); equip.append(w); return
    sort map ascending by learn level; rebuild map hashes
    if any WazaID is zero or no entry has learnLevel==1:
        mastered.append(Human_Punch); equip.append(Human_Punch); return
    appended = 0
    if rare:
        pool = GetMasterableWaza_Tamago(speciesForMoves)
        if pool nonempty:
            w = pool[Index(pool.count)]
            if w==0:
                mastered.append(Human_Punch); equip.append(Human_Punch); return
            mastered.append(w); equip.append(w); appended=1
        # Empty rare pool logs a diagnostic, then continues ordinary processing.
    sort map descending by learn level; rebuild map hashes
    for (w,learnLevel) in map:
        if learnLevel <= requestedLevel:
            equip.append(w); appended += 1
            if not collectAll and appended > 2: break
```

These are direct appends: the helper neither clears the outputs nor deduplicates
them. Its cap counts entries appended by this call, including a valid rare draw,
rather than distinct identities or the destination's preexisting length. Ordinary
natural moves go only into equipment here; rare extras and fallbacks go into both
arrays. A rare extra can duplicate a natural equipped move.

Fresh executable enum records bind Human_Punch=1 (`0x147864fe0`, pointer
`0x147866cd0`) and Weapon_Use=0x16a (`0x147866670`, pointer `0x14786a7b8`).
The empty-map mask is `0x50081`; malformed nonempty maps take the separate
Human_Punch early return regardless of that mask.
Fresh helper `0x142e7db80`,
`ev_6ad47a2aa642624fa5c8256b520ccb6046b50ac723db71d53b162d814c675c39`,
returns character-row byte `+0x54`, or zero on lookup failure. Descriptor
`0x1473ee290` names that field Weapon; EPalWeaponType name/value table
`0x1477becc0` binds None=0, MeleeWeapon=7, Katana=16 and GiantClub=18.
This is a weapon test, separate from the rare-form helper's BattleBGM test.

The final sort `0x1432b87d0` selects the minimum to the tail in its small-partition
loop, producing descending order;
`ev_90031a4764efb0e3920a1afe3ab2372766392811b32e56879d17aedaa5dceef2`.
The initial ascending sort is `0x1432b8a20`,
`ev_effd455d49be095cb309b67d3eb850f6cce32a158f5ee2ecd75eda310074edb7`.
Their shown selection/swap paths aren't stable for equal levels. The fresh raw
learning rows have no duplicate `(PalId,Level)` groups; replacement merging could
still introduce ties. Exact tie replay through sort depth-fallback helpers remains
unverified, but is not required for overall set availability: AvailableWaza reads
mastered plus all level-eligible natural moves and does not read equipment.
Tamago inputs are bound below. The general initializer passes collect-all=true.
Its separate move-species resolver `0x1430ef5c0` uses the
[Unique NPC lookup](#unique-npc-key-lookup-and-saved-species), in addition to the
learning table's CharacterIDReplacer.

Fresh Tamago callback `0x14332d430`,
`ev_f6d3be21466fb48b2e9c2a3277f782bb0d65db7c5f239a2609e37fe1a857da96`,
and getter `0x143334600`,
`ev_c3d04e632bdc094102254a0505fefb00f9e42e68ff68bbd3f405d40d11722d9a`,
establish its separate pool:

```text
TamagoPool(species):
    result=[]
    for row in WazaMasterTamago_DataTable:
        if CharacterIDReplacer(row.PalId)==species:
            result.append(row.WazaID)  # no deduplication or level filter
    return result
```

Fresh descriptor `0x1478635a0` binds WazaMasterTamago_DataTable to database
`+0x88`. The fresh BP_PalWazaDatabase CDO references
`Pal/Content/Pal/DataTable/Waza/DT_WazaMasterTamago.0`. That extracted table has
7,111 rows across 283 PalIds, all prefixed BOSS_, no None WazaID, and 32 duplicate
`(PalId,WazaID)` pairs. For example BOSS_Umihebi has 13 rows / 12 distinct moves,
with SandTornado repeated. Duplicate rows are separate lottery entries here;
the pool is not a uniformly sampled set of distinct moves. These are shipped
input observations, not measured rare-spawn frequencies.

Capture invokes `0x14310b010`,
`ev_9779ca43cb7da60b48a77aefbe7ef702929da0b889d062b08326629276b87076`:
if equipment exceeds three, copy it, clear equipment and re-add the first three
through AddUnique. Fresh helper `0x142f6e740`,
`ev_94880390aeae76775d415e45f0d74770aebd04a6c68b16ff9f7ab94ef59a3577`,
returns an existing index on a duplicate, otherwise appends. A duplicate in that
prefix can therefore leave fewer than three equipped entries; there is no retry
from later entries. The hook doesn't clear the mastered array. NPC setup `0x1430f7260`,
`ev_b605f3a770cadac9b884651c3dec643bcb445b5b2ff31bf95b2531e0f699fcac`,
has availability-relevant behavior beyond simply reusing nonempty arrays; its
complete conditional model is below.

Level update `0x1430f78d0`,
`ev_c8c7cb4f4df7b86d27a700f02606cd29d1b420dbf938d6550575a99ba67f45fd`,
processes natural moves in `(oldLevel,newLevel]`, invokes the learn operation and
equips when a slot is free, or conditionally replaces the lowest-power move when
IsAutoEquipMasteredWaza is enabled. The complete 36-byte learn helper at
`0x142f73380` returns without a mastered-array write when its third argument is
nonzero; the derived-availability routine above independently supplies natural moves
through the current level. This explains the separate raw-storage and availability
boundaries without treating a function name as proof of a stored-array write.

The shipped inherited IsAutoEquipMasteredWaza default is true: constructor
`0x142f068cd` stores word 1 at settings `+0x164c` (same retained constructor
assembly cited above; fresh bytes `66 c7 87 4c 16 00 00 01 00`). Descriptor
`0x14747b4d0` names the property and points to setter `0x142925450`, which writes
byte `+0x164c`. The direct-parent Blueprint CDO and shipped ini inventory contain
no override. This is a base default, not a sampled live setting.

Dependencies for overall availability: explicit overrides, saved species/level,
replacement maps, natural/Tamago data, fallback and rare draws, NPC mastered writes,
installation/capture preservation and any battle-specific mastered overrides.
Equipment sort/trim is relevant only if it changes one of those inputs.

### Overall wild attack availability

The output is the overall available attack set at captured storage. Equipped
loadout is outside that output. The established
[AvailableWaza/HasMasteredWaza routines](#derived-learned-availability) give the
observable boundary. In set notation, retaining the original engine list model:

```text
Available(save) = unique(save.MasteredWaza union
    {w from GetMasterableWaza_Level(save.CharacterID) where learnLevel <= save.Level})
HasMasteredWaza(None) = false
# Equipment is not an input to these queries.
```

Thus nonempty explicit overrides suppress initial natural setup but do not
suppress derived natural availability. They supply the mastered input; natural
moves through the final saved species/level remain available. A rare/Tamago extra
is separately stored in mastered. Equipment-only capture trimming and equal-level
equipment order cannot remove those attacks. The final saved species/level still
matter: the Unique NPC resolver and the KingWhale capture replacement must not
be replaced by the original spawn species in this formula.

#### NPC-companion attack source

Native `0x142e711b0` names itself
`UPalDatabaseCharacterParameter::FindNPCOtomoWaza`. It initializes an empty output,
enumerates database `+0x150`, finds the first row whose FName at `+8` equals its
**input species**, and appends the three uint16 values at `+0x10/+0x12/+0x14`.
It does not deduplicate, filter None, or continue to a second matching row.
No match returns an empty array. This key is not the general initializer's
separate Unique NPC row-name parameter.

Native property descriptor `0x1473f6840` names `NPCOtomoWazaDataTable` and gives
`+0x150`. Struct params `0x148f059b0` name PalNPCOtomoWazaDataRow, size `0x18`;
property array `0x1473eb518` binds PalName `+8`, Waza1 `+0x10`, Waza2 `+0x12`,
Waza3 `+0x14` (with the three separate underlying-enum metadata entries).
The shipped GameInstance CDO selects BP_PalDatabaseCharacterParameter_C; that
CDO selects `Pal/Content/Pal/DataTable/Waza/DT_NPCOtomoWaza.0`. Its complete
**two-row** snapshot is:

| PalName.Key | Waza1 | Waza2 | Waza3 |
| --- | --- | --- | --- |
| ColorfulBird | SelfExplosion | HyperBeam | PowerShot |
| RobinHood_Ground | RockLance | SandTornado | Unique_RobinHood_BowSnipe |

Both keys are distinct and all six entries are non-None, so first-row order
cannot distinguish matching results in these shipped inputs. Wrapper
`0x1432e5ef0` passes its input species (`param_2`) to this helper, copies the
result to general initializer p9, supplies empty p10 and disables p13 form
selection. The matching species therefore supply a nonempty explicit three-entry
array; other species take the natural branch. The separate Unique NPC key still
controls later resolution as already documented. This establishes this API's
input contract, not the distribution of species/keys chosen by NPC spawners or
an ordinary-wild invocation of this API.

Decoded CDO SHA-256:
`3bdd5460cd07c66f3a23fd57f1cfd30e5c054c75839b886aa9d37f31c08b9517`;
table SHA-256:
`0a69edf9183fd1e8a23b73f7e89582190e5dd4b8f7aebc4b45fb2f60a4caa32d`.
They use the same base archive/mapping/decoder identities as this report.
Source helper: `ev_14b5f1aaf0e11a563204e864d3d18ae90f6cd165cf3510ad207170a9b27cae76`;
field/schema bytes: `ev_313546a5c2540fe598db96cb9b2eb229a80319fe76f7933d346fa79391244ad9`,
`ev_3f8fdff51fc5a7ec1c4821bb55a967a48bf4998996aa72e29103e6aecf14044e`.
The retained wrapper is
`ev_907710a2df1aac6228d5a37760ab4a7534eae0400ee58d298535148e0f7c8430`.

#### NPC master-setup and availability effects

NPC master setup `0x1430f7260` includes weapon normalization as well as array
setup. Its body is `ev_b605f3a770cadac9b884651c3dec643bcb445b5b2ff31bf95b2531e0f699fcac`.
Getter and writer checks separate equipment at live `+0x428` from mastered at
`+0x438`. The availability-relevant conditional model is:

```text
MasterWazaSetup(actor):
    if actor role byte != 3 or parameter is absent: return
    M = saved mastered; E = saved equipment
    if M is empty and E is empty:
        (newM,newE) = SetupAttacks(current saved species, saved level, policy, rare)
        for w in newM: Learn(w, false)              # add-unique to mastered
        for w in newE: Equip(w)                     # equipment only
    else if Weapon(current saved species) not in {None,MeleeWeapon,Katana,GiantClub}:
        if Weapon_Use absent from BOTH M and E: Learn(Weapon_Use, false)
        Equip(Weapon_Use)
        remove Human_Punch from equipment only
    else if Human_Punch is already in M or E:
        ensure its combat action exists             # not a mastered-array grant
```

Learn helper `0x142f73380` writes mastered only when argument three is zero;
`0x1430f73da..0x1430f73e2` explicitly clear R8D before the weapon grant. Equipment
removal `0x142f892a0` changes only `+0x428/+0x430`; it does not remove a stored
mastered Human_Punch. Getters `0x142f7aca0` and `0x142f7c7d0` copy those two
separate arrays. `0x142f7cce0` gets the current species' Weapon, and
`0x143305260` applies mask `0x50081` for the four excluded weapon categories.

The melee action path `0x142cd8a20` installs/loads combat action entries. Its
completion adapter `0x142cce7e0` has bytes `48 8b 12 e9 88 9f ff ff`, forwarding
to `0x142cc8770`, which inserts in the combat-cache object's `+0x3a0` map via
`0x142cc3db0`; the shown path does not grant a mastered attack. This is distinct
from the explicit `Learn(...,false)` above.

Native `0x1430e0080` and `0x1430e0810` register `0x1430f7260` with diagnostic
`&APalNPC::MasterWazaSetup` in the actor's `+0x8d0` initialization sequence,
using stage value 1. This binds the callback's owner and registration. It does
not by itself show every spawner's dispatch or every possible supplied-save state.

**Materiality check:** all **753** decoded rows of the shipped
DT_PalMonsterParameter have `Weapon=EPalWeaponType::None`. For a Pal drawn from
those rows with an existing nonempty attack array, this normalization does not
add Weapon_Use to mastered. Consequently the weapon branch is a meaningful
shared-helper branch, not evidence of a new attack available to an ordinary
Pal. If both arrays are empty, the explicit fallback model above remains usable;
no arbitrary supplied-save contents are presumed.

| Decisive observation | Evidence ID |
| --- | --- |
| Equipment getter | `ev_cd86b158243fe0cf7b7bfe0f975252662c833ec46896ba028771132aa3e51b21` |
| Mastered getter | `ev_631de23d7f100464a1e7a1ad9a9ebe4a259307c34103e7ed3505bb0bdb08d409` |
| Weapon getter / mask | `ev_eba77c2847b3db684ae90b0627e8b7e972ec3fa089c07c64114fbe72d5fd8dc1`, `ev_a7bc56d7440c223166b28145a01b68ba3d7889b3c5795db98bb4df2a1b17f5c5` |
| Equipment-only removal | `ev_b6b2859f688047fe1f3c3519af2e9309cf5b1d02f8f2b1721b36c5e261532e06` |
| Learn's mastered write | `ev_8b26c12386c29f1154aa4a212fb200eef61bfdadba0b733b3b57325d40dd98eb` |
| Setup argument/branch instructions | `ev_e5daec116105c0f95c7bc41a87a9bacc3c94d3876c30e32285b3fadd26b3c517` |
| Melee completion and action-map insertion | `ev_4cbd8bcd6e9977c265b85ed01e6a2085c12f5fd3f0497c096e6b6271cfed8ba8`, `ev_c298c8df8c44f202e0b44d46e3faeb3756ef79f58b2743d66a6887dfebdefc07` |
| Named callback registration | `ev_3621ae6cfde0b57ee5e91936b0a1174efde7e3c6eb05158d33c7293b68f65b38`, `ev_0e8d9190726872824f232c95476774b1046b99cbaa16a886b4a40d5c44a0d8b0` |

#### KingWhale battle actions and captured availability

The connected callback preserves the then-current mastered list while replacing
CharacterID with `BOSS_KingWhale_otomo`. Source and target natural tables match:
HomingBubble, CreepingBubble, AquaBlade, WaveTackle, Maelstrom, Breaching,
HolyBlast and AquaTornado at levels 1/7/15/22/30/40/50/60 (the six
species-specific names carry `Unique_KingWhale_`). The target's entire learning
set lacks these three combat-selection identities:

| Shipped module input | Selected ID (prefix `Unique_KingWhale_`) | Concrete action Blueprint |
| --- | --- | --- |
| PhaseWazaList[Phase3] | Breaching_P3 | BP_UniqueAction_KingWhale_Breaching_Phase3 |
| InterruptWazaMap[0.3] | TidalBore | BP_UniqueAction_KingWhale_TidalBore2 |
| InterruptWazaMap[0.1] | SuperTidalBore | BP_UniqueAction_KingWhale_SuperTidalBore |

The current module CDO SHA-256 is
`46337abd37d9153f922e546ea1d6204bcca83153c76087117acab8215233d801`.
Descriptors `1472cb5d0` / `1472cb6e0` bind PhaseWazaList (`+0xa8`) and
InterruptWazaMap (`+0xf8`). Its controller/capture hookup is established
[above](#special-capture-input-kingwhale).

```text
module setup -> preload actor combat-action cache from both lists
phase update -> AI slot/Waza selection maps
interrupt update -> pending action/ID; FindNextWaza consumes that selection
KingWhale combat Blueprint -> PlayAction / PlayActionLocation
PalActionComponent -> queue, then concrete action initialization/start
successful capture -> preserve mastered; replace species
available = mastered union final-species natural moves through saved level
```

The cache/AI entries are distinct from saved MasteredWaza. The checked producers,
audio-phase handlers and request/queue path do not grant mastered attacks; queue
preservation alone would not settle concrete action startup, whose result follows.

##### Concrete action initialization/start

All nonempty shipped `WazaActionDeclarationMap` candidates for each selected ID
agree on the concrete classes above, so the preservation conclusion does not
depend on an assumed map merge order. A missing action executes no grant.
Breaching_Phase3 is data-only, inherits BP_UniqueAction_KingWhale_Breaching,
and raises MaxAttackCount from 2 to 3. Its **internal WazaID is ordinary
Unique_KingWhale_Breaching**, distinct from selector ID Breaching_P3.
TidalBore2 and SuperTidalBore inherit BP_ActionUniqueAttackBase; Breaching also
reaches that base, whose native parent is PalActionWazaBase.

The checked concrete/ancestor Blueprint scripts and actual native startup
preserve **MasteredWaza, saved CharacterID and saved Level** on this route.
Their movement, audio, collision, projectile, action and immediate resource work
does not write those availability inputs. Common startup's work-speed callback
updates mesh modifiers; its parameter/resource setters write separate status/
resource fields. This conclusion follows the selected implementations and immediate
writers, rather than assuming that any combat-oriented method is harmless.

Useful revisit landmarks:

| Stage | Named class/function or address |
| --- | --- |
| Setup, phase and interrupt selection | `142d30810`, `142d1b360`, `142d36050`; FindNextWaza `142d1ef60` |
| Blueprint action consumer | `BP_AIAction_CombatPal_KingWhale.ChangeNextAction` / `PlayWazaAction`; its SetupSkillSlot override is empty |
| Native request and queue | `PalActionComponent.PlayAction` / `PlayActionLocation` -> `142bd0170` -> `142bd02e0` |
| Actual queued start | Component tick `142bd9000` -> common start `142ccc100` |
| Concrete native lifecycle | `PalActionWazaBase`: OnQueue/Tick return; Begin `142cd9960`, End `142cdc640` |
| Immediate startup writes | Common setup `142cce100`, work-speed callback `142cdb860` / mesh setter `142ce2b20`, resource path `142f77350` -> `142f8bde0` |

Decisive retained start observations are
`ev_bbb8ae2048e002d57eabff8bdf5b091572477674432acc174e8b9a194215be6b`
(actual tick), `ev_18d3d1f163b974d293f72a32d16e008d382afbe683c70196eeba15c030696518`
(native Begin), and `ev_839e02e61f7d6b1630584354ff394a9f813e2b4678116d536c0d6f8f615b1402`
(common start). These refer to the report's identified shipped executable/assets.

For an input with none of the three IDs mastered, this battle-action/capture
route supplies none after capture: startup does not grant them and the target
natural set lacks them. An independently supplied mastered entry remains
available. This is not global unobtainability, exact encounter timing, a live
frequency measurement, or preservation under arbitrary mods/later grants.

## Wild-Pal gender

**Verified for declared inputs, updated 2026-10-24.** The ratio helper, general initializer, supplied-save installation, actor association and ordinary/KingWhale storage path establish the saved-gender output. See the [complete transfer argument](#saved-wild-gender-from-spawn-input-through-captured-storage). Initializer gender uses the species after initial form selection, not necessarily the later Unique NPC CharacterID. A supplied/overridden save contributes its own saved Gender. The detailed caller and field observations below remain useful for the other wild mechanics.

### Saved wild gender from spawn input through captured storage

For a species/form passed to initialization, or a supplied nonplayer save including
its saved-gender override, the following model establishes gender at successful
captured storage. It does not establish distributions over caller species/overrides.

```text
BuildWildSave(species, flags, optional suppliedSaveOrOverride):
    if using shared initialization:
        S = species after initial form selection
        save.Gender = Gender(S)             # shared helper above
        finish skills/common setup          # no gender rewrite
    else:
        save = supplied nonplayer save
    if caller supplied an explicit saved-gender override:
        save.Gender = supplied value
    return save

SpawnAndCapture(save):
    parameter = new PalIndividualCharacterParameter
    parameter.Save = copy(save)              # includes Gender and IsPlayer
    initialize nickname, work suitability and derived stats/health
    # nonplayer branch preserves Gender
    register parameter under individual identity I
    complete returned handle H with I
    create actor A, retaining I through either creation-dispatch branch
    bind A.ParameterComponent.IndividualHandle = handle(I)
    bind A.ParameterComponent.parameter = handle(I).GetParameter()
    # this is the installed parameter, not merely an object of the same class
    capture A through the ordinary or connected KingWhale path
    store the same individual handle; saved Gender is unchanged
    return stored Gender
```

Thus a shared-initializer save yields `Gender(S)`; a supplied/overridden saved value `G` yields
`G`. The latter is a byte-preservation rule, not a claim that every spawner samples the species
ratio. Later Unique NPC replacement can change saved CharacterID without rerolling gender; the
Standard spawner's None key makes that later resolution an identity. Initial form selection
still matters. A subsequent explicit change is a new input value, not another ratio draw
inferred from the final species.

#### Nonplayer installation preserves the gender input

Assignment `0x1428bf910(parameter+0x3d0, save)` copies Gender at save `+0x10` to live parameter
`+0x3e0`. The post-copy audit follows installer `0x142f81a70`, nickname initializer
`0x142f8c730`, and postprocessor `0x142e981a0`. Nickname updates at live `+0x400` are embedded
save `+0x30`, disjoint from gender. Stat setter `0x142f8c570` writes `+0x4b8/+0x4bc`, array
`+0x4c0`, and factors `+0x53c/+0x540`; health helpers `0x142f84e70`, `0x142f8bfe0`,
`0x142f8a240` write health/state/cache fields rather than `+0x3e0`.

The copied-save branch in `0x142e981a0` is **IsPlayer at +0xb0**, not a separate +0x110 save
flag: assembly copies to `[RSP+0x60]`, then tests `[RSP+0x110]`. Installer enters this
postprocessor only for live `+0x480 == 0`, the same saved IsPlayer field. Its player-only
`0x142e98310` branch is therefore not a missing nonplayer dependency. The newly constructed
parameter has no bound actor and empty name/health delegates during this initialization;
actor-dependent initialization is skipped at this point. This is positive field/control-flow
evidence for the generic supplied-save path, not an inference from constructor defaults about
later capture eligibility.

#### Completion identity reaches the actor used by capture

Successful creation preserves the individual identity through the returned handle
and either actor-creation dispatch route. Their common binder `142c78ca0` resolves
that identity, then writes the handle/parameter to the actual actor's parameter
component `+0x170/+0x178`. Capture resolves the same handle/individual, not merely
another object of the same class. Binding's actor back-reference and cache writes
are distinct from Gender; the ordinary and KingWhale storage paths preserve it.

Useful landmarks: `142c6da30` completion identity, `142e36d30` returned handle,
`143106180` / `143106790` actor dispatch, `143108210` common queue,
`142c6d530` -> `142c78ca0` binder. Retained binder-store observation:
`ev_907a4fb85a0612678da2fd4bf0a32c469317b0b39005869469bbdb099a44c21c`.

ParamSetup (`142b84150`), NPCOtomo (`1432e5ef0`) and ArenaSoloNPC (`142d45b60`)
make no later gender write in their recovered wrappers. An upstream saved-gender
change supplies the explicit override input; its frequency is not needed for
the conditional transfer rule. This does not claim every spawner uses these APIs,
or cover player creation, mods or unobserved runtime distributions.

### Wild form selection and saved-level handoff

The form-selection producer derives resolved species and saved level from the
requested spawn species/level and supplied world state. Its accessors, named
settings/defaults, alternate replacement and position gate are modeled below.

Inputs are spawn species, requested level, form-enable flag, caller rank/arrays,
Unique NPC key, current character/learning tables, effective game/option settings,
and the actual spawner position and world-stage registry/query keys. The last
group is consumed by the recovered Predator gate; it is explicit supplied world
state, without a claimed distribution over every spawner.
Outputs are assignment species, resulting rare flag, saved species/level and the
mastered inputs reaching the established overall-availability calculation.

#### Settings and current shipped inputs

`1432e5010` returns GameInstance `+0x498`, falling back to the native settings CDO
through `142925d90` when absent. This is the same settings source used by the
initializer's direct accesses. `1432ec360` gets `PalOptionSubsystem` through
`1432ec2b0`, then returns its option record at `+0x370` through `1411546a0`.
Without that subsystem it uses a static record constructed by `142922ed0`.
This is option data, distinct from an AWorldSettings actor.

| Named input | Native byte offset | Native default | Current BP/option input |
| --- | ---: | --- | --- |
| RarePal_AppearanceProbability | e0 | float32 1 | BP float32 0.1; percent threshold input |
| PredatorPal_AppearanceProbability | e4 | float32 0.5 | BP float32 4; percent threshold input |
| RarePal_LevelAdd | e8 | int32 5 | Not serialized in BP; inherits 5 |
| CharacterMaxLevel | 28 | int32 50 | BP int32 80 |
| RarePalSettings | 118 | Array | BP ordered ranges below |
| EnablePredatorBossPal (option record) | 188 | true | Actual loaded option value, or true fallback |

The scalar property descriptors are `147488530`, `147488570`, `1474885b0`
and `147487d40`. `RarePalSettings` outer descriptor `147488890` binds `+118`;
inner descriptor `147488840` selects struct getter `142925670`.
`PalRarePalSetting` StructParams `148f1b6c8` names a size-`0x18`, alignment-4
element with three descriptors: `LevelRange` at0 (`147486890`), `Rank` at10
(`1474868e0`) and `AwakeningProbability` at14 (`147486930`).
`EnablePredatorBossPal` descriptor `147676740` names string `147677880` and
setter `141524a60`; bytes `c6818801000001c3` write byte `+188` to1. Its fallback
constructor explicitly writes the same byte to1.

The checked BP class directly derives native `PalGameSetting`; it has no
intermediate BP override. Constructor `142f04880` uses a qword pointer:
`*(uint32*)(param_1+0x1d)=5` is **byte e8**, not byte1d. It sets bytee0 to
`0x3f800000`, bytee4 to `0x3f000000` and byte28 to50. The decoded BP overrides
the fields stated above. Actual modified effective settings remain supplied
inputs; this audit does not inspect player configuration.

| BP LevelRange, tested before the max-level cap | Rank | AwakeningProbability |
| --- | ---: | ---: |
| Inclusive 1 through inclusive 25 | 2 | 0 |
| Inclusive 26 through inclusive 50 | 2 | 5 |
| Inclusive 51 through open upper bound | 4 | 10 |

The initializer takes the first matching range. Assembly at `1432e6464` reads
Rank from element `+10`; at `1432e646e` it loads float `+14` into XMM0 before
calling `143309c60`. That helper returns false for nonpositive percentages,
true for at least100, and otherwise performs its retained scalar comparison.
It is not the raw rare/Predator threshold branch.

#### Full form-to-save model

For valid context/database and successful save initialization, retaining native
int32/float32 arithmetic and normal game-level inputs:

```text
requested = requestedLevel
level = requested
S = inputSpecies
rare = false
inputPal = character(inputSpecies).IsPal       # helper returns true if row missing
inputBoss = character(inputSpecies).IsBoss     # helper returns false if row missing
settings = GameSettings(context)

if enableForms and inputPal and not inputBoss:
    if Scalar15() <= f32(settings.RarePal_AppearanceProbability * f32(0.01)):
        replacement = FirstRareRowForTribe(inputSpecies)
        if replacement != None:
            S = replacement
            rare = true
            output.IsRarePal = true           # saved byte50

if rare:
    level = requested + settings.RarePal_LevelAdd
    if level == requested: level += 1
    first matching settings.RarePalSettings range at this level:
        rank = range.Rank
        if PercentageTest(range.AwakeningProbability): output.byte51 = true
    if no range matched: rank = callerRank
else:
    rank = callerRank

if not rare and enableForms and inputPal and not inputBoss:
    if PredatorPositionAllowed(context) and Options(context).EnablePredatorBossPal:
        if Scalar15() <= f32(settings.PredatorPal_AppearanceProbability * f32(0.01)):
            key = FName("PREDATOR_" + String(inputSpecies))
            row = character(key)
            if row exists and row.BattleBGM == Predator: S = key
            # No Tribe/IsBoss filter here; this does NOT set rare or add five levels.

level = min(level, settings.CharacterMaxLevel)
output.Gender = Gender(S)
initialize passives from assignment species S, caller list, rare and fill mode
output.rank = rank
if explicit attacks nonempty:
    copy them into mastered and equipped arrays
else:
    moveSpecies = Resolve(S, UniqueNPCKey) when resolver/key applies, else S
    SetupAttacks(moveSpecies, requested, collectAll=true, rare, mastered, equipped)
savedSpecies = Resolve(S, UniqueNPCKey) when resolver applies, else S
CommonInitialize(savedSpecies, level, output)
    # valid character row: saved CharacterID=savedSpecies; saved level=low_byte(level)
available = unique(mastered union NaturalMoves(savedSpecies, savedLevel))
```

`Scalar15` is the native `(rand() & 0x7fff)` scaled by the float32 at
`1467e9800`; percent conversion loads the float32 at `14674dd2c`.
Both raw form branches use `<=`. A configured zero therefore still accepts
the zero ticket; it must not be described as disabling the branch. No nominal
percentage above is asserted as a measured spawn frequency, independent draw
probability or exact RNG replay. The separate awakening helper's nonpositive
short circuit remains explicit.

`FirstRareRowForTribe` is the existing `142e71f20`: first row matching input
Tribe, IsBoss true, tower/raid false and BattleBGM not Predator, otherwise None.
It is not string-prefix replacement. If a rare threshold passes but no replacement
exists, `rare` stays false and the Predator branch can still be attempted.
Predator helper `142e71d50` instead explicitly constructs the prefixed original
FName and checks only row existence/BattleBGM. The
[shipped character-table order](#shipped-species-table-binding-and-order) and
data remain relevant to the first-match rare lookup. For example, the current
PlantSlime Tribe has `BOSS_PlantSlime` and `BOSS_PlantSlime_Flower`; do not assume
a rare replacement preserves a variant suffix. The checked Pal table has48
`PREDATOR_` rows passing the music test; PinkCat has no such row.

The original requested level is preserved as `iStackX_20` and explicitly passed
to `1432ed8a0`. Modified level is passed to `142e73be0`; the latter writes its low
byte to saved `+20` after finding a valid row. With the shipped inputs, a successful
rare replacement of requested21 uses level26 for the range (rank2, awakening5)
and save, while attack setup receives21. Requested78 becomes83 for range choice
(rank4, awakening10), then saves80. Overall natural availability uses the saved
species/level, so the equipment/attack formula requires this producer bridge.
An invalid/missing resolved row does not gain a valid-save guarantee merely from
the common initializer's nonzero return; the existing row-guard boundary remains.

#### Predator position gate and supplied stage keys

`PredatorPositionAllowed` initially returns true. If context is a valid
`PalNPCSpawnerBase` with a stage-world subsystem, it instead tests whether
`143264290(subsystem, position)` returns null. Position is the root component's
three doubles at `+260`, or the zero-vector constant at `148fdd680` if absent.
`1432651d0` and `142f01510` forward the returned subsystem in RAX by tail jumps;
their decompiler `void` labels do not discard it. Class getter `142b06f60`
names `PalStageWorldSubsystem`.

```text
StageAtPosition(stageSubsystem, position):     # 143264290
    if required world/collection interfaces invalid: return null
    for occupied bounding record in ascending slot order:
        skip invalid bounding-record object
        if not (min.x < x < max.x and min.y < y < max.y and min.z < z < max.z):
            continue
        key = 0
        for eligible resolved world object in collection order:
            token = ActorLookupToken(record.object)       # 14367fcf0
            key = object.map_at_2e0.find(token).value or 0 # 1458c6560
            if key != 0: break
        for occupied registered stage record in ascending slot order:
            if record valid and record.virtual_360() == key: return record
        return null                         # do NOT retry later overlapping boxes
    return null
```

`142d40190` writes that map result through the output pointer and tells iterator
`1458c53a0` to stop on a nonzero key. With its caller's third argument0, the
iterator admits valid resolved weak objects when helper `144f0fda0` or the
`1436811b0(...,1458ab5b0())` result is absent, or the two results' qwords at`+18`
are equal. This is the recovered filter, not an assumption that every world
object is eligible. `143266ea0` performs the final registered-record equality;
there is no special rejection of key0 before that lookup.

The records are stage models, not generic actors. `142b05130` names
`PalStageModelBase`; its constructor `142b08e60` installs table `147778a90`.
The model does not label virtual`+360` as AActor.GetLevel or infer all concrete
subclass returns from the base table. Actual registered matching keys, maps,
position and loaded options remain supplied world state. Their query interface,
validity, filtering, order and first-box early return are established; no
population/lifecycle census is needed to evaluate this conditional transformation.

#### Decisive native observations and data identity

| Observation | Evidence ID |
| --- | --- |
| Game settings accessor,1432e5010 | `ev_a6e9f392edf44e6c65dc509c05021d3512174146dfead8671a43f07aa559b7a2` |
| Input IsPal predicate,142e796f0 (retained) | `ev_b5b2f67b2b02e819845540bdb250bd21f7098926f480f43b696ec5e59e9a9835` |
| Input IsBoss predicate,142e79680 (retained) | `ev_964175a4a7eb155f643df5c2622f267cb3575b5cd748cd28fc093e6f2ee8d9e7` |
| Complete RarePal_LevelAdd name | `ev_505a99d64ea38abeb7d13b98809d473a9b915d490d098ab19bf40f5dc3a0ce34` |
| Option accessor,1432ec360 | `ev_eeb3104f18cbfddb475fcd48d62322eaa6da20af87c2623458d0616229ee41f0` |
| Option subsystem lookup,1432ec2b0 | `ev_05a83287a358d7fd4314b467d16350a8e1b87b0355c180a80b6a4ba9bf5b3e6d` |
| Option record +370,1411546a0 | `ev_3dde0c631029bb0e32782fd58c15545428b77d43b4446dcdff7c9e689d524d21` |
| Native option fallback constructor | `ev_11a495b7e29f8d3171b9beb838194093b8638bac5a85fb62cb86233cab2051ec` |
| Predator replacement,142e71d50 | `ev_e4c05f96cc3fd4741e2781d6560a5843f5b2977d59a2f70f34e57b1a68fc91fa` |
| Native game-setting constructor | `ev_96bcee25b8abb214acab9b42531956c678207335aede6b46daa0492f0694d449` |
| Rate/level descriptors | `ev_5730bc7ab05e427cd430c85842e0622cd8ea9692c7c9aaefcc2cf97bae1b448a` |
| Rate/level names | `ev_ef8af1de0a947ac89f80d41630c669dbe231e32c849bca8aa17ac6b973d63439` |
| Level cap descriptor | `ev_549f5ca6f6ada060f4d7900d7c6360a3d082fe69eff24ad202a886a7c0cf0c1d` |
| Level cap name | `ev_2c84c03d0e03c57a8de63170d28eb8f711dda1610ff074b62248e8952b8f25f6` |
| Rare range array descriptor | `ev_83a09ac1e188182badd672dfcdf617adf0fe2c19e708fda6660e77687677babc` |
| Rare range element descriptors | `ev_101bb51efe7d665e7bdac2a2fd2ce6e4a5c3a0bfd764e8b6a7c357092ff237a5` |
| Rank property name | `ev_995fe82b624b1129a2da8941215de964f8cc723693bdb5decab486eb77c06f43` |
| Range struct registration | `ev_cab19c9fce6c72c3c88e4e5d99ca665cb13cbaac63263b0e9debca6bdacdd6fc` |
| Range struct name | `ev_e5bcf16674861925e74eb6dcb47f8d8dee48f7c6a01fcb66ea170d5620eea850` |
| Range struct getter | `ev_593fdd306221d141913223e3ccae4687fa19a9a790e0f2bda6322dc267ded949` |
| Predator option descriptor | `ev_084beb0d8e6b55dc7ca4c0afbfd1aef6a505001824be544742230910162538c9` |
| Predator option name | `ev_a4dbaee2cf1011ad90f8c170bd6d7aff75b151dec36a88a51fbb3d6de85d3043` |
| Predator option setter | `ev_91c2e80eab3f384bc943c595bb5a6afbce3e2012bc2a85a1e0b6405956ccedb3` |
| Initializer argument/range assembly | `ev_48067d46f2236e36c9da4ca892a7aa75748e830b7e72c1d158e73d8d16801707` |
| Form scalar constant | `ev_366b6039b3f2f1a047adc7a0c0e92307b7f136e2c8305f6e4bb1b5d7106f4a18` |
| Form percent factor | `ev_69e2ed3fe40ffb2b5c632487329428357039a57e0338bb7d877a616cff57f76d` |
| Spawner class identity | `ev_a4d05ca6757d71caa7048e7749dfa91544a299f7248216be892a2d3f8e200b5d` |
| Stage subsystem relay assembly | `ev_374da2bcc072ec425a9c2ae50b2409887730dad6c5f51c0a96549ef416ed3d55` |
| Stage subsystem getter assembly | `ev_1e9f675207c7e8d370d08171895e51ebf6d7d4e07c72e3177def8411be372b56` |
| Stage subsystem class | `ev_ade40088095a5c432e9a27e544cbf18115ec4885cc148d425304319001e2b990` |
| Strict-bound stage query,143264290 | `ev_7fa4715b74e0df185807c420fde1197791cbd4b217746a09630afe402d1529f4` |
| Key callback,142d40190 | `ev_a56792ee8cb19f9392fe92bfba68c56b98178f471040027dee397f66e0ae8729` |
| World-object iterator,1458c53a0 | `ev_7489ccc88c726cc60208793acc4fdeb6a3b41ded283eb24b1455072b722a8717` |
| Actor-token map lookup,1458c6560 | `ev_c6f0b8a354e1eb4863199e410ab03cbc2d6c5146b84b655430910e803c57a537` |
| Registered stage match,143266ea0 | `ev_7c53753b3ca28607f1ea0f8d2445a1c8cf73c6e5559ea39f53d2ccfc93b2c35f` |
| Absent-root position constant | `ev_23a4180aa6efda89adf406ae6502ea27ee632b69ed8c66f43313a5e8db4067d5` |
| Stage-model class identity | `ev_3853b46043d449dff82c60923df9e22a42bb60c90388dac568fa1d7137b3c366` |
| Stage-model base constructor | `ev_507631b36e49d017db895d420615268838d47abdc3703c1ae3ba1b2c95373528` |

All records bind the same executable SHA-256 and reduced analysis profile in
the identity section. The retained decoded BP CDO hash is
`3048902dcf178ad85c514c5dcc70373e8007d1c5e0b03da9f394dc2d7758ac8f`;
BP class hash `9d18d32a7bff1a3972242feab7146ba8cf04166c536b9fb282daa8832654bb82`
establishes its native superclass. The Pal character-table export hash is
`b8fce52f84af279b5494a6de87ea6966aa412a5b00e555e0730a3b3ae5ad8599`.
`BP_PalSpawner_Standard` class hash
`4bdd73408385cf71ad2b9fbf42089d09f351a2a85f529b4951dec762a2a202b9`
establishes its `PalNPCSpawnerBase` superclass. These use the recorded archive,
decoder and mappings; no mod/player configuration or fresh archive extraction
was substituted. Native reads were checked against current source-file mappings.

### Wild input contracts: retained wrapper and sequencing boundaries

The wrappers below retain distinct supplied-input contracts. Form selection,
rare-pool identity and saved transfer are established in their respective sections;
capture target/source conditions remain explicit.

General initializer `0x1432e6070`,
`ev_06a0c4c0404e03ccfee8f18e7230feeda2c6ff9339e82d3b9b14c95cd2bd31c1`,
initializes a fresh output save. Its relevant argument positions, numbered from
the decompiled signature, are `p7=output`, `p8=skip random passive filling`,
`p9=explicit attacks`, `p10=caller passives`, and `p13=enable form-selection
branches`. These describe observed consumers, not recovered original field names.
Some wrappers show surplus inferred arguments, so those trailing expressions
must not be treated as a reliable ABI declaration.

```text
S = species after the initial form-selection branches
output.Gender = Gender(S)
apply character passive defaults for S; append caller passives under cap
possibly append rare passive; optionally perform random filling
if explicit attacks nonempty:
    copy them into both mastered and equipped arrays
else:
    moveSpecies = S
    if resolver exists and caller parameter p3 is nonempty:
        moveSpecies = Resolve(S,p3)                # 1430ef5c0
    SetupAttacks(moveSpecies, original requestedLevel, collectAll=true, rareFlag)
if resolver exists:
    S = Resolve(S,p3)                              # later call, outside attack branch
continue talent/common initialization using S
```

Decisive sequencing in the retained body is the gender store to `output+0x10`,
then `142e7bab0(...,S,output+0x90)`, later `1432ed8a0(...)` or the two explicit
array copies, then a second `1430ef5c0(...,S,p3)` before `142e73be0(...,S,...)`.
The attack setup receives the saved original requested-level variable, while
earlier form handling can adjust the level passed to common initialization.
This is a contract distinction, not proof of a reachable visible discrepancy.

#### Unique NPC key lookup and saved species

The
nonempty resolver key is a Unique NPC row-name input, not a generic species
randomizer ticket. Native `0x1430ee060` names itself
`UPalNPCManager::FindRowUniqueNPCDT`. It reads the manager's **UniqueNPCDataTable**
field at `+0x1b0`, checks its `RowStruct` against **PalUniqueNPCDatabaseRow**,
and searches the table's row map using the full FName key. Empty/missing keys
return no row. The property descriptor at `0x14763dc00` names
UniqueNPCDataTable (`0x14763dea0`) and records offset `0x1b0`.

StaticStruct getter `0x142a3d7e0` names PalUniqueNPCDatabaseRow; registration
`0x142a40ff0` reaches StructParams `0x148f66650` and property array
`0x14764d2e0`. Its CharacterID property has offset **8**. This binds the row member
consumed by `0x1430ef5c0`; the struct check is not a UDataTable-class check.
The shipped GameInstance CDO selects `NPCManagerClass=BP_PalNPCManager_C`, whose
CDO selects `UniqueNPCDataTable=DT_UniqueNPC`. That composite table uses
PalUniqueNPCDatabaseRow and parent `DT_UniqueNPC_Common`, with 216 exported rows.
For example, both row keys `Johnson` and `Bobby` have CharacterID `MobuCitizen`.
Those are decoded supplied-key examples, not evidence that an ordinary wild spawn
selected either row.

```text
FindUniqueNPCRow(manager, key):                         # 1430ee060
    if key == None: return null
    table = manager.UniqueNPCDataTable
    require table.RowStruct is compatible with PalUniqueNPCDatabaseRow
    return table.RowMap.find(full_FName=key) or null

Resolve(manager, inputSpecies, key):                   # 1430ef5c0
    if key == None: return inputSpecies
    row = FindUniqueNPCRow(manager,key)
    return row.CharacterID if row exists else None
```

A nonempty key ignores the input species; a missing row does **not** fall back to
it. Companion helper `0x1430ef610` returns the same row CharacterID or None without
this identity-on-empty branch; it is not an attack-array lookup.

At the fresh-save output boundary, general initializer
`0x1432e6070` assigns gender and character-default passives using the earlier
assignment species **S**. With no explicit attacks, its nonempty-key path supplies
resolved species **R** to natural attack setup. Explicit arrays instead populate
mastered/equipped attacks directly. Its later resolver then supplies **R** to
common initializer `0x142e73be0`. When R has a valid character row, that routine
writes **R to saved CharacterID at output+0**. It does not rewrite saved gender
`+0x10`, default/caller passives `+0x90`, or the two attack arrays in its recovered
body. Existing capture/storage transformations retain their separately stated
conditions; this is the fresh-save boundary, not proof of every spawn mode.

Decisive common-initializer assembly preserves the incoming species in RBX,
checks its row lookup, then writes it into the same output save:

```text
142e73c07 MOV RBX,RDX                  # incoming resolved species
142e73c15 CALL 142e721f0               # incoming RCX/RDX still supply database/species
142e73c20 TEST RAX,RAX
142e73c23 JZ 142e73c8b                # no species write when the row is absent
142e73c7d MOV qword ptr [RDI],RBX     # saved CharacterID = resolved species
```

The decompiler omitted the first lookup's arguments; assembly establishes them.
That helper's eventual `AL=1` return alone does not prove the row/write succeeded.
No valid replacement guarantee is inferred for an invalid or missing Unique NPC
row. For valid rows the supported model is **gender/defaults from S; natural
attacks and saved CharacterID from R**, or caller-supplied attacks. If the key is
None, the resolver preserves S. A concrete gameplay discrepancy needs an actual
caller supplying differing S/R and distinguishing gender/passive/attack data;
none is asserted solely from this ordering.

The standard-spawner path below supplies a None Unique NPC key; other modes
retain their own keys/flags. The [saved-gender transfer](#saved-wild-gender-from-spawn-input-through-captured-storage)
uses the assignment-stage gender, while the form and rare-pool sections establish
the separate species/level and rare-input contracts. No scheduling or population
distribution is inferred from these caller inputs and saved contents.

| Decisive observation | Evidence ID |
| --- | --- |
| Named Unique NPC row lookup, `0x1430ee060` | `ev_8903e79cbbfa30267089cce7e9a2dcd43cd624522b37e5cf47c67f64d90656ca` |
| Identity-on-empty resolver, `0x1430ef5c0` | `ev_9b123f0576fff750b7862f0563b8a54ae750210f1d9745fd5392a12b9158aa98` |
| PalUniqueNPCDatabaseRow getter, `0x142a3d7e0` | `ev_ba4579871df40368892fbf6b96d186faa4476ec0b75ae7241c4476920b9d29f2` |
| Row registration, `0x142a40ff0` | `ev_49bc4674552f0a5be4cbbbf8a59ebd55523354bb20a895bc613010832fb4843a` |
| Row CharacterID helper, `0x1430ef610` | `ev_c63969e2fc00b5faac63bc602b7361616901a86b5bb9e99ce1db85421377d05a` |
| Common save initialization, `0x142e73be0` | `ev_f5ae3c7210283fd1440355dd20cb3ba41096f62299c9a6b007407eb49eecaf9e` |
| Common initialization assembly | `ev_7192d58b5e9d05426cb4f9fac7243421cff9e37813daa4fc535c298d831c7e4a` |

Inputs use the recorded base archive, mappings and decoder identities:

| Decoded input | SHA-256 |
| --- | --- |
| `BP_PalGameInstance-Default__BP_PalGameInstance_C.json` | `78942b1698dacf4f621e4092f7e413ef38e3fdad8f81fb08b2a762449ea4a75e` |
| `BP_PalNPCManager-Default__BP_PalNPCManager_C.json` | `4848fa7384a45d579b37ec9c4caf49032e42e63497720bfc0811a20fd4682d30` |
| `DT_UniqueNPC-DT_UniqueNPC.json` | `2fa11af6670e527e2ac8754ecf33458643f6d3bd80e5e3a967ea981388b0dcb0` |
| `DT_UniqueNPC_Common-DT_UniqueNPC_Common.json` | `80e55d51d6dd84280d8ea783c8ae6068107a7f65455ee418a3e6406ecf81c2cd` |

#### Standard wild spawner inputs and initializer forwarding

The concrete shipped Pal-spawn path here is
`BP_PalSpawner_Standard.Spawn(SpawnGroup)`. This is a scoped standard-spawner
contract, not proof that every wild mode uses this Blueprint or that its external
SpawnGroup has a particular species. Its CDO selects
`BP_MonsterAIController_Wild_C`, sets `RarePalAble=true` and has an empty default
PalList; the invoked SpawnGroup supplies the actual entries.

The decoded Spawn script selects `SpawnGroup.PalList[i].PalID.Key` into
`tempCharaID` and gets a level from the same selected record through
`GetSpawnLevelRandom_OneTribe`. Its two fresh-save calls are:

| Branch | Unique NPC key | Explicit attacks | Caller passives | Skip random filling | Form-selection input |
| --- | --- | --- | --- | --- | --- |
| General initializer | literal None | empty | empty | false | instance RarePalAble, CDO default true |
| World Tree aura passive-list initializer | literal None | empty | GetRandomWorldTreePassiveSkill output | false | literal false |

The World Tree aura branch and its computed array are explicit inputs, not an
assumption that the returned passive list is empty, nonempty or has fixed weights.
That producer's current ordered pool and exact selection rule are established in
[the World Tree input evidence](#world-tree-standard-spawner-passive-input); branch activation frequency remains unclaimed.
Instance overrides of RarePalAble are not inferred from the CDO.

In decoded expression 55, the passive-list helper receives Self, tempCharaID,
Name None, selected-record level, its Guid argument, output save, false, computed
passive list, false. Expression 118 supplies the general helper with the same
species/key/level roles, its Guid argument, output save, false and RarePalAble.
Expressions 56/119 assign the corresponding output to **InitPalIndividualParam**;
expression 76 passes that same local to **PalCharacterManager.SpawnNewCharacter**.
These are parsed expression indices, not claims about instruction offsets.

Native name/exec pairs close the helper binding:

| Pair | Native registered name | Exec / callee |
| --- | --- | --- |
| `0x147854ea0` | GetInitializedCharacterSaveParemter | `0x142b83c30` → `0x1432e5e30` → `0x1432e6070` |
| `0x147854ed0` | GetInitializedCharacterSaveParemter_PassiveSkillList | `0x142b846d0` → `0x1432e6e70` → `0x1432e6070` |

The general helper's decompiler text omits arguments. Its complete assembly
establishes the forwarding instead: incoming registers supply world/species/key/
level; common p5=-1, p6/p7 receive the Guid/output pointers, p8 receives the first
input boolean, p9/p10/p12 point to empty arrays, p11=1, p13 receives the final
input boolean, and p14=0. The passive-list wrapper forwards its supplied list
as common p10, empty attacks as p9, and the two booleans as p8/p13.

For a valid selected Pal record, the resulting scoped model is:

```text
requestedSpecies = selected PalID.Key
level = selected record's level draw
input = (requestedSpecies, NPC key=None, level, branch's caller passives/flags)
S = species after the already recovered initial form-selection logic
assign saved gender/default passives from S; append supplied passives and fill
set natural attacks using S                         # no explicit attacks here
UniqueNPC resolver preserves S                     # key is None
common initialization saves CharacterID=S for a valid character row
InitPalIndividualParam = resulting save
SpawnNewCharacter(InitPalIndividualParam, network parameters, callback)
```

Thus the later Unique NPC replacement does not split the assignment and saved species on
this standard path. This does not assert that initial form selection leaves the
requested species unchanged, or establish unconditional passive probabilities. The World
Tree caller array and manager order are bound. Other spawn modes retain their declared
caller inputs. The wild-passive, attack and gender sections establish their recorded
scopes.

| Observation | Identity |
| --- | --- |
| General helper forwarding assembly | `ev_1ec1ac4b174894d6386500512bc5323471bd12c194b75a7cc427fa1705734ac9` |
| General helper direct callers, including registered exec | `ev_d50ec19f4c27307b55c8354a9ea65b43d7bb3f839cb4e817fed99410e706730a` |
| Passive-list exec | `ev_953037ef073af4fc956c3e2486347609f741b6014ad4f959b02be8a6163c7961` |
| Passive-list native wrapper | `ev_096b4cfea4cfb3423b416637670e86beeecc077cf189b4931c1c534ef36e7ffb` |
| `BP_PalSpawner_Standard-BP_PalSpawner_Standard_C.json` | SHA-256 `4bdd73408385cf71ad2b9fbf42089d09f351a2a85f529b4951dec762a2a202b9` |
| `BP_PalSpawner_Standard-Default__BP_PalSpawner_Standard_C.json` | SHA-256 `0fe57634abde566fef3e772399389d69143ba5fbbd009edb2316f62211946670` |
| `BP_PalSpawner_Standard-Spawn-script.json` | SHA-256 `981374f9d209ad2497d74bd8450f3d4183bf866853f5c81b80cbb74fba66a74e` |

#### Fresh save installation into individual parameters

The standard spawner's save-to-native handoff reaches a constructed
**PalIndividualCharacterParameter**, rather than ending at a Blueprint local:

```text
BP_PalSpawner_Standard.Spawn -> PalCharacterManager.SpawnNewCharacter
  native exec 1428a7850:
    copy reflected save through 1428a3090
    call 142e53e20 -> 142e54050 with the save and network/callback parameters
  manager/common path, under its valid-state and spawn guards:
    construct a PalIndividualCharacterHandle
    register completion callbacks
    142c6af00: generate storage identity; retain callbacks; copy save
    142c6aac0: require no conflicting stored identity
      construct PalIndividualCharacterParameter through class 14295bce0
      142f81a70(parameter, save)
        1428bf910(parameter+3d0, save)       # actual embedded save assignment
        perform additional parameter initialization
      142e37dc0: copy supplied record fields and submit parameter to manager map
      notify storage completion through 142c6b3a0
```

The actual native function table is `0x14739c3a0`, registered by `0x1428a56a0`
on class `0x1428a2b60`, which names PalCharacterManager. Its SpawnNewCharacter
pair is **`0x14739c470`: name `0x147395d60` → exec `0x1428a7850`**.
By contrast, `0x1428a2730` is the UFunction getter for FunctionParams
`0x148eecee0`; it is not the native exec. This distinction is verified from the
bodies, not assumed from a name/pointer pair.

Copy constructor `0x1428a3090` and assignment `0x1428bf910` preserve the scoped
save contents, including CharacterID, gender, attack arrays and passive names.
They copy IsPlayer at save `+0xb0`, the owner-history count/entries from array
`+0xd0`, and both qwords of Dynamic.GroupId at `+0x328/+0x330`. Therefore those
input values reach the **embedded assignment boundary**. Assignment occurs at
live-object `+0x3d0`, the same parameter layout established for the capture
lookup. The manager helper writes supplied record fields around live-object `+0x320/+0x328`;
those lie outside the save and are not confused with Dynamic.GroupId at `+0x6f8`.

**Transfer boundary:** the [saved-gender model](#saved-wild-gender-from-spawn-input-through-captured-storage)
connects the completion callbacks to the installed parameter and actor, and
checks gender-specific post-copy preservation. This does not establish the full
query-time capture-addition state: live trainer references and group/history
changes before the gate remain conditions. A healthy returned handle or a copied
constructor default alone still does not prove capture eligibility.

| Decisive observation | Evidence ID |
| --- | --- |
| PalCharacterManager class | `ev_ee816ea9514ec438c15767ea9abba8eab90cedfc97242732ae7a6fe78a30d46c` |
| Native method registration | `ev_16c55a9344c4f8b30f4d7018f59f70ce9a7790d4dcee65e4ef56c07fdc0426cc` |
| SpawnNewCharacter real exec | `ev_4014379d528150b85731e7895e998816f20fef9ee03c8dcba1cddbac394f0905` |
| Save copy constructor | `ev_a1bd2f218e3b027dd784b5b405749170dbf94fad35babae5320df7463d387501` |
| Manager spawn forwarding | `ev_1114315a7d8bde59e1f41da3e00c35a21c8a01eb2846881b237b507f77e7bf9a` |
| Forwarding assembly, resolving omitted decompiler arguments | `ev_c1dcbfeabddad9aa1bc4489f52dc8109ff22ee6eddd17740f4c662b7fe49a9a2` |
| Common manager spawn path | `ev_7f201ec52b29fb3a3eb71f62e2fcd14b2420e8783f0774a476c68a5f227a9ed9` |
| Storage creation / callback retention | `ev_c4a1590fecfa32bc9d00be22da5abbdb5b6f34798fa6f98efda365ddc820fa3a` |
| Parameter construction / dispatch | `ev_a5effcb9b748bde92fdef1a676a07a4d2347e2d5c7cbb2b5e5d6f5c7ab99334b` |
| Embedded save installation | `ev_e43beee97ff8d1ef56aa81f992c91e8c13783b453c7fd8fb75ad7b6b563b0f14` |
| Scoped save assignment | `ev_ab5a3118a929b2d1c679f6179c2186f763e2eb532e6fc03196c5d0c0b6405cf1` |
| Parameter registration | `ev_30f0afc704cbbcbefda3cc8a416a441b22230529a9bb4c945b4ebc2b7f285340` |
| Non-player installation postprocessor | `ev_1475918b6326e916e60d15b30762a8eeaea64ec190428cbff6654f2d2ba506c5` |
| Death-drop candidate classification | `ev_8ab8df8c62f786e51c06e3aa2abc6c67426b947fc4b1975a92c8517077e53f73` |

| Retained caller | Established contract | Remaining bound |
| --- | --- | --- |
| `0x1432e5ef0`, `ev_907710a2df1aac6228d5a37760ab4a7534eae0400ee58d298535148e0f7c8430` | Registered NPCOtomo API forwards the attack array from `0x142e711b0` as p9; p10 empty, p8=0, p13=0. Only a nonempty p9 bypasses initial natural setup; the two-row source table and three-entry matching results are established above. | NPC-companion caller species/Unique NPC keys remain caller-specific; this is not an ordinary-wild input proof. |
| `0x1432e6e70`, `ev_096b4cfea4cfb3423b416637670e86beeecc077cf189b4931c1c534ef36e7ffb` | p9 empty, p10 copied from supplied passives; forwards the skip/form flags, allowing natural attack setup. | Values supplied by each upstream caller remain separate contracts. |
| `0x142d45b60`, `ev_73e8dd25ef9c5f55d629bc3fdab7f37b247fe55889a135f3f77f20da8cdac391` | p8=1, p13=0; forwards copied explicit arrays and caller talent override. Random passive filling is skipped, while defaults/caller passives remain separate. A registered PalArenaSoloNPCSpawner NPC entry reaches this helper. | Other API callers and concrete arena field values remain separate; the method is not ordinary-wild input proof. |
| `0x142b84150`, `ev_6c2faccb3b1e3bef42d13650cebd8078c74e7a21ecc26529a8324d6f5cd50554` | Registered GetInitializedCharacterSaveParemter_ParamSetup marshals arrays and booleans to general initialization. | Marshalling is not proof of ordinary wild-spawn inputs or concrete flag values. |
| `0x143035620`, `ev_dcf71cb7b0412941fe9ba97fc22305b9e4117d675811a5d49db5ec94692a81c5` | Copies dynamic egg data `+0x78`; calls `1432e6e70` only when the copied CharacterID is empty, using metadata species `+0x70`. | Egg-save fallback is distinct from ordinary wild spawning. |
| `0x142ed6840`, `ev_929af7c2e0667593f578e1f3dd774d4a57b7170296387534baab5bb78e517fcf` | Calls `1432e6e70`, then copies the generated save into its dynamic-item object. | Item-generation context must not be relabelled ordinary wild spawning; incomplete inferred call arguments prevent assigning every flag here. |

The recruiter, NPC attack setup and connected KingWhale capture callback retain
their separately established contracts. Remaining anonymous candidates in the
inventory below are not silently promoted to required wild modes.

#### Existing caller APIs: NPC companion and arena solo NPC

**2026-10-24 caller classification.** Native name/exec pairs bind the utility
entry points without inferring ordinary gameplay from an address:

| Name/exec pair | Registered name | Entry and recovered callee |
| --- | --- | --- |
| `0x147854eb0` | GetInitializedCharacterSaveParemter_NPCOtomo | `0x142b83f10` → `0x1432e5ef0` |
| `0x147854ec0` | GetInitializedCharacterSaveParemter_ParamSetup | `0x142b84150` → general initializer |
| `0x147307cc0` | GetInitializedNPCSaveParameter | `0x142849450` → `0x142d45d70` → `0x142d45b60` |

The spelling `Paremter` is the native registered name. For the last entry, registration
`0x142848850` passes the six-method array starting at `0x147307cb0` and UClass from
`0x1428464d0` to the native-function registrar. That class getter names
**PalArenaSoloNPCSpawner**, size `0x2e8`. Thus the owner is established, not inferred
from the NPC token or function location. The native registration array supplies the
API/class binding.

Its NPC method forwards source fields `+0x2ac/+0x2b4/+0x2bc/+0x2c0/+0x2c4/+0x2c8`
and empty temporary arrays to the existing parameterized helper. That helper
sets skip-random-passive-filling true and form selection false. Empty explicit
attack arrays permit the natural-attack branch; character-default passives are
still a distinct earlier stage. These are recovered input relationships for the
arena-NPC API, not ordinary-wild defaults. The six field names and their effective
values are not established here; a bounded descriptor check for names containing
CharacterID/NPCID found no matching field descriptor at those offsets. That no-hit
does not prove the fields lack metadata or that another input is absent.

The NPCOtomo entry is a separate NPC-companion API. Its attack helper's source table and matching conditions are bound above:
ColorfulBird and RobinHood_Ground supply three entries, while nonmatching
species return empty. **Forwarding an array alone does not prove that natural
setup is skipped**, and derived natural availability remains separate. No ordinary spawn-mode argument values are
inferred from either reflected wrapper. Egg fallback and dynamic-item generation
remain separately classified in the table above.

**Caller boundary:** the Unique NPC lookup and final save write are established.
NPC-companion and arena-solo-NPC entries have distinct contracts from the standard
wild-spawner invocation, including its None key and two branch contracts. Other spawn
modes retain their own input gaps. That caller must be justified by its effect on saved
gender/passives/attacks, rather than expanding into spawn scheduling. Arena
field-name/default recovery is a separate follow-up only if its declared arena output is
needed. Later attack, gender and target/source sections close their specific
supplied-input and saved-transfer questions; no broader spawner census is inferred.

| Decisive observation | Evidence ID |
| --- | --- |
| NPCOtomo wrapper, `0x142b83f10` | `ev_64bb31bc881f41e2e86075f82be9ad87e31063c97e024444227397a7079c519c` |
| Arena NPC exec wrapper, `0x142849450` | `ev_87f4f532a11a8f208b4a2229c48dc6dcfa5e6e278ec0059b5ed708ed1763ffa7` |
| Arena NPC argument forwarding, `0x142d45d70` | `ev_b4434ffd39b1d9b27a76da3fbff5135fd4834fe7cccddfe87e09d137c3fe6831` |
| Native function registration, `0x142848850` | `ev_a70883abf84aa1be60a2e640457730fd7c9639a0ecda54fa7604e69f240e84b2` |
| PalArenaSoloNPCSpawner class, `0x1428464d0` | `ev_92b7a432731aa8efd303bef67737c609ee3fb4c223dda953871f8ff1fd9fd9aa` |

## Shared capture and storage boundary

Updated 2026-10-22. The recovered ordering is:

```text
CaptureSuccess(captor,pal):
    NPC hook: if equipment.count>3, rebuild AddUnique(equipment.first(3))
    broadcast actor OnCaptured                       # includes KingWhale handler
    if native capture-augmentation predicate passes:
        append synchronization results under the shared four-slot cap
        conditionally append RainbowPassiveRate rank-four result
    update ownership metadata and health state
    status = place the SAME individual handle in an eligible container slot
    if status != 0: construct fallback transfer/drop snapshot
```

Insertion `0x142e38270`,
`ev_81782a864b0079965e496fa0eb55a03e0cf61920d86dfa448ce5717942544220`,
returns **0 on success**, 10 when no suitable slot exists, 4 for an invalid
container and 6 for an invalid handle. Treating this return as a success boolean
would reverse the normal and fallback paths.

The successful concrete setter `0x142f933a0`,
`ev_93dc57359675b21c80a5984917fdf29e252f911932355a18397f6031eae50d73`,
is bound through PalIndividualCharacterSlot constructor `0x14296a7d0`:
primary vtable `0x1474f90a0`, secondary interface at slot `+0x28` with vtable
`0x1474f9380`. Primary slot `+0x2c8` points to leaf `0x14296b960`; its bytes
`33d2488d41284885c9480f44c2c3` return slot+0x28, or null for a null slot.
Secondary vtable entry zero points to the recovered setter:

```text
SetSlotHandle(interface,handle):
    if interface.handle == handle: return
    old = interface.handle
    interface.handle = handle                       # slot+0x60
    if handle invalid: clear slot's cached metadata/parameter pointer
    else:
        copy handle metadata to slot interface
        parameter = handle.GetParameter()
        if parameter valid:
            parameter.containerID_and_index = interface.containerID_and_index
            optionally cache parameter in slot
    notify slot change with old handle
```

Its only direct writes into the parameter are `+0x528..0x53b` (container identity
and index). Species `+0x3d0`, gender `+0x3e0`, level `+0x3f0`, equipment `+0x428`,
mastered `+0x438`, talents `+0x450..453` and passives `+0x460` are preserved.
Ownership setter `0x142f851b0` writes owner/history ranges outside those traits;
the audited health setter `0x142f8bfe0` writes health/state/cache fields only.
This closes the explicit ordinary server capture-to-container field boundary.

Nonzero insertion status takes `0x1432d5030`,
`ev_df0b305aa648a00ef0f1c249f88e497e2188425f385ed6f5167121b3a2573bc7`:
snapshot `0x142f75f90` deep-copies the saved block, resets only snapshot
`+0x158..16b`, and moves it through `0x142ea1d40` into a transfer packet.
The required fields therefore survive this explicit fallback snapshot too.
This is static field-transfer evidence, not a live capture or disk-save test.
