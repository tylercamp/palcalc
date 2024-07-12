using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Inspector
{
    public class SaveInspectorWindowViewModel
    {
        private static SaveInspectorWindowViewModel designerInstance = null;
        public static SaveInspectorWindowViewModel DesignerInstance => designerInstance ??= new SaveInspectorWindowViewModel(CachedSaveGame.SampleForDesignerView);

        public SearchViewModel Search { get; }
        public SaveDetailsViewModel Details { get; }

        public SaveInspectorWindowViewModel(CachedSaveGame csg)
        {
            var rawData = csg.UnderlyingSave.Level.ReadRawCharacterData();
            var players = csg.UnderlyingSave.Players.Select(p => p.ReadPlayerContent()).ToList();

            Search = new SearchViewModel(csg);
            Details = new SaveDetailsViewModel(csg, rawData, players);
        }
    }
}
