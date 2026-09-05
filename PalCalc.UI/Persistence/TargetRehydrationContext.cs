using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.ViewModel.SaveSelection;
using GameSettingsModel = PalCalc.Model.GameSettings;

namespace PalCalc.UI.Persistence
{

    internal sealed record TargetRehydrationContext(
        PalDB Database,
        SaveGameViewModel Save,
        CachedSaveGame CachedSave,
        GameSettingsModel CurrentGameSettings,
        SerializableSolverSettings CurrentSolverSettings
    );

}