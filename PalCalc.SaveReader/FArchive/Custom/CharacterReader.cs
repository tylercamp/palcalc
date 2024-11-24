using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.FArchive.Custom
{
    public class CharacterDataPropertyMeta : BasicPropertyMeta
    {
        public Guid? GroupId => Id;
    }

    public class CharacterDataProperty : IProperty
    {
        public IPropertyMeta Meta { get; set; }

        public Dictionary<string, object> Data { get; set; }

        public void Traverse(Action<IProperty> action)
        {
            foreach (var value in Data.Values)
            {
                var prop = value as IProperty;
                if (prop != null)
                {
                    action(prop);
                    prop.Traverse(action);
                }
            }
        }
    }

    public class CharacterReader : ICustomByteArrayReader
    {
        private static ILogger logger = Log.ForContext<CharacterReader>();

        public override string MatchedPath => ".worldSaveData.CharacterSaveParameterMap.Value.RawData";

        protected override IProperty Decode(FArchiveReader subReader, string path, IEnumerable<IVisitor> visitors)
        {
            logger.Verbose("decoding");

            var pathVisitors = visitors.Where(v => v.Matches(path));
            var extraVisitors = pathVisitors.SelectMany(v => v.VisitCharacterPropertyBegin(path)).ToList();

            var newVisitors = visitors.Concat(extraVisitors);

            var data = subReader.ReadPropertiesUntilEnd(path, visitors);
            subReader.ReadBytes(4); // unknown data?

            var meta = new CharacterDataPropertyMeta { Path = path, Id = subReader.ReadGuid() };

            foreach (var v in extraVisitors) v.Exit();
            foreach (var v in pathVisitors) v.VisitCharacterPropertyEnd(path, meta);

            logger.Verbose("done");
            return new CharacterDataProperty
            {
                Meta = meta,
                Data = data,
            };
        }
    }
}
