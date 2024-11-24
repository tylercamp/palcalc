using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class BaseInstance
    {
        public string Id { get; set; }
        public string OwnerGuildId { get; set; }
        
        public BasePalContainer Container { get; set; }

        public List<ViewingCageContainer> ViewingCages { get; set; }

        public double WorldX { get; set; }
        public double WorldY { get; set; }
        public double WorldZ { get; set; }
        
    }
}
