using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector.Search
{
    public class ContainerViewModel(string id, LocationType detectedType, List<PalInstance> contents)
    {
        public string Id => id;
        public LocationType DetectedType => detectedType;
        public List<string> OwnerIds { get; } = contents.Select(p => p.OwnerPlayerId).Distinct().ToList();
        public List<PalInstance> Contents => contents;
    }
}
