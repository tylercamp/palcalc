namespace PalCalc.Model.Tests
{
    [TestClass]
    public sealed class PalBreedingDBTests : PalTestBase
    {
        [TestMethod]
        public void BreedingByParent_ResultsAreCommutative()
        {
            var pals = paldb.Pals.ToList();

            for (int ip1 = 0; ip1 < pals.Count; ip1++)
            {
                for (int ip2 = ip1; ip2 < pals.Count; ip2++)
                {
                    var p1 = pals[ip1];
                    var p2 = pals[ip2];

                    var childOrder1 = breedingdb.BreedingByParent[p1][p2];
                    var childOrder2 = breedingdb.BreedingByParent[p2][p1];

                    CollectionAssert.AreEquivalent(childOrder1, childOrder2);
                }
            }
        }

        private void TestSpecificBreedingResult(string parent1Name, string parent2Name, string childName)
        {
            var parent1 = parent1Name.ToPal(paldb);
            var parent2 = parent2Name.ToPal(paldb);
            var child = childName.ToPal(paldb);

            var result = breedingdb.BreedingByParent[parent1][parent2];

            Assert.IsTrue(result.Any(br => br.Child == child), $"Expected {parent1Name} + {parent2Name} = {childName}({child.InternalName}), got {string.Join(",", result.Select(br => $"{br.Child.Name}({br.Child.InternalName})"))}");
        }

        [TestMethod]
        public void BreedingByParent_Specific_KitsunNoctAndFellbat_gives_Bulldosu()
        {
            TestSpecificBreedingResult(
                parent1Name: "Kitsun Noct",
                parent2Name: "Felbat",
                childName: "Bulldosu"
            );
        }

        [TestMethod]
        public void BreedingByParent_Specific_FuddlerAndSuzaku_gives_Souffline()
        {
            TestSpecificBreedingResult(
                parent1Name: "Fuddler",
                parent2Name: "Suzaku",
                childName: "Souffline"
            );
        }

        [TestMethod]
        public void BreedingByParent_Specific_JormuntideIgnisAndBraloha_gives_WumpoBotan()
        {
            TestSpecificBreedingResult(
                parent1Name: "Jormuntide Ignis",
                parent2Name: "Braloha",
                childName: "Wumpo Botan"
            );
        }

        [TestMethod]
        public void BreedingByParent_Specific_ElgroveCrystAndMycora_gives_WumpoBotan()
        {
            TestSpecificBreedingResult(
                parent1Name: "Elgrove Cryst",
                parent2Name: "Mycora",
                childName: "Wumpo Botan"
            );
        }


    }
}
