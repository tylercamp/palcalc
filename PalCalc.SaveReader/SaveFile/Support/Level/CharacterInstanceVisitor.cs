using PalCalc.Model;
using PalCalc.SaveReader.FArchive;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Support.Level
{
    public class GvasCharacterInstance
    {
        public Guid InstanceId { get; set; }
        public string NickName { get; set; }
        public int Level { get; set; }

        public bool IsPlayer { get; set; }
        public Guid? PlayerId { get; set; }

        public Guid? OwnerPlayerId { get; set; }
        public List<Guid> OldOwnerPlayerIds { get; set; }

        public string CharacterId { get; set; }
        public Guid? ContainerId { get; set; }
        public int SlotIndex { get; set; }
        public string Gender { get; set; }

        public int? TalentHp { get; set; }
        public int? TalentShot { get; set; }
        public int? TalentMelee { get; set; }
        public int? TalentDefense { get; set; }

        public int? Rank { get; set; }

        public List<string> PassiveSkills { get; set; }

        public List<string> ActiveSkills { get; set; }
        public List<string> EquippedActiveSkills { get; set; }

        private static ILogger logger = Log.ForContext<GvasCharacterInstance>();

        public PalInstance ToPalInstance(PalDB db, LocationType locationType)
        {
            var sanitizedCharId = CharacterId.Replace("Boss_", "", StringComparison.InvariantCultureIgnoreCase);

            if (sanitizedCharId == "None") return null;
            if (db.Humans.Any(h => h.InternalName == sanitizedCharId)) return null;

            var pal = db.Pals.FirstOrDefault(p => p.InternalName.Equals(sanitizedCharId, StringComparison.OrdinalIgnoreCase));

            if (pal == null)
            {
                // skip unrecognized pals
                logger.Warning("unrecognized pal '{name}', skipping", sanitizedCharId);
                return null;
            }

            var passives = PassiveSkills
                .Select(name =>
                {
                    var passive = db.StandardPassiveSkills.FirstOrDefault(t => t.InternalName == name);
                    if (passive == null)
                    {
                        logger.Warning("unrecognized passive skill '{internalName}' on pal {Pal}", name, CharacterId);
                    }
                    return passive ?? new UnrecognizedPassiveSkill(name);
                })
                .ToList();

            ActiveSkill MakeActiveSkill(string name)
            {
                var activeSkill = db.ActiveSkills.FirstOrDefault(t => t.InternalName == name);
                if (activeSkill == null)
                {
                    logger.Warning("unrecognized active skill '{internalName}' on pal {Pal}", name, CharacterId);
                }
                return activeSkill ?? new UnrecognizedActiveSkill(name);
            }

            string FormatSkillName(string skillId) => skillId.Replace("EPalWazaID::", "");

            var equippedActiveSkills = EquippedActiveSkills
                .Select(FormatSkillName)
                .Select(MakeActiveSkill)
                .ToList();

            var activeSkills = ActiveSkills
                .Select(FormatSkillName)
                .Select(MakeActiveSkill)
                // for some reason the equipped skills won't always show in the available list of skills
                .Concat(equippedActiveSkills)
                .Distinct()
                .ToList();

            return new PalInstance()
            {
                Pal = pal,
                InstanceId = InstanceId.ToString(),
                OwnerPlayerId = OwnerPlayerId?.ToString() ?? OldOwnerPlayerIds.First().ToString(),

                IV_HP = TalentHp ?? 0,
                IV_Melee = TalentMelee ?? 0,
                IV_Shot = TalentShot ?? 0,
                IV_Defense = TalentDefense ?? 0,

                Rank = Rank ?? 1,

                Level = Level,
                NickName = NickName,
                Gender = Gender switch
                {
                    null => PalGender.NONE,
                    var g when g.Contains("Female", StringComparison.InvariantCultureIgnoreCase) => PalGender.FEMALE,
                    _ => PalGender.MALE
                },
                PassiveSkills = passives,
                ActiveSkills = activeSkills,
                EquippedActiveSkills = equippedActiveSkills,
                Location = new PalLocation()
                {
                    ContainerId = ContainerId.ToString(),
                    Type = locationType,
                    Index = SlotIndex,
                }
            };
        }
    }
    
    class CharacterInstanceVisitor : IVisitor
    {
        private static ILogger logger = Log.ForContext<CharacterInstanceVisitor>();

        public CharacterInstanceVisitor(string basePath) : base(basePath) { }

        public GvasCharacterInstance Result { get; private set; } = null;

        public event Action<GvasCharacterInstance> OnExit;

        public override void Exit()
        {
            base.Exit();
            OnExit?.Invoke(Result);
        }

        GvasCharacterInstance pendingInstance = null;

        const string K_NICKNAME             = ".NickName";
        const string K_LEVEL                = ".Level";
        const string K_CHARACTER_ID         = ".CharacterID";
        const string K_IS_PLAYER            = ".IsPlayer";
        const string K_GENDER               = ".Gender";
        const string K_CONTAINER_ID         = ".SlotID.ContainerId.ID";
        const string K_CONTAINER_SLOT_INDEX = ".SlotID.SlotIndex";
        const string K_PASSIVE_SKILL_LIST   = ".PassiveSkillList";
        const string K_ACTIVE_SKILL_LIST    = ".MasteredWaza";
        const string K_ACTIVE_SKILL_USED_LIST = ".EquipWaza";
        const string K_OWNER_PLAYER_ID      = ".OwnerPlayerUId";
        const string K_OLD_OWNER_PLAYER_IDS = ".OldOwnerPlayerUIds.OldOwnerPlayerUIds";
        const string K_TALENT_HP            = ".Talent_HP";
        const string K_TALENT_SHOT          = ".Talent_Shot";
        const string K_TALENT_MELEE         = ".Talent_Melee";
        const string K_TALENT_DEFENSE       = ".Talent_Defense";
        const string K_RANK                 = ".Rank";

        static readonly List<string> REQUIRED_PAL_PROPS = new List<string>()
        {
            K_CHARACTER_ID,
            K_CONTAINER_ID,
            K_CONTAINER_SLOT_INDEX,
        };

        public IEnumerable<IVisitor> VisitCharacterBegin()
        {
            logger.Verbose("Beginning object read");

            pendingInstance = new GvasCharacterInstance();

            {
                var collectingVisitor = new ValueCollectingVisitor(this, isCaseSensitive: false,
                    K_NICKNAME,
                    K_LEVEL,
                    K_CHARACTER_ID,
                    K_IS_PLAYER,
                    K_GENDER,
                    K_CONTAINER_ID,
                    K_CONTAINER_SLOT_INDEX,
                    K_OWNER_PLAYER_ID,
                    K_TALENT_HP,
                    K_TALENT_SHOT,
                    K_TALENT_MELEE,
                    K_TALENT_DEFENSE,
                    K_RANK
                );

                collectingVisitor.OnExit += (vals) =>
                {
                    logger.Verbose("property collector exited with values for {fieldNames}", string.Join(", ", vals.Keys));

                    pendingInstance.IsPlayer = (bool)vals.GetValueOrElse(K_IS_PLAYER, false);

                    // level 1 (i.e. "default level") instances don't have a Level property
                    pendingInstance.Level = Convert.ToInt32(vals.GetValueOrElse(K_LEVEL, 1));

                    if (pendingInstance.IsPlayer)
                    {
                        pendingInstance.NickName = (string)vals[K_NICKNAME];
                    }
                    else
                    {
                        var missingProps = REQUIRED_PAL_PROPS.Where(p => !vals.ContainsKey(p)).ToList();
                        if (missingProps.Any())
                        {
                            logger.Warning("character instance is missing {missingProps}, skipping", string.Join(", ", missingProps));
                            pendingInstance = null;
                            return;
                        }

                        pendingInstance.NickName = vals.GetValueOrDefault(K_NICKNAME)?.ToString();

                        pendingInstance.OwnerPlayerId = (Guid?)vals.GetValueOrDefault(K_OWNER_PLAYER_ID);
                        pendingInstance.CharacterId = (string)vals[K_CHARACTER_ID];
                        pendingInstance.Gender = (string)vals.GetValueOrDefault(K_GENDER);

                        pendingInstance.ContainerId = (Guid)vals[K_CONTAINER_ID];
                        pendingInstance.SlotIndex = Convert.ToInt32(vals[K_CONTAINER_SLOT_INDEX]);

                        pendingInstance.TalentHp = vals.ContainsKey(K_TALENT_HP) ? Convert.ToInt32(vals[K_TALENT_HP]) : null;
                        pendingInstance.TalentMelee = vals.ContainsKey(K_TALENT_MELEE) ? Convert.ToInt32(vals[K_TALENT_MELEE]) : null;
                        pendingInstance.TalentShot = vals.ContainsKey(K_TALENT_SHOT) ? Convert.ToInt32(vals[K_TALENT_SHOT]) : null;
                        pendingInstance.TalentDefense = vals.ContainsKey(K_TALENT_DEFENSE) ? Convert.ToInt32(vals[K_TALENT_DEFENSE]) : null;

                        pendingInstance.Rank = vals.ContainsKey(K_RANK) ? Convert.ToInt32(vals[K_RANK]) : null;
                    }
                };

                yield return collectingVisitor;
            }

            {
                List<string> passives = new List<string>();
                var passiveSkillVisitor = new ValueEmittingVisitor(this, isCaseSensitive: false, K_PASSIVE_SKILL_LIST);
                passiveSkillVisitor.OnValue += (_, v) =>
                {
                    logger.Verbose("Storing passive skill value {name}", v);
                    passives.Add(v.ToString());
                };

                passiveSkillVisitor.OnExit += () =>
                {
                    if (pendingInstance != null) pendingInstance.PassiveSkills = passives;
                };

                yield return passiveSkillVisitor;
            }

            {
                List<string> activeSkills = [];
                var activeSkillsVisitor = new ValueEmittingVisitor(this, isCaseSensitive: false, K_ACTIVE_SKILL_LIST);
                activeSkillsVisitor.OnValue += (_, v) =>
                {
                    logger.Verbose("Storing active skill value {name}", v);
                    activeSkills.Add(v.ToString());
                };

                activeSkillsVisitor.OnExit += () =>
                {
                    if (pendingInstance != null) pendingInstance.ActiveSkills = activeSkills;
                };

                yield return activeSkillsVisitor;
            }

            {
                List<string> equippedSkills = [];
                var equippedSkillsVisitor = new ValueEmittingVisitor(this, isCaseSensitive: false, K_ACTIVE_SKILL_USED_LIST);
                equippedSkillsVisitor.OnValue += (_, v) =>
                {
                    logger.Verbose("Storing equipped skill value {name}", v);
                    equippedSkills.Add(v.ToString());
                };

                equippedSkillsVisitor.OnExit += () =>
                {
                    if (pendingInstance != null) pendingInstance.EquippedActiveSkills = equippedSkills;
                };

                yield return equippedSkillsVisitor;
            }

            {
                List<Guid> oldOwnerIds = new List<Guid>();
                var oldOwnersVisitor = new ValueEmittingVisitor(this, isCaseSensitive: false, K_OLD_OWNER_PLAYER_IDS);
                oldOwnersVisitor.OnValue += (_, v) =>
                {
                    oldOwnerIds.Add((Guid)v);
                };
                oldOwnersVisitor.OnExit += () =>
                {
                    if (pendingInstance != null) pendingInstance.OldOwnerPlayerIds = oldOwnerIds;
                };

                yield return oldOwnersVisitor;
            }
        }

        public void VisitCharacterEnd()
        {
            logger.Verbose("Ending object read");
            if (pendingInstance != null)
            {
                logger.Verbose("Generated valid character instance");
                Result = pendingInstance;
            }

            pendingInstance = null;
        }
    }

    class WorldSaveData_CharacterInstanceVisitor : IVisitor
    {
        private static ILogger logger = Log.ForContext<WorldSaveData_CharacterInstanceVisitor>();

        public WorldSaveData_CharacterInstanceVisitor() : base(".worldSaveData.CharacterSaveParameterMap") { }

        public List<GvasCharacterInstance> Result = new List<GvasCharacterInstance>();

        private Dictionary<string, object> pendingProperties;
        private CharacterInstanceVisitor pendingCharacterVisitor;

        const string K_INSTANCE_ID = ".Key.InstanceId";
        const string K_PLAYER_ID = ".Key.PlayerUId";


        public override IEnumerable<IVisitor> VisitMapEntryBegin(string path, int index, MapPropertyMeta meta)
        {
            logger.Verbose("Beginning object read");
            base.VisitMapEntryBegin(path, index, meta);

            {
                var pendingIdVisitor = new ValueCollectingVisitor(this, isCaseSensitive: false,
                    K_PLAYER_ID,
                    K_INSTANCE_ID
                );
                pendingIdVisitor.WithOnExit(values => pendingProperties = values);

                yield return pendingIdVisitor;
            }

            {
                pendingCharacterVisitor = new CharacterInstanceVisitor($"{MatchedPath}.Value.RawData.SaveParameter");

                foreach (var v in pendingCharacterVisitor.VisitCharacterBegin())
                    yield return v;

                yield return pendingCharacterVisitor;
            }
        }

        public override void VisitMapEntryEnd(string path, int index, MapPropertyMeta meta)
        {
            logger.Verbose("Ending object read");
            pendingCharacterVisitor.VisitCharacterEnd();

            if (pendingCharacterVisitor.Result != null)
            {
                logger.Verbose("Generated valid character instance");
                var instance = pendingCharacterVisitor.Result;

                instance.InstanceId = (Guid)pendingProperties[K_INSTANCE_ID];
                instance.PlayerId = (Guid?)pendingProperties[K_PLAYER_ID];

                Result.Add(instance);
            }

            pendingCharacterVisitor = null;
            pendingProperties = null;
        }
    }

    // ".SaveParameterArray.SaveParameterArray.SaveParameter.Gender"
    // ".SaveParameterArray.SaveParameterArray.InstanceId.InstanceId"

    class DimensionalPalStorage_CharacterInstanceVisitor : IVisitor
    {
        private static ILogger logger = Log.ForContext<CharacterInstanceVisitor>();

        public DimensionalPalStorage_CharacterInstanceVisitor() : base(".SaveParameterArray") { }

        public List<GvasCharacterInstance> Result = new List<GvasCharacterInstance>();

        private Dictionary<string, object> pendingProperties;
        private CharacterInstanceVisitor pendingCharacterVisitor;

        const string K_INSTANCE_ID = ".SaveParameterArray.InstanceId.InstanceId";
        const string K_PLAYER_ID = ".SaveParameterArray.InstanceId.PlayerUId";


        public override IEnumerable<IVisitor> VisitArrayEntryBegin(string path, int index, ArrayPropertyMeta meta)
        {
            logger.Verbose("Beginning object read");
            base.VisitArrayEntryBegin(path, index, meta);

            {
                var pendingIdVisitor = new ValueCollectingVisitor(this, isCaseSensitive: false,
                    K_PLAYER_ID,
                    K_INSTANCE_ID
                );
                pendingIdVisitor.WithOnExit(values => pendingProperties = values);

                yield return pendingIdVisitor;
            }

            {
                pendingCharacterVisitor = new CharacterInstanceVisitor($"{MatchedPath}.SaveParameterArray.SaveParameter");

                foreach (var v in pendingCharacterVisitor.VisitCharacterBegin())
                    yield return v;

                yield return pendingCharacterVisitor;
            }
        }

        public override void VisitArrayEntryEnd(string path, int index, ArrayPropertyMeta meta)
        {
            logger.Verbose("Ending object read");
            pendingCharacterVisitor.VisitCharacterEnd();

            if (pendingCharacterVisitor.Result != null)
            {
                logger.Verbose("Generated valid character instance");
                var instance = pendingCharacterVisitor.Result;

                instance.InstanceId = (Guid)pendingProperties[K_INSTANCE_ID];
                instance.PlayerId = (Guid?)pendingProperties[K_PLAYER_ID];

                Result.Add(instance);
            }

            pendingCharacterVisitor = null;
            pendingProperties = null;
        }
    }
}
