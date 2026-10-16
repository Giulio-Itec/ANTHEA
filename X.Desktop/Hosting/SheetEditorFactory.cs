using System.Text.Json.Nodes;
using Anthea.Application.Modules;

namespace X.Desktop;

/// <summary>
/// Decides which editor opens a sheet (F5.2, proposal 5.2). Today every module opens the historic
/// <see cref="SheetEditor"/>. When a module migrates, the ModuleHost returns its MVVM editor here, and MainWindow and
/// the F3 captures, which open sheets only through this factory, follow without other changes.
/// </summary>
internal sealed class SheetEditorFactory
{
    /// <summary>Opens <paramref name="data"/> (copied, never modified) in the editor of <paramref name="module"/>.</summary>
    /// <param name="calculation">Calculation service of the module; null uses the registered one. The checks inject barriers here.</param>
    internal ISheetEditor Open(string module, JsonObject data, IModuleService? calculation = null) =>
        new SheetEditor(module, data, calculation);
}
