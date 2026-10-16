using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace X.Desktop;

/// <summary>
/// Outcome of --check-contrast in the stage ui of build/ci.ps1 (F5.3, proposal F5 sections 6.1 and 7.3), from report.json of the
/// run: 0 defects in Scuro and Molto scuro; the defects of Chiara, which F5 does not change (Q6), within the frozen baseline
/// tests/fixtures/f5/contrasto-chiara.json. A defect is identified by its view and by the key of ContrastAudit (element type, ANTHEA
/// components, owner, original colour and value source of text and background: no texts, positions or source lines), with the
/// number of occurrences. A key that is new or has more occurrences fails; fewer occurrences pass and are reported, so that the
/// baseline is lowered in a commit of its own (it may only go down). With --viste only the views of the filter are compared.
/// </summary>
internal static class ContrastBaseline
{
    internal const string Frozen = "tests/fixtures/f5/contrasto-chiara.json";

    internal sealed record Entry(string View, string Key, int Count);

    internal static void Check(string output, string baselineFile, string? views)
    {
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "report.json")))!.AsObject();
        var defects = report["difetti"]!.AsArray().OfType<JsonObject>().ToList();
        int dark = defects.Count(d => d["modalita"]?.GetValue<string>() == "Dark"), veryDark = defects.Count(d => d["modalita"]?.GetValue<string>() == "VeryDark");
        var current = Entries(defects.Where(d => d["modalita"]?.GetValue<string>() == "Light"));
        var dpi = report["ambiente"]?["dpi_finestra"]?.GetValue<double>() ?? 0;
        File.WriteAllText(Path.Combine(output, "baseline-chiara.json"), Serialize(current, dpi, report["comando"]?.GetValue<string>() ?? ""), new UTF8Encoding(false));

        var problems = new List<string>(); var notes = new List<string>();
        if (dark > 0) problems.Add($"{dark} difetti in Scuro");
        if (veryDark > 0) problems.Add($"{veryDark} difetti in Molto scuro");
        int frozenTotal = 0;
        if (!File.Exists(baselineFile)) problems.Add("baseline della Chiara assente: " + baselineFile + " (candidata: baseline-chiara.json)");
        else
        {
            var frozen = JsonNode.Parse(File.ReadAllText(baselineFile))!.AsObject();
            var filter = views is null ? null : new Regex(views, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var known = frozen["voci"]!.AsArray().OfType<JsonObject>()
                .Select(v => new Entry(v["vista"]!.GetValue<string>(), v["chiave"]!.GetValue<string>(), v["occorrenze"]!.GetValue<int>()))
                .Where(v => filter is null || filter.IsMatch(v.View)).ToDictionary(v => (v.View, v.Key));
            frozenTotal = known.Values.Sum(v => v.Count);
            double frozenDpi = frozen["dpi"]?.GetValue<double>() ?? 0;
            if (frozenDpi != dpi) notes.Add($"DPI della finestra {dpi} invece di {frozenDpi} della baseline: le misure dai pixel possono cambiare");
            foreach (var entry in current)
            {
                int allowed = known.TryGetValue((entry.View, entry.Key), out var k) ? k.Count : 0;
                if (entry.Count > allowed) problems.Add($"Chiara, {entry.View}: {entry.Count} occorrenze invece di {allowed} · {entry.Key}");
            }
            var seen = current.ToDictionary(e => (e.View, e.Key), e => e.Count);
            int fewer = known.Values.Sum(k => Math.Max(0, k.Count - seen.GetValueOrDefault((k.View, k.Key))));
            if (fewer > 0) notes.Add($"{fewer} occorrenze della Chiara in meno della baseline: abbassarla con baseline-chiara.json in un commit dedicato");
        }
        int light = current.Sum(e => e.Count);
        string summary = $"Scuro {dark}, Molto scuro {veryDark}; Chiara {light} difetti in {current.Count} voci (baseline {frozenTotal})";
        var lines = new List<string> { (problems.Count == 0 ? "PASS contrasto: " : "FAIL contrasto: ") + summary };
        lines.AddRange(problems.Take(40).Select(p => "- " + p));
        if (problems.Count > 40) lines.Add($"- … altri {problems.Count - 40}");
        lines.AddRange(notes.Select(n => "nota: " + n));
        File.WriteAllLines(Path.Combine(output, "esito.txt"), lines);
        if (problems.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, lines));
    }

    internal static List<Entry> Entries(IEnumerable<JsonObject> defects) => defects
        .GroupBy(d => (View: d["vista"]!.GetValue<string>(), Key: d["chiave"]!.GetValue<string>()))
        .Select(g => new Entry(g.Key.View, g.Key.Key, g.Count()))
        .OrderBy(e => e.View, StringComparer.Ordinal).ThenBy(e => e.Key, StringComparer.Ordinal).ToList();

    internal static string Serialize(List<Entry> entries, double dpi, string command)
    {
        var document = new JsonObject
        {
            ["nota"] = "Difetti di contrasto della sola Chiara, lasciati per non cambiarla (decisioni.md, «Tema scuro»; F5 Q6): baseline di --check-contrast " +
                "nello stadio ui (tests/Desktop/ContrastBaseline.cs). Può solo scendere: si sostituisce con baseline-chiara.json di una corsa con meno difetti.",
            ["strumento"] = "supporto/test/Desktop/ContrastAudit.cs --baseline", ["comando"] = command, ["dpi"] = dpi,
            ["totale"] = entries.Sum(e => e.Count), ["voci_distinte"] = entries.Count,
            ["voci"] = new JsonArray(entries.Select(e => (JsonNode?)new JsonObject { ["vista"] = e.View, ["chiave"] = e.Key, ["occorrenze"] = e.Count }).ToArray())
        };
        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }).ReplaceLineEndings("\r\n") + "\r\n";
    }
}
