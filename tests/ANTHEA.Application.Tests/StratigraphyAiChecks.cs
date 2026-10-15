using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Stratigraphy;

internal static class StratigraphyAiChecks
{
    internal static int Run(string output)
    {
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception("Bozza invalida accettata"); }
        var data = StratigraphyDocument.Create(); var row = StratigraphyDocument.Layers(data)[0]!.AsObject();
        row["gamma"] = 19; row["phi"] = 32; row["c"] = 0;
        Check(StratigraphyDocument.ForVerification(data, "gamma", "phi", "c").Keys.SequenceEqual(new[] { "Medi" }), "Medi di default");
        data["scenario"] = "Entrambi";
        Reject(() => StratigraphyDocument.ForVerification(data, "gamma", "phi", "c"));
        row["minimum"] = StratigraphyDocument.NewMinimum();
        var minimum = row["minimum"]!.AsObject(); minimum["gamma"] = 18; minimum["phi"] = 27; minimum["c"] = 0;
        var sets = StratigraphyDocument.ForVerification(data, "gamma", "phi", "c");
        Check(sets.Count == 2 && sets["Medi"]["layers"]![0]!["phi"]!.GetValue<int>() == 32 && sets["Minimi"]["layers"]![0]!["phi"]!.GetValue<int>() == 27, "Scenari distinti");
        sets["Minimi"]["layers"]![0]!["phi"] = 12;
        Check(minimum["phi"]!.GetValue<int>() == 27 && row["phi"]!.GetValue<int>() == 32, "Scenari condividono dati");
        data["scenario"] = "Minimi"; row["phi"] = null;
        Reject(() => StratigraphyDocument.ForVerification(data, "phi")); row["phi"] = 32;
        minimum["phi"] = 91; Reject(() => StratigraphyDocument.Validate(data)); minimum["phi"] = null;
        Reject(() => StratigraphyDocument.ForVerification(data, "phi"));
        Check(StratigraphyDocument.Warnings(data).Any(w => w.Contains("minimi selezionati")), "Minimi incompleti non segnalati");
        data["schema"] = 1; data.Remove("scenario"); row.Remove("minimum");
        StratigraphyDocument.Validate(data); Check(StratigraphyDocument.Scenario(data) == "Medi", "Compatibilità documento iniziale");

        JsonObject Values(double? phi) => new(StratigraphyDocument.Parameters.Select(p => new KeyValuePair<string, JsonNode?>(p,
            new JsonObject { ["value"] = p == "phi" ? phi : null, ["source"] = p == "phi" && phi is not null ? "p. 3 PDF, tabella S1: φ = " + phi : "" })));
        var inputLayer = new JsonObject { ["name"] = "Sabbia", ["description"] = "Sabbia limosa", ["soil_type"] = "Sabbia", ["thickness"] = 3,
            ["infinite"] = false, ["geometry_source"] = "p. 2 PDF: 0–3 m", ["mean"] = Values(32), ["minimum"] = Values(27), ["notes"] = "" };
        var profile = new JsonObject { ["name"] = "S1", ["description"] = "Sondaggio S1", ["ground_elevation"] = null,
            ["water_depth"] = null, ["source"] = "p. 2–3 PDF", ["notes"] = "Falda non indicata", ["layers"] = new JsonArray(inputLayer) };
        var payload = new JsonObject { ["warnings"] = "Controllare la relazione", ["profiles"] = new JsonArray(profile, profile.DeepClone()) };
        string Envelope() => new JsonObject { ["status"] = "completed", ["output"] = new JsonArray(new JsonObject { ["type"] = "message",
            ["content"] = new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = payload.ToJsonString() }) }) }.ToJsonString();
        var drafts = StratigraphyAiImporter.ParseResponse(Envelope(), "relazione.pdf", "hash", "test-model");
        var first = drafts[0]; var imported = StratigraphyDocument.Layers(first)[0]!.AsObject();
        Check(drafts.Count == 2 && first["id"]!.ToString() != drafts[1]["id"]!.ToString(), "Sondaggi fusi o id condivisi");
        Check(imported["phi"]!.GetValue<double>() == 32 && imported["minimum"]!["phi"]!.GetValue<double>() == 27, "Medi e minimi importati male");
        Check(imported["gamma"] is null && first["water_state"]!.ToString() == "Non nota", "Dati mancanti inventati");
        Check(StratigraphyDocument.SoilType(imported) == "Sabbia" && imported["description"]!.ToString() == "Sabbia limosa", "Famiglia o descrizione mista perse");
        Check(imported["source"]!.ToString().Contains("p. 3 PDF") && first["import_source"]!["reviewed"]!.GetValue<bool>() == false, "Fonte/revisione perse");
        first["scenario"] = "Entrambi"; first["import_source"]!["reviewed"] = true;
        var sheet = Anthea.Reports.StratigraphyReport.Create("Stratigrafia medi e minimi", first);
        string reportText = sheet.GetXml("word/document.xml").ToString();
        Check(reportText.Contains("Valori medi") && reportText.Contains("Valori minimi") && reportText.Contains("32") && reportText.Contains("27") && reportText.Contains("p. 3 PDF"), "Report perde scenari o fonti");
        Anthea.Reports.ReportFiles.Write(Path.Combine(output, "stratigrafia-medi-minimi.docx"), Anthea.Reports.DocxWriter.Write(sheet), "Stratigrafia medi e minimi");
        var saved = Anthea.Application.Documents.DocumentService.CreateDocument(StratigraphyDocument.Module); saved["dati"] = first.DeepClone();
        string savedPath = Path.Combine(output, "stratigrafia-ai.anth"); Anthea.Application.Documents.DocumentService.Save(savedPath, saved);
        Check(JsonNode.DeepEquals(saved, Anthea.Application.Documents.DocumentService.Open(savedPath)), "Round trip perde minimi o provenienza AI");
        inputLayer["minimum"] = Values(null);
        Check(StratigraphyDocument.Layers(StratigraphyAiImporter.ParseResponse(Envelope(), "f.pdf", "hash", "test")[0])[0]!["minimum"] is null, "Minimi vuoti creati");
        inputLayer["mean"]!["phi"]!["source"] = "";
        Reject(() => StratigraphyAiImporter.ParseResponse(Envelope(), "f.pdf", "hash", "test"));
        inputLayer["mean"] = Values(32);
        inputLayer["soil_type"] = "Inventato"; Reject(() => StratigraphyAiImporter.ParseResponse(Envelope(), "f.pdf", "hash", "test")); inputLayer["soil_type"] = "Sabbia";
        Reject(() => StratigraphyAiImporter.ParseResponse("{\"status\":\"incomplete\"}", "f.pdf", "hash", "test"));
        Reject(() => StratigraphyAiImporter.ParseResponse("{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"refusal\"}]}]}", "f.pdf", "hash", "test"));
        Reject(() => StratigraphyAiImporter.ParseResponse("{}", "f.pdf", "hash", "test"));
        var pdfPath = Path.Combine(output, "ai-fixture.pdf"); File.WriteAllText(pdfPath, "%PDF-1.4\nsynthetic transport fixture");
        var handler = new FakeHandler(Envelope()); using var http = new HttpClient(handler);
        var result = new StratigraphyAiImporter(http).ExtractAsync(pdfPath, "test-key-not-real", "test-model", CancellationToken.None).GetAwaiter().GetResult();
        Check(result.Count == 2 && handler.Calls == 1, "Trasporto importatore");
        Check(handler.Request!["store"]!.GetValue<bool>() == false && handler.Request["text"]!["format"]!["strict"]!.GetValue<bool>(), "Persistenza/struttura richiesta");
        Check(result[0]["import_source"]!["sha256"]!.ToString().Length == 64 && !result[0].ToJsonString().Contains("test-key"), "Impronta o segreto in documento");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        try { new StratigraphyAiImporter(http).ExtractAsync(pdfPath, "key", "model", canceled.Token).GetAwaiter().GetResult(); throw new Exception("Annullamento ignorato"); }
        catch (OperationCanceledException) { count++; }
        handler.Status = HttpStatusCode.Unauthorized;
        try { new StratigraphyAiImporter(http).ExtractAsync(pdfPath, "key", "model", CancellationToken.None).GetAwaiter().GetResult(); throw new Exception("Errore HTTP ignorato"); }
        catch (InvalidOperationException ex) { Check(ex.Message.Contains("401"), "Errore HTTP non spiegato"); }
        File.WriteAllText(pdfPath, "not a PDF");
        Reject(() => new StratigraphyAiImporter(http).ExtractAsync(pdfPath, "key", "model", CancellationToken.None).GetAwaiter().GetResult());
        return count;
    }
    private sealed class FakeHandler(string response) : HttpMessageHandler
    {
        internal int Calls;
        internal JsonObject? Request;
        internal HttpStatusCode Status = HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsoluteUri != "https://api.openai.com/v1/responses") throw new Exception("Destinazione inattesa");
            Calls++; Request = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
            return new HttpResponseMessage(Status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
