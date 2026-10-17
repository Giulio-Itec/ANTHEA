using Anthea.Application.Modules;
using Anthea.FoundationWorkspace;

namespace Anthea.Presentation.Modules.ShallowFoundation;

/// <summary>
/// The shallow foundations as a module migrated to MVVM (F5.9b preparation): identity from <see cref="ModuleDirectory"/>, guide of
/// the module, the interface state its sheets save (vista_fondazioni) and its sheet, <see cref="ShallowFoundationSheetViewModel"/>.
/// Registered in the composition root of the desktop; the window opens it only when ModuleImplementations chooses the MVVM
/// implementation, which before the F5.9b checkpoint happens only in the UiTests bench (--module-impl).
/// </summary>
/// <param name="libraryAvailable">Whether the approved release of the GPC library is installed; by default
/// <see cref="FoundationLibrary.Available"/>, read when a sheet is created.</param>
/// <param name="transfer">The soil transfer dialog of «Invia / carica terreno…» (the desktop passes the historic window behind
/// <see cref="Shared.ISoilTransferDialog"/>, decision T5); without it the sheets have no transfer command.</param>
public sealed class ShallowFoundationSheetModule(Func<bool>? libraryAvailable = null, Shared.ISoilTransferDialog? transfer = null) : ISheetModule
{
    private readonly Func<bool> available = libraryAvailable ?? (() => new FoundationLibrary().Available);

    public ModuleInfo Info { get; } = ModuleDirectory.Get(FoundationDocument.Module);

    /// <summary>Read when asked: building the registry at startup does not load the Wiki catalogue.</summary>
    public ModuleHelp Help => ModuleHelp.For(Info.Id);

    /// <summary>vista_fondazioni (groups and collapsed inputs); the lazy migration is FoundationMaterials.Complete at every opening.</summary>
    public ModulePersistence Persistence { get; } = new(ShallowFoundationSheetViewModel.UiStateKeys);

    /// <summary>One report (FoundationReport, Word with the PDF of MigraDoc beside it, <see cref="IModuleReportPdf"/>), no short report.</summary>
    public ModuleReportCapabilities Reports { get; } = new(false);

    public ISheetViewModel CreateSheet(SheetContext context) => new ShallowFoundationSheetViewModel(context, available(), transfer);
}
