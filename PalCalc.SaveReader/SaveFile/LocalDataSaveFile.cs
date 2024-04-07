using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile
{
    public class LocalDataSaveFile : ISaveFile
    {
        public LocalDataSaveFile(string folderPath) : base(folderPath) { }

        public override string FileName => "LocalData.sav";
    }
}
