using PalCalc.UI.Model;
using PalCalc.UI.Model.Service;

namespace PalCalc.UI.Tests;

[TestClass]
public class UILayoutValidationTests
{
    [TestMethod]
    public void WindowPlacementRejectsObsoleteAndInvalidBounds()
    {
        var valid = new WindowPlacementSettings
        {
            Version = 1,
            Left = 100,
            Top = 100,
            Right = 1380,
            Bottom = 820,
        };

        Assert.IsTrue(UILayoutValidation.IsValidWindowPlacement(valid, 1));
        Assert.IsFalse(UILayoutValidation.IsValidWindowPlacement(valid, 2));

        valid.Right = valid.Left;
        Assert.IsFalse(UILayoutValidation.IsValidWindowPlacement(valid, 1));
    }

    [TestMethod]
    public void GridLengthsRejectChangedStructure()
    {
        var saved = new[]
        {
            Length(LayoutGridUnit.Star, 1),
            Length(LayoutGridUnit.Star, 1),
        };
        var constraints = new[]
        {
            Constraint(100),
        };

        var restored = UILayoutValidation.TryNormalizeGridLengths(
            saved,
            constraints,
            500,
            primaryIndex: 0,
            primaryMinimum: 100,
            out _
        );

        Assert.IsFalse(restored);
    }

    [TestMethod]
    public void GridLengthsShrinkPixelsToPreserveMinimums()
    {
        var saved = new[]
        {
            Length(LayoutGridUnit.Pixel, 400),
            Length(LayoutGridUnit.Auto, 1),
            Length(LayoutGridUnit.Pixel, 400),
        };
        var constraints = new[]
        {
            Constraint(100),
            Constraint(0, autoSize: 5),
            Constraint(100),
        };

        var restored = UILayoutValidation.TryNormalizeGridLengths(
            saved,
            constraints,
            availableSize: 600,
            primaryIndex: 2,
            primaryMinimum: 250,
            out var normalized
        );

        Assert.IsTrue(restored);
        Assert.IsNotNull(normalized);
        Assert.IsGreaterThanOrEqualTo(100, normalized[0].Value);
        Assert.IsGreaterThanOrEqualTo(250, normalized[2].Value);
        Assert.AreEqual(600, normalized[0].Value + 5 + normalized[2].Value, 0.01);
    }

    [TestMethod]
    public void GridLengthsFallBackWhenMinimumsCannotFit()
    {
        var saved = new[]
        {
            Length(LayoutGridUnit.Star, 1),
            Length(LayoutGridUnit.Star, 1),
        };
        var constraints = new[]
        {
            Constraint(400),
            Constraint(400),
        };

        var restored = UILayoutValidation.TryNormalizeGridLengths(
            saved,
            constraints,
            availableSize: 500,
            primaryIndex: 1,
            primaryMinimum: 400,
            out _
        );

        Assert.IsFalse(restored);
    }

    [TestMethod]
    public void GridLengthsRejectNonFiniteValues()
    {
        var restored = UILayoutValidation.TryNormalizeGridLengths(
            [Length(LayoutGridUnit.Star, double.NaN)],
            [Constraint(0)],
            availableSize: 500,
            primaryIndex: 0,
            primaryMinimum: 100,
            out _
        );

        Assert.IsFalse(restored);
    }

    private static GridLengthSettings Length(LayoutGridUnit unit, double value) => new()
    {
        Unit = unit,
        Value = value,
    };

    private static GridDefinitionConstraints Constraint(
        double minimum,
        double maximum = double.PositiveInfinity,
        double autoSize = 0
    ) => new(minimum, maximum, autoSize);
}
