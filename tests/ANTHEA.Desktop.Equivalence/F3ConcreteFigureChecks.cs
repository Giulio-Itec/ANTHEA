using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    internal Dictionary<string, object> FigureState()
    {
        var states = new Dictionary<string, object>();
        var panel = domainPanels.Single(p => !p.ThreeD);
        foreach (string key in new[] { "SLU", "SLV" })
        foreach (string filter in new[] { "Tutte", "Entro il dominio", "Fuori dominio", "Da controllare" })
        foreach (bool all in new[] { false, true })
        {
            panel.Options["stato"] = key; panel.Options["filtro"] = filter;
            panel.Options["tutte_rd"] = all; panel.Options["colora_eta"] = all;
            AttachDomainRows(panel); RefreshDomainPanel(panel, true);
            var p = panel.Plot;
            states[key + "/" + filter + "/" + all] = new
            {
                p.Title, p.XLabel, p.YLabel, p.Note, p.InvertY, p.CenteredAxes, p.ScaleX, p.ScaleY, p.FitIncludesMarkers, p.EmptyMessage,
                Segments = p.Segments.Select(s => new { s.A, s.B }).ToArray(),
                Lines = p.VerificationSegments.Select(s => new { s.A, s.B }).ToArray(),
                Markers = p.Markers.Select(m => new { m.X, m.Y, m.Label, Color = m.Color.ToString(), m.Radius }).ToArray()
            };
        }
        return states;
    }
    internal static async Task CheckF3ConcreteFigures(string root, string output, bool capture)
    {
        Directory.CreateDirectory(output); CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        int count = 0;
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "str_palo--*.json").Order(StringComparer.Ordinal))
        {
            var fixture = JsonNode.Parse(File.ReadAllText(file))!.AsObject(); var data = fixture["data"]!.AsObject();
            SectionWorkspace.Prepare(data);
            foreach (string key in SectionWorkspace.Sets)
                foreach (var (row, index) in data["combinazioni"]!.Array(key).OfType<JsonObject>().Select((r, i) => (r, i))) row["id"] = key + "-" + index;
            using var view = new ConcreteWorkspace(data); view.Commit(); await view.CalculateAllAsync(); view.Commit();
            string actual = JsonSerializer.Serialize(view.FigureState(), new JsonSerializerOptions { WriteIndented = true });
            string name = Path.GetFileName(file); File.WriteAllText(Path.Combine(output, name), actual);
            if (!capture)
            {
                string expected = Path.Combine(root, "tests/fixtures/f3-concrete-figures", name);
                if (!File.Exists(expected) || File.ReadAllText(expected).Replace("\r\n", "\n") != actual.Replace("\r\n", "\n"))
                    throw new InvalidOperationException("Dati grafici CA diversi: " + name);
            }
            count++;
        }
        // 4 fixtures before F5.10; 11 with the seven added in F5.10 (section fixture_aggiunte_f5_10 of the F3 manifest).
        if (count != 11) throw new InvalidOperationException("Corpus grafico CA incompleto");
        File.WriteAllText(Path.Combine(output, "esito.txt"), (capture ? "CAPTURE " : "PASS ") + count + " figure CA, filtri e punti resistenti esatti.");
    }
}
