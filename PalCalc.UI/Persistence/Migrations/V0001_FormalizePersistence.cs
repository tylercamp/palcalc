using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.UI.Persistence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PalCalc.UI.Persistence.Migrations
{
    internal sealed class V0001_FormalizePersistence : StorageMigration
    {
        private const string LegacyResultsDirectoryName = "results";
        private const string LegacyTargetFileName = "pal-targets.json";
        private const string TargetIndexFileName = "pal-target-ids.json";
        private const string TargetsDirectoryName = "targets";
        private const string RandomPassiveInternalName = "__VIRT_RAND__";
        private const string AnySourceSelectionId = "ANY";

        public V0001_FormalizePersistence() : base(0, 1) { }

        public override void Apply(StorageMigrationContext context)
        {
            MoveLegacyResults(context.DataPath);
            RewriteAppSettings(context.DataPath);
            RewriteGameSettings(context.DataPath);
            RewriteCustomizations(context.DataPath);
            RewriteTargets(context.DataPath);
        }

        private static void MoveLegacyResults(string dataPath)
        {
            var oldResultsPath = Path.Combine(dataPath, LegacyResultsDirectoryName);
            if (!Directory.Exists(oldResultsPath))
                return;

            foreach (var entry in Directory.EnumerateFileSystemEntries(oldResultsPath).ToList())
            {
                var destination = Path.Combine(dataPath, Path.GetFileName(entry));
                if (File.Exists(destination) || Directory.Exists(destination))
                    throw new IOException($"Cannot migrate '{entry}' because '{destination}' already exists.");

                if (Directory.Exists(entry))
                    Directory.Move(entry, destination);
                else
                    File.Move(entry, destination);
            }

            Directory.Delete(oldResultsPath);
        }

        private static void RewriteAppSettings(string dataPath)
        {
            var path = Path.Combine(dataPath, "settings.json");
            if (!File.Exists(path))
                return;

            var root = JObject.Parse(File.ReadAllText(path));
            StorageFile.WriteAtomic(path, NormalizeAppSettings(root).ToString(Formatting.None), backup: true);
        }

        private static void RewriteGameSettings(string dataPath)
        {
            if (!Directory.Exists(dataPath))
                return;

            foreach (var saveDirectory in Directory.EnumerateDirectories(dataPath))
            {
                var path = Path.Combine(saveDirectory, "game-settings.json");
                if (!File.Exists(path))
                    continue;

                var root = JObject.Parse(File.ReadAllText(path));
                StorageFile.WriteAtomic(path, NormalizeGameSettings(root).ToString(Formatting.None), backup: true);
            }
        }

        private static void RewriteCustomizations(string dataPath)
        {
            if (!Directory.Exists(dataPath))
                return;

            foreach (var saveDirectory in Directory.EnumerateDirectories(dataPath))
            {
                var path = Path.Combine(saveDirectory, "custom-containers.json");
                if (!File.Exists(path))
                    continue;

                var root = JObject.Parse(File.ReadAllText(path));
                StorageFile.WriteAtomic(path, NormalizeCustomizations(root).ToString(Formatting.None), backup: true);
            }
        }

        private static void RewriteTargets(string dataPath)
        {
            if (!Directory.Exists(dataPath))
                return;

            foreach (var saveDirectory in Directory.EnumerateDirectories(dataPath))
            {
                var monolithPath = Path.Combine(saveDirectory, LegacyTargetFileName);
                var indexPath = Path.Combine(saveDirectory, TargetIndexFileName);

                if (File.Exists(monolithPath))
                {
                    RewriteMonolith(saveDirectory, monolithPath, indexPath);
                    continue;
                }

                if (!File.Exists(indexPath))
                    continue;

                var index = ParseIndex(JObject.Parse(File.ReadAllText(indexPath)));
                var targetsPath = Path.Combine(saveDirectory, TargetsDirectoryName);
                var targetIds = new HashSet<string>(index["OrderedTargetIds"].Values<string>(), StringComparer.Ordinal);
                if (Directory.Exists(targetsPath))
                {
                    targetIds.UnionWith(Directory.EnumerateFiles(targetsPath, "*.json").Select(Path.GetFileNameWithoutExtension));
                }

                foreach (var targetId in targetIds)
                {
                    var path = Path.Combine(targetsPath, TargetFileName(targetId));
                    if (!File.Exists(path))
                        continue;

                    var target = ParseTarget(JObject.Parse(File.ReadAllText(path)), targetId);
                    var documentId = ReadString(target["Id"]);
                    if (!string.Equals(documentId, targetId, StringComparison.Ordinal))
                        throw new StorageFormatException($"Target filename '{targetId}' does not match document ID '{documentId}'.");

                    StorageFile.WriteAtomic(path, target.ToString(Formatting.None), backup: true);
                }

                StorageFile.WriteAtomic(indexPath, index.ToString(Formatting.None), backup: true);
            }
        }

        private static void RewriteMonolith(string saveDirectory, string monolithPath, string indexPath)
        {
            var root = JObject.Parse(File.ReadAllText(monolithPath));
            var oldTargets = (root["Targets"] as JArray)?.OfType<JObject>().ToList() ?? [];
            var targetIds = new List<string>();
            var targetsPath = Path.Combine(saveDirectory, TargetsDirectoryName);
            Directory.CreateDirectory(targetsPath);
            var source = ReadSource(root["SourcePals"] as JObject ?? root);

            for (var index = 0; index < oldTargets.Count; index++)
            {
                var oldTarget = oldTargets[index];
                source = MergeSource(source, ReadSource(oldTarget));
                var target = ParseTarget(oldTarget, $"legacy-target-{index}");
                var documentId = ReadString(target["Id"]);
                if (targetIds.Contains(documentId, StringComparer.Ordinal))
                    throw new StorageFormatException($"Legacy target list contains duplicate target ID '{documentId}'.");

                targetIds.Add(documentId);
                StorageFile.WriteAtomic(
                    Path.Combine(targetsPath, TargetFileName(documentId)),
                    target.ToString(Formatting.None),
                    backup: true
                );
            }

            var indexDocument = new JObject
            {
                ["OrderedTargetIds"] = new JArray(targetIds.Distinct()),
                ["SourcePals"] = source.DeepClone(),
            };
            StorageFile.WriteAtomic(indexPath, indexDocument.ToString(Formatting.None), backup: true);

            var backupPath = monolithPath + ".bak";
            if (!File.Exists(backupPath))
                File.Move(monolithPath, backupPath);
            else
                File.Delete(monolithPath);
        }

        private static JObject ParseIndex(JObject root) => new()
        {
            ["OrderedTargetIds"] = new JArray(ReadStringArray(root["OrderedTargetIds"]).Values<string>().Distinct()),
            ["SourcePals"] = ReadSource(root["SourcePals"] as JObject ?? root),
        };

        private static JObject ParseTarget(JObject root, string fallbackId)
        {
            var required = ReadPassiveSlots(root, "Passive", "Trait");
            var optional = ReadPassiveSlots(root, "OptionalPassive", "OptionalTrait");
            var id = ReadString(root["Id"]) ?? fallbackId ?? throw new StorageFormatException("Legacy target has no ID.");

            return new JObject
            {
                ["Id"] = id,
                ["TargetPalInternalName"] = ReadIdentity(root["TargetPal"] ?? root["TargetPalInternalName"]),
                ["RequiredPassiveInternalNames"] = new JArray(required.Select(name => name == null ? JValue.CreateNull() : new JValue(name))),
                ["OptionalPassiveInternalNames"] = new JArray(optional.Select(name => name == null ? JValue.CreateNull() : new JValue(name))),
                ["MinimumIV_HP"] = ReadInt(root["MinimumIV_HP"] ?? root["MinIV_HP"], 0),
                ["MinimumIV_Attack"] = ReadInt(root["MinimumIV_Attack"] ?? root["MinIV_Attack"], 0),
                ["MinimumIV_Defense"] = ReadInt(root["MinimumIV_Defense"] ?? root["MinIV_Defense"], 0),
                ["RequiredGender"] = ReadGender(root["RequiredGender"], "WILDCARD"),
                ["CurrentResults"] = root["CurrentResults"] == null || root["CurrentResults"].Type == JTokenType.Null
                    ? null
                    : NormalizeResults(root["CurrentResults"] as JObject
                        ?? throw new JsonSerializationException("Legacy target results were not an object."), required.Concat(optional).Where(name => name != null).ToList()),
            };
        }

        private static JObject ReadSource(JObject value)
        {
            var source = value["SourcePals"] as JObject ?? value;
            var selections = ReadStringArray(source["SelectionIds"] ?? source["PalSourceSelections"]);
            if (selections.Count == 0 && source["PalSourceId"] != null)
                selections.Add(ReadString(source["PalSourceId"]));
            if (selections.Count == 0)
                selections.Add(AnySourceSelectionId);

            return new JObject
            {
                ["SelectionIds"] = selections,
                ["IncludeBasePals"] = ReadBool(source["IncludeBasePals"], true),
                ["IncludeCustomPals"] = ReadBool(source["IncludeCustomPals"], true),
                ["IncludeCagedPals"] = ReadBool(source["IncludeCagedPals"], true),
                ["IncludeGlobalStoragePals"] = ReadBool(source["IncludeGlobalStoragePals"], true),
                ["IncludeExpeditionPals"] = ReadBool(source["IncludeExpeditionPals"], true),
            };
        }

        private static JObject MergeSource(JObject left, JObject right)
        {
            var selections = new JArray();
            foreach (var selection in (left["SelectionIds"] as JArray ?? []).Concat(right["SelectionIds"] as JArray ?? []))
            {
                if (!selections.Values<string>().Contains(selection.Value<string>(), StringComparer.Ordinal))
                    selections.Add(selection.DeepClone());
            }

            return new JObject
            {
                ["SelectionIds"] = selections,
                ["IncludeBasePals"] = ReadBool(left["IncludeBasePals"], true) || ReadBool(right["IncludeBasePals"], true),
                ["IncludeCustomPals"] = ReadBool(left["IncludeCustomPals"], true) || ReadBool(right["IncludeCustomPals"], true),
                ["IncludeCagedPals"] = ReadBool(left["IncludeCagedPals"], true) || ReadBool(right["IncludeCagedPals"], true),
                ["IncludeGlobalStoragePals"] = ReadBool(left["IncludeGlobalStoragePals"], true) || ReadBool(right["IncludeGlobalStoragePals"], true),
                ["IncludeExpeditionPals"] = ReadBool(left["IncludeExpeditionPals"], true) || ReadBool(right["IncludeExpeditionPals"], true),
            };
        }

        private static List<string> ReadPassiveSlots(JObject value, string currentPrefix, string legacyPrefix) =>
            value[currentPrefix == "Passive" ? "RequiredPassiveInternalNames" : "OptionalPassiveInternalNames"] is JArray current
                ? current.Select(ReadPassiveName).Concat(Enumerable.Repeat<string>(null, 4)).Take(4).ToList()
                : Enumerable.Range(1, 4)
                    .Select(slot => ReadPassiveName(value[$"{currentPrefix}{slot}"] ?? value[$"{legacyPrefix}{slot}"]))
                    .ToList();

        private static JObject NormalizeAppSettings(JObject root)
        {
            var solver = root["SolverSettings"] as JObject ?? new JObject();
            return new JObject
            {
                ["ExtraSaveLocations"] = ReadStringArray(root["ExtraSaveLocations"]),
                ["FakeSaveNames"] = ReadStringArray(root["FakeSaveNames"]),
                ["SolverSettings"] = NormalizeSolverSettings(solver),
                ["PassiveSkillsPresets"] = NormalizePassivePresets(root["PassiveSkillsPresets"]),
                ["PalListPresets"] = NormalizePalListPresets(root["PalListPresets"]),
                ["SelectedGameIdentifier"] = NullableStringToken(root["SelectedGameIdentifier"]),
                ["Locale"] = ReadLocale(root["Locale"]),
                ["IsDarkTheme"] = ReadBool(root["IsDarkTheme"], true),
                ["BreedingResultListColumns"] = NormalizeColumns(root["BreedingResultListColumns"] as JObject),
                ["UiLayout"] = NormalizeUiLayout(root["UiLayout"] as JObject),
                ["SkippedAppVersion"] = NullableStringToken(root["SkippedAppVersion"]),
            };
        }

        private static JObject NormalizeSolverSettings(JObject value)
        {
            return new JObject
            {
                ["MaxBreedingSteps"] = ReadInt(value["MaxBreedingSteps"], 10),
                ["MaxSolverIterations"] = ReadInt(value["MaxSolverIterations"], 20),
                ["MaxWildPals"] = ReadInt(value["MaxWildPals"], 1),
                ["MaxInputIrrelevantPassives"] = ReadInt(value["MaxInputIrrelevantPassives"], 3),
                ["MaxBredIrrelevantPassives"] = ReadInt(value["MaxBredIrrelevantPassives"], 1),
                ["MaxThreads"] = ReadInt(value["MaxThreads"], 0),
                ["MaxGoldCost"] = ReadInt(value["MaxGoldCost"], 0),
                ["UseGenderReversers"] = ReadBool(value["UseGenderReversers"], false),
                ["BannedBredPalInternalNames"] = ReadStringArray(value["BannedBredPalInternalNames"]),
                ["BannedWildPalInternalNames"] = ReadStringArray(value["BannedWildPalInternalNames"], "PlantSlime_Flower"),
                // V1 predates disposable-implant defaults. V1 -> V2 adds that frozen list.
                ["BannedSurgeryPassiveInternalNames"] = ReadStringArray(value["BannedSurgeryPassiveInternalNames"]),
            };
        }

        private static JArray NormalizePassivePresets(JToken token)
        {
            var result = new JArray();
            foreach (var item in token as JArray ?? [])
            {
                var value = item as JObject ?? new JObject();
                result.Add(new JObject
                {
                    ["Name"] = NullableStringToken(value["Name"]),
                    ["Passive1InternalName"] = NullableStringToken(value["Passive1InternalName"]),
                    ["Passive2InternalName"] = NullableStringToken(value["Passive2InternalName"]),
                    ["Passive3InternalName"] = NullableStringToken(value["Passive3InternalName"]),
                    ["Passive4InternalName"] = NullableStringToken(value["Passive4InternalName"]),
                    ["OptionalPassive1InternalName"] = NullableStringToken(value["OptionalPassive1InternalName"]),
                    ["OptionalPassive2InternalName"] = NullableStringToken(value["OptionalPassive2InternalName"]),
                    ["OptionalPassive3InternalName"] = NullableStringToken(value["OptionalPassive3InternalName"]),
                    ["OptionalPassive4InternalName"] = NullableStringToken(value["OptionalPassive4InternalName"]),
                });
            }

            return result;
        }

        private static JArray NormalizePalListPresets(JToken token)
        {
            var result = new JArray();
            foreach (var item in token as JArray ?? [])
            {
                var value = item as JObject ?? new JObject();
                result.Add(new JObject
                {
                    ["Name"] = NullableStringToken(value["Name"]),
                    ["PalInternalNames"] = ReadStringArray(value["PalInternalNames"]),
                });
            }

            return result;
        }

        private static JObject NormalizeColumns(JObject value) => new()
        {
            ["ColumnVisibility"] = ReadBoolDictionary(value?["ColumnVisibility"]),
            ["ColumnOrder"] = ReadStringArray(value?["ColumnOrder"]),
        };

        private static JObject NormalizeUiLayout(JObject value)
        {
            var windows = new JObject();
            foreach (var property in (value?["Windows"] as JObject)?.Properties() ?? [])
            {
                var placement = property.Value as JObject ?? new JObject();
                windows[property.Name] = new JObject
                {
                    ["Version"] = ReadInt(placement["Version"], 1),
                    ["Left"] = ReadInt(placement["Left"], 0),
                    ["Top"] = ReadInt(placement["Top"], 0),
                    ["Right"] = ReadInt(placement["Right"], 0),
                    ["Bottom"] = ReadInt(placement["Bottom"], 0),
                    ["IsMaximized"] = ReadBool(placement["IsMaximized"], false),
                };
            }

            var grids = new JObject();
            foreach (var property in (value?["Grids"] as JObject)?.Properties() ?? [])
            {
                var grid = property.Value as JObject ?? new JObject();
                grids[property.Name] = new JObject
                {
                    ["Version"] = ReadInt(grid["Version"], 1),
                    ["Columns"] = NormalizeGridLengths(grid["Columns"]),
                    ["Rows"] = NormalizeGridLengths(grid["Rows"]),
                };
            }

            return new JObject { ["Windows"] = windows, ["Grids"] = grids };
        }

        private static JArray NormalizeGridLengths(JToken token)
        {
            var result = new JArray();
            foreach (var item in token as JArray ?? [])
            {
                var value = item as JObject ?? new JObject();
                result.Add(new JObject
                {
                    ["Unit"] = ReadInt(value["Unit"], 0),
                    ["Value"] = ReadDouble(value["Value"], 0),
                });
            }

            return result;
        }

        private static JObject NormalizeGameSettings(JObject root)
        {
            var breedingTimeSeconds = root["BreedingTimeSeconds"];
            return new JObject
            {
                ["BreedingTimeSeconds"] = breedingTimeSeconds != null && breedingTimeSeconds.Type != JTokenType.Null
                    ? ReadInt(breedingTimeSeconds, 0)
                    : ReadInt(root["BreedingTimeMinutes"], 0) * 60,
                ["MassiveEggIncubationTimeMinutes"] = ReadInt(root["MassiveEggIncubationTimeMinutes"], 0),
                ["MultipleBreedingFarms"] = ReadBool(root["MultipleBreedingFarms"], false),
                ["PalboxTabWidth"] = ReadInt(root["PalboxTabWidth"], 0),
                ["PalboxTabHeight"] = ReadInt(root["PalboxTabHeight"], 0),
            };
        }

        private static JObject NormalizeCustomizations(JObject root)
        {
            var containers = new JArray();
            foreach (var item in root["CustomContainers"] as JArray ?? [])
            {
                var container = item as JObject ?? new JObject();
                var contents = new JArray();
                foreach (var instance in container["Contents"] as JArray ?? [])
                    contents.Add(NormalizeInstance(instance as JObject ?? new JObject()));

                containers.Add(new JObject
                {
                    ["Label"] = NullableStringToken(container["Label"]),
                    ["Contents"] = contents,
                });
            }

            return new JObject { ["CustomContainers"] = containers };
        }

        private static JObject NormalizeResults(JObject root, IReadOnlyCollection<string> desiredPassives)
        {
            var settings = root["Settings"] as JObject;
            var gameSettings = root["GameSettings"] as JObject ?? settings?["GameSettings"] as JObject;
            var solverSettings = root["SolverSettings"] as JObject ?? settings?["SolverSettings"] as JObject;
            var results = new JArray();

            foreach (var item in root["Results"] as JArray ?? [])
            {
                var result = item as JObject ?? throw new JsonSerializationException("Legacy result was not an object.");
                results.Add(new JObject
                {
                    ["PalReference"] = NormalizeReference(result["PalReference"] ?? result, desiredPassives),
                    ["CheckedNodes"] = result["CheckedNodes"] is JArray checkedNodes
                        ? checkedNodes.DeepClone()
                        : JValue.CreateNull(),
                });
            }

            return new JObject
            {
                ["GameSettings"] = NormalizeResultGameSettings(gameSettings),
                ["SolverSettings"] = NormalizeResultSolverSettings(solverSettings),
                ["Results"] = results,
            };
        }

        private static JObject NormalizeResultGameSettings(JObject value)
        {
            var result = new JObject
            {
                ["BreedingTimeSeconds"] = ReadDurationSeconds(value, 300),
                ["MassiveEggIncubationTimeMinutes"] = ReadDurationMinutes(value?["MassiveEggIncubationTimeMinutes"] ?? value?["MassiveEggIncubationTime"], 120),
                ["MultipleBreedingFarms"] = ReadBool(value?["MultipleBreedingFarms"], false),
                ["MultipleIncubators"] = ReadBool(value?["MultipleIncubators"], true),
                ["PlayerPartySize"] = ReadInt(value?["PlayerPartySize"], 5),
                ["LocationTypeGridWidths"] = ReadIntDictionary(value?["LocationTypeGridWidths"], new Dictionary<string, int>
                {
                    ["PlayerParty"] = 5,
                    ["Palbox"] = 6,
                    ["Base"] = 5,
                    ["ViewingCage"] = 6,
                    ["DimensionalPalStorage"] = 6,
                    ["GlobalPalStorage"] = 6,
                    ["Custom"] = 8,
                }),
                ["LocationTypeGridHeights"] = ReadNullableIntDictionary(value?["LocationTypeGridHeights"], new Dictionary<string, int?>
                {
                    ["Palbox"] = 5,
                    ["DimensionalPalStorage"] = 5,
                    ["GlobalPalStorage"] = 5,
                    ["PlayerParty"] = null,
                    ["Base"] = null,
                    ["ViewingCage"] = null,
                    ["Custom"] = null,
                }),
            };

            return result;
        }

        private static JObject NormalizeResultSolverSettings(JObject value)
        {
            return new JObject
            {
                ["MaxBreedingSteps"] = ReadInt(value?["MaxBreedingSteps"], 10),
                ["MaxSolverIterations"] = ReadInt(value?["MaxSolverIterations"], 20),
                ["MaxWildPals"] = ReadInt(value?["MaxWildPals"], 1),
                ["MaxInputIrrelevantPassives"] = ReadInt(value?["MaxInputIrrelevantPassives"], 3),
                ["MaxBredIrrelevantPassives"] = ReadInt(value?["MaxBredIrrelevantPassives"], 1),
                ["MaxThreads"] = ReadInt(value?["MaxThreads"], 0),
                ["MaxGoldCost"] = ReadInt(value?["MaxGoldCost"], 0),
                ["UseGenderReversers"] = ReadBool(value?["UseGenderReversers"], false),
                ["BannedBredPalInternalNames"] = NamesArray(ReadNames(value?["BannedBredPalInternalNames"] ?? value?["BannedBredPals"])),
                ["BannedWildPalInternalNames"] = NamesArray(ReadNames(value?["BannedWildPalInternalNames"] ?? value?["BannedWildPals"])),
                ["BannedSurgeryPassiveInternalNames"] = NamesArray(ReadPassiveNames(value?["BannedSurgeryPassiveInternalNames"] ?? value?["BannedSurgeryPassives"])),
            };
        }

        private static JObject NormalizeReference(JToken token, IReadOnlyCollection<string> desiredPassives)
        {
            if (token == null || token.Type == JTokenType.Null)
                throw new JsonSerializationException("Legacy result contains a null reference.");

            var wrapper = token as JObject;
            var type = ReadString(wrapper?["RefType"]);
            var content = wrapper?["Content"] as JObject ?? wrapper;
            if (content == null)
                throw new JsonSerializationException("Legacy result reference was not an object.");

            if (type == null)
            {
                if (content["Instance"] != null || content["Id"] != null && content["Location"] != null)
                    type = "OWNED_PAL";
                else if (content["Parent1"] != null)
                    type = "BRED_PAL";
                else
                    throw new JsonSerializationException("Legacy result reference has no RefType.");
            }

            return type.ToUpperInvariant() switch
            {
                "OWNED_PAL" => NormalizeOwned(content, desiredPassives),
                "WILD_PAL" => NormalizeWild(content),
                "BRED_PAL" => NormalizeBred(content, desiredPassives),
                "COMPOSITE_PAL" => NormalizeComposite(content, desiredPassives),
                "SURGERY_PAL" => NormalizeSurgery(content, desiredPassives),
                _ => throw new JsonSerializationException($"Unknown legacy result reference type '{type}'."),
            };
        }

        private static JObject NormalizeOwned(JObject value, IReadOnlyCollection<string> desiredPassives)
        {
            var instance = NormalizeInstance(value["Instance"] as JObject ?? value);
            var ivs = value["IVs"] as JObject ?? new JObject
            {
                ["HP"] = FixedIv(ReadInt(instance["IV_HP"], 0)),
                ["Attack"] = FixedIv(ReadInt(instance["IV_Melee"], 0)),
                ["Defense"] = FixedIv(ReadInt(instance["IV_Defense"], 0)),
            };
            var effective = value["EffectivePassiveInternalNames"] != null
                ? ReadPassiveNames(value["EffectivePassiveInternalNames"])
                : ReadPassiveNames(instance["PassiveSkills"])
                    .Select(name => desiredPassives.Contains(name) ? name : RandomPassiveInternalName)
                    .ToList();

            var result = NewReference("OWNED_PAL");
            result["Instance"] = instance;
            result["ActualGender"] = ReadGender(value["ActualGender"], ReadString(instance["Gender"]) ?? "WILDCARD");
            result["IVs"] = NormalizeIvSet(ivs);
            result["EffectivePassiveInternalNames"] = new JArray(effective.Select(name => name == null ? JValue.CreateNull() : new JValue(name)).Distinct());
            return result;
        }

        private static JObject NormalizeWild(JObject value)
        {
            var result = NewReference("WILD_PAL");
            result["PalInternalName"] = ReadIdentity(value["PalInternalName"] ?? value["PalId"] ?? value["Id"]);
            result["GuaranteedPassiveInternalNames"] = NamesArray(ReadPassiveNames(value["GuaranteedPassiveInternalNames"] ?? value["GuaranteedPassives"] ?? value["GuaranteedTraits"]));
            result["RandomPassiveCount"] = ReadInt(value["RandomPassiveCount"] ?? value["NumPassives"] ?? value["NumTraits"], 0);
            result["Gender"] = ReadGender(value["Gender"], "WILDCARD");
            return result;
        }

        private static JObject NormalizeBred(JObject value, IReadOnlyCollection<string> desiredPassives)
        {
            var result = NewReference("BRED_PAL");
            result["PalInternalName"] = ReadIdentity(value["PalInternalName"] ?? value["PalId"] ?? value["Id"]);
            result["EffectivePassiveInternalNames"] = NamesArray(ReadPassiveNames(value["EffectivePassiveInternalNames"] ?? value["Passives"] ?? value["Traits"]));
            result["Parent1"] = NormalizeReference(value["Parent1"], desiredPassives);
            result["Parent2"] = NormalizeReference(value["Parent2"], desiredPassives);
            result["Gender"] = ReadGender(value["Gender"], "WILDCARD");
            result["PassivesProbability"] = ReadFloat(value["PassivesProbability"] ?? value["TraitsProbability"], 1);
            result["IVs"] = NormalizeIvSet(value["IVs"] as JObject ?? new JObject
            {
                ["HP"] = value["IV_HP"] ?? "any",
                ["Attack"] = value["IV_Attack"] ?? "any",
                ["Defense"] = value["IV_Defense"] ?? "any",
            });
            result["IVsProbability"] = ReadFloat(value["IVsProbability"], 1);
            return result;
        }

        private static JObject NormalizeComposite(JObject value, IReadOnlyCollection<string> desiredPassives)
        {
            var result = NewReference("COMPOSITE_PAL");
            result["Male"] = NormalizeReference(value["Male"], desiredPassives);
            result["Female"] = NormalizeReference(value["Female"], desiredPassives);
            result["Gender"] = ReadGender(value["Gender"], "WILDCARD");
            return result;
        }

        private static JObject NormalizeSurgery(JObject value, IReadOnlyCollection<string> desiredPassives)
        {
            var operations = new JArray();
            foreach (var operation in value["Operations"] as JArray ?? [])
            {
                var item = operation as JObject ?? throw new JsonSerializationException("Legacy surgery operation was not an object.");
                operations.Add(new JObject
                {
                    ["Type"] = ReadString(item["Type"]) ?? throw new JsonSerializationException("Legacy surgery operation has no Type."),
                    ["AddedPassiveInternalName"] = NullableStringToken(ReadPassiveName(item["AddedPassiveInternalName"] ?? item["AddedPassive"])),
                    ["RemovedPassiveInternalName"] = NullableStringToken(ReadPassiveName(item["RemovedPassiveInternalName"] ?? item["RemovedPassive"])),
                });
            }

            var result = NewReference("SURGERY_PAL");
            result["Input"] = NormalizeReference(value["Input"], desiredPassives);
            result["Operations"] = operations;
            return result;
        }

        private static JObject NormalizeInstance(JObject value)
        {
            var location = value["Location"] as JObject;
            return new JObject
            {
                ["InternalName"] = ReadIdentity(value["InternalName"] ?? value["PalInternalName"] ?? value["Id"]),
                ["Location"] = new JObject
                {
                    ["ContainerId"] = NullableStringToken(location?["ContainerId"]),
                    ["Type"] = ReadLocationType(location?["Type"], "Custom"),
                    ["Index"] = ReadInt(location?["Index"], 0),
                },
                ["Gender"] = ReadGender(value["Gender"], "WILDCARD"),
                ["PassiveSkills"] = NamesArray(ReadPassiveNames(value["PassiveSkills"] ?? value["Traits"])),
                ["ActiveSkills"] = NamesArray(ReadNames(value["ActiveSkills"])),
                ["EquippedActiveSkills"] = NamesArray(ReadNames(value["EquippedActiveSkills"])),
                ["OwnerPlayerId"] = NullableStringToken(value["OwnerPlayerId"]),
                ["NickName"] = NullableStringToken(value["NickName"]),
                ["Level"] = ReadInt(value["Level"], 0),
                ["InstanceId"] = NullableStringToken(value["InstanceId"]),
                ["IV_HP"] = ReadInt(value["IV_HP"], 0),
                ["IV_Melee"] = ReadInt(value["IV_Melee"], 0),
                ["IV_Shot"] = ReadInt(value["IV_Shot"], 0),
                ["IV_Defense"] = ReadInt(value["IV_Defense"], 0),
                ["IsOnExpedition"] = ReadBool(value["IsOnExpedition"], false),
            };
        }

        private static JObject NormalizeIvSet(JObject value) => new()
        {
            ["HP"] = NormalizeIv(value?["HP"]),
            ["Attack"] = NormalizeIv(value?["Attack"]),
            ["Defense"] = NormalizeIv(value?["Defense"]),
        };

        private static JObject NormalizeIv(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
                return RandomIv();
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
                return FixedIv(ReadInt(value, 0));
            if (value.Type == JTokenType.String && !string.Equals(value.Value<string>(), "any", StringComparison.OrdinalIgnoreCase))
                return FixedIv(ReadInt(value, 0));
            if (value.Type != JTokenType.Object)
                return RandomIv();

            var obj = (JObject)value;
            return new JObject
            {
                ["IsRandom"] = ReadBool(obj["IsRandom"], false),
                ["IsRelevant"] = ReadBool(obj["IsRelevant"], true),
                ["Min"] = ReadInt(obj["Min"], 0),
                ["Max"] = ReadInt(obj["Max"], 0),
            };
        }

        private static JObject RandomIv() => new()
        {
            ["IsRandom"] = true,
            ["IsRelevant"] = false,
            ["Min"] = 0,
            ["Max"] = 0,
        };

        private static JObject FixedIv(int value) => new()
        {
            ["IsRandom"] = false,
            ["IsRelevant"] = true,
            ["Min"] = value,
            ["Max"] = value,
        };

        private static JObject NewReference(string type) => new()
        {
            ["RefType"] = type,
            ["Instance"] = null,
            ["ActualGender"] = null,
            ["IVs"] = null,
            ["EffectivePassiveInternalNames"] = null,
            ["PalInternalName"] = null,
            ["GuaranteedPassiveInternalNames"] = null,
            ["RandomPassiveCount"] = null,
            ["Gender"] = null,
            ["Parent1"] = null,
            ["Parent2"] = null,
            ["PassivesProbability"] = null,
            ["IVsProbability"] = null,
            ["Male"] = null,
            ["Female"] = null,
            ["Input"] = null,
            ["Operations"] = null,
        };

        private static string ReadIdentity(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
                throw new JsonSerializationException("Legacy document has no pal identity.");
            if (value.Type == JTokenType.String)
                return value.Value<string>();

            if (value is JObject obj)
            {
                if (ReadString(obj["InternalName"]) is { } internalName)
                    return internalName;
                if (obj["Id"] != null)
                    return ResolveLegacyPalInternalName(obj["Id"]);
            }

            return ResolveLegacyPalInternalName(value);
        }

        private static string ResolveLegacyPalInternalName(JToken id)
        {
            var db = PalDB.LoadEmbedded();
            if (id.Type == JTokenType.String)
                return id.Value<string>();

            PalId palId = id.Type switch
            {
                JTokenType.Integer => new PalId { PalDexNo = ReadInt(id, 0) },
                JTokenType.Float => new PalId
                {
                    PalDexNo = (int)Math.Truncate(id.Value<double>()),
                    IsVariant = Math.Abs(id.Value<double>() - Math.Truncate(id.Value<double>()) - 0.1) < 0.000001,
                },
                _ when id is JObject obj && obj["PalDexNo"] != null => new PalId
                {
                    PalDexNo = ReadInt(obj["PalDexNo"], 0),
                    IsVariant = ReadBool(obj["IsVariant"], false),
                },
                _ => null,
            };

            var pal = palId == null
                ? null
                : db.Pals.FirstOrDefault(p => p.Id.PalDexNo == palId.PalDexNo && p.Id.IsVariant == palId.IsVariant);
            return (pal ?? db.Pals.First()).InternalName;
        }

        private static string ReadPassiveName(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
                return null;

            var name = value.Type == JTokenType.String
                ? value.Value<string>()
                : ReadString(value["InternalName"]) ?? ReadString(value["Name"]);
            return name == "(Random)" ? RandomPassiveInternalName : name;
        }

        private static List<string> ReadPassiveNames(JToken value) =>
            ReadNameList(value, ReadPassiveName);

        private static List<string> ReadNames(JToken value) =>
            ReadNameList(value, token => token?.Type == JTokenType.String ? token.Value<string>() : ReadString(token?["InternalName"]));

        private static List<string> ReadNameList(JToken value, Func<JToken, string> readName)
        {
            if (value is not JArray array)
                return [];

            return array.Select(readName).Where(name => name != null).Distinct().ToList();
        }

        private static JArray NamesArray(IEnumerable<string> names) =>
            new(names.Select(name => name == null ? JValue.CreateNull() : new JValue(name)).Distinct());

        private static JArray ReadStringArray(JToken value, params string[] missingDefaults)
        {
            if (value == null)
                return new JArray(missingDefaults.Select(name => new JValue(name)));
            if (value.Type == JTokenType.Null)
                return [];
            if (value is not JArray array)
                throw new JsonSerializationException("Legacy document contains a non-array string collection.");

            return new JArray(
                array
                    .Select(item => item.Type == JTokenType.Null ? JValue.CreateNull() : new JValue(ReadString(item)))
                    .Distinct()
            );
        }

        private static JObject ReadBoolDictionary(JToken value)
        {
            var result = new JObject();
            if (value is not JObject dictionary)
                return result;
            foreach (var property in dictionary.Properties())
                result[property.Name] = ReadBool(property.Value, false);
            return result;
        }

        private static JObject ReadIntDictionary(JToken value, IReadOnlyDictionary<string, int> defaults)
        {
            if (value == null || value.Type == JTokenType.Null)
                return new JObject(defaults.Select(pair => new JProperty(pair.Key, pair.Value)));
            if (value is not JObject dictionary)
                throw new JsonSerializationException("Legacy document contains a non-object integer dictionary.");

            var result = new JObject();
            foreach (var property in dictionary.Properties())
                result[property.Name] = ReadInt(property.Value, 0);
            return result;
        }

        private static JObject ReadNullableIntDictionary(JToken value, IReadOnlyDictionary<string, int?> defaults)
        {
            if (value == null || value.Type == JTokenType.Null)
                return new JObject(defaults.Select(pair => new JProperty(pair.Key, pair.Value.HasValue ? new JValue(pair.Value.Value) : JValue.CreateNull())));
            if (value is not JObject dictionary)
                throw new JsonSerializationException("Legacy document contains a non-object nullable integer dictionary.");

            var result = new JObject();
            foreach (var property in dictionary.Properties())
                result[property.Name] = property.Value.Type == JTokenType.Null ? JValue.CreateNull() : ReadInt(property.Value, 0);
            return result;
        }

        private static JToken NullableStringToken(JToken value) =>
            value == null || value.Type == JTokenType.Null ? JValue.CreateNull() : new JValue(ReadString(value));

        private static JToken ReadLocale(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
                return 1; // TranslationLocale.en in the V1 contract.
            if (value.Type == JTokenType.Integer)
                return value.Value<int>();

            return value.Value<string>() switch
            {
                "de" => 0,
                "en" => 1,
                "es_MX" => 2,
                "es" => 3,
                "fr" => 4,
                "id" => 5,
                "it" => 6,
                "ja" => 7,
                "ko" => 8,
                "pl" => 9,
                "pt_BR" => 10,
                "ru" => 11,
                "th" => 12,
                "tr" => 13,
                "vi" => 14,
                "zh_Hans" => 15,
                "zh_Hant" => 16,
                _ => 1,
            };
        }

        private static string ReadGender(JToken value, string fallback)
        {
            if (value == null || value.Type == JTokenType.Null)
                return fallback;
            if (value.Type == JTokenType.String)
                return value.Value<string>();

            return ReadInt(value, 3) switch
            {
                1 => "MALE",
                2 => "FEMALE",
                3 => "WILDCARD",
                4 => "OPPOSITE_WILDCARD",
                8 => "NONE",
                _ => fallback,
            };
        }

        private static string ReadLocationType(JToken value, string fallback)
        {
            if (value == null || value.Type == JTokenType.Null)
                return fallback;
            if (value.Type == JTokenType.String)
                return value.Value<string>();

            return ReadInt(value, 4) switch
            {
                0 => "PlayerParty",
                1 => "Palbox",
                2 => "Base",
                3 => "ViewingCage",
                4 => "Custom",
                5 => "DimensionalPalStorage",
                6 => "GlobalPalStorage",
                _ => fallback,
            };
        }

        private static string ReadString(JToken value) =>
            value == null || value.Type == JTokenType.Null ? null : value.Value<string>();

        private static int ReadInt(JToken value, int fallback)
        {
            if (value == null || value.Type == JTokenType.Null)
                return fallback;
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
                return value.Value<int>();
            return int.TryParse(value.Value<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : fallback;
        }

        private static double ReadDouble(JToken value, double fallback)
        {
            if (value == null || value.Type == JTokenType.Null)
                return fallback;
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
                return value.Value<double>();
            return double.TryParse(value.Value<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? result
                : fallback;
        }

        private static float ReadFloat(JToken value, float fallback)
        {
            if (value == null || value.Type == JTokenType.Null)
                return fallback;
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
                return value.Value<float>();
            return float.TryParse(value.Value<string>(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? result
                : fallback;
        }

        private static bool ReadBool(JToken value, bool fallback)
        {
            if (value == null || value.Type == JTokenType.Null)
                return fallback;
            if (value.Type == JTokenType.Boolean)
                return value.Value<bool>();
            return bool.TryParse(value.Value<string>(), out var result) ? result : fallback;
        }

        private static int ReadDurationSeconds(JObject value, int fallback)
        {
            if (value == null)
                return fallback;
            if (value["BreedingTimeSeconds"] != null)
                return ReadInt(value["BreedingTimeSeconds"], fallback);
            if (value["BreedingTimeMinutes"] != null)
                return ReadInt(value["BreedingTimeMinutes"], fallback / 60) * 60;
            return ReadDuration(value["BreedingTime"], fallback, seconds: true);
        }

        private static int ReadDurationMinutes(JToken value, int fallback)
        {
            if (value == null || value.Type == JTokenType.Null)
                return fallback;
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
                return value.Value<int>();
            return TimeSpan.TryParse(value.Value<string>(), CultureInfo.InvariantCulture, out var duration)
                ? (int)duration.TotalMinutes
                : fallback;
        }

        private static int ReadDuration(JToken value, int fallback, bool seconds)
        {
            if (value == null || value.Type == JTokenType.Null)
                return fallback;
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
                return value.Value<int>() * (seconds ? 1 : 60);
            return TimeSpan.TryParse(value.Value<string>(), CultureInfo.InvariantCulture, out var duration)
                ? (int)(seconds ? duration.TotalSeconds : duration.TotalMinutes)
                : fallback;
        }

        private static string TargetFileName(string id)
        {
            if (string.IsNullOrWhiteSpace(id)
                || id != Path.GetFileName(id)
                || id.Contains(Path.DirectorySeparatorChar)
                || id.Contains(Path.AltDirectorySeparatorChar))
            {
                throw new StorageFormatException($"Invalid target ID '{id}'.");
            }

            return id.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? id : id + ".json";
        }

    }
}
