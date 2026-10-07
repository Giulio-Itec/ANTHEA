namespace X.Desktop.Services;

/// <summary>
/// Questions that a command asks the user before going on. The signatures contain no WPF type (no Window
/// parameter): the owner of the dialogs belongs to the implementation, so the interface can move unchanged to
/// the application layer (F3/F5). Production: WpfConfirmationService; WPF checks: ScriptedConfirmations.
/// </summary>
public interface IConfirmationService
{
    /// <summary>Shared data changed in a project sheet: true updates the linked sheets, false keeps the change in this sheet only.</summary>
    bool ConfirmSharedUpdate(SharedUpdatePrompt prompt);

    /// <summary>A move into another section changes common data of the moved sheets; true moves, false cancels.</summary>
    /// <param name="destination">Name of the destination section.</param>
    /// <param name="changes">One line per change: "sheet · field: before → after".</param>
    bool ConfirmProjectMove(string destination, IReadOnlyList<string> changes);

    /// <summary>The main window is closing: true closes, false keeps it open.</summary>
    /// <param name="saveOrDiscard">Commits the open sheet and asks whether to save the unsaved changes; false when the user cancels.</param>
    bool ConfirmClose(Func<bool> saveOrDiscard);
}

/// <summary>Content of the shared data question: changed groups, section and, for each linked sheet, the changes ("field: before → after").</summary>
public sealed record SharedUpdatePrompt(IReadOnlyList<string> ChangedGroups, string SectionName, IReadOnlyList<SharedUpdateTarget> Targets);

/// <summary>A linked sheet that the shared data question would update.</summary>
public sealed record SharedUpdateTarget(string SheetName, IReadOnlyList<string> Changes);
