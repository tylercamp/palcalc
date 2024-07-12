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
        public static SaveInspectorWindowViewModel DesignerInstance
        {
            get
            {
                if (designerInstance == null)
                {
                    var save = DirectSavesLocation.AllLocal
                        .SelectMany(l => l.ValidSaveGames)
                        .OrderByDescending(g => g.LastModified)
                        .Select(g => CachedSaveGame.FromSaveGame(g, PalDB.LoadEmbedded()))
                        .First();

                    designerInstance = new SaveInspectorWindowViewModel(save);
                }
                return designerInstance;
            }
        }

        public SaveDetailsViewModel Details { get; }

        public SaveInspectorWindowViewModel(CachedSaveGame csg)
        {
            var rawData = csg.UnderlyingSave.Level.ReadRawCharacterData();
            var players = csg.UnderlyingSave.Players.Select(p => p.ReadPlayerContent()).ToList();

            Details = new SaveDetailsViewModel(csg, rawData, players);
        }
    }
}
