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

        [TestMethod]
        public void BreedingResults_FuddlerAndSuzaku_gives_Souffline()
        {
            var fuddler = "Fuddler".ToPal(paldb);
            var suzaku = "Suzaku".ToPal(paldb);
            var souffline = "Souffline".ToPal(paldb);

            var result = breedingdb.BreedingByParent[fuddler][suzaku];

            Assert.IsTrue(result.Any(br => br.Child == souffline), $"Expected Fuddler + Suzaku = Souffline({souffline.InternalName}), got {string.Join(",", result.Select(br => $"{br.Child.Name}({br.Child.InternalName})"))}");
        }

        [TestMethod]
        public void BreedingResults_JormuntideIgnisAndBraloha_gives_WumpoBotan()
        {
            var jormuntideIgnis = "Jormuntide Ignis".ToPal(paldb);
            var braloha = "Braloha".ToPal(paldb);
            var wumpoBotan = "Wumpo Botan".ToPal(paldb);

            var result = breedingdb.BreedingByParent[jormuntideIgnis][braloha];

            Assert.IsTrue(result.Any(br => br.Child == wumpoBotan), $"Expected Jormuntide Ignis + Braloha = Wumpo Botan({wumpoBotan.InternalName}), got {string.Join(",", result.Select(br => $"{br.Child.Name}({br.Child.InternalName})"))}");
        }
    }
}
