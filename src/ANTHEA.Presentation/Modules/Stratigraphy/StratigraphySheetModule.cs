using Anthea.Application.Modules;
using Anthea.Application.Stratigraphy;

namespace Anthea.Presentation.Modules.Stratigraphy;

/// <summary>
/// The stratigraphy as a module migrated to MVVM (F5.6b): identity from <see cref="ModuleDirectory"/>, guide of the module, no interface
/// state of its own, its sheet <see cref="StratigraphySheetViewModel"/> with the options of the module (source of the AI drafts, proposed
/// model and key, soil transfer dialog, import enabled). Registered in the composition root of the desktop; the window opens it only when
/// ModuleImplementations chooses the MVVM implementation, which before the F5.6b checkpoint happens only in the UiTests bench
/// (--module-impl).
/// </summary>
public sealed class StratigraphySheetModule : ISheetModule
{
    private readonly StratigraphySheetOptions options;

    /// <param name="options">Source of the AI drafts, proposed model and key, soil transfer dialog, import enabled.</param>
    public StratigraphySheetModule(StratigraphySheetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    public ModuleInfo Info { get; } = ModuleDirectory.Get(StratigraphyDocument.Module);

    /// <summary>Read when asked: building the registry at startup does not load the Wiki catalogue.</summary>
    public ModuleHelp Help => ModuleHelp.For(Info.Id);

    /// <summary>
    /// No key of interface state: the selected layer is not saved, and everything the sheet writes is data of the stratigraphy (the import
    /// adds «import_source», the transfer «soil_transfer», both data of the document).
    /// </summary>
    public ModulePersistence Persistence { get; } = new([]);

    /// <summary>One report (the data sheet), no short report.</summary>
    public ModuleReportCapabilities Reports { get; } = new(false);

    public ISheetViewModel CreateSheet(SheetContext context) => new StratigraphySheetViewModel(context, options);
}
