using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model.Tests
{
    [TestClass]
    public class PalTestBase
    {
        private static PalDB defaultDb;
        private static PalBreedingDB defaultBreedingDb;

        protected PalDB paldb => defaultDb;
        protected PalBreedingDB breedingdb => defaultBreedingDb;

        [AssemblyInitialize]
        public static async Task AssemblyInit(TestContext context)
        {
            defaultDb = PalDB.LoadEmbedded();
            defaultBreedingDb = PalBreedingDB.LoadEmbedded(defaultDb);
        }
    }
}
