using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.ConcreteWorkspace;
using Anthea.Presentation.Forms;
using X.Core;

namespace X.Desktop;

internal static class ConcreteTemplateViewChecks
{
    internal static async Task Run(string output)
    {
        Directory.CreateDirectory(output);
        int checks = 0;
        void Check(bool good, string label) { checks++; if (!good) throw new InvalidOperationException(label); }
        var document = DocumentService.CreateDocument("str_palo");
        var data = document["dati"]!.AsObject();
        ConcreteSheetPreparation.PrepareReport(data);
        string before = data.ToJsonString();
        var window = new ConcreteTemplateWizardWindow(data) {
            Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
            ShowActivated = false, ShowInTaskbar = false
        };
        Appearance.Watch(window); window.Show();
        try
        {
            foreach (string choice in ConcreteTemplateWizard.Choices)
            {
                var field = window.Model.Form.Fields.Single(f => f.Key == "wizard_type");
                var selector = Ui.Descendants<ComboBox>(window).Single(c => c.DataContext is FieldViewModel f && f.Key == "wizard_type");
                selector.SelectedItem = choice;
                window.Model.Refresh();
                window.UpdateLayout();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                Check(window.Model.CanApply, choice + ": " + window.Model.Error);
                Check(data.ToJsonString() == before, choice + ": anteprima modifica documento");
                Check(selector.ActualWidth >= 350 && field.SelectedChoice == choice, choice + ": selettore tagliato o non collegato");
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, $"wizard-{ConcreteTemplateWizard.Choices.IndexOf(choice):D2}.png")); encoder.Save(file);
                var applied = DocumentService.CreateDocument("str_palo");
                ConcreteSheetPreparation.PrepareReport(applied["dati"]!.AsObject());
                ConcreteTemplateWizard.Apply(applied["dati"]!.AsObject(), window.Model.Draft);
                string saved = Path.Combine(output, $"editor-{ConcreteTemplateWizard.Choices.IndexOf(choice):D2}.anthea");
                DocumentService.Save(saved, applied);
                var reopened = DocumentService.Open(saved)["dati"]!.AsObject();
                var native = CheckerSection.PrepareModel(reopened["input"]!.AsObject(), reopened["workspace_ca"]!.AsObject()).Section.Guid;
                using var editor = new ConcreteWorkspace(reopened);
                editor.CheckTemplateEditor(Check, window.Model.Geometry!.Bars.Count);
                editor.Commit();
                Check(CheckerSection.PrepareModel(reopened["input"]!.AsObject(), reopened["workspace_ca"]!.AsObject()).Section.Guid == native,
                    choice + ": l'editor abbandona il modello GPC appena riaperto");
            }
            window.Model.Form.Fields.Single(f => f.Key == "cover_mm").EditText = "10000";
            window.Model.Form.Commit();
            Check(!window.Model.CanApply && window.Model.Error.Length > 0, "Geometria non valida accettata");
            Check(!window.Model.Apply() && data.ToJsonString() == before, "Bozza non valida scritta nel documento");
            window.Model.Form.Fields.Single(f => f.Key == "cover_mm").EditText = "70";
            window.Model.Form.Commit();
            Check(window.Model.Apply(), "Conferma finale fallita: " + window.Model.Error);
            string path = Path.Combine(output, "sezione.anthea");
            DocumentService.Save(path, document);
            Check(DocumentService.Open(path)["dati"]!["input"].S("shape") == data["input"].S("shape"), "Tipologia non riaperta");
            File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli wizard, anteprime, rifiuti e persistenza.");
        }
        finally { window.Close(); }
    }
}

internal sealed partial class ConcreteWorkspace
{
    internal void CheckTemplateEditor(Action<bool, string> check, int bars)
    {
        check(preview.Section is not null && preview.Message.Length == 0 && barInventory.Rows.Count == bars,
            Input.S("shape") + ": riapertura nell'editor senza geometria o armature");
        if (!ConcreteTemplateAdapter.Uses(Input)) return;
        check(geometry.Visibility == Visibility.Collapsed && reinforcement.Visibility == Visibility.Collapsed &&
            additionalLayers.Values.All(layer => layer.Body.Visibility == Visibility.Collapsed),
            Input.S("shape") + ": controlli di disposizioni estranee ancora visibili");
        check(preview.Section!.TemplateLinks.Count > 0, Input.S("shape") + ": percorsi staffe persi nell'editor");
    }
}
