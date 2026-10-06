using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

namespace Anthea.Testing.Capture;

/// <summary>One case of the corpus: module, identifier, origin, source file (relative to the repository) and data of the sheet.</summary>
public sealed record CaptureCase(string Module, string Id, string Origin, string? SourceFile, JsonObject? Data, string? Error = null)
{
    public string Key => Module + "/" + Id;
}

/// <summary>
/// The B0 corpus: the defaults of the 11 modules of ModuleCatalog.All (and the elastic view of the two horizontal modules), the Wiki
/// examples (X.Desktop/Wiki/Examples, applied as MainWindow.WikiExamples does), the documents of supporto/esempi read with
/// Archivio.Leggi, and RetainingWall.Example("gravity"/"cantilever").
/// </summary>
public static class Corpus
{
    public static readonly string[] DocumentExtensions = [".json", ".anthea", ".programma"];

    public static List<CaptureCase> Build(string root)
    {
        var cases = new List<CaptureCase>();
        foreach (var module in ModuleCatalog.All)
            cases.Add(Safe(module.Id, "default", "default del catalogo (ModuleCatalog.CreateData)", null, () => ModuleCatalog.CreateData(module.Id)));
        // The elastic response of the horizontal sheets (HorizontalWorkspace: vista_orizzontale = "elastico" after PrepareShared).
        foreach (var module in new[] { PaloOrizzontale.Module, MicropaloOrizzontale.Module })
            cases.Add(Safe(module, "default-elastico", "default con la vista elastica (PrepareShared, vista_orizzontale = elastico)", null, () =>
            {
                var data = ModuleCatalog.CreateData(module); ElasticHorizontalPile.PrepareShared(data); data["vista_orizzontale"] = "elastico"; return data;
            }));

        string wiki = Path.Combine(root, "X.Desktop", "Wiki", "Examples");
        if (Directory.Exists(wiki))
            foreach (var file in Directory.EnumerateFiles(wiki, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                JsonObject definition;
                try { definition = JsonNode.Parse(File.ReadAllText(file, Encoding.UTF8))!.AsObject(); }
                catch (Exception ex) { cases.Add(new("sconosciuto", "wiki-" + Id(Path.GetFileNameWithoutExtension(file)), "esempio Wiki", Relative(root, file), null, ex.Message)); continue; }
                string module = definition["moduleId"]?.GetValue<string>() ?? "sconosciuto";
                cases.Add(Safe(module, "wiki-" + Id(Path.GetFileNameWithoutExtension(file)), "esempio Wiki applicato ai default (MainWindow.WikiExamples)", Relative(root, file), () =>
                {
                    var data = ModuleCatalog.CreateData(module);
                    Apply(data, definition["overrides"]!.AsObject());
                    ModuleCatalog.ValidateData(module, data);
                    return data;
                }));
            }

        string examples = Path.Combine(root, "supporto", "esempi");
        if (Directory.Exists(examples))
            foreach (var file in Directory.EnumerateFiles(examples, "*", SearchOption.AllDirectories)
                .Where(f => DocumentExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f, StringComparer.Ordinal))
            {
                string relative = Relative(root, file), id = "esempio-" + Id(Path.ChangeExtension(Path.GetRelativePath(examples, file), null)!);
                JsonObject document;
                JsonFallbackTrace.Context.Value = "corpus/" + id;
                try { document = Archivio.Leggi(file); }
                catch (Exception ex) { cases.Add(new("sconosciuto", id, "documento di supporto/esempi", relative, null, ex.GetType().Name + ": " + ex.Message)); continue; }
                finally { JsonFallbackTrace.Context.Value = null; }
                var sheets = Sheets(document).ToList();
                for (int i = 0; i < sheets.Count; i++)
                {
                    var (module, data) = sheets[i];
                    cases.Add(new(module, sheets.Count == 1 ? id : id + "-foglio-" + (i + 1), "documento di supporto/esempi letto con Archivio.Leggi", relative, data));
                }
            }

        // Vertical piles and micropiles: the defaults are empty forms, so the numerical coverage comes from the regression cases
        // of supporto/test/casi_confronto.json (types palo and micropalo, with independent expected values checked by X.Verifiche).
        string regression = Path.Combine(root, "supporto", "test", "casi_confronto.json");
        if (File.Exists(regression))
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in JsonNode.Parse(File.ReadAllText(regression, Encoding.UTF8))!.AsArray().OfType<JsonObject>())
            {
                string kind = item["tipo"]?.GetValue<string>() ?? "";
                if (kind is not ("palo" or "micropalo") || item["input"] is not JsonObject input) continue;
                string id = "regressione-" + Id(item["nome"]?.GetValue<string>() ?? kind);
                for (int n = 2; !ids.Add(id); n++) id = "regressione-" + Id(item["nome"]?.GetValue<string>() ?? kind) + "-" + n;
                cases.Add(new(kind == "palo" ? "geo_palo_verticale" : "geo_micropalo_verticale", id, "caso della regressione (casi_confronto.json, tipo " + kind + ")",
                    Relative(root, regression), (JsonObject)input.DeepClone()));
            }
        }

        foreach (var family in new[] { "gravity", "cantilever" })
            cases.Add(Safe(RetainingWall.Module, "example-" + family, $"RetainingWall.Example(\"{family}\")", null, () => RetainingWall.Example(family)));
        return cases;
    }

    static CaptureCase Safe(string module, string id, string origin, string? source, Func<JsonObject> data)
    {
        JsonFallbackTrace.Context.Value = "corpus/" + module + "/" + id;
        try { return new(module, id, origin, source, data()); }
        catch (Exception ex) { return new(module, id, origin, source, null, ex.GetType().Name + ": " + ex.Message); }
        finally { JsonFallbackTrace.Context.Value = null; }
    }

    /// <summary>Same merge as MainWindow.WikiExamples.Create: objects merged, the combination families replaced as a whole.</summary>
    static void Apply(JsonObject target, JsonObject values)
    {
        foreach (var (key, value) in values)
        {
            if (key != "combinazioni" && value is JsonObject child && target[key] is JsonObject existing) Apply(existing, child);
            else target[key] = value?.DeepClone();
        }
    }

    /// <summary>The sheets of a document: the sheet of a 'calcolo' document, or every sheet with data of a 'progetti' document.</summary>
    static IEnumerable<(string Module, JsonObject Data)> Sheets(JsonObject document)
    {
        if (document["tipo"]?.GetValue<string>() == "calcolo")
        {
            if (document["dati"] is JsonObject data) yield return (document["modulo_id"]!.GetValue<string>(), (JsonObject)data.DeepClone());
            yield break;
        }
        var found = new List<(string, JsonObject)>();
        void Walk(JsonNode? node)
        {
            if (node is JsonArray array) { foreach (var item in array) Walk(item); return; }
            if (node is not JsonObject o) return;
            if (o["modulo_id"] is JsonValue m && o["dati"] is JsonObject d) found.Add((m.GetValue<string>(), (JsonObject)d.DeepClone()));
            foreach (var key in new[] { "fogli", "strutture" }) Walk(o[key]);
        }
        Walk(document["progetti"]);
        foreach (var item in found) yield return item;
    }

    public static string Id(string text)
    {
        var builder = new StringBuilder();
        foreach (char c in text.Replace('\\', '-').Replace('/', '-')) builder.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? char.ToLowerInvariant(c) : '-');
        return builder.ToString().Trim('-');
    }

    public static string Relative(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');

    static readonly Dictionary<string, string> hashes = new(StringComparer.OrdinalIgnoreCase);
    public static string Sha256(string file)
    {
        string path = Path.GetFullPath(file);
        if (!hashes.TryGetValue(path, out var hash)) hashes[path] = hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        return hash;
    }
}
