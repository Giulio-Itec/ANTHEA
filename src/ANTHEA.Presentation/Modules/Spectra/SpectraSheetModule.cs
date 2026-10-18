using Anthea.Application.Actions;
using Anthea.Application.Modules;

namespace Anthea.Presentation.Modules.Spectra;

public sealed class SpectraSheetModule : ISheetModule
{
    public ModuleInfo Info => ModuleDirectory.Get(SpectraDocument.Module);
    public ModuleHelp Help => ModuleHelp.For(Info.Id);
    public ModulePersistence Persistence { get; } = new([]);
    public ModuleReportCapabilities Reports { get; } = new(false);
    public ISheetViewModel CreateSheet(SheetContext context) => new SpectraSheetViewModel(context);
}
