using System.IO;
using System.Text.Json.Nodes;
using Anthea.Application.Actions;

namespace X.Desktop;

/// <summary>Absolute contrast check of the new module: no inherited defects or DPI-dependent legacy baseline.</summary>
internal static class SpectraContrastChecks
{
    internal static async Task Run(string output)
    {
        string view = "modulo-" + SpectraDocument.Module;
        await ContrastAudit.Run([output, "--viste", "^" + view + "$"]);
        var report = JsonNode.Parse(File.ReadAllText(Path.Combine(output, "report.json")))!.AsObject();
        var captures = report["catture"]!.AsArray().OfType<JsonObject>().ToArray();
        foreach (string theme in new[] { "Light", "Dark", "VeryDark" })
            for (int phase = 1; phase <= 4; phase++)
                if (!captures.Any(c => c["modalita"]?.GetValue<string>() == theme
                    && c["vista"]?.GetValue<string>() == view + "-dati-fase-" + phase))
                    throw new InvalidOperationException($"Contrasto Spettri: cattura assente per {theme}, fase {phase}");
        int defects = report["difetti"]!.AsArray().Count;
        if (defects != 0) throw new InvalidOperationException($"Contrasto Spettri: {defects} difetti, vedere report.json");
        if (report["violazioni_fuori_schermo"]!.AsArray().Count != 0)
            throw new InvalidOperationException("Contrasto Spettri: una finestra della prova ha occupato il desktop");
        File.WriteAllText(Path.Combine(output, "esito.txt"),
            $"PASS contrasto Spettri: quattro fasi in tre temi, {captures.Length} catture, zero difetti; DPI {report["ambiente"]!["dpi_finestra"]}");
    }
}
