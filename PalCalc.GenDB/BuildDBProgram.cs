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
        static string MappingsPath = @"C:\Users\algor\Desktop\Mappings.usmap";

        private static List<Pal> BuildPals(List<UPal> rawPals, Dictionary<string, (int, int)> wildPalLevels, Dictionary<string, Dictionary<string, string>> palNames)
        {
            return rawPals.Select(rawPal =>
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

            var uniqueBreedingCombos = UniqueBreedComboReader.ReadUniqueBreedCombos(provider);
            var breedingCalc = new PalBreedingCalculator(
                pals,
                uniqueBreedingCombos.Select(c => BuildUniqueBreedingCombo(pals, c)).SkipNull().ToList()
            );

            var db = PalDB.MakeEmptyUnsafe("v13");

            db.PalsById = pals.ToDictionary(p => p.Id);
            db.PassiveSkills = passives;
            db.Breeding = BuildAllBreedingResults(pals, breedingCalc);
            db.MinBreedingSteps = BreedingDistanceMap.CalcMinDistances(db);

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

            logger.Information("Scraping pal icons");
            ExportPalIcons(
                pals: pals,
                palIcons: PalIconMappingsReader.ReadPalIconMappings(provider),
                iconSize: 100
            );

            logger.Information("Scraping map data");
            var mapInfo = MapReader.ReadMapInfo(provider);

            if (mapInfo != null)
            {
                var rawData = mapInfo.MapTexture.Decode(ETexturePlatform.DesktopMobile);
                var resized = rawData.Resize(new SKSizeI() { Width = 2048, Height = 2048 }, SKFilterQuality.High);

                // this image seems to have some extra margin with a vignette? this margin messes with coord calcs
                // crop it just enough to remove that vignette
                // (would prefer to properly read this info from game files but I can't find anything for it)
                //var marginPercent = 0.05f;
                //var resizedPM = new SKPixmap(resized.Info, resized.GetPixels());
                //var cropped = resizedPM.ExtractSubset(
                //    new SKRectI()
                //    {
                //        Left = (int)(resized.Width * marginPercent),
                //        Top = (int)(resized.Height * marginPercent),
                //        Right = (int)(resized.Width * (1 - marginPercent)),
                //        Bottom = (int)(resized.Height * (1 - marginPercent))
                //    }
                //);
                //
                // (... BUT the general "Map Coord" -> "Image Position" calc is incomplete in general, and cropping
                //  makes the issue worse, so leaving it uncropped for now)

                var encoded = resized.Encode(SKEncodedImageFormat.Jpeg, 80);

                using (var o = new FileStream("../PalCalc.UI/Resources/Map.jpeg", FileMode.Create))
                        encoded.SaveTo(o);

                // Dimensions should be reflected in `PalCalc.Model.GameConstants`
                logger.Information("Map dimensions:\nMin: {0} | {1}\nMax: {2} | {3}", mapInfo.MapMinX, mapInfo.MapMaxX, mapInfo.MapMinY, mapInfo.MapMaxY);
            }
        }


    }
}
