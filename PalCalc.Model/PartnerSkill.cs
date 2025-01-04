using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class PartnerSkill
    {
        public List<RankEffect> RankEffects { get; set; }

        public class RankEffect
        {
            public List<string> PassiveInternalNames { get; set; }

            public IEnumerable<PassiveSkill> PassiveSkills(PalDB db) => PassiveInternalNames.Select(n => db.PassiveSkills.FirstOrDefault(p => p.InternalName == n));
        }
    }
}
