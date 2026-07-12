using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.FArchive.Custom
{
    public class GroupDataPropertyMeta : IPropertyMeta
    {
        public string Path { get; set; }
        public Guid? Id { get; set; }
    }

    public class GroupDataProperty : ICustomProperty
    {
        public IPropertyMeta Meta => TypedMeta;
        public GroupDataPropertyMeta TypedMeta { get; set; }

        public GroupType GroupType { get; set; }
        public string GroupName { get; set; }
        public EntityInstanceId[] CharacterHandleIds { get; set; }

        // For `Meta` match with `Mask_HasBaseIds`
        public byte? OrgType { get; set; }
        public Guid[] BaseIds { get; set; }

        // For `Meta` match with `Mask_HasGuildName`
        public int? BaseCampLevel { get; set; }
        public Guid[] MapObjectBasePointInstanceIds { get; set; }
        public string GuildName { get; set; }

        // For `IndependentGuild`
        public Guid? PlayerUid { get; set; }
        public string GuildName2 { get; set; } // ?
        public long? PlayerLastOnlineRealTime { get; set; }
        public string PlayerName { get; set; }

        // For `Guild`
        public Guid? AdminPlayerUid { get; set; }
        public PlayerReference[] Members { get; set; }

        // V4 Guild fields
        public GuildMarkerData[] GuildMarkers { get; set; }
        public byte[] GuildChestAllowedRoles { get; set; }
        public RolePermission[] RolePermissions { get; set; }

        public override string ToString()
        {
            switch (GroupType)
            {
                case GroupType.Guild: return $"Group '{GroupName}' - Guild ({OrgType}) '{GuildName}' w/ {Members.Length} members";
                case GroupType.IndependentGuild: return $"Group '{GroupName}' - IndependentGuild ({OrgType}) '{GuildName}'/'{GuildName2}' of '{PlayerName}'";
                case GroupType.Organization: return $"Group '{GroupName}' - Organization ({OrgType}) w/ {CharacterHandleIds.Length} char handles, {BaseIds.Length} base ids";
                default: return $"Group '{GroupName}' - {Enum.GetName(GroupType)} w/ {CharacterHandleIds.Length} char handles";
            }
        }

        public void Traverse(Action<IProperty> action) { }
    }

    [Flags]
    public enum GroupType
    {
        Guild = 0b_0_0001,
        IndependentGuild = 0b_0_0010,
        Organization = 0b_0_0100,
        Neutral = 0b_0_1000,

        Unrecognized = 0b_1_0000,

        Mask_HasBaseIds = Guild | IndependentGuild | Organization,
        Mask_HasGuildName = Guild | IndependentGuild,
    }

    public struct EntityInstanceId
    {
        public Guid Guid;
        public Guid InstanceId;
    }

    public struct PlayerReference
    {
        public Guid PlayerUid; // corresponds to EntityInstanceId.Guid (but what does it imply exactly??)
        public Int64 LastOnlineRealTime;
        public string PlayerName;

        public static PlayerReference ReadFrom(FArchiveReader reader)
        {
            return new PlayerReference()
            {
                PlayerUid = reader.ReadGuid(),
                LastOnlineRealTime = reader.ReadInt64(),
                PlayerName = reader.ReadString(),
            };
        }
    }

    public struct GuildPlayerReference
    {
        public Guid PlayerUid;
        public Int64 LastOnlineRealTime;
        public string PlayerName;
        public byte Role; // EPalGuildRole

        public static GuildPlayerReference ReadFrom(FArchiveReader reader)
        {
            return new GuildPlayerReference()
            {
                PlayerUid = reader.ReadGuid(),
                LastOnlineRealTime = reader.ReadInt64(),
                PlayerName = reader.ReadString(),
                Role = reader.ReadByte(),
            };
        }
    }

    public struct GuildMarkerData
    {
        public Guid MarkerId;
        public Dictionary<string, VectorLiteral> IconLocation;
        public int IconType;
        public Guid OwnerPlayerUid;

        public static GuildMarkerData ReadFrom(FArchiveReader reader)
        {
            var iconLocation = new Dictionary<string, VectorLiteral>();
            var dictCount = reader.ReadUInt32();
            for (int i = 0; i < dictCount; i++)
            {
                var key = reader.ReadString();
                var value = reader.ReadVector();
                iconLocation[key] = value;
            }
            return new GuildMarkerData()
            {
                MarkerId = reader.ReadGuid(),
                IconLocation = iconLocation,
                IconType = reader.ReadInt32(),
                OwnerPlayerUid = reader.ReadGuid(),
            };
        }
    }

    public struct RolePermission
    {
        public byte Role; // EPalGuildRole
        public byte[] Permissions; // EPalGuildPermission

        public static RolePermission ReadFrom(FArchiveReader reader)
        {
            return new RolePermission()
            {
                Role = reader.ReadByte(),
                Permissions = reader.ReadArray(r => r.ReadByte()),
            };
        }
    }

    public class GroupReader : ICustomReader
    {
        private static ILogger logger = Log.ForContext<GroupReader>();

        public override string MatchedPath => ".worldSaveData.GroupSaveDataMap.Value";

        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors)
        {
            return reader.WithArchivePreserveOverride(true, () =>
            {
                logger.Verbose("decoding");

                var props = reader.ReadPropertiesUntilEnd(path, Enumerable.Empty<IVisitor>());

                var groupTypeString = (props["GroupType"] as EnumProperty).EnumValue;
                var rawDataBytes = (props["RawData"] as ArrayProperty).ByteValues;

                logger.Verbose("groupType is {groupTypeString}", groupTypeString);

                var groupType = groupTypeString switch
                {
                    "EPalGroupType::Neutral" => GroupType.Neutral,
                    "EPalGroupType::Guild" => GroupType.Guild,
                    "EPalGroupType::IndependentGuild" => GroupType.IndependentGuild,
                    "EPalGroupType::Organization" => GroupType.Organization,
                    _ => GroupType.Unrecognized
                };

                List<Func<GroupType, FArchiveReader, GroupDataProperty>> parsers = [
                    ParseStream_V4_PalworldV1_0_0,
                    ParseStream_V3_TidesOfTerraria,
                    ParseStream_V2_Feybreak,
                    ParseStream_V1,
                ];

                foreach (var parser in parsers)
                {
                    GroupDataProperty result = null;
                    try
                    {
                        using (var byteStream = new MemoryStream(rawDataBytes))
                        using (var subReader = reader.Derived(byteStream))
                        {
                            result = parser(groupType, subReader);
                            result.TypedMeta.Path = path;
                        }
                    }
                    catch {}

                    if (result != null)
                    {
                        foreach (var v in visitors.Where(v => v.Matches(path)))
                            v.VisitCharacterGroupProperty(path, result);

                        logger.Verbose("done");
                        return result;
                    }
                }

                throw new Exception("All attempts to parse guild data failed!");
            });
        }

        private static GroupDataProperty ParseStream_V1(GroupType groupType, FArchiveReader subReader)
        {
            var result = new GroupDataProperty() { TypedMeta = new GroupDataPropertyMeta() };

            result.TypedMeta.Id = subReader.ReadGuid();
            result.GroupType = groupType;
            result.GroupName = subReader.ReadString();
            result.CharacterHandleIds = subReader.ReadArray(r => new EntityInstanceId() { Guid = r.ReadGuid(), InstanceId = r.ReadGuid() });

            if (GroupType.Mask_HasBaseIds.HasFlag(groupType))
            {
                result.OrgType = subReader.ReadByte();
                result.BaseIds = subReader.ReadArray(r => r.ReadGuid());
            }

            if (GroupType.Mask_HasGuildName.HasFlag(groupType))
            {
                result.BaseCampLevel = subReader.ReadInt32();
                result.MapObjectBasePointInstanceIds = subReader.ReadArray(r => r.ReadGuid());
                result.GuildName = subReader.ReadString();
            }

            if (groupType == GroupType.IndependentGuild)
            {
                result.PlayerUid = subReader.ReadGuid();
                result.GuildName2 = subReader.ReadString();
                result.PlayerLastOnlineRealTime = subReader.ReadInt64();
                result.PlayerName = subReader.ReadString();
            }

            if (groupType == GroupType.Guild)
            {
                // parse using the new format
                result.AdminPlayerUid = subReader.ReadGuid();
                result.Members = subReader.ReadArray(PlayerReference.ReadFrom);
            }

            return result;
        }

        private static GroupDataProperty ParseStream_V2_Feybreak(GroupType groupType, FArchiveReader subReader)
        {
            var result = new GroupDataProperty() { TypedMeta = new GroupDataPropertyMeta() };

            result.TypedMeta.Id = subReader.ReadGuid();
            result.GroupType = groupType;
            result.GroupName = subReader.ReadString();
            result.CharacterHandleIds = subReader.ReadArray(r => new EntityInstanceId() { Guid = r.ReadGuid(), InstanceId = r.ReadGuid() });

            if (GroupType.Mask_HasBaseIds.HasFlag(groupType))
            {
                result.OrgType = subReader.ReadByte();
                result.BaseIds = subReader.ReadArray(r => r.ReadGuid());
            }

            if (GroupType.Mask_HasGuildName.HasFlag(groupType))
            {
                result.BaseCampLevel = subReader.ReadInt32();
                result.MapObjectBasePointInstanceIds = subReader.ReadArray(r => r.ReadGuid());
                result.GuildName = subReader.ReadString();
            }

            if (groupType == GroupType.IndependentGuild)
            {
                result.PlayerUid = subReader.ReadGuid();
                result.GuildName2 = subReader.ReadString();
                result.PlayerLastOnlineRealTime = subReader.ReadInt64();
                result.PlayerName = subReader.ReadString();
            }

            if (groupType == GroupType.Guild)
            {
                result.AdminPlayerUid = subReader.ReadGuid();

                // https://github.com/cheahjs/palworld-save-tools/issues/192
                subReader.ReadInt64();
                subReader.ReadInt64();

                result.Members = subReader.ReadArray(PlayerReference.ReadFrom);
            }

            return result;
        }

        private static GroupDataProperty ParseStream_V3_TidesOfTerraria(GroupType groupType, FArchiveReader subReader)
        {
            var result = new GroupDataProperty() { TypedMeta = new GroupDataPropertyMeta() };

            result.TypedMeta.Id = subReader.ReadGuid();
            result.GroupType = groupType;
            result.GroupName = subReader.ReadString();
            result.CharacterHandleIds = subReader.ReadArray(r => new EntityInstanceId() { Guid = r.ReadGuid(), InstanceId = r.ReadGuid() });

            if (GroupType.Mask_HasBaseIds.HasFlag(groupType))
            {
                subReader.ReadInt32(); // ? NEW
                result.OrgType = subReader.ReadByte();
                result.BaseIds = subReader.ReadArray(r => r.ReadGuid());
            }

            if (GroupType.Mask_HasGuildName.HasFlag(groupType))
            {
                subReader.ReadInt32(); // ? NEW
                result.BaseCampLevel = subReader.ReadInt32();
                result.MapObjectBasePointInstanceIds = subReader.ReadArray(r => r.ReadGuid());
                result.GuildName = subReader.ReadString();
            }

            if (groupType == GroupType.IndependentGuild)
            {
                result.PlayerUid = subReader.ReadGuid();
                result.GuildName2 = subReader.ReadString();
                result.PlayerLastOnlineRealTime = subReader.ReadInt64();
                result.PlayerName = subReader.ReadString();
            }

            if (groupType == GroupType.Guild)
            {
                // parse using the new format
                result.AdminPlayerUid = subReader.ReadGuid();

                // https://github.com/cheahjs/palworld-save-tools/issues/192
                subReader.ReadInt64();
                subReader.ReadInt64();

                subReader.ReadInt32(); // ? NEW

                result.Members = subReader.ReadArray(PlayerReference.ReadFrom);
            }

            return result;
        }

        private static GroupDataProperty ParseStream_V4_PalworldV1_0_0(GroupType groupType, FArchiveReader subReader)
        {
            // Ref: https://github.com/oMaN-Rod/palworld-save-tools/blob/b34cf3c514c76b4cfa5653a6b44a7d7cf041692b/palworld_save_tools/rawdata/group.py

            var result = new GroupDataProperty() { TypedMeta = new GroupDataPropertyMeta() };

            result.TypedMeta.Id = subReader.ReadGuid();
            result.GroupType = groupType;
            result.GroupName = subReader.ReadString();
            result.CharacterHandleIds = subReader.ReadArray(r => new EntityInstanceId() { Guid = r.ReadGuid(), InstanceId = r.ReadGuid() });

            if (GroupType.Mask_HasBaseIds.HasFlag(groupType))
            {
                result.OrgType = subReader.ReadByte();
            }

            if (groupType == GroupType.Organization)
            {
                subReader.Skip(12); // trailing_bytes
            }

            if (groupType == GroupType.IndependentGuild)
            {
                result.BaseCampLevel = subReader.ReadInt32();
                result.MapObjectBasePointInstanceIds = subReader.ReadArray(r => r.ReadGuid());
                result.GuildName = subReader.ReadString();
                result.PlayerUid = subReader.ReadGuid();
                result.GuildName2 = subReader.ReadString();
                result.PlayerLastOnlineRealTime = subReader.ReadInt64();
                result.PlayerName = subReader.ReadString();

                if (result.PlayerLastOnlineRealTime < 0)
                    throw new ArgumentOutOfRangeException($"Parsed PlayerLastOnlineRealTime {result.PlayerLastOnlineRealTime} is less than zero");
            }

            if (groupType == GroupType.Guild)
            {
                subReader.ReadBytes(4); // leading_bytes
                result.BaseIds = subReader.ReadArray(r => r.ReadGuid());
                subReader.ReadInt32();  // unknown_1
                result.BaseCampLevel = subReader.ReadInt32();
                result.MapObjectBasePointInstanceIds = subReader.ReadArray(r => r.ReadGuid());
                result.GuildName = subReader.ReadString();
                subReader.ReadGuid();   // last_guild_name_modifier_player_uid
                result.GuildMarkers = subReader.ReadArray(GuildMarkerData.ReadFrom);

                if (result.BaseIds.Length != result.MapObjectBasePointInstanceIds.Length)
                    throw new ArgumentException($"Guild BaseIds length of {result.BaseIds.Length} != MapObjectBasePointInstanceIds length {result.MapObjectBasePointInstanceIds.Length}");

                ReadGuildTail(groupType, subReader, result);
            }

            if (result.BaseCampLevel < 0 || result.BaseCampLevel > 100)
                throw new ArgumentOutOfRangeException($"Parsed BaseCampLevel {result.BaseCampLevel} is outside the expected range [0 100]");

            return result;
        }

        private static void ReadGuildTail(GroupType groupType, FArchiveReader reader, GroupDataProperty result)
        {
            byte[] tail = reader.ReadBytes((int)(reader.StreamSize - reader.StreamPosition));

            using (var tailStream = new MemoryStream(tail))
            using (var tailReader = reader.Derived(tailStream))
            {
                // Try v2 tail (2026-07 update: chest roles, per-player role, permissions)
                result.GuildChestAllowedRoles = tailReader.ReadArray(r => r.ReadByte());
                tailReader.ReadInt32(); // unknown_i32

                result.AdminPlayerUid = tailReader.ReadGuid();
                var v2Players = tailReader.ReadArray(GuildPlayerReference.ReadFrom);
                result.RolePermissions = tailReader.ReadArray(RolePermission.ReadFrom);
                tailReader.ReadBytes(4); // trailing_bytes

                if (tailReader.StreamPosition == tailReader.StreamSize)
                {
                    result.Members = Array.ConvertAll(v2Players, p => new PlayerReference
                    {
                        PlayerUid = p.PlayerUid,
                        LastOnlineRealTime = p.LastOnlineRealTime,
                        PlayerName = p.PlayerName,
                    });
                    return;
                }

                // v2 didn't consume all bytes — fall back to v1
                tailStream.Seek(0, SeekOrigin.Begin);
            }

            using (var tailStream = new MemoryStream(tail))
            using (var tailReader = reader.Derived(tailStream))
            {
                result.AdminPlayerUid = tailReader.ReadGuid();
                result.Members = tailReader.ReadArray(PlayerReference.ReadFrom);
                tailReader.ReadBytes(4); // trailing_bytes

                if (tailReader.StreamPosition != tailReader.StreamSize)
                    throw new Exception("Guild tail: neither v1 nor v2 format consumed all bytes");
            }
        }
    }
}
