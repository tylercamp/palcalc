using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion;
using CUE4Parse_Conversion.Textures;
using PalCalc.GenDB.GameDataReaders;
using PalCalc.Model;
using Serilog;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

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
        static string MappingsPath = @"C:\Users\algor\OneDrive\Desktop\Mappings.usmap";

        private static List<Pal> BuildPals(List<UPal> rawPals, Dictionary<string, (int, int)> wildPalLevels, Dictionary<string, Dictionary<string, string>> palNames)
        {
            return rawPals
                .Where(p => !p.InternalName.StartsWith("SUMMON_", StringComparison.InvariantCultureIgnoreCase))
                .Where(p => !p.InternalName.EndsWith("_Oilrig", StringComparison.InvariantCultureIgnoreCase))
                .Select(rawPal =>
                {
                    var localizedNames = palNames.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetOneOf(rawPal.InternalName, rawPal.AlternativeInternalName));
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
                        InternalIndex = rawPal.InternalIndex,
                        InternalName = rawPal.InternalName,
                        Name = englishName,
                        LocalizedNames = localizedNames,
                        MinWildLevel = minWildLevel,
                        MaxWildLevel = maxWildLevel,

                        GuaranteedPassivesInternalIds = rawPal.GuaranteedPassives,
                    };
                }).ToList();
        }

        private static List<PassiveSkill> BuildPassiveSkills(List<UPassiveSkill> rawPassiveSkills, Dictionary<string, Dictionary<string, string>> skillNames)
        {
            return rawPassiveSkills.Select(rawPassive =>
            {
                var localizedNames = skillNames.ToDictionary(kvp => kvp.Key, kvp => kvp.Value[rawPassive.InternalName]);
                var englishName = localizedNames["en"];

                return new PassiveSkill(englishName, rawPassive.InternalName, rawPassive.Rank)
                {
                    LocalizedNames = localizedNames
                };
            }).ToList();
        }

        private static List<ActiveSkill> BuildActiveSkills(List<UActiveSkill> rawActiveSkills, List<PalElement> elements, Dictionary<string, Dictionary<string, string>> attackNames)
        {
            return rawActiveSkills.Where(s => !s.DisabledData).Select(rawAttack =>
            {
                var attackId = rawAttack.WazaType.Replace("EPalWazaID::", "");
                var localizedNames = attackNames.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetValueOrDefault(attackId));

                if (localizedNames.Any(kvp => kvp.Value == null))
                {
                    logger.Warning("Skill {InternalName} missing at least 1 translation, skipping", attackId);
                    return null;
                }

                var englishName = localizedNames["en"];
                var element = elements.Single(e => e.InternalName == rawAttack.Element.Replace("EPalElementType::", ""));

                return new ActiveSkill(englishName, attackId, element)
                {
                    CooldownSeconds = rawAttack.CoolTime,
                    CanInherit = !rawAttack.IgnoreRandomInherit,
                    Power = rawAttack.Power,
                    LocalizedNames = localizedNames
                };
            }).SkipNull().ToList();
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
                .BatchedForParallel()
                .AsParallel()
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

            var provider = new DefaultFileProvider(PalworldDirPath, SearchOption.AllDirectories, true, new VersionContainer(EGame.GAME_UE5_1));
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(MappingsPath);

            provider.Initialize();
            provider.Mount();
            provider.LoadVirtualPaths();
            provider.LoadLocalization();

            logger.Information("Reading localizations, pals, and passives...");
            var localizations = LocalizationsReader.FetchLocalizations(provider);

            var rawPals = PalReader.ReadPals(provider);
            var wildPalLevels = PalSpawnerReader.ReadWildLevelRanges(provider);

            var pals = BuildPals(
                rawPals,
                wildPalLevels,
                palNames: localizations.ToDictionary(l => l.LanguageCode, l => l.ReadPalNames(provider))
            );

            // (passives in game data may have "IsPal" or similar flags, which affect whether those passives can be
            //  obtained randomly, but this flag isn't set for passives which are pal-specific, e.g. Legend.)
            var rawPassiveSkills = PassiveSkillsReader.ReadPassiveSkills(
                provider,
                extraPassives: pals.SelectMany(p => p.GuaranteedPassivesInternalIds).Distinct().ToList()
            );

            var passives = BuildPassiveSkills(
                rawPassiveSkills,
                skillNames: localizations.ToDictionary(l => l.LanguageCode, l => l.ReadSkillNames(provider))
            );

            var elements = BuildElements(localizations.ToDictionary(l => l.LanguageCode, l => l.ReadElementNames(provider)));

            var rawAttacks = ActiveSkillReader.ReadActiveSkills(provider);

            var attacks = BuildActiveSkills(
                rawAttacks,
                elements,
                localizations.ToDictionary(l => l.LanguageCode, l => l.ReadAttackNames(provider))
            );

            var uniqueBreedingCombos = UniqueBreedComboReader.ReadUniqueBreedCombos(provider);
            var breedingCalc = new PalBreedingCalculator(
                pals,
                uniqueBreedingCombos.Select(c => BuildUniqueBreedingCombo(pals, c)).SkipNull().ToList()
            );

            var db = PalDB.MakeEmptyUnsafe("v14");

            db.PalsById = pals.ToDictionary(p => p.Id);
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
        }


    }
}
