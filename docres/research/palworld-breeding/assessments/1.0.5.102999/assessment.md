# Palworld 1.0.5.102999: mechanics assessment

- Game version and source: packaged `ProjectVersion=1.0.5.102999` in
  `Pal/Config/DefaultGame.ini`; no title-screen observation.
- Platform/configuration: Windows x86-64, Steam, shipped executable/base archive.
  An installed breeding mod is excluded; effective modded/world configuration
  and player data were not inspected.
- Supporting identity: Steam build 25246127; executable SHA-256
  `e590b5e7bfaa3fea40fab1a02cc72c8fc5fd6f8631ef2308e95ac56c25195837`.
  [Archive and decoder identities](evidence.md#identity-and-methods).
- Review dates: 2026-10-21 through 2026-10-29; editorially simplified 2026-10-30; findings last reviewed 2026-10-29.
- Previous game-version report: none; this establishes a baseline only where
  the evidence supports the stated scope.
- Compared PalCalc baseline: mechanics document at
  `1c09c860fefc8769250ffaf52af42910d48916b9`, with the initial working-tree
  modifications to `PalCalc.Model/GameConstants.cs` and `PalCalc.UI/App.xaml.cs`
  preserved. This is a documentation/model comparison, not a solver-code audit.
- Investigation state: **Finished**, 2026-10-29, after both acceptance findings
  received new native proof and independent recheck. The October 28 completion
  remains withdrawn; this is a separate evidence-based completion decision.
- Verification coverage: **Full: eight Verified** for the recorded base-game
  questions, inputs, modes and saved-output boundaries. No original mode was
  removed or question narrowed to restore coverage. There is no previous fully
  verified version; these are baseline findings, not verified-unchanged claims.

## Review summary

| Mechanic | Status | Checked date | Modes covered / gaps | Change from previous version | Conclusions | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| Child species | Verified | 2026-10-27 | Recipes/ranks/items, boss/mutation, shipped first-load order, both egg routes and extra-save application; supplied effect conditions explicit | Further baseline verification | [Conclusions](#child-species) | [Evidence](evidence.md#child-species) |
| Child gender | Verified | 2026-10-22 | Ordinary/mutation, both egg routes, pickup effects and final extra-egg save | Baseline recovery | [Conclusions](#child-gender) | [Evidence](evidence.md#child-gender) |
| Child passives | Verified | 2026-10-27 | Ordinary/item/mutation, ordered current pools and primary/additional saved contents; supplied pickup effects and stated source-setup conditions retained | Further baseline verification | [Conclusions](#child-passives) | [Evidence](evidence.md#child-passives) |
| Child attacks | Verified | 2026-10-22 | Generation/availability, both egg routes and final extra-egg save | Baseline recovery | [Conclusions](#child-attacks) | [Evidence](evidence.md#child-attacks) |
| Child IVs | Verified | 2026-10-26 | Sources/items/mutation, primary/additional saves through hatch; collection routes bound, supplied effects and fixed-rank setup conditions explicit | Further baseline verification | [Conclusions](#child-ivs) | [Evidence](evidence.md#child-ivs) |
| Wild-Pal passives | Verified | 2026-10-29 | Named AddRarePal/byte43 binding established; assignment/resolved species, supplied spawn modes, current pools, capture predicates/source contracts, additions, KingWhale and saved storage; runtime frequencies explicit | Further baseline verification | [Conclusions](#wild-pal-passives) | [Evidence](evidence.md#wild-pal-passives) |
| Wild-Pal attacks | Verified | 2026-10-29 | Form/settings-to-saved-input bridge independently rechecked; overall available set, natural/Tamago, NPC source and storage; KingWhale selected action start preserves availability inputs | Further baseline verification | [Conclusions](#wild-pal-attacks) | [Evidence](evidence.md#wild-pal-attacks) |
| Wild-Pal gender | Verified | 2026-10-24 | Species/form initialization or supplied saved gender through installation, actor binding and ordinary/KingWhale capture | Baseline recovery | [Conclusions](#wild-pal-gender) | [Evidence](evidence.md#wild-pal-gender) |

## Child species

Question: which saved child ID reaches hatch? Inputs are parent IDs/Tribes/genders, recipe and character tables, `CombiRankBonus`, mutation settings and supplied pickup effects. Ordinary/item/mutation, primary and additional eggs are included. Output is saved CharacterID, distinct from egg-item metadata. Selection and shipped first-load tie/first-match inputs are established; supplied pickup effects remain conditional inputs, without arbitrary activation-frequency claims.

**Verified for declared inputs/modes, updated 2026-10-27.** Recipe matching checks both parent Tribe orders and
required genders, with None unrestricted; the first qualifying row wins. Ordinary
selection globally excludes recipe children, IsBoss and IgnoreCombi rows. It uses
nearest rank, higher CombiDuplicatePriority for distance ties, then enumeration
order. There is no separate same-species shortcut in the recovered selector.
CombiRankBonus is clamped to 0..10. Ordinary generation can replace the selected
ID with a boss form; mutation uses a separate rank formula and permits recipe
children in its nearest-rank pool. [Full model and proof](evidence.md#child-species).

Proposed reduced PalCalc model:

```text
match recipe with gender constraints
otherwise nearest eligible rank to ceil((rankA+rankB)/2)+clampedRankBonus
preserve native priority/order where ties can affect the resulting usable ID
model subsequent species replacements separately, or explicitly exclude them
```

The ceiling expression agrees within normal representable positive-rank inputs
and default rounding; it is not the native formula for arbitrary int32 inputs.
The documented ordinary recipe/rank account agrees with this recovered core.
The current tables contain eligible quest variants and equal-rank/equal-priority
rows. Their first-load order is now bound through native table members, the
identified single-parent composite/plain DataTable load paths and the checked
decoder bridge. The ordinary pool has 186 candidates; mutation has 263. Recipe
inputs have no overlapping valid matches with different child IDs.

PinkCat, PlantSlime and SheepBall win their respective equal-priority ties over
the quest/Flower counterparts. The rare-form helper's BattleBGM=Predator,
IsTowerBoss and IsRaidBoss exclusions are bound; its first matching PlantSlime
rare form is BOSS_PlantSlime. With specified PlantSlime_Flower parents, zero rank
bonus and ordinary nonmutation generation, the target 2950 selects PlantSlime,
or BOSS_PlantSlime if the ordinary boss branch succeeds. Thus an unconditional
same-ID self-pair shortcut is not equivalent for those declared inputs. This is
a concrete static model consequence, not a live test, parent-acquisition claim
or current solver-code audit. [Ordered inputs and witness](evidence.md#concrete-species-ties-recipes-and-self-pairs).

For PalCalc, preserve recipe/gender precedence, native candidate eligibility,
priority and material table order; evaluate a self-pair shortcut only where it
agrees with those rules. Ordinary boss replacement and mutation remain omissions
from the simpler documented rank account. A missing implementation mode is not
proof of a recent game change, and this documentation update implements no fix.

The first-load order argument is specific to the shipped tables. Arbitrary runtime
table edits, altered floating-point rounding and effect-source activation
frequencies remain outside this conclusion. [Binding proof](evidence.md#shipped-species-table-binding-and-order).

## Child gender

Question: how is saved child gender assigned and preserved? Inputs are the selected species and signed `MaleProbability`; ordinary/item/mutation and both egg routes are included. Output is the gender byte carried into the hatched save. Assignment and preservation are established; nominal ratio probabilities are not measured live frequencies.

**Verified, updated 2026-10-22.** The recovered helper reads signed percent
MaleProbability: negative/missing → None, 100 → Male, 0 → Female; other values
compare against a float32 draw on 0..100. Mutation repeats the same assignment
inline. Assignment uses the selected child species, not the recipe's required
parent genders. [Model and proof](evidence.md#child-gender).

Proposed reduced model: `gender chance = species ratio`, with explicit fixed and
None cases. This is a nominal probability approximation to the finite RNG grid;
the traced current-farm, legacy and extra-egg paths preserve the assigned byte. The baseline documentation correctly
separates child assignment from recipe matching but leaves this algorithm open.
No complete code-accuracy finding is made. Current reflection corroborates the
MaleProbability field. The current inventory-farm save path preserves gender through live registration.
The latest [extra-egg continuation](evidence.md#extra-egg-save-application-and-mutation-selectors)
closes the remaining grant boundary. Both pickup effects preserve gender in their
child-save writes; unknown activation rates do not change that conclusion.

## Child passives

Question: which identities and achieved count reach the child save, and with what selection rules? Inputs are ordered parental lists, `Combi_PassiveInheritNum`, `Combi_PassiveRandomAddNum`, `PassiveInheritCountOverride`, manager pools/weights, mutation mode and supplied pickup effects. Output is the final saved passive list for primary/additional eggs. Ordered current pools, selection and final transfer are established for these per-egg inputs. Supplied effects remain conditional inputs; no unconditional per-pickup mixture for arbitrary changing source setups is claimed.

**Verified, updated 2026-10-27.** Ordinary generation selects without replacement from
the distinct parent union, then attempts random additions. Positive
PassiveInheritCountOverride replaces both count rolls; Cake05 supplies 4.
The filler compares compacted local weights but subtracts original manager
weights. Removal can therefore bias outcomes or consume an attempt without a
selection. Mutation has its own pool composition and can displace parental
entries, including after a positive override. [Model and proof](evidence.md#child-passives).

Proposed reduced PalCalc model:

```text
keep parent union identities and achieved parent count
ordinary count weights -> sample distinct parents -> random-attempt count
positive override -> sample up to target -> attempt shortage fill
if assuming fill always succeeds, label that approximation explicitly
keep mutation a distinct mode, not an ordinary count-weight adjustment
```

Uniform-subset selection is a nominal approximation to clamped finite-grid
indices. Ignoring the identities of unwanted additions can be useful, but does
not justify assuming attempted additions always achieve their count. The baseline
mechanics and limitations already identify this distinction and the Cake05
shortfall. The current asset snapshot newly binds those inputs to this report's
version. It does not establish a new game bug or quantify its live incidence.
Current inventory-farm copies preserve the generated passive list. Native
first-load reconstruction now binds the shipped composite table to its sole
Common parent, serialized row order, ascending reserved/allocated slots and
fresh manager pools. The decoder's counted reader and unsorted writer were
checked; fresh extraction reproduces the same order. All 85 ordinary candidates,
12 rank>3 candidates and five mutation candidates now have explicit ordered
inputs. [Load contracts and snapshots](evidence.md#shipped-composite-first-load-order).

This resolves a practical data condition: with CraftSpeed_up3 excluded, a first
fill attempt has total 7,355 and 95 tickets select nothing. CraftSpeed_up1 gets
five tickets and Deffence_up2 gets 195, despite both row weights being 100;
WorkSuitabilityAddRank_MonsterFarm_2 gets none in that attempt. With Cake05's
four-passive target and only CraftSpeed_up3 in the parental union, a first failed
fill leaves at most three final passives. [Current-data witness](evidence.md#concrete-current-data-fill-shortfall).
These are integer-ticket consequences, not measured frequencies or a current
solver-code audit. Treating filling as guaranteed remains a model approximation.

The saved-content calculation remains conditional on the declared per-egg inputs
and supplied pickup effects. It does not establish an unconditional per-pickup
mixture for arbitrary changing parties. The separate unchanged-rank NaughtyCat
source contract is summarized under [effect sources](#ordinary-effect-source-conclusions).

## Child attacks

Question: which attacks remain available, and which are initially equipped? Inputs are equipped parental moves, child species/level, learning/Waza tables, `bInheritAllActiveSkills` and mutation mode. Output is saved mastered/equipped arrays plus derived learned availability after hatch. Ordinary/item/mutation and additional eggs are covered; pickup effects preserve these contents.

**Verified, updated 2026-10-22.** Parents supply equipped attacks. The ordinary routine
draws one from the distinct eligible union; missing rows and IgnoreRandomInherit
are excluded. It adds level-one natural moves to starting equipment. Cake05's
bInheritAllActiveSkills grants all eligible equipped parent attacks to the
generation mastered list, with equipment capped at three. Mutation makes an
additional primary-element Strong-pool draw; duplicates aren't retried.
[Model and proof](evidence.md#child-attacks).

Proposed reduced model:

```text
eligible union of equipped parent attacks
ordinary: one selected parent attack; cake: all eligible parent attacks
union with the child's guaranteed natural moves for the availability question
available moves = stored mastered union natural moves through current level
mutation: separate extra draw; do not promise an additional distinct move
```

The ordinary documented union/eligibility model agrees at the recovered generation
boundary. Equipment truncation alone does not show lost inheritance. Conversely,
raw MasteredWaza and learned availability are distinct: the recovered availability
getter unions raw mastered moves with level-eligible natural moves. This closes
the earlier availability gap; natural moves need not be copied into raw mastered.
No false discrepancy is raised from equipment ordering or the term “exclusive.”
The shipped CharacterIDReplacer alias Gorrila→Gorilla affects no current learning
row IDs. Current inventory-farm copies preserve the stored arrays through hatch
and live registration. The latest extra-egg creation hook copies both arrays into the new egg save;
alpha conversion also preserves them. Activation rates do not affect their
preservation, so this row is now Verified. [Shared boundary](evidence.md#shared-child-save-boundary).

## Child IVs

Question: which values reach saved HP, Shot/Attack and Defense, with Melee copying
tracked separately? Inputs remain parental talents, `Combi_TalentInheritNum`, item
bonus range, mutation settings and supplied pickup effects. Output includes
primary and additional egg saves through hatch. Ordinary/item/mutation,
current-farm direct hatch, inventory collection/drop/re-pickup, and the separate
legacy world-egg route are covered. The unchanged ordinary NaughtyCat setup is a
separately established source calculation, not an unconditional party model.

**Verified, updated 2026-10-26.** The middle weighted category always takes HP
from a parent and has an all-three-parent branch. It is not a uniformly selected
two-stat subset. Each selected parental stat separately chooses either parent.
Other stats get fresh 0..100 candidates; ordinary talent-bonus items add 1..5 and
clamp. Mutation instead uses fresh default 90..100 values without that ordinary
bonus path. Shot is copied to Melee. [Generation model and proof](evidence.md#child-ivs).

Proposed reduced model:

```text
sum weighted category/branch probabilities using the actual parent-source sets
for each parent-sourced stat, evaluate either parent's qualifying value
optionally omit coincidentally qualifying fresh random values, with that omission stated
apply ordinary item bonuses only in their own branch
evaluate mutation's fresh range separately
```

The baseline document's asymmetric source sets agree with the recovered helper.
Its ordinary inheritance discussion omits talent bonuses and mutation; the
limitations identify those omissions. Source labels must remain separate from
numerical matches. No solver change or probability fix is claimed here.

**Collection determines whether a transform applies.** Current-farm direct hatch
preserves the generated save through its shared container and pending hatch record.
The item-container pickup API separately transfers the existing dynamic identity.
The shipped drop/re-pickup route selects CommonDropItem3D / PalMapObjectDropItemModel,
whose generic pickup hooks do not transform that attached child save. Thus these
successful routes preserve generated IVs without the legacy extra-egg roll.
This is a route-specific conclusion, not a universal world-object bypass.
[Direct hatch](evidence.md#current-farm-direct-hatch-and-world-pickup-separation) /
[module pickup](evidence.md#container-pickup-into-inventory) /
[drop/re-pickup](evidence.md#world-placement-proof-boundary).

The separate legacy PalMapObjectPalEggModel applies supplied pickup effects.
Alpha conversion copies a nonempty child save; an additional egg can reroll
selected talents in ordinary mode or generate fresh mutation talents. The
prepared save reaches successful insertion or the recovered fallback creation
path, then hatch. [Transforms and final save](evidence.md#extra-egg-save-application-and-mutation-selectors).

For unchanged ordinary NaughtyCat sources satisfying the initialization, ownership,
party, unlock, restriction and disable contract, fixed initial ranks 1–5 supply
50/55/60/65/75. Shipped same-Tribe non-stacking takes their maximum. The checked
static item sources add no extra-egg record. Those values apply to the legacy
egg consumer, not generic dropped-farm-egg pickup.
[Item inputs](evidence.md#shipped-item-sources-do-not-add-extra-egg-records) /
[rank mixtures](evidence.md#unchanged-ordinary-naughtycat-rank-mixtures).

Arbitrary post-grant source changes, modified worlds, live activation frequencies,
exact joint RNG replay and disk-save/gameplay observations are not established.
The scoped calculation does not promise an unconditional extra-egg or threshold
success rate for a different or changing setup.

## Wild-Pal passives

**Verified for declared assignment/rare inputs, updated 2026-10-29.** Native
descriptor/name/setter proof binds `AddRarePal` to row byte `+0x43`. The recovered
consumer and current decoded singleton therefore join: the rare addition draws
`Rare` with weight100, with existing-list duplicate suppression and no four-slot
append guard. Ordinary filling and the declared target/source closures remain
established. [Rare-pool proof](evidence.md#rare-passive-draw-and-current-pool). The question
remains which list reaches captured storage. Inputs are the assignment-stage
species, Unique NPC key/resolved saved species, explicit caller passive array,
random/rare/skip flags, current manager pools, supplied target/source state and
relic value. Ordinary/rare initialization, overrides, World Tree spawning,
capture additions and KingWhale remain in scope; rare initialization retains
the distinct producer contract below. No mode was removed to preserve the grade. [Full model and observations](evidence.md#wild-pal-passives).

Initialization applies assignment-species defaults before explicit caller traits
and random filling. A nonempty Unique NPC key can replace saved CharacterID
without replacing that earlier assignment input. The native random-count weights
are [10,76,10,3,1] for indices0..4. Filling retains the recovered ordered-pool,
weight-array mismatch and possible shortfall; an attempted fill is not a guaranteed
new passive. The World Tree caller contributes its established one-of-seven draw
when that supplied mode is selected. These rules establish contents given the
caller inputs, not a distribution of every placed spawner.

Capture preserves the initialized list and conditionally adds synchronization
and relic traits before later ownership/storage. For a valid resolved target,
its gate requires no live trainer, a valid PalGroupManager, no matching live
GuildBase-map entry, zero OldOwnerPlayerUIds.Num and false IsPlayer. The full
predicate retains its missing-individual fallback; that fallback is not used to
prove the ordinary setup. Below-four passives alone is insufficient. Source traversal is ascending occupied registry
slots, then stored source-passive order; filters, existing-list deduplication,
pending entries, integer-percent draws and the four-slot cap remain as recovered.
RainbowPassiveRate separately supplies its high-rank-pool percent input.
KingWhale's connected callback preserves existing passives while changing its
other saved fields; a target-row default does not erase a stored Legend.

### Ordinary target and source closure

The original fresh PinkCat setup is now connected from initialization through
augmentation to saved storage. Construction/copy/install values are joined to
actual binder, group, spawner/controller, facial, equipment and removal recipients.
The last two concrete initialization dispatches now resolve to a query and engine
ForceNetUpdate, with network-owned scheduling/dormancy destinations. Under the
declared unchanged setup and recovered native routes, trainer stays null,
IsPlayer false, owner history empty, and Neutral GroupId does not create the
GuildBase rejection entry. The target passes the named gate. This is a static
state-transfer conclusion, not a claim that every catchable target is eligible.
[Target argument](evidence.md#fresh-pinkcat-capture-target-argument) /
[last virtual bindings](evidence.md#final-ordinary-target-initialization-virtuals-and-eligibility).

Freshness does not itself exclude equipment notifications: ordinary collect-all
PinkCat generation reaches a fourth equipped move at level22. The
[count/notification boundary](evidence.md#capture-equipment-notifications-supplied-count-boundary)
and concrete consumer contracts are retained because they precede augmentation;
equipment trimming does not reduce the overall available-attack set.

The unchanged eligible active rank-1 GuardianDog setup reaches an accepted
type55/value15 contribution to the captor. Its battle-mode listener schedules
reset work rather than clearing inline; a completed eligible reset deletes the
exhausted Active invocation state and retries the same grant. Thus a prior
completed reset alone does not leave that sole-source answer unknown. Validity,
recipient, unlock/restriction/disable and world-exclusion conditions remain
explicit. Multiple sources retain runtime membership/order inputs; a query
reentered during reset is not inferred from a hypothetical listener.
[Source grant](evidence.md#ordinary-rank-1-guardiandog-active-grant-to-capture-collector) /
[completed reset](evidence.md#completed-partner-reset-and-unchanged-guardiandog-regrant).

The named target/source result is separate from the rare-pool and form-input
bindings. Both later acceptance gaps are resolved; their significant coverage
corrections are recorded in the [completion history](#acceptance-review-and-bounded-continuation).

### Reduced PalCalc model and comparison

Start from assignment-species/caller contents, apply the actual random-count/fill
process, and model capture additions separately from breeding inheritance.

```text
P = InitializeWild(assignmentSpecies, callerPassives, flags, currentOrderedPools)
P = ApplyCapture(P, targetPredicateInputs, orderedQuerySources, relicValue)
return P in the captured save
```

Condition them on eligible target state and the supplied ordered source records;
retain filters, slot competition and fill failures when estimating desired
contents/attempts. Existing guaranteed-wild examples require their specified
initialization/capture conditions. Treating capture as a universal no-op is not
supported. This is further baseline recovery, not a game-version change or a
solver fix. The current code was not audited or changed; the named documentation/model
comparison remains the recorded baseline.

Unconditional live activation/spawn frequencies, arbitrary changing parties,
modified world/network implementations and external player state are not inferred.
Their absence does not invalidate the recovered calculations or declared setup,
and none was silently removed from a claimed unconditional probability.

## Wild-Pal attacks

Question: which attacks are **available overall**
at captured storage? Equipped loadout is not a requested output; equipment
handling matters only where it changes the available set. Inputs remain spawn
species, move-species resolver, level, explicit attacks, rare/Tamago flags and
learning tables. Ordinary/rare initialization, relevant supplied overrides and
the connected KingWhale capture mode remain included.

**Verified after independent acceptance recheck, 2026-10-29.** The form/settings
producer and stage-query contract now derive assignment species, rare state and
saved species/level for the original declared spawn inputs and supplied world
state. [Full producer model](evidence.md#wild-form-selection-and-saved-level-handoff).
The requested level still feeds initial attack setup; successful rare selection
adds inherited RarePal_LevelAdd=5, range choice precedes the CharacterMaxLevel=80
cap, and final natural availability uses the saved level. Predator selection
uses a validated prefixed row and does not set the rare flag or add those levels.
The question was not narrowed to resolved inputs. Availability is the union of stored MasteredWaza
and all natural moves through the current saved species/level. The query does
not read EquipWaza. Nonempty explicit attacks populate mastered and suppress
initial natural setup, but derived natural availability remains. Rare/Tamago
extras and fallbacks can add mastered entries; the Tamago draw is row-weighted,
including duplicate rows. Equipment-only capture trimming and equal-level
ordering do not remove overall availability. [Full model and evidence](evidence.md#wild-pal-attacks).

Reduced model:

```text
M = explicit attacks when nonempty; otherwise generated rare/fallback mastered entries
preserve relevant later grants/removals to M
after capture, use the final saved CharacterID and level
available attacks = unique(M union level-eligible natural attacks for that species)
# Do not restrict this set to equipped slots or infer learning from a combat action.
```

The move-species helper is already bound to Unique NPC lookup, and the standard
spawner supplies a None key. New NPC-companion source evidence binds
FindNPCOtomoWaza to the shipped two-row table: ColorfulBird and RobinHood_Ground
supply three explicit attacks each; nonmatching species supply an empty array.
The general initializer's separate Unique NPC key still controls saved species.
[Caller/source model](evidence.md#npc-companion-attack-source).

The NPC master-setup review corrects an earlier incomplete summary: a weapon
normalization branch can add Weapon_Use to mastered, while punch removal affects
only equipment. All 753 rows in the checked Pal table have Weapon=None, so this
weapon-addition branch does not change availability for those Pals with existing
attack arrays. Empty-array fallback remains explicitly modeled. The named
APalNPC::MasterWazaSetup callback registration and its array writers are now
bound. [Correction and materiality check](evidence.md#npc-master-setup-correction-and-availability-effects).

**KingWhale action-start conclusion, 2026-10-27:** PhaseWazaList and
InterruptWazaMap preload/select combat actions. The concrete shipped actions for
Unique_KingWhale_Breaching_P3, Unique_KingWhale_TidalBore and
Unique_KingWhale_SuperTidalBore do not grant those IDs as mastered attacks during
initialization/start. Class declarations, Blueprint ancestry/overrides, the actual
component queue/start and immediate native parameter writes are now checked.
They preserve MasteredWaza, saved CharacterID and saved level. Capture preserves
the mastered list and replaces species with BOSS_KingWhale_otomo, whose natural
learning set contains none of the three IDs. An input without them mastered thus
gains none through this battle-action/capture route. An independent supplied
mastered entry remains available. Breaching_P3's action internally uses the
ordinary Breaching ID; these identities are not interchangeable.
[Concrete model and evidence](evidence.md#kingwhale-battle-actions-and-captured-availability).

The selected class/start and immediate-write evidence establishes the KingWhale
conclusion across the declared route. Arbitrary NPC/arena input distributions,
encounter timing, global unobtainability, unconditional frequencies and current
solver accuracy are not inferred.

## Wild-Pal gender

Question: what saved gender reaches captured storage? Inputs are resolved species/form and any supplied spawn save or explicit override. Output is saved gender, not recipe-parent gender.

**Verified, updated 2026-10-24.** Shared initialization assigns `Gender(S)` using the species after initial form selection. A supplied nonplayer save, including an explicit saved-gender override, instead supplies its own `G`. Native installation preserves that byte; both actor-creation dispatch routes connect the installed record to the actor's parameter component. Ordinary capture and the connected KingWhale path preserve it into storage. [End-to-end evidence](evidence.md#saved-wild-gender-from-spawn-input-through-captured-storage).

Reduced model:

```text
G = Gender(assignment-stage species) if initialized, else supplied saved Gender
apply any caller-supplied saved-gender override to G
successful spawn/install/bind/capture preserves G into storage
```

The Standard spawner concretely uses the ratio branch and a None Unique NPC key; later Unique NPC resolution therefore preserves its assignment species. For a nonempty key in another initializer caller, the saved CharacterID can differ without a new gender draw. Use the assignment-stage species rather than automatically applying the final species' ratio. No universal ratio-based wild distribution, caller-override frequency or live frequency is asserted.

### Wild caller contracts

The [Unique NPC resolver](evidence.md#unique-npc-key-lookup-and-saved-species)
returns the input species for an empty key. A nonempty key instead supplies the
shipped DT_UniqueNPC row's CharacterID; a missing row returns None without
falling back. With a valid resolved character row, common initialization saves
that ID. Gender and default passives retain the earlier assignment species;
natural attack setup uses the resolved species unless explicit attacks are supplied.
This ordering alone is not a live discrepancy without distinguishing caller inputs.

The [standard wild spawner](evidence.md#standard-wild-spawner-inputs-and-initializer-forwarding)
supplies selected PalID/level, None Unique NPC key, empty explicit attacks and
enabled random passive filling. Its World Tree branch supplies the recovered
passive draw with form selection disabled; the general branch supplies instance
RarePalAble. NPC-companion and arena APIs retain their separate
[caller contracts](evidence.md#existing-caller-apis-npc-companion-and-arena-solo-npc),
not inferred ordinary-wild defaults. Installation/binding preserves the supplied
nonplayer gender through the same individual/actor into captured storage.
No spawner population or override-frequency claim follows.

Capture additions separately use the actual trainer, owner-history, IsPlayer and
group predicate. This invocation's ownership-history update occurs after its
augmentation check; it cannot retroactively block that check. Preservation through
every release/re-capture mode is not established.

## Checks and reference updates

The baseline's static native/data checks and independent acceptance rechecks
support the published models. Reconstruction checks covered finite-grid selectors,
species rounding/ties/recipes, ordered passive pools and the current-data shortfall,
World Tree selection, save transfer and KingWhale availability consequences.
These test the reconstruction; they do not measure live frequencies or RNG
independence. No live-game/disk-save test or solver-code audit is claimed.

Dated corrections accompany the affected evidence and the
[completion history](#acceptance-review-and-bounded-continuation) below. These
baseline findings and corrections to understanding are not evidence of a historical
game update. The [changelog](../../CHANGELOG.md) records checked versions,
observed game changes and assessment scope.

The October 30 editorial pass changed no mechanics, coverage grades or checked
dates. It shortened supporting delivery/callback machinery and consolidated
status narratives, retaining conditions and verification landmarks. Local
links/anchors, Markdown structure and mechanics/qualification preservation were
checked against the saved pre-edit publication.

### Discrepancies relevant to the scoped PalCalc model

- Mutation's species/passive/attack/IV paths and ordinary talent bonuses require
  separate models; the ordinary explanation doesn't cover them. The baseline
  already declares these omissions. This report doesn't establish a new defect
  in solver code or a change since a previous release.
- Successful random filling is an approximation where the native vector mismatch
  can affect final counts. This is already documented; current data provenance
  does not quantify the practical frequency.

## Ordinary effect-source conclusions

The shared [partner-effect model](evidence.md#shared-partner-effect-model) connects
shipped definitions, runtime restrictions, activation and accepted records to
pickup/capture consumers. Trigger masks, recipients and source membership are
material inputs; accepted handler state alone is not proof of an accepted effect.
Removal can preserve records with other flags. General rank changes do not have
an established immediate passive-record refresh.

For **NaughtyCat**, use shipped defaults, ordinary non-BOSS fixed initial rank,
valid party identity/trainer, SkillUnlock_NaughtyCat, no active disable entries
or other contributing effects, and no intervening rank, ownership, holder,
restriction or disable changes. The trainer must also be the successful legacy
egg pickup player. Both spawned and unspawned valid companion cases satisfy the
ToTrainer identity test; the answer does not depend on activated index -1.
Shipped effective arena lists exclude neither extra egg nor synchronization.
The accepted rank-1 record reaches the scalar pickup cache and supplies nominal
50; qualifying fixed-rank mixtures use the [same-Tribe maximum](evidence.md#unchanged-ordinary-naughtycat-rank-mixtures)
of 50/55/60/65/75. Current item sources add no extra-egg record. This is not an
arbitrary-party activation model or measured extra-egg frequency.

For **GuardianDog**, use the analogous isolated ordinary active rank-1 setup
with SkillUnlock_GuardianDog, the same trainer/captor, unchanged validity,
restriction/disable/recipient conditions, an eligible target below four passives,
and a supplied eligible source-passive list. The accepted ToTrainer type55/value15
record reaches the source-key capture collector. Fifteen is each eligible
source passive's nominal integer-percent input, subject to the actual target
predicate, filters, duplicate handling and four-slot competition; no relic
contribution is assumed. Below-four contents alone do not establish eligibility.

The battle-mode listener **schedules**, rather than runs, reset inline. With no
intervening timer/source processing, it does not change the same synchronous
capture query. A completed eligible reset deletes exhausted Active invocation
state and retries/restores the same sole-source grant. Mid-reset reentry,
changed inputs and multiple-source registry positions remain separate conditions.
[Active grant](evidence.md#ordinary-rank-1-guardiandog-active-grant-to-capture-collector) /
[scheduled reset](evidence.md#battle-mode-source-reset-is-scheduled-not-invoked-during-registration) /
[completed regrant](evidence.md#completed-partner-reset-and-unchanged-guardiandog-regrant).

## Investigation state and remaining gaps

**Finished, 2026-10-29; eight Verified** for the recorded shipped-base questions,
inputs, modes and saved-output boundaries. No required observation remains planned.
Runtime inputs and unclaimed outputs below remain explicit; they do not remove
an original mode or narrow a question to obtain verification.

### Acceptance review and bounded continuation

The October 23 premature completion and October 28 all-eight-Verified/Finished
claim remain withdrawn. The October 29 acceptance review identified missing
form/settings-to-saved-input and rare-pool schema proof. New native evidence
closed both gaps and independent rechecks accepted the resolutions before the
current completion decision. This does not retroactively justify either earlier
stop or imply a game update.

| Resolved requirement | Current conclusion / boundary |
| --- | --- |
| Form selection and saved-level handoff | Named settings/defaults, rare ranges, Predator replacement and stage-position query derive assignment/rare/saved species and level from original spawn inputs plus supplied world state. Initial attack setup uses requested level; final natural availability uses saved level. [Evidence](evidence.md#wild-form-selection-and-saved-level-handoff). |
| Rare passive current-input link | Named descriptor/setter binds row byte +0x43 to AddRarePal, joining the consumer to the Rare/weight100 singleton. Duplicate suppression and uncapped append remain explicit. [Evidence](evidence.md#rare-passive-draw-and-current-pool). |

The observations and current data support verification; reviewer agreement alone
does not supply missing proof. Established capture target/source closure stands.

| Remaining limit | Consequence |
| --- | --- |
| Arbitrary source membership and occupied-slot order | Query interface/traversal are recovered; actual sources/positions are inputs. The unchanged sole-source setup is connected; no mixture over all parties is claimed. |
| Other spawn/group/world states | Unique NPC, World Tree, organization/named-group and trainer/history/IsPlayer/exclusion predicates retain their supplied inputs. The ordinary Neutral result is not generalized to different states. |
| Live frequencies, exact RNG replay and mid-reset reentry | Static rules and labelled nominal calculations do not establish these outputs. A concrete output-relevant reentry route would require a targeted follow-up. |
| Mods, replacement network implementations and player configuration/data | Excluded from the shipped-base target. Recovered record/notification contracts do not guarantee arbitrary replacements harmless. |
| Solver implementation and supporting topics | Comparison is with the recorded documentation/model baseline. Solver fixes, breeding time and map conversion remain separate work. |
