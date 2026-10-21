using System.Text.Json.Nodes;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.ConcreteWorkspace;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The sections made by the wizard in the ViewModel of the RC section (F5.10 S1b) beyond the traces: the wizard on the dialog service (its
/// title, «Annulla», «Applica sezione» available only with a valid preview), the drafts committed before the wizard, the command in a
/// read-only revision (fast stage); and the native persistence of version 3 (with the long traces, decision C7): every example of the
/// distribution opened, calculated and committed by the ViewModel keeps its GPC section (the stamp of the recipe is still current, the
/// archive is restored), and its save keeps the same archive.
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private static async Task WizardAsync(string root, Expect expect)
    {
        ConcreteTemplateWizardViewModel? shown = null;
        bool refused = false, accepted = false;
        var dialogs = new ScriptedDialogs()
            .Then(dialog => { shown = (ConcreteTemplateWizardViewModel)dialog; shown.CancelCommand.Execute(null); })
            .Then(dialog =>
            {
                shown = (ConcreteTemplateWizardViewModel)dialog;
                shown.Form["wizard_type"].SelectedChoice = "A I";
                shown.Form["bottom_flange_width_mm"].EditText = "abc";
                refused = !shown.CanApply && !shown.ApplyCommand.CanExecute(null);
                shown.Form["bottom_flange_width_mm"].EditText = "700";
                accepted = shown.ApplyCommand.CanExecute(null);
                shown.ApplyCommand.Execute(null);
            });
        using (var sheet = OpenSheet(ConcreteSectionScenarios.FixtureData(root, "default"), services: Scripted.Services(dialogs: dialogs)))
        {
            await IdleAsync(sheet);
            var inputs = sheet.Inputs;
            expect.That(!inputs.IsTemplateSection && inputs.GeometryShown && inputs.ReinforcementShown && !inputs.TemplateNotesShown && inputs.HoleFormEnabled
                && inputs.OpenWizardCommand.CanExecute(null), "Sezione ordinaria: editor della geometria e dell'armatura, wizard disponibile");
            // A text typed with the focus is a draft: the wizard commits the sheet before it opens (Commit of OpenTemplateWizard).
            var width = inputs.Geometry["width_mm"];
            width.IsFocused = true; width.EditText = "650";
            await inputs.OpenWizardCommand.ExecuteAsync(null);
            width.IsFocused = false;
            expect.That(shown is { Title: ConcreteSectionTrace.WizardTitle, Result: false } && sheet.Input.S("width_mm") == "650" && sheet.Input.S("shape") == "Rettangolare",
                "Wizard annullato: titolo della finestra, bozza confermata prima dell'apertura, sezione invariata");
            await IdleAsync(sheet);
            await inputs.OpenWizardCommand.ExecuteAsync(null);
            expect.That(refused && accepted && shown!.Result, "Wizard: «Applica sezione» solo con un'anteprima valida, poi chiusura con la sezione applicata");
            expect.That(sheet.Input.S("shape") == "A I" && !sheet.Input.ContainsKey(ConcreteSectionArchive.Key) && inputs.IsTemplateSection && !inputs.GeometryShown
                && !inputs.GeometryEnabled && !inputs.ReinforcementShown && inputs.TemplateNotesShown && !inputs.ReinforcementEnabled && !inputs.HoleEnabled && !inputs.HoleFormEnabled
                && inputs.Layers.All(l => !l.IsShown) && inputs.Geometry["shape"].Choices.Contains("A I") && inputs.Geometry["shape"].SelectedChoice == "A I"
                && sheet.IsCalculationQueued, "Sezione a I applicata: editor nascosto, note del wizard, voce nuova di «Sezione», calcolo in coda");
            await IdleAsync(sheet);
            expect.That(sheet.ShearAutomaticNote == sheet.Settings["taglio"]!["nota_wizard"]!.ToString() && sheet.ShearAutomaticNote.StartsWith("Staffe nei singoli elementi", StringComparison.Ordinal),
                "Taglio con parametri manuali: nota scritta dal wizard");
            sheet.IsReadOnly = true;
            expect.That(!inputs.OpenWizardCommand.CanExecute(null), "Sola lettura: wizard non disponibile");
            sheet.IsReadOnly = false;
            expect.That(inputs.OpenWizardCommand.CanExecute(null), "Revisione modificabile: wizard di nuovo disponibile");
        }
    }

    // Native persistence (version 3): the ViewModel opens, calculates and commits every example. When the opening leaves the recipe as it is
    // (the stamp of the editor), the GPC section of the file stays current and the save keeps its archive; when the opening completes the
    // recipe with the defaults of the editor (examples saved without them: a historic behaviour of the opening, kept), the archive is no
    // longer current, the calculation rebuilds the section and the save captures a new one. The examples of the wizard keep their section.
    private static async Task NativePersistenceAsync(string root, Expect expect)
    {
        var failures = new List<string>();
        var completed = new List<string>();
        var examples = ConcreteSectionScenarios.ExampleFixtures(root);
        string file = Path.Combine(Path.GetTempPath(), "anthea-f5-10-s1b-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".anthea");
        try
        {
            foreach (string id in examples)
            {
                var data = ConcreteSectionScenarios.FixtureData(root, id);
                string archive = data["input"]![ConcreteSectionArchive.Key]!["archivio_sha256"]!.ToString();
                string stamp = ConcreteSectionArchive.EditorStamp(data["input"]!.AsObject(), data["workspace_ca"]!.AsObject());
                using var sheet = OpenSheet(data);
                await IdleAsync(sheet);
                sheet.Commit();
                bool same = ConcreteSectionArchive.EditorStamp(sheet.Input, sheet.Settings) == stamp;
                bool current = ConcreteSectionArchive.Restore(sheet.Input, sheet.Settings) is not null;
                if (!same) completed.Add(id);
                if (same != current) { failures.Add(id + ": sezione GPC " + (current ? "corrente con la ricetta cambiata" : "non più corrente con la stessa ricetta")); continue; }
                DocumentService.Save(file, J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", ConcreteSectionScenarios.Module), ("dati", sheet.Data.DeepClone())));
                var saved = JsonNode.Parse(File.ReadAllText(file))!;
                var reopened = DocumentService.Open(file)["dati"]!.AsObject();
                string again = reopened["input"]![ConcreteSectionArchive.Key]!["archivio_sha256"]!.ToString();
                bool restored = ConcreteSectionArchive.Restore(reopened["input"]!.AsObject(), reopened["workspace_ca"]!.AsObject()) is not null;
                if (saved["versione"]?.ToString() != "3" || !restored || (again == archive) != same)
                    failures.Add(id + ": salvataggio" + (restored ? "" : " senza sezione GPC corrente") + (again == archive ? " con l'archivio precedente" : " con un archivio nuovo"));
            }
        }
        finally { if (File.Exists(file)) File.Delete(file); }
        expect.That(examples.Count == 15 && failures.Count == 0, "Esempi aperti e salvati dal ViewModel con la loro sezione GPC (" + examples.Count + "): " + string.Join(" | ", failures));
        expect.That(completed.All(id => !id.StartsWith(ConcreteSectionScenarios.ExamplePrefix + "wizard-", StringComparison.Ordinal)),
            "Esempi del wizard: ricetta invariata all'apertura, sezione GPC riusata (ricette completate: " + string.Join(", ", completed) + ")");
    }
}
