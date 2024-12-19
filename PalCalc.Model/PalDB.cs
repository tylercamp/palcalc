using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    [JsonConverter(typeof(PalDBJsonConverter))]
    public class PalDB
    {
        internal PalDB() { }

        public string Version { get; set; }

        public Dictionary<PalId, Pal> PalsById { get; set; }

        // Map[ParentPal, Map[ChildPal, NumSteps]]
        public Dictionary<Pal, Dictionary<Pal, int>> MinBreedingSteps { get; set; }

        public Dictionary<Pal, Dictionary<PalGender, float>> BreedingGenderProbability { get; set; }

        public List<PassiveSkill> PassiveSkills { get; set; }

        public List<BreedingResult> Breeding { get; set; }

        public List<PalElement> Elements { get; set; }
        public List<ActiveSkill> ActiveSkills { get; set; }



        public IEnumerable<Pal> Pals => PalsById.Values;

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

        private Dictionary<Pal, PalGender> breedingMostLikelyGender;
        public Dictionary<Pal, PalGender> BreedingMostLikelyGender =>
            breedingMostLikelyGender ??= Pals.ToDictionary(
                p => p,
                p =>
                {
                    var genderProbability = BreedingGenderProbability[p];
                    var maleProbability = genderProbability[PalGender.MALE];
                    var femaleProbability = genderProbability[PalGender.FEMALE];

                    if (maleProbability > femaleProbability) return PalGender.MALE;
                    else if (femaleProbability > maleProbability) return PalGender.FEMALE;
                    else return PalGender.WILDCARD;
                }
            );


        private Dictionary<Pal, PalGender> breedingLeastLikelyGender;
        public Dictionary<Pal, PalGender> BreedingLeastLikelyGender =>
            breedingLeastLikelyGender ??= BreedingMostLikelyGender.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    if (kvp.Value == PalGender.WILDCARD) return PalGender.WILDCARD;
                    else if (kvp.Value == PalGender.MALE) return PalGender.FEMALE;
                    else return PalGender.MALE;
                }
            );


        private Dictionary<string, PassiveSkill> passiveSkillsByName;
        public Dictionary<string, PassiveSkill> PassiveSkillsByName =>
            passiveSkillsByName ??= PassiveSkills.GroupBy(t => t.Name).ToDictionary(t => t.Key, t => t.First());

        private static ILogger logger = Log.ForContext<PalDB>();

        private static object loadEmbeddedLock = new object();
        private static PalDB embedded = null;

        private static PalDB _LoadEmbedded()
        {
            logger.Information("Loading embedded pal DB");
            var info = Assembly.GetExecutingAssembly().GetName();
            var name = info.Name;
            using var stream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream($"{name}.db.json")!;

            PalDB result;
            using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                result = FromJson(streamReader.ReadToEnd());

            logger.Information("Successfully loaded embedded pal DB");
            return result;
        }

        public static void BeginLoadEmbedded()
        {
            Task.Run(() =>
            {
                lock (loadEmbeddedLock)
                {
                    embedded = _LoadEmbedded();
                }
            });
        }

        public static PalDB LoadEmbedded()
        {
            lock (loadEmbeddedLock)
            {
                if (embedded != null)
                {
                    logger.Verbose("Using previously-loaded pal DB");
                    return embedded;
                }

                embedded = _LoadEmbedded();
                return embedded;
            }
        }

        public static PalDB FromJson(string json) => JsonConvert.DeserializeObject<PalDB>(json);

        public string ToJson() => JsonConvert.SerializeObject(this);

        // should only be used when constructing a DB for serialization
        public static PalDB MakeEmptyUnsafe(string version) => new PalDB() { Version = version };
    }
}
