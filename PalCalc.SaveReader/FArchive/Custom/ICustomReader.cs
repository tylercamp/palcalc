﻿using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.FArchive.Custom
{
    // for custom-serialized properties which don't trigger the usual Begin/End events
    public interface ICustomProperty : IProperty { }

    // ".worldSaveData.BaseCampSaveData"
    // ".worldSaveData.MapObjectSaveData"
    public abstract class ICustomReader
    {
        public abstract string MatchedPath { get; }
        public abstract IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors);

        public static List<ICustomReader> All = new List<ICustomReader>()
        {
            new CharacterReader(),
            new CharacterContainerReader(),
            new GroupReader(),
            new BaseCampReader(),
            new WorkerDirectorReader(),
            new MapModelReader(),
        };
    }

    public abstract class ICustomByteArrayReader : ICustomReader
    {
        private static ILogger logger = Log.ForContext<ICustomByteArrayReader>();

        public override IProperty Decode(FArchiveReader reader, string typeName, ulong size, string path, IEnumerable<IVisitor> visitors)
        {
            logger.Verbose("decoding");
            var arrayProp = (ArrayProperty)reader.ReadProperty(typeName, size, path, path, visitors);
            using (var byteStream = new MemoryStream(arrayProp.ByteValues))
            using (var subReader = reader.Derived(byteStream))
            {
                var res = Decode(subReader, path, visitors);
                logger.Verbose("done");
                return res;
            }
        }

        protected abstract IProperty Decode(FArchiveReader subReader, string path, IEnumerable<IVisitor> visitors);
    }
}
