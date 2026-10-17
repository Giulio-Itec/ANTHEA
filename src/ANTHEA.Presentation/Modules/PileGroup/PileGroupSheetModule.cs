using Anthea.Application.Modules;

namespace Anthea.Presentation.Modules.PileGroup;

/// <summary>
/// The horizontal pile group as a module migrated to MVVM (F5.4 pilot): identity from <see cref="ModuleDirectory"/>, guide of the
/// module, the interface state its sheets save (the selected result) and its sheet, <see cref="PileGroupSheetViewModel"/>.
/// Registered in the composition root of the desktop; since the F5.4 checkpoint (17/10/2026) ModuleImplementations chooses it in the
/// Release, and the historic view is archived (supporto/SUPERATI/ui-storica/palificata).
/// </summary>
public sealed class PileGroupSheetModule : ISheetModule
{
    public ModuleInfo Info { get; } = ModuleDirectory.Get(HorizontalPileGroupEditor.Module);

    /// <summary>Read when asked: building the registry at startup does not load the Wiki catalogue.</summary>
    public ModuleHelp Help => ModuleHelp.For(Info.Id);

    /// <summary>metodo, direzione_selezionata and angolo; the lazy migrations are HorizontalPileGroupEditor.Upgrade and Regenerate.</summary>
    public ModulePersistence Persistence { get; } = new(PileGroupSheetViewModel.UiStateKeys);

    /// <summary>One report (ReportPileGroup), no short report.</summary>
    public ModuleReportCapabilities Reports { get; } = new(false);

    public ISheetViewModel CreateSheet(SheetContext context) => new PileGroupSheetViewModel(context);
}
