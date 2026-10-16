namespace Anthea.Presentation.Services;

/// <summary>
/// Open, save and folder dialogs (proposal F5, section 3.3). The ViewModels get only the chosen path, never a dialog;
/// null when the user cancels. Production: WpfFileDialogService (Microsoft.Win32 dialogs).
/// </summary>
public interface IFileDialogService
{
    /// <summary>Path of an existing file, or null.</summary>
    string? OpenFile(OpenFileRequest request);

    /// <summary>Path of the file to write, or null.</summary>
    string? SaveFile(SaveFileRequest request);

    /// <summary>Path of a folder, or null.</summary>
    string? ChooseFolder(string? title = null);
}

/// <summary>Request of an open dialog: filters in display order, proposed name and caption (null: the default of the dialog).</summary>
public sealed record OpenFileRequest(IReadOnlyList<FileFilter> Filters, string? FileName = null, string? Title = null);

/// <summary>Request of a save dialog; <paramref name="OverwritePrompt"/> false when the caller writes a folder next to the file.</summary>
public sealed record SaveFileRequest(IReadOnlyList<FileFilter> Filters, string FileName, string? Title = null, bool OverwritePrompt = true);

/// <summary>A typed filter: display name and patterns, for example ("Cartella Excel", "*.xlsx").</summary>
public sealed record FileFilter
{
    public FileFilter(string name, params string[] patterns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(patterns);
        if (name.Contains('|')) throw new ArgumentException("Nome del filtro con il separatore '|'.", nameof(name));
        if (patterns.Length == 0 || patterns.Any(p => string.IsNullOrWhiteSpace(p) || p.Contains('|') || p.Contains(';')))
            throw new ArgumentException("Filtro senza modelli validi: " + name, nameof(patterns));
        Name = name; Patterns = [.. patterns];
    }

    public string Name { get; }
    public IReadOnlyList<string> Patterns { get; }

    /// <summary>Filter string of the Windows dialogs: "Name|*.a;*.b|Other|*.c", the same text the views write today.</summary>
    public static string Join(IEnumerable<FileFilter> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        return string.Join("|", filters.Select(f => f.Name + "|" + string.Join(";", f.Patterns)));
    }

    public bool Equals(FileFilter? other) => other is not null && Name == other.Name && Patterns.SequenceEqual(other.Patterns);
    public override int GetHashCode() => HashCode.Combine(Name, string.Join(";", Patterns));
}
