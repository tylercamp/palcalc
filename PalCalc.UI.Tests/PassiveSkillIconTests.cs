using PalCalc.UI.Model;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.Tests;

[TestClass]
public class PassiveSkillIconTests
{
    [TestMethod]
    public void RankIconsArePreRenderedFrozenBitmaps()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PassiveSkillIcon.Initialize();

                CollectionAssert.AreEquivalent(Enumerable.Range(-3, 9).ToArray(), PassiveSkillIcon.Images.Keys.ToArray());
                Assert.IsTrue(PassiveSkillIcon.Images.Values.All(icon => icon is BitmapSource { IsFrozen: true }));
                Assert.AreSame(PassiveSkillIcon.Images[0], PassiveSkillIcon.Images[1]);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            throw failure;
    }
}
