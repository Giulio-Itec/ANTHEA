using System.Text.Json.Nodes;

namespace Anthea.Presentation.Services;

/// <summary>
/// The PDF documents installed with ANTHEA in the «Guide» folder beside the executable (supporto/installer/Guide.json and the two guides): the
/// article on the layered horizontal pile opened from «Diagrammi e dettagli › Riferimenti» and the links «guida:» of the Wiki (1.16.0).
/// Outside an installation (development build, checks) the name is looked up in the catalogue Guide.json of the repository that contains the
/// program, so the same reference opens the published source of the installed copy.
/// </summary>
public static class InstalledDocuments
{
    /// <summary>Scheme of the links of the Wiki and of the references to an installed document: «guida:» followed by the file name.</summary>
    public const string Scheme = "guida:";

    /// <summary>The installed file name of a «guida:» link, or null when the text is not such a link (no folders, only a PDF name).</summary>
    public static string? NameOf(string? link)
        => link is { } value && value.StartsWith(Scheme, StringComparison.Ordinal) && IsName(value[Scheme.Length..]) ? value[Scheme.Length..] : null;

    /// <summary>True for a plain PDF file name, without folders, drive or parent references.</summary>
    public static bool IsName(string? name)
        => !string.IsNullOrWhiteSpace(name) && name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && name.IndexOfAny(['/', '\\', ':']) < 0
           && !name.StartsWith('.') && name.Trim() == name && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>
    /// Full path of the document <paramref name="name"/>: «Guide» beside the program when installed, otherwise the source of the entry with that
    /// target in supporto/installer/Guide.json of the first parent folder that has it; null when the document is not available.
    /// </summary>
    public static string? Resolve(string name) => Resolve(name, AppContext.BaseDirectory);

    /// <summary><see cref="Resolve(string)"/> from the folder of the program <paramref name="programFolder"/>.</summary>
    public static string? Resolve(string name, string programFolder)
    {
        if (!IsName(name)) return null;
        string installed = Path.Combine(programFolder, "Guide", name);
        if (File.Exists(installed)) return installed;
        for (var folder = new DirectoryInfo(programFolder); folder is not null; folder = folder.Parent)
        {
            string catalogue = Path.Combine(folder.FullName, "supporto", "installer", "Guide.json");
            if (!File.Exists(catalogue)) continue;
            try
            {
                foreach (var entry in JsonNode.Parse(File.ReadAllText(catalogue))?.AsArray() ?? [])
                    if (entry?["target"]?.GetValue<string>() == name && entry["source"]?.GetValue<string>() is { Length: > 0 } source)
                    {
                        string path = Path.GetFullPath(Path.Combine(folder.FullName, source));
                        return File.Exists(path) ? path : null;
                    }
            }
            catch (System.Text.Json.JsonException) { }
            return null;
        }
        return null;
    }

    /// <summary>The absolute file URI of an installed document, or null when it is not available.</summary>
    public static Uri? Uri(string name) => Resolve(name) is { } path ? new Uri(path) : null;
}
