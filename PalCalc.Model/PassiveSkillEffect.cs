using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class PassiveSkillEffect
    {
        public string InternalName { get; set; }
        public float EffectStrength { get; set; }

        public static string BreedSpeed => "BreedSpeed";

        public static readonly IEnumerable<string> TrackedEffects = [BreedSpeed];
    }
}
