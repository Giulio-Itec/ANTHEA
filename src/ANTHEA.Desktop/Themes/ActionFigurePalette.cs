using System.Windows.Media;

namespace X.Desktop.Themes;

/// <summary>Maps the fixed print palette of our packaged SVG figures to existing UI tokens. No new literal UI colours.</summary>
internal static class ActionFigurePalette
{
    internal static Brush? FromOriginal(string value)=>value switch
    {
        "none"=>null,"#ffffff"=>ThemeTokens.Brush("Paper"),
        "currentColor" or "#203348" or "#000000"=>ThemeTokens.Brush("Ink"),
        "#1976a3"=>ThemeTokens.Brush("Series.Blue"),"#c36522"=>ThemeTokens.Brush("Series.Orange"),
        "#218463"=>ThemeTokens.Brush("Series.Green"),"#c02672"=>ThemeTokens.Brush("Series.Magenta"),
        "#7c8b99" or "#b0b0b0"=>ThemeTokens.Brush("Border"),
        "#cbd6df" or "#edf1f4" or "#eaf1f5" or "#f4d5b9" or "#e7edf2" or "#edf4f7" or "#b7c7b5" or "#edf1f5" or "#eaf3f7" or "#f4e6ce" or "#e3ceb5" or "#dce5eb" or "#dbeaf3"=>ThemeTokens.Brush("Surface"),
        _=>throw new InvalidOperationException("Colore della figura senza associazione al tema: "+value)
    };
}
