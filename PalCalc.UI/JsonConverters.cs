using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using static PalCalc.Model.BreedingResult;

namespace PalCalc.UI
{
    internal abstract class PalConverterBase<T> : JsonConverter<T>
    {
        protected PalDB db;
        protected GameSettings gameSettings;
        protected JsonConverter[] dependencyConverters;
        public PalConverterBase(PalDB db, GameSettings gameSettings)
        {
            this.db = db;
            this.gameSettings = gameSettings;

            dependencyConverters = Array.Empty<JsonConverter>();
        }

        protected void InjectDependencyConverters(JsonSerializer serializer)
        {
            if (dependencyConverters == null) return;

            foreach (var d in dependencyConverters)
            {
                if (!serializer.Converters.Any(c => c.GetType() == d.GetType()))
                    serializer.Converters.Add(d);
            }
        }

        public sealed override T ReadJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            InjectDependencyConverters(serializer);
            return ReadTypeJson(reader, objectType, existingValue, hasExistingValue, serializer);
        }

        public sealed override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
        {
            InjectDependencyConverters(serializer);
            WriteTypeJson(writer, value, serializer);
        }

        protected abstract T ReadTypeJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, JsonSerializer serializer);
        protected abstract void WriteTypeJson(JsonWriter writer, T value, JsonSerializer serializer);
    }

    #region Model Converters
    internal class TraitConverter : PalConverterBase<Trait>
    {
        public TraitConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings)
        {
        }

        protected override Trait ReadTypeJson(JsonReader reader, Type objectType, Trait existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);
            var traitInternalName = token.ToObject<string>();
            return traitInternalName != null
                ? traitInternalName.InternalToTrait(db)
                : null;
        }

        protected override void WriteTypeJson(JsonWriter writer, Trait value, JsonSerializer serializer)
        {
            JToken.FromObject(value.InternalName, serializer).WriteTo(writer, dependencyConverters);
        }
    }

    internal abstract class IPalReferenceConverterBase<T> : PalConverterBase<T>
    {
        private string typeLabel;

        protected IPalReferenceConverterBase(PalDB db, GameSettings gameSettings, string refTypeLabel) : base(db, gameSettings)
        {
            typeLabel = refTypeLabel;
        }

        public string TypeLabel => typeLabel;

        protected sealed override T ReadTypeJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var wrapperToken = JToken.ReadFrom(reader);

            var actualLabel = PalReferenceConverter.ReadWrappedTypeLabel(wrapperToken);
            if (actualLabel != typeLabel)
            {
                throw new Exception($"Stored type label '{actualLabel}' did not match expected label of '{typeLabel}'");
            }

            var wrappedContent = PalReferenceConverter.ReadWrappedContent(wrapperToken);
            return ReadRefJson(wrappedContent, objectType, existingValue, hasExistingValue, serializer);
        }

        protected sealed override void WriteTypeJson(JsonWriter writer, T value, JsonSerializer serializer)
        {
            PalReferenceConverter
                .MakeWrappedToken(typeLabel, MakeRefJson(value, serializer))
                .WriteTo(writer, dependencyConverters);
        }

        internal abstract T ReadRefJson(JToken token, Type objectType, T existingValue, bool hasExistingValue, JsonSerializer serializer);
        internal abstract JToken MakeRefJson(T value, JsonSerializer serializer);
    }

    internal class PalReferenceConverter : PalConverterBase<IPalReference>
    {
        OwnedPalReferenceConverter oprc;
        WildPalReferenceConverter wprc;
        BredPalReferenceConverter bprc;

        public PalReferenceConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings)
        {
            this.oprc = new OwnedPalReferenceConverter(db, gameSettings);
            this.wprc = new WildPalReferenceConverter(db, gameSettings);
            this.bprc = new BredPalReferenceConverter(db, gameSettings, this);
        }

        public static string ReadWrappedTypeLabel(JToken wrapperToken) => wrapperToken["RefType"].ToObject<string>();
        public static JToken ReadWrappedContent(JToken wrapperToken) => wrapperToken["Content"];
        public static JToken MakeWrappedToken(string typeLabel, JToken value)
        {
            return JToken.FromObject(new
            {
                RefType = typeLabel,
                Content = value
            });
        }

        protected override IPalReference ReadTypeJson(JsonReader reader, Type objectType, IPalReference existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);
            var type = ReadWrappedTypeLabel(token);
            var wrappedContent = ReadWrappedContent(token);

            if (type == oprc.TypeLabel) return oprc.ReadRefJson(wrappedContent, objectType, existingValue as OwnedPalReference, hasExistingValue, serializer);
            if (type == wprc.TypeLabel) return wprc.ReadRefJson(wrappedContent, objectType, existingValue as WildPalReference, hasExistingValue, serializer);
            if (type == bprc.TypeLabel) return bprc.ReadRefJson(wrappedContent, objectType, existingValue as BredPalReference, hasExistingValue, serializer);

            throw new Exception($"Unhandled IPalReference type label {type}");
        }

        protected override void WriteTypeJson(JsonWriter writer, IPalReference value, JsonSerializer serializer)
        {
            switch (value)
            {
                case OwnedPalReference opr: oprc.WriteJson(writer, opr, serializer); break;
                case WildPalReference wpr: wprc.WriteJson(writer, wpr, serializer); break;
                case BredPalReference bpr: bprc.WriteJson(writer, bpr, serializer); break;
                default: throw new Exception($"Unhandled IPalReference type {value?.GetType()?.Name}");
            }
        }
    }

    internal class OwnedPalReferenceConverter : IPalReferenceConverterBase<OwnedPalReference>
    {
        public OwnedPalReferenceConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings, "OWNED_PAL")
        {
            dependencyConverters = new JsonConverter[]
            {
                new PalInstanceJsonConverter(db),
            };
        }

        internal override JToken MakeRefJson(OwnedPalReference value, JsonSerializer serializer)
        {
            InjectDependencyConverters(serializer);
            return JToken.FromObject(value.UnderlyingInstance, serializer);
        }

        internal override OwnedPalReference ReadRefJson(JToken token, Type objectType, OwnedPalReference existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            InjectDependencyConverters(serializer);
            return new OwnedPalReference(token.ToObject<PalInstance>(serializer));
        }
    }

    internal class WildPalReferenceConverter : IPalReferenceConverterBase<WildPalReference>
    {
        public WildPalReferenceConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings, "WILD_PAL")
        {
        }

        internal override JToken MakeRefJson(WildPalReference value, JsonSerializer serializer)
        {
            return JToken.FromObject(new
            {
                PalId = value.Pal.Id,
                NumTraits = value.Traits.Count,
            }, serializer);
        }

        internal override WildPalReference ReadRefJson(JToken token, Type objectType, WildPalReference existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var pal = token["PalId"].ToObject<PalId>(serializer).ToPal(db);
            var numTraits = token["NumTraits"].ToObject<int>();

            return new WildPalReference(pal, numTraits);
        }
    }

    internal class BredPalReferenceConverter : IPalReferenceConverterBase<BredPalReference>
    {
        public BredPalReferenceConverter(PalDB db, GameSettings gameSettings, PalReferenceConverter genericConverter) : base(db, gameSettings, "BRED_PAL")
        {
            dependencyConverters = new JsonConverter[]
            {
                genericConverter,
                new TraitConverter(db, gameSettings),
            };
        }

        internal override BredPalReference ReadRefJson(JToken token, Type objectType, BredPalReference existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            InjectDependencyConverters(serializer);
            var pal = token["PalId"].ToObject<PalId>(serializer).ToPal(db);
            var traits = token["Traits"].ToObject<List<Trait>>(serializer);
            var parent1 = token["Parent1"].ToObject<IPalReference>(serializer);
            var parent2 = token["Parent2"].ToObject<IPalReference>(serializer);
            var gender = token["Gender"].ToObject<PalGender>(serializer);
            var traitsProbability = token["TraitsProbability"].ToObject<float>(serializer);

            return new BredPalReference(gameSettings, pal, parent1, parent2, traits, traitsProbability).WithGuaranteedGender(db, gender) as BredPalReference;
        }

        internal override JToken MakeRefJson(BredPalReference value, JsonSerializer serializer)
        {
            InjectDependencyConverters(serializer);
            return JToken.FromObject(new
            {
                PalId = value.Pal.Id,
                Traits = value.Traits,
                Parent1 = value.Parent1,
                Parent2 = value.Parent2,
                Gender = value.Gender,
                TraitsProbability = value.TraitsProbability,
            }, serializer);
        }
    }
    #endregion

    #region ViewModel Converters
    internal class PalViewModelConverter : PalConverterBase<PalViewModel>
    {
        public PalViewModelConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings)
        {
        }

        protected override PalViewModel ReadTypeJson(JsonReader reader, Type objectType, PalViewModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var palId = JToken.ReadFrom(reader).ToObject<PalId>(serializer);
            return new PalViewModel(palId.ToPal(db));
        }

        protected override void WriteTypeJson(JsonWriter writer, PalViewModel value, JsonSerializer serializer)
        {
            JToken.FromObject(value.ModelObject.Id, serializer).WriteTo(writer, dependencyConverters);
        }
    }

    internal class TraitViewModelConverter : PalConverterBase<TraitViewModel>
    {
        public TraitViewModelConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings)
        {
            dependencyConverters = new JsonConverter[]
            {
                new TraitConverter(db, gameSettings),
            };
        }

        protected override TraitViewModel ReadTypeJson(JsonReader reader, Type objectType, TraitViewModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var trait = JToken.ReadFrom(reader).ToObject<Trait>(serializer);
            return trait != null
                ? new TraitViewModel(trait)
                : null;
        }

        protected override void WriteTypeJson(JsonWriter writer, TraitViewModel value, JsonSerializer serializer)
        {
            JToken.FromObject(value.ModelObject, serializer).WriteTo(writer, dependencyConverters);
        }
    }

    internal class PalSpecifierViewModelConverter : PalConverterBase<PalSpecifierViewModel>
    {
        public PalSpecifierViewModelConverter(PalDB db, GameSettings gameSettings, CachedSaveGame source) : base(db, gameSettings)
        {
            dependencyConverters = new JsonConverter[]
            {
                new PalViewModelConverter(db, gameSettings),
                new TraitViewModelConverter(db, gameSettings),
                new BreedingResultListViewModelConverter(db, gameSettings, source),
            };
        }

        protected override PalSpecifierViewModel ReadTypeJson(JsonReader reader, Type objectType, PalSpecifierViewModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JToken.ReadFrom(reader);
            return new PalSpecifierViewModel()
            {
                TargetPal = obj["TargetPal"].ToObject<PalViewModel>(serializer),
                Trait1 = obj["Trait1"].ToObject<TraitViewModel>(serializer),
                Trait2 = obj["Trait2"].ToObject<TraitViewModel>(serializer),
                Trait3 = obj["Trait3"].ToObject<TraitViewModel>(serializer),
                Trait4 = obj["Trait4"].ToObject<TraitViewModel>(serializer),
                CurrentResults = obj["CurrentResults"].ToObject<BreedingResultListViewModel>(serializer)
            };
        }

        protected override void WriteTypeJson(JsonWriter writer, PalSpecifierViewModel value, JsonSerializer serializer)
        {
            JToken.FromObject(new
            {
                TargetPal = value.TargetPal,
                Trait1 = value.Trait1,
                Trait2 = value.Trait2,
                Trait3 = value.Trait3,
                Trait4 = value.Trait4,
                CurrentResults = value.CurrentResults
            }, serializer).WriteTo(writer, dependencyConverters);
        }
    }

    internal class BreedingResultViewModelConverter : PalConverterBase<BreedingResultViewModel>
    {
        private CachedSaveGame source;
        public BreedingResultViewModelConverter(PalDB db, GameSettings gameSettings, CachedSaveGame source) : base(db, gameSettings)
        {
            dependencyConverters = new JsonConverter[]
            {
                new PalReferenceConverter(db, gameSettings),
            };

            this.source = source;
        }

        protected override BreedingResultViewModel ReadTypeJson(JsonReader reader, Type objectType, BreedingResultViewModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var palRef = JToken.ReadFrom(reader).ToObject<IPalReference>(serializer);
            return new BreedingResultViewModel(source, palRef);
        }

        protected override void WriteTypeJson(JsonWriter writer, BreedingResultViewModel value, JsonSerializer serializer)
        {
            JToken.FromObject(value.DisplayedResult, serializer).WriteTo(writer, dependencyConverters);
        }
    }

    internal class BreedingResultListViewModelConverter : PalConverterBase<BreedingResultListViewModel>
    {
        public BreedingResultListViewModelConverter(PalDB db, GameSettings gameSettings, CachedSaveGame source) : base(db, gameSettings)
        {
            dependencyConverters = new JsonConverter[]
            {
                new BreedingResultViewModelConverter(db, gameSettings, source),
            };
        }

        protected override BreedingResultListViewModel ReadTypeJson(JsonReader reader, Type objectType, BreedingResultListViewModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var resultsToken = JToken.ReadFrom(reader)["Results"];
            return new BreedingResultListViewModel() { Results = resultsToken.ToObject<List<BreedingResultViewModel>>(serializer) };
        }

        protected override void WriteTypeJson(JsonWriter writer, BreedingResultListViewModel value, JsonSerializer serializer)
        {
            JToken.FromObject(new { Results = value.Results }, serializer).WriteTo(writer, dependencyConverters);
        }
    }

    internal class PalTargetListViewModelConverter : PalConverterBase<PalTargetListViewModel>
    {
        public PalTargetListViewModelConverter(PalDB db, GameSettings gameSettings, CachedSaveGame source) : base(db, gameSettings)
        {
            dependencyConverters = new JsonConverter[]
            {
                new PalSpecifierViewModelConverter(db, gameSettings, source),
            };
        }

        protected override PalTargetListViewModel ReadTypeJson(JsonReader reader, Type objectType, PalTargetListViewModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var specifiers = JToken.ReadFrom(reader)["Targets"].ToObject<List<PalSpecifierViewModel>>(serializer);
            return new PalTargetListViewModel(specifiers);
        }

        protected override void WriteTypeJson(JsonWriter writer, PalTargetListViewModel value, JsonSerializer serializer)
        {
            JToken
                .FromObject(new { Targets = value.Targets.Where(t => !t.IsReadOnly).ToList() }, serializer)
                .WriteTo(writer, dependencyConverters);
        }
    }
    #endregion
}
