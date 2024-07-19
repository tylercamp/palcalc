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
            return new PalInstance()
            {
                Pal = token["Id"].ToObject<PalId>().ToPal(db),
                Location = token["Location"].ToObject<PalLocation>(),
                Gender = token["Gender"].ToObject<PalGender>(),
                Traits = token["Traits"].ToObject<List<string>>().Select(s => s.ToTrait(db)).ToList(),
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
                Traits = value.Traits.Select(t => t.InternalName),
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
            var traits = asToken["Traits"].ToObject<List<Trait>>();
            var breedingGenderProbability = asToken["BreedingGenderProbability"].ToObject<Dictionary<string, Dictionary<PalGender, float>>>();
            var minBreedingSteps = asToken["MinBreedingSteps"].ToObject<Dictionary<string, Dictionary<string, int>>>();

            serializer.Converters.Add(new BreedingResultJsonConverter(pals));
            var breeding = asToken["Breeding"].ToObject<List<BreedingResult>>(serializer);

            return new PalDB()
            {
                Version = version,
                PalsById = pals.ToDictionary(p => p.Id),
                Traits = traits,
                Breeding = breeding,

                MinBreedingSteps = minBreedingSteps.ToDictionary(
                    kvp => kvp.Key.ToPal(pals),
                    kvp => kvp.Value.ToDictionary(
                        ikvp => ikvp.Key.ToPal(pals),
                        ikvp => ikvp.Value
                    )
                ),

                BreedingGenderProbability = breedingGenderProbability.ToDictionary(
                    kvp => kvp.Key.ToPal(pals),
                    kvp => kvp.Value
                ),
            };
        }

        public override void WriteJson(JsonWriter writer, PalDB value, JsonSerializer serializer)
        {
            var breedingResultConverter = new BreedingResultJsonConverter(value.Pals);
            serializer.Converters.Add(breedingResultConverter);

            var breedingToken = JToken.FromObject(value.Breeding, serializer);
            JToken.FromObject(new
            {
                Version = value.Version,
                Pals = value.Pals,
                Breeding = breedingToken,
                Traits = value.Traits,
                BreedingGenderProbability = value.BreedingGenderProbability.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value),
                MinBreedingSteps = value.MinBreedingSteps.ToDictionary(
                    kvp => kvp.Key.Name,
                    kvp => kvp.Value.ToDictionary(
                        ikvp => ikvp.Key.Name,
                        ikvp => ikvp.Value
                    )
                )
            }).WriteTo(writer, breedingResultConverter);
        }
    }
}
