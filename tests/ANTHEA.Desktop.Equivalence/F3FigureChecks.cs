using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

internal sealed partial class SheetEditor
{
    internal static async Task CheckF3Figures(string root, string output, bool capture = false)
    {
        Directory.CreateDirectory(output);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        int count = 0;
        // F5.6: the sheets open through the bench (--module-impl): with the MVVM view of the vertical piles the same states are read from
        // the Plot of VerticalPileSheetView, driven through its controls (MvvmFigureStates); the historic view stays the default.
        var editors = ModuleHostBench.SheetEditors();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "*.json").Order(StringComparer.Ordinal))
        {
            var input = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            if (input.S("module") is not ("geo_palo_verticale" or "geo_micropalo_verticale")) continue;
            using var opened = editors.Open(input.S("module"), input["data"]!.AsObject());
            if (opened is MvvmSheetEditor mvvm)
            {
                Compare(root, output, capture, Path.GetFileName(file), await MvvmFigureStates(mvvm));
                count++;
                continue;
            }
            var editor = (SheetEditor)opened;
            editor.Commit(); await editor.CalculateAsync(); editor.Commit();
            var states = new Dictionary<string, object>();
            states["hasResults"] = editor.HasResults;
            states["allSeries"] = editor.allSeries.Select(s => new { s.Key, Series = SeriesState(s.Series) }).ToArray();
            for (int i = 0; i < editor.capacityView.Items.Count; i++)
            {
                editor.capacityView.SelectedIndex = i;
                states["capacity-" + i] = PlotState(editor.plot);
            }
            for (int i = 0; i < editor.sondages.Items.Count; i++)
            {
                editor.sondages.SelectedIndex = i; editor.BuildReferencePlot(i);
                states["reference-" + i] = PlotState(editor.reference);
            }
            Compare(root, output, capture, Path.GetFileName(file), states);
            count++;
        }
        if (count == 0) throw new InvalidOperationException("Nessuna fixture grafica trovata.");
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"{(capture ? "CAPTURE" : "PASS")} {count} fixture, tutte le serie, modalità e stratigrafie; confronto esatto.");
    }
    private static void Compare(string root, string output, bool capture, string name, Dictionary<string, object> states)
    {
        string actual = JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(output, name), actual);
        if (capture) return;
        string expected = Path.Combine(root, "tests/fixtures/f3-figures", name);
        if (!File.Exists(expected) || File.ReadAllText(expected).Replace("\r\n", "\n") != actual.Replace("\r\n", "\n"))
            throw new InvalidOperationException("Dati grafici diversi dal riferimento congelato: " + name);
    }

    /// <summary>
    /// F5.6: the states of the frozen figures read from the MVVM view of a vertical pile sheet: every curve of the result (the plain series
    /// converted as the view draws them), the capacity chart of each view chosen in its combo box and the reference chart of each stratigraphy
    /// chosen in its tab, from the Plot of VerticalPileSheetView.
    /// </summary>
    private static async Task<Dictionary<string, object>> MvvmFigureStates(MvvmSheetEditor editor)
    {
        var root = editor.View;
        void Layout() { root.Measure(new System.Windows.Size(1400, 1000)); root.Arrange(new System.Windows.Rect(0, 0, 1400, 1000)); root.UpdateLayout(); }
        Layout();
        editor.Commit(); await editor.CalculateAsync(); editor.Commit();
        Layout();
        var sheet = (Anthea.Presentation.Modules.VerticalPile.VerticalPileSheetViewModel)editor.Sheet;
        var view = VerticalPileViewChecks.Visual(root).OfType<Views.VerticalPile.VerticalPileSheetView>().Single();
        var states = new Dictionary<string, object>();
        states["hasResults"] = editor.HasResults;
        states["allSeries"] = sheet.Curves.Select(c => new { c.Key, Series = SeriesState(PlotPresentation.Series(c.Series)) }).ToArray();
        for (int i = 0; i < view.CapacityView.Items.Count; i++)
        {
            view.CapacityView.SelectedIndex = i; Layout();
            states["capacity-" + i] = PlotState(view.CapacityPlot);
        }
        for (int i = 0; i < view.Surveys.Items.Count; i++)
        {
            view.Surveys.SelectedIndex = i; Layout();
            states["reference-" + i] = PlotState(view.ReferencePlot);
        }
        return states;
    }

    private static object SeriesState(Serie s) => new { s.Name, s.Points, Color = s.Color.ToString(), s.Dashed, s.Highlighted,
        Dashes = s.DashPattern?.Dashes.ToArray(), DashOffset = s.DashPattern?.Offset };
    private static object PlotState(Plot p) => new { p.Title, p.XLabel, p.YLabel, p.Note, p.InvertY, p.Capacity,
        p.CapacityDepth, p.XMinimum, p.XMaximum, p.XTickStep, p.EmptyMessage, p.AxisNumberFormat, p.ScaleX, p.ScaleY,
        Series = p.Series.Select(SeriesState).ToArray(),
        Markers = p.Markers.Select(m => new { m.X, m.Y, m.Label, Color = m.Color.ToString(), m.ProjectToAxes, m.XCaption, m.YCaption, m.Radius }).ToArray() };
}
