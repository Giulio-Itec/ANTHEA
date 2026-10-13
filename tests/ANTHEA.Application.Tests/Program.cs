using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using X.Core;

int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
void Reject(Action action, string message)
{
    try { action(); } catch (ArgumentException) { checks++; return; }
    throw new InvalidOperationException(message);
}
try
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ANTHEA.sln"))) directory = directory.Parent;
    string root = directory?.FullName ?? throw new DirectoryNotFoundException("ANTHEA.sln non trovato.");
    string output = args.Length == 0 ? Path.Combine(root, "supporto/artefatti/refactoring/f3/application") : Path.GetFullPath(args[0]);
    Directory.CreateDirectory(output);
    string path = Path.Combine(output, "documento.anth");
    foreach (string fixturePath in Directory.GetFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "*.json"))
    {
        var fixture = JsonNode.Parse(File.ReadAllText(fixturePath))!.AsObject();
        var original = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"),
            ("modulo_id", fixture.S("module")), ("dati", fixture["data"]!.DeepClone()));
        string before = original.ToJsonString();
        File.WriteAllText(path, before);
        var opened = DocumentService.Open(path);
        Check(JsonNode.DeepEquals(original, opened), "Apertura ha migrato la fixture " + fixturePath);
        DocumentService.Save(path, opened);
        Check(before == opened.ToJsonString(), "Salvataggio ha mutato i dati in memoria");
        Check(JsonNode.DeepEquals(original, DocumentService.Open(path)), "Round trip fixture " + fixturePath);
    }
    var document = DocumentService.CreateDocument("geo_efficienza_orizzontale");
    DocumentService.Save(path, document);
    byte[] valid = File.ReadAllBytes(path);
    document["versione"] = 99;
    Reject(() => DocumentService.Save(path, document), "Versione sconosciuta accettata");
    Check(valid.SequenceEqual(File.ReadAllBytes(path)), "Validazione tardiva ha alterato il documento valido");
    Check(Directory.GetFiles(output, ".x_*.tmp").Length == 0, "File temporaneo rimasto");
    document["versione"] = 1;
    document["modulo_id"] = "modulo-non-installato";
    Reject(() => DocumentService.Save(path, document), "Modulo sconosciuto accettato");
    document["modulo_id"] = "geo_efficienza_orizzontale";
    document["formato"] = "ANTHEA";
    File.WriteAllText(path, document.ToJsonString());
    Check(DocumentService.Open(path).S("formato") == "X", "Alias storico del formato perso");

    var archive = ProjectDocuments.CreateArchive();
    var project = ProjectDocuments.AddProject(archive, "Prova");
    var section = ProjectDocuments.AddSection(project, "Fase");
    var sheet = ProjectDocuments.AddSheet(section, "geo_efficienza_orizzontale");
    var originalData = sheet["dati"]!.DeepClone();
    ProjectRevisions.NewRevision(archive, section, "Prima modifica");
    var entry = section.Array("revisioni")[0]!.AsObject();
    var snapshot = ProjectRevisions.Snapshot(archive, entry);
    sheet["dati"]!["diametro"] = 2.5;
    Check(JsonNode.DeepEquals(originalData, ProjectSharedData.SubtreeSheets(snapshot).Single()["dati"]), "Revisione mutata con il foglio corrente");
    string archiveBefore = archive.ToJsonString();
    DocumentService.Save(path, archive);
    Check(archive.ToJsonString() == archiveBefore, "Pack ha mutato archivio e revisioni originali");
    var reopened = DocumentService.Open(path);
    var reopenedSection = ProjectRevisions.Find(reopened, section.S("id"))!;
    var reloadedSnapshot = ProjectRevisions.Snapshot(reopened, reopenedSection.Array("revisioni")[0]!.AsObject());
    Check(JsonNode.DeepEquals(snapshot, reloadedSnapshot), "Contesto congelato alterato dal round trip");
    Check(ProjectRevisions.Find(reopened, sheet.S("id"))!["dati"].D("diametro") == 2.5, "Modifica corrente persa");
    var damaged = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    var pool = damaged["archivio_revisioni"]!.AsObject();
    pool.First().Value!["diametro"] = 123;
    File.WriteAllText(path, damaged.ToJsonString());
    Reject(() => DocumentService.Open(path), "Pool revisioni danneggiato accettato");

    byte[] template = SectionActionsExcel.Template();
    Check(SHA256.HashData(template).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,
        "src/ANTHEA.Application/Templates/ANTHEA_Sollecitazioni.xlsx")))), "Risorsa Excel diversa dal modello originale");
    SectionActionsExcel.Row[] rows = [new("SLU", "Combinazione firmata", -125.5, 34.75, -8, null, null),
        new("Taglio", "Torsione", 9, null, null, -22.25, 32.5, -2.75)];
    var imported = SectionActionsExcel.Read(SectionActionsExcel.Write(rows));
    Check(rows.SequenceEqual(imported.Rows), "Segni, valori o campi opzionali Excel alterati");
    Check(imported.FormulaCells == 0, "Export a valori contiene formule");
    checks += await ModuleServiceChecks.Run();
    checks += await ElasticPileSessionChecks.Run(root);
    checks += MaterialPresentationChecks.Run(root);
    checks += MaterialEditorChecks.Run();
    checks += BridgeDesignAnalysisChecks.Run();
    checks += await ConcreteBatchChecks.Run(root);
    checks += ConcretePresentationChecks.Run(root);
    checks += await ConcretePropertyChecks.Run();
    checks += ConcreteGeometryChecks.Run();
    checks += await ConcreteInspectionChecks.Run();
    checks += await WallParameterChecks.Run();
    checks += HorizontalPreviewChecks.Run(root);
    checks += HorizontalSoilChecks.Run();
    checks += VerticalPilePreviewChecks.Run();
    checks += await PileSegmentOperationChecks.Run(root);
    checks += await ProjectReportServiceChecks.Run(output);
    string result = $"PASS {checks} controlli Application, documenti e revisioni";
    File.WriteAllText(Path.Combine(output, "esito.txt"), result);
    Console.WriteLine(result);
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
