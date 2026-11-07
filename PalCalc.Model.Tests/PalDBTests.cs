using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        [TestMethod]
        public void AttackLevelingRoundTripsThroughJson()
        {
            var json = JObject.Parse(paldb.ToJson());
            var firstPal = (JObject)json["Pals"]!.First!;
            firstPal["InternalAttackLeveling"] = JArray.FromObject(new[]
            {
                new PalInternalAttackLevel { AttackInternalId = paldb.ActiveSkills.First().InternalName, Level = 7 },
            });

            var roundTrippedPal = PalDB.FromJson(json.ToString()).Pals.First(p =>
                p.InternalName == firstPal["InternalName"]!.ToObject<string>()
            );

            Assert.AreEqual(7, roundTrippedPal.InternalAttackLeveling.Single().Level);
            Assert.AreEqual(
                paldb.ActiveSkills.First().InternalName,
                roundTrippedPal.InternalAttackLeveling.Single().AttackInternalId
            );
        }

        [TestMethod]
        public void WildAttacksFallBackToLevelOneWithoutWildLevelData()
        {
            var source = paldb.Pals.First(pal => pal.InternalAttackLeveling.Any(entry => entry.Level > 1));
            var pal = new Pal
            {
                MinWildLevel = null,
                InternalAttackLeveling = source.InternalAttackLeveling,
            };

            CollectionAssert.AreEquivalent(
                pal.Level1ActiveSkills(paldb).ToArray(),
                pal.WildActiveSkills(paldb).ToArray()
            );
        }

        [TestMethod]
        public void PalInstanceRankRoundTripsThroughJson()
        {
            var converter = new PalInstanceJsonConverter(paldb);
            var json = JsonConvert.SerializeObject(MakeInstanceWithRank(4), converter);

            Assert.AreEqual(4, JsonConvert.DeserializeObject<PalInstance>(json, converter).Rank);
        }

        [TestMethod]
        public void PalInstanceRankDefaultsToOneWhenMissingFromJson()
        {
            var converter = new PalInstanceJsonConverter(paldb);
            var json = JObject.Parse(JsonConvert.SerializeObject(MakeInstanceWithRank(4), converter));
            json.Remove("Rank");

            Assert.AreEqual(1, JsonConvert.DeserializeObject<PalInstance>(json.ToString(), converter).Rank);
        }

        private PalInstance MakeInstanceWithRank(int rank) => new()
        {
            Pal = paldb.Pals.First(),
            Location = new PalLocation { Type = LocationType.Palbox, ContainerId = "box", Index = 1 },
            Gender = PalGender.MALE,
            PassiveSkills = [],
            Rank = rank,
        };
    }
}
