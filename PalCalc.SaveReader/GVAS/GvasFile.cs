using PalCalc.SaveReader.FArchive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// gvas.py
namespace PalCalc.SaveReader.GVAS
{
    public struct GvasHeader
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

    public class GvasFile
    {
        public GvasHeader Header { get; set; }
        public Dictionary<string, object> Properties { get; set; }

        public List<object> Collect(params string[] paths)
        {
            var result = new List<object>();

            foreach (var value in Properties.Values)
            {
                var prop = value as IProperty;
                if (prop != null)
                {
                    if (paths.Contains(prop.Meta.Path))
                        result.Add(prop);

                    prop.Traverse(subProp =>
                    {
                        if (paths.Contains(subProp.Meta.Path))
                            result.Add(subProp);
                    });
                }
            }

            return result;
        }

        public static GvasFile FromFArchive(FArchiveReader reader, IEnumerable<IVisitor> visitors)
        {
            var result = new GvasFile();
            result.Header = GvasHeader.Read(reader);

            result.Properties = reader.ReadPropertiesUntilEnd("", visitors);

            return result;
        }

        public static bool IsValidGvas(string file)
        {
            if (CompressedSAV.IsValidSave(file))
            {
                var isValid = false;
                CompressedSAV.WithDecompressedSave(file, stream =>
                {
                    using (var reader = new FArchiveReader(stream, PalWorldTypeHints.Hints))
                        isValid = IsValidGvas(reader);
                });
                return isValid;
            }
            else
            {
                using (var stream = File.OpenRead(file))
                {
                    using (var reader = new FArchiveReader(stream, PalWorldTypeHints.Hints))
                        return IsValidGvas(reader);
                }
            }
        }

        public static bool IsValidGvas(FArchiveReader reader)
        {
            var magic = reader.ReadInt32();
            if (magic != 0x53415647) return false;

            var gameVersion = reader.ReadInt32();
            if (gameVersion != 3) return false;

            reader.ReadInt32();
            reader.ReadInt32();

            reader.ReadUInt16();
            reader.ReadUInt16();
            reader.ReadUInt16();
            reader.ReadUInt32();

            reader.ReadString();
            
            var customVersionFormat = reader.ReadInt32();

            if (customVersionFormat != 3) return false;

            return true;
        }
    }
}
