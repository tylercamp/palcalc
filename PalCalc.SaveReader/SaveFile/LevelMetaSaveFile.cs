using PalCalc.SaveReader.FArchive;

namespace PalCalc.SaveReader.SaveFile
{
    public class GameMeta
    {
        public string WorldName { get; set; }
        public string PlayerName { get; set; }
        public int PlayerLevel { get; set; }

        public override string ToString() => $"{PlayerName} lv {PlayerLevel} in {WorldName}";
    }

    public class LevelMetaSaveFile : ISaveFile
    {
        public LevelMetaSaveFile(string folderPath) : base(folderPath) { }

        public override string FileName => "LevelMeta.sav";

        public GameMeta ReadGameOptions()
        {
            var valuesVisitor = new ValueCollectingVisitor(".SaveData", ".WorldName", ".HostPlayerName", ".HostPlayerLevel");
            VisitGvas(valuesVisitor);
            try
            {
                return new GameMeta{
                    WorldName = (string)valuesVisitor.Result[".WorldName"],
                    PlayerName = (string)valuesVisitor.Result[".HostPlayerName"],
                    PlayerLevel = (int)valuesVisitor.Result[".HostPlayerLevel"]
                };
            }
            catch (Exception)
            {
                return new GameMeta
                {
                    WorldName = "Multiplayer Game",
                    PlayerName = "All Players",
                    PlayerLevel = 50
                };
            }


        }
    }
}
