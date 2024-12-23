using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;

namespace PalCalc.UI.ViewModel.Mapped
{
    public class MapCoordViewModel(WorldCoord worldCoord)
    {
        public WorldCoord WorldCoords => worldCoord;

        public MapCoord DisplayCoords { get; } = MapCoord.UIFromWorldCoord(worldCoord);
        public MapCoord NormalizedCoords { get; } = MapCoord.NormalizedFromWorldCoord(worldCoord);

        public string DisplayCoordsText => $"{DisplayCoords.X:N0}, {DisplayCoords.Y:N0}";
        public string WorldCoordsText => $"{WorldCoords.X:N0}, {WorldCoords.Y:N0}, {WorldCoords.Z:N0}";

        public static MapCoordViewModel DesignerInstance { get; } = new MapCoordViewModel(new WorldCoord() { X = -343155, Y = 244585, Z = 0 });

        public static MapCoordViewModel FromCoord(WorldCoord coord) => coord == null ? null : new MapCoordViewModel(coord);
        public static MapCoordViewModel FromBase(BaseInstance inst) => FromCoord(inst?.Position);
    }
}
