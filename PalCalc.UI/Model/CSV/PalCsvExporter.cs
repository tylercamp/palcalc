using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.FImpl.AttrId;
using PalCalc.Solver.PalReference;
using PalCalc.UI.Localization;
using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Services.Maps.LocalSearch;

namespace PalCalc.UI.Model.CSV
{
    static class PalCSVExporter
    {
        private class PalPassiveSkillsSerializer : CSVPropertySerializer<PalInstanceViewModel>
        {
            public List<string> ColumnsReservations(IEnumerable<PalInstanceViewModel> items)
            {
                var maxPassives = items.Select(i => i.ModelObject.PassiveSkills.Count).DefaultIfEmpty(0).Max();
                return [
                    LocalizationCodes.LC_COMMON_TRAITS.Bind().Value,
                    .. Enumerable.Repeat("", maxPassives - 1)
                ];
            }

            public List<string> ValuesOf(PalInstanceViewModel item) => item.PassiveSkills.Passives.Select(p => p.Name.Value).ToList();
        }

        private class PalActiveSkillsSerializer : CSVPropertySerializer<PalInstanceViewModel>
        {
            public List<string> ColumnsReservations(IEnumerable<PalInstanceViewModel> items)
            {
                var maxActives = items.Select(i => i.ModelObject.ActiveSkills.Count).DefaultIfEmpty(0).Max();
                return [
                    LocalizationCodes.LC_COMMON_ATTACK_SKILLS.Bind().Value,
                    .. Enumerable.Repeat("", maxActives - 1)
                ];
            }

            public List<string> ValuesOf(PalInstanceViewModel item) => item.ActiveSkills.Select(s => s.Name.Value).ToList();
        }

        public static string Export(PalDB db, CachedSaveGame csg, GameSettings settings)
        {
            SimpleCSVPropertySerializer<PalInstanceViewModel> Simple(string col, Func<PalInstanceViewModel, object> sel) =>
                new(col, p => sel(p)?.ToString() ?? "");

            SimpleCSVPropertySerializer<PalInstanceViewModel> SimplePalRef(string col, Func<IPalReference, object> sel) =>
                Simple(col, p => sel(new OwnedPalReference(db, p.ModelObject, [], FIVSet.AllRandom)));

            SimpleCSVPropertySerializer<PalInstanceViewModel> SimplePalLoc(string col, Func<SpecificPalRefLocationViewModel, object> sel) =>
                SimplePalRef(col, p => sel(new SpecificPalRefLocationViewModel(csg, settings, p.Location)));

            var exporter = new CSVExporter<PalInstanceViewModel>([
                Simple(LocalizationCodes.LC_COMMON_PAL.Bind().Value, p => p.Pal.Name.Value),
                Simple(LocalizationCodes.LC_COMMON_GENDER.Bind().Value, p => p.Gender.Label.Value),
                Simple(LocalizationCodes.LC_COMMON_LEVEL.Bind().Value, p => p.ModelObject.Level),
                SimplePalLoc(LocalizationCodes.LC_COMMON_OWNER.Bind().Value, loc => loc.LocationOwnerDescription?.Value),
                SimplePalLoc(LocalizationCodes.LC_COMMON_LOCATION.Bind().Value, loc => loc.LocationCoordDescription?.Value),
                SimplePalLoc(LocalizationCodes.LC_COMMON_MAP_COORDS.Bind().Value, loc => loc.MapLocationPreview?.MapCoord?.DisplayCoordsText),

                Simple(LocalizationCodes.LC_COMMON_IV_HP_SHORT.Bind().Value, p => p.ModelObject.IV_HP),
                Simple(LocalizationCodes.LC_COMMON_IV_ATTACK_SHORT.Bind().Value, p => p.ModelObject.IV_Attack),
                Simple(LocalizationCodes.LC_COMMON_IV_DEFENSE_SHORT.Bind().Value, p => p.ModelObject.IV_Defense),

                new PalPassiveSkillsSerializer(),
                new PalActiveSkillsSerializer(),
            ]);

            return exporter.Export(csg.OwnedPals.Select(p => new PalInstanceViewModel(p)).ToList());
        }
    }
}
