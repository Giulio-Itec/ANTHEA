using System.Text.RegularExpressions;

namespace Anthea.Testing.Normalization;

/// <summary>
/// Stable placeholders for the GUIDs of one capture case: the n-th distinct GUID met becomes &lt;GUID-n&gt;.
/// The cases visit the input first and then the results, so the numbering depends on the document order, not on the random values.
/// </summary>
public sealed class GuidMap
{
    readonly Dictionary<string, string> map = new(StringComparer.Ordinal);
    public string Placeholder(string guid)
    {
        string key = guid.Replace("-", "").ToLowerInvariant();
        if (!map.TryGetValue(key, out var value)) map[key] = value = "<GUID-" + (map.Count + 1) + ">";
        return value;
    }
    public int Count => map.Count;
}

/// <summary>
/// Replaces the parts of a text that change from run to run without a change of behaviour: GUIDs, the dates of the day of the run
/// (± 1 day, with the time that follows them), durations in milliseconds and absolute paths. Constant dates of the texts (normative
/// references, method versions) are kept. Numbers are never touched here; their comparison is done by <see cref="Comparison.NumberText"/>.
/// </summary>
public sealed class TextNormalizer
{
    static readonly Regex Guid = new(@"\b[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}\b", RegexOptions.Compiled);
    static readonly Regex IsoDateTime = new(@"\b(?<y>\d{4})-(?<m>\d{2})-(?<d>\d{2})[T ]\d{2}:\d{2}(?::\d{2}(?:[.,]\d+)?)?(?:Z|[+-]\d{2}:?\d{2})?", RegexOptions.Compiled);
    static readonly Regex Date = new(@"\b(?:(?<d>\d{1,2})[/.](?<m>\d{1,2})[/.](?<y>\d{4})|(?<y>\d{4})-(?<m>\d{2})-(?<d>\d{2}))\b", RegexOptions.Compiled);
    static readonly Regex TimeAfterDate = new(@"<DATA>(\s+|T|\s*·\s*|\s*,\s*)\d{1,2}[:.]\d{2}(?:[:.]\d{2}(?:[.,]\d+)?)?\b", RegexOptions.Compiled);
    static readonly Regex Duration = new(@"\b\d+(?:[.,]\d+)?\s?(?:ms|millisecondi)\b", RegexOptions.Compiled);
    static readonly Regex WindowsPath = new(@"(?<![\p{L}\p{N}])[A-Za-z]:\\(?:[^\\/:*?""<>|\r\n\t ]+\\)*[^\\/:*?""<>|\r\n\t ]*", RegexOptions.Compiled);

    readonly (string Value, string Placeholder)[] roots;
    readonly DateOnly first, last;

    /// <param name="roots">Known folders (repository, output, temporary) replaced by name before the generic path pattern.</param>
    /// <param name="day">Day of the run (default today): its dates, and those of the day before and after, are replaced.</param>
    public TextNormalizer(IEnumerable<(string Path, string Placeholder)>? roots = null, DateOnly? day = null)
    {
        var reference = day ?? DateOnly.FromDateTime(DateTime.Now);
        first = reference.AddDays(-1); last = reference.AddDays(1);
        var list = new List<(string, string)>();
        foreach (var (path, placeholder) in roots ?? [])
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            foreach (var variant in Variants(path)) list.Add((variant, placeholder));
        }
        this.roots = list.OrderByDescending(r => r.Item1.Length).ToArray();
    }

    static IEnumerable<string> Variants(string path)
    {
        string full = System.IO.Path.GetFullPath(path).TrimEnd('\\', '/');
        yield return full;
        yield return full.Replace('\\', '/');
        string? longPath = null;
        try { longPath = new DirectoryInfo(full).FullName; } catch (IOException) { } catch (UnauthorizedAccessException) { }
        if (longPath is not null && longPath != full) { yield return longPath; yield return longPath.Replace('\\', '/'); }
    }

    public string Normalize(string text, GuidMap? guids = null)
    {
        if (text.Length == 0) return text;
        foreach (var (value, placeholder) in roots)
            if (text.Contains(value, StringComparison.OrdinalIgnoreCase)) text = text.Replace(value, placeholder, StringComparison.OrdinalIgnoreCase);
        text = Guid.Replace(text, m => guids?.Placeholder(m.Value) ?? "<GUID>");
        text = IsoDateTime.Replace(text, m => Recent(m) ? "<DATAORA>" : m.Value);
        text = Date.Replace(text, m => Recent(m) ? "<DATA>" : m.Value);
        text = TimeAfterDate.Replace(text, m => "<DATA>" + m.Groups[1].Value + "<ORA>");
        text = Duration.Replace(text, "<DURATA>");
        text = WindowsPath.Replace(text, "<PERCORSO>");
        return text;
    }

    bool Recent(Match m)
    {
        int y = int.Parse(m.Groups["y"].Value), mo = int.Parse(m.Groups["m"].Value), d = int.Parse(m.Groups["d"].Value);
        if (mo is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(Math.Clamp(y, 1, 9999), mo)) return false;
        var date = new DateOnly(Math.Clamp(y, 1, 9999), mo, d);
        return date >= first && date <= last;
    }

    /// <summary>GUIDs only: used for the property names of the JSON captures.</summary>
    public static string NormalizeKey(string key, GuidMap guids) => Guid.Replace(key, m => guids.Placeholder(m.Value));

    /// <summary>Registers the GUIDs of a text in order, without changing it.</summary>
    public static void Register(string text, GuidMap guids)
    {
        foreach (Match m in Guid.Matches(text)) guids.Placeholder(m.Value);
    }
}
