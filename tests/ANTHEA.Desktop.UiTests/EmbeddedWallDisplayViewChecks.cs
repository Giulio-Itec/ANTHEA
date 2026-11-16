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

    /// <summary>After the approval of 16/11: in a table every cell of a numeric column has exactly the decimals of its unit (final zeros kept),
    /// and the numbers and their headers are aligned on the right.</summary>
    private static void CheckTable(DataGrid grid, IReadOnlyDictionary<string, int> decimals, Action<bool, string> check, string where)
    {
        string separator = Regex.Escape(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        foreach (var (header, places) in decimals)
        {
            var column = grid.Columns.Single(c => c.Header as string == header);
            var cells = Visual(grid).OfType<DataGridCell>().Where(c => ReferenceEquals(c.Column, column)).Select(c => c.Content as TextBlock).OfType<TextBlock>().ToArray();
            var pattern = new Regex("^-?[0-9]+" + separator + "[0-9]{" + places + "}$", RegexOptions.CultureInvariant);
            check(cells.Length > 0 && cells.All(t => pattern.IsMatch(t.Text) && t.TextAlignment == TextAlignment.Right),
                $"Cifre: {where}, colonna «{header}» senza {places} decimali fissi o non allineata a destra: " + string.Join(" | ", cells.Where(t => !pattern.IsMatch(t.Text)).Select(t => t.Text).Take(6)));
            var head = Visual(grid).OfType<System.Windows.Controls.Primitives.DataGridColumnHeader>().FirstOrDefault(h => ReferenceEquals(h.Column, column));
            check(head?.HorizontalContentAlignment == HorizontalAlignment.Right, $"Cifre: {where}, intestazione «{header}» non allineata a destra.");
        }
    }

    private static async Task CheckDisplayDigits(FrameworkElement view, EmbeddedWallSheetViewModel sheet,
        Func<Task> settle, Action<bool, string> check, Action<string> capture)
    {
        string before = sheet.Data.ToJsonString();
        sheet.Documents.Show(EmbeddedWallDocuments.Diagrams); await settle();
        var points = Visual(view).OfType<DataGrid>().Single(g => g.IsVisible && ReferenceEquals(g.ItemsSource, sheet.Points));
        var cells = Cells(points);
        CheckNumbers(cells, ["Lato nodo", "Sezione"], check, "tabella dei risultati");
        CheckTable(points, new Dictionary<string, int> { ["Quota [m]"] = 3, ["u [mm]"] = 2, ["M [kNm]"] = 2, ["p sx [kPa]"] = 2 }, check, "tabella dei risultati");
        check(cells.Where(c => c.Header == "Lato nodo").All(c => c.Text is "sotto" or "sopra" or "interno") && cells.Any(c => c.Header == "Lato nodo"),
            "Cifre: lato del nodo non in italiano.");
        capture("cifre2-risultati");
        Appearance.Set(AppAppearance.Dark, false); await settle(); capture("cifre2-risultati-scuro"); Appearance.Set(AppAppearance.Light, false); await settle();
        if (sheet.EnvelopeSeries.Count > 0)
        {
            sheet.Documents.Show(EmbeddedWallDocuments.Envelopes); sheet.SelectedEnvelopeSeries = sheet.EnvelopeSeries.First(s => s.Quantity == "Moment"); await settle();
            var stations = Visual(view).OfType<DataGrid>().Single(g => g.IsVisible && ReferenceEquals(g.ItemsSource, sheet.EnvelopeStations));
            CheckNumbers(Cells(stations), ["Lato", "Caso min", "Fase min", "Caso max", "Fase max"], check, "tabella degli inviluppi");
            CheckTable(stations, new Dictionary<string, int> { ["Quota [m]"] = 3, ["Minimo"] = 2, ["Massimo"] = 2 }, check, "tabella degli inviluppi");
            check(!ExponentText.IsMatch(sheet.EnvelopeExtrema), "Cifre: esponenti negli estremi degli inviluppi: " + sheet.EnvelopeExtrema);
            capture("cifre2-inviluppi");
            Appearance.Set(AppAppearance.Dark, false); await settle(); capture("cifre2-inviluppi-scuro"); Appearance.Set(AppAppearance.Light, false); await settle();
        }
        sheet.Documents.Show(EmbeddedWallDocuments.Diagrams); await settle();
        check(sheet.Data.ToJsonString() == before, "Cifre: la presentazione modifica i dati.");
    }

    /// <summary>
    /// The filtration (approved on 16/11): nodes with fixed decimals; velocities in m/s and permeabilities as a power of ten («1,0·10⁻⁵»), in
    /// the table of the flows and in the table «Dati e fasi» of the permeabilities (the document keeps 1E-05). Captures cifre2-filtrazione-*,
    /// cifre2-permeabilita in Chiara and Scuro.
    /// </summary>
    private static async Task CheckSeepageDigits(FrameworkElement view, EmbeddedWallSheetViewModel sheet,
        Func<Task> settle, Action<bool, string> check, Action<string> capture)
    {
        string before = sheet.Data.ToJsonString(), separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        sheet.Documents.Show(EmbeddedWallDocuments.Hydraulics); await settle();
        var tabs = Visual(view).OfType<TabControl>().Single(t => t.IsVisible && t.Items.OfType<TabItem>().Any(i => i.Header as string == "Flussi"));
        tabs.SelectedIndex = 0; await settle();
        var nodes = Visual(view).OfType<DataGrid>().Single(g => g.IsVisible && ReferenceEquals(g.ItemsSource, sheet.HydraulicNodes));
        CheckTable(nodes, new Dictionary<string, int> { ["x [m]"] = 3, ["z [m]"] = 3, ["h [m]"] = 3, ["u [kPa]"] = 2 }, check, "nodi della filtrazione");
        capture("cifre2-filtrazione-nodi");
        Appearance.Set(AppAppearance.Dark, false); await settle(); capture("cifre2-filtrazione-nodi-scuro"); Appearance.Set(AppAppearance.Light, false); await settle();
        tabs.SelectedIndex = 1; await settle();
        var flows = Visual(view).OfType<DataGrid>().Single(g => g.IsVisible && ReferenceEquals(g.ItemsSource, sheet.HydraulicElements));
        var power = new Regex("^(0|-?[0-9]" + Regex.Escape(separator) + "[0-9]·10⁻?[⁰¹²³⁴⁵⁶⁷⁸⁹]+|-?[0-9]" + Regex.Escape(separator) + "[0-9])$", RegexOptions.CultureInvariant);
        var velocities = Cells(flows).Where(c => c.Header is "qx [m/s]" or "qz [m/s]").Select(c => c.Text).ToArray();
        check(velocities.Length > 0 && velocities.All(power.IsMatch) && velocities.Any(t => t.Contains("·10")),
            "Cifre: velocità di filtrazione non come potenza di dieci: " + string.Join(" | ", velocities.Where(t => !power.IsMatch(t)).Take(6)));
        capture("cifre2-filtrazione-flussi");
        Appearance.Set(AppAppearance.Dark, false); await settle(); capture("cifre2-filtrazione-flussi-scuro"); Appearance.Set(AppAppearance.Light, false); await settle();
        tabs.SelectedIndex = 0;
        // The permeabilities of the data: 1E-05 in the document, «1,0·10⁻⁵» in the cells of «Dati e fasi».
        sheet.Documents.Show(EmbeddedWallDocuments.Tables); await settle();
        var tables = Visual(view).OfType<TabControl>().Single(t => t.ItemsSource is not null && t.IsVisible);
        tables.SelectedItem = sheet.Tables.Single(t => t.Key == "hydraulic_left"); await settle();
        var grid = Visual(view).OfType<DataGrid>().Single(g => g.Name == "RowsGrid" && g.IsVisible);
        var permeability = Cells(grid).Where(c => c.Header.StartsWith("Permeabilità Kxx", StringComparison.Ordinal)).Select(c => c.Text).ToArray();
        check(permeability.Length > 0 && permeability.All(t => t == "1" + separator + "0·10⁻⁵") && sheet.Tables.Single(t => t.Key == "hydraulic_left").Rows.Last()["kxx"] == "1E-05",
            "Cifre: permeabilità non mostrata come 1,0·10⁻⁵ o valore del documento cambiato: " + string.Join(" | ", permeability));
        capture("cifre2-permeabilita");
        Appearance.Set(AppAppearance.Dark, false); await settle(); capture("cifre2-permeabilita-scuro"); Appearance.Set(AppAppearance.Light, false); await settle();
        sheet.Documents.Show(EmbeddedWallDocuments.Hydraulics); await settle();
        check(sheet.Data.ToJsonString() == before, "Cifre: la presentazione della filtrazione modifica i dati.");
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
