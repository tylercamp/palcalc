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

    [TestMethod]
    public void ReplacingItemsThroughNullPreservesSelectedValueAndBinding()
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
                Assert.AreEqual("attack", comboBox.SelectedValue);

                comboBox.ItemsSource = null;
                Assert.AreEqual("attack", selection.Attack, "Clearing choices changed the bound model");
                var replacementSelection = new Selection { Attack = "attack" };
                comboBox.SetBinding(Selector.SelectedValueProperty, new Binding(nameof(Selection.Attack))
                {
                    Source = replacementSelection,
                    Mode = BindingMode.TwoWay,
                });
                var replacement = new Item("attack");
                comboBox.ItemsSource = new[] { replacement };

                Assert.AreEqual("attack", comboBox.SelectedValue);
                Assert.AreSame(replacement, comboBox.SelectedItem);
                Assert.AreEqual("attack", replacementSelection.Attack);
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

    [TestMethod]
    public void ReplacingItemsThroughNullUsesEmptyReplacementSelection()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var comboBox = new AutoCompleteComboBox
                {
                    ItemsSource = new[] { new Item("attack") },
                    SelectedValuePath = nameof(Item.Attack),
                    SelectedValue = "attack",
                };

                comboBox.ItemsSource = null;
                var replacementSelection = new Selection();
                comboBox.SetBinding(Selector.SelectedValueProperty, new Binding(nameof(Selection.Attack))
                {
                    Source = replacementSelection,
                    Mode = BindingMode.TwoWay,
                });
                comboBox.ItemsSource = new[] { new Item("attack") };

                Assert.IsNull(comboBox.SelectedValue);
                Assert.IsNull(comboBox.SelectedItem);
                Assert.IsNull(replacementSelection.Attack);
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
