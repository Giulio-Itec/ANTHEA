using System.Windows.Media;

namespace X.Desktop;

internal static class UtilizationPalette
{
    internal const string Legend = Anthea.Application.Presentation.UtilizationColors.Legend;
    internal static Color Color(double? value) => (Color)ColorConverter.ConvertFromString(Anthea.Application.Presentation.UtilizationColors.Color(value));
    internal static Brush Brush(double? value) { var b = new SolidColorBrush(Color(value)); b.Freeze(); return b; }
}
