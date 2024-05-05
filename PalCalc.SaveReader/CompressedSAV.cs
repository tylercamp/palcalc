using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

// palsav.py
namespace PalCalc.SaveReader
{
    public static class CompressedSAV
    {
        private static ILogger logger = Log.ForContext(typeof(CompressedSAV));

        // don't know what this is, but I've seen this in newer palworld saves from Game Pass. seems
        // to clear itself up (back to normal format) after the game has been closed for a little while.
        // likely an indicator of unsynced save or something like that.
        static byte[] WRAPPER_MAGIC_BYTES = Encoding.ASCII.GetBytes("CNK");

        static byte[] MAGIC_BYTES = Encoding.ASCII.GetBytes("PlZ");
        public static void WithDecompressedSave(string filePath, Action<Stream> action)
        {
            logger.Information("Loading {file} as GVAS", filePath);
            using (var fs = File.OpenRead(filePath))
            using (var binaryReader = new BinaryReader(fs))
            {
                // unused
                var uncompressedLength = binaryReader.ReadInt32();
                var compressedLen = binaryReader.ReadInt32();

                var magicBytes = binaryReader.ReadBytes(3);
                if (WRAPPER_MAGIC_BYTES.SequenceEqual(magicBytes))
                {
                    // unknown content
                    binaryReader.ReadBytes(9);

                    magicBytes = binaryReader.ReadBytes(3);
                }

                if (!MAGIC_BYTES.SequenceEqual(magicBytes))
                {
                    throw new Exception("Magic bytes mismatch");
                }

                var saveType = binaryReader.ReadByte();

                switch (saveType)
                {
                    case 0x31: break;
                    case 0x32: break;
                    default: throw new Exception("Unrecognized compression type");
                }

                using (var decompressed = new InflaterInputStream(fs))
                {
                    if (saveType == 0x32)
                    {
                        using (var doubleDecompressed = new InflaterInputStream(decompressed))
                            action(doubleDecompressed);
                    }
                    else
                    {
                        action(decompressed);
                    }
                }
            }
            logger.Information("done");
        }

        public static bool IsValidSave(string filePath)
        {
            using (var fs = File.OpenRead(filePath))
            using (var binaryReader = new BinaryReader(fs))
            {
                // unused
                var uncompressedLength = binaryReader.ReadInt32();
                var compressedLen = binaryReader.ReadInt32();

                var magicBytes = binaryReader.ReadBytes(3);
                
                if (WRAPPER_MAGIC_BYTES.SequenceEqual(magicBytes))
                {
                    binaryReader.ReadBytes(9);

                    magicBytes = binaryReader.ReadBytes(3);
                }

                if (!MAGIC_BYTES.SequenceEqual(magicBytes))
                    return false;

                var saveType = binaryReader.ReadByte();

                switch (saveType)
                {
                    case 0x31: return true;
                    case 0x32: return true;
                    default: return false;
                }
            }
        }
    }
}
