using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PalCalc.Model.BreedingResult;

namespace PalCalc.Model
{
    public class PalContainerJsonConverter : JsonConverter<IPalContainer>
    {
        public override IPalContainer ReadJson(JsonReader reader, Type objectType, IPalContainer existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);
            switch (token["Type"].ToObject<LocationType>())
            {
                case LocationType.Palbox: return token.ToObject<PalboxPalContainer>();
                case LocationType.Base: return token.ToObject<BasePalContainer>();
                case LocationType.PlayerParty: return token.ToObject<PlayerPartyContainer>();
                case LocationType.ViewingCage: return token.ToObject<ViewingCageContainer>();
                case LocationType.DimensionalPalStorage: return token.ToObject<DimensionalPalStorageContainer>();
                case LocationType.GlobalPalStorage: return token.ToObject<GlobalPalStorageContainer>();
                default: throw new NotImplementedException();
            }
        }

        public override void WriteJson(JsonWriter writer, IPalContainer value, JsonSerializer serializer)
        {
            var baseResult = JToken.FromObject(value);
            baseResult["Type"] = JToken.FromObject(value.Type);

            baseResult.WriteTo(writer);
        }
    }

    public class PalInstanceJsonConverter : JsonConverter<PalInstance>
    {
        private PalDB db;
        public PalInstanceJsonConverter(PalDB db)
        {
            this.db = db;
        }

        public override PalInstance ReadJson(JsonReader reader, Type objectType, PalInstance existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);
            var passives = token["PassiveSkills"] ?? token["Traits"];
            return new PalInstance()
            {
                // TODO - apparently paldex num. can change, should switch to internal pal name
                Pal = token["Id"].ToObject<PalId>().ToPal(db),
                Location = token["Location"].ToObject<PalLocation>(),
                Gender = token["Gender"].ToObject<PalGender>(),
                PassiveSkills = passives.ToObject<List<string>>().Select(s => s.ToStandardPassive(db)).ToList(),
                ActiveSkills = (token["ActiveSkills"]?.ToObject<List<string>>() ?? []).Select(s => s.ToActive(db)).ToList(),
                EquippedActiveSkills = (token["EquippedActiveSkills"]?.ToObject<List<string>>() ?? []).Select(s => s.ToActive(db)).ToList(),
                OwnerPlayerId = token["OwnerPlayerId"].ToObject<string>(),
                NickName = token["NickName"].ToObject<string>(),
                Level = token["Level"].ToObject<int>(),
                InstanceId = token["InstanceId"].ToObject<string>(),
                IV_HP = token["IV_HP"]?.ToObject<int>() ?? 0,
                IV_Melee = token["IV_Melee"]?.ToObject<int>() ?? 0,
                IV_Shot = token["IV_Shot"]?.ToObject<int>() ?? 0,
                IV_Defense = token["IV_Defense"]?.ToObject<int>() ?? 0,
            };
        }

        public override void WriteJson(JsonWriter writer, PalInstance value, JsonSerializer serializer)
        {
            JToken.FromObject(new
            {
                Id = value.Pal.Id,
                Location = value.Location,
                Gender = value.Gender,
                PassiveSkills = value.PassiveSkills.Select(t => t.InternalName),
                ActiveSkills = value.ActiveSkills?.Select(s => s.InternalName) ?? [],
                EquippedActiveSkills = value.EquippedActiveSkills?.Select(s => s.InternalName) ?? [],
                OwnerPlayerId = value.OwnerPlayerId,
                NickName = value.NickName,
                Level = value.Level,
                InstanceId = value.InstanceId,
                IV_HP = value.IV_HP,
                IV_Melee = value.IV_Melee,
                IV_Shot = value.IV_Shot,
                IV_Defense = value.IV_Defense,
            }).WriteTo(writer);
        }
    }

    public class BreedingResultJsonConverter : JsonConverter<BreedingResult>
    {
        private IEnumerable<Pal> pals;

        public BreedingResultJsonConverter(IEnumerable<Pal> pals)
        {
            this.pals = pals;
        }

        public override BreedingResult ReadJson(JsonReader reader, Type objectType, BreedingResult existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);
            return new BreedingResult()
            {
                Parent1 = new GenderedPal()
                {
                    Pal = token["Parent1ID"].ToObject<PalId>().ToPal(pals),
                    Gender = token["Parent1Gender"].ToObject<PalGender>(),
                },
                Parent2 = new GenderedPal()
                {
                    Pal = token["Parent2ID"].ToObject<PalId>().ToPal(pals),
                    Gender = token["Parent2Gender"].ToObject<PalGender>(),
                },
                Child = token["ChildID"].ToObject<PalId>().ToPal(pals)
            };
        }

        public override void WriteJson(JsonWriter writer, BreedingResult value, JsonSerializer serializer)
        {
            JToken.FromObject(new
            {
                Parent1ID = value.Parent1.Pal.Id,
                Parent1Gender = value.Parent1.Gender,
                Parent2ID = value.Parent2.Pal.Id,
                Parent2Gender = value.Parent2.Gender,
                ChildID = value.Child.Id,
            }).WriteTo(writer);
        }
    }

    public class PalDBJsonConverter : JsonConverter<PalDB>
    {
        public override PalDB ReadJson(JsonReader reader, Type objectType, PalDB existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var asToken = JToken.ReadFrom(reader);

            var version = asToken["Version"]?.ToObject<string>();
            var pals = asToken["Pals"].ToObject<List<Pal>>();
            var humans = asToken["Humans"].ToObject<List<string>>();
            var passives = asToken["PassiveSkills"].ToObject<List<PassiveSkill>>();
            var attacks = asToken["ActiveSkills"].ToObject<List<ActiveSkill>>();
            var elements = asToken["Elements"].ToObject<List<PalElement>>();
            var breedingGenderProbability = asToken["BreedingGenderProbability"].ToObject<Dictionary<string, Dictionary<PalGender, float>>>();

            foreach (var attack in attacks)
                attack.Element = elements.Single(e => e.InternalName == attack.ElementInternalName);

            return new PalDB()
            {
                Version = version,
                PalsById = pals.ToDictionary(p => p.Id),
                Humans = humans.Select(n => new Human(n)).ToList(),
                PassiveSkills = passives,
                ActiveSkills = attacks,
                Elements = elements,

                BreedingGenderProbability = breedingGenderProbability.ToDictionary(
                    kvp => kvp.Key.InternalToPal(pals),
                    kvp => kvp.Value
                ),
            };
        }

        public override void WriteJson(JsonWriter writer, PalDB value, JsonSerializer serializer)
        {
            JToken.FromObject(new
            {
                Version = value.Version,
                Pals = value.Pals,
                Humans = value.Humans.Select(h => h.InternalName),
                PassiveSkills = value.PassiveSkills,
                ActiveSkills = value.ActiveSkills,
                Elements = value.Elements,
                BreedingGenderProbability = value.BreedingGenderProbability.ToDictionary(kvp => kvp.Key.InternalName, kvp => kvp.Value),
            }).WriteTo(writer);
        }
    }

    public class PalBreedingDBJsonConverter(PalDB paldb) : JsonConverter<PalBreedingDB>
    {
        public override PalBreedingDB ReadJson(JsonReader reader, Type objectType, PalBreedingDB existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var asToken = JToken.ReadFrom(reader);

            var minBreedingSteps = asToken["MinBreedingSteps"].ToObject<Dictionary<string, Dictionary<string, int>>>();

            serializer.Converters.Add(new BreedingResultJsonConverter(paldb.Pals));
            var breeding = asToken["Breeding"].ToObject<List<BreedingResult>>(serializer);

            return new PalBreedingDB(paldb)
            {
                Breeding = breeding,

                MinBreedingSteps = minBreedingSteps.ToDictionary(
                    kvp => kvp.Key.InternalToPal(paldb.Pals),
                    kvp => kvp.Value.ToDictionary(
                        ikvp => ikvp.Key.InternalToPal(paldb.Pals),
                        ikvp => ikvp.Value
                    )
                ),
            };
        }

        public override void WriteJson(JsonWriter writer, PalBreedingDB value, JsonSerializer serializer)
        {
            var breedingResultConverter = new BreedingResultJsonConverter(paldb.Pals);
            serializer.Converters.Add(breedingResultConverter);

            var breedingToken = JToken.FromObject(value.Breeding, serializer);
            JToken.FromObject(new
            {
                Breeding = breedingToken,
                MinBreedingSteps = value.MinBreedingSteps.ToDictionary(
                    kvp => kvp.Key.InternalName,
                    kvp => kvp.Value.ToDictionary(
                        ikvp => ikvp.Key.InternalName,
                        ikvp => ikvp.Value
                    )
                )
            }).WriteTo(writer, breedingResultConverter);
        }
    }
}
