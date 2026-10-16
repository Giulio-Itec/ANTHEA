using Anthea.Application.Modules;
using Anthea.Wiki;

namespace Anthea.Presentation.Modules;

/// <summary>
/// What the interface knows of a module (proposal F5, section 5.1). The identity is the <see cref="ModuleInfo"/> of
/// <see cref="ModuleDirectory"/>: there is no second catalogue, and <see cref="ModuleRegistry"/> gives one descriptor for
/// every module of the directory, migrated or not.
/// </summary>
public interface IModuleDescriptor
{
    /// <summary>Identity, area, name and description, as <see cref="ModuleDirectory"/> lists them.</summary>
    ModuleInfo Info { get; }

    /// <summary>Guide of the module in the Wiki.</summary>
    ModuleHelp Help { get; }

    /// <summary>Keys of interface state that the sheets of the module save in their data.</summary>
    ModulePersistence Persistence { get; }

    /// <summary>What the reports of the module offer.</summary>
    ModuleReportCapabilities Reports { get; }
}

/// <summary>
/// A module migrated to MVVM (proposal 5.1): it creates the ViewModel of a sheet. The view is found by the type of the
/// ViewModel (a DataTemplate in X.Desktop/Views, decision Q3); the host is the window that opens the sheet.
/// </summary>
public interface ISheetModule : IModuleDescriptor
{
    /// <summary>ViewModel of a sheet on <see cref="SheetContext.Data"/>, a copy owned by the sheet.</summary>
    ISheetViewModel CreateSheet(SheetContext context);
}

/// <summary>
/// Help of a module (proposal 5.1). F5.2: only the guide of the module, from the Wiki catalogue
/// (<see cref="WikiCatalog.ForModule"/>, the same lookup of the «Wiki del modulo» command). The topics of the fields
/// (today WikiContextHelp.Topics in X.Desktop) are added module by module when the module migrates.
/// </summary>
/// <param name="GuideId">Identifier of the guide article of the module, null when the Wiki has none.</param>
public sealed record ModuleHelp(string? GuideId)
{
    /// <summary>Help of <paramref name="module"/> read from the Wiki catalogue.</summary>
    public static ModuleHelp For(string module)
    {
        ArgumentException.ThrowIfNullOrEmpty(module);
        return new(WikiCatalog.ForModule(module)?.Id);
    }
}

/// <summary>
/// Interface state saved in the sheet (proposal 5.1): keys such as <c>metodo</c>, <c>direzione_selezionata</c>,
/// <c>workspace_ca</c>, <c>report_sezioni</c>. Filled module by module when the module migrates (F5.4–F5.12), to document
/// what the ViewModel writes and to check that the view writes no other key.
/// </summary>
/// <param name="UiStateKeys">Keys of interface state written in the sheet data, in no particular order.</param>
public sealed record ModulePersistence(IReadOnlyList<string> UiStateKeys)
{
    /// <summary>Nothing declared: a module that has not migrated keeps its keys in the historic view, as today.</summary>
    public static ModulePersistence Undeclared { get; } = new([]);
}

/// <summary>
/// Report capabilities of a module (proposal 5.1): sections and defaults (ReportWord, ReportConcrete, ReportBridge), short
/// report, JSON results. F5.2 keeps only the short report flag; the rest is added module by module when the module
/// migrates. The report code does not change: reports are frozen (8/10).
/// </summary>
/// <param name="ShortReport">True when the module writes the short report (today only the concrete section).</param>
public sealed record ModuleReportCapabilities(bool ShortReport)
{
    /// <summary>Nothing declared: for a module that has not migrated the historic view and MainWindow decide, as today.</summary>
    public static ModuleReportCapabilities Undeclared { get; } = new(false);
}
