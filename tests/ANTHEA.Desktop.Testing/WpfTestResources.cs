using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;

namespace Anthea.Desktop.Testing;

/// <summary>Loads production styles without starting the product or writing user preferences.</summary>
internal static class WpfTestResources
{
    internal static void Load(Application app, string source = "X.Desktop/App.xaml")
    {
        var document = XDocument.Load(source);
        var dictionary = document.Root!.Elements().Single().Elements().Single();
        dictionary.SetAttributeValue(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
        dictionary.SetAttributeValue(XNamespace.Xmlns + "local", "clr-namespace:X.Desktop;assembly=ANTHEA");
        foreach (var item in dictionary.Descendants().SelectMany(x => x.Attributes("Source"))) item.Value = "/ANTHEA;component/" + item.Value;
        app.Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
    }
}
