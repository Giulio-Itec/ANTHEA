namespace X.Desktop;

/// <summary>
/// The open sheet as the historic <see cref="SheetEditor"/>, for the WPF suites that still reach its internals: module
/// workspaces (<c>materials</c>, <c>bridge</c>, <c>retainingWall</c>…) and the methods of the SheetEditor test partials.
/// The window knows only <see cref="ISheetEditor"/> (F5.2); these suites move with their module from F5.4 (proposal
/// 4.5) and fail here, with the module named, if the module already opens another editor.
/// </summary>
public sealed partial class MainWindow
{
    private SheetEditor? historicEditor => editor is null ? null
        : editor as SheetEditor ?? throw new InvalidOperationException("Il foglio aperto non usa SheetEditor: " + editor.Module);
}
