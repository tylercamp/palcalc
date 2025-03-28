﻿using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.Mapped;
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
        public static SaveInspectorWindowViewModel DesignerInstance => designerInstance ??= new SaveInspectorWindowViewModel(SaveGameViewModel.DesignerInstance, GameSettings.Defaults);

        public SaveGameViewModel DisplayedSave { get; }

        public SearchViewModel Search { get; }
        public SaveDetailsViewModel Details { get; }

        public ILocalizedText WindowTitle { get; }

        public SaveInspectorWindowViewModel(SaveGameViewModel sgvm, GameSettings settings)
        {
            DisplayedSave = sgvm;

            var rawData = sgvm.CachedValue.UnderlyingSave.Level.ReadRawCharacterData();
            var players = sgvm.Value.Players.Select(p => p.ReadPlayerContent()).SkipNull().ToList();

            Search = new SearchViewModel(sgvm, settings);
            Details = new SaveDetailsViewModel(sgvm.CachedValue, rawData, players);

            WindowTitle = LocalizationCodes.LC_SAVEWINDOW_TITLE.Bind(sgvm.Label);
        }
    }
}
