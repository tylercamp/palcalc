using PalCalc.Model;
using PalCalc.SaveReader.FArchive;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile
{
    public class GameMeta
    {
        public string WorldName { get; set; }
        public int InGameDay { get; set; }

        public bool IsServerSave { get; set; }

        // (when `IsServerSave = false`)
        public string PlayerName { get; set; }
        public int? PlayerLevel { get; set; }

        public override string ToString() => IsServerSave
            ? $"{WorldName} day {InGameDay} (Server)"
            : $"{PlayerName} lv {PlayerLevel} in {WorldName} day {InGameDay}";
    }

    public class LevelMetaSaveFile : ISaveFile
    {
        private static ILogger logger = Log.ForContext<LevelMetaSaveFile>();

        public LevelMetaSaveFile(string folderPath) : base(folderPath) { }

        public override string FileName => "LevelMeta.sav";

        public GameMeta ReadGameOptions()
        {
            logger.Debug("parsing content");
            var valuesVisitor = new ValueCollectingVisitor(".SaveData", ".WorldName", ".HostPlayerName", ".HostPlayerLevel", ".InGameDay");
            VisitGvas(valuesVisitor);

            bool isServerSave = !valuesVisitor.Result.ContainsKey(".HostPlayerName");

            logger.Debug("done");
            return new GameMeta
            {
                WorldName = (string)valuesVisitor.Result[".WorldName"],
                InGameDay = (int)valuesVisitor.Result.GetValueOrElse(".InGameDay", 1),
                IsServerSave = isServerSave,
                PlayerName = isServerSave ? null : (string)valuesVisitor.Result[".HostPlayerName"],
                PlayerLevel = isServerSave ? null : (int?)valuesVisitor.Result[".HostPlayerLevel"],
            };
        }
    }
}
