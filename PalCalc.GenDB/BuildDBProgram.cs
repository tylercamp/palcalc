using CUE4Parse.Compression;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Textures;
using Newtonsoft.Json;
using PalCalc.GenDB.GameDataReaders;
using PalCalc.Model;
using Serilog;
using SkiaSharp;
using System.Collections.Immutable;
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
 */

namespace PalCalc.GenDB
{
    static class BuildDBProgram
    {
        private static ILogger logger = Log.ForContext(typeof(BuildDBProgram));

        // This is all HEAVILY dependent on having the right Mappings.usmap file for the Palworld version!
        //
        // (should be a folder containing "Pal-Windows.pak")
        static string PalworldDirPath = @"C:\Program Files (x86)\Steam\steamapps\common\Palworld\Pal\Content\Paks";
        static string MappingsPath = @"C:\Users\algor\Desktop\Mappings.usmap";

        private static List<Pal> BuildPals(List<UPal> rawPals, Dictionary<string, (int, int)> wildPalLevels, Dictionary<string, Dictionary<string, string>> palNames)
        {
            var allPals = rawPals
                .Where(p => !p.InternalName.StartsWith("SUMMON_", StringComparison.InvariantCultureIgnoreCase))
                .Where(p => !p.InternalName.EndsWith("_Oilrig", StringComparison.InvariantCultureIgnoreCase))
                .Select(rawPal =>
                {
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

                    return new Pal()
                    {
                        Id = new PalId()
                        {
                            PalDexNo = rawPal.PalDexNum,
                            IsVariant = rawPal.PalDexNumSuffix != null && rawPal.PalDexNumSuffix.Length > 0,
                        },
                        BreedingPower = rawPal.BreedingPower,
                        Price = (int)rawPal.Price,
                        Rarity = rawPal.Rarity,
                        InternalIndex = rawPal.InternalIndex,
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
            };

            foreach (var kvp in formatArgs)
                description = description.Replace($"{{{kvp.Key}}}", kvp.Value);

            if (description.Contains("{"))
            {
                logger.Warning("Description contains leftover format params: {description}", description);
            }

            description = Regex.Replace(description, @"<(.+?)\s+id=\|(.+?)\|/>", match =>
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
                    (Strip(rawPassive.EffectType3), Strip(rawPassive.TargetType3), rawPassive.EffectValue3)
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
                    SurgeryCost = surgeryData?.Price ?? 0,
                    // ("no required item" is indicated as a value of "None", but handle nullability just in case that changes)
                    SurgeryRequiredItem = (surgeryData?.RequireItemId ?? "None") == "None" ? null : surgeryData.RequireItemId,
                };
            }).SkipNull().ToList();
        }

        private static List<ActiveSkill> BuildActiveSkills(List<UActiveSkill> rawActiveSkills, List<PalElement> elements, List<UItem> allItems, Dictionary<string, Dictionary<string, string>> attackNames)
        {
            return rawActiveSkills.Where(s => !s.DisabledData).Select(rawAttack =>
            {
                var attackId = rawAttack.WazaType.Replace("EPalWazaID::", "");

                if (ManualFixes.ActiveSkillInternalNameOverrides.ContainsKey(attackId))
                {
                    var fixedId = ManualFixes.ActiveSkillInternalNameOverrides[attackId];
                    if (rawActiveSkills.Any(s => s.WazaType == "EPalWazaID::" +  fixedId))
                    {
                        logger.Warning("Attack ID {OldId} is manually reassigned to {NewId}, but that ID is already in use", attackId, fixedId);
                    }
                    else
                    {
                        logger.Information("Overriding attack ID {OldId} with {NewId}", attackId, fixedId);
                        attackId = fixedId;
                    }
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

        private static UniqueBreedingCombo BuildUniqueBreedingCombo(List<Pal> pals, ((string, PalGender?), (string, PalGender?), string) combo)
        {
            var ((parent1Id, parent1Gender), (parent2Id, parent2Gender), childId) = combo;

            var parent1 = pals.SingleOrDefault(p => p.InternalName.Equals(parent1Id, StringComparison.InvariantCultureIgnoreCase));
            var parent2 = pals.SingleOrDefault(p => p.InternalName.Equals(parent2Id, StringComparison.InvariantCultureIgnoreCase));
            var child = pals.SingleOrDefault(p => p.InternalName.Equals(combo.Item3, StringComparison.InvariantCultureIgnoreCase));

            // (game data seems to have combos for unreleased pals; pal data scraper here skips pals with paldex no. -1)

            List<string> errors = [];
            if (parent1 == null)
                errors.Add($"Unrecognized parent1 {parent1Id}");
            if (parent2 == null)
                errors.Add($"Unrecognized parent2 {parent2Id}");
            if (child == null)
                errors.Add($"Unrecognized child {childId}");

            if (parent1 == null || parent2 == null || child == null)
            {
                logger.Warning("{Errors} - skipping", string.Join(", ", errors));
                return null;
            }

            return new UniqueBreedingCombo()
            {
                Parent1 = parent1,
                Parent1Gender = parent1Gender,

                Parent2 = parent2,
                Parent2Gender = parent2Gender,

                Child = child
            };
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
                                new GenderedPal() { Pal = pair.Item1, Gender = PalGender.FEMALE },
                                new GenderedPal() { Pal = pair.Item2, Gender = PalGender.MALE }
                            ),
                            (
                                new GenderedPal() { Pal = pair.Item1, Gender = PalGender.MALE },
                                new GenderedPal() { Pal = pair.Item2, Gender = PalGender.FEMALE }
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
                                    Parent1 = new GenderedPal()
                                    {
                                        Pal = results.First().Parent1.Pal,
                                        Gender = PalGender.WILDCARD
                                    },
                                    Parent2 = new GenderedPal()
                                    {
                                        Pal = results.First().Parent2.Pal,
                                        Gender = PalGender.WILDCARD
                                    },
                                    Child = results.First().Child
                                }
                            ];
                        })
                )
                .ToList();

            return res;
        }

        private static void ExportImage(UTexture2D tex, string path, int width, int height, SKEncodedImageFormat format, int quality = 100)
        {
            var rawData = tex.Decode(ETexturePlatform.DesktopMobile);
            var resized = rawData.Resize(new SKSizeI() { Width = width, Height = height }, SKFilterQuality.High);
            var encoded = resized.Encode(format, quality);

            using (var o = new FileStream(path, FileMode.Create))
                encoded.SaveTo(o);
        }

        private static void ExportImage(UTexture2D tex, string path, SKEncodedImageFormat format, int quality = 100)
        {
            var rawData = tex.Decode(ETexturePlatform.DesktopMobile);
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
                var rawData = tex.Decode(ETexturePlatform.DesktopMobile);
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

            ExportImage(icons.DungeonIconSmall, "../PalCalc.UI/Resources/DungeonSmall.png", SKEncodedImageFormat.Png);

            ExportImage(icons.SurgeryTableIcon, "../PalCalc.UI/Resources/SurgeryTable.png", 256, 256, SKEncodedImageFormat.Png);
        }

        private static void ExportPalIcons(List<Pal> pals, Dictionary<string, UTexture2D> palIcons, int iconSize)
        {
            logger.Information("Exporting pal icons...");
            foreach (var icon in palIcons)
            {
                string palName;

                var internalName = icon.Key;
                // ("Human" icon is used as a placeholder for unknown pals in pal calc)
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
            OodleHelper.Initialize(OodleHelper.OODLE_DLL_NAME);

            var provider = new DefaultFileProvider(PalworldDirPath, SearchOption.AllDirectories, true, new VersionContainer(EGame.GAME_UE5_1));
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(MappingsPath);

            provider.Initialize();
            provider.Mount();
            provider.LoadVirtualPaths();

            logger.Information("Reading localizations, pals, and passives...");
            var localizations = LocalizationsReader.FetchLocalizations(provider);

            logger.Information("Found language codes: {codes}", localizations.Select(l => l.LanguageCode));

            var rawPals = PalReader.ReadPals(provider);
            var wildPalLevels = PalSpawnerReader.ReadWildLevelRanges(provider);

            var pals = BuildPals(
                rawPals,
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

            var rawAttacks = ActiveSkillReader.ReadActiveSkills(provider);
            var rawItems = ItemReader.ReadItems(provider);

            var attacks = BuildActiveSkills(
                rawAttacks,
                elements,
                rawItems,
                localizations.ToDictionary(l => l.LanguageCode, l => l.ReadAttackNames(provider))
            );

            var uniqueBreedingCombos = UniqueBreedComboReader.ReadUniqueBreedCombos(provider);
            var breedingCalc = new PalBreedingCalculator(
                pals,
                uniqueBreedingCombos.Select(c => BuildUniqueBreedingCombo(pals, c)).SkipNull().ToList()
            );

            var db = PalDB.MakeEmptyUnsafe("v22");

            db.PalsById = pals.ToDictionary(p => p.Id);
            db.Humans = humans;
            db.PassiveSkills = passives;
            db.ActiveSkills = attacks;
            db.Elements = elements;

            var genderProbabilities = rawPals.ToDictionary(p => p.InternalName, p => new Dictionary<PalGender, float>()
            {
                { PalGender.MALE, p.MaleProbability / 100.0f },
                { PalGender.FEMALE, 1 - (p.MaleProbability / 100.0f) }
            });
            db.BreedingGenderProbability = pals.ToDictionary(
                p => p,
                p => genderProbabilities[p.InternalName]
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
                var rawData = mapInfo.MapTexture.Decode(ETexturePlatform.DesktopMobile);
                var resized = rawData.Resize(new SKSizeI() { Width = 4096, Height = 4096 }, SKFilterQuality.High);

                var encoded = resized.Encode(SKEncodedImageFormat.Jpeg, 90);

                using (var o = new FileStream("../PalCalc.UI/Resources/Map.jpeg", FileMode.Create))
                        encoded.SaveTo(o);
            }

            // I can't find a reliable method for extracting the info tying the in-game map to game coordinates. Instead I gathered
            // a bunch of samples of world coords (XYZ from game data), map coords (coords on in-game map), and image-pixel coords
            // (x/y coords in the map image itself), and use those samples to solve for the transformation matrices we need.
            //
            // The output should be kept in sync with `GameConstants.WorldToMapMatrix` and `GameConstants.WorldToImageMatrix`
            // (copy/paste and just insert braces and commas to make it compile)
            //
            // Sample data is based on spawners for overworld boss Pals like the Mammorest near the beginning. 
            //
            // Boss pal spawns are at:
            // /Pal/Content/Pal/DataTable/UI/DT_BossSpawnerLoactionData.uasset
            //
            // ---
            //
            // `sampleMapTexSize` is the image size used when the "map pixel coordinates" were gathered
            //
            // I was using a 2048x2048 image at the time, this should only change if the `ImageCoords` in `coord-samples.json` are updated
            // from a new image resolution
            MapTransformSolver.Run("coord-samples.json", sampleMapTexSize: 2048);

            CSVExport.Write(
                outDir: "out-csv",
                db: db,
                humans: rawHumans,
                humanLocalizations: localizations.ToDictionary(l => l.LanguageCode, l => l.ReadHumanNames(provider))
            );
        }


    }
}
