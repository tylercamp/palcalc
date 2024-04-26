using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.FArchive
{
    public abstract class ICustomReader
    {
        public abstract string MatchedPath { get; }
        public abstract IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors);

        public static List<ICustomReader> All = new List<ICustomReader>()
        {
            new CharacterReader(),
            new CharacterContainerReader(),
            new GroupReader(),
        };
    }

    public class CharacterContainerDataPropertyMeta : BasicPropertyMeta
    {
        public Guid? InstanceId => Id;
        public Guid PlayerId { get; set; }
    }

    public class CharacterContainerDataProperty : IProperty
    {
        public IPropertyMeta Meta => TypedMeta;
        public CharacterContainerDataPropertyMeta TypedMeta { get; set; }

        public byte PermissionTribeId;
    }

    public class CharacterContainerReader : ICustomReader
    {
        private static ILogger logger = Log.ForContext<CharacterContainerReader>();

        public override string MatchedPath => ".worldSaveData.CharacterContainerSaveData.Value.Slots.Slots.RawData";
        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors)
        {
            logger.Verbose("decoding");
            var arrayProp = (ArrayProperty)reader.ReadProperty(typeName, size, path, path, visitors);

            var pathVisitors = visitors.Where(v => v.Matches(path));

            using (var byteStream = new MemoryStream(arrayProp.Value as byte[]))
            using (var subReader = reader.Derived(byteStream))
            {
                var meta = new CharacterContainerDataPropertyMeta
                {
                    Path = path,
                    PlayerId = subReader.ReadGuid(),
                    Id = subReader.ReadGuid(),
                };

                foreach (var v in pathVisitors) v.VisitCharacterContainerPropertyBegin(path, meta);

                var result = new CharacterContainerDataProperty
                {
                    TypedMeta = meta,
                    PermissionTribeId = subReader.ReadByte()
                };

                foreach (var v in pathVisitors) v.VisitCharacterContainerPropertyEnd(path, meta);

                logger.Verbose("done");
                return result;
            }
        }
    }

    public class CharacterDataPropertyMeta : BasicPropertyMeta
    {
        public Guid? GroupId => Id;
    }

    public class CharacterDataProperty : IProperty
    {
        public IPropertyMeta Meta { get; set; }

        public Dictionary<string, object> Data { get; set; }
    }

    public class CharacterReader : ICustomReader
    {
        private static ILogger logger = Log.ForContext<CharacterReader>();

        public override string MatchedPath => ".worldSaveData.CharacterSaveParameterMap.Value.RawData";
        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors)
        {
            logger.Verbose("decoding");
            var arrayProp = (ArrayProperty)reader.ReadProperty(typeName, size, path, path, visitors);

            using (var byteStream = new MemoryStream(arrayProp.ByteValues))
            using (var subReader = reader.Derived(byteStream))
            {
                var pathVisitors = visitors.Where(v => v.Matches(path));
                var extraVisitors = pathVisitors.SelectMany(v => v.VisitCharacterPropertyBegin(path)).ToList();

                var newVisitors = visitors.Concat(extraVisitors);

                var data = subReader.ReadPropertiesUntilEnd(path, visitors);
                subReader.ReadBytes(4); // unknown data?

                var meta = new CharacterDataPropertyMeta { Path = path, Id = subReader.ReadGuid() };

                foreach (var v in extraVisitors) v.Exit();
                foreach (var v in pathVisitors) v.VisitCharacterPropertyEnd(path, meta);

                logger.Verbose("done");
                return new CharacterDataProperty
                {
                    Meta = meta,
                    Data = data,
                };
            }
        }
    }

    public class GroupDataPropertyMeta : IPropertyMeta
    {
        public string Path { get; set; }
        public Guid? Id { get; set; }
    }

    public class GroupDataProperty : IProperty
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

    public class GroupReader : ICustomReader
    {
        private static ILogger logger = Log.ForContext<GroupReader>();

        public override string MatchedPath => ".worldSaveData.GroupSaveDataMap.Value";

        private (string, string, ulong) ReadRawProperty(FArchiveReader reader)
        {
            var name = reader.ReadString();
            if (name == "None") throw new Exception(); // TODO

            var typeName = reader.ReadString();
            var size = reader.ReadUInt64();

            return (name, typeName, size);
        }

        private string ReadGroupType(FArchiveReader reader)
        {
            var (propName, typeName, size) = ReadRawProperty(reader);

            if (propName != "GroupType") throw new Exception(); // TODO
            if (typeName != "EnumProperty") throw new Exception(); // TODO
            
            var enumType = reader.ReadString();
            if (enumType != "EPalGroupType") throw new Exception(); // TODO

            var enumId = reader.ReadOptionalGuid();

            return reader.ReadString();
        }

        private byte[] ReadRawData(FArchiveReader reader)
        {
            var (propName, typeName, size) = ReadRawProperty(reader);

            if (propName != "RawData") throw new Exception(); // TODO
            if (typeName != "ArrayProperty") throw new Exception(); // TODO

            var arrayType = reader.ReadString();
            if (arrayType != "ByteProperty") throw new Exception(); // TODO

            var id = reader.ReadOptionalGuid();
            var count = reader.ReadUInt32();

            if (count != size - 4) throw new Exception("Labelled ByteProperty not implemented"); // sic

            return reader.ReadBytes((int)count);
        }

        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors)
        {
            logger.Verbose("decoding");

            // manually read properties instead of using FArchiveReader property helpers to preserve values regardless
            // of ARCHIVE_PRESERVE flag
            var groupTypeString = ReadGroupType(reader);
            var rawDataBytes = ReadRawData(reader);
            reader.ReadString(); // skip "None" at end of property list

            logger.Verbose("groupType is {groupTypeString}", groupTypeString);

            var groupType = groupTypeString switch
            {
                "EPalGroupType::Neutral" => GroupType.Neutral,
                "EPalGroupType::Guild" => GroupType.Guild,
                "EPalGroupType::IndependentGuild" => GroupType.IndependentGuild,
                "EPalGroupType::Organization" => GroupType.Organization,
                _ => GroupType.Unrecognized
            };

            using (var byteStream = new MemoryStream(rawDataBytes))
            using (var subReader = reader.Derived(byteStream))
            {
                var result = new GroupDataProperty() { TypedMeta = new GroupDataPropertyMeta() { Path = path } };

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
                    result.Members = subReader.ReadArray(PlayerReference.ReadFrom);
                }

                foreach (var v in visitors.Where(v => v.Matches(path)))
                    v.VisitCharacterGroupProperty(path, result);

                logger.Verbose("done");
                return result;
            }
        }
    }
}
