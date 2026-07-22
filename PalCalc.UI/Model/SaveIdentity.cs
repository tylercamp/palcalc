using PalCalc.SaveReader;

namespace PalCalc.UI.Model
{
    internal readonly record struct SaveIdentity(string UserId, string GameId)
    {
        // TODO - Replace this quick identity with a canonical save/session identity during the save-services refactor.
        public static SaveIdentity From(ISaveGame save) => new(save.UserId, save.GameId);
    }
}
