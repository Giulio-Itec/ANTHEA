namespace X.Desktop;

/// <summary>Implementation that opens the sheets of a module in the window (proposal F5, section 5.2).</summary>
internal enum SheetImplementation
{
    /// <summary>The historic view, <see cref="SheetEditor"/>.</summary>
    Historic,
    /// <summary>The ViewModel of the migrated module, hosted by <see cref="MvvmSheetEditor"/>.</summary>
    Mvvm
}

/// <summary>
/// Which implementation opens the sheets of each module (F5.2, proposal 5.2). <see cref="Declared"/> is the versioned choice of
/// the Release: one line per module of ModuleDirectory, today <see cref="SheetImplementation.Mvvm"/> for the horizontal pile group
/// (F5.4 checkpoint, 17/10/2026) and the new Spectra and EmbeddedWalls modules, which have no historic views. The other modules keep
/// <see cref="SheetImplementation.Historic"/>. An existing module passes to
/// <see cref="SheetImplementation.Mvvm"/> here only after its checkpoint (F5.4–F5.12), in the commit that retires its historic
/// view (Q11): a module MVVM in the Release has no historic view any more (archived in supporto/SUPERATI/ui-storica); this file is
/// the only point of contact between the parallel tracks of the migration (proposal, section 8).
/// Before that checkpoint the MVVM sheet of a module can be chosen only in the UiTests build, with the bench argument of the
/// WPF checks (tests/Desktop/ModuleHostBench.cs); never with an environment variable (as decision D3 for the fallback trace).
/// </summary>
internal sealed partial class ModuleImplementations
{
    /// <summary>The choice of the Release, in the order of ModuleDirectory. The UiTests check verifies that it lists exactly its modules.</summary>
    internal static IReadOnlyList<(string Module, SheetImplementation Implementation)> Declared { get; } =
    [
        ("geo_fondazioni_superficiali", SheetImplementation.Historic),
        ("geo_efficienza_orizzontale", SheetImplementation.Mvvm),
        ("geo_palo_verticale", SheetImplementation.Historic),
        ("geo_palo_orizzontale", SheetImplementation.Historic),
        ("geo_micropalo_verticale", SheetImplementation.Historic),
        ("geo_micropalo_orizzontale", SheetImplementation.Historic),
        ("geo_muri_sostegno", SheetImplementation.Historic),
        ("str_palo", SheetImplementation.Historic),
        ("str_mista_ponte", SheetImplementation.Mvvm),
        ("str_bridge_design", SheetImplementation.Mvvm),
        ("mat_calcestruzzo", SheetImplementation.Historic),
        ("mat_acciaio_armatura", SheetImplementation.Historic),
        ("geo_stratigrafia", SheetImplementation.Historic),
        ("act_spettri_ntc", SheetImplementation.Mvvm),
        ("geo_paratie", SheetImplementation.Mvvm),
    ];

    /// <summary>The implementations of the Release.</summary>
    internal static ModuleImplementations Release { get; } = new(Declared);

    private readonly IReadOnlyDictionary<string, SheetImplementation> choices;

    private ModuleImplementations(IEnumerable<(string Module, SheetImplementation Implementation)> table) =>
        choices = table.ToDictionary(entry => entry.Module, entry => entry.Implementation, StringComparer.Ordinal);

    /// <summary>Implementation of <paramref name="module"/>; a module not listed is historic, whose editor refuses it as today.</summary>
    internal SheetImplementation For(string module) => choices.GetValueOrDefault(module, SheetImplementation.Historic);

    /// <summary>Modules opened by their MVVM implementation, in ordinal order.</summary>
    internal IReadOnlyList<string> Mvvm => choices.Where(c => c.Value == SheetImplementation.Mvvm).Select(c => c.Key).Order(StringComparer.Ordinal).ToArray();
}
