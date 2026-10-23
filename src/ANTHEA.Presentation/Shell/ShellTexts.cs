using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Calculations;
using X.Core;

namespace Anthea.Presentation.Shell;

/// <summary>
/// Texts of the shell derived from the documents (F5.13 b), the same of MainWindow: title of the window, heading of a sheet, names
/// of modules and sheets. Pure functions: the historic window and the new shell can both call them.
/// </summary>
public static class ShellTexts
{
    /// <summary>Name of a document without a file (MainWindow.UpdateTitle).</summary>
    public const string NewDocument = "Nuovo documento";

    /// <summary>Title of the window (MainWindow.UpdateTitle): file name or «Nuovo documento», « *» when not saved, and the revision on screen.</summary>
    /// <param name="path">Path of the document, null for a new document.</param>
    /// <param name="dirty">True when the document has unsaved changes.</param>
    /// <param name="displayedRevision">Number of the archived revision on screen (read-only), null for the current version.</param>
    public static string WindowTitle(string? path, bool dirty, int? displayedRevision) =>
        "ANTHEA — " + (path is null ? NewDocument : System.IO.Path.GetFileName(path)) + (dirty ? " *" : "") +
        (displayedRevision is int revision ? $" · Rev. {revision} · sola lettura" : "");

    /// <summary>Name of a module (MainWindow.ModuleName); an unknown module fails as ModuleDirectory.Get.</summary>
    public static string ModuleName(string module) => ModuleDirectory.Get(module).Name;

    /// <summary>Name of a sheet: its «nome», otherwise the name of its module (the label of the tree and of the soil sources).</summary>
    public static string SheetName(JsonObject sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return sheet.S("nome", ModuleName(sheet.S("modulo_id")));
    }

    /// <summary>
    /// Heading of a sheet (MainWindow.SheetHeading): its name; with an archived revision on screen « · Rev. n · sola lettura»;
    /// otherwise « · Rev. n» of the nearest section above it with a revision; nothing more outside a revised section.
    /// </summary>
    public static string SheetHeading(JsonObject sheet, int? displayedRevision)
    {
        string title = SheetName(sheet);
        if (displayedRevision is int selected) return title + " · Rev. " + selected + " · sola lettura";
        for (var owner = sheet.Parent?.Parent as JsonObject; owner is not null; owner = owner.Parent?.Parent as JsonObject)
            if (owner["revisione"] is JsonObject revision) return title + " · Rev. " + RevisionNumber(revision);
        return title;
    }

    /// <summary>Common data named in the notice of a reloaded tab before «e altre n proprietà» (as the status line of the shared data).</summary>
    public const int ReloadNoticeFields = 5;

    /// <summary>
    /// The notice of a tab reloaded because another sheet confirmed shared data (SH9, F5.13 c, night of 22-23/10; proposal 4.4, point 3: which data
    /// changed and from which sheet): «Aggiornato da «foglio»: campo, campo e altre n proprietà.», or «Aggiornato da «foglio».» when no common datum
    /// of the sheet itself changed (the data of a linked sheet below it, for example).
    /// </summary>
    public static string ReloadNotice(string source, IReadOnlyList<string> fields)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(fields);
        string text = "Aggiornato da «" + source + "»";
        if (fields.Count == 0) return text + ".";
        return text + ": " + string.Join(", ", fields.Take(ReloadNoticeFields)) +
            (fields.Count > ReloadNoticeFields ? $" e altre {fields.Count - ReloadNoticeFields} proprietà" : "") + ".";
    }

    /// <summary>Number of a revision entry («numero»), 0 when missing or not a number: the reading of MainWindow, (int)D("numero").</summary>
    public static int RevisionNumber(JsonObject? revision) => (int)(J.Number(revision?["numero"]) ?? 0);

    /// <summary>Date of a revision as the window shows it (MainWindow.RevisionDate): dd/MM/yyyy HH:mm when readable, otherwise the text.</summary>
    public static string RevisionDate(JsonObject revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        return DateTime.TryParse(revision.S("data"), out var time) ? time.ToString("dd/MM/yyyy HH:mm") : revision.S("data");
    }
}
