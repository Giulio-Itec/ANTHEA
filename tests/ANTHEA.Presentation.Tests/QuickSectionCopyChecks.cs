using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.QuickSection;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;

namespace Anthea.Presentation.Tests;

internal static class QuickSectionCopyChecks
{
    internal static async Task Run(Expect expect, string folder)
    {
        CopiesSettings(expect);
        await QuickCombinationResetChecks.Run(expect, folder);
        var messages = new ScriptedMessages(); var dialogs = new ScriptedDialogs();
        var services = Scripted.Services(messages, dialogs: dialogs);
        var modules = new ModuleRegistry([new QuickSectionSheetModule(), new ProbeSheetModule(ModuleDirectory.Get("mat_calcestruzzo"))]);
        var factory = new RegistrySheetFactory(modules, services, new CalculationLimiter(2), ModuleServices.Get);
        var files = new DocumentFiles(Path.Combine(folder, "esempi-assenti"));
        using var workspace = new WorkspaceSession(services, factory, files);
        using var commands = new ShellCommandsViewModel(workspace, services, new NoExports(), files);
        var first = workspace.NewCalculation(ConcreteQuickSection.Module);
        var source = (QuickSectionSheetViewModel)first.ViewModel;
        source.Title = "Sorgente";
        expect.That(commands.SectionCopyVisible && !commands.CopySectionToCommand.CanExecute(first) && !commands.CopySectionFromCommand.CanExecute(first),
            "Copia: con una sola sezione entrambi i comandi sono disattivati");
        source.GeometryForm.Set("width_mm", "550");
        source.MaterialsForm.Set("fck_mpa", "45");
        source.CombinationRows[0].N = "-700"; source.CombinationRows[0].Mx = "200";
        source.AddCombinationCommand.Execute(null); source.CombinationRows[1].Mx = "330";
        source.MoveCombinationUpCommand.Execute(null);
        source.Commit(); await source.PendingCalculation;
        string sourceData = source.Data.ToJsonString();
        var second = workspace.NewCalculation(ConcreteQuickSection.Module);
        var target = (QuickSectionSheetViewModel)second.ViewModel; target.Title = "Destinazione";
        target.CombinationRows[0].N = "-250"; target.CombinationRows[0].Mx = "120";
        await target.PendingCalculation;
        string targetData = target.Data.ToJsonString();
        string targetActions = source.Data["combinazioni"]!.ToJsonString();
        string targetOptions = source.Data["workspace_ca"]!.ToJsonString();
        string[] targetIds = source.CombinationRows.Select(r => r.Id).ToArray();
        string oldSummary = target.Summary!.Value;
        expect.That(commands.CopySectionToCommand.CanExecute(first) && commands.CopySectionFromCommand.CanExecute(second), "Copia: due sezioni disponibili in entrambe le direzioni");
        dialogs.Then(d => ((QuickSectionCopyDialogViewModel)d).CancelCommand.Execute(null));
        await commands.CopySectionToCommand.ExecuteAsync(first);
        expect.That(source.Data.ToJsonString() == sourceData && target.Data.ToJsonString() == targetData, "Copia: Annulla non modifica le sezioni");
        int edits = 0; target.Modified += (_, _) => edits++;
        dialogs.Then(d =>
        {
            var dialog = (QuickSectionCopyDialogViewModel)d;
            expect.That(dialog.Title == "Copia in" && dialog.Choices.Count == 1 && dialog.Selected!.Sheet == second
                && dialog.Direction.Contains("Sorgente") && dialog.Direction.Contains("Destinazione"), "Copia in: origine e destinazione esplicite");
            dialog.CopyCommand.Execute(null);
        });
        await commands.CopySectionToCommand.ExecuteAsync(first);
        expect.That(edits == 1 && second.IsUnsaved && second.Owner.IsDirty && workspace.Active == second, "Copia in: destinazione modificata una volta e attivata");
        expect.That(target.Title == "Destinazione" && source.Data.ToJsonString() == sourceData
            && target.Data["input"].S("width_mm") == "550" && J.Number(target.Data["input"]!["fck_mpa"]) == 45
            && JsonNode.DeepEquals(target.Data["input"]!["barre_manuali"], source.Data["input"]!["barre_manuali"]), "Copia: geometria, armature e materiali trasferiti, nome della destinazione conservato");
        expect.That(target.Data["combinazioni"]!.ToJsonString() == targetActions && target.Data["workspace_ca"]!.ToJsonString() == targetOptions
            && target.CombinationRows.Select(r => r.Id).SequenceEqual(targetIds) && target.SelectedCombinationId == targetIds[0], "Copia: azioni, identificatori, selezione e tutte le opzioni trasferiti dalla sorgente");
        expect.That(!target.HasResults, "Copia: vecchi risultati invalidati subito");
        await target.PendingCalculation;
        expect.That(target.HasResults && target.Section!.AreaCls == 440000 && target.Summary!.Value != oldSummary
            && target.Current!.Combinations.Select(r => r.Id).SequenceEqual(targetIds), "Copia: ricalcolo della nuova sezione e delle combinazioni copiate, area indipendente 550 × 800 mm²");
        target.CombinationRows[0].Mx = "90";
        target.GeometryForm.Set("width_mm", "650");
        expect.That(source.Data.ToJsonString() == sourceData, "Copia: modificare la destinazione non modifica l’origine");
        dialogs.Then(d => { var dialog = (QuickSectionCopyDialogViewModel)d; expect.That(dialog.Title == "Copia da" && dialog.Selected!.Sheet == first, "Copia da: scelta della sorgente"); dialog.CopyCommand.Execute(null); });
        await commands.CopySectionFromCommand.ExecuteAsync(second); await target.PendingCalculation;
        expect.That(target.Data["input"].S("width_mm") == "550" && target.Title == "Destinazione" && target.CombinationRows[0].Mx == source.CombinationRows[0].Mx
            && target.CombinationRows.Count == 2, "Copia da: geometria e combinazioni copiate, nome della destinazione conservato");
        target.Commit(); var document = DocumentService.CreateDocument(ConcreteQuickSection.Module); document["dati"] = target.Data.DeepClone();
        string path = Path.Combine(folder, "sezione-copiata.anthea"); DocumentService.Save(path, document);
        expect.That(JsonNode.DeepEquals(DocumentService.Open(path)["dati"], target.Data), "Copia: salvataggio e riapertura conservano la sezione copiata");
        target.IsReadOnly = true; targetData = target.Data.ToJsonString();
        expect.That(!target.CopyFrom(source.Data) && target.Data.ToJsonString() == targetData
            && !commands.CopySectionFromCommand.CanExecute(second) && !commands.CopySectionToCommand.CanExecute(first), "Copia: destinazione in sola lettura protetta");
        expect.That(commands.CopySectionToCommand.CanExecute(second) && commands.CopySectionFromCommand.CanExecute(first), "Copia: la sezione in sola lettura può essere un’origine");
        target.IsReadOnly = false;
        var invalid = (JsonObject)source.Data.DeepClone(); invalid[ConcreteQuickSection.ViewKey] = null;
        expect.Throws<ArgumentException>(() => target.CopyFrom(invalid), "Copia: documento sorgente non valido rifiutato");
        expect.That(target.Data.ToJsonString() == targetData, "Copia: errore di validazione lascia intatta la destinazione");
        var third = workspace.NewCalculation(ConcreteQuickSection.Module); ((QuickSectionSheetViewModel)third.ViewModel).Title = "Destinazione";
        dialogs.Then(d =>
        {
            var dialog = (QuickSectionCopyDialogViewModel)d;
            expect.That(dialog.Choices.Count == 2 && dialog.Choices.Select(c => c.Label).Distinct().Count() == 2, "Copia: nomi uguali distinguibili senza usare il nome come identità");
            dialog.Selected = null; expect.That(!dialog.CopyCommand.CanExecute(null), "Copia: scelta assente non confermabile");
            dialog.Selected = dialog.Choices.Single(c => c.Sheet == third); dialog.CopyCommand.Execute(null);
        });
        await commands.CopySectionToCommand.ExecuteAsync(first);
        expect.That(workspace.Active == third && ((QuickSectionSheetViewModel)third.ViewModel).Data["input"].S("width_mm") == "550"
            && ((QuickSectionSheetViewModel)third.ViewModel).CombinationRows.Count == 2, "Copia: la scelta fra nomi uguali raggiunge il foglio esatto");
        var circular = ConcreteQuickSection.Create(); circular["input"]!["shape"] = ConcreteQuickSection.Circular;
        circular["input"]!["longitudinal_bar_count"] = "12";
        circular["input"]!["second_inner_enabled"] = true; circular["input"]!["second_inner_count"] = "8";
        circular["input"]!["second_inner_diameter"] = "20";
        ConcreteQuickSection.View(circular)[ConcreteQuickSection.SecondRingLinkKey] = "12";
        ConcreteQuickSection.ApplyLayers(circular);
        var circularTarget = (QuickSectionSheetViewModel)third.ViewModel; int layerNotifications = 0;
        circularTarget.AddLayerCommand.CanExecuteChanged += (_, _) => layerNotifications++;
        circularTarget.CopyFrom(circular); await circularTarget.PendingCalculation;
        expect.That(circularTarget.IsCircular && circularTarget.BarRows.Count == 2 && circularTarget.LayerRows.Count == 0
            && circularTarget.Section?.Bars.Count == 20 && ConcreteQuickSection.SecondRingLink(circularTarget.Data) == "12"
            && !circularTarget.AddLayerCommand.CanExecute(null) && layerNotifications > 0,
            "Copia circolare: entrambi gli anelli e staffa trasferiti, disegno, campi e disponibilità dei comandi aggiornati");
        dialogs.Then(d => { target.IsReadOnly = true; ((QuickSectionCopyDialogViewModel)d).Selected = ((QuickSectionCopyDialogViewModel)d).Choices.Single(c => c.Sheet == second); ((QuickSectionCopyDialogViewModel)d).CopyCommand.Execute(null); });
        targetData = target.Data.ToJsonString();
        await commands.CopySectionToCommand.ExecuteAsync(first);
        expect.That(target.Data.ToJsonString() == targetData, "Copia: sola lettura sopraggiunta durante il dialogo ricontrollata");
        target.IsReadOnly = false;
        workspace.NewCalculation("mat_calcestruzzo");
        expect.That(!commands.SectionCopyVisible && !commands.CopySectionToCommand.CanExecute(null), "Copia: altri moduli esclusi");
        source.GeometryForm.Set("width_mm", "500"); targetData = target.Data.ToJsonString();
        dialogs.Then(d => { commands.Dispose(); ((QuickSectionCopyDialogViewModel)d).CopyCommand.Execute(null); });
        await commands.CopySectionToCommand.ExecuteAsync(first);
        expect.That(target.Data.ToJsonString() == targetData, "Copia: comando chiuso durante il dialogo non modifica la destinazione");
        expect.That(messages.Shown.Count == 0 && messages.Failures.Count == 0, "Copia: nessun errore dei comandi");
    }

    private static void CopiesSettings(Expect expect)
    {
        var source = ConcreteQuickSection.Create(); var destination = ConcreteQuickSection.Create();
        ConcreteQuickSection.View(source)["titolo"] = "Origine"; ConcreteQuickSection.View(destination)["titolo"] = "Destinazione";
        ConcreteQuickSection.ReplaceCombinations(source,
            [new("SLU propria", -800, 200, 20), new("SLE propria", -300, 100, 10, "SLE"), new("SLV propria", -500, 150, 0, "SLV")]);
        var ids = ConcreteQuickSection.Combinations(source).Select(r => r.S("id")).ToArray();
        ConcreteQuickSection.WriteCombinationSetting(source, ids[0], ConcreteQuickSection.ModelField, "Elastico");
        ConcreteQuickSection.WriteCombinationSetting(source, ids[0], ConcreteQuickSection.SignField, "SLU−");
        ConcreteQuickSection.SelectCombination(source, ids[1]); ConcreteQuickSection.MoveCombination(source, ids[1], -1);
        ConcreteQuickSection.WriteCreep(source, "2"); ConcreteQuickSection.WriteServiceOption(source, ConcreteQuickSection.ExposureKey, "XC3");
        ConcreteQuickSection.View(source)["flessione"] = "Deviata";
        source["input"]!["gamma_c"] = "1.7"; source["input"]!["gamma_s"] = "1.2"; source["input"]!["fck_mpa"] = "45";
        ConcreteQuickSection.WriteLayer(source, 0, "n", "5"); ConcreteQuickSection.ApplyLayers(source);
        string beforeSource = source.ToJsonString(), beforeDestination = destination.ToJsonString();
        var copy = ConcreteQuickSection.CopyFor(source, destination);
        var expected = (JsonObject)source.DeepClone(); ConcreteQuickSection.View(expected)["titolo"] = "Destinazione";
        expect.That(JsonNode.DeepEquals(copy, expected), "Copia completa: intero documento identico alla sorgente, tranne il nome della destinazione");
        expect.That(source.ToJsonString() == beforeSource && destination.ToJsonString() == beforeDestination,
            "Copia completa: preparazione indipendente senza mutazioni dei documenti");
        ConcreteQuickSection.WriteLayer(copy, 0, "n", "6"); ConcreteQuickSection.ApplyLayers(copy);
        ConcreteQuickSection.WriteCombination(copy, ids[0], "Mx", "333"); ConcreteQuickSection.WriteCreep(copy, "3");
        expect.That(source.ToJsonString() == beforeSource, "Copia completa: armature, combinazioni e impostazioni senza riferimenti condivisi");
    }

    private sealed class NoExports : ISheetExports
    {
        public void ExportResult(SheetSession sheet, string path) => throw new NotSupportedException();
        public void ExportReport(SheetSession sheet, string path, string title, IReadOnlySet<string> options) => throw new NotSupportedException();
        public byte[] BuildShortReport(SheetSession sheet, string title) => throw new NotSupportedException();
        public Task WriteShortReportAsync(string path, byte[] report, string title) => throw new NotSupportedException();
    }
}
