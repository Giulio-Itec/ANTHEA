using Anthea.Calculations;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Anthea.Presentation.Forms;

/// <summary>
/// Presentation of a number in a field without focus or in a table cell: the pure function of the former
/// X.Desktop NumericPresentation.Format, moved in F5.2 (proposal F5, section 2.1). Presentation only: editable values and the
/// document keep the full text. The WPF converter stays in X.Desktop and calls this function.
/// </summary>
public static class NumericDisplay
{
    /// <summary>The text with at most two decimals (no negative zero), small strains in engineering notation; identifiers and
    /// texts that are not finite numbers unchanged.</summary>
    public static string Format(string text, string key)
    {
        if (key is "id" or "nome" or "name" or "descrizione" || J.Number(JsonValue.Create(text)) is not double n || !double.IsFinite(n)) return text;
        bool strain = key.Contains("strain", StringComparison.OrdinalIgnoreCase) || key.Contains("eps", StringComparison.OrdinalIgnoreCase);
        if (strain && n != 0 && Math.Abs(n) < .01) return EngineeringFormat.Number(n);
        return (Math.Round(n, 2) == 0 ? 0 : n).ToString("0.##", CultureInfo.CurrentCulture);
    }
}
