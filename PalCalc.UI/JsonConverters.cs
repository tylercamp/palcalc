using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    // not meant to be used, just for debugging to ensure localized text isn't serialized
    internal class ILocalizedTextConverter : PalConverterBase<ILocalizedText>
    {
        public ILocalizedTextConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings)
        {
        }

        protected override ILocalizedText ReadTypeJson(JsonReader reader, Type objectType, ILocalizedText existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
#if DEBUG
            Debugger.Break();
#endif
            return null;
        }

        protected override void WriteTypeJson(JsonWriter writer, ILocalizedText value, JsonSerializer serializer)
        {
#if DEBUG
            Debugger.Break();
#endif
        }
    }

    #region Model Converters
    internal class PassiveSkillConverter : PalConverterBase<PassiveSkill>
    {
        public PassiveSkillConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings)
        {
        }

        protected override PassiveSkill ReadTypeJson(JsonReader reader, Type objectType, PassiveSkill existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);
            var passiveInternalName = token.ToObject<string>();
            return passiveInternalName != null
                ? passiveInternalName.InternalToPassive(db)
                : null;
        }

        protected override void WriteTypeJson(JsonWriter writer, PassiveSkill value, JsonSerializer serializer)
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
        CompositePalReferenceConverter cprc;

        public PalReferenceConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings)
        {
            this.oprc = new OwnedPalReferenceConverter(db, gameSettings);
            this.wprc = new WildPalReferenceConverter(db, gameSettings);
            this.bprc = new BredPalReferenceConverter(db, gameSettings, this);
            this.cprc = new CompositePalReferenceConverter(db, gameSettings);
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
            if (type == cprc.TypeLabel) return cprc.ReadRefJson(wrappedContent, objectType, existingValue as CompositeOwnedPalReference, hasExistingValue, serializer);

            throw new Exception($"Unhandled IPalReference type label {type}");
        }

        protected override void WriteTypeJson(JsonWriter writer, IPalReference value, JsonSerializer serializer)
        {
            switch (value)
            {
                case OwnedPalReference opr: oprc.WriteJson(writer, opr, serializer); break;
                case WildPalReference wpr: wprc.WriteJson(writer, wpr, serializer); break;
                case BredPalReference bpr: bprc.WriteJson(writer, bpr, serializer); break;
                case CompositeOwnedPalReference cpr: cprc.WriteJson(writer, cpr, serializer); break;
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
                new ILocalizedTextConverter(db, gameSettings),
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
            var inst = token.ToObject<PalInstance>(serializer);
            return new OwnedPalReference(
                inst,
                // supposed to be "effective passives", but that only matters when the solver is running, and this is a saved solver result
                inst.PassiveSkills,
                new IV_Set()
                {
                    // (isRelevant only matters while the solver is running)
                    HP = new IV_Range(isRelevant: true, inst.IV_HP),
                    Attack = new IV_Range(isRelevant: true, inst.IV_Attack),
                    Defense = new IV_Range(isRelevant: true, inst.IV_Defense)
                }
            );
        }
    }

    internal class CompositePalReferenceConverter : IPalReferenceConverterBase<CompositeOwnedPalReference>
    {
        public CompositePalReferenceConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings, "COMPOSITE_PAL")
        {
            dependencyConverters = new JsonConverter[]
            {
                new OwnedPalReferenceConverter(db, gameSettings),
                new ILocalizedTextConverter(db, gameSettings),
            };
        }

        internal override JToken MakeRefJson(CompositeOwnedPalReference value, JsonSerializer serializer)
        {
            InjectDependencyConverters(serializer);
            return JToken.FromObject(new
            {
                Male = value.Male,
                Female = value.Female,
                Gender = value.Gender,
            }, serializer);
        }

        internal override CompositeOwnedPalReference ReadRefJson(JToken token, Type objectType, CompositeOwnedPalReference existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            InjectDependencyConverters(serializer);
            var male = token["Male"].ToObject<OwnedPalReference>(serializer);
            var female = token["Female"].ToObject<OwnedPalReference>(serializer);
            var gender = token["Gender"]?.ToObject<PalGender>(serializer) ?? PalGender.WILDCARD;

            return (CompositeOwnedPalReference)new CompositeOwnedPalReference(male, female).WithGuaranteedGender(db, gender);
        }
    }

    internal class WildPalReferenceConverter : IPalReferenceConverterBase<WildPalReference>
    {
        public WildPalReferenceConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings, "WILD_PAL")
        {
            dependencyConverters = [
                new ILocalizedTextConverter(db, gameSettings),
            ];
        }

        internal override JToken MakeRefJson(WildPalReference value, JsonSerializer serializer)
        {
            return JToken.FromObject(new
            {
                PalId = value.Pal.Id,
                GuaranteedPassives = value.EffectivePassives.Where(t => t is not RandomPassiveSkill).ToList(),
                NumPassives = value.EffectivePassives.Count(t => t is RandomPassiveSkill),
            }, serializer);
        }

        internal override WildPalReference ReadRefJson(JToken token, Type objectType, WildPalReference existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var pal = token["PalId"].ToObject<PalId>(serializer).ToPal(db);
            var guaranteedPassives = (token["GuaranteedPassives"] ?? token["GuaranteedTraits"])?.ToObject<List<string>>()?.Select(s => s.InternalToPassive(db))?.ToList();
            var numPassives = (token["NumPassives"] ?? token["NumTraits"]).ToObject<int>();

            return new WildPalReference(pal, guaranteedPassives ?? Enumerable.Empty<PassiveSkill>(), numPassives);
        }
    }

    internal class IV_IValueConverter : JsonConverter<IV_IValue>
    {
        public override IV_IValue ReadJson(JsonReader reader, Type objectType, IV_IValue existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);
            if (token is not JObject) return IV_Random.Instance;
            else
            {
                return new IV_Range(
                    IsRelevant: true, // (note: isRelevant only matters while the solver is running)
                    token["Min"].ToObject<int>(),
                    token["Max"].ToObject<int>()
                );
            }
        }

        public override void WriteJson(JsonWriter writer, IV_IValue value, JsonSerializer serializer)
        {
            switch (value)
            {
                case IV_Random:
                    JToken.FromObject("any").WriteTo(writer); break;

                case IV_Range range:
                    JToken.FromObject(new
                    {
                        Min = range.Min,
                        Max = range.Max
                    }).WriteTo(writer);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }

    internal class BredPalReferenceConverter : IPalReferenceConverterBase<BredPalReference>
    {
        public BredPalReferenceConverter(PalDB db, GameSettings gameSettings, PalReferenceConverter genericConverter) : base(db, gameSettings, "BRED_PAL")
        {
            dependencyConverters = new JsonConverter[]
            {
                genericConverter,
                new PassiveSkillConverter(db, gameSettings),
                new ILocalizedTextConverter(db, gameSettings),
                new IV_IValueConverter(),
            };
        }

        internal override BredPalReference ReadRefJson(JToken token, Type objectType, BredPalReference existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            InjectDependencyConverters(serializer);
            var pal = token["PalId"].ToObject<PalId>(serializer).ToPal(db);
            var passives = (token["Passives"] ?? token["Traits"]).ToObject<List<PassiveSkill>>(serializer);
            var parent1 = token["Parent1"].ToObject<IPalReference>(serializer);
            var parent2 = token["Parent2"].ToObject<IPalReference>(serializer);
            var gender = token["Gender"].ToObject<PalGender>(serializer);
            var passivesProbability = (token["PassivesProbability"] ?? token["TraitsProbability"]).ToObject<float>(serializer);
            var ivsProbability = token["IVsProbability"]?.ToObject<float>(serializer) ?? 1.0f;

            var IV_hp = token["IV_HP"]?.ToObject<IV_IValue>(serializer) ?? IV_Random.Instance;
            var IV_attack = token["IV_Attack"]?.ToObject<IV_IValue>(serializer) ?? IV_Random.Instance;
            var IV_defense = token["IV_Defense"]?.ToObject<IV_IValue>(serializer) ?? IV_Random.Instance;
            var ivs = new IV_Set() { HP = IV_hp, Attack = IV_attack, Defense = IV_defense };

            return new BredPalReference(gameSettings, pal, parent1, parent2, passives, passivesProbability, ivs, ivsProbability).WithGuaranteedGender(db, gender) as BredPalReference;
        }

        internal override JToken MakeRefJson(BredPalReference value, JsonSerializer serializer)
        {
            InjectDependencyConverters(serializer);
            return JToken.FromObject(new
            {
                PalId = value.Pal.Id,
                Passives = value.EffectivePassives,
                Parent1 = value.Parent1,
                Parent2 = value.Parent2,
                Gender = value.Gender,
                PassivesProbability = value.PassivesProbability,
                IV_HP = value.IVs.HP,
                IV_Attack = value.IVs.Attack,
                IV_Defense = value.IVs.Defense,
                IVsProbability = value.IVsProbability

            }, serializer);
        }
    }
    #endregion

    #region ViewModel Converters
    internal class PalViewModelConverter : PalConverterBase<PalViewModel>
    {
        public PalViewModelConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings)
        {
            dependencyConverters = [
                new ILocalizedTextConverter(db, gameSettings),
            ];
        }

        protected override PalViewModel ReadTypeJson(JsonReader reader, Type objectType, PalViewModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var palId = JToken.ReadFrom(reader).ToObject<PalId>(serializer);
            return PalViewModel.Make(palId.ToPal(db));
        }

        protected override void WriteTypeJson(JsonWriter writer, PalViewModel value, JsonSerializer serializer)
        {
            JToken.FromObject(value.ModelObject.Id, serializer).WriteTo(writer, dependencyConverters);
        }
    }

    internal class PassiveSkillViewModelConverter : PalConverterBase<PassiveSkillViewModel>
    {
        public PassiveSkillViewModelConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings)
        {
            dependencyConverters = new JsonConverter[]
            {
                new PassiveSkillConverter(db, gameSettings),
                new ILocalizedTextConverter(db, gameSettings),
            };
        }

        protected override PassiveSkillViewModel ReadTypeJson(JsonReader reader, Type objectType, PassiveSkillViewModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var passive = JToken.ReadFrom(reader).ToObject<PassiveSkill>(serializer);
            return passive != null
                ? PassiveSkillViewModel.Make(passive)
                : null;
        }

        protected override void WriteTypeJson(JsonWriter writer, PassiveSkillViewModel value, JsonSerializer serializer)
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
                new PassiveSkillViewModelConverter(db, gameSettings),
                new BreedingResultListViewModelConverter(db, gameSettings, source),
                new ILocalizedTextConverter(db, gameSettings),
            };
        }

        protected override PalSpecifierViewModel ReadTypeJson(JsonReader reader, Type objectType, PalSpecifierViewModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JToken.ReadFrom(reader);
            return new PalSpecifierViewModel(null)
            {
                TargetPal = obj["TargetPal"].ToObject<PalViewModel>(serializer),
                Passive1 = (obj["Passive1"] ?? obj["Trait1"]).ToObject<PassiveSkillViewModel>(serializer),
                Passive2 = (obj["Passive2"] ?? obj["Trait2"]).ToObject<PassiveSkillViewModel>(serializer),
                Passive3 = (obj["Passive3"] ?? obj["Trait3"]).ToObject<PassiveSkillViewModel>(serializer),
                Passive4 = (obj["Passive4"] ?? obj["Trait4"]).ToObject<PassiveSkillViewModel>(serializer),
                OptionalPassive1 = (obj["OptionalPassive1"] ?? obj["OptionalTrait1"])?.ToObject<PassiveSkillViewModel>(serializer),
                OptionalPassive2 = (obj["OptionalPassive2"] ?? obj["OptionalTrait2"])?.ToObject<PassiveSkillViewModel>(serializer),
                OptionalPassive3 = (obj["OptionalPassive3"] ?? obj["OptionalTrait3"])?.ToObject<PassiveSkillViewModel>(serializer),
                OptionalPassive4 = (obj["OptionalPassive4"] ?? obj["OptionalTrait4"])?.ToObject<PassiveSkillViewModel>(serializer),
                MinIv_HP = obj["MinIV_HP"]?.ToObject<int>() ?? 0,
                MinIv_Attack = obj["MinIV_Attack"]?.ToObject<int>() ?? 0,
                MinIv_Defense = obj["MinIV_Defense"]?.ToObject<int>() ?? 0,
                PalSourceId = obj["PalSourceId"]?.ToObject<string>(),
                RequiredGender = PalGenderViewModel.Make(obj["RequiredGender"]?.ToObject<PalGender>() ?? PalGender.WILDCARD),
                IncludeBasePals = obj["IncludeBasePals"]?.ToObject<bool>() ?? true,
                IncludeCustomPals = obj["IncludeCustomPals"]?.ToObject<bool>() ?? true,
                IncludeCagedPals = obj["IncludeCagedPals"]?.ToObject<bool>() ?? true,
                CurrentResults = obj["CurrentResults"].ToObject<BreedingResultListViewModel>(serializer)
            };
        }

        protected override void WriteTypeJson(JsonWriter writer, PalSpecifierViewModel value, JsonSerializer serializer)
        {
            JToken.FromObject(new
            {
                TargetPal = value.TargetPal,
                Passive1 = value.Passive1,
                Passive2 = value.Passive2,
                Passive3 = value.Passive3,
                Passive4 = value.Passive4,
                OptionalPassive1 = value.OptionalPassive1,
                OptionalPassive2 = value.OptionalPassive2,
                OptionalPassive3 = value.OptionalPassive3,
                OptionalPassive4 = value.OptionalPassive4,
                MinIV_HP = value.MinIv_HP,
                MinIV_Attack = value.MinIv_Attack,
                MinIV_Defense = value.MinIv_Defense,
                PalSourceId = value.PalSourceId,
                RequiredGender = value.RequiredGender?.Value ?? PalGender.WILDCARD,
                IncludeBasePals = value.IncludeBasePals,
                IncludeCustomPals = value.IncludeCustomPals,
                IncludeCagedPals = value.IncludeCagedPals,
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
                new ILocalizedTextConverter(db, gameSettings),
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
                new ILocalizedTextConverter(db, gameSettings),
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
                new ILocalizedTextConverter(db, gameSettings),
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
