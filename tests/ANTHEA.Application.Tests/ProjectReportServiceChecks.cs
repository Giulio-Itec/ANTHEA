using System.IO.Compression;
using System.Text.Json.Nodes;
using Anthea.Application.Reporting;
using Anthea.Calculations;
using X.Core;

internal static class ProjectReportServiceChecks
{
    public static async Task<int> Run(string output)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        var archive = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(archive, "Progetto");
        var section = ProjectDocuments.AddSection(project, "Ramo");
        var first = ProjectDocuments.AddSheet(section, RebarMaterial.Module); first["nome"] = "Primo";
        var second = ProjectDocuments.AddSheet(section, RebarMaterial.Module); second["nome"] = "Incompleto";
        string before = archive.ToJsonString();
        var snapshot = ProjectReportService.Snapshot(section);
        Check(ProjectSharedData.Ancestors(snapshot).Any(), "Snapshot perde l’ascendenza del ramo");
        var builder = new TestChapters();
        string path = Path.Combine(output, "report-servizio.docx");
        int missing = await ProjectReportService.WriteSnapshotAsync(snapshot, path, builder);
        Check(missing == 1 && builder.Visited.SequenceEqual(new[] { "Primo", "Incompleto" }), "Ordine o capitoli incompleti persi");
        Check(before == archive.ToJsonString(), "Preparazione capitoli modifica il progetto sorgente");
        Check(ProjectSharedData.SubtreeSheets(snapshot).All(s => s["dati"].B("report_prepared")), "Dati preparati non conservati nello snapshot");
        using (var package = ZipFile.OpenRead(path))
        using (var reader = new StreamReader(package.GetEntry("word/document.xml")!.Open()))
        {
            string text = reader.ReadToEnd();
            Check(text.Contains("Primo") && text.Contains("Errore di prova"), "Manca il capitolo o l’avviso nel report senza UI");
        }
        byte[] valid = File.ReadAllBytes(path);
        using var cancel = new CancellationTokenSource();
        builder.AfterFirst = cancel.Cancel;
        try { await ProjectReportService.WriteSnapshotAsync(ProjectReportService.Snapshot(section), path, builder, cancellation: cancel.Token); throw new Exception("Annullamento ignorato"); }
        catch (OperationCanceledException) { Check(valid.SequenceEqual(File.ReadAllBytes(path)), "Annullamento sostituisce il report precedente"); }
        builder.AfterFirst = null; builder.IoFailure = true;
        try { await ProjectReportService.WriteSnapshotAsync(ProjectReportService.Snapshot(section), path, builder); throw new Exception("Errore IO nascosto"); }
        catch (IOException) { Check(valid.SequenceEqual(File.ReadAllBytes(path)), "Errore IO sostituisce il report precedente"); }
        return checks;
    }
    private sealed class TestChapters : IProjectReportChapterBuilder
    {
        internal readonly List<string> Visited = [];
        internal Action? AfterFirst;
        internal bool IoFailure;
        public Task<ReportProject.SheetContent> BuildAsync(JsonObject sheet, CancellationToken cancellation)
        {
            if (IoFailure) throw new IOException("Errore IO di prova");
            Visited.Add(sheet.S("nome")); sheet["dati"]!["report_prepared"] = true;
            if (sheet.S("nome") == "Incompleto") throw new ArgumentException("Errore di prova");
            AfterFirst?.Invoke();
            return Task.FromResult(Materiali.MaterialReportChapter.Create(RebarMaterial.Module, sheet["dati"]!.AsObject()));
        }
    }
}
