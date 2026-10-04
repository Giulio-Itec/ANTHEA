using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace X.Desktop;

internal sealed record WikiArticle(string Id, string Type, string Area, string Title, string Summary,
    string Source, long Offset, int Length, int Order, int ReadingTime, string[] Keywords,
    string[] Related, string[] Modules, string? Example, string[] Sections,
    string Key = "", string ChapterId = "", string Level = "intermediate", string Status = "existing",
    string[]? Prerequisites = null, string[]? References = null);

internal sealed record WikiChapter(string Id, int Number, string Title, string Description, string Introduction);
internal sealed record WikiReference(string Id, string Title, string Url, string Kind);

/// <summary>Metadata only in memory; chapter bodies are read on demand from the two canonical manuals.</summary>
internal static class WikiCatalog
{
    private static readonly Assembly Assembly = typeof(WikiCatalog).Assembly;
    internal static Stream Resource(string name) => Assembly.GetManifestResourceStream("Wiki." + name)
        ?? throw new InvalidOperationException("Contenuto Wiki mancante: " + name);
    internal static readonly WikiArticle[] Articles = Read<WikiArticle[]>("index.json");
    internal static readonly WikiChapter[] Chapters = Read<WikiChapter[]>("chapters.json");
    internal static readonly WikiReference[] References = Read<WikiReference[]>("references.json");
    internal static readonly Dictionary<string, string> Aliases = Read<Dictionary<string, string>>("aliases.json");
    internal static WikiChapter Chapter(WikiArticle a) => Chapters.Single(c => c.Id == a.ChapterId);
    internal static WikiArticle[] InChapter(string id) => Articles.Where(a => a.ChapterId == id && a.Status != "historical").OrderBy(a => a.Order).ToArray();
    private static readonly Lazy<Dictionary<string, int[]>> SearchIndex = new(() => Read<Dictionary<string, int[]>>("search.json"));
    private static readonly Lazy<Dictionary<string, string>> Assets = new(() => Read<Dictionary<string, string>>("assets.json"));
    internal static string? AssetFor(string path) => Assets.Value.GetValueOrDefault(path);
    private static T Read<T>(string name) { using var stream = Resource(name); return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!; }
    internal static string Body(WikiArticle article)
    {
        using var stream = Resource(article.Source);
        stream.Seek(article.Offset, SeekOrigin.Begin);
        var bytes = new byte[article.Length]; stream.ReadExactly(bytes);
        return Encoding.UTF8.GetString(bytes);
    }
    internal static string Normalize(string value)
    {
        value = value.ToLowerInvariant();
        foreach (var (symbol, name) in new[] { ("σ", "sigma"), ("τ", "tau"), ("π", "pi"), ("λ", "lambda"), ("γ", "gamma"), ("κ", "kappa"), ("θ", "theta"), ("δ", "delta"), ("ρ", "rho") }) value = value.Replace(symbol, name);
        return new(value.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }
    internal static string Slug(string value) => Regex.Replace(Normalize(value), "[^a-z0-9]+", "-").Trim('-');
    internal static WikiArticle[] Search(string query)
    {
        var words = Regex.Matches(Normalize(query), "[a-z0-9]+").Select(m => m.Value).Distinct().ToArray();
        if (words.Length == 0) return [];
        HashSet<int>? ids = null;
        foreach (var word in words)
        {
            var found = SearchIndex.Value.Where(p => p.Key.StartsWith(word, StringComparison.Ordinal))
                .SelectMany(p => p.Value).ToHashSet();
            if (ids is null) ids = found; else ids.IntersectWith(found);
        }
        return ids!.OrderByDescending(i => words.Count(w => SearchIndex.Value.GetValueOrDefault(w)?.Contains(i) == true))
            .ThenByDescending(i => words.Count(w => Normalize(Articles[i].Title).Contains(w)))
            .ThenBy(i => Articles[i].Order).Take(60).Select(i => Articles[i]).ToArray();
    }
    internal static WikiArticle? ForModule(string module) => Articles.FirstOrDefault(a => a.Type == "guide" && a.Modules.Contains(module));
    internal static WikiArticle? Resolve(string uri)
    {
        var key = CanonicalUri(uri).Split('#')[0].TrimEnd('/');
        return Articles.FirstOrDefault(a => a.Id == key || a.Key == key);
    }
    internal static string CanonicalUri(string uri)
    {
        if (uri.StartsWith("wiki:")) uri = uri[5..];
        if (Aliases.TryGetValue(uri, out var full)) return full;
        var parts = uri.Split('#', 2); var path = parts[0].TrimEnd('/');
        return Aliases.GetValueOrDefault(path, path) + (parts.Length == 2 ? "#" + parts[1] : "");
    }
}

internal sealed class WikiProgress
{
    internal sealed record Entry(string Section, double Fraction, string[] Read, DateTime Visited);
    private readonly string? file;
    internal Dictionary<string, Entry> Entries { get; private set; } = [];
    internal WikiProgress(string? storage = null, bool persist = true)
    {
        file = persist ? storage ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ANTHEA", "wiki-progress.json") : null;
        try { if (file is not null && File.Exists(file)) Entries = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(file)) ?? []; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
    }
    internal void Update(string id, string section, double fraction, bool read = false)
    {
        var visited = Entries.GetValueOrDefault(id);
        var sections = (visited?.Read ?? []).ToHashSet(); if (read) sections.Add(section);
        Entries[id] = new(section, Math.Clamp(double.IsFinite(fraction) ? fraction : 0, 0, 1), sections.ToArray(), DateTime.UtcNow);
    }
    internal void Save()
    {
        if (file is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file + ".new", JsonSerializer.Serialize(Entries)); File.Move(file + ".new", file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
