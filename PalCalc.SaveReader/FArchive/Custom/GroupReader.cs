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
                        var startPos = byteStream.Position;

                        try
                        {
                            // parse using the new format
                            result.AdminPlayerUid = subReader.ReadGuid();

                            // https://github.com/cheahjs/palworld-save-tools/issues/192
                            subReader.ReadInt64();
                            subReader.ReadInt64();

                            result.Members = subReader.ReadArray(PlayerReference.ReadFrom);
                        }
                        catch (Exception e)
                        {
                            if (e is EndOfStreamException || e is ArgumentOutOfRangeException)
                            {
                                logger.Debug("EndOfStreamException while reading guild data using Feybreak format, falling back to old format");
                                byteStream.Seek(startPos, SeekOrigin.Begin);

                                // as a fallback, try parsing with the old format
                                // parse using the new format
                                result.AdminPlayerUid = subReader.ReadGuid();
                                result.Members = subReader.ReadArray(PlayerReference.ReadFrom);
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }

                    foreach (var v in visitors.Where(v => v.Matches(path)))
                        v.VisitCharacterGroupProperty(path, result);

                    logger.Verbose("done");
                    return result;
                }
            });
        }
    }
}
