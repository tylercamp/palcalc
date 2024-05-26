using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile;
using PalCalc.UI2;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2.Model
{
    internal abstract class ISaveGameInfo
    {
        public abstract DateTime SourceLastModified { get; }

        /// <summary>
        /// Whether or not this info has been outdated by a more recent `ISaveGameInfo`
        /// </summary>
        public abstract bool Expired { get; set; }

        private static readonly ILogger logger = Log.ForContext<LoadedSaveGameInfo>();
        private static readonly Dictionary<string, ISaveGameInfo> cachedInfos = [];
        public static ISaveGameInfo FromSave(ISaveGame save)
        {
            var id = save.Identifier();
            if (cachedInfos.TryGetValue(id, out ISaveGameInfo? existing))
            {
                if (existing.SourceLastModified < (save.LevelMeta?.LastModified ?? save.LastModified))
                {
                    var newResult = BuildFrom(save);
                    cachedInfos[id] = newResult;
                    existing.Expired = true;
                    return newResult;
                }
                else
                {
                    return existing;
                }
            }
            else
            {
                var newResult = BuildFrom(save);
                cachedInfos.Add(id, newResult);
                return newResult;
            }
        }

        private static ISaveGameInfo BuildFrom(ISaveGame save)
        {
            if (save.LevelMeta == null) return new MissingSaveGameInfo(save);

            try
            {
                return new LoadedSaveGameInfo(save.LevelMeta, save.LevelMeta.ReadGameOptions());
            }
            catch (Exception e)
            {
                logger.Warning(e, "unexpected error while loading game info from {path}", save.LevelMeta.FilePath);
                return new InvalidSaveGameInfo(save.LevelMeta);
            }
        }
    }

    internal class InvalidSaveGameInfo(LevelMetaSaveFile invalidFile) : ISaveGameInfo
    {
        public override DateTime SourceLastModified { get; } = invalidFile.LastModified;
        public override bool Expired { get; set; }
    }

    internal class MissingSaveGameInfo(ISaveGame sourceSave) : ISaveGameInfo
    {
        public override DateTime SourceLastModified { get; } = sourceSave.LastModified;
        public override bool Expired { get; set; }
    }

    internal class LoadedSaveGameInfo : ISaveGameInfo
    {
        public LoadedSaveGameInfo(LevelMetaSaveFile source, GameMeta info)
        {
            SourceLastModified = source.LastModified;

            IsServerSave = info.IsServerSave;
            WorldName = info.WorldName;
            PlayerName = info.PlayerName;
            InGameDay = info.InGameDay;
        }
        public override DateTime SourceLastModified { get; }

        public bool IsServerSave { get; }

        public string WorldName { get; }
        public string PlayerName { get; }
        public int? PlayerLevel { get; }
        public int InGameDay { get; }

        public override bool Expired { get; set; }
    }

    internal abstract class PlaceholderSaveGameInfo(string label) : ISaveGameInfo
    {
        public string Label { get; } = label;

        public override DateTime SourceLastModified { get; } = DateTime.MinValue;

        public override bool Expired { get; set; }
    }
}
