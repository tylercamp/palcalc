using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public interface IPalRefLocation { }
    public class OwnedRefLocation : IPalRefLocation
    {
        public string OwnerId { get; set; }
        public PalLocation Location { get; set; }

        public override string ToString() => Location.ToString();
    }

    public class CapturedRefLocation : IPalRefLocation
    {
        public override string ToString() => "(Wild)";

        public static IPalRefLocation Instance { get; } = new CapturedRefLocation();
    }

    public class BredRefLocation : IPalRefLocation
    {
        public override string ToString() => "(Bred)";

        public static IPalRefLocation Instance { get; } = new BredRefLocation();
    }

    public class SurgeryRefLocation : IPalRefLocation
    {
        public override string ToString() => "(Surgery Result)";

        public static IPalRefLocation Instance { get; } = new SurgeryRefLocation();
    };

    public class CompositeRefLocation : IPalRefLocation
    {
        public CompositeRefLocation(IPalRefLocation maleLoc, IPalRefLocation femaleLoc)
        {
            MaleLoc = maleLoc;
            FemaleLoc = femaleLoc;
        }

        public IPalRefLocation MaleLoc { get; }
        public IPalRefLocation FemaleLoc { get; }

        public override string ToString() => $"Either {MaleLoc} (male) or {FemaleLoc} (female)";
    }
}
