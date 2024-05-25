using PalCalc.SaveReader.SaveFile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2.Model
{
    internal class SaveGameInfo
    {
        public SaveGameInfo(GameMeta info)
        {
            this.info = info;
        }
        private GameMeta info;

        public string Label => info.ToString();
    }
}
