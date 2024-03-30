using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.model
{
    internal class PalDB
    {
        public Dictionary<PalId, Pal> PalsById { get; set; }
        public IEnumerable<Pal> Pals => PalsById.Values;



        public List<BreedingResult> Breeding { get; set; }

        // Map[Parent1, Map[Parent2, BreedingResult]]
        public Dictionary<Pal, Dictionary<Pal, BreedingResult>> BreedingByParent { get; set; }

        // Map[Child, Map[Parent1, List<Parent2>]]
        public Dictionary<Pal, Dictionary<Pal, List<Pal>>> BreedingByChild { get; set; }



        public Dictionary<Pal, Dictionary<PalGender, float>> BreedingGenderProbability { get; set; }



        public List<Trait> Traits { get; set; }
        public Dictionary<string, Trait> TraitsByName { get; set; }



        private class Serialized
        {
            public List<Pal> Pals { get; set; }
            public List<BreedingResult.Serialized> Breeding { get; set; }
            public List<Trait> Traits { get; set; }
            public Dictionary<String, Dictionary<PalGender, float>> BreedingGenderProbability { get; set; }
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

            return result;
        }

        public string ToJson() => JsonConvert.SerializeObject(new Serialized
        {
            Pals = Pals.ToList(),
            Breeding = Breeding.Select(b => new BreedingResult.Serialized { Parent1ID = b.Parent1.Id, Parent2ID = b.Parent2.Id, Child1ID = b.Child.Id }).ToList(),
            Traits = Traits,
            BreedingGenderProbability = BreedingGenderProbability.ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value),
        });
    }
}
