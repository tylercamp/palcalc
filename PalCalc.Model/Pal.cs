using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MountType
    {
        None,
        Ground,
        Swim,
        Fly,
        FlyLand,
    }

    public class Pal
    {
        public PalId Id { get; set; }
        public string Name { get; set; }

        public string InternalName { get; set; }
        public int InternalIndex { get; set; } // index in the game's internal pal ordering (not paldex)

        public int BreedingPower { get; set; }

        public int RideWalkSpeed { get; set; }
        public int RideSprintSpeed { get; set; }

        public bool CanMount { get; set; }
        public MountType MountType { get; set; }

        public int Stamina { get; set; }

        public override string ToString() => $"{Name} ({Id})";

        public static bool operator ==(Pal a, Pal b) => (ReferenceEquals(a, null) && ReferenceEquals(b, null)) || (a?.Equals(b) ?? false);
        public static bool operator !=(Pal a, Pal b) => !(a == b);

        public override bool Equals(object obj) => (obj as Pal)?.Id == Id;
        public override int GetHashCode() => Id.GetHashCode();
    }
}
