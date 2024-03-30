using PalCalc.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc
{
    internal static class PalExtensions
    {
        public static Pal ToPal(this string s, PalDB db) => db.Pals.Single(p => p.Name == s);
        public static Pal ToPal(this PalId id, PalDB db) => db.PalsById[id];

        public static Trait ToTrait(this string s, PalDB db) => db.TraitsByName[s];
    }
}
