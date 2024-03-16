using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.model
{
    internal class BreedingResult
    {
        public Pal Parent1, Parent2, Child;

        public IEnumerable<Pal> Parents
        {
            get
            {
                yield return Parent1;
                yield return Parent2;
            }
        }

        public Pal OtherParent(Pal parent)
        {
            if (parent == Parent1) return Parent2;
            else if (parent == Parent2) return Parent1;
            else return null;
        }

        public override string ToString() => $"{Parent1} + {Parent2} = {Child}";

        public class Serialized
        {
            public PalId Parent1ID { get; set; }
            public PalId Parent2ID { get; set; }
            public PalId Child1ID { get; set; }
        }
    }
}
