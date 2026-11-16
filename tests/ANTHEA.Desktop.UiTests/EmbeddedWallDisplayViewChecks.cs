using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.EmbeddedWalls;
using X.Desktop.Views.EmbeddedWalls;

namespace X.Desktop;

/// <summary>
/// Cifre significative (16/11), off screen: the table of the results and the envelopes show the numbers with the decimals of their unit and the
/// decimal separator of the application (the checks fix «Virgola»), never an exponent or «-0»; the side of the node in Italian. Captures
/// cifre-* in Chiara and Scuro. In the section: «Tipo» of a tie shows «Tirante» (the value stays «Anchor») and the name of a layer stays
/// off the dashed line of the water.
/// </summary>
internal static partial class EmbeddedWallViewChecks
{
    private static readonly Regex ExponentText = new("[0-9][eE][+-]?[0-9]", RegexOptions.CultureInvariant);

    private static IReadOnlyList<(string Header, string Text)> Cells(DataGrid grid) => Visual(grid).OfType<DataGridCell>()
        .Select(c => (c.Column.Header as string ?? "", (c.Content as TextBlock)?.Text ?? "")).Where(c => c.Item2.Length > 0).ToArray();

    private static void CheckNumbers(IReadOnlyList<(string Header, string Text)> cells, string[] texts, Action<bool, string> check, string where)
    {
        string separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, other = separator == "," ? "." : ",";
        var numbers = cells.Where(c => !texts.Contains(c.Header)).Select(c => c.Text).ToArray();
        check(numbers.Length > 0 && numbers.All(t => !ExponentText.IsMatch(t) && t != "-0" && !t.Contains(other)) && numbers.Any(t => t.Contains(separator)),
            $"Cifre: {where} con esponenti, «-0» o separatore diverso da «{separator}»: " + string.Join(" | ", numbers.Where(t => ExponentText.IsMatch(t) || t == "-0" || t.Contains(other)).Take(8)));
    }

    private static async Task CheckDisplayDigits(FrameworkElement view, EmbeddedWallSheetViewModel sheet,
        Func<Task> settle, Action<bool, string> check, Action<string> capture)
    {
        string before = sheet.Data.ToJsonString();
        sheet.Documents.Show(EmbeddedWallDocuments.Diagrams); await settle();
        var points = Visual(view).OfType<DataGrid>().Single(g => g.IsVisible && ReferenceEquals(g.ItemsSource, sheet.Points));
        var cells = Cells(points);
        CheckNumbers(cells, ["Lato nodo", "Sezione"], check, "tabella dei risultati");
        check(cells.Where(c => c.Header == "Lato nodo").All(c => c.Text is "sotto" or "sopra" or "interno") && cells.Any(c => c.Header == "Lato nodo"),
            "Cifre: lato del nodo non in italiano.");
        capture("cifre-risultati");
        Appearance.Set(AppAppearance.Dark, false); await settle(); capture("cifre-risultati-scuro"); Appearance.Set(AppAppearance.Light, false); await settle();
        if (sheet.EnvelopeSeries.Count > 0)
        {
            sheet.Documents.Show(EmbeddedWallDocuments.Envelopes); sheet.SelectedEnvelopeSeries = sheet.EnvelopeSeries.First(s => s.Quantity == "Moment"); await settle();
            var stations = Visual(view).OfType<DataGrid>().Single(g => g.IsVisible && ReferenceEquals(g.ItemsSource, sheet.EnvelopeStations));
            CheckNumbers(Cells(stations), ["Lato", "Caso min", "Fase min", "Caso max", "Fase max"], check, "tabella degli inviluppi");
            check(!ExponentText.IsMatch(sheet.EnvelopeExtrema), "Cifre: esponenti negli estremi degli inviluppi: " + sheet.EnvelopeExtrema);
            capture("cifre-inviluppi");
            Appearance.Set(AppAppearance.Dark, false); await settle(); capture("cifre-inviluppi-scuro"); Appearance.Set(AppAppearance.Light, false); await settle();
        }
        sheet.Documents.Show(EmbeddedWallDocuments.Diagrams); await settle();
        check(sheet.Data.ToJsonString() == before, "Cifre: la presentazione modifica i dati.");
    }

    /// <summary>In the section with the tie open in the editor: the box «Tipo» shows «Tirante» and the value stays «Anchor»; the names of the
    /// layers keep clear of the lines of the water.</summary>
    private static async Task CheckSectionTerms(FrameworkElement view, EmbeddedWallSheetViewModel sheet,
        Func<Task> settle, Action<bool, string> check, Action<string> capture)
    {
        await settle();
        var kind = Visual(view).OfType<ComboBox>().First(c => c.IsVisible && c.DataContext is FieldViewModel { Key: "kind" });
        check(kind.SelectedItem as string == "Anchor" && Visual(kind).OfType<TextBlock>().Any(t => t.IsVisible && t.Text == "Tirante")
            && !Visual(kind).OfType<TextBlock>().Any(t => t.IsVisible && t.Text == "Anchor"), "Cifre: la casella Tipo non mostra «Tirante».");
        var sketch = Visual(view).OfType<EmbeddedWallModelSketch>().Single();
        var names = sheet.Sketch!.Bands.Where(b => b.Table != "segments").Select(b => b.Name).ToHashSet();
        var layers = sketch.LabelPlacements.Where(l => names.Contains(l.Text)).ToArray();
        check(sketch.WaterLines.Count > 0 && layers.Length > 0 && layers.All(l => !sketch.WaterLines.Any(w => w.IntersectsWith(l.Bounds))),
            "Cifre: il nome di uno strato è tagliato dalla linea della falda.");
        capture("cifre-sezione-tipo");
    }
}
