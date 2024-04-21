using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class GuildInstance
    {
        public string Id { get; set; }

        public string InternalName { get; set; }
        public string Name { get; set; }

        public string OwnerId { get; set; }
        public List<string> MemberIds { get; set; }
    }
}
