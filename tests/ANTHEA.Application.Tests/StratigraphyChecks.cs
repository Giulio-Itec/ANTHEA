using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Application.Reporting;
using Anthea.Application.Stratigraphy;
using Anthea.Reports;
using X.Core;

internal static class StratigraphyChecks
{
    internal static int Run(string output)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        void Reject(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception("Input stratigrafico invalido accettato"); }
        var data = StratigraphyDocument.Create();
        StratigraphyDocument.Validate(data);
        Check(StratigraphyDocument.Warnings(data).Count == 2, "Dati mancanti e falda ignota devono essere segnalati");
        var row = StratigraphyDocument.Layers(data)[0]!.AsObject();
        Check(StratigraphyDocument.Parameters.All(p => row[p] is null), "Parametri inventati nel nuovo strato");
        row.Remove("soil_type"); Check(StratigraphyDocument.SoilType(row) == "Non classificato", "Retino legacy inventato");
        row["soil_type"] = "Invalido"; Reject(() => StratigraphyDocument.Validate(data));
        foreach (string soilType in StratigraphyDocument.SoilTypes)
        {
            row["soil_type"] = soilType; StratigraphyDocument.Validate(data);
            Check(StratigraphyDocument.Parameters.All(p => row[p] is null), "La famiglia ha assegnato parametri");
        }
        row["soil_type"] = "Sabbia";
        row["phi"] = 0; row["c"] = 0; row["cu"] = 0;
        StratigraphyDocument.Validate(data); checks++;
        row["thickness"] = "1,25"; Check(StratigraphyDocument.Number(row["thickness"], "h") == 1.25, "Virgola decimale");
        foreach (var invalid in new[] { "0", "-1", "NaN", "Infinity", "abc", "1e999" })
        { row["thickness"] = invalid; Reject(() => StratigraphyDocument.Validate(data)); }
        row["thickness"] = 2;
        var firstId = row["id"]!.ToString();
        StratigraphyDocument.Duplicate(data, 0);
        var second = StratigraphyDocument.Layers(data)[1]!.AsObject();
        Check(firstId != second["id"]!.ToString(), "Duplicazione senza nuovo id");
        second["name"] = "Altro"; Check(row["name"]!.ToString() != "Altro", "Duplicazione con dati condivisi");
        StratigraphyDocument.Move(data, 0, 1);
        Check(StratigraphyDocument.Layers(data)[1]!["id"]!.ToString() == firstId, "Riordino ha perso id");
        row["infinite"] = true; row["thickness"] = null;
        StratigraphyDocument.Validate(data); checks++;
        Reject(() => StratigraphyDocument.Move(data, 1, 0));
        Reject(() => StratigraphyDocument.Duplicate(data, 1));
        StratigraphyDocument.Add(data);
        Check(ReferenceEquals(StratigraphyDocument.Layers(data)[2], row), "Inserimento dopo semispazio");
        second["infinite"] = true; second["thickness"] = null;
        Reject(() => StratigraphyDocument.Validate(data)); second["infinite"] = false; second["thickness"] = 2;
        data["water_state"] = "Assegnata"; Reject(() => StratigraphyDocument.Validate(data));
        data["water_depth"] = -1; Reject(() => StratigraphyDocument.Validate(data));
        data["water_depth"] = 0; StratigraphyDocument.Validate(data); checks++;
        data["water_state"] = "Non considerata"; StratigraphyDocument.Validate(data); checks++;
        data["water_state"] = "Non nota";
        var snapshot = StratigraphyDocument.Snapshot(data);
        snapshot.Read()["name"] = "Modifica esterna";
        Check(data["name"]!.ToString() == "Stratigrafia", "Snapshot mutabile dall’esterno");
        Check(snapshot.Fingerprint == StratigraphyDocument.Snapshot(data).Fingerprint, "Impronta instabile");
        row["source"] = "Sondaggio S1";
        Check(snapshot.Fingerprint != StratigraphyDocument.Snapshot(data).Fingerprint, "Modifica non rilevata");
        var file = Path.Combine(output, "stratigrafia.anth");
        var document = DocumentService.CreateDocument(StratigraphyDocument.Module); document["dati"] = data.DeepClone();
        DocumentService.Save(file, document);
        Check(JsonNode.DeepEquals(document, DocumentService.Open(file)), "Round trip stratigrafia");
        var archive = ProjectDocuments.CreateArchive(); var project = ProjectDocuments.AddProject(archive);
        var sheet = ProjectDocuments.AddSheet(project, StratigraphyDocument.Module);
        sheet["dati"] = data.DeepClone();
        DocumentService.Save(file, archive);
        Check(JsonNode.DeepEquals(archive, DocumentService.Open(file)), "Round trip progetto con stratigrafia");
        Check(ModuleDirectory.All.Count(m => m.Id == StratigraphyDocument.Module) == 1, "Catalogo stratigrafia");
        Check(JsonNode.DeepEquals(data, ModuleServices.Get(StratigraphyDocument.Module).Prepare(data)), "Preparazione ha modificato input");
        Check(ModuleReportService.Supports(StratigraphyDocument.Module), "Report di progetto non supportato");
        var report = StratigraphyReport.Create("Stratigrafia di prova", data);
        var pdf = ReportFiles.Write(Path.Combine(output, "stratigrafia.docx"), DocxWriter.Write(report), "Stratigrafia di prova");
        Check(pdf.Pages > 0 && pdf.Bytes.Length > 1000, "PDF stratigrafia vuoto");
        var chapter = new ModuleReportService(new NoFigures()).BuildAsync(sheet, CancellationToken.None).GetAwaiter().GetResult();
        Check(chapter.Error is null && chapter.Document is not null, "Capitolo stratigrafia non disponibile");
        ReportProject.Write(Path.Combine(output, "progetto-stratigrafia.docx"), new ProjectReportPlan(project),
            new Dictionary<JsonObject, ReportProject.SheetContent> { [sheet] = chapter }, []);
        Check(File.Exists(Path.Combine(output, "progetto-stratigrafia.pdf")), "PDF progetto stratigrafia mancante");
        StratigraphyDocument.Remove(data, 1); StratigraphyDocument.Remove(data, 1);
        Reject(() => StratigraphyDocument.Remove(data, 0));
        row = StratigraphyDocument.Layers(data)[0]!.AsObject();
        row["phi"] = 90; Reject(() => StratigraphyDocument.Validate(data)); row["phi"] = null;
        row["gamma"] = 0; Reject(() => StratigraphyDocument.Validate(data)); row["gamma"] = 18;
        row["gamma_sat"] = 17;
        Check(StratigraphyDocument.Warnings(data).Any(s => s.Contains("minore")), "Avviso γsat < γ assente");
        StratigraphyDocument.Duplicate(data, 0);
        StratigraphyDocument.Layers(data)[1]!["id"] = row["id"]!.DeepClone(); Reject(() => StratigraphyDocument.Validate(data));
        return checks + StratigraphyAiChecks.Run(output) + StratigraphyTransferChecks.Run();
    }
    private sealed class NoFigures : IFigureRenderer
    {
        public Task<IReadOnlyList<ReportFigure>> RenderAsync(ModuleReportRequest request, CancellationToken cancellation)
            => throw new Exception("La scheda stratigrafica non deve richiedere calcoli o figure di altri moduli.");
    }
}
