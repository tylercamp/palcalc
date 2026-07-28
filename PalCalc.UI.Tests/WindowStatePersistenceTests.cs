using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using PalCalc.UI.Model;

namespace PalCalc.UI.Tests
{
    [TestClass]
    public class WindowStatePersistenceTests
    {
        [TestMethod]
        public void Columns_PreservePixelAndStarUnits()
        {
            var cols = new List<ColumnDefinition>
            {
                new() { Width = new GridLength(300) },
                new() { Width = new GridLength(232, GridUnitType.Star) },
                new() { Width = new GridLength(80, GridUnitType.Star) },
            };

            var saved = WindowStatePersistence.SaveColumns(cols);

            var restored = new List<ColumnDefinition>
            {
                new() { Width = new GridLength(1) },
                new() { Width = new GridLength(1) },
                new() { Width = new GridLength(1) },
            };
            WindowStatePersistence.ApplyColumns(restored, saved);

            Assert.AreEqual(new GridLength(300), restored[0].Width);
            Assert.AreEqual(new GridLength(232, GridUnitType.Star), restored[1].Width);
            Assert.AreEqual(new GridLength(80, GridUnitType.Star), restored[2].Width);
        }

        [TestMethod]
        public void Columns_PreserveAutoUnit()
        {
            var cols = new List<ColumnDefinition> { new() { Width = GridLength.Auto } };
            var saved = WindowStatePersistence.SaveColumns(cols);

            var restored = new List<ColumnDefinition> { new() { Width = new GridLength(50) } };
            WindowStatePersistence.ApplyColumns(restored, saved);

            Assert.AreEqual(GridLength.Auto, restored[0].Width);
        }

        [TestMethod]
        public void ApplyColumns_IgnoresCountMismatch()
        {
            var cols = new List<ColumnDefinition> { new() { Width = new GridLength(1) } };
            WindowStatePersistence.ApplyColumns(cols, new List<string> { "10", "20" });
            Assert.AreEqual(new GridLength(1), cols[0].Width);
        }

        [TestMethod]
        public void ApplyColumns_NullIsNoOp()
        {
            var cols = new List<ColumnDefinition> { new() { Width = new GridLength(5) } };
            WindowStatePersistence.ApplyColumns(cols, null);
            Assert.AreEqual(new GridLength(5), cols[0].Width);
        }

        [TestMethod]
        public void UILayouts_RoundTripsThroughJson()
        {
            var settings = new AppSettings();
            settings.UILayouts["main"] = new WindowLayoutState
            {
                Left = 100, Top = 50, Width = 1280, Height = 720, Maximized = true
            };
            settings.UILayouts["solver"] = new WindowLayoutState
            {
                ColumnWidths = new List<string> { "300", "232*", "80*" }
            };

            var json = JsonConvert.SerializeObject(settings);
            var back = JsonConvert.DeserializeObject<AppSettings>(json);

            Assert.AreEqual(100, back.UILayouts["main"].Left);
            Assert.AreEqual(720, back.UILayouts["main"].Height);
            Assert.AreEqual(true, back.UILayouts["main"].Maximized);
            CollectionAssert.AreEqual(
                new[] { "300", "232*", "80*" },
                back.UILayouts["solver"].ColumnWidths);
        }

        [TestMethod]
        public void UILayouts_DefaultsToEmptyNotNull()
        {
            Assert.IsNotNull(new AppSettings().UILayouts);
        }
    }
}
