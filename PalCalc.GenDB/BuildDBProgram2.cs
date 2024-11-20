using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Versions;
using PalCalc.GenDB.GameDataReaders;
using PalCalc.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

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
    static class BuildDBProgram2
    {
        // This is all HEAVILY dependent on having the right Mappings.usmap file for the Palworld version!
        static string PalworldDirPath = @"C:\Program Files (x86)\Steam\steamapps\common\Palworld";
        static string MappingsPath = @"C:\Users\algor\Desktop\Mappings.usmap";

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            var provider = new DefaultFileProvider(PalworldDirPath, SearchOption.AllDirectories, true, new VersionContainer(EGame.GAME_UE5_1));
            provider.MappingsContainer = new FileUsmapTypeMappingsProvider(MappingsPath);

            provider.Initialize();
            provider.Mount();
            provider.LoadVirtualPaths();
            provider.LoadLocalization();

            var localizations = LocalizationsReader.FetchLocalizations(provider);
            var palNames = localizations.ToDictionary(l => l.LanguageCode, l => l.ReadPalNames(provider));
            var skillNames = localizations.ToDictionary(l => l.LanguageCode, l => l.ReadSkillNames(provider));

            var rawPals = PalReader.ReadPals(provider);
            var palIcons = PalIconMappingsReader.ReadPalIconMappings(provider);
            var uniqueBreedingCombos = UniqueBreedComboReader.ReadUniqueBreedCombos(provider);
            var wildPalLevels = PalSpawnerReader.ReadWildLevelRanges(provider);

            var pals = rawPals.Select(rawPal =>
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
                    Price = rawPal.Price,
                    InternalIndex = rawPal.InternalIndex,
                    InternalName = rawPal.InternalName,
                    Name = englishName,
                    LocalizedNames = localizedNames,
                    MinWildLevel = minWildLevel,
                    MaxWildLevel = maxWildLevel,

                    GuaranteedPassivesInternalIds = new List<string>()
                    {
                        rawPal.PassiveSkill1,
                        rawPal.PassiveSkill2,
                        rawPal.PassiveSkill3,
                        rawPal.PassiveSkill4
                    }.Where(n => n != null && n.Length > 0).ToList()
                };
            }).ToList();

            // (passives in game data may have "IsPal" or similar flags, which affect whether those passives can be
            //  obtained randomly, but this flag isn't set for passives which are pal-specific, e.g. Legend.)
            var extraPassives = pals.SelectMany(p => p.GuaranteedPassivesInternalIds).ToList();

            var rawPassiveSkills = PassiveSkillsReader.ReadPassiveSkills(provider, extraPassives);
            var passives = rawPassiveSkills.Select(rawPassive =>
            {
                var localizedNames = skillNames.ToDictionary(kvp => kvp.Key, kvp => kvp.Value[rawPassive.InternalName]);
                var englishName = localizedNames["en"];

                return new PassiveSkill(englishName, rawPassive.InternalName, rawPassive.Rank)
                {
                    LocalizedNames = localizedNames
                };
            }).ToList();

            var breedingCalc = new PalBreedingCalculator(
                pals,
                uniqueBreedingCombos.Select(combo =>
                {
                    var parent1 = pals.SingleOrDefault(p => p.InternalName.Equals(combo.Item1.Item1, StringComparison.InvariantCultureIgnoreCase));
                    var parent2 = pals.SingleOrDefault(p => p.InternalName.Equals(combo.Item2.Item1, StringComparison.InvariantCultureIgnoreCase));
                    var child = pals.SingleOrDefault(p => p.InternalName.Equals(combo.Item3, StringComparison.InvariantCultureIgnoreCase));

                    // (game data seems to have combos for unreleased pals; pal data scraper here skips pals with paldex no. -1)

                    if (parent1 == null)
                        Console.WriteLine("Unrecognized parent1 {0}", combo.Item1.Item1);
                    if (parent2 == null)
                        Console.WriteLine("Unrecognized parent2 {0}", combo.Item2.Item1);
                    if (child == null)
                        Console.WriteLine("Unrecognized child {0}", combo.Item3);

                    if (parent1 == null || parent2 == null || child == null)
                    {
                        Console.WriteLine("Skipping");
                        return null;
                    }

                    return new UniqueBreedingCombo()
                    {
                        Parent1 = parent1,
                        Parent1Gender = combo.Item1.Item2,

                        Parent2 = parent2,
                        Parent2Gender = combo.Item2.Item2,

                        Child = child
                    };
                }).SkipNull().ToList()
            );

            var db = PalDB.MakeEmptyUnsafe("v13");

            Console.WriteLine("Calculating all possible pal children");
            db.Breeding = pals
                .SelectMany(parent1 => pals.Select(parent2 => (parent1, parent2)))
                .Select(pair => pair.parent1.GetHashCode() > pair.parent2.GetHashCode() ? (pair.parent1, pair.parent2) : (pair.parent2, pair.parent1))
                .Distinct()
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

            Console.WriteLine("Done");

            db.PalsById = pals.ToDictionary(p => p.Id);

            db.PassiveSkills = passives;

            var genderProbabilities = rawPals.ToDictionary(p => p.InternalName, p => new Dictionary<PalGender, float>()
            {
                { PalGender.MALE, p.MaleProbability / 100.0f },
                { PalGender.FEMALE, 1 - (p.MaleProbability / 100.0f) }
            });
            db.BreedingGenderProbability = pals.ToDictionary(
                p => p,
                p => genderProbabilities[p.InternalName]
            );

            db.MinBreedingSteps = BreedingDistanceMap.CalcMinDistances(db);

            File.WriteAllText("../PalCalc.Model/db2.json", db.ToJson());
        }


    }
}
