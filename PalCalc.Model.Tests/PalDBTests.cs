using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model.Tests
{
    [TestClass]
    public class PalDBTests : PalTestBase
    {
        [TestMethod]
        public void AllPalsUniqueByInternalName()
        {
            var groups = paldb.Pals
                .GroupBy(p => p.InternalName)
                .Where(g => g.Count() > 1)
                .ToList();

            Assert.IsEmpty(groups, $"Expected no duplicate pals by internal name, found duplicates of {string.Join(", ", groups.Select(g => g.Key))}");
        }

        [TestMethod]
        public void AllPalsUniqueByEnglishName()
        {
            var groups = paldb.Pals
                .GroupBy(p => p.Name)
                .Where(g => g.Count() > 1)
                .Where(g => g.Key != "Gumoss") // (Pals known to have variants with the same exact name)
                .ToList();

            Assert.IsEmpty(groups, $"Expected no duplicate pals by english name, found duplicates of {string.Join(", ", groups.Select(g => g.Key))}");
        }
    }
}
