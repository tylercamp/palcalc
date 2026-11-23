using PalCalc.Model;

namespace PalCalc.Model.Tests;

[TestClass]
public class MapCoordTests
{
    [TestMethod]
    [DataRow(-123888d, 158000d, 0d, 0d)]
    [DataRow(-582888d, 617000d, 1000d, -1000d)]
    [DataRow(794112d, -760000d, -2000d, 2000d)]
    [DataRow(-124117.5d, 158688.5d, 1.5d, -0.5d)]
    public void UIFromWorldCoord_MatchesUnclampedMapLabels(double worldX, double worldY, double x, double y)
    {
        var result = MapCoord.UIFromWorldCoord(new WorldCoord { X = worldX, Y = worldY, Z = 100 });

        Assert.AreEqual(x, result.X);
        Assert.AreEqual(y, result.Y);
    }

    [TestMethod]
    [DataRow(349400d, -724400d, 0d, 0d)]
    [DataRow(-1099400d, 724400d, 1d, 1d)]
    [DataRow(-737200d, -362200d, 0.25d, 0.75d)]
    [DataRow(-2548200d, -2173200d, -1d, 2d)]
    public void NormalizedFromWorldCoord_MatchesUnclampedMainMapBounds(double worldX, double worldY, double x, double y)
    {
        var result = MapCoord.NormalizedFromWorldCoord(new WorldCoord { X = worldX, Y = worldY, Z = 100 });

        Assert.AreEqual(x, result.X);
        Assert.AreEqual(y, result.Y);
    }
}
