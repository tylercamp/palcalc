
using PalSaveReader;
using PalSaveReader.FArchive;
using PalSaveReader.GVAS;

CompressedSAV.WithDecompressedSave(@"C:\Users\algor\AppData\Local\Pal\Saved\SaveGames\76561198963790804\F0CEFEA04271692EB202A78E57A3A40D\Level.sav", stream =>
{
    using (var archiveReader = new FArchiveReader(stream, PalWorldTypeHints.Hints))
    {
        var gvas = GvasFile.FromFArchive(archiveReader);
    }
});