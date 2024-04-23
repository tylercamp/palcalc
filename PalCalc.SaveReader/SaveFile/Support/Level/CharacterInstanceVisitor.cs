using PalCalc.Model;
using PalCalc.SaveReader.FArchive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Support.Level
{
    class GvasCharacterInstance
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

        public List<string> Traits { get; set; }
    }

    class CharacterInstanceVisitor : IVisitor
    {
        public CharacterInstanceVisitor() : base(".worldSaveData.CharacterSaveParameterMap") { }

        public List<GvasCharacterInstance> Result = new List<GvasCharacterInstance>();

        GvasCharacterInstance pendingInstance = null;

        const string K_INSTANCE_ID          = ".Key.InstanceId";
        const string K_PLAYER_ID            = ".Key.PlayerUId";
        const string K_NICKNAME             = ".Value.RawData.SaveParameter.NickName";
        const string K_LEVEL                = ".Value.RawData.SaveParameter.Level";
        const string K_CHARACTER_ID         = ".Value.RawData.SaveParameter.CharacterID";
        const string K_IS_PLAYER            = ".Value.RawData.SaveParameter.IsPlayer";
        const string K_GENDER               = ".Value.RawData.SaveParameter.Gender";
        const string K_CONTAINER_ID         = ".Value.RawData.SaveParameter.SlotID.ContainerId.ID";
        const string K_CONTAINER_SLOT_INDEX = ".Value.RawData.SaveParameter.SlotID.SlotIndex";
        const string K_PASSIVE_SKILL_LIST   = ".Value.RawData.SaveParameter.PassiveSkillList";
        const string K_OWNER_PLAYER_ID      = ".Value.RawData.SaveParameter.OwnerPlayerUId";
        const string K_OLD_OWNER_PLAYER_IDS = ".Value.RawData.SaveParameter.OldOwnerPlayerUIds.OldOwnerPlayerUIds";

        static readonly List<string> REQUIRED_PAL_PROPS = new List<string>()
        {
            K_CHARACTER_ID,
            K_GENDER,
            K_CONTAINER_ID,
            K_CONTAINER_SLOT_INDEX,
        };

        public override IEnumerable<IVisitor> VisitMapEntryBegin(string path, int index, MapPropertyMeta meta)
        {
            base.VisitMapEntryBegin(path, index, meta);

            pendingInstance = new GvasCharacterInstance();

            var collectingVisitor = new ValueCollectingVisitor(this,
                K_PLAYER_ID,
                K_INSTANCE_ID,
                K_NICKNAME,
                K_LEVEL,
                K_CHARACTER_ID,
                K_IS_PLAYER,
                K_GENDER,
                K_CONTAINER_ID,
                K_CONTAINER_SLOT_INDEX,
                K_OWNER_PLAYER_ID
            );

            collectingVisitor.OnExit += (vals) =>
            {
                pendingInstance.InstanceId = (Guid)vals[K_INSTANCE_ID];
                pendingInstance.IsPlayer = (bool)vals.GetValueOrElse(K_IS_PLAYER, false);

                // level 1 (i.e. "default level") instances don't have a Level property
                pendingInstance.Level = (int)vals.GetValueOrElse(K_LEVEL, 1);

                if (pendingInstance.IsPlayer)
                {
                    pendingInstance.PlayerId = (Guid?)vals[K_PLAYER_ID];
                    pendingInstance.NickName = (string)vals[K_NICKNAME];
                }
                else
                {
                    var missingProps = REQUIRED_PAL_PROPS.Where(p => !vals.ContainsKey(p)).ToList();
                    if (missingProps.Any())
                    {
                        // TODO - log missing props
                        pendingInstance = null;
                        return;
                    }

                    pendingInstance.NickName = vals.GetValueOrDefault(K_NICKNAME)?.ToString();
                    
                    pendingInstance.OwnerPlayerId = (Guid?)vals.GetValueOrDefault(K_OWNER_PLAYER_ID);
                    pendingInstance.CharacterId = (string)vals[K_CHARACTER_ID];
                    pendingInstance.Gender = (string)vals[K_GENDER];

                    pendingInstance.ContainerId = (Guid)vals[K_CONTAINER_ID];
                    pendingInstance.SlotIndex = (int)vals[K_CONTAINER_SLOT_INDEX];
                }
            };

            yield return collectingVisitor;

            List<string> traits = new List<string>();
            var traitsVisitor = new ValueEmittingVisitor(this, K_PASSIVE_SKILL_LIST);
            traitsVisitor.OnValue += (_, v) =>
            {
                traits.Add(v.ToString());
            };

            traitsVisitor.OnExit += () =>
            {
                if (pendingInstance != null) pendingInstance.Traits = traits;
            };

            yield return traitsVisitor;

            List<Guid> oldOwnerIds = new List<Guid>();
            var oldOwnersVisitor = new ValueEmittingVisitor(this, K_OLD_OWNER_PLAYER_IDS);
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

        public override void VisitMapEntryEnd(string path, int index, MapPropertyMeta meta)
        {
            if (pendingInstance != null) Result.Add(pendingInstance);

            pendingInstance = null;
        }
    }
}
