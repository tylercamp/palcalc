using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.Probabilities;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Solver;
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
            if (token.Type == JTokenType.Null)
            {
                return null;
            }
            else if (token.Type == JTokenType.String)
            {
                var passiveInternalName = token.ToObject<string>();
                return passiveInternalName != null
                    ? passiveInternalName.InternalToStandardPassive(db)
                    : null;
            }
            else
            {
                // (Some converters were missing this as a dependency and wound up serializing the whole object
                // structure of PassiveSkill instead of just storing the internal-name string)
                var asDirectPassive = token.ToObject<PassiveSkill>();
                return asDirectPassive.InternalName.InternalToStandardPassive(db);
            }
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
        SurgeryPalReferenceConverter sprc;

        public PalReferenceConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings)
        {
            this.oprc = new OwnedPalReferenceConverter(db, gameSettings);
            this.wprc = new WildPalReferenceConverter(db, gameSettings);
            this.bprc = new BredPalReferenceConverter(db, gameSettings, this);
            this.cprc = new CompositePalReferenceConverter(db, gameSettings);
            this.sprc = new SurgeryPalReferenceConverter(db, gameSettings, this);
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
            if (type == sprc.TypeLabel) return sprc.ReadRefJson(wrappedContent, objectType, existingValue as SurgeryTablePalReference, hasExistingValue, serializer);

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
                case SurgeryTablePalReference spr: sprc.WriteJson(writer, spr, serializer); break;
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
                new IV_IValueConverter(),
                new PassiveSkillConverter(db, gameSettings),
                new PalInstanceJsonConverter(db),
                new ILocalizedTextConverter(db, gameSettings),
            };
        }

        internal override JToken MakeRefJson(OwnedPalReference value, JsonSerializer serializer)
        {
            InjectDependencyConverters(serializer);

            return JToken.FromObject(new
            {
                Instance = value.UnderlyingInstance,
                IVs = new
                {
                    HP = value.IVs.HP,
                    Attack = value.IVs.Attack,
                    Defense = value.IVs.Defense
                }
            }, serializer);
        }

        internal override OwnedPalReference ReadRefJson(JToken token, Type objectType, OwnedPalReference existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            InjectDependencyConverters(serializer);

            PalInstance inst;
            IV_IValue hp;
            IV_IValue attack;
            IV_IValue defense;

            if (token["Instance"] != null)
            {
                inst = token["Instance"].ToObject<PalInstance>(serializer);
                hp = token["IVs"]["HP"].ToObject<IV_IValue>(serializer);
                attack = token["IVs"]["Attack"].ToObject<IV_IValue>(serializer);
                defense = token["IVs"]["Defense"].ToObject<IV_IValue>(serializer);
            }
            else
            {
                // old format (changed 1.10.0)
                inst = token.ToObject<PalInstance>(serializer);

                hp = new IV_Range(isRelevant: true, inst.IV_HP);
                attack = new IV_Range(isRelevant: true, inst.IV_Attack);
                defense = new IV_Range(isRelevant: true, inst.IV_Defense);
            }

            return new OwnedPalReference(
                inst,
                // supposed to be "effective passives", but that only matters when the solver is running, and this is a saved solver result
                inst.PassiveSkills,
                new IV_Set()
                {
                    HP = hp,
                    Attack = attack,
                    Defense = defense
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
                new PassiveSkillConverter(db, gameSettings),
            ];
        }

        internal override JToken MakeRefJson(WildPalReference value, JsonSerializer serializer)
        {
            return JToken.FromObject(new
            {
                PalId = value.Pal.Id,
                GuaranteedPassives = value.EffectivePassives.Where(t => t is not RandomPassiveSkill).ToList(),
                NumPassives = value.EffectivePassives.Count(t => t is RandomPassiveSkill),
                Gender = value.Gender,
            }, serializer);
        }

        internal override WildPalReference ReadRefJson(JToken token, Type objectType, WildPalReference existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var pal = token["PalId"].ToObject<PalId>(serializer).ToPal(db);
            var numPassives = (token["NumPassives"] ?? token["NumTraits"]).ToObject<int>();
            var gender = token["Gender"]?.ToObject<PalGender>(serializer) ?? PalGender.WILDCARD;

            var guaranteedPassives = (token["GuaranteedPassives"] ?? token["GuaranteedTraits"])
                ?.ToObject<List<PassiveSkill>>(serializer)
                ?.ToList()
                ?? Enumerable.Empty<PassiveSkill>();

            return (WildPalReference)new WildPalReference(pal, guaranteedPassives, numPassives).WithGuaranteedGender(db, gender);
        }
    }

    internal class SurgeryOperationConverter : PalConverterBase<ISurgeryOperation>
    {
        public SurgeryOperationConverter(PalDB db, GameSettings gameSettings) : base(db, gameSettings)
        {
            dependencyConverters = [
                new PassiveSkillConverter(db, gameSettings),
                new ILocalizedTextConverter(db, gameSettings)
            ];
        }

        protected override ISurgeryOperation ReadTypeJson(JsonReader reader, Type objectType, ISurgeryOperation existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);
            var operationType = token["Type"].ToObject<string>();
            switch (operationType)
            {
                case "ADD_PASSIVE":
                    return new AddPassiveSurgeryOperation(token["AddedPassive"].ToObject<PassiveSkill>(serializer));

                case "REPLACE_PASSIVE":
                    return new ReplacePassiveSurgeryOperation(
                        removedPassive: token["RemovedPassive"].ToObject<PassiveSkill>(serializer),
                        addedPassive: token["AddedPassive"].ToObject<PassiveSkill>(serializer)
                    );

                case "CHANGE_GENDER":
                    return new ChangeGenderSurgeryOperation(token["NewGender"].ToObject<PalGender>(serializer));

                default:
                    throw new Exception($"Unrecognized ISurgeryOperation type: {operationType}");
            }
        }

        protected override void WriteTypeJson(JsonWriter writer, ISurgeryOperation value, JsonSerializer serializer)
        {
            switch (value)
            {
                case AddPassiveSurgeryOperation apso:
                    JToken.FromObject(
                        new
                        {
                            Type = "ADD_PASSIVE",
                            AddedPassive = apso.AddedPassive
                        },
                        serializer
                    ).WriteTo(writer, dependencyConverters);
                    break;

                case ReplacePassiveSurgeryOperation rpso:
                    JToken.FromObject(
                        new
                        {
                            Type = "REPLACE_PASSIVE",
                            RemovedPassive = rpso.RemovedPassive,
                            AddedPassive = rpso.AddedPassive,
                        },
                        serializer
                    ).WriteTo(writer, dependencyConverters);
                    break;

                case ChangeGenderSurgeryOperation cgso:
                    JToken.FromObject(
                        new
                        {
                            Type = "CHANGE_GENDER",
                            NewGender = cgso.NewGender,
                        },
                        serializer
                    ).WriteTo(writer, dependencyConverters);
                    break;

                default:
                    throw new Exception($"Missing serialization for ISurgeryOperation type {value?.GetType()?.Name}");
            }
        }
    }

    internal class SurgeryPalReferenceConverter : IPalReferenceConverterBase<SurgeryTablePalReference>
    {
        public SurgeryPalReferenceConverter(PalDB db, GameSettings gameSettings, PalReferenceConverter genericConverter) : base(db, gameSettings, "SURGERY_PAL")
        {
            dependencyConverters = [
                genericConverter,
                new ILocalizedTextConverter(db, gameSettings),
                new PassiveSkillConverter(db, gameSettings),
                new SurgeryOperationConverter(db, gameSettings),
            ];
        }

        internal override JToken MakeRefJson(SurgeryTablePalReference value, JsonSerializer serializer)
        {
            return JToken.FromObject(new
            {
                Input = value.Input,
                Operations = value.Operations
            }, serializer);
        }

        internal override SurgeryTablePalReference ReadRefJson(JToken token, Type objectType, SurgeryTablePalReference existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return new SurgeryTablePalReference(
                input: token["Input"].ToObject<IPalReference>(serializer),
                operations: token["Operations"].ToObject<List<ISurgeryOperation>>(serializer)
            );
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
                    token["IsRelevant"]?.ToObject<bool>() ?? true,
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
                        IsRelevant = range.IsRelevant,
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
        private CachedSaveGame source;

        public PalSpecifierViewModelConverter(PalDB db, GameSettings gameSettings, CachedSaveGame source) : base(db, gameSettings)
        {
            this.source = source;

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

            var modelSpecifier = new PalSpecifier()
            {
                Pal = obj["TargetPal"].ToObject<PalViewModel>(serializer).ModelObject,
                RequiredPassives = [
                    (obj["Passive1"] ?? obj["Trait1"]).ToObject<PassiveSkillViewModel>(serializer)?.ModelObject,
                    (obj["Passive2"] ?? obj["Trait2"]).ToObject<PassiveSkillViewModel>(serializer)?.ModelObject,
                    (obj["Passive3"] ?? obj["Trait3"]).ToObject<PassiveSkillViewModel>(serializer)?.ModelObject,
                    (obj["Passive4"] ?? obj["Trait4"]).ToObject<PassiveSkillViewModel>(serializer)?.ModelObject,
                ],
                OptionalPassives = [
                    (obj["OptionalPassive1"] ?? obj["OptionalTrait1"])?.ToObject<PassiveSkillViewModel>(serializer)?.ModelObject,
                    (obj["OptionalPassive2"] ?? obj["OptionalTrait2"]) ?.ToObject<PassiveSkillViewModel>(serializer)?.ModelObject,
                    (obj["OptionalPassive3"] ?? obj["OptionalTrait3"]) ?.ToObject<PassiveSkillViewModel>(serializer)?.ModelObject,
                    (obj["OptionalPassive4"] ?? obj["OptionalTrait4"])?.ToObject<PassiveSkillViewModel>(serializer)?.ModelObject,
                ]
            };

            List<IPalSourceTreeSelection> palSourceSelections;

            if (obj["PalSourceId"] != null)
            {
                var selectionId = obj["PalSourceId"].ToObject<string>();
                if (selectionId != null) palSourceSelections = [IPalSourceTreeSelection.SingleFromId(source, selectionId)];
                else palSourceSelections = [new SourceTreeAllSelection()];
            }
            else
            {
                palSourceSelections = obj["PalSourceSelections"]
                    ?.ToObject<List<string>>()
                    ?.Select(id => IPalSourceTreeSelection.SingleFromId(source, id))
                    ?.SkipNull()
                    ?.ToList();
            }

            // null-coalesce for backwards compatibility with older saves
            var id = obj["Id"]?.ToObject<string>() ?? Guid.NewGuid().ToString();
            return new PalSpecifierViewModel(id, modelSpecifier)
            {
                MinIv_HP = obj["MinIV_HP"]?.ToObject<int>() ?? 0,
                MinIv_Attack = obj["MinIV_Attack"]?.ToObject<int>() ?? 0,
                MinIv_Defense = obj["MinIV_Defense"]?.ToObject<int>() ?? 0,
                PalSourceSelections = palSourceSelections ?? [new SourceTreeAllSelection()],
                RequiredGender = PalGenderViewModel.Make(obj["RequiredGender"]?.ToObject<PalGender>() ?? PalGender.WILDCARD),
                IncludeBasePals = obj["IncludeBasePals"]?.ToObject<bool>() ?? true,
                IncludeCustomPals = obj["IncludeCustomPals"]?.ToObject<bool>() ?? true,
                IncludeCagedPals = obj["IncludeCagedPals"]?.ToObject<bool>() ?? true,
                IncludeGlobalStoragePals = obj["IncludeGlobalStoragePals"]?.ToObject<bool>() ?? true,
                IncludeExpeditionPals = obj["IncludeExpeditionPals"]?.ToObject<bool>() ?? true,
                CurrentResults = obj["CurrentResults"].ToObject<BreedingResultListViewModel>(serializer),
            };
        }

        protected override void WriteTypeJson(JsonWriter writer, PalSpecifierViewModel value, JsonSerializer serializer)
        {
            JToken.FromObject(new
            {
                Id = value.Id,
                TargetPal = value.TargetPal,
                Passive1 = value.RequiredPassives.Passive1,
                Passive2 = value.RequiredPassives.Passive2,
                Passive3 = value.RequiredPassives.Passive3,
                Passive4 = value.RequiredPassives.Passive4,
                OptionalPassive1 = value.OptionalPassives.Passive1,
                OptionalPassive2 = value.OptionalPassives.Passive2,
                OptionalPassive3 = value.OptionalPassives.Passive3,
                OptionalPassive4 = value.OptionalPassives.Passive4,
                MinIV_HP = value.MinIv_HP,
                MinIV_Attack = value.MinIv_Attack,
                MinIV_Defense = value.MinIv_Defense,
                PalSourceSelections = value.PalSourceSelections?.Select(s => s.SerializedId),
                RequiredGender = value.RequiredGender?.Value ?? PalGender.WILDCARD,
                IncludeBasePals = value.IncludeBasePals,
                IncludeCustomPals = value.IncludeCustomPals,
                IncludeCagedPals = value.IncludeCagedPals,
                IncludeGlobalStoragePals = value.IncludeGlobalStoragePals,
                IncludeExpeditionPals = value.IncludeExpeditionPals,
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
            return new BreedingResultViewModel(source, gameSettings, palRef);
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
            var fullToken = JToken.ReadFrom(reader);
            if (fullToken.Type == JTokenType.Null) return new BreedingResultListViewModel();

            var resultsToken = fullToken["Results"];
            return new BreedingResultListViewModel() { Results = resultsToken.ToObject<List<BreedingResultViewModel>>(serializer) };
        }

        protected override void WriteTypeJson(JsonWriter writer, BreedingResultListViewModel value, JsonSerializer serializer)
        {
            JToken.FromObject(new { Results = value.Results }, serializer).WriteTo(writer, dependencyConverters);
        }
    }

    internal class PalTargetListViewModelConverter : PalConverterBase<PalTargetListViewModel>
    {
        private Dictionary<string, PalSpecifierViewModel> expectedSpecifiers;

        public PalTargetListViewModelConverter(PalDB db, GameSettings gameSettings, CachedSaveGame source, Dictionary<string, PalSpecifierViewModel> specifiersById) : base(db, gameSettings)
        {
            expectedSpecifiers = specifiersById;
            dependencyConverters = new JsonConverter[]
            {
                new PalSpecifierViewModelConverter(db, gameSettings, source),
                new ILocalizedTextConverter(db, gameSettings),
            };
        }

        protected override PalTargetListViewModel ReadTypeJson(JsonReader reader, Type objectType, PalTargetListViewModel existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var orderedIds = JToken.ReadFrom(reader)["OrderedTargetIds"].ToObject<List<string>>();
            return new PalTargetListViewModel(expectedSpecifiers.Values.OrderBy(s => orderedIds.Contains(s.Id) ? orderedIds.IndexOf(s.Id) : int.MaxValue));
        }

        protected override void WriteTypeJson(JsonWriter writer, PalTargetListViewModel value, JsonSerializer serializer)
        {
            JToken
                .FromObject(new { OrderedTargetIds = value.Targets.Where(t => !t.IsReadOnly).Select(t => t.Id).ToList() }, serializer)
                .WriteTo(writer, dependencyConverters);
        }
    }
    #endregion
}
