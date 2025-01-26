using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

// palsav.py
namespace PalCalc.SaveReader
{
    public class CompressedSAVHeader
    {
        public bool HasMagicBytes_Wrapper { get; set; }
        public bool HasMagicBytes_Main { get; set; }

        public int UncompressedLength { get; set; }
        public int CompressedLength { get; set; }

        public byte CompressionTypeId { get; set; }

        // don't know what this is, but I've seen this in newer palworld saves from Game Pass. seems
        // to clear itself up (back to normal format) after the game has been closed for a little while.
        // likely an indicator of unsynced save or something like that.
        static byte[] WRAPPER_MAGIC_BYTES = Encoding.ASCII.GetBytes("CNK");

        static byte[] MAGIC_BYTES = Encoding.ASCII.GetBytes("PlZ");

        public static CompressedSAVHeader Read(Stream stream)
        {
            using (var binaryReader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                var res = new CompressedSAVHeader();

                // TODO - seems incorrect when `WRAPPER_MAGIC_BYTES` matches, though this 8-byte offset is needed with/without that match
                res.UncompressedLength = binaryReader.ReadInt32();
                res.CompressedLength = binaryReader.ReadInt32();

                var magicBytes = binaryReader.ReadBytes(3);
                if (WRAPPER_MAGIC_BYTES.SequenceEqual(magicBytes))
                {
                    res.HasMagicBytes_Wrapper = true;

                    // unknown content
                    binaryReader.ReadBytes(9);

                    magicBytes = binaryReader.ReadBytes(3);
                }

                res.HasMagicBytes_Main = MAGIC_BYTES.SequenceEqual(magicBytes);
                res.CompressionTypeId = binaryReader.ReadByte();

                return res;
            }
        }
    }

    public static class CompressedSAV
    {
        private static ILogger logger = Log.ForContext(typeof(CompressedSAV));

        // file operations can take a while, and they can happen while Palworld is running + saving file contents.
        // save files are relatively small (typically ~10MB at most), prefer loading the whole file into memory up
        // front so we can close the handle ASAP.
        private static Stream ReadFileNonLocking(string filePath)
        {
            // since the file can be moved or changed during the read op, auto-retry any failed attempts

            byte[] fileBytes;

            const int maxAttempts = 3;
            for (int i = 1; i <= maxAttempts; i++)
            {
                try
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var ms = new MemoryStream())
                    {
                        fs.CopyTo(ms);
                        fileBytes = ms.ToArray();
                    }

                    return new MemoryStream(fileBytes);
                }
                catch (Exception e) when (i != maxAttempts)
                {
                    logger.Warning(e, "Error while reading {File}, retrying", filePath);
                }
            }

            // (shouldn't happen, we should have returned a stream or thrown an exception by now)
            throw new NotImplementedException();
        }

        public static void WithDecompressedSave(string filePath, Action<Stream> action)
        {
            logger.Information("Loading {file} as GVAS", filePath);
            
            using (var fs = ReadFileNonLocking(filePath))
            using (var binaryReader = new BinaryReader(fs))
            {
                var header = CompressedSAVHeader.Read(fs);

                using (var decompressed = new InflaterInputStream(fs))
                {
                    if (!header.HasMagicBytes_Main) throw new Exception("Magic bytes mismatch");

                    switch (header.CompressionTypeId)
                    {
                        case 0x31: break;
                        case 0x32: break;
                        default: throw new Exception("Unrecognized compression type");
                    }

                    if (header.CompressionTypeId == 0x32)
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

        public static void WithDecompressedAggregateSave(IEnumerable<string> filePaths, Action<Stream> action)
        {
            var combinedStream = new MemoryStream();

            CompressedSAVHeader firstFileHeader = null;
            foreach (var file in filePaths)
            {
                using var fs = ReadFileNonLocking(file);

                if (firstFileHeader == null)
                {
                    var header = CompressedSAVHeader.Read(fs);

                    if (!header.HasMagicBytes_Main) throw new Exception("Magic bytes mismatch");
                    firstFileHeader = header;
                }
                else
                {
                    fs.Seek(12, SeekOrigin.Begin);
                }

                fs.CopyTo(combinedStream);
            }

            using (var readStream = new MemoryStream(combinedStream.ToArray()))
            using (var decompressed = new InflaterInputStream(readStream))
            {
                if (firstFileHeader.CompressionTypeId == 0x32)
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

        public static bool IsValidSave(string filePath)
        {
            using (var fs = ReadFileNonLocking(filePath))
            using (var binaryReader = new BinaryReader(fs))
            {
                var header = CompressedSAVHeader.Read(fs);
                if (!header.HasMagicBytes_Main) return false;

                switch (header.CompressionTypeId)
                {
                    case 0x31: return true;
                    case 0x32: return true;
                    default: return false;
                }
            }
        }
    }
}
