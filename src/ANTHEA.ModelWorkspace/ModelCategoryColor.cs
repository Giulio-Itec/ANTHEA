namespace ANTHEA.ModelWorkspace;

/// <summary>A graphical preference, never a material or section property.</summary>
public sealed record ModelCategoryColor(int Mode, string Key, string Color)
{
    public static bool IsColor(string? color) => color is { Length: 7 } && color[0] == '#' && color.Skip(1).All(Uri.IsHexDigit);

    public static void Validate(IReadOnlyList<ModelCategoryColor>? colors)
    {
        if (colors == null) return;
        if (colors.Count > 4096 || colors.Any(c => c == null || c.Mode is < 0 or > 4 || string.IsNullOrWhiteSpace(c.Key) || c.Key.Length > 128 || !IsColor(c.Color)) ||
            colors.Select(c => (c.Mode, c.Key)).Distinct().Count() != colors.Count)
            throw new InvalidDataException("Colori delle categorie non validi.");
    }
}
