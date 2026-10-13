using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Concrete;
using Anthea.Application.Reporting;
using Anthea.Calculations;
using Anthea.Reports;
using X.Core;

internal static class ConcreteReportCurvatureChecks
{
    internal static async Task<int> Run(string root, string output)
    {
        int checks = 0;
        void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); checks++; }
        var original = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/str_palo--default.json")))!["data"]!.AsObject();
        foreach (var (family, rows) in original["combinazioni"]!.AsObject())
            foreach (var (row, index) in rows!.AsArray().Select((row, index) => (row!, index)))
                row["id"] = $"report-{family}-{index}";
        ConcreteSheetPreparation.PrepareReport(original);
        original["workspace_ca"]!["momento_curvatura"]!["passi"] = "10";
        var renderer = new RecordingRenderer();
        var builder = new ModuleReportService(renderer);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        JsonObject Sheet(JsonObject data) => J.Obj(("nome", "Verifica M–χ nel progetto"), ("modulo_id", "str_palo"), ("dati", data.DeepClone()));
        foreach (var (axial, angle) in new[] { ("0", "0"), ("-350", "27") })
        {
            var data = (JsonObject)original.DeepClone();
            var settings = data["workspace_ca"]!.AsObject(); var options = settings["momento_curvatura"]!.AsObject();
            options["N"] = axial; options["theta"] = angle;
            string before = data.ToJsonString();
            // Existing library-backed path is the numerical reference; no expected fixture is overwritten.
            var expected = ConcreteCurvatureAnalysis.Calculate(data["input"]!.AsObject(), settings, options);
            var chapter = await builder.BuildAsync(Sheet(data), default);
            var result = renderer.Request!.Calculation.Values;
            File.WriteAllText(Path.Combine(output, $"report-curvature-N{axial}.json"), result.ToJsonString());
            Check(result["momento_curvatura"] is JsonObject, "F3.4-U5: il report di progetto non calcola la curva M–χ");
            Check(JsonNode.DeepEquals(result["momento_curvatura"], J.Node(expected)), "Curva del report diversa dal servizio condiviso");
            Check(data.ToJsonString() == before, "Il report modifica gli input della scheda");
            Check(chapter.Error is null, "Report valido segnalato incompleto: " + chapter.Error);
            var xml = chapter.Document!.GetXml("word/document.xml");
            var table = xml.Descendants(w + "tbl").Single(t => t.Descendants(w + "t").Any(v => v.Value == "χ [1/m]"));
            var rows = table.Elements(w + "tr").Skip(1).ToArray();
            Check(rows.Length == expected.Points.Count && rows.Length >= 11, "Tabella M–χ incompleta");
            for (int i = 0; i < rows.Length; i++)
            {
                var p = expected.Points[i];
                var values = new[] { p.Moment, p.Curvature, p.Epsilon0, p.ConcreteCompressionStrain, p.SteelStrain };
                Check(rows[i].Elements(w + "tc").Select(c => string.Concat(c.Descendants(w + "t").Select(t => t.Value)))
                    .SequenceEqual(values.Select(v => EngineeringFormat.Number(v))), "Valori o unità M–χ alterati nel DOCX");
            }
            File.WriteAllBytes(Path.Combine(output, $"report-curvature-N{axial}.docx"), DocxWriter.Write(chapter.Document));
        }
        foreach (string failure in new[] { "passi", "trefoli", "N" })
        {
            var data = (JsonObject)original.DeepClone(); var settings = data["workspace_ca"]!.AsObject();
            if (failure == "trefoli") settings["trefoli"] = new JsonArray(J.Obj(("id", "T1"), ("x", "0"), ("y", "0"),
                ("area", "150"), ("sigma0", "1000"), ("Ep", "195000"), ("fpk", "1860"), ("fpyk", "1640"), ("eps_u", "35"), ("gamma_p", "1.15")));
            else settings["momento_curvatura"]![failure] = failure == "passi" ? "9" : "-1000000000000";
            var chapter = await builder.BuildAsync(Sheet(data), default);
            var result = renderer.Request!.Calculation.Values;
            Check(result["momento_curvatura"] is null && chapter.Error?.Contains("Momento–curvatura") == true,
                "Curva non applicabile non segnalata: " + failure);
            Check(result["domini"]!.AsObject().Count > 0 && chapter.Document is not null, "Errore curva elimina le altre verifiche");
            Check(chapter.Document!.GetXml("word/document.xml").Root!.Value.Contains("Curva non disponibile:"), "Motivo assente nel capitolo curva");
        }
        renderer.Request = null;
        try { await builder.BuildAsync(Sheet(original), new CancellationToken(true)); throw new Exception("Annullamento ignorato"); }
        catch (OperationCanceledException) { Check(renderer.Request is null, "Rendering avviato dopo annullamento"); }
        using var cancellation = new CancellationTokenSource();
        renderer.AfterCapture = cancellation.Cancel;
        try { await builder.BuildAsync(Sheet(original), cancellation.Token); throw new Exception("Annullamento tardivo ignorato"); }
        catch (OperationCanceledException) { Check(renderer.Request is not null, "Annullamento tardivo non esercitato"); }
        Console.WriteLine($"M–χ report progetto: {checks} controlli, due percorsi, tre rifiuti e annullamento");
        return checks;
    }
    private sealed class RecordingRenderer : IFigureRenderer
    {
        internal ModuleReportRequest? Request;
        internal Action? AfterCapture;
        public Task<IReadOnlyList<ReportFigure>> RenderAsync(ModuleReportRequest request, CancellationToken cancellation)
        { Request = request; AfterCapture?.Invoke(); return Task.FromResult<IReadOnlyList<ReportFigure>>([]); }
    }
}
