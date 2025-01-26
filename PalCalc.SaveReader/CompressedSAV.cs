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
    public enum TypeMarker
    {
        PLZ1        = 0b0000_0001 | TYPE_PLZ,
        PLZ2        = 0b0000_0010 | TYPE_PLZ,
        PLZ_Unknown = 0b0000_0000 | TYPE_PLZ,
        TYPE_PLZ    = 0b1000_0000,

        CNK0        = 0b0000_0001 | TYPE_CNK,
        CNK_Unknown = 0b0000_0000 | TYPE_CNK,
        TYPE_CNK    = 0b0100_0000,
    }

    public class CompressedSAVHeader
    {
        public bool HasGamePassMarker { get; set; }
        public bool HasCompressionMarker { get; set; }

        public int UncompressedLength { get; set; }
        public int CompressedLength { get; set; }

        public bool DoubleCompressed { get; set; }

        private static TypeMarker? ParseTypeMarker(byte[] data) => Encoding.ASCII.GetString(data) switch
        {
            "PlZ1" => TypeMarker.PLZ1,
            "PlZ2" => TypeMarker.PLZ2,
            var s when s.StartsWith("PlZ") => TypeMarker.PLZ_Unknown,

            // don't know what this is, but I've seen this in newer palworld saves from Game Pass. seems
            // to clear itself up (back to normal format) after the game has been closed for a little while.
            // likely an indicator of unsynced save or something like that.
            "CNK0" => TypeMarker.CNK0,
            var s when s.StartsWith("CNK") => TypeMarker.CNK_Unknown,

            _ => null
        };

        public static CompressedSAVHeader Read(Stream stream)
        {
            using (var binaryReader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                var res = new CompressedSAVHeader();

                // TODO - seems incorrect when `HasGamePassMarker` matches, though this 8-byte offset is needed with/without that match
                res.UncompressedLength = binaryReader.ReadInt32();
                res.CompressedLength = binaryReader.ReadInt32();

                var compressionFormat = ParseTypeMarker(binaryReader.ReadBytes(4));

                if (compressionFormat != null && compressionFormat.Value.HasFlag(TypeMarker.TYPE_CNK))
                {
                    res.HasGamePassMarker = true;

                    // unknown content
                    binaryReader.ReadBytes(8);
                    compressionFormat = ParseTypeMarker(binaryReader.ReadBytes(4));

                    if (compressionFormat == null)
                    {
                        // XGP saves can be split across multiple files. Partial save files don't have the PlZ type marker,
                        // and the data starts immediately after CNK0.
                        //
                        // reverse back to the start of the intended data block
                        stream.Seek(-12, SeekOrigin.Current);
                    }
                }

                res.HasCompressionMarker = compressionFormat != null;
                res.DoubleCompressed = compressionFormat == TypeMarker.PLZ2;

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
                    if (!header.HasCompressionMarker)
                        throw new Exception("Magic bytes mismatch");

                    if (header.DoubleCompressed)
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

                var header = CompressedSAVHeader.Read(fs);

                if (firstFileHeader == null)
                {
                    if (!header.HasCompressionMarker) throw new Exception("Magic bytes mismatch");
                    firstFileHeader = header;
                }

                fs.CopyTo(combinedStream);
            }

            using (var readStream = new MemoryStream(combinedStream.ToArray()))
            using (var decompressed = new InflaterInputStream(readStream))
            {
                if (firstFileHeader.DoubleCompressed)
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
                return CompressedSAVHeader.Read(fs).HasCompressionMarker;
            }
        }
    }
}
