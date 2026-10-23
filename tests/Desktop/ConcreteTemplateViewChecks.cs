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
using Anthea.Presentation.Modules.ConcreteSection;
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
        // Passaggio della Release della sezione c.a. (23/10/2026): the wizard of the XAML view (ConcreteTemplateWizardView from the DataTemplate of its
        // ViewModel, as WpfDialogService shows it) in place of the historic ConcreteTemplateWizardWindow, off screen.
        var model = new ConcreteTemplateWizardViewModel(data);
        var window = new Window {
            Title = model.Title, Content = model, SizeToContent = SizeToContent.WidthAndHeight,
            Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual,
            ShowActivated = false, ShowInTaskbar = false
        };
        window.SetValue(NumericPresentation.EnabledProperty, true);
        Appearance.Watch(window); window.Show();
        try
        {
            foreach (string choice in ConcreteTemplateWizard.Choices)
            {
                var field = model.Form.Fields.Single(f => f.Key == "wizard_type");
                var selector = Ui.Descendants<ComboBox>(window).Single(c => c.DataContext is FieldViewModel f && f.Key == "wizard_type");
                selector.SelectedItem = choice;
                if (choice == ConcreteTemplateWizard.Choices[7])
                {
                    foreach (var (key, value) in new[] {
                        ("width_mm", "5000"), ("height_mm", "2500"), ("cover_mm", "50"),
                        ("transverse_bar_diameter_mm", "12"), ("transverse_spacing_mm", "150"),
                        ("wizard_stadium_curve_count", "17"), ("wizard_stadium_curve_diameter_mm", "32"),
                        ("wizard_stadium_straight_count", "8"), ("wizard_stadium_straight_diameter_mm", "25") })
                        model.Form[key].EditText = value;
                    model.Form.Commit();
                    window.UpdateLayout();
                    await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                    Check(model.CanApply && model.Geometry!.Bars.Count == 50,
                        "Capsula 5000 × 2500: anteprima delle due armature non disponibile.");
                    Check(Ui.Descendants<TextBox>(window).Any(t => t.IsVisible &&
                        t.DataContext is FieldViewModel f && f.Key == "wizard_stadium_curve_diameter_mm") &&
                        Ui.Descendants<TextBox>(window).Any(t => t.IsVisible &&
                        t.DataContext is FieldViewModel f && f.Key == "wizard_stadium_straight_diameter_mm"),
                        "Capsula: diametri distinti non modificabili nella vista.");
                }
                model.Refresh();
                window.UpdateLayout();
                await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
                Check(model.CanApply, choice + ": " + model.Error);
                Check(data.ToJsonString() == before, choice + ": anteprima modifica documento");
                Check(selector.ActualWidth >= 350 && field.SelectedChoice == choice, choice + ": selettore tagliato o non collegato");
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, $"wizard-{ConcreteTemplateWizard.Choices.IndexOf(choice):D2}.png")); encoder.Save(file);
                var applied = DocumentService.CreateDocument("str_palo");
                ConcreteSheetPreparation.PrepareReport(applied["dati"]!.AsObject());
                ConcreteTemplateWizard.Apply(applied["dati"]!.AsObject(), model.Draft);
                string saved = Path.Combine(output, $"editor-{ConcreteTemplateWizard.Choices.IndexOf(choice):D2}.anthea");
                DocumentService.Save(saved, applied);
                var reopened = DocumentService.Open(saved)["dati"]!.AsObject();
                var native = CheckerSection.PrepareModel(reopened["input"]!.AsObject(), reopened["workspace_ca"]!.AsObject()).Section.Guid;
                var editor = ConcreteSectionSmoke.Reopen(reopened, out var opened);
                using (opened)
                {
                    ConcreteSectionSmoke.CheckTemplateEditor(editor, Check, model.Geometry!.Bars.Count);
                    editor.Commit();
                }
                Check(CheckerSection.PrepareModel(editor.Input, editor.Settings).Section.Guid == native,
                    choice + ": l'editor abbandona il modello GPC appena riaperto");
            }
            model.Form.Fields.Single(f => f.Key == "cover_mm").EditText = "10000";
            model.Form.Commit();
            Check(!model.CanApply && model.Error.Length > 0, "Geometria non valida accettata");
            Check(!model.Apply() && data.ToJsonString() == before, "Bozza non valida scritta nel documento");
            model.Form.Fields.Single(f => f.Key == "cover_mm").EditText = "70";
            model.Form.Commit();
            Check(model.Apply(), "Conferma finale fallita: " + model.Error);
            string path = Path.Combine(output, "sezione.anthea");
            DocumentService.Save(path, document);
            Check(DocumentService.Open(path)["dati"]!["input"].S("shape") == data["input"].S("shape"), "Tipologia non riaperta");
            File.WriteAllText(Path.Combine(output, "esito.txt"), $"PASS {checks} controlli wizard, anteprime, rifiuti e persistenza.");
        }
        finally { window.Close(); }
    }
}
