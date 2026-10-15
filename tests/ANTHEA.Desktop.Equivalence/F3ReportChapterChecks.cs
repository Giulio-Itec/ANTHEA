using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Text;
using Anthea.Testing.Normalization;
using Anthea.Application.Reporting;
using Anthea.Reports;
using X.Core;
namespace X.Desktop;

internal static class F3ReportChapterChecks
{
    internal static async Task Run(string root, string output)
    {
        Directory.CreateDirectory(output);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        int count = 0, images = 0;
        var builder = new ModuleReportService(new ModuleFigureRenderer());
        foreach (var (file, fixture) in Inputs(root))
        {
            string module = fixture.S("module");
            // Missing legacy row identities are created once, before the independent copies.
            if (module == "str_palo") SectionWorkspace.Prepare(fixture["data"]!.AsObject());
            // Initialize the revision timestamp once, before independent UI/report snapshots.
            if (module == BridgeConcept.Module) Anthea.Application.Modules.ModuleSheetPreparation.PrepareBridgeDesign(fixture["data"]!.AsObject());
            Check(ModuleReportService.Supports(module), "Modulo privo di capitolo autonomo: " + module);
            string title = "F3 capitolo " + fixture.S("id");
            var sheet = J.Obj(("nome", title), ("modulo_id", module), ("dati", fixture["data"]!.DeepClone()));
            string original = fixture.ToJsonString();
            using var editor = new SheetEditor(module, fixture["data"]!.AsObject());
            await editor.CalculateAsync(); editor.Commit();
            // U5 intentionally adds the curve to project reports. Compare against the existing
            // interactive curve command, not the previous report that never calculated it.
            if (module == "str_palo" && editor.HasResults) await editor.CalculateCurvatureForReportCheck();
            ReportProject.SheetContent? chapter = null; Exception? error = null;
            try { chapter = await builder.BuildAsync(sheet, default); } catch (ArgumentException ex) { error = ex; }
            Check(fixture.ToJsonString() == original, "Input originale modificato");
            Check(JsonNode.DeepEquals(editor.Data, sheet["dati"]), "Dati preparati diversi: " + file);
            if (module is "mat_calcestruzzo" or RebarMaterial.Module)
            {
                var expected = editor.MaterialReportContent();
                Check(error is null && expected.Error == chapter?.Error, "Esito del capitolo materiali diverso");
                Check(JsonNode.DeepEquals(System.Text.Json.JsonSerializer.SerializeToNode(expected.Derived), System.Text.Json.JsonSerializer.SerializeToNode(chapter?.Derived)), "Proprietà materiali diverse");
            }
            else if (!editor.HasResults)
                Check(error?.Message == "Dati incompleti o calcolo non riuscito. Aprire la scheda per correggere gli input.", "Capitolo incompleto non segnalato");
            else
            {
                Check(error is null && chapter?.Document is not null, "Capitolo assente: " + error);
                string? expectedError = editor.Result?["errori_calcolo"] is JsonObject errors && errors.Count > 0
                    ? string.Join("; ", errors.Select(e => e.Key + ": " + e.Value)) : null;
                Check(chapter!.Error == expectedError, "Errori parziali diversi: " + file);
                byte[] expected = editor.BuildReport(title, ModuleReportService.DefaultSections(module), true);
                byte[] actual = DocxWriter.Write(chapter!.Document!);
                if (module == "str_palo")
                {
                    byte[] curve = editor.BuildReport(title, ["curvatura", "grafici"], true);
                    using var curveZip = new ZipArchive(new MemoryStream(curve));
                    Check(curveZip.Entries.Count(p => p.FullName.StartsWith("word/media/")) == 1, "Figura M–χ assente");
                    File.WriteAllBytes(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + "-curvatura.docx"), curve);
                }
                File.WriteAllBytes(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + ".docx"), actual);
                File.WriteAllBytes(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + "-view.docx"), expected);
                using var a = new ZipArchive(new MemoryStream(actual)); using var e = new ZipArchive(new MemoryStream(expected));
                Check(a.Entries.Select(v => v.FullName).Order().SequenceEqual(e.Entries.Select(v => v.FullName).Order()), "Parti del documento diverse");
                foreach (var part in e.Entries)
                {
                    using var oldStream = part.Open(); using var newStream = a.GetEntry(part.FullName)!.Open();
                    using var oldBytes = new MemoryStream(); using var newBytes = new MemoryStream();
                    oldStream.CopyTo(oldBytes); newStream.CopyTo(newBytes);
                    var normalizer = new TextNormalizer();
                    string Normalize(byte[] bytes) => part.FullName == "docProps/core.xml"
                        ? normalizer.Normalize(Encoding.UTF8.GetString(bytes))
                        : normalizer.NormalizeReportTimestamps(Encoding.UTF8.GetString(bytes));
                    bool equal = part.FullName is "docProps/core.xml" or "word/document.xml"
                        ? Normalize(oldBytes.ToArray()) == Normalize(newBytes.ToArray())
                        : oldBytes.ToArray().SequenceEqual(newBytes.ToArray());
                    Check(equal, "Parte diversa " + file + ": " + part.FullName);
                    if (part.FullName.StartsWith("word/media/")) images++;
                }
            }
            count++;
        }
        await CheckProject(root, output, builder);
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {count} capitoli autonomi, {images} immagini e tutte le parti XML confrontate con la vista.");
    }
    private static async Task CheckProject(string root, string output, ModuleReportService builder)
    {
        var archive = ProjectDocuments.CreateArchive();
        var project = ProjectDocuments.AddProject(archive, "Progetto M–χ");
        var section = ProjectDocuments.AddSection(project, "Sezioni");
        var valid = ProjectDocuments.AddSheet(section, "str_palo"); valid["nome"] = "Percorso valido";
        valid["dati"] = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/str_palo--default.json")))!["data"]!.DeepClone();
        var settings = SectionWorkspace.Prepare(valid["dati"]!.AsObject()); settings["momento_curvatura"]!["passi"] = "10";
        var invalid = ProjectDocuments.AddSheet(section, "str_palo"); invalid["nome"] = "Percorso da correggere";
        invalid["dati"] = valid["dati"]!.DeepClone(); invalid["dati"]!["workspace_ca"]!["momento_curvatura"]!["passi"] = "9";
        string source = archive.ToJsonString(), path = Path.Combine(output, "progetto-curvature.docx");
        int incomplete = await ProjectReportService.WriteSnapshotAsync(ProjectReportService.Snapshot(section), path, builder);
        Check(incomplete == 1 && source == archive.ToJsonString(), "Avviso locale perso o progetto sorgente modificato");
        byte[] saved = File.ReadAllBytes(path);
        var xml = DocxWriter.Read(saved).GetXml("word/document.xml");
        System.Xml.Linq.XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        Check(xml.Descendants(w + "t").Count(v => v.Value == "Momento–curvatura · percorso assegnato") == 1, "Figura mancante o inventata nel progetto");
        Check(xml.Descendants(w + "t").Count(v => v.Value == "χ [1/m]") == 1 && xml.Root!.Value.Contains("Curva non disponibile:"),
            "Tabella o motivo di indisponibilità perso nella composizione");
        using var cancel = new CancellationTokenSource();
        try
        {
            await ProjectReportService.WriteSnapshotAsync(ProjectReportService.Snapshot(section), path, builder,
                text => { if (text.Contains("capitolo pronto")) cancel.Cancel(); }, cancel.Token);
            throw new InvalidOperationException("Annullamento del progetto ignorato");
        }
        catch (OperationCanceledException) { Check(saved.SequenceEqual(File.ReadAllBytes(path)), "Annullamento sostituisce il documento precedente"); }
    }
    private static IEnumerable<(string File, JsonObject Fixture)> Inputs(string root)
    {
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "*.json").Order(StringComparer.Ordinal))
            yield return (file, JsonNode.Parse(File.ReadAllText(file))!.AsObject());
        var wall = RetainingWall.Example("cantilever"); wall.Array("layers")[0]!["thickness"] = 25;
        RetainingWall.PrepareGlobalProfile(wall);
        var global = wall["global_stability"]!; global["enabled"] = true; global["profile_confirmed"] = true;
        global["search_mode"] = "Assegnata"; global["depth_min"] = .1;
        yield return ("wall-global.json", J.Obj(("id", "globale-confermata"), ("module", RetainingWall.Module), ("data", wall)));
    }
    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}

internal sealed partial class SheetEditor
{
    internal Task CalculateCurvatureForReportCheck() => concrete!.CalculateCurvatureForReportCheck();
}
internal sealed partial class ConcreteWorkspace
{
    internal async Task CalculateCurvatureForReportCheck()
    {
        await CalculateCurvature();
        if (curvatureResult is null) throw new InvalidOperationException("Curva della fixture non disponibile: " + curvatureText.Text);
    }
}
