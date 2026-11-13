using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class PlayerInstance
    {
        /// <summary>The bare instance GUID of the player's character.</summary>
        public string InstanceId { get; set; }

        /// <summary>The Palworld player GUID used by ownership and player-container references.</summary>
        public string PlayerId { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }

        public string PartyContainerId { get; set; }
        public string PalboxContainerId { get; set; }
        public string DimensionalPalStorageContainerId { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PalGender
    {
        // note: pal world is not P.C., so it only has two genders
        MALE = 0b0001,
        FEMALE = 0b0010,
        // (but this program is P.C., and has four genders)
        WILDCARD = 0b0011,
        OPPOSITE_WILDCARD = 0b0100, // contextual - pals of this gender should be paired with pals of WILDCARD gender

        // for pals read which are missing the `Gender` attribute entirely, not sure
        // how but this apparently can happen
        // https://github.com/tylercamp/palcalc/issues/78
        NONE = 0b1000
    }

    public class PalInstance
    {
        /// <summary>The full game key, when available. Custom Pals and older snapshots may have no key.</summary>
        public PalInstanceId? PalInstanceId { get; init; }

        /// <summary>
        /// PalCalc's container ID for a Global or Dimensional Palbox record. (i.e. not stored directly in the Level.sav file.)
        /// World records use <see langword="null"/>.
        /// </summary>
        /// <remarks>Equality uses this value with the game key within a single loaded save.</remarks>
        public string ExternalStorageContainerId { get; init; }

        public string NickName { get; set; }
        public int Level { get; set; }

        /// <summary>The owner GUID resolved by the save reader, with a fallback to the first ownership history entry.</summary>
        public string OwnerPlayerId { get; set; }

        public Pal Pal { get; set; }
        public PalLocation Location { get; set; }
        public PalGender Gender { get; set; }
        public List<PassiveSkill> PassiveSkills { get; set; }
        public int Rank { get; set; } = 1; // (1 - 5)

        public List<ActiveSkill> ActiveSkills { get; set; }
        public List<ActiveSkill> EquippedActiveSkills { get; set; }

        public bool IsOnExpedition { get; set; }

        public int IV_HP { get; set; }
        public int IV_Shot { get; set; }
        public int IV_Defense { get; set; }

        public int IV_Attack => IV_Shot;

        // supposedly this is deprecated/unused
        // https://www.reddit.com/r/Palworld/comments/1aedboa/partner_skill_upgrade_stats_exact_values_for_lv1/
        public int IV_Melee { get; set; }

        public override bool Equals(object obj) => ReferenceEquals(this, obj) ||
            (obj is PalInstance other && PalInstanceId.HasValue &&
             PalInstanceId == other.PalInstanceId && ExternalStorageContainerId == other.ExternalStorageContainerId);

        public override string ToString() => $"{Gender} {Pal} at {Location} with passive skills ({string.Join(", ", PassiveSkills)})";

        public override int GetHashCode() => PalInstanceId.HasValue
            ? HashCode.Combine(PalInstanceId, ExternalStorageContainerId)
            : base.GetHashCode();
    }
}
