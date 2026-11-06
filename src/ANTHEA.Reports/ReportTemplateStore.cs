using System.Text.Json;

namespace Anthea.Reports;

/// <summary>A local, self-contained copy of the chosen styles; the original may be on a removable or network drive.</summary>
public sealed class ReportTemplateStore
{
    public static string UserPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ANTHEA", "report-template.json");
    private sealed record Saved(string Name, string Styles);
    private readonly string path;
    private ReportStyleTemplate? current;
    public string? Error { get; private set; }
    public ReportStyleTemplate? Current => Error is null ? current : throw new InvalidOperationException(Error + " Scegliere nuovamente il template o ripristinare lo stile ANTHEA dal menu Aspetto.");
    public string? Name => current?.Name;

    public ReportTemplateStore(string path)
    {
        this.path = path;
        try { current = Load(path); }
        catch (InvalidDataException error) { Error = error.Message; }
    }

    public void Select(string source)
    {
        var template = ReportStyleTemplate.Read(source);
        Save(new(template.Name, template.StylesXml));
        current = template; Error = null;
    }

    public void Reset() { Save(null); current = null; Error = null; }

    private void Save(Saved? saved)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        DocumentFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(saved));
    }

    private static ReportStyleTemplate? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(path));
            if (saved is not null && (string.IsNullOrWhiteSpace(saved.Name) || string.IsNullOrWhiteSpace(saved.Styles)))
                throw new InvalidDataException("Nome o stili del template salvato mancanti.");
            return saved is null ? null : ReportStyleTemplate.Restore(saved.Name, saved.Styles);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or System.Xml.XmlException or InvalidDataException)
        { throw new InvalidDataException("Impossibile leggere il template dei report salvato: " + path, error); }
    }
}
