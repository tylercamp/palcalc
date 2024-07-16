using PalCalc.UI.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Details
{
    public enum OwnerType { Player, Guild, Unknown }

    public class OwnerViewModel(OwnerType type, ILocalizedText name, string id)
    {
        public OwnerType Type { get; } = type;
        public ILocalizedText Name { get; } = name;
        public string Id { get; } = id;

        private static ILocalizedText LocalizedUnknown => LocalizationCodes.LC_COMMON_UNKNOWN.Bind();

        public static OwnerViewModel Unknown => new OwnerViewModel(OwnerType.Unknown, LocalizedUnknown, "null");
        public static OwnerViewModel UnknownWithId(string id) => new OwnerViewModel(OwnerType.Unknown, LocalizedUnknown, id);
    }
}
