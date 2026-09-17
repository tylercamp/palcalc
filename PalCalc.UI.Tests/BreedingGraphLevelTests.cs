using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Tree;
using PalCalc.UI.Model;
using PalCalc.UI.Persistence.Dto;
using PalCalc.UI.Persistence.Serialization;
using PalCalc.UI.ViewModel.Converters;
using PalCalc.UI.ViewModel.GraphSharp;
using System.Globalization;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using GraphSharp.Controls;
using PalCalc.UI.View.Main;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

[TestClass]
public class BreedingGraphLevelTests
{
    private static readonly PalDB DB = PalDB.LoadEmbedded();

    [TestMethod]
    public void TrainingDescriptionsSurviveResultRoundTripAndGenderCopies()
    {
        var pal = "Beakon".ToPal(DB);
        var owned = new OwnedPalReference(new PalInstance
        {
            Pal = pal, Gender = PalGender.MALE, Level = 5, InstanceId = "trained",
            PassiveSkills = [], ActiveSkills = [], EquippedActiveSkills = [],
            Location = new PalLocation { Type = LocationType.Palbox, ContainerId = "box", Index = 1 }
        }, [], new(IV_Value.Random, IV_Value.Random, IV_Value.Random), AttackProfile.Inactive, new(5, 20));
        var wild = new WildPalReference(pal, [], 0, DB.BreedingMechanics, AttackProfile.Inactive, new(10, 30))
            .WithGuaranteedGender(DB, PalGender.FEMALE, false);
        foreach (var reference in new IPalReference[] { owned, wild })
        {
            var json = JsonConvert.SerializeObject(ResultJsonSerializer.ToDto(reference));
            var restored = ResultJsonSerializer.FromDto(JsonConvert.DeserializeObject<PalReferenceDto>(json)!, DB,
                new GameSettings(), new SerializableSolverSettings());
            Assert.AreEqual(reference.LevelRequirements, restored.LevelRequirements);
            Assert.AreEqual(reference.Gender, restored.Gender);
            var node = new StandardBreedingTreeNodeViewModel(null, new GameSettings(), new DirectPalNode(restored));
            Assert.IsTrue(node.HasLevelDescription);
            StringAssert.Contains(node.LevelDescription.Value, $"{reference.LevelRequirements.InitialLevel}");
            StringAssert.Contains(node.LevelDescription.Value, $"{reference.LevelRequirements.FinalLevel}");
            StringAssert.Contains(node.LevelDescription.Value, "→");

            var oldJson = JObject.Parse(json);
            oldJson.Remove(nameof(PalReferenceDto.LevelRequirements));
            Assert.Throws<JsonSerializationException>(() => oldJson.ToObject<PalReferenceDto>());
        }
    }

    [TestMethod]
    public void WildLevelDependsOnInstructedAttackAndClearsForAnyAttack()
    {
        var pal = DB.Pals.First(p => p.AttackLeveling(DB).Any(e => e.Level > 1));
        var attack = pal.AttackLeveling(DB).First(e => e.Level > 1);
        var reference = new WildPalReference(pal, [], 0, DB.BreedingMechanics, AttackProfile.Inactive, null);
        var node = new StandardBreedingTreeNodeViewModel(null, new GameSettings(), new DirectPalNode(reference));
        var changes = new List<string?>();
        node.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        node.SetEquippedAttacks([attack.Attack]);
        Assert.IsTrue(node.HasLevelDescription);
        StringAssert.Contains(node.LevelDescription.Value, $"{attack.Level}");
        Assert.DoesNotContain("→", node.LevelDescription.Value);
        node.SetEquippedAttacks([new RandomActiveSkill()]);
        Assert.IsFalse(node.HasLevelDescription);
        CollectionAssert.Contains(changes, nameof(node.HasLevelDescription));
        CollectionAssert.Contains(changes, nameof(node.LevelDescription));
    }

    [TestMethod]
    public void EdgeAttachmentsExcludeBadgeHeight()
    {
        var converter = new BreedingEdgeRouteToPathConverter();
        object[] values = [0d, 100d, 100d, 80d, 200d, 100d, 100d, 80d, null!, 60d, 60d];
        var path = (PathFigureCollection)converter.Convert(values, typeof(PathFigureCollection), null!, CultureInfo.InvariantCulture);
        Assert.AreEqual(90d, path[0].StartPoint.Y);
        Assert.AreEqual(90d, path[1].StartPoint.Y);
        values[9] = values[10] = 80d;
        path = (PathFigureCollection)converter.Convert(values, typeof(PathFigureCollection), null!, CultureInfo.InvariantCulture);
        Assert.AreEqual(100d, path[0].StartPoint.Y);
    }

    [TestMethod]
    public void LevelBadgeAlignsWithMainBodyAndCollapsesWithoutAttackTargets()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var pal = "Beakon".ToPal(DB);
                var attack = pal.Level1ActiveSkills(DB).First();
                var reference = new WildPalReference(pal, [], 0, DB.BreedingMechanics, AttackProfile.Inactive, new(5, 20));
                var display = new BreedingResultViewModel(null, new GameSettings(), reference, [attack]);
                var view = new BreedingResultView { DisplayedResult = display };
                var content = (UserControl)view.Content;
                content.Resources[typeof(Button)] = new Style(typeof(Button));
                content.Resources[AdonisUI.Brushes.Layer1IntenseHighlightBorderBrush] = Brushes.SlateGray;
                var node = display.Graph.Nodes.Single();
                var vertex = new VertexControl
                {
                    Vertex = node, DataContext = node,
                    Style = (Style)content.Resources[typeof(VertexControl)]
                };
                content.Content = vertex;
                vertex.ApplyTemplate();
                Layout();
                var body = (Border)vertex.Template.FindName("NodeBorder", vertex);
                var badge = (Border)vertex.Template.FindName("LevelBorder", vertex);
                Assert.AreEqual(Visibility.Visible, badge.Visibility);
                var bodyPosition = body.TranslatePoint(new Point(), vertex);
                var badgePosition = badge.TranslatePoint(new Point(), vertex);
                Assert.AreEqual(bodyPosition.X + body.ActualWidth, badgePosition.X + badge.ActualWidth, 0.01);
                Assert.IsGreaterThanOrEqualTo(bodyPosition.Y + body.ActualHeight, badgePosition.Y);
                Assert.AreEqual(body.ActualHeight, BreedingResultView.GetNodeBodyHeight(vertex));
                Assert.IsGreaterThan(body.ActualHeight, vertex.ActualHeight);

                foreach (var theme in new[] { "Light", "Dark" })
                {
                    content.Resources.MergedDictionaries.Clear();
                    content.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri($"pack://application:,,,/PalCalc.UI;component/View/Main/BreedingResultView.{theme}.xaml")
                    });
                    vertex.Foreground = theme == "Dark" ? Brushes.Gainsboro : Brushes.Black;
                    Layout();
                    Assert.AreEqual(body.Background, badge.Background);
                    if (Environment.GetEnvironmentVariable("PALCALC_LEVEL_PREVIEW") is { } path)
                    {
                        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),
                            (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(view);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var output = System.IO.File.Create(System.IO.Path.ChangeExtension(path, $"{theme}.png"));
                        encoder.Save(output);
                    }
                }

                view.DisplayedResult = new BreedingResultViewModel(null, new GameSettings(), reference, []);
                Layout();
                Assert.AreEqual(Visibility.Collapsed, ((Grid)badge.Parent).Visibility);
                Assert.AreEqual(body.ActualHeight, vertex.ActualHeight);

                void Layout()
                {
                    view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    view.Arrange(new Rect(view.DesiredSize));
                    view.UpdateLayout();
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }
}
