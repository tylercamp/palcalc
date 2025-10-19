using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public interface IPalworldCharacterInstance
    {
        string InstanceId { get; }
    }

    public class PlayerInstance : IPalworldCharacterInstance
    {
        public string InstanceId { get; set; }

        public string PlayerId { get; set; } // typically this is referenced instead of InstanceId
        public string Name { get; set; }
        public int Level { get; set; }

        public string PartyContainerId { get; set; }
        public string PalboxContainerId { get; set; }
        public string DimensionalPalStorageContainerId { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PalGender : byte
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

    public class PalInstance : IPalworldCharacterInstance
    {
        public string InstanceId { get; set; }
        public string NickName { get; set; }
        public int Level { get; set; }

        public string OwnerPlayerId { get; set; }

        public Pal Pal { get; set; }
        public PalLocation Location { get; set; }
        public PalGender Gender { get; set; }
        public List<PassiveSkill> PassiveSkills { get; set; }
        public int Rank { get; set; } // (1 - 5)

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

        public override bool Equals(object obj) => (obj as PalInstance)?.InstanceId == InstanceId;

        public override string ToString() => $"{Gender} {Pal} at {Location} with passive skills ({string.Join(", ", PassiveSkills)})";

        private int? hashCode;
        public override int GetHashCode()
        {
            if (hashCode == null)
            {
                hashCode = HashCode.Combine(
                    HashCode.Combine(
                        InstanceId,
                        NickName,
                        Level,
                        OwnerPlayerId
                    ),
                    HashCode.Combine(
                        Pal,
                        Location,
                        Gender,
                        PassiveSkills.SetHash()
                    ),
                    HashCode.Combine(
                        IV_HP,
                        IV_Melee,
                        IV_Shot,
                        IV_Defense
                    )
                );
            }
            return hashCode.Value;
        }
    }
}
