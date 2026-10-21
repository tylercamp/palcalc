using PalCalc.UI.View;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;

namespace PalCalc.UI.Tests;

[TestClass]
public class WeblinkTests
{
    [STATestMethod]
    public void UriSupportsXamlBindingAndHyperlinkStyle()
    {
        var link = (Weblink)XamlReader.Parse("""
            <view:Weblink xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                          xmlns:view="clr-namespace:PalCalc.UI.View;assembly=PalCalc.UI"
                          Uri="https://example.com/">Example</view:Weblink>
            """);
        Assert.AreEqual(new Uri("https://example.com/"), link.NavigateUri);
        Assert.AreEqual("Example", new TextRange(link.ContentStart, link.ContentEnd).Text);

        var style = new Style(typeof(Hyperlink));
        link.Resources[typeof(Hyperlink)] = style;
        Assert.AreSame(style, link.Style);

        var destination = new Uri("https://example.org/");
        BindingOperations.SetBinding(link, Weblink.UriProperty, new Binding { Source = destination });
        Assert.AreEqual(destination, link.Uri);
        Assert.AreEqual(destination, link.NavigateUri);
    }
}
