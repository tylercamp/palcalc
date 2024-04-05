using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

// palsav.py
namespace PalSaveReader
{
    internal class CompressedSAV
    {
        private static void AssertZLibHeader(Stream stream)
        {
            var b1 = stream.ReadByte();
            var b2 = stream.ReadByte();

            if (b1 != 0x78 || b2 != 0x9C) throw new Exception();
        }

        static byte[] MAGIC_BYTES = Encoding.ASCII.GetBytes("PlZ");
        public static void WithDecompressedSave(string filePath, Action<Stream> action)
        {
            using (var fs = File.OpenRead(filePath))
            using (var binaryReader = new BinaryReader(fs))
            {
                // unused
                var uncompressedLength = binaryReader.ReadInt32();
                var compressedLen = binaryReader.ReadInt32();

                var magicBytes = binaryReader.ReadBytes(3);
                if (!MAGIC_BYTES.SequenceEqual(magicBytes))
                    throw new Exception("Magic bytes mismatch");

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
        }
    }
}
