using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Details
{
    public enum OwnerType { Player, Guild, Unknown }

    public class OwnerViewModel(OwnerType type, string name, string id)
    {
        public OwnerType Type { get; } = type;
        public string Name { get; } = name;
        public string Id { get; } = id;

        public static OwnerViewModel Unknown => new OwnerViewModel(OwnerType.Unknown, "Unknown", "null");
        public static OwnerViewModel UnknownWithId(string id) => new OwnerViewModel(OwnerType.Unknown, "Unknown", id);
    }
}
