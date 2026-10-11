using ANTHEA.ModelWorkspace;
using System.Text.Json;

namespace ANTHEA.ModelReports;

public enum ReportBlockKind { Figure, Text, Verification }

public sealed record ModelReportBlock
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public ReportBlockKind Kind { get; init; }
    public string Title { get; init; } = "";
    public string Text { get; init; } = "";
    public bool Included { get; init; } = true;
    public ModelViewPreset? View { get; init; }
    public string? SheetId { get; init; }
    public string? SheetName { get; init; }
}

/// <summary>A portable recipe, not a copy of engineering results or cached screenshots.</summary>
public sealed record ModelReportDocument
{
    public int Version { get; init; } = 1;
    public string Title { get; init; } = "Relazione del modello";
    public string Author { get; init; } = "";
    public string ModelName { get; init; } = "";
    public string SourceRevision { get; init; } = "";
    public int ImageWidth { get; init; } = 2400;
    public bool WhiteBackground { get; init; } = true;
    public ModelReportBlock[] Blocks { get; init; } = [];
}

public static class ModelReportFiles
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static void Validate(ModelReportDocument document)
    {
        if (document.Version != 1 || string.IsNullOrWhiteSpace(document.Title) || document.Title.Length > 160 ||
            document.Author is null || document.Author.Length > 200 || document.ModelName is null || document.ModelName.Length > 300 ||
            document.SourceRevision is null || document.SourceRevision.Length != 64 || document.SourceRevision.Any(c => !Uri.IsHexDigit(c)) ||
            document.ImageWidth is not (1600 or 2400 or 3840) || document.Blocks is null || document.Blocks.Length > 100 ||
            document.Blocks.Any(b => b is null) || document.Blocks.Select(b => b.Id).Distinct().Count() != document.Blocks.Length)
            throw new InvalidDataException("Il modello di report non è valido (massimo 100 capitoli).");
        foreach (var b in document.Blocks)
        {
            if (!Enum.IsDefined(b.Kind) || b.Id == Guid.Empty || string.IsNullOrWhiteSpace(b.Title) || b.Title.Length > 160 ||
                b.Text is null || b.Text.Length > 30000 || b.SheetId?.Length > 200 || b.SheetName?.Length > 300)
                throw new InvalidDataException("Titolo o testo di un capitolo non valido.");
            if (b.Kind == ReportBlockKind.Figure)
            {
                if (b.View?.Camera is null) throw new InvalidDataException("La figura non contiene una camera salvata.");
                ModelViewPresetStore.Validate([b.View]);
            }
            else if (b.View != null) throw new InvalidDataException("Solo le figure possono contenere una vista.");
        }
    }
    public static ModelReportDocument Read(string path)
    {
        if (new FileInfo(path).Length > 32_000_000) throw new InvalidDataException("Modello di report troppo grande.");
        var document = JsonSerializer.Deserialize<ModelReportDocument>(File.ReadAllText(path)) ?? throw new InvalidDataException("Report vuoto.");
        Validate(document); return document;
    }
    public static void Write(string path, ModelReportDocument document)
    {
        Validate(document);
        string json = JsonSerializer.Serialize(document, JsonOptions);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 32_000_000) throw new InvalidDataException("Modello di report troppo grande.");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        try { File.WriteAllText(temporary, json); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    /// <summary>Fail explicitly when references disappeared; never substitute another case or element.</summary>
    public static void ValidateReferences(ModelReportDocument document, ModelSnapshot snapshot)
    {
        Validate(document);
        var elements = snapshot.Elements.Select(e => e.Id).ToHashSet();
        var nodes = snapshot.Nodes.Select(n => n.Id).ToHashSet();
        foreach (var block in document.Blocks.Where(b => b.Included && b.Kind == ReportBlockKind.Figure))
        {
            var view = block.View!;
            if ((view.Visible ?? []).Concat(view.Selected).Any(id => !elements.Contains(id)) || view.SelectedNodes.Any(id => !nodes.Contains(id)))
                throw new InvalidDataException($"«{block.Title}»: alcuni oggetti salvati non esistono più. Rivedi la figura.");
            if (view.Mode == 2 && !snapshot.Results.Any(r => r.Name == view.Case))
                throw new InvalidDataException($"«{block.Title}»: caso o combinazione «{view.Case}» non disponibile.");
        }
    }
}

public sealed record ReportCategory(string Label, string Color, int Count);
public sealed record ReportFigure(string PngPath, string Description, IReadOnlyList<ReportCategory>? Categories = null);
public sealed record ReportProgress(int Completed, int Total, string Message)
{
    public double Percent => Total == 0 ? 0 : 100.0 * Completed / Total;
}
public sealed record ReportExportResult(string PdfPath, string RecipePath, string HtmlPath, string Revision, int Figures);
