using PalCalc.SaveReader.FArchive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Support.Level
{
    struct GvasGroup
    {
        public Guid GroupId;
        public string GroupType;
        public string Name;

        public List<Guid> InstanceIds;
    }

    internal class GroupVisitor : IVisitor
    {
        public GroupVisitor() : base(".worldSaveData.GroupSaveDataMap.Value.GroupType")
        {
        }
    }
}
