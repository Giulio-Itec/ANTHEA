using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

/// <summary>
/// F5.13 g (UiTests): the observable values of the historic window (MainWindow) recorded before its archiving, in tests/fixtures/f5-shell
/// (README.md there): the capture of the project of ui/shell-capture (cattura-mainwindow/, the 14 files of the folder mainwindow/ at 67eafba5),
/// the data of the examples of the Wiki (MainWindow.WikiExamples) and the parts of the Word report of a section (MainWindow.GenerateSectionReport)
/// in finestra-storica.json. The checks of the new shell compare with them; the references are never regenerated to make a check pass.
/// </summary>
internal static class ShellReferences
{
    /// <summary>The repository root: the first folder above the program that contains ANTHEA.sln.</summary>
    internal static string Root
    {
        get
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
                if (File.Exists(Path.Combine(folder.FullName, "ANTHEA.sln"))) return folder.FullName;
            throw new DirectoryNotFoundException("Radice del repository (ANTHEA.sln) non trovata sopra " + AppContext.BaseDirectory);
        }
    }

    /// <summary>tests/fixtures/f5-shell.</summary>
    internal static string Folder => Path.Combine(Root, "tests", "fixtures", "f5-shell");

    /// <summary>The capture of the project through MainWindow (ShellProjectCapture.Steps, the saved file and esito.txt).</summary>
    internal static string Capture => Path.Combine(Folder, "cattura-mainwindow");

    /// <summary>finestra-storica.json: «esempi_wiki» (example → data) and «report_sezione» (parts of the Word report, sheets without results).</summary>
    internal static JsonObject Window => JsonNode.Parse(File.ReadAllText(Path.Combine(Folder, "finestra-storica.json")))!.AsObject();

    /// <summary>
    /// The report of a section as MainWindow wrote it (ProjectReport.cs at 67eafba5, report code frozen on 8/10): SectionReportSnapshot, that is
    /// ProjectReportService.Snapshot, then GenerateSectionReport, that is ProjectReportService.WriteSnapshotAsync with the chapters of ModuleReportService and
    /// the figures of the desktop (ModuleFigureRenderer). The sheets with missing results.
    /// </summary>
    internal static Task<int> SectionReport(JsonObject section, string path) =>
        Anthea.Application.Reporting.ProjectReportService.WriteSnapshotAsync(Anthea.Application.Reporting.ProjectReportService.Snapshot(section), path,
            new Anthea.Application.Reporting.ModuleReportService(new ModuleFigureRenderer()), _ => { }, CancellationToken.None);

    /// <summary>The parts of a Word report, in ordinal order, and the sheets without results: the shape of «report_sezione».</summary>
    internal static JsonObject ReportShape(string docx, int unavailable)
    {
        using var zip = ZipFile.OpenRead(docx);
        return J.Obj(("schede_senza_risultati", unavailable),
            ("parti", new JsonArray(zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).Select(n => (JsonNode)JsonValue.Create(n)!).ToArray())));
    }

    /// <summary>Indented JSON, UTF-8 without BOM, for the values written next to the outputs of the checks.</summary>
    internal static void WriteJson(string path, JsonNode value) =>
        File.WriteAllText(path, value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));

    /// <summary>
    /// Two folders file by file, byte for byte: a line «UGUALE name» or «DIVERSO name» for every file of either, into <paramref name="lines"/>. True when
    /// both have the same files, all equal.
    /// </summary>
    internal static bool SameFolders(string a, string b, List<string> lines)
    {
        bool same = true;
        var names = Directory.GetFiles(a).Select(Path.GetFileName).Concat(Directory.GetFiles(b).Select(Path.GetFileName)).Distinct().Order(StringComparer.Ordinal);
        foreach (var name in names)
        {
            string left = Path.Combine(a, name!), right = Path.Combine(b, name!);
            bool equal = File.Exists(left) && File.Exists(right) && File.ReadAllBytes(left).AsSpan().SequenceEqual(File.ReadAllBytes(right));
            same &= equal;
            lines.Add((equal ? "UGUALE " : "DIVERSO ") + name);
        }
        return same;
    }
}
