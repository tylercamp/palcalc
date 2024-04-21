using PalCalc.Model;
using PalCalc.SaveReader.FArchive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Support.Level
{
    internal class GroupVisitor : IVisitor
    {
        public GroupVisitor() : base(".worldSaveData.GroupSaveDataMap.Value") { }

        public List<GuildInstance> Result { get; } = new List<GuildInstance>();
        
        public override void VisitCharacterGroupProperty(string path, GroupDataProperty property)
        {
            // seems like every player should always be in a `Guild`?
            if (property.GroupType != GroupType.Guild) return;

            if (property.OrgType != 0) return; // all player guilds I've seen have OrgType 0?

            Result.Add(new GuildInstance()
            {
                Id = property.Meta.Id.ToString(),
                InternalName = property.GroupName,
                Name = property.GuildName,
                OwnerId = property.AdminPlayerUid.ToString(),
                MemberIds = property.Members.Select(p => p.PlayerUid.ToString()).ToList()
            });
        }
    }
}
