using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB
{
    internal class ParseSaveFileProgram
    {
        class PalContainer
        {
            public string Id { get; set; }
            public int MaxEntries { get; set; }
            public int NumEntries { get; set; }

            public override string ToString() => $"{Id} ({NumEntries}/{MaxEntries})";
        }

        class JsonPalInstance
        {
            public string PalType { get; set; } // internal name
            public PalGender Gender { get; set; }
            public List<string> PassiveSkills { get; set; }
            public string ContainerId { get; set; }
            public int SlotIndex { get; set; }

            public override string ToString() => $"{PalType} {Gender} ({string.Join(", ", PassiveSkills)}) in {ContainerId} at {SlotIndex}";
        }

        static void Main(string[] args)
        {
            var db = PalDB.FromJson(File.ReadAllText("db.json"));

            // https://github.com/cheahjs/palworld-save-tools
            var json = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("ref/Level.sav.json"));
            Console.WriteLine("Loaded JSON");

            var containers = json["properties"]["worldSaveData"]["value"]["CharacterContainerSaveData"]["value"]
                .ToObject<JArray>()
                .Select(jcontainer => new PalContainer
                {
                    Id = (string)jcontainer["key"]["ID"]["value"],
                    MaxEntries = jcontainer["value"]["Slots"]["value"]["values"].ToObject<JArray>().Count,
                    NumEntries = jcontainer["value"]["Slots"]["value"]["values"].ToObject<JArray>().Where(jentry => (string)jentry["RawData"]["value"]["instance_id"] != "00000000-0000-0000-0000-000000000000").Count()
                })
                .ToList();
            Console.WriteLine(string.Join("\n", containers));

            var palInstances = json["properties"]["worldSaveData"]["value"]["CharacterSaveParameterMap"]["value"]
                .ToObject<JArray>()
                .Where(jentry => (string)jentry["value"]["RawData"]["value"]["object"]["SaveParameter"]["struct_type"] == "PalIndividualCharacterSaveParameter")
                .Select(jentry => jentry["value"]["RawData"]["value"]["object"]["SaveParameter"]["value"])
                .Where(jentry => !((bool?)jentry["IsPlayer"]?["value"] == true))
                // for captured humans
                .Where(jentry =>
                {
                    var palType = (string)jentry["CharacterID"]["value"];

                    return !(
                        palType.StartsWith("Hunter_") ||
                        palType.Contains("Sales")
                    );
                })
                .Select(jentry =>
                {
                    var pt = (string)jentry["CharacterID"]["value"];
                    var g = ((string)jentry["Gender"]["value"]["value"]).Contains("Female") ? PalGender.FEMALE : PalGender.MALE;
                    var ps = jentry["PassiveSkillList"]?["value"]?["values"]?.ToObject<List<string>>() ?? new List<string>();
                    var cid = (string)jentry["SlotID"]["value"]["ContainerId"]["value"]["ID"]["value"];
                    var si = (int)jentry["SlotID"]["value"]["SlotIndex"]["value"];
                    return new JsonPalInstance
                    {
                        PalType = pt.TrimStart().Replace("BOSS_", ""),
                        Gender = g,
                        PassiveSkills = ps,
                        ContainerId = cid,
                        SlotIndex = si
                    };
                })
                .ToList();

            Console.WriteLine(string.Join("\n", palInstances));
            Console.WriteLine($"{palInstances.Count} total");

            var partyContainer = containers[0];
            var boxContainer = containers[1];
            var baseContainers = containers.Skip(2).ToList();

            PalLocation LocationOf(JsonPalInstance jpi)
            {
                LocationType palLocationType;

                var container = containers.Single(c => c.Id == jpi.ContainerId);
                if (container == partyContainer) palLocationType = LocationType.PlayerParty;
                else if (container == boxContainer) palLocationType = LocationType.Palbox;
                else if (baseContainers.Any(bc => bc.Id == jpi.ContainerId)) palLocationType = LocationType.Base;
                else throw new Exception("Could not determine container type for ID " + jpi.ContainerId);

                return new PalLocation { Type = palLocationType, Index = jpi.SlotIndex };
            }

            File.WriteAllText("savegame.json", PalInstance.ListToJson(palInstances.Select(jpi => new PalInstance
            {
                Pal = db.Pals.Single(p => p.InternalName == jpi.PalType),
                Gender = jpi.Gender,
                Traits = jpi.PassiveSkills.Select(n => db.Traits.Single(t => t.InternalName == n)).ToList(),
                Location = LocationOf(jpi)
            }).ToList()));
        }
    }
}
