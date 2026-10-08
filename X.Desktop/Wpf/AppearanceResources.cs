using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

internal static class AppearanceResources
{
    internal static void Register(ResourceDictionary resources)
    {
        resources["Appearance.background.0B2A4A"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#0B2A4A"), "background");
        resources["Appearance.background.244665"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#244665"), "background");
        resources["Appearance.selection.E2EFFC"] = Appearance.Selection("#E2EFFC");
        resources["Appearance.background.E3EFFA"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#E3EFFA"), "background");
        resources["Appearance.background.E8EFF7"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#E8EFF7"), "background");
        resources["Appearance.background.F0F6FC"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#F0F6FC"), "background");
        resources["Appearance.background.F3F5F8"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#F3F5F8"), "background");
        resources["Appearance.background.F8FAFC"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#F8FAFC"), "background");
        resources["Appearance.background.FFFFFF"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#FFFFFF"), "background");
        resources["Appearance.border.2582D2"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#2582D2"), "border");
        resources["Appearance.border.3986CB"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#3986CB"), "border");
        resources["Appearance.border.CAD7E5"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#CAD7E5"), "border");
        resources["Appearance.border.DCE2E9"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#DCE2E9"), "border");
        resources["Appearance.border.DCE5EE"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#DCE5EE"), "border");
        resources["Appearance.border.E4EAF1"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#E4EAF1"), "border");
        resources["Appearance.foreground.0B2A4A"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#0B2A4A"), "foreground");
        resources["Appearance.foreground.234767"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#234767"), "foreground");
        resources["Appearance.foreground.9A4D0A"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#9A4D0A"), "foreground");
        resources["Appearance.foreground.FFFFFF"] = Appearance.Colour((Color)ColorConverter.ConvertFromString("#FFFFFF"), "foreground");
    }
}
