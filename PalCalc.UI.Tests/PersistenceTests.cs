using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.UI.Model;
using PalCalc.UI.Persistence;
using PalCalc.UI.Persistence.Dto;
using PalCalc.UI.Persistence.Serialization;
using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Solver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PalCalc.UI.Tests
{
    [TestClass]
    public class PersistenceTests
    {
    [TestMethod]
    public void FreshStorageReceivesCurrentManifest()
    {
        WithTemporaryDirectory(path =>
        {
            Directory.CreateDirectory(Path.Combine(path, "empty-save-directory"));
            StorageMigrationRunner.EnsureCurrent(path);

            var manifest = JsonConvert.DeserializeObject<StorageManifest>(
                File.ReadAllText(Path.Combine(path, StorageFormat.ManifestFileName))
            );

            Assert.IsNotNull(manifest);
            Assert.AreEqual(StorageFormat.CurrentVersion, manifest.Version);
        });
    }

    [TestMethod]
    public void LegacyStorageMigratesBeforeManifestCommit()
    {
        WithTemporaryDirectory(path =>
        {
            var results = Directory.CreateDirectory(Path.Combine(path, "results"));
            File.WriteAllText(Path.Combine(results.FullName, "legacy.txt"), "legacy");

            var settings = new AppSettings
            {
                IsDarkTheme = false,
                SolverSettings = new SerializableSolverSettings
                {
                    BannedSurgeryPassiveInternalNames = ["user-choice"],
                },
            };
            File.WriteAllText(Path.Combine(path, "settings.json"), JsonConvert.SerializeObject(settings));

            var savePath = Directory.CreateDirectory(Path.Combine(path, "save-1"));
            File.WriteAllText(
                Path.Combine(savePath.FullName, "game-settings.json"),
                "{\"BreedingTimeMinutes\":5,\"MultipleBreedingFarms\":true,\"PalboxTabWidth\":42,\"PalboxTabHeight\":43}"
            );

            StorageMigrationRunner.EnsureCurrent(path);

            Assert.AreEqual("legacy", File.ReadAllText(Path.Combine(path, "legacy.txt")));
            Assert.IsFalse(Directory.Exists(results.FullName));
            Assert.IsTrue(File.Exists(Path.Combine(path, "settings.json.bak")));
            Assert.IsTrue(File.Exists(Path.Combine(savePath.FullName, "game-settings.json.bak")));

            var currentSettings = AppSettingsJsonSerializer.FromCurrentJson(
                File.ReadAllText(Path.Combine(path, "settings.json"))
            );
            Assert.IsFalse(currentSettings.IsDarkTheme);
            CollectionAssert.Contains(currentSettings.SolverSettings.BannedSurgeryPassiveInternalNames, "user-choice");
            CollectionAssert.Contains(currentSettings.SolverSettings.BannedSurgeryPassiveInternalNames, "SwimSpeed_up_3");

            var currentGameSettings = GameSettingsJsonSerializer.FromCurrentJson(
                File.ReadAllText(Path.Combine(savePath.FullName, "game-settings.json"))
            );
            Assert.AreEqual(300, currentGameSettings.BreedingTimeSeconds);
            Assert.AreEqual(0, currentGameSettings.MassiveEggIncubationTimeMinutes);
            Assert.IsTrue(currentGameSettings.MultipleBreedingFarms);
        });
    }

    [TestMethod]
    public void CurrentSettingsRequireEveryField()
    {
        Assert.Throws<JsonSerializationException>(() => AppSettingsJsonSerializer.FromCurrentJson("{}"));
        Assert.Throws<JsonSerializationException>(() => GameSettingsJsonSerializer.FromCurrentJson("{}"));
    }

    [TestMethod]
    public void FutureStorageIsRejectedWithoutChangingFiles()
    {
        WithTemporaryDirectory(path =>
        {
            File.WriteAllText(
                Path.Combine(path, StorageFormat.ManifestFileName),
                JsonConvert.SerializeObject(new StorageManifest { Version = StorageFormat.CurrentVersion + 1 }));
            File.WriteAllText(Path.Combine(path, "settings.json"), "keep");

            Assert.Throws<StorageFormatException>(() => StorageMigrationRunner.EnsureCurrent(path));
            Assert.AreEqual("keep", File.ReadAllText(Path.Combine(path, "settings.json")));
        });
    }

    [TestMethod]
    public void LegacyTargetAndResultAliasesNormalizeToCurrentDtos()
    {
        WithTemporaryDirectory(path =>
        {
            var savePath = Directory.CreateDirectory(Path.Combine(path, "save-1"));
            var targetsPath = Directory.CreateDirectory(Path.Combine(savePath.FullName, "targets"));
            File.WriteAllText(
                Path.Combine(savePath.FullName, "pal-target-ids.json"),
                "{\"OrderedTargetIds\":[\"target-1\"],\"SourcePals\":{\"PalSourceSelections\":[\"ANY\"]}}"
            );
            File.WriteAllText(
                Path.Combine(targetsPath.FullName, "target-1.json"),
                """
                {
                  "Id": "target-1",
                  "TargetPal": "Pal_Internal",
                  "Trait1": "Swift_Internal",
                  "OptionalTrait1": "Runner_Internal",
                  "MinIV_Attack": 25,
                  "CurrentResults": {
                    "Settings": {
                      "GameSettings": { "BreedingTimeMinutes": 5 },
                      "SolverSettings": {
                        "UseGenderReversers": true,
                        "BannedBredPals": [{ "InternalName": "Bred_Internal" }],
                        "BannedWildPals": [{ "InternalName": "Wild_Internal" }]
                      }
                    },
                    "Results": [
                      { "PalReference": { "RefType": "WILD_PAL", "Content": {
                        "PalInternalName": "Pal_Internal", "GuaranteedTraits": ["Swift_Internal"], "NumTraits": 2
                      } } }
                    ]
                  }
                }
                """
            );

            StorageMigrationRunner.EnsureCurrent(path);

            var dto = TargetJsonSerializer.FromCurrentTargetJson(
                File.ReadAllText(Path.Combine(targetsPath.FullName, "target-1.json")));
            Assert.AreEqual("target-1", dto.Id);
            Assert.AreEqual("Pal_Internal", dto.TargetPalInternalName);
            CollectionAssert.AreEqual(new[] { "Swift_Internal", null, null, null }, dto.RequiredPassiveInternalNames);
            Assert.AreEqual(25, dto.MinimumIV_Attack);
            Assert.IsNotNull(dto.CurrentResults);
            Assert.AreEqual(-1, dto.CurrentResults.SelectedResultIndex);
            Assert.AreEqual(300, dto.CurrentResults.GameSettings.BreedingTimeSeconds);
            CollectionAssert.AreEqual(
                new[] { "Bred_Internal" },
                dto.CurrentResults.SolverSettings.BannedBredPalInternalNames);
            CollectionAssert.AreEqual(
                new[] { "Wild_Internal" },
                dto.CurrentResults.SolverSettings.BannedWildPalInternalNames);
            Assert.AreEqual("WILD_PAL", dto.CurrentResults.Results[0].PalReference.RefType);
            Assert.AreEqual(2, dto.CurrentResults.Results[0].PalReference.RandomPassiveCount);
        });
    }

    [TestMethod]
    public void LegacyTargetFilesAreMigratedBeforeCurrentManifest()
    {
        WithTemporaryDirectory(path =>
        {
            var savePath = Directory.CreateDirectory(Path.Combine(path, "save-1"));
            var targetsPath = Directory.CreateDirectory(Path.Combine(savePath.FullName, "targets"));
            File.WriteAllText(Path.Combine(savePath.FullName, "pal-target-ids.json"),
                "{\"OrderedTargetIds\":[\"target-1\"],\"SourcePals\":{\"PalSourceSelections\":[\"ANY\"]}}");
            File.WriteAllText(Path.Combine(targetsPath.FullName, "target-1.json"),
                "{\"Id\":\"target-1\",\"TargetPal\":\"Pal_Internal\",\"Trait1\":\"Swift_Internal\",\"CurrentResults\":null}");

            StorageMigrationRunner.EnsureCurrent(path);

            var migrated = TargetJsonSerializer.FromCurrentTargetJson(
                File.ReadAllText(Path.Combine(targetsPath.FullName, "target-1.json")));
            Assert.AreEqual("Pal_Internal", migrated.TargetPalInternalName);
            Assert.AreEqual("Swift_Internal", migrated.RequiredPassiveInternalNames[0]);
            Assert.IsTrue(File.Exists(Path.Combine(savePath.FullName, "pal-target-ids.json.bak")));
            var manifest = JsonConvert.DeserializeObject<StorageManifest>(
                File.ReadAllText(Path.Combine(path, StorageFormat.ManifestFileName)));
            Assert.IsNotNull(manifest);
            Assert.AreEqual(StorageFormat.CurrentVersion, manifest.Version);
        });
    }

    [TestMethod]
    public void LegacyPalIdentitiesUseInternalNamesAndCurrentDatabaseIds()
    {
        WithTemporaryDirectory(path =>
        {
            var savePath = Directory.CreateDirectory(Path.Combine(path, "save-1"));
            var targetsPath = Directory.CreateDirectory(Path.Combine(savePath.FullName, "targets"));
            File.WriteAllText(
                Path.Combine(savePath.FullName, "pal-target-ids.json"),
                "{\"OrderedTargetIds\":[\"direct\",\"matching\",\"unknown\"],\"SourcePals\":{\"SelectionIds\":[\"ANY\"],\"IncludeBasePals\":true,\"IncludeCustomPals\":true,\"IncludeCagedPals\":true,\"IncludeGlobalStoragePals\":true,\"IncludeExpeditionPals\":true}}"
            );

            var db = PalDB.LoadEmbedded();
            var matchingPal = db.Pals.Skip(1).First();
            var fallbackPal = db.Pals.First();
            File.WriteAllText(
                Path.Combine(targetsPath.FullName, "direct.json"),
                "{\"Id\":\"direct\",\"TargetPal\":\"DirectInternalName\",\"CurrentResults\":null}"
            );
            File.WriteAllText(
                Path.Combine(targetsPath.FullName, "matching.json"),
                $"{{\"Id\":\"matching\",\"TargetPal\":{{\"PalDexNo\":{matchingPal.Id.PalDexNo},\"IsVariant\":{matchingPal.Id.IsVariant.ToString().ToLowerInvariant()}}},\"CurrentResults\":null}}"
            );
            File.WriteAllText(
                Path.Combine(targetsPath.FullName, "unknown.json"),
                "{\"Id\":\"unknown\",\"TargetPal\":-1,\"CurrentResults\":null}"
            );

            StorageMigrationRunner.EnsureCurrent(path);

            Assert.AreEqual(
                "DirectInternalName",
                TargetJsonSerializer.FromCurrentTargetJson(File.ReadAllText(Path.Combine(targetsPath.FullName, "direct.json"))).TargetPalInternalName);
            Assert.AreEqual(
                matchingPal.InternalName,
                TargetJsonSerializer.FromCurrentTargetJson(File.ReadAllText(Path.Combine(targetsPath.FullName, "matching.json"))).TargetPalInternalName);
            Assert.AreEqual(
                fallbackPal.InternalName,
                TargetJsonSerializer.FromCurrentTargetJson(File.ReadAllText(Path.Combine(targetsPath.FullName, "unknown.json"))).TargetPalInternalName);
        });
    }

    [TestMethod]
    public void TargetIndexInvalidEntriesAreSanitizedInOrder()
    {
        var index = TargetJsonSerializer.FromCurrentJson(
            """
            {
              "OrderedTargetIds": ["target-1", " ", "target-1", "../escape", "target-2"],
              "SourcePals": {
                "SelectionIds": ["ANY", "", "ANY", "BASE"],
                "IncludeBasePals": true,
                "IncludeCustomPals": true,
                "IncludeCagedPals": true,
                "IncludeGlobalStoragePals": true,
                "IncludeExpeditionPals": true
              }
            }
            """);

        TargetPersistenceService.SanitizeIndex(index);

        CollectionAssert.AreEqual(new[] { "target-1", "target-2" }, index.OrderedTargetIds);
        CollectionAssert.AreEqual(new[] { "ANY", "BASE" }, index.SourcePals.SelectionIds);
    }

    [TestMethod]
    public void V1SettingsReceiveFrozenDisposableImplantsWithoutCurrentReader()
    {
        WithTemporaryDirectory(path =>
        {
            File.WriteAllText(Path.Combine(path, StorageFormat.ManifestFileName), "{\"version\":1}");
            File.WriteAllText(
                Path.Combine(path, "settings.json"),
                """
                {
                  "ExtraSaveLocations": [],
                  "FakeSaveNames": [],
                  "SolverSettings": {
                    "MaxBreedingSteps": 10,
                    "MaxSolverIterations": 20,
                    "MaxWildPals": 1,
                    "MaxInputIrrelevantPassives": 3,
                    "MaxBredIrrelevantPassives": 1,
                    "MaxThreads": 0,
                    "MaxGoldCost": 0,
                    "UseGenderReversers": false,
                    "BannedBredPalInternalNames": [],
                    "BannedWildPalInternalNames": ["PlantSlime_Flower"],
                    "BannedSurgeryPassiveInternalNames": ["user-choice"]
                  },
                  "PassiveSkillsPresets": [],
                  "PalListPresets": [],
                  "SelectedGameIdentifier": null,
                  "Locale": 1,
                  "IsDarkTheme": true,
                  "BreedingResultListColumns": {"ColumnVisibility": {}, "ColumnOrder": []},
                  "UiLayout": {"Windows": {}, "Grids": {}},
                  "SkippedAppVersion": null,
                  "UnknownFutureProperty": {"keep": true}
                }
                """
            );

            StorageMigrationRunner.EnsureCurrent(path);

            var raw = JObject.Parse(File.ReadAllText(Path.Combine(path, "settings.json")));
            var unknownPropertyWasPreserved = raw["UnknownFutureProperty"]?["keep"]?.Value<bool>();
            Assert.IsNotNull(unknownPropertyWasPreserved);
            Assert.IsTrue(unknownPropertyWasPreserved.Value);

            var settings = AppSettingsJsonSerializer.FromCurrentJson(raw.ToString(Formatting.None));
            CollectionAssert.Contains(settings.SolverSettings.BannedSurgeryPassiveInternalNames, "user-choice");
            CollectionAssert.Contains(settings.SolverSettings.BannedSurgeryPassiveInternalNames, "SwimSpeed_up_3");
            CollectionAssert.Contains(settings.SolverSettings.BannedSurgeryPassiveInternalNames, "PAL_ALLAttack_up3");
        });
    }

    [TestMethod]
    public void LegacyCustomizationsNormalizeToCurrentDto()
    {
        WithTemporaryDirectory(path =>
        {
            var savePath = Directory.CreateDirectory(Path.Combine(path, "save-1"));
            File.WriteAllText(
                Path.Combine(savePath.FullName, "custom-containers.json"),
                """
                {
                  "CustomContainers": [
                    {
                      "Label": "favorites",
                      "Contents": [
                        {
                          "InternalName": "Pal_Internal",
                          "Location": {"ContainerId": null, "Type": "Custom", "Index": 2},
                          "Gender": "MALE",
                          "Traits": ["Swift_Internal"],
                          "OwnerPlayerId": null,
                          "NickName": null,
                          "InstanceId": null,
                          "Level": 1,
                          "IV_HP": 10,
                          "IV_Melee": 20,
                          "IV_Shot": 30,
                          "IV_Defense": 40,
                          "IsOnExpedition": false
                        }
                      ]
                    }
                  ]
                }
                """
            );

            StorageMigrationRunner.EnsureCurrent(path);

            var dto = CustomizationsJsonSerializer.FromCurrentJson(
                File.ReadAllText(Path.Combine(savePath.FullName, "custom-containers.json")));
            Assert.AreEqual("favorites", dto.CustomContainers[0].Label);
            Assert.AreEqual("Pal_Internal", dto.CustomContainers[0].Contents[0].InternalName);
            CollectionAssert.AreEqual(new[] { "Swift_Internal" }, dto.CustomContainers[0].Contents[0].PassiveSkills);
            Assert.AreEqual(40, dto.CustomContainers[0].Contents[0].IV_Defense);
        });
    }

    [TestMethod]
    public void VersionThreeDocumentsGainAttackInheritanceFields()
    {
        WithTemporaryDirectory(path =>
        {
            File.WriteAllText(
                StorageFormat.ManifestPath(path),
                JsonConvert.SerializeObject(new StorageManifest { Version = 3 })
            );
            File.WriteAllText(
                Path.Combine(path, "settings.json"),
                """{"SolverSettings":{}}"""
            );
            var targetsPath = Directory.CreateDirectory(Path.Combine(path, "save-1", "targets"));
            var targetPath = Path.Combine(targetsPath.FullName, "target.json");
            File.WriteAllText(
                targetPath,
                """
                {
                  "CurrentResults": {
                    "SolverSettings": {},
                    "Results": [
                      {
                        "PalReference": {
                          "RefType": "BRED_PAL",
                          "Parent1": {"RefType": "WILD_PAL"},
                          "Parent2": {"RefType": "WILD_PAL"}
                        }
                      }
                    ]
                  }
                }
                """
            );

            StorageMigrationRunner.EnsureCurrent(path);

            var settings = JObject.Parse(File.ReadAllText(Path.Combine(path, "settings.json")));
            var target = JObject.Parse(File.ReadAllText(targetPath));
            var reference = (JObject)target["CurrentResults"]!["Results"]![0]!["PalReference"]!;

            Assert.AreEqual(0, settings["SolverSettings"]!["MaxSpecialCakes"]!.Value<int>());
            Assert.IsInstanceOfType<JArray>(target["RequiredAttackInternalNames"]);
            Assert.AreEqual(0, target["CurrentResults"]!["SolverSettings"]!["MaxSpecialCakes"]!.Value<int>());
            Assert.AreEqual(JTokenType.Null, reference["AvgRequiredBreedings"]!.Type);
            Assert.AreEqual(JTokenType.Null, reference["MaterializedAttackInheritance"]!.Type);
            Assert.AreEqual(JTokenType.Null, reference["Parent1"]!["MaterializedAttackInheritance"]!.Type);
        });
    }

    [TestMethod]
    public void LegacyAttackSettingsAndTargetsSurviveFormalization()
    {
        WithTemporaryDirectory(path =>
        {
            File.WriteAllText(
                Path.Combine(path, "settings.json"),
                """{"SolverSettings":{"MaxSpecialCakes":7}}"""
            );
            var savePath = Directory.CreateDirectory(Path.Combine(path, "save-1"));
            File.WriteAllText(
                Path.Combine(savePath.FullName, "pal-targets.json"),
                """
                {
                  "Targets": [
                    {
                      "Id": "attack-target",
                      "TargetPal": "Pal_Internal",
                      "RequiredAttacks": ["Attack_A", "Attack_B"],
                      "CurrentResults": null
                    }
                  ]
                }
                """
            );

            StorageMigrationRunner.EnsureCurrent(path);

            var settings = JObject.Parse(File.ReadAllText(Path.Combine(path, "settings.json")));
            var target = JObject.Parse(File.ReadAllText(
                Path.Combine(savePath.FullName, "targets", "attack-target.json")
            ));

            Assert.AreEqual(7, settings["SolverSettings"]!["MaxSpecialCakes"]!.Value<int>());
            CollectionAssert.AreEqual(
                new[] { "Attack_A", "Attack_B" },
                target["RequiredAttackInternalNames"]!.Values<string>().ToArray()
            );
        });
    }

    [TestMethod]
    public void AtomicMigrationBackupPreservesTheOriginalAcrossRetries()
    {
        WithTemporaryDirectory(path =>
        {
            var file = Path.Combine(path, "settings.json");
            File.WriteAllText(file, "original");

            StorageFile.WriteAtomic(file, "first migration", backup: true);
            StorageFile.WriteAtomic(file, "retry", backup: true);

            Assert.AreEqual("original", File.ReadAllText(file + ".bak"));
            Assert.AreEqual("retry", File.ReadAllText(file));
        });
    }

    [TestMethod]
    public void LegacyTargetIdCannotEscapeTheTargetsDirectory()
    {
        Assert.Throws<StorageFormatException>(() => TargetPersistenceService.TargetFileName("../outside"));
    }

    [TestMethod]
    public void EveryProductionPalReferenceVariantRoundTrips()
    {
        var db = PalDB.LoadEmbedded();
        var pal = "Beakon".ToPal(db);
        var swift = "Swift".ToStandardPassive(db);
        var ivs = new IV_Set(
            new IV_Value(true, 80, 100),
            IV_Value.Random,
            new IV_Value(false, 0, 50));

        OwnedPalReference Owned(PalGender gender, string instanceId) => new(
            new PalInstance
            {
                Pal = pal,
                Location = new PalLocation { Type = LocationType.Palbox, ContainerId = "box", Index = 1 },
                Gender = gender,
                PassiveSkills = [swift],
                ActiveSkills = [],
                EquippedActiveSkills = [],
                InstanceId = instanceId,
            },
            [swift],
            ivs,
            AttackProfile.Inactive);

        var male = Owned(PalGender.MALE, "male");
        var female = Owned(PalGender.FEMALE, "female");
        var wild = new WildPalReference(pal, [], 1, db.BreedingMechanics, AttackProfile.Inactive);
        var bred = new BredPalReference(GameSettings.Defaults, pal, male, wild, [swift], 1, ivs, 1, AttackProfile.Inactive, null, null, PalGender.WILDCARD);
        var composite = new CompositeOwnedPalReference(male, female);
        var surgery = new SurgeryTablePalReference(wild, [new AddPassiveSurgeryOperation(swift)]);
        var settings = new SerializableSolverSettings();

        foreach (var original in new IPalReference[] { male, wild, bred, composite, surgery })
        {
            var json = JsonConvert.SerializeObject(ResultJsonSerializer.ToDto(original));
            var dto = JsonConvert.DeserializeObject<PalReferenceDto>(json);
            Assert.IsNotNull(dto);

            var roundTripped = ResultJsonSerializer.FromDto(dto, db, GameSettings.Defaults, settings);
            Assert.AreEqual(original.GetType(), roundTripped.GetType());
            Assert.AreEqual(original.Pal.InternalName, roundTripped.Pal.InternalName);
            CollectionAssert.AreEqual(
                original.EffectivePassives.ConvertAll(passive => passive.InternalName),
                roundTripped.EffectivePassives.ConvertAll(passive => passive.InternalName));
        }
    }

    [TestMethod]
    public void OwnedPalResultRoundTripPreservesRealSaveLocation()
    {
        var db = PalDB.LoadEmbedded();
        var containerId = Guid.NewGuid().ToString();
        var original = new OwnedPalReference(
            new PalInstance
            {
                Pal = "Beakon".ToPal(db),
                Location = new PalLocation
                {
                    ContainerId = containerId,
                    Type = LocationType.Base,
                    Index = 7,
                },
                Gender = PalGender.MALE,
                PassiveSkills = [],
                ActiveSkills = [],
                EquippedActiveSkills = [],
            },
            [],
            new IV_Set(IV_Value.Random, IV_Value.Random, IV_Value.Random),
            AttackProfile.Inactive);

        var dto = JsonConvert.DeserializeObject<PalReferenceDto>(
            JsonConvert.SerializeObject(ResultJsonSerializer.ToDto(original)));
        var restored = (OwnedPalReference)ResultJsonSerializer.FromDto(
            dto,
            db,
            GameSettings.Defaults,
            new SerializableSolverSettings());

        Assert.AreEqual(LocationType.Base, restored.UnderlyingInstance.Location.Type);
        Assert.AreEqual(containerId, restored.UnderlyingInstance.Location.ContainerId);
        Assert.AreEqual(original.UnderlyingInstance.Location.ToString(), restored.UnderlyingInstance.Location.ToString());
    }

    [TestMethod]
    public void SelectedResultRoundTrips()
    {
        var db = PalDB.LoadEmbedded();
        var pal = "Beakon".ToPal(db);
        var wild = new WildPalReference(pal, [], 1, db.BreedingMechanics, AttackProfile.Inactive);
        var dto = new BreedingResultListDto
        {
            GameSettings = ResultJsonSerializer.ToDto(GameSettings.Defaults),
            SolverSettings = ResultJsonSerializer.ToDto(new SerializableSolverSettings()),
            Results =
            [
                new BreedingResultDto { PalReference = ResultJsonSerializer.ToDto(wild) },
                new BreedingResultDto { PalReference = ResultJsonSerializer.ToDto(wild) },
            ],
            SelectedResultIndex = 1,
        };

        var json = JsonConvert.SerializeObject(dto);
        var restoredDto = JsonConvert.DeserializeObject<BreedingResultListDto>(json);
        Assert.AreEqual(1, restoredDto!.SelectedResultIndex);

        var restored = ResultJsonSerializer.FromDto(
            restoredDto,
            db,
            null,
            GameSettings.Defaults,
            null,
            null,
            null);
        Assert.AreSame(restored.Results[1], restored.SelectedResult);
    }

    [TestMethod]
    public void CustomContentsUseTheirOwningContainerLocation()
    {
        var db = PalDB.LoadEmbedded();
        var pal = "Beakon".ToPal(db);
        var dto = new SaveCustomizationsDto
        {
            CustomContainers =
            [
                new CustomContainerDto
                {
                    Label = "favorites",
                    Contents =
                    [
                        new PalInstanceSnapshotDto
                        {
                            InternalName = pal.InternalName,
                            Location = new PalLocationDto
                            {
                                ContainerId = "real-save-container",
                                Type = LocationType.Palbox,
                                Index = 2,
                            },
                            Gender = PalGender.MALE,
                            PassiveSkills = [],
                            ActiveSkills = [],
                            EquippedActiveSkills = [],
                        },
                    ],
                },
            ],
        };

        var restored = CustomizationsJsonSerializer.ToRuntime(dto, db).CustomContainers.Single().Contents.Single();

        Assert.AreEqual(LocationType.Custom, restored.Location.Type);
        Assert.AreEqual("favorites", restored.Location.ContainerId);
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "palcalc-persistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            action(path);
        }
        finally
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
    }
}
}
