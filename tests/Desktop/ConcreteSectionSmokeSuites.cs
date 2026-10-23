using System.IO;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Core;

namespace X.Desktop;

/// <summary>
/// The c.a. smokes of the window (ui/smoke-section-actions, ui/smoke-ca-extensions, ui/smoke-ca-features): the sheet opened as the window opens it,
/// then the checks of <see cref="ConcreteSectionSmoke"/> on the XAML view (the RC section with the XAML view: the bench option module-impl
/// str_palo=mvvm until the switch of the Release, the Release after it); on the historic view the historic checks, until the switch.
/// </summary>
public sealed partial class MainWindow
{
    internal async Task SmokeSectionActions(string directory)
    {
        Directory.CreateDirectory(directory);
        document = DocumentService.CreateDocument("str_palo"); currentSheet = null;
        var data = document["dati"]!.AsObject();
        var vm = new SectionActionsViewModel(data);
        vm.Import(SectionActionsExcel.Write([new("SLU", "C1", -100, 10, 5, 20, 30, 0), new("SLV", "C1", -90, 8, 3, 12, 24, 0, "P02"), new("SLE", "Rara 1", -60, 6, 2, 8, 10, 0)]), true);
        if (!vm.Apply()) throw new Exception(vm.Status);
        document["dati"] = vm.Result!.DeepClone(); ShowSheet(document); await editor!.CalculateAsync();
        if (ConcreteSectionSmoke.Handles(editor)) await OpenSheet.Verify(openSheet.ConcreteSection, async s => { await s.Settle(); await s.VerifySectionActions(directory); });
        else await historicEditor!.VerifySectionActions(directory);
        dirty = false;
    }

    internal async Task SmokeConcreteExtensions(string directory)
    {
        Directory.CreateDirectory(directory); document = Archivio.Documento("str_palo"); document["dati"] = SezioneCA.DefaultData(); currentSheet = null; ShowSheet(document);
        await editor!.CalculateAsync();
        if (ConcreteSectionSmoke.Handles(editor)) await OpenSheet.Verify(openSheet.ConcreteSection, async s => { await s.Settle(); await s.VerifyExtensions(directory); });
        else await historicEditor!.VerifyConcreteExtensions(directory);
        dirty = false;
    }

    internal async Task SmokeConcreteFeatures(string directory)
    {
        Directory.CreateDirectory(directory);
        document = Archivio.Documento("str_palo"); document["dati"] = SezioneCA.DefaultData(); currentSheet = null; ShowSheet(document);
        await editor!.CalculateAsync();
        if (ConcreteSectionSmoke.Handles(editor)) await OpenSheet.Verify(openSheet.ConcreteSection, async s => { await s.Settle(); await s.VerifyFeatures(directory); });
        else await historicEditor!.VerifyConcreteFeatures(directory);
        dirty = false;
    }
}
