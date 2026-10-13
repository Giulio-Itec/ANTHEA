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
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "*.json").Order(StringComparer.Ordinal))
        {
            var fixture = JsonNode.Parse(File.ReadAllText(file))!.AsObject(); string module = fixture.S("module");
            if (!ModuleReportService.Supports(module)) continue;
            string title = "F3 capitolo " + fixture.S("id");
            var sheet = J.Obj(("nome", title), ("modulo_id", module), ("dati", fixture["data"]!.DeepClone()));
            string original = fixture.ToJsonString();
            using var editor = new SheetEditor(module, fixture["data"]!.AsObject());
            await editor.CalculateAsync(); editor.Commit();
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
                byte[] expected = editor.BuildReport(title, ModuleReportService.DefaultSections(module), true);
                byte[] actual = DocxWriter.Write(chapter!.Document!);
                File.WriteAllBytes(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + ".docx"), actual);
                using var a = new ZipArchive(new MemoryStream(actual)); using var e = new ZipArchive(new MemoryStream(expected));
                Check(a.Entries.Select(v => v.FullName).Order().SequenceEqual(e.Entries.Select(v => v.FullName).Order()), "Parti del documento diverse");
                foreach (var part in e.Entries)
                {
                    using var oldStream = part.Open(); using var newStream = a.GetEntry(part.FullName)!.Open();
                    using var oldBytes = new MemoryStream(); using var newBytes = new MemoryStream();
                    oldStream.CopyTo(oldBytes); newStream.CopyTo(newBytes);
                    bool equal = part.FullName == "docProps/core.xml"
                        ? new TextNormalizer().Normalize(Encoding.UTF8.GetString(oldBytes.ToArray())) == new TextNormalizer().Normalize(Encoding.UTF8.GetString(newBytes.ToArray()))
                        : oldBytes.ToArray().SequenceEqual(newBytes.ToArray());
                    Check(equal, "Parte diversa " + file + ": " + part.FullName);
                    if (part.FullName.StartsWith("word/media/")) images++;
                }
            }
            count++;
        }
        File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {count} capitoli autonomi, {images} immagini e tutte le parti XML confrontate con la vista.");
    }
    private static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
