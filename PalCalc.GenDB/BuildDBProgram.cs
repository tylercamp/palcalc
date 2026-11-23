using CUE4Parse.Compression;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Textures;
using Newtonsoft.Json;
using PalCalc.GenDB.GameDataReaders;
using PalCalc.Model;
using Serilog;
using SkiaSharp;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.RegularExpressions;

/*
 * To get the latest usmap file:
 * 
 * 1. Download the latest UE4SS dev build: https://github.com/UE4SS-RE/RE-UE4SS/releases
 *       "zDEV-UE4SS...zip"
 * 
 * 2. Go to Palworld install dir, copy contents directly next to Palworld-Win64-Shipping.exe
 * 
 * 3. Run the game, secondary windows pop up in background, one of them will be "UE4SS Debugging Tools"
 * 
 * 4. Go to "Dumpers" tab, click "Generate .usmap file..."
 * 
 * 5. Copy "Mappings.usmap" file created next to "Palworld-Win64-Shipping.exe"
 * 
 * (Delete / rename "dwmapi.dll" to effectively disable)
 *
 * The Steam Workshop "UE4SS Experimental (Palworld)" mod can also generate the usmap
 * without copying UE4SS into the Win64 folder. In that case, look under the workshop
 * UE4SS folder in Mods/NativeMods.
 */

namespace PalCalc.GenDB
{
    static class BuildDBProgram
    {
        private static ILogger logger = Log.ForContext(typeof(BuildDBProgram));

        // This is all HEAVILY dependent on having the right Mappings.usmap file for the Palworld version!
        //
        // (should be a folder containing "Pal-Windows.pak")
        static string PalworldDirPath = Environment.GetEnvironmentVariable("PALCALC_PALWORLD_PAKS")
            ?? @"C:\Program Files (x86)\Steam\steamapps\common\Palworld\Pal\Content\Paks";
        static string MappingsPath = Environment.GetEnvironmentVariable("PALCALC_MAPPINGS_USMAP")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Mappings.usmap");

        private static List<Pal> BuildPals(
            List<UPal> characterRows,
            IFileProvider provider,
            Dictionary<string, (int, int)> wildPalLevels,
            Dictionary<string, Dictionary<string, string>> palNames
        )
        {
            var icons = provider.LoadPackageObject<UDataTable>(AssetPaths.PAL_ICONS_MAPPING_PATH);
            var palsWithIcons = icons.RowMap
                .Where(row => !row.Value.Get<FSoftObjectPath>("Icon").AssetPathName.Text.Contains("T_dummy_icon"))
                .Select(row => row.Key.Text)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var allPals = characterRows
                // only get normal Pals
                .Where(p => p.IsPal && !p.IsBoss && !p.IsRaidBoss && !p.IsTowerBoss)
                .Where(p =>
                    !p.InternalName.Contains("Quest") &&
                    // (Rampaging pals have "PREDATOR" in the name. Note there's also a separate `Predator` field which is apparently completely different)
                    !p.InternalName.Contains("PREDATOR") &&
                    !p.InternalName.Contains("POLICE") &&
                    !p.InternalName.StartsWith("GYM_")
                )
                .Where(p => palsWithIcons.Contains(p.InternalName) || palsWithIcons.Contains(p.TribeName))
                // make sure the Pal is fully configured - unreleased Pals typically set these to -1
                .Where(p => p.Rarity > 0 && p.RunSpeed > 0 && p.WalkSpeed > 0 && p.BreedingPower > 0)
                .Select((row, index) => (Row: row, InternalIndex: index + 1))
                .Where(p =>
                    !p.Row.InternalName.StartsWith("SUMMON_", StringComparison.InvariantCultureIgnoreCase) &&
                    !p.Row.InternalName.EndsWith("_Oilrig", StringComparison.InvariantCultureIgnoreCase)
                )
                .Select(entry =>
                {
                    var (rawPal, internalIndex) = entry;
                    var localizedNames = palNames.ToDictionary(
                        kvp => kvp.Key,
                        kvp =>
                        {
                            var fromName = kvp.Value.GetValueOrDefault(rawPal.InternalName);
                            var fromAlternative = kvp.Value.GetValueOrDefault(rawPal.AlternativeInternalName);
                            return fromName ?? fromAlternative;
                        }
                    );

                    if (localizedNames.Values.Any(v => v == null))
                    {
                        logger.Warning("Pal {InternalName} missing at least 1 translation, skipping: {Json}", rawPal.InternalName, JsonConvert.SerializeObject(rawPal));
                        return null;
                    }

                    var englishName = localizedNames["en"];

                    var minWildLevel = wildPalLevels.ContainsKey(rawPal.InternalName) ? (int?)wildPalLevels[rawPal.InternalName].Item1 : null;
                    var maxWildLevel = wildPalLevels.ContainsKey(rawPal.InternalName) ? (int?)wildPalLevels[rawPal.InternalName].Item2 : null;

                    var isVariant =
                        rawPal.PalDexNumSuffix != null &&
                        rawPal.PalDexNumSuffix.Length > 0 &&
                        rawPal.PalDexNumSuffix != "None";

                    // (this used to have a Suffix, but that was removed for some reason)
                    if (rawPal.InternalName == "PlantSlime_Flower")
                        isVariant = true;

                    return new Pal()
                    {
                        Id = new PalId()
                        {
                            PalDexNo = rawPal.PalDexNum,
                            IsVariant = isVariant,
                        },
                        BreedingPower = rawPal.BreedingPower,
                        BreedingPowerPriority = rawPal.BreedingPowerPriority,
                        Price = (int)rawPal.Price,
                        Rarity = rawPal.Rarity,
                        InternalIndex = internalIndex,
                        InternalName = rawPal.InternalName,
                        Name = englishName,
                        LocalizedNames = localizedNames,
                        MinWildLevel = minWildLevel,
                        MaxWildLevel = maxWildLevel,

                        Size = Enum.Parse<PalSize>(rawPal.Size.Replace("EPalSizeType::", "")),
                        Hp = rawPal.Hp,
                        Defense = rawPal.Defense,
                        Attack = rawPal.ShotAttack,

                        WalkSpeed = rawPal.WalkSpeed,
                        RunSpeed = rawPal.RunSpeed,
                        RideSprintSpeed = rawPal.RideSprintSpeed,
                        TransportSpeed = rawPal.TransportSpeed,
                        Stamina = rawPal.Stamina,

                        MaxFullStomach = rawPal.MaxFullStomach,
                        FoodAmount = rawPal.FoodAmount,

                        Nocturnal = rawPal.Nocturnal,
                        CraftSpeed = rawPal.CraftSpeed,

                        WorkSuitability = new()
                        {
                            { WorkType.Kindling, rawPal.WorkSuitability_EmitFlame },
                            { WorkType.Watering, rawPal.WorkSuitability_Watering },
                            { WorkType.Planting, rawPal.WorkSuitability_Seeding },
                            { WorkType.GenerateElectricity, rawPal.WorkSuitability_GenerateElectricity },
                            { WorkType.Handiwork, rawPal.WorkSuitability_Handcraft },
                            { WorkType.Gathering, rawPal.WorkSuitability_Collection },
                            { WorkType.Lumbering, rawPal.WorkSuitability_Deforest },
                            { WorkType.Mining, rawPal.WorkSuitability_Mining },
                            { WorkType.MedicineProduction, rawPal.WorkSuitability_ProductMedicine },
                            { WorkType.Cooling, rawPal.WorkSuitability_Cool },
                            { WorkType.Transporting, rawPal.WorkSuitability_Transport },
                            { WorkType.Farming, rawPal.WorkSuitability_MonsterFarm },
                        },

                        GuaranteedPassivesInternalIds = rawPal.GuaranteedPassives,
                    };
                }).SkipNull().ToList();

            var missingIds = allPals
                .Where(p => p.Id.PalDexNo < 0)
                .OrderBy(p => p.BreedingPower)
                .ThenBy(p => p.InternalName);

            foreach (var (pal, index) in missingIds.ZipWithIndex())
            {
                pal.Id = new PalId() { PalDexNo = 10000 + index, IsVariant = false };
            }

            return allPals;
        }

        // descriptions may be formatted e.g.:
        // - "{EffectValue1}% increase to movement speed."
        // - "SAN drops <NumBlue_13>+20.0%</> slower."
        // - "Absorbs a portion of the damage dealt to restore <uiCommon id=|COMMON_STATUS_HP|/>. \r\nDoes not sleep at night and continues to work."
        //
        // (there's also `<characterName />` but this is only used for attack skills)
        private static string FormatPassiveDescriptionText(
            UPassiveSkill rawPassive,
            string description,
            Dictionary<string, string> commonTexts
        )
        {
            if (description == null) return null;

            var formatArgs = new Dictionary<string, string>()
            {
                { "EffectValue1", rawPassive.EffectValue1.ToString() },
                { "EffectValue2", rawPassive.EffectValue2.ToString() },
                { "EffectValue3", rawPassive.EffectValue3.ToString() },
                { "EffectValue4", rawPassive.EffectValue4.ToString() }
            };

            foreach (var kvp in formatArgs)
                description = description.Replace($"{{{kvp.Key}}}", kvp.Value);

            if (description.Contains("{"))
            {
                logger.Warning("Description contains leftover format params: {description}", description);
            }

            description = Regex.Replace(description, @"<(\w+?)\s+id=\|([^\|]+?)\|/>", match =>
            {
                var kind = match.Groups[1].Value;
                var id = match.Groups[2].Value;

                switch (kind)
                {
                    default: break;

                    case "uiCommon":
                        if (commonTexts != null && commonTexts.TryGetValue(id, out var itl))
                            return itl;
                        break;
                }

                logger.Warning("Unhandled format part {fmt} in {desc}", match.Value, description);

                return match.Value;
            });


            description = Regex.Replace(description, "<.+?>", "");

            if (description.Contains("<") || description.Contains(">"))
                Debugger.Break();

            return description.Replace("\r\n", "\n");
        }

        private static string BuildDefaultPassiveDescriptionText(
            UPassiveSkill rawPassive,
            Dictionary<string, string> commonTexts
        )
        {
            var k = commonTexts.Keys.ToList();
            string FormatEffect(string effect, float value)
            {
                var label = effect.Replace("EPalPassiveSkillEffectType::", "") switch
                {
                    "no" => null,

                    "CraftSpeed" => commonTexts["COMMON_STATUS_SPEED"],
                    "ShotAttack" => commonTexts["COMMON_STATUS_RANGE_ATTACK"],
                    "Defense" => commonTexts["COMMON_STATUS_DEFENCE"],
                    "MoveSpeed" => commonTexts["MONITORING_EFFECT_MOVESPEED"],

                    _ => effect,
                };

                if (label == effect)
                {
                    logger.Warning("Unhandled effect type {effect}", effect);

                    label = effect.Replace("EPalPassiveSkillEffectType::", "");
                }

                if (label != null) return $"{label} {value.ToString("+0;-#")}%";
                else return null;
            }

            var s = k.Where(v => v.Contains("SPEED"));
            var m = k.Where(v => v.Contains("MOVE"));

            var parts = new List<string>()
            {
                FormatEffect(rawPassive.EffectType1, rawPassive.EffectValue1),
                FormatEffect(rawPassive.EffectType2, rawPassive.EffectValue2),
                FormatEffect(rawPassive.EffectType3, rawPassive.EffectValue3),
                FormatEffect(rawPassive.EffectType4, rawPassive.EffectValue4)
            }.SkipNull().ToList();

            if (parts.Count > 0) return string.Join('\n', parts);
            else return null;
        }

        private static List<PassiveSkill> BuildPassiveSkills(
            List<UPassiveSkill> rawPassiveSkills,
            List<USurgeryPassive> rawSurgeryPassives,
            Dictionary<string, Dictionary<string, string>> commonTexts,
            Dictionary<string, Dictionary<string, string>> skillNames,
            Dictionary<string, Dictionary<string, string>> skillDescriptions
        )
        {
            return rawPassiveSkills.Select(rawPassive =>
            {
                // (note: passive skills without translations are typically bound to partner skills)
                var localizedNames = skillNames.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetValueOrDefault(rawPassive.InternalName));
                if (localizedNames.All(kvp => kvp.Value == null)) localizedNames = null;

                var englishName = localizedNames?.GetValueOrDefault("en") ?? rawPassive.InternalName;

                var localizedDescriptions = !rawPassive.IsStandardPassiveSkill
                    ? null
                    : rawPassive.OverrideDescMsgID != null && rawPassive.OverrideDescMsgID != "None"
                        ? skillDescriptions.ToDictionary(
                            kvp => kvp.Key,
                            kvp => FormatPassiveDescriptionText(rawPassive, kvp.Value.GetValueOrDefault(rawPassive.OverrideDescMsgID), commonTexts[kvp.Key])
                        )
                        : commonTexts.ToDictionary(
                            kvp => kvp.Key,
                            kvp => BuildDefaultPassiveDescriptionText(rawPassive, kvp.Value)
                        );

                var englishDescription = localizedDescriptions?.GetValueOrDefault("en");

                string Strip(string internalName) => internalName.Replace("EPalPassiveSkillEffectType::", "").Replace("EPalPassiveSkillEffectTargetType::", "");

                var effects = (new[]
                {
                    (Strip(rawPassive.EffectType1), Strip(rawPassive.TargetType1), rawPassive.EffectValue1),
                    (Strip(rawPassive.EffectType2), Strip(rawPassive.TargetType2), rawPassive.EffectValue2),
                    (Strip(rawPassive.EffectType3), Strip(rawPassive.TargetType3), rawPassive.EffectValue3),
                    (Strip(rawPassive.EffectType4), Strip(rawPassive.TargetType4), rawPassive.EffectValue4)
                }).Where(t => t.Item1 != "EPalPassiveSkillEffectType::no");

                var trackedEffects = effects
                    .Where(t => PassiveSkillEffect.TrackedEffects.Contains(t.Item1))
                    .Select(t => new PassiveSkillEffect() { InternalName = t.Item1, EffectStrength = t.Item3 })
                    .ToList();

                var surgeryData = rawSurgeryPassives.FirstOrDefault(p => p.PassiveSkill == rawPassive.InternalName);

                return new PassiveSkill(englishName, rawPassive.InternalName, rawPassive.Rank)
                {
                    Description = englishDescription,

                    LocalizedNames = localizedNames,
                    LocalizedDescriptions = localizedDescriptions,
                    RandomInheritanceAllowed = rawPassive.AddPal,
                    RandomInheritanceWeight = rawPassive.LotteryWeight,
                    TrackedEffects = trackedEffects,
                    IsStandardPassiveSkill = rawPassive.IsStandardPassiveSkill,
                    SupportsSurgery = surgeryData != null,
                    SurgeryCost = surgeryData?.Price ?? 0,
                    // ("no required item" is indicated as a value of "None", but handle nullability just in case that changes)
                    SurgeryRequiredItem = (surgeryData?.RequireItemId ?? "None") == "None" ? null : surgeryData.RequireItemId,
                };
            }).SkipNull().ToList();
        }

        private static string ActiveSkillInternalName(
            string wazaId,
            IReadOnlySet<string> rawWazaIds
        )
        {
            var attackId = wazaId.Replace("EPalWazaID::", "");
            if (
                ManualFixes.ActiveSkillInternalNameOverrides.TryGetValue(attackId, out var fixedId) &&
                !rawWazaIds.Contains("EPalWazaID::" + fixedId)
            )
                return fixedId;

            return attackId;
        }

        private static List<ActiveSkill> BuildActiveSkills(List<UActiveSkill> rawActiveSkills, List<PalElement> elements, List<UItem> allItems, Dictionary<string, Dictionary<string, string>> attackNames)
        {
            var rawWazaIds = rawActiveSkills
                .Select(attack => attack.WazaType)
                .ToHashSet();

            return rawActiveSkills.Where(s => !s.DisabledData).Select(rawAttack =>
            {
                var originalAttackId = rawAttack.WazaType.Replace("EPalWazaID::", "");
                var attackId = ActiveSkillInternalName(rawAttack.WazaType, rawWazaIds);

                if (attackId != originalAttackId)
                {
                    logger.Information("Overriding attack ID {OldId} with {NewId}", originalAttackId, attackId);
                }
                else if (
                    ManualFixes.ActiveSkillInternalNameOverrides.TryGetValue(originalAttackId, out var fixedId) &&
                    rawWazaIds.Contains("EPalWazaID::" + fixedId)
                )
                {
                    logger.Warning("Attack ID {OldId} is manually reassigned to {NewId}, but that ID is already in use", originalAttackId, fixedId);
                }

                var localizedNames = attackNames.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetValueOrDefault(attackId));

                if (localizedNames.Any(kvp => kvp.Value == null))
                {
                    logger.Warning("Skill {InternalName} missing at least 1 translation, skipping: {Json}", attackId, JsonConvert.SerializeObject(rawAttack));
                    return null;
                }

                var englishName = localizedNames["en"];
                var element = elements.Single(e => e.InternalName == rawAttack.Element.Replace("EPalElementType::", ""));

                return new ActiveSkill(englishName, attackId, element)
                {
                    CooldownSeconds = rawAttack.CoolTime,
                    CanInherit = !rawAttack.IgnoreRandomInherit,
                    HasSkillFruit = allItems.Any(i => i.TypeB == "EPalItemTypeB::ConsumeWazaMachine" && i.WazaID == rawAttack.WazaType),
                    Power = rawAttack.Power,
                    LocalizedNames = localizedNames,
                };
            }).SkipNull().ToList();
        }

        private static void AssignActiveSkillLevels(
            List<Pal> pals,
            List<ActiveSkill> attacks,
            List<UActiveSkill> rawAttacks,
            IEnumerable<UActiveSkillLevel> levels
        )
        {
            var attackInternalNames = attacks.Select(attack => attack.InternalName).ToHashSet(StringComparer.Ordinal);
            var rawWazaIds = rawAttacks
                .Select(attack => attack.WazaType)
                .ToHashSet();
            var levelsByPalId = levels
                .GroupBy(level => level.PalId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var pal in pals)
            {
                if (!levelsByPalId.TryGetValue(pal.InternalName, out var palLevels))
                {
                    logger.Warning("No attacks found for Pal {InternalName}", pal.InternalName);
                    pal.InternalAttackLeveling = [];
                    continue;
                }

                pal.InternalAttackLeveling = palLevels
                    .Select(level => new PalInternalAttackLevel
                    {
                        AttackInternalId = ActiveSkillInternalName(level.WazaID, rawWazaIds),
                        Level = level.Level,
                    })
                    .Where(entry => attackInternalNames.Contains(entry.AttackInternalId))
                    .OrderBy(entry => entry.Level)
                    .ToList();
            }
        }

        public static Dictionary<Pal, PartnerSkill> BuildPartnerSkills(List<RawPartnerSkill> rawPartnerSkills, List<Pal> pals)
        {
            var res = new Dictionary<Pal, PartnerSkill>();

            foreach (var s in rawPartnerSkills)
            {
                var pal = pals.FirstOrDefault(p => p.InternalName.Equals(s.BPClassName, StringComparison.InvariantCultureIgnoreCase));
                if (pal == null)
                {
                    logger.Warning("Unrecognized pal {name} for partner skill, skipping", s.BPClassName);
                    continue;
                }

                res.Add(pal, new PartnerSkill()
                {
                    RankEffects = s.RankEffects.Select(e => new PartnerSkill.RankEffect() { PassiveInternalNames = e.PassiveSkillInternalNames }).ToList()
                });
            }

            return res;
        }

        private static List<PalElement> BuildElements(Dictionary<string, Dictionary<string, string>> elementNames)
        {
            var elementTypes = elementNames.SelectMany(kvp => kvp.Value.Keys).Distinct().ToList();

            return elementTypes.Select(internalName =>
            {
                var localizedNames = elementNames.ToDictionary(kvp => kvp.Key, kvp => kvp.Value[internalName]);
                var englishName = localizedNames["en"];

                return new PalElement(englishName, internalName) { LocalizedNames = localizedNames };
            }).ToList();
        }

        private static List<BreedingResult> BuildAllBreedingResults(List<Pal> pals, PalBreedingCalculator breedingCalc)
        {
            logger.Information("Building the complete list of breeding results...");

            var res = pals
                .SelectMany(parent1 => pals.Select(parent2 => (parent1, parent2)))
                .Select(pair => pair.parent1.GetHashCode() > pair.parent2.GetHashCode() ? (pair.parent1, pair.parent2) : (pair.parent2, pair.parent1))
                .Distinct()
                // (the `.Child` calc takes a while, parallelize that part)
                .ToList()
                .BatchedAsParallel()
                .SelectMany(batch =>
                    batch
                        .SelectMany(pair => new[] {
                            (
                                new GenderedPal(Pal: pair.Item1, Gender: PalGender.FEMALE),
                                new GenderedPal(Pal: pair.Item2, Gender: PalGender.MALE)
                            ),
                            (
                                new GenderedPal(Pal: pair.Item1, Gender: PalGender.MALE),
                                new GenderedPal(Pal: pair.Item2, Gender: PalGender.FEMALE)
                            )
                        })
                        // get the results of breeding with swapped genders (for results where the child is determined by parent genders)
                        .Select(p => new BreedingResult
                        {
                            Parent1 = p.Item1,
                            Parent2 = p.Item2,
                            Child = breedingCalc.Child(p.Item1, p.Item2)
                        })
                        .ToList()
                )
                // (join all threads)
                .ToList()
                // simplify cases where the child is the same regardless of gender
                .GroupBy(br => br.Child)
                .SelectMany(cg =>
                    cg
                        .GroupBy(br => (br.Parent1.Pal, br.Parent2.Pal))
                        .SelectMany(g =>
                        {
                            var results = g.ToList();
                            if (results.Count == 1) return results;

                            return
                            [
                                new BreedingResult()
                                {
                                    Parent1 = new GenderedPal(Pal: results.First().Parent1.Pal, Gender: PalGender.WILDCARD),
                                    Parent2 = new GenderedPal(Pal: results.First().Parent2.Pal, Gender: PalGender.WILDCARD),
                                    Child = results.First().Child
                                }
                            ];
                        })
                )
                // sort to ensure consistent order across runs, makes it easier to see if there are real changes
                .OrderBy(br => br.Child.InternalName)
                .ThenBy(br => br.Parent1.Pal.InternalName)
                .ThenBy(br => br.Parent2.Pal.InternalName)
                .ThenBy(br => br.Parent1.Gender)
                .ThenBy(br => br.Parent2.Gender)
                .ToList();

            return res;
        }

        private static void ExportImage(UTexture2D tex, string path, int width, int height, SKEncodedImageFormat format, int quality = 100)
        {
            using var rawData = tex.Decode(ETexturePlatform.DesktopMobile).ToSkBitmap();
            var resized = rawData.Resize(new SKSizeI() { Width = width, Height = height }, SKFilterQuality.High);
            var encoded = resized.Encode(format, quality);

            using (var o = new FileStream(path, FileMode.Create))
                encoded.SaveTo(o);
        }

        private static void ExportImage(UTexture2D tex, string path, SKEncodedImageFormat format, int quality = 100)
        {
            using var rawData = tex.Decode(ETexturePlatform.DesktopMobile).ToSkBitmap();
            var encoded = rawData.Encode(format, 100);
            using (var o = new FileStream(path, FileMode.Create))
                encoded.SaveTo(o);
        }

        private static void ExportElementIcons(Dictionary<string, UTexture2D> elementIcons)
        {
            // AssetFileName => ExportedFileName
            var fileNames = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "T_Icon_element_s_00.uasset", "Normal.png" },
                { "T_Icon_element_s_01.uasset", "Fire.png" },
                { "T_Icon_element_s_02.uasset", "Water.png" },
                { "T_Icon_element_s_03.uasset", "Electricity.png" },
                { "T_Icon_element_s_04.uasset", "Leaf.png" },
                { "T_Icon_element_s_05.uasset", "Dark.png" },
                { "T_Icon_element_s_06.uasset", "Dragon.png" },
                { "T_Icon_element_s_07.uasset", "Earth.png" },
                { "T_Icon_element_s_08.uasset", "Ice.png" },
            };

            foreach (var icon in elementIcons)
                ExportImage(icon.Value, $"../PalCalc.UI/Resources/Elements/{fileNames[icon.Key]}", SKEncodedImageFormat.Png);
        }

        private static void ExportSkillElementIcons(Dictionary<string, UTexture2D> skillElementIcons)
        {
            var fileNames = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "T_prt_pal_skill_base_element_00.uasset", "Normal.png" },
                { "T_prt_pal_skill_base_element_01.uasset", "Fire.png" },
                { "T_prt_pal_skill_base_element_02.uasset", "Water.png" },
                { "T_prt_pal_skill_base_element_03.uasset", "Electricity.png" },
                { "T_prt_pal_skill_base_element_04.uasset", "Leaf.png" },
                { "T_prt_pal_skill_base_element_05.uasset", "Dark.png" },
                { "T_prt_pal_skill_base_element_06.uasset", "Dragon.png" },
                { "T_prt_pal_skill_base_element_07.uasset", "Earth.png" },
                { "T_prt_pal_skill_base_element_08.uasset", "Ice.png" },
            };

            foreach (var icon in skillElementIcons)
                ExportImage(icon.Value, $"../PalCalc.UI/Resources/SkillElements/{fileNames[icon.Key]}", SKEncodedImageFormat.Png);
        }

        private static void ExportSkillRankIcons(Dictionary<string, UTexture2D> skillRankIcons)
        {
            /*
            note:
            we get T_icon_skillstatus_rank_arrow_00 through 04, but paldb.cc only uses 01 through 03 (positive rank)
            and just flips them for the negative ranks
            
            not sure why they skip 00, and 04 just seems unused (three-bar with "+" icon)
            */

            var ciSkillRankIcons = new Dictionary<string, UTexture2D>(skillRankIcons, StringComparer.InvariantCultureIgnoreCase);

            void ExportRankIcon(UTexture2D tex, string iconName, Func<SKBitmap, SKBitmap> transform)
            {
                using var rawData = tex.Decode(ETexturePlatform.DesktopMobile).ToSkBitmap();
                var modified = transform(rawData);
                var encoded = modified.Encode(SKEncodedImageFormat.Png, 100);
                using (var o = new FileStream($"../PalCalc.UI/Resources/TraitRank/{iconName}", FileMode.Create))
                    encoded.SaveTo(o);
            }

            SKBitmap NoOp(SKBitmap b) => b;
            SKBitmap Flip(SKBitmap b)
            {
                // https://github.com/mono/SkiaSharp/discussions/2978#discussioncomment-10491028

                // Create a bitmap (to return)
                var flipped = new SKBitmap(b.Width, b.Height, b.Info.ColorType, b.Info.AlphaType);

                // Create a canvas to draw into the bitmap
                using var canvas = new SKCanvas(flipped);
                canvas.Clear(new SKColor(0, 0, 0, 0));

                // Set a transform matrix which moves the bitmap to the right,
                // and then "scales" it by -1, which just flips the pixels
                // horizontally
                canvas.Translate(0, b.Height);
                canvas.Scale(1, -1);
                canvas.DrawBitmap(b, 0, 0);
                return flipped;
            }

            ExportRankIcon(ciSkillRankIcons["T_icon_skillstatus_rank_arrow_01.uasset"], "Passive_Positive_1_icon.png", NoOp);
            ExportRankIcon(ciSkillRankIcons["T_icon_skillstatus_rank_arrow_01.uasset"], "Passive_Negative_1_icon.png", Flip);

            ExportRankIcon(ciSkillRankIcons["T_icon_skillstatus_rank_arrow_02.uasset"], "Passive_Positive_2_icon.png", NoOp);
            ExportRankIcon(ciSkillRankIcons["T_icon_skillstatus_rank_arrow_02.uasset"], "Passive_Negative_2_icon.png", Flip);

            ExportRankIcon(ciSkillRankIcons["T_icon_skillstatus_rank_arrow_03.uasset"], "Passive_Positive_3_icon.png", NoOp);
            ExportRankIcon(ciSkillRankIcons["T_icon_skillstatus_rank_arrow_03.uasset"], "Passive_Negative_3_icon.png", Flip);

            ExportRankIcon(ciSkillRankIcons["T_icon_skillstatus_rank_arrow_04.uasset"], "Passive_Positive_4_icon.png", NoOp);
            ExportRankIcon(ciSkillRankIcons["T_icon_skillstatus_rank_arrow_05.uasset"], "Passive_Positive_5_icon.png", NoOp);
        }

        public static void ExportWorkSuitabilityIcons(Dictionary<string, UTexture2D> workIcons)
        {
            logger.Information("Exporting work suitability icons...");

            var fileNames = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "T_icon_palwork_00.uasset", "Kindling.png" },
                { "T_icon_palwork_01.uasset", "Watering.png" },
                { "T_icon_palwork_02.uasset", "Planting.png" },
                { "T_icon_palwork_03.uasset", "ElectricityGeneration.png" },
                { "T_icon_palwork_04.uasset", "Handiwork.png" },
                { "T_icon_palwork_05.uasset", "Gathering.png" },
                { "T_icon_palwork_06.uasset", "Lumbering.png" },
                { "T_icon_palwork_07.uasset", "Mining.png" },
                { "T_icon_palwork_08.uasset", "MedicineProduction.png" },
                //{ "T_icon_palwork_09.uasset", "" },
                { "T_icon_palwork_10.uasset", "Cooling.png" },
                { "T_icon_palwork_11.uasset", "Transporting.png" },
                { "T_icon_palwork_12.uasset", "Farming.png" },
                //{ "T_icon_palwork_13.uasset", "" },
            };

            foreach (var mapping in fileNames)
                ExportImage(workIcons[mapping.Key], $"../PalCalc.UI/Resources/{mapping.Value}", SKEncodedImageFormat.Png);
        }

        public static void ExportStatusIcons(Dictionary<string, UTexture2D> statusIcons)
        {
            logger.Information("Exporting status icons...");

            var fileNames = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "T_icon_status_00.uasset", "Health.png" },
                //{ "T_icon_status_01.uasset", "" },
                { "T_icon_status_02.uasset", "Attack.png" },
                { "T_icon_status_03.uasset", "Defense.png" },
                { "T_icon_status_04.uasset", "Weight.png" },
                { "T_icon_status_05.uasset", "WorkSpeed.png" },
                //{ "T_icon_status_06.uasset", "" },
                //{ "T_icon_status_07.uasset", "" },
            };

            foreach (var mapping in fileNames)
                ExportImage(statusIcons[mapping.Key], $"../PalCalc.UI/Resources/{mapping.Value}", SKEncodedImageFormat.Png);
        }

        public static void ExportMiscIcons(OtherIcons icons)
        {
            logger.Information("Exporting misc. icons...");

            ExportImage(icons.DayIcon, "../PalCalc.UI/Resources/Day.png", SKEncodedImageFormat.Png);
            ExportImage(icons.NightIcon, "../PalCalc.UI/Resources/Night.png", SKEncodedImageFormat.Png);
            ExportImage(icons.FoodIconOff, "../PalCalc.UI/Resources/FoodOff.png", SKEncodedImageFormat.Png);
            ExportImage(icons.FoodIconOn, "../PalCalc.UI/Resources/FoodOn.png", SKEncodedImageFormat.Png);
            ExportImage(icons.TimerIcon, "../PalCalc.UI/Resources/Timer.png", SKEncodedImageFormat.Png);

            ExportImage(icons.SpecialCakeIcon, "../PalCalc.UI/Resources/Cake_Special.png", 64, 64, SKEncodedImageFormat.Png);

            ExportImage(icons.DungeonIconSmall, "../PalCalc.UI/Resources/DungeonSmall.png", SKEncodedImageFormat.Png);

            ExportImage(icons.SurgeryTableIcon, "../PalCalc.UI/Resources/SurgeryTable.png", 256, 256, SKEncodedImageFormat.Png);

            ExportImage(icons.PassiveSkillSpecialBackground, "../PalCalc.UI/Resources/PassiveSpecialBackground.png", SKEncodedImageFormat.Png);
        }

        private static void ExportPalIcons(List<Pal> pals, Dictionary<string, UTexture2D> palIcons, int iconSize)
        {
            logger.Information("Exporting pal icons...");
            foreach (var icon in palIcons)
            {
                string palName;

                var internalName = icon.Key;
                // ("Human" icon is used as a placeholder for unknown pals in palcalc)
                if (internalName == "Human")
                {
                    palName = internalName;
                }
                else
                {
                    var pal = pals.SingleOrDefault(p => p.InternalName.ToLower() == internalName.ToLower());
                    if (pal == null)
                    {
                        logger.Warning("Unknown pal {PalName}, skipping icon", internalName);
                        continue;
                    }
                    palName = pal.Name;
                }

                var img = icon.Value;
                ExportImage(icon.Value, "../PalCalc.UI/Resources/Pals/" + palName + ".png", iconSize, iconSize, SKEncodedImageFormat.Png);
            }
        }

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            OodleHelper.DownloadOodleDll();
            OodleHelper.Initialize();

            var provider = new DefaultFileProvider(PalworldDirPath, SearchOption.AllDirectories, true, new VersionContainer(EGame.GAME_UE5_1));
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(MappingsPath);

            provider.Initialize();
            provider.Mount();
            provider.LoadVirtualPaths();

            logger.Information("Reading localizations, pals, and passives...");
            var localizations = LocalizationsReader.FetchLocalizations(provider);

            logger.Information("Found language codes: {codes}", localizations.Select(l => l.LanguageCode));

            var characterRows = PalReader.ReadPals(provider);
            var wildPalLevels = PalSpawnerReader.ReadWildLevelRanges(provider);

            var pals = BuildPals(
                characterRows,
                provider,
                wildPalLevels,
                palNames: localizations.ToDictionary(l => l.LanguageCode, l => l.ReadPalNames(provider))
            );

            var rawHumans = HumanReader.ReadHumans(provider);
            var humans = rawHumans.Select(h => new Human(h.InternalName)).ToList();

            // (passives in game data may have "IsPal" or similar flags, which affect whether those passives can be
            //  obtained randomly, but this flag isn't set for passives which are pal-specific, e.g. Legend.)
            var rawPassiveSkills = PassiveSkillsReader.ReadPassiveSkills(
                provider,
                extraPassives: pals.SelectMany(p => p.GuaranteedPassivesInternalIds).Distinct().ToList()
            );

            var rawSurgeryPassives = SurgeryTableReader.ReadSurgeryPassives(provider);

            var passives = BuildPassiveSkills(
                rawPassiveSkills,
                rawSurgeryPassives,
                commonTexts: localizations.ToDictionary(l => l.LanguageCode, l => l.ReadCommonText(provider)),
                skillNames: localizations.ToDictionary(l => l.LanguageCode, l => l.ReadSkillNames(provider)),
                skillDescriptions: localizations.ToDictionary(l => l.LanguageCode, l => l.ReadSkillDescriptions(provider))
            );

            var rawPartnerSkills = PartnerSkillReader.ReadPartnerSkills(provider);
            var partnerSkills = BuildPartnerSkills(rawPartnerSkills, pals);

            foreach (var kvp in partnerSkills)
                kvp.Key.PartnerSkill = kvp.Value;

            var elements = BuildElements(localizations.ToDictionary(l => l.LanguageCode, l => l.ReadElementNames(provider)));

            var rawAttackData = ActiveSkillReader.ReadActiveSkills(provider);
            var rawItems = ItemReader.ReadItems(provider);

            var attacks = BuildActiveSkills(
                rawAttackData.Skills,
                elements,
                rawItems,
                localizations.ToDictionary(l => l.LanguageCode, l => l.ReadAttackNames(provider))
            );
            AssignActiveSkillLevels(
                pals,
                attacks,
                rawAttackData.Skills,
                rawAttackData.Levels
            );

            var uniqueBreedingCombos = UniqueBreedComboReader.ReadUniqueBreedCombos(provider);
            var characterRowsById = characterRows.ToDictionary(p => p.InternalName, StringComparer.OrdinalIgnoreCase);
            var breedingCalc = new PalBreedingCalculator(pals, characterRowsById, uniqueBreedingCombos);

            var db = PalDB.MakeEmptyUnsafe("v28");

            var dups = pals.GroupBy(p => p.Id).Where(g => g.Count() > 1).ToList();
            db.PalsById = pals.ToDictionary(p => p.Id);
            db.Humans = humans;
            db.PassiveSkills = passives;
            db.ActiveSkills = attacks;
            db.Elements = elements;

            db.BreedingGenderProbability = pals.ToDictionary(p => p, p =>
            {
                var maleProbability = characterRowsById[p.InternalName].MaleProbability / 100.0f;
                return new Dictionary<PalGender, float>
                {
                    { PalGender.MALE, maleProbability },
                    { PalGender.FEMALE, 1 - maleProbability }
                };
            });

            var rawGameSettings = GameSettingReader.ReadGameSettings(provider);
            db.BreedingMechanics = new BreedingMechanics(
                // this array has special logic associated with it, see `../PALWORLD-BREEDING-MECHANICS.md` - store as-is
                ivInheritanceWeights: rawGameSettings.TalentInheritNum.ToArray(),
                // this array has 4 entries, and we empirically know it's possible to inherit 4 passives, so starting index must be 1
                passiveInheritanceWeights: Enumerable.Range(1, 4).ToDictionary(n => n, n => rawGameSettings.PassiveInheritNum[n - 1]),
                // this array has 4 entries, and we empirically know it's possible to get 0 random passives, so starting index must be 0
                passiveRandomWeights: Enumerable.Range(0, 4).ToDictionary(n => n, n => rawGameSettings.PassiveRandomAddNum[n])
            );

            File.WriteAllText("../PalCalc.Model/db.json", db.ToJson());

            var breedingdb = new PalBreedingDB(db);
            breedingdb.Breeding = BuildAllBreedingResults(pals, breedingCalc);
            breedingdb.MinBreedingSteps = BreedingDistanceMap.CalcMinDistances(db, breedingdb);

            File.WriteAllText("../PalCalc.Model/breeding.json", breedingdb.ToJson());

            logger.Information("Scraping pal icons");
            ExportPalIcons(
                pals: pals,
                palIcons: PalIconMappingsReader.ReadPalIconMappings(provider),
                iconSize: 100
            );

            logger.Information("Scraping misc. icons");
            var otherIcons = OtherIconsReader.ReadIcons(provider);
            ExportElementIcons(otherIcons.ElementIcons);
            ExportSkillElementIcons(otherIcons.SkillElementIcons);
            ExportSkillRankIcons(otherIcons.SkillRankIcons);
            ExportWorkSuitabilityIcons(otherIcons.WorkSuitabilityIcons);
            ExportStatusIcons(otherIcons.StatusIcons);
            ExportMiscIcons(otherIcons);

            logger.Information("Scraping map data");
            var mapInfo = MapReader.ReadMapInfo(provider);

            if (mapInfo != null)
            {
                // MapCoord assumes the exported FirstRegion texture covers the MainMap landscape bounds.
                using var rawData = mapInfo.MapTexture.Decode(ETexturePlatform.DesktopMobile).ToSkBitmap();
                var resized = rawData.Resize(new SKSizeI() { Width = 8192, Height = 8192 }, SKFilterQuality.High);

                var encoded = resized.Encode(SKEncodedImageFormat.Jpeg, 70);

                using (var o = new FileStream("../PalCalc.UI/Resources/Map.jpeg", FileMode.Create))
                    encoded.SaveTo(o);
            }

            CSVExport.Write(
                outDir: "out-csv",
                db: db,
                humans: rawHumans,
                humanLocalizations: localizations.ToDictionary(l => l.LanguageCode, l => l.ReadHumanNames(provider))
            );
        }


    }
}
