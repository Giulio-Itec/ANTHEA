using System.IO;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Core;

namespace X.Desktop;

/// <summary>
/// The c.a. smokes of the window (ui/smoke-section-actions, ui/smoke-ca-extensions, ui/smoke-ca-features): the sheet opened as the window opens it,
/// then the checks of <see cref="ConcreteSectionSmoke"/> on the XAML view, as the Release opens the RC section since the switch of 23/10/2026 (the
/// historic checks are archived with the historic view in supporto/SUPERATI/ui-storica/sezione-ca).
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
        await OpenSheet.Verify(openSheet.ConcreteSection, async s => { await s.Settle(); await s.VerifySectionActions(directory); });
        dirty = false;
    }

    internal async Task SmokeConcreteExtensions(string directory)
    {
        Directory.CreateDirectory(directory); document = Archivio.Documento("str_palo"); document["dati"] = SezioneCA.DefaultData(); currentSheet = null; ShowSheet(document);
        await editor!.CalculateAsync();
        await OpenSheet.Verify(openSheet.ConcreteSection, async s => { await s.Settle(); await s.VerifyExtensions(directory); });
        dirty = false;
    }

    internal async Task SmokeConcreteFeatures(string directory)
    {
        Directory.CreateDirectory(directory);
        document = Archivio.Documento("str_palo"); document["dati"] = SezioneCA.DefaultData(); currentSheet = null; ShowSheet(document);
        await editor!.CalculateAsync();
        await OpenSheet.Verify(openSheet.ConcreteSection, async s => { await s.Settle(); await s.VerifyFeatures(directory); });
        dirty = false;
    }
}
