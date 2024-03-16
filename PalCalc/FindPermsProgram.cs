using PalCalc.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc
{
    internal class FindPermsProgram
    {

        static void Main(string[] args)
        {
            var db = PalDB.FromJson(File.ReadAllText("db.json"));
            Console.WriteLine("Loaded Pal DB");
            var instances = PalInstance.JsonToList(db, File.ReadAllText("savegame.json"));
            Console.WriteLine("Loaded save game");
        }
    }
}
