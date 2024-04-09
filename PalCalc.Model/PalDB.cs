using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public Dictionary<PalId, Pal> PalsById { get; set; }

        // Map[ParentPal, Map[ChildPal, NumSteps]]
        public Dictionary<Pal, Dictionary<Pal, int>> MinBreedingSteps { get; set; }

        public Dictionary<Pal, Dictionary<PalGender, float>> BreedingGenderProbability { get; set; }

        public List<Trait> Traits { get; set; }

        public List<BreedingResult> Breeding { get; set; }



        public IEnumerable<Pal> Pals => PalsById.Values;

        // Map[Parent1, Map[Parent2, BreedingResult]]
        private Dictionary<Pal, Dictionary<Pal, BreedingResult>> breedingByParent;
        public Dictionary<Pal, Dictionary<Pal, BreedingResult>> BreedingByParent
        {
            get
            {
                if (breedingByParent == null)
                {
                    breedingByParent = Breeding
                        .SelectMany(breed => breed.Parents.Select(parent1 => (parent1, breed))) // List<(parent, breeding)>
                        .GroupBy(p => p.parent1)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Distinct().ToDictionary(p => p.breed.OtherParent(g.Key), p => p.breed)
                        );
                }
                return breedingByParent;
            }
        }

        // Map[Child, Map[Parent1, List<Parent2>]]
        private Dictionary<Pal, Dictionary<Pal, List<Pal>>> breedingByChild;
        public Dictionary<Pal, Dictionary<Pal, List<Pal>>> BreedingByChild
        {
            get
            {
                if (breedingByChild == null)
                {
                    breedingByChild =
                        Breeding
                        .GroupBy(b => b.Child)
                        .ToDictionary(
                            g => g.Key,
                            g => g.SelectMany(b => b.Parents.Select(p1 => (p1, b))).GroupBy(p => p.p1).ToDictionary(
                                g => g.Key,
                                g => g.Select(p => p.b.OtherParent(g.Key)).Distinct().ToList()
                            )
                        );
                }
                return breedingByChild;
            }
        }

        private Dictionary<Pal, PalGender> breedingMostLikelyGender;
        public Dictionary<Pal, PalGender> BreedingMostLikelyGender
        {
            get
            {
                if (breedingMostLikelyGender == null)
                {
                    breedingMostLikelyGender = Pals.ToDictionary(
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
                }

                return breedingMostLikelyGender;
            }
        }


        private Dictionary<Pal, PalGender> breedingLeastLikelyGender;
        public Dictionary<Pal, PalGender> BreedingLeastLikelyGender
        {
            get
            {
                if (breedingLeastLikelyGender == null)
                {
                    breedingLeastLikelyGender = BreedingMostLikelyGender.ToDictionary(
                        kvp => kvp.Key,
                        kvp =>
                        {
                            if (kvp.Value == PalGender.WILDCARD) return PalGender.WILDCARD;
                            else if (kvp.Value == PalGender.MALE) return PalGender.FEMALE;
                            else return PalGender.MALE;
                        }
                    );
                }

                return breedingLeastLikelyGender;
            }
        }


        private Dictionary<string, Trait> traitsByName;
        public Dictionary<string, Trait> TraitsByName
        {
            get
            {
                if (traitsByName == null)
                {
                    traitsByName = Traits.GroupBy(t => t.Name).ToDictionary(t => t.Key, t => t.First());
                }

                return traitsByName;
            }
        }



        private class Serialized
        {
            public List<Pal> Pals { get; set; }
            public List<BreedingResult.Serialized> Breeding { get; set; }
            public List<Trait> Traits { get; set; }
            public Dictionary<string, Dictionary<PalGender, float>> BreedingGenderProbability { get; set; }
            public Dictionary<string, Dictionary<string, int>> MinBreedingSteps { get; set; }
        }

        public static PalDB LoadEmbedded()
        {
            var info = Assembly.GetExecutingAssembly().GetName();
            var name = info.Name;
            using var stream = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream($"{name}.db.json")!;

            using var streamReader = new StreamReader(stream, Encoding.UTF8);
                return FromJson(streamReader.ReadToEnd());
        }

        public static PalDB FromJson(string json) => JsonConvert.DeserializeObject<PalDB>(json);

        public string ToJson() => JsonConvert.SerializeObject(this);

        // should only be used when constructing a DB for serialization
        public static PalDB MakeEmptyUnsafe() => new PalDB();
    }
}
