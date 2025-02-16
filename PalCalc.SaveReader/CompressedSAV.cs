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

        private const int MaxRetryAttempts = 3;

        public static void WithDecompressedSave(string filePath, Action<Stream> action) =>
            WithDecompressedSave(new SingleFileSource(filePath), action);

        public static void WithDecompressedSave(IEnumerable<string> orderedFilePartPaths, Action<Stream> action) =>
            WithDecompressedSave(new MultiFileSource(orderedFilePartPaths), action);

        public static void WithDecompressedSave(IFileSource fileSource, Action<Stream> action)
        {
            MemoryStream combinedStream = null;
            CompressedSAVHeader firstFileHeader = null;

            // since the file can be moved or changed during the read op, auto-retry any failed attempts

            for (int i = 1; i <= MaxRetryAttempts; i++)
            {
                string lastFile = null;
                combinedStream = new();
                firstFileHeader = null;

                try
                {
                    foreach (var file in fileSource.Content)
                    {
                        lastFile = file;
                        using var fs = FileUtil.ReadFileNonLocking(file);

                        var header = CompressedSAVHeader.Read(fs);

                        if (firstFileHeader == null)
                        {
                            if (!header.HasCompressionMarker) throw new Exception("Magic bytes mismatch");
                            firstFileHeader = header;
                            fs.Seek(0, SeekOrigin.Begin);
                        }

                        fs.CopyTo(combinedStream);
                    }
                }
                catch (Exception e) when (i != MaxRetryAttempts)
                {
                    logger.Warning(e, "Error while reading {File}, retrying", lastFile);
                }
            }

            combinedStream.Seek(0, SeekOrigin.Begin);

            using (combinedStream)
            {
                WithDecompressedSave(combinedStream, action);
            }
        }

        public static void WithDecompressedSave(Stream inputStream, Action<Stream> action)
        {
            var header = CompressedSAVHeader.Read(inputStream);

            using (var decompressed = new InflaterInputStream(inputStream))
            {
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

        public static bool HasSaveCompression(Stream stream) => CompressedSAVHeader.Read(stream).HasCompressionMarker;

        public static bool HasSaveCompression(IFileSource fileSource)
        {
            for (int i = 1; i <= MaxRetryAttempts; i++)
            {
                string firstFile = null;
                try
                {
                    firstFile = fileSource.Content.First();
                    using (var fs = FileUtil.ReadFileNonLocking(firstFile))
                        return HasSaveCompression(fs);
                }
                catch (Exception e) when (i != MaxRetryAttempts)
                {
                    logger.Warning(e, "Error while reading {File}, retrying", firstFile);
                }
            }

            // (shouldn't happen)
            throw new NotImplementedException();
        }
    }
}
