using System.IO;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Presentation.Modules.ConcreteSection;
using X.Core;

namespace X.Desktop;

/// <summary>
/// The c.a. smokes (ui/smoke-section-actions, ui/smoke-ca-extensions, ui/smoke-ca-features): the sheet opened in a tab, then the checks of
/// <see cref="ConcreteSectionSmoke"/> on the XAML view (the historic checks are archived with the historic view in supporto/SUPERATI/ui-storica/sezione-ca).
/// Since F5.13 g on the new shell, off screen (ShellSmokeBench of part e): the document in a tab in place of ShowSheet of MainWindow, the same data, the
/// same calculation and the same checks.
/// </summary>
internal static class ConcreteSectionSmokeSuites
{
    internal static async Task SectionActions(string directory)
    {
        Directory.CreateDirectory(directory);
        var document = DocumentService.CreateDocument("str_palo");
        var data = document["dati"]!.AsObject();
        var vm = new SectionActionsViewModel(data);
        vm.Import(SectionActionsExcel.Write([new("SLU", "C1", -100, 10, 5, 20, 30, 0), new("SLV", "C1", -90, 8, 3, 12, 24, 0, "P02"), new("SLE", "Rara 1", -60, 6, 2, 8, 10, 0)]), true);
        if (!vm.Apply()) throw new Exception(vm.Status);
        document["dati"] = vm.Result!.DeepClone();
        await OnShell(directory, document, s => s.VerifySectionActions(directory));
    }

    internal static async Task Extensions(string directory)
    {
        Directory.CreateDirectory(directory); var document = Archivio.Documento("str_palo"); document["dati"] = SezioneCA.DefaultData();
        await OnShell(directory, document, s => s.VerifyExtensions(directory));
    }

    internal static async Task Features(string directory)
    {
        Directory.CreateDirectory(directory); var document = Archivio.Documento("str_palo"); document["dati"] = SezioneCA.DefaultData();
        await OnShell(directory, document, s => s.VerifyFeatures(directory));
    }

    /// <summary>The document in a tab of the shell, calculated (commit and calculation, as MvvmSheetEditor.CalculateAsync), then the checks.</summary>
    private static async Task OnShell(string directory, JsonObject document, Func<ConcreteSectionSmoke, Task> check)
    {
        using var b = await ShellSmokeBench.OpenAsync(directory);
        b.Workspace.OpenDocument(document, null, false); await b.Idle();
        var sheet = b.Sheet;
        await sheet.Hosted.CalculateAsync();
        await OpenSheet.Verify(new ConcreteSectionSmoke(sheet.Hosted, b.Services), async s => { await s.Settle(); await check(s); });
        b.Services.Confirmations.SaveChanges = () => Anthea.Presentation.Services.SaveChangesAnswer.Discard;
    }
}
