using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class PalDB
    {
        public Dictionary<PalId, Pal> PalsById { get; set; }
        public IEnumerable<Pal> Pals => PalsById.Values;



        public List<BreedingResult> Breeding { get; set; }

        // Map[Parent1, Map[Parent2, BreedingResult]]
        public Dictionary<Pal, Dictionary<Pal, BreedingResult>> BreedingByParent { get; set; }

        // Map[Child, Map[Parent1, List<Parent2>]]
        public Dictionary<Pal, Dictionary<Pal, List<Pal>>> BreedingByChild { get; set; }



        // Map[ParentPal, Map[ChildPal, NumSteps]]
        public Dictionary<Pal, Dictionary<Pal, int>> MinBreedingSteps { get; set; }



        public Dictionary<Pal, Dictionary<PalGender, float>> BreedingGenderProbability { get; set; }
        public Dictionary<Pal, PalGender> BreedingMostLikelyGender { get; set; }
        public Dictionary<Pal, PalGender> BreedingLeastLikelyGender { get; set; }



        public List<Trait> Traits { get; set; }
        public Dictionary<string, Trait> TraitsByName { get; set; }



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

        public static PalDB FromJson(string json)
        {
            var deserialized = JsonConvert.DeserializeObject<Serialized>(json);

            var result = new PalDB();
            result.PalsById = deserialized.Pals.ToDictionary(p => p.Id);
            result.Traits = deserialized.Traits;
            result.BreedingGenderProbability = deserialized.BreedingGenderProbability.ToDictionary(
                kvp => deserialized.Pals.Single(p => p.Name == kvp.Key),
                kvp => kvp.Value
            );
            result.MinBreedingSteps = deserialized.MinBreedingSteps.ToDictionary(
                kvp => kvp.Key.ToPal(result),
                kvp => kvp.Value.ToDictionary(
                    ikvp => ikvp.Key.ToPal(result),
                    ikvp => ikvp.Value
                )
            );

            result.TraitsByName = result.Traits.GroupBy(t => t.Name).ToDictionary(t => t.Key, t => t.First());

            result.Breeding = deserialized.Breeding.Select(s => new BreedingResult
            {
                Parent1 = result.PalsById[s.Parent1ID],
                Parent2 = result.PalsById[s.Parent2ID],
                Child = result.PalsById[s.Child1ID]
            }).ToList();

            result.BreedingByParent = result.Breeding
                .SelectMany(breed => breed.Parents.Select(parent1 => (parent1, breed))) // List<(parent, breeding)>
                .GroupBy(p => p.parent1)
                .ToDictionary(
                    g => g.Key,
                    g => g.Distinct().ToDictionary(p => p.breed.OtherParent(g.Key), p => p.breed)
                );

            result.BreedingByChild = result.Breeding
                .GroupBy(b => b.Child)
                .ToDictionary(
                    g => g.Key,
                    g => g.SelectMany(b => b.Parents.Select(p1 => (p1, b))).GroupBy(p => p.p1).ToDictionary(
                        g => g.Key,
                        g => g.Select(p => p.b.OtherParent(g.Key)).Distinct().ToList()
                    )
                );

            result.BreedingMostLikelyGender = deserialized.Pals.ToDictionary(
                p => p,
                p =>
                {
                    var genderProbability = result.BreedingGenderProbability[p];
                    var maleProbability = genderProbability[PalGender.MALE];
                    var femaleProbability = genderProbability[PalGender.FEMALE];

                    if (maleProbability > femaleProbability) return PalGender.MALE;
                    else if (femaleProbability > maleProbability) return PalGender.FEMALE;
                    else return PalGender.WILDCARD;
                }
            );

            result.BreedingLeastLikelyGender = result.BreedingMostLikelyGender.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    if (kvp.Value == PalGender.WILDCARD) return PalGender.WILDCARD;
                    else if (kvp.Value == PalGender.MALE) return PalGender.FEMALE;
                    else return PalGender.MALE;
                }
            );

            return result;
        }

        public string ToJson() => JsonConvert.SerializeObject(new Serialized
        {
            Pals = Pals.ToList(),
            Breeding = Breeding.Select(b => new BreedingResult.Serialized { Parent1ID = b.Parent1.Id, Parent2ID = b.Parent2.Id, Child1ID = b.Child.Id }).ToList(),
            Traits = Traits,
            BreedingGenderProbability = BreedingGenderProbability.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value),
            MinBreedingSteps = MinBreedingSteps.ToDictionary(
                kvp => kvp.Key.Name,
                kvp => kvp.Value.ToDictionary(
                    ikvp => ikvp.Key.Name,
                    ikvp => ikvp.Value
                )
            )
        });
    }
}
