using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Virtual
{
    public class VirtualLevelMetaSaveFile(string worldName) : LevelMetaSaveFile(null)
    {
        public override GameMeta ReadGameOptions()
        {
            return new GameMeta()
            {
                InGameDay = 0,
                IsServerSave = true,
                WorldName = worldName
            };
        }
    }
}
