using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile
{
    public class WorldOptionSaveFile : ISaveFile
    {
        public WorldOptionSaveFile(string folderPath) : base(folderPath) { }

        public override string FileName => "WorldOption.sav";
    }
}
