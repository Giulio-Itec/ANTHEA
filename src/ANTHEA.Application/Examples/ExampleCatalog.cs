using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Application.Modules;

namespace Anthea.Application.Examples;

public sealed record ExampleEntry(string Path, string Module, string Title, string Description, string Kind, string Sha256, string Origin);

/// <summary>One catalog for the application and installer. Documents are read into owned working copies.</summary>
public sealed class ExampleCatalog
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    private sealed record CatalogFile(int Version, ExampleEntry[] Entries);
    private readonly string root;
    public IReadOnlyList<ExampleEntry> Entries { get; }

    private ExampleCatalog(string root, ExampleEntry[] entries)
    {
        this.root = root;
        Entries = Array.AsReadOnly(entries);
    }

    public static ExampleCatalog Load(string directory)
    {
        var root = System.IO.Path.GetFullPath(directory);
        if (!Directory.Exists(root)) return new(root, []);
        var file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(System.IO.Path.Combine(root, "catalogo.json")), Options)
            ?? throw new InvalidDataException("Catalogo esempi vuoto.");
        if (file.Version != 1 || file.Entries is null) throw new InvalidDataException("Versione del catalogo esempi non supportata.");
        var catalog = new ExampleCatalog(root, file.Entries);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in catalog.Entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.Title) || string.IsNullOrWhiteSpace(entry.Description) ||
                entry.Kind is not ("calcolo" or "dati" or "geometria") || string.IsNullOrWhiteSpace(entry.Origin) ||
                entry.Sha256 is null || entry.Sha256.Length != 64 || !entry.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("Voce del catalogo esempi non valida.");
            ModuleDirectory.Get(entry.Module);
            var path = catalog.Resolve(entry);
            if (!paths.Add(path)) throw new InvalidDataException("Esempio duplicato: " + entry.Path);
            if (!File.Exists(path)) throw new FileNotFoundException("Esempio mancante: " + entry.Title, path);
        }
        return catalog;
    }

    private string Resolve(ExampleEntry entry)
    {
        // Exactly one module directory and a portable filename; no traversal, drive or NSIS metacharacters.
        var parts = entry.Path?.Split('/');
        if (parts is not { Length: 2 } || parts[0] != entry.Module || !parts[1].EndsWith(".anthea", StringComparison.Ordinal) ||
            parts.Any(p => p.Length == 0 || p.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))) ||
            parts.Any(p => p.StartsWith('.')))
            throw new InvalidDataException("Percorso dell'esempio non valido: " + entry.Path);
        return System.IO.Path.Combine(root, parts[0], parts[1]);
    }

    public ExampleEntry? FindFile(string filename)
    {
        var path = System.IO.Path.GetFullPath(filename);
        return Entries.FirstOrDefault(e => string.Equals(Resolve(e), path, StringComparison.OrdinalIgnoreCase));
    }

    public JsonObject OpenCopy(ExampleEntry entry)
    {
        if (!Entries.Contains(entry)) throw new ArgumentException("Esempio estraneo al catalogo.", nameof(entry));
        var path = Resolve(entry);
        using (var stream = File.OpenRead(path))
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("L'esempio è stato modificato o danneggiato: " + entry.Title);
        var document = DocumentService.Open(path);
        if (document["tipo"]?.ToString() == "calcolo" && document["modulo_id"]?.ToString() != entry.Module)
            throw new InvalidDataException("Modulo incoerente nell'esempio: " + entry.Title);
        return document;
    }
}
