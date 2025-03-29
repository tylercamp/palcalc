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
                ContainerContents = [],
                Groups = [],
                Bases = [],
                MapObjects = [],
            };
        }

        public override LevelSaveData ReadCharacterData(PalDB db, GameSettings settings, List<PlayersSaveFile> playersFiles, GlobalPalStorageSaveFile gpsFile)
        {
            return new LevelSaveData()
            {
                Guilds = [],
                Pals = [],
                Players = [],
                Bases = [],
                PalContainers = [],
            };
        }
    }
}
