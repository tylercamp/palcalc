A slightly modified of [AutoCompleteComboBox by vain0x](https://github.com/vain0x/DotNetKit.Wpf.AutoCompleteComboBox/tree/main). Adds:

- An option to clear/reset the value if the dropdown loses focus when set to an invalid text value
- Escape key clears/resets the current value

# AutoCompleteComboBox for WPF

[![NuGet version](https://badge.fury.io/nu/DotNetKit.Wpf.AutoCompleteComboBox.svg)](https://badge.fury.io/nu/DotNetKit.Wpf.AutoCompleteComboBox)

Provides a lightweight combobox with filtering (auto-complete).

## Screenshot
![](documents/images/screenshot.gif)

## Usage
[Install via NuGet](https://www.nuget.org/packages/DotNetKit.Wpf.AutoCompleteComboBox).

Declare XML namespace.

```xml
<Window
    ...
    xmlns:dotNetKitControls="clr-namespace:DotNetKit.Windows.Controls;assembly=DotNetKit.Wpf.AutoCompleteComboBox"
    ... >
```

Then you can use `AutoCompleteComboBox`. It's like a normal `ComboBox` because of inheritance.

```xml
<dotNetKitControls:AutoCompleteComboBox
    SelectedValuePath="Id"
    TextSearch.TextPath="Name"
    ItemsSource="{Binding Items}"
    SelectedItem="{Binding SelectedItem}"
    SelectedValue="{Binding SelectedValue}"
    />
```

Note that:

- Set a property path to ``TextSearch.TextPath`` property.
    - The path leads to a property whose getter produces a string value to identify items. For example, assume each item is an instance of `Person`, which has `Name` property, and the property path is "Name". If the user input "va", the combobox filters the items to remove ones (persons) whose `Name` don't contain "va".
    - No support for ``TextSeach.Text``.
- Don't use ``ComboBox.Items`` property directly. Use `ItemsSource` instead.
- Although the Demo project uses DataTemplate to display items, you can also use `DisplayMemberPath`.

### Configuration
This library works fine in the default setting, however, it also provides how to configure.

- Define a class derived from [DotNetKit.Windows.Controls.AutoCompleteComboBoxSetting](DotNetKit.Wpf.AutoCompleteComboBox/Windows/Controls/AutoCompleteComboBoxSetting.cs) to override some of properties.
- Set the instance to ``AutoCompleteComboBox.Setting`` property.

```xml
<dotNetKitControls:AutoCompleteComboBox
    Setting="..."
    ...
    />
```

- Or set to ``AutoCompleteComboBoxSetting.Default`` to apply to all comboboxes.

### Performance
Filtering allows you to add a lot of items to a combobox without loss of usability, however, that makes the performance poor. To get rid of the issue, we recommend you to use `VirtualizingStackPanel` as the panel.

Use `ItemsPanel` property:

```csharp
<dotNetKitControls:AutoCompleteComboBox ...>
    <dotNetKitControls:AutoCompleteComboBox.ItemsPanel>
        <ItemsPanelTemplate>
            <VirtualizingStackPanel />
        </ItemsPanelTemplate>
    </dotNetKitControls:AutoCompleteComboBox.ItemsPanel>
</dotNetKitControls:AutoCompleteComboBox>
```

or declare a style in resources as the Demo app does.

See also [WPF: Using a VirtualizingStackPanel to Improve ComboBox Performance](http://vbcity.com/blogs/xtab/archive/2009/12/15/wpf-using-a-virtualizingstackpanel-to-improve-combobox-performance.aspx) for more detailed explanation.
