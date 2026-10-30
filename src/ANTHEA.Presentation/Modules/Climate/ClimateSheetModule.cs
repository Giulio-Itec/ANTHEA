using Anthea.Application.Modules;
namespace Anthea.Presentation.Modules.Climate;

public sealed class ClimateSheetModule(string module) : ISheetModule
{
    public ModuleInfo Info=>ModuleDirectory.Get(module);
    public ModuleHelp Help=>ModuleHelp.For(module);
    public ModulePersistence Persistence {get;}=new([]);
    public ModuleReportCapabilities Reports {get;}=new(false);
    public ISheetViewModel CreateSheet(SheetContext context)=>new ClimateSheetViewModel(module,context);
}
