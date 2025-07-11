﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public static class PalExtensions
    {
        public static Pal ToPal(this string s, PalDB db) => db.Pals.Single(p => p.Name == s);
        public static Pal ToPal(this string s, IEnumerable<Pal> pals) => pals.Single(p => p.Name == s);
        public static Pal InternalToPal(this string s, PalDB db) => db.Pals.Single(p => p.InternalName.Equals(s, StringComparison.OrdinalIgnoreCase));
        public static Pal InternalToPal(this string s, IEnumerable<Pal> pals) => pals.Single(p => p.InternalName.Equals(s, StringComparison.OrdinalIgnoreCase));

        // GetValueOrElse a hackfix for change in "Variant" classification after change in data scraping method
        public static Pal ToPal(this PalId id, PalDB db) => db.PalsById.GetValueFromAny(id, id.InvertedVariant);
        public static Pal ToPal(this PalId id, IEnumerable<Pal> pals) => pals.Single(p => p.Id == id);

        private static PassiveSkill RAND_REF = new RandomPassiveSkill();
        public static PassiveSkill ToStandardPassive(this string s, PalDB db)
        {
            if (s == null) return null;
            else if (s == RAND_REF.Name) return new RandomPassiveSkill();
            else if (db.StandardPassiveSkillsByName.ContainsKey(s)) return db.StandardPassiveSkillsByName[s];
            else if (db.StandardPassiveSkills.Any(t => t.InternalName == s)) return db.StandardPassiveSkills.Single(t => t.InternalName == s);
            else return new UnrecognizedPassiveSkill(s);
        }
        public static PassiveSkill InternalToStandardPassive(this string s, PalDB db)
        {
            if (s == null) return null;
            else if (s == RAND_REF.InternalName) return new RandomPassiveSkill();
            else return db.StandardPassiveSkills.SingleOrDefault(t => t.InternalName == s) ?? new UnrecognizedPassiveSkill(s);
        }

        public static PalElement ToElement(this string s, PalDB db) => db.Elements.SingleOrDefault(el => el.InternalName == s) ?? new UnrecognizedPalElement(s);

        public static ActiveSkill ToActive(this string s, PalDB db) => db.ActiveSkills.SingleOrDefault(a => a.InternalName == s) ?? new UnrecognizedActiveSkill(s);

        public static PalGender OppositeGender(this PalGender gender)
        {
            switch (gender)
            {
                case PalGender.MALE: return PalGender.FEMALE;
                case PalGender.FEMALE: return PalGender.MALE;
                case PalGender.WILDCARD: return PalGender.OPPOSITE_WILDCARD;
                case PalGender.OPPOSITE_WILDCARD: return PalGender.WILDCARD;
                case PalGender.NONE: throw new InvalidOperationException("'No gender' has no opposite gender");
                default: throw new NotImplementedException();
            }
        }

        public static string PassiveSkillListToString(this IEnumerable<PassiveSkill> passives)
        {
            if (!passives.Any()) return "no passives";

            var definite = passives.Where(t => t is not IUnknownPassive);
            var random = passives.Where(t => t is RandomPassiveSkill);
            var unrecognized = passives.Where(t => t is UnrecognizedPassiveSkill);

            var parts = new List<string>();
            if (definite.Any()) parts.Add(string.Join(", ", definite));
            if (random.Any()) parts.Add($"{random.Count()} random");
            if (unrecognized.Any()) parts.Add($"{unrecognized.Count()} unrecognized");

            return string.Join(", ", parts);
        }

        public static TimeSpan IncubationTime(this EggSize eggSize, GameSettings settings)
        {
            // https://palworld.fandom.com/wiki/Egg_Incubator

            var modifier = eggSize switch
            {
                // No egg size, i.e. can't hatch
                EggSize.None => 0.000001,
                EggSize.Normal => 12,
                EggSize.Large => 2,
                EggSize.Huge => 1,
                _ => throw new NotImplementedException(),
            };

            return settings.MassiveEggIncubationTime / modifier;
        }
    }
}
