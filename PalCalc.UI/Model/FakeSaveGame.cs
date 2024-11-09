using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile.Virtual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model
{
    internal static class FakeSaveGame
    {
        public static ISaveGame Create(string name) =>
            new VirtualSaveGame(
                "Fake User",
                name,
                new VirtualLevelSaveFile(),
                new VirtualLevelMetaSaveFile(name),
                new VirtualLocalDataSaveFile(),
                new VirtualWorldOptionSaveFile()
            );
    }
}
