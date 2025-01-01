using Newtonsoft.Json;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class PalBreedingDB(PalDB db)
    {
        private static ILogger logger = Log.ForContext<PalBreedingDB>();

        public List<BreedingResult> Breeding { get; set; }

        // Map[ParentPal, Map[ChildPal, NumSteps]]
        public Dictionary<Pal, Dictionary<Pal, int>> MinBreedingSteps { get; set; }

        // Map[Parent1, Map[Parent2, BreedingResult]]
        // 
        // there can be multiple breeding results depending on the genders of the parents (namely for Wixen and Kativa)
        private IReadOnlyDictionary<Pal, IReadOnlyDictionary<Pal, BreedingResult[]>> breedingByParent;
        public IReadOnlyDictionary<Pal, IReadOnlyDictionary<Pal, BreedingResult[]>> BreedingByParent =>
            breedingByParent ??= Breeding
                .SelectMany(breed => breed.Parents.Select(parent1 => (parent1.Pal, breed))) // List<(parent, breeding)>
                .GroupBy(p => p.Pal)
                .ToDictionary(
                    g => g.Key,
                    g => g.Distinct()
                        .GroupBy(p => p.breed.OtherParent(g.Key).Pal)
                        .ToDictionary(g2 => g2.Key, g2 => g2.Select(p => p.breed).ToArray())
                        .ToFrozenDictionary() as IReadOnlyDictionary<Pal, BreedingResult[]>
                );

        // Map[Child, Map[Parent1, List<Parent2>]]
        private Dictionary<Pal, Dictionary<GenderedPal, List<GenderedPal>>> breedingByChild;
        public Dictionary<Pal, Dictionary<GenderedPal, List<GenderedPal>>> BreedingByChild =>
            breedingByChild ??= Breeding
                .GroupBy(b => b.Child)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(b => b.Parents.Select(p1 => (p1, b))).GroupBy(p => p.p1).ToDictionary(
                        g => g.Key,
                        g => g.Select(p => p.b.OtherParent(g.Key)).Distinct().ToList()
                    )
                );

        private static object loadEmbeddedLock = new object();
        private static PalBreedingDB embedded = null;

        private static PalBreedingDB _LoadEmbedded(PalDB paldb)
        {
            logger.Information("Loading embedded breeding DB");
            var info = Assembly.GetExecutingAssembly().GetName();
            var name = info.Name;
            using var stream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream($"{name}.breeding.json")!;

            var sw = Stopwatch.StartNew();
            PalBreedingDB result;
            using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                result = FromJson(paldb, streamReader.ReadToEnd());

            logger.Information("Successfully loaded embedded breeding DB in {ms}ms", sw.ElapsedMilliseconds);
            return result;
        }

        public static void BeginLoadEmbedded(PalDB paldb)
        {
            Task.Run(() =>
            {
                lock (loadEmbeddedLock)
                {
                    embedded = _LoadEmbedded(paldb);
                }
            });
        }

        public static PalBreedingDB LoadEmbedded(PalDB paldb)
        {
            lock (loadEmbeddedLock)
            {
                if (embedded != null)
                {
                    logger.Verbose("Using previously-loaded pal DB");
                    return embedded;
                }

                embedded = _LoadEmbedded(paldb);
                return embedded;
            }
        }

        public static PalBreedingDB FromJson(PalDB paldb, string json) => JsonConvert.DeserializeObject<PalBreedingDB>(json, new PalBreedingDBJsonConverter(paldb));

        public string ToJson() => JsonConvert.SerializeObject(this, new PalBreedingDBJsonConverter(db));
    }
}
