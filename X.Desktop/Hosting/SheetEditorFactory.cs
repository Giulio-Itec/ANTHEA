using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;

namespace X.Desktop;

/// <summary>
/// Decides which editor opens a sheet (F5.2, proposal 5.2), as <see cref="ModuleImplementations"/> says: the historic
/// SheetEditor (archived in F5.13 g: now refused, NoView) or the <see cref="MvvmSheetEditor"/> of the module migrated to MVVM, from the
/// <see cref="ModuleRegistry"/>. Created by the composition root (DesktopComposition) and passed to MainWindow; MainWindow and
/// the F3 captures open sheets only through it. In the Release the pile group is MVVM since F5.4; the new Spectra
/// and EmbeddedWalls modules start in MVVM, while the other modules keep the historic view.
/// </summary>
internal sealed class SheetEditorFactory
{
    private readonly ModuleImplementations implementations;
    private readonly ModuleRegistry modules;
    private readonly PresentationServices services;
    private readonly CalculationLimiter limiter;

    /// <exception cref="InvalidOperationException">A module chosen as MVVM has no MVVM implementation in <paramref name="modules"/>.</exception>
    internal SheetEditorFactory(ModuleImplementations implementations, ModuleRegistry modules, PresentationServices services, CalculationLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(implementations);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(limiter);
        var missing = implementations.Mvvm.Where(module => !modules.TryGetSheetModule(module, out _)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("Implementazione MVVM scelta per moduli che non ne hanno una: " + string.Join(", ", missing) + ".");
        this.implementations = implementations; this.modules = modules; this.services = services; this.limiter = limiter;
    }

    /// <summary>Opens <paramref name="data"/> (copied, never modified) in the editor of <paramref name="module"/>.</summary>
    /// <param name="calculation">Calculation service of the module; null uses the registered one. The checks inject barriers here.</param>
    internal ISheetEditor Open(string module, JsonObject data, IModuleService? calculation = null) =>
        implementations.For(module) == SheetImplementation.Mvvm
            ? new MvvmSheetEditor(modules.SheetModule(module), data, services, calculation ?? ModuleServices.Get(module), limiter)
            : throw NoView(module);

    /// <summary>
    /// A module without an MVVM view (F5.13 g): the refusal of the historic SheetEditor, archived in supporto/SUPERATI/ui-storica/shell, with its text; an
    /// unknown module fails in ModuleDirectory first, as before.
    /// </summary>
    internal static InvalidOperationException NoView(string module)
    {
        _ = ModuleDirectory.Get(module);
        return new InvalidOperationException("Vista storica non disponibile per " + module + ": il modulo si apre con la vista MVVM.");
    }
}
