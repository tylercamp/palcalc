using PalSaveReader.FArchive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// gvas.py
namespace PalSaveReader.GVAS
{
    struct GvasHeader
    {
        public int
            Magic,
            SaveGameVersion,
            PackageFileVersionUE4,
            PackageFileVersionUE5,
            EngineVersionMajor,
            EngineVersionMinor,
            EngineVersionPatch,
            CustomVersionFormat;

        public uint EngineVersionChangelist;

        public string EngineVersionBranch, SaveGameClassName;
        public List<(Guid, int)> CustomVersions;

        public static GvasHeader Read(FArchiveReader reader)
        {
            var result = new GvasHeader();

            result.Magic = reader.ReadInt32();
            if (result.Magic != 0x53415647) throw new Exception();

            result.SaveGameVersion = reader.ReadInt32();
            if (result.SaveGameVersion != 3) throw new Exception();

            result.PackageFileVersionUE4 = reader.ReadInt32();
            result.PackageFileVersionUE5 = reader.ReadInt32();

            result.EngineVersionMajor = reader.ReadUInt16();
            result.EngineVersionMinor = reader.ReadUInt16();
            result.EngineVersionPatch = reader.ReadUInt16();
            result.EngineVersionChangelist = reader.ReadUInt32();

            result.EngineVersionBranch = reader.ReadString();
            result.CustomVersionFormat = reader.ReadInt32();

            if (result.CustomVersionFormat != 3) throw new Exception();

            result.CustomVersions = reader.ReadArray(r => (r.ReadGuid(), r.ReadInt32())).ToList();
            result.SaveGameClassName = reader.ReadString();

            return result;
        }
    }

    internal class GvasFile
    {
        public GvasHeader Header { get; set; }
        public Dictionary<string, object> Properties { get; set; }

        public static GvasFile FromFArchive(FArchiveReader reader)
        {
            var result = new GvasFile();
            result.Header = GvasHeader.Read(reader);

            result.Properties = reader.ReadPropertiesUntilEnd();

            return result;
        }

        public static void VisitFromFArchive(FArchiveReader reader, Action<string, object> onValue)
        {
            GvasHeader.Read(reader);

        }
    }
}
