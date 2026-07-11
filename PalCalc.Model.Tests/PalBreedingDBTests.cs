namespace PalCalc.Model.Tests
{
    [TestClass]
    public sealed class PalBreedingDBTests : PalTestBase
    {
        [TestMethod]
        public void BreedingResults_KitsunNoctAndFellbat_gives_Bulldosu()
        {
            var kitsunNoct = "Kitsun Noct".ToPal(paldb);
            var fellbat = "Felbat".ToPal(paldb);
            var bulldosu = "Bulldosu".ToPal(paldb);

            var result = breedingdb.BreedingByParent[kitsunNoct][fellbat];

            Assert.IsTrue(result.Any(br => br.Child == bulldosu), $"Expected Kitsun Noct + Fellbat = Bulldosu({bulldosu.InternalName}), got {string.Join(",", result.Select(br => $"{br.Child.Name}({br.Child.InternalName})"))}");
        }
    }
}
