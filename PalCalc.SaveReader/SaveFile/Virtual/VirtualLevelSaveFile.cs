using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile.Virtual
{
    public class VirtualLevelSaveFile : LevelSaveFile
    {
        public VirtualLevelSaveFile() : base(null)
        {
        }

        public override RawLevelSaveData ReadRawCharacterData()
        {
            return new RawLevelSaveData()
            {
                Characters = [],
                Containers = [],
                Groups = []
            };
        }

        public override LevelSaveData ReadCharacterData(PalDB db, List<PlayersSaveFile> playersFiles)
        {
            return new LevelSaveData()
            {
                Guilds = [],
                Pals = [],
                Players = []
            };
        }
    }
}
