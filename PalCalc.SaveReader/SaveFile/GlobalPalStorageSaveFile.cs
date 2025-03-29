using PalCalc.Model;
using PalCalc.SaveReader.SaveFile.Support.Level;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.SaveFile
{
    public class GlobalPalStorageData
    {
        public string ContainerId { get; internal set; }
        public List<PalInstance> Pals { get; internal set; }
    }

    public class GlobalPalStorageSaveFile(IFileSource files) : ISaveFile(files)
    {
        // (atm this is just a copy/paste of PlayersDpsSaveFile - the same format is used for both)

        public virtual List<GvasCharacterInstance> ReadRawCharacters()
        {
            var v = new DimensionalPalStorage_CharacterInstanceVisitor();
            ParseGvas(v);
            return v.Result;
        }

        public virtual GlobalPalStorageData ReadPals(string containerId)
        {
            var db = PalDB.LoadEmbedded();
            var pals = ReadRawCharacters()
                .Select(c => c.ToPalInstance(db, LocationType.GlobalPalStorage))
                .ZipWithIndex()
                .Select(p =>
                {
                    var (c, i) = p;
                    if (c == null) return null;

                    c.Location.Index = i;
                    c.Location.ContainerId = containerId;
                    return c;
                })
                .SkipNull()
                .ToList();

            return new()
            {
                ContainerId = containerId,
                Pals = pals,
            };
        }
    }
}
