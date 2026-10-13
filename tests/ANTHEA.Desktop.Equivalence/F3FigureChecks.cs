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
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "*.json").Order(StringComparer.Ordinal))
        {
            var input = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            if (input.S("module") is not ("geo_palo_verticale" or "geo_micropalo_verticale")) continue;
            using var editor = new SheetEditor(input.S("module"), input["data"]!.AsObject());
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
            string name = Path.GetFileName(file), actual = JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(output, name), actual);
            if (!capture)
            {
                string expected = Path.Combine(root, "tests/fixtures/f3-figures", name);
                if (!File.Exists(expected) || File.ReadAllText(expected).Replace("\r\n", "\n") != actual.Replace("\r\n", "\n"))
                    throw new InvalidOperationException("Dati grafici diversi dal riferimento congelato: " + name);
            }
            count++;
        }
        if (count == 0) throw new InvalidOperationException("Nessuna fixture grafica trovata.");
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"{(capture ? "CAPTURE" : "PASS")} {count} fixture, tutte le serie, modalità e stratigrafie; confronto esatto.");
    }
    private static object SeriesState(Serie s) => new { s.Name, s.Points, Color = s.Color.ToString(), s.Dashed, s.Highlighted,
        Dashes = s.DashPattern?.Dashes.ToArray(), DashOffset = s.DashPattern?.Offset };
    private static object PlotState(Plot p) => new { p.Title, p.XLabel, p.YLabel, p.Note, p.InvertY, p.Capacity,
        p.CapacityDepth, p.XMinimum, p.XMaximum, p.XTickStep, p.EmptyMessage, p.AxisNumberFormat, p.ScaleX, p.ScaleY,
        Series = p.Series.Select(SeriesState).ToArray(),
        Markers = p.Markers.Select(m => new { m.X, m.Y, m.Label, Color = m.Color.ToString(), m.ProjectToAxes, m.XCaption, m.YCaption, m.Radius }).ToArray() };
}
