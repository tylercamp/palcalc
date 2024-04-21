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
        public override string MatchedPath => ".worldSaveData.CharacterContainerSaveData.Value.Slots.Slots.RawData";
        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors)
        {
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
        public override string MatchedPath => ".worldSaveData.CharacterSaveParameterMap.Value.RawData";
        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors)
        {
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

        public string GroupType { get; set; }
        public string GroupName { get; set; }
        public List<EntityInstanceId> CharacterHandleIds { get; set; }
        public Dictionary<string, object> Properties { get; set; }
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
        public override string MatchedPath => ".worldSaveData.GroupSaveDataMap.Value";

        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors)
        {
            var props = reader.ReadPropertiesUntilEnd(path, visitors);

            var groupType = (EnumProperty)props["GroupType"];
            var arrayProp = (ArrayProperty)props["RawData"];

            using (var byteStream = new MemoryStream(arrayProp.ByteValues))
            using (var subReader = reader.Derived(byteStream))
            {
                var pathVisitors = visitors.Where(v => v.Matches(path));
                //var extraVisitors = pathVisitors.SelectMany(v => v)

                var groupId = subReader.ReadGuid();
                var groupName = subReader.ReadString();
                var characterHandleIds = subReader.ReadArray(r => new EntityInstanceId() { Guid = r.ReadGuid(), InstanceId = r.ReadGuid() });

                var resultProps = new Dictionary<string, object>(); // TODO

                if (new List<string>() { "EPalGroupType::Guild", "EPalGroupType::IndependentGuild", "EPalGroupType::Organization" }.Contains(groupType.EnumValue))
                {
                    resultProps.Add("OrgType", subReader.ReadByte());
                    resultProps.Add("BaseIds", subReader.ReadArray(r => r.ReadGuid()));
                }

                if (new string[] { "EPalGroupType::Guild", "EPalGroupType::IndependentGuild" }.Contains(groupType.EnumValue))
                {
                    resultProps.Add("BaseCampLevel", subReader.ReadInt32());
                    resultProps.Add("MapObjectInstanceIdsBaseCampPoints", subReader.ReadArray(r => r.ReadGuid()));
                    resultProps.Add("GuildName", subReader.ReadString());
                }

                if (groupType.EnumValue == "EPalGroupType::IndependentGuild")
                {
                    resultProps.Add("PlayerUid", subReader.ReadGuid());
                    resultProps.Add("GuildName2", subReader.ReadString());
                    resultProps.Add("PlayerLastOnlineRealTime", subReader.ReadInt64());
                    resultProps.Add("PlayerName", subReader.ReadString());
                }

                if (groupType.EnumValue == "EPalGroupType::Guild")
                {
                    resultProps.Add("AdminPlayerUid", subReader.ReadGuid());
                    resultProps.Add("Members", subReader.ReadArray(PlayerReference.ReadFrom));
                }

                return new GroupDataProperty()
                {
                    TypedMeta = new GroupDataPropertyMeta()
                    {
                        Id = groupId,
                        Path = path
                    },
                    GroupName = groupName,
                    GroupType = groupType.EnumValue,
                    CharacterHandleIds = characterHandleIds.ToList(),
                    Properties = resultProps,
                };
            }
        }
    }
}
