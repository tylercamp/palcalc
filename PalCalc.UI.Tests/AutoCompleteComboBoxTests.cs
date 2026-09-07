using DotNetKit.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace PalCalc.UI.Tests;

[TestClass]
public class AutoCompleteComboBoxTests
{
    [TestMethod]
    public void ReplacingItemsPreservesSelectedValueAndBinding()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var selection = new Selection { Attack = "attack" };
                var comboBox = new AutoCompleteComboBox
                {
                    ItemsSource = new[] { new Item("attack") },
                    SelectedValuePath = nameof(Item.Attack),
                };
                comboBox.SetBinding(Selector.SelectedValueProperty, new Binding(nameof(Selection.Attack))
                {
                    Source = selection,
                    Mode = BindingMode.TwoWay,
                });
                Assert.AreEqual(1, comboBox.Items.Count);
                Assert.AreEqual("attack", comboBox.SelectedValue);
                Assert.IsNotNull(comboBox.SelectedItem);

                var replacement = new Item("attack");
                comboBox.ItemsSource = new[] { replacement };

                Assert.AreEqual(1, comboBox.Items.Count);
                Assert.AreEqual("attack", comboBox.SelectedValue);
                Assert.AreSame(replacement, comboBox.SelectedItem);
                Assert.AreEqual("attack", selection.Attack);
                Assert.IsTrue(BindingOperations.IsDataBound(comboBox, Selector.SelectedValueProperty));
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

    private sealed class Item(string attack)
    {
        public string Attack { get; } = attack;
    }

    private sealed class Selection
    {
        public string? Attack { get; set; }
    }
}
