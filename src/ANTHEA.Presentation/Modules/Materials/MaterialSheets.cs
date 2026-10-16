using Anthea.Application.Materials;
using Anthea.Application.Modules;
using Anthea.Reports;
using Materiali;
using X.Core;

namespace Anthea.Presentation.Modules.Materials;

/// <summary>
/// A material sheet (F5.5): concrete or reinforcing steel. Besides the report of the sheet it gives the chapter of the material for
/// the reports (ISheetEditor.MaterialReportContent of the window, the project report).
/// </summary>
public interface IMaterialSheetViewModel : ISheetViewModel
{
    /// <summary>The chapter of the material (<see cref="MaterialReportChapter.Create"/>) on the committed data: the sheet is committed first.</summary>
    ReportProject.SheetContent ReportContent();
}

/// <summary>
/// The report of a material sheet, as the historic sheet exports it: the sheet is committed, then the report of a section with
/// only this sheet (<see cref="MaterialReportChapter.Document"/>, reports frozen on 8/10).
/// </summary>
internal sealed class MaterialSheetReport(SheetViewModelBase sheet) : IModuleReportSource
{
    public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false)
    {
        sheet.Commit();
        return DocxWriter.Write(MaterialReportChapter.Document(sheet.ModuleId, title, sheet.Data));
    }
}

/// <summary>
/// The concrete material as a module migrated to MVVM (F5.5): identity from <see cref="ModuleDirectory"/>, guide of the module, no
/// interface state of its own, its sheet <see cref="ConcreteMaterialSheetViewModel"/>. Registered in the composition root of the
/// desktop; the window opens it only when ModuleImplementations chooses the MVVM implementation, which before the F5.5 checkpoint
/// happens only in the UiTests bench (--module-impl).
/// </summary>
public sealed class ConcreteMaterialSheetModule : ISheetModule
{
    public ModuleInfo Info { get; } = ModuleDirectory.Get(ConcreteMaterialSheetViewModel.Module);

    /// <summary>Read when asked: building the registry at startup does not load the Wiki catalogue.</summary>
    public ModuleHelp Help => ModuleHelp.For(Info.Id);

    /// <summary>
    /// No key of interface state: the sheet saves its whole state (class, numbers, choices, options, exposures), which is input of
    /// the calculation. The lazy migration is <see cref="MaterialSheetInput.NormalizeState"/>, at every opening.
    /// </summary>
    public ModulePersistence Persistence { get; } = new([]);

    /// <summary>One report (the material sheet), no short report.</summary>
    public ModuleReportCapabilities Reports { get; } = new(false);

    public ISheetViewModel CreateSheet(SheetContext context) => new ConcreteMaterialSheetViewModel(context);
}

/// <summary>
/// The reinforcing steel as a module migrated to MVVM (F5.5): identity, guide, no interface state of its own, its sheet
/// <see cref="RebarMaterialSheetViewModel"/>. Registered in the composition root; opened only when ModuleImplementations says so.
/// </summary>
public sealed class RebarMaterialSheetModule : ISheetModule
{
    public ModuleInfo Info { get; } = ModuleDirectory.Get(RebarMaterialSheetViewModel.Module);

    /// <summary>Read when asked: building the registry at startup does not load the Wiki catalogue.</summary>
    public ModuleHelp Help => ModuleHelp.For(Info.Id);

    /// <summary>No key of interface state: «input» and «riferimento» are data of the steel; no lazy migration in the sheet.</summary>
    public ModulePersistence Persistence { get; } = new([]);

    /// <summary>One report (the material sheet), no short report.</summary>
    public ModuleReportCapabilities Reports { get; } = new(false);

    public ISheetViewModel CreateSheet(SheetContext context) => new RebarMaterialSheetViewModel(context);
}
