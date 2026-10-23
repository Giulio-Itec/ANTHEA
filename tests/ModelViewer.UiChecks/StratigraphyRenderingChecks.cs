using System.Reflection;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.Stratigraphy;
using Anthea.Presentation.Modules.Stratigraphy;
using Anthea.Presentation.Services;

static partial class Program
{
    /// <summary>
    /// --stratigraphy (Stratigraphy.UiChecks): the stratigraphy sheet as the composition root of the product opens it (the XAML view since the
    /// Release switch of 23/10, f5-pass-geo), the hatches of the soils, the historic soil transfer window (still used by other historic modules)
    /// and the AI import, all in windows outside the screen (never activated). The AI import is the dialog of the view
    /// (StratigraphyImportDialogViewModel through its DataTemplate) with a scripted source of the drafts and a scripted file dialog: never the
    /// network, never a window of the system. It replaces the historic StratigraphyImportWindow, archived in supporto/SUPERATI/ui-storica/stratigrafia.
    /// </summary>
    static void StratigraphyRenderingChecks(string output)
    {
        var data = StratigraphyDocument.Create();
        var rows = StratigraphyDocument.Layers(data);
        var row = rows[0]!.AsObject(); row["name"] = "Sabbia limosa"; row["soil_type"] = "Sabbia"; row["thickness"] = 3;
        row["gamma"] = 18; row["gamma_sat"] = 20; row["phi"] = 30; row["c"] = 0;
        row["minimum"] = StratigraphyDocument.NewMinimum(); row["minimum"]!["phi"] = 26;
        data["scenario"] = "Entrambi";
        StratigraphyDocument.Add(data);
        row = rows[1]!.AsObject(); row["name"] = "Argilla"; row["soil_type"] = "Argilla"; row["infinite"] = true; row["thickness"] = null; row["color"] = "#A7B7C7";
        row["cu"] = 60; data["water_state"] = "Assegnata"; data["water_depth"] = 1.5;
        var assembly = typeof(X.Desktop.MainWindow).Assembly;
        // The sheet opens through the factory of the composition root and its contract (F5.2), as in the product; the checks read its view.
        var contract = assembly.GetType("X.Desktop.ISheetEditor")!;
        var composition = ProductComposition();
        var factory = ProductSheetEditors(composition);
        var sheet = factory.GetType().GetMethod("Open", Flags)!.Invoke(factory, [StratigraphyDocument.Module, data, null])!;
        var editor = (FrameworkElement)contract.GetProperty("View")!.GetValue(sheet)!;
        using var disposable = (IDisposable)sheet;
        var host = Offscreen(new Window { Content = editor, Width = 1280, Height = 900 });
        try
        {
            host.Show(); Wait(150);
            Check(Children<TextBlock>(editor).Any(t => t.Text == "Stratigrafia del terreno"), "Standalone stratigraphy editor opened");
            var list = Children<ListBox>(editor).Single();
            Check(list.Items.Count == 2, "Two soil layers shown");
            Check(Children<ComboBox>(editor).Any(c => c.SelectedItem?.ToString() == "Entrambi"), "Scenario selection shown");
            Check(Children<TextBlock>(editor).Any(t => t.Text == "Valori minimi · opzionali"), "Minimum fields shown");
            CapturePage(editor, Path.Combine(output, "stratigrafia-1280.png"));
            var scroll = Children<ScrollViewer>(editor).First(s => s.VerticalScrollBarVisibility == ScrollBarVisibility.Auto);
            scroll.ScrollToEnd(); Wait(50); CapturePage(editor, Path.Combine(output, "stratigrafia-parametri.png")); scroll.ScrollToTop();
            list.SelectedIndex = 1; Wait(50);
            Check(Children<CheckBox>(editor).Single(c => c.Content?.ToString() == "Ultimo strato esteso indefinitamente").IsChecked == true, "Infinite last layer shown");
            contract.GetMethod("Commit")!.Invoke(sheet, null); checks++;
            foreach (string theme in new[] { "Dark", "VeryDark" })
            {
                SetAppearance(theme); Wait(50);
                CapturePage(editor, Path.Combine(output, "stratigrafia-" + theme + ".png"));
            }
            SetAppearance("Light");
            host.Width = 900; host.Height = 700; Wait(50);
            CapturePage(editor, Path.Combine(output, "stratigrafia-900.png"));
            var hatchType = assembly.GetType("X.Desktop.SoilHatchBrush")!;
            var samples = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Background = System.Windows.Media.Brushes.White };
            foreach (string soil in StratigraphyDocument.SoilTypes)
            {
                var brush = (System.Windows.Media.Brush?)hatchType.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { soil, System.Windows.Media.Colors.White });
                Check(soil == "Non classificato" ? brush is null : brush?.IsFrozen == true, "Hatch family " + soil);
                var sample = new StackPanel { Margin = new Thickness(12) };
                sample.Children.Add(new TextBlock { Text = soil, Foreground = System.Windows.Media.Brushes.Black });
                sample.Children.Add(new System.Windows.Shapes.Rectangle { Fill = brush, Stroke = System.Windows.Media.Brushes.Gray, Height = 130, Margin = new Thickness(0, 8, 0, 0) });
                samples.Children.Add(sample);
            }
            host.Content = samples; Wait(50); CapturePage(samples, Path.Combine(output, "stratigrafia-retini.png"));
        }
        finally { host.Close(); }
        var transferType = assembly.GetType("X.Desktop.SoilTransferDialog")!;
        var sourceData = data.DeepClone().AsObject(); sourceData["name"] = "Sondaggio S2";
        var sourceRows = new[] { ("Sondaggio S2", StratigraphyDocument.Module, sourceData, "source-sheet-id") };
        var owner = Offscreen(new Window { Width = 300, Height = 200 });
        owner.Show();
        var transfer = Offscreen((Window)Activator.CreateInstance(transferType, Flags, null, [owner, StratigraphyDocument.Module, data, sourceRows], null)!);
        try
        {
            transfer.Show(); Wait(50);
            var import = Children<Button>(transfer).Single(b => b.Content?.ToString() == "Importa dal foglio selezionato");
            Check(import.IsEnabled, "Project source picker enabled");
            import.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(50);
            var profile = (JsonObject)transferType.GetField("profile", Flags)!.GetValue(transfer)!;
            Check(profile["source_id"]!.ToString() == "source-sheet-id", "Project source identity retained");
            Check(profile["stratigraphy"]!["name"]!.ToString() == "Sondaggio S2", "Selected sheet loaded");
            Check(data["name"]!.ToString() != "Sondaggio S2", "Loading preview does not replace destination");
            CapturePage(transfer, Path.Combine(output, "stratigrafia-scambio.png"));
        }
        finally { transfer.Close(); owner.Close(); }
        // The AI import: the product services with a scripted file dialog, a scripted source of the drafts, the view of the dialog by its DataTemplate.
        string pdf = Path.Combine(output, "relazione-di-prova.pdf");
        File.WriteAllText(pdf, "%PDF-1.4\nsynthetic transport fixture");
        var draft = data.DeepClone().AsObject(); draft["import_source"] = new JsonObject { ["warnings"] = "Fixture: confrontare pagine e unità", ["reviewed"] = false };
        var products = (PresentationServices)composition.GetType().GetProperty("Services", Flags)!.GetValue(composition)!;
        using var importDialog = new StratigraphyImportDialogViewModel(new ScriptedDrafts(draft), new StratigraphyImportDefaults("modello-di-prova", ""),
            new PresentationServices(products.Messages, products.Confirmations, new ScriptedFile(pdf), products.Dialogs, products.Clipboard, products.Links, products.Wiki));
        var dialog = Offscreen(new Window { Content = importDialog, Width = 1200, Height = 850 });
        try
        {
            dialog.Show(); Wait(50);
            Button Apply() => Children<Button>(dialog).Single(b => b.Content?.ToString()?.StartsWith("Sostituisci la stratigrafia") == true);
            Check(!Apply().IsEnabled && !importDialog.ApplyCommand.CanExecute(null), "Import cannot apply before review");
            CapturePage(dialog, Path.Combine(output, "stratigrafia-importazione.png"));
            importDialog.BrowseCommand.Execute(null);
            var extraction = importDialog.ExtractCommand.ExecuteAsync(null);
            for (int i = 0; i < 200 && !extraction.IsCompleted; i++) Wait(20);
            Wait(50);
            Check(extraction.IsCompletedSuccessfully && importDialog.File == pdf && importDialog.Profiles.Count == 1 && importDialog.SelectedProfileIndex == 0, "Scripted draft extracted from the chosen file");
            Check(Children<ListBox>(dialog).Single().Items.Count == 2, "AI draft preview uses actual editor");
            Check(data["import_source"] is null, "Draft changed source before applying");
            CapturePage(dialog, Path.Combine(output, "stratigrafia-anteprima.png"));
            var review = Children<CheckBox>(dialog).Single(c => c.Content?.ToString()?.StartsWith("Ho confrontato") == true);
            review.IsChecked = true; Wait(20);
            Check(importDialog.Reviewed && Apply().IsEnabled, "Reviewed draft can be applied");
            var name = Children<TextBox>(dialog).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Nome della stratigrafia");
            name.Text = "Bozza corretta"; Wait(20);
            Check(review.IsChecked == false && !importDialog.Reviewed, "Editing draft invalidates review");
            Check(data["name"]!.ToString() != "Bozza corretta", "Preview edits changed original");
            importDialog.Dismiss();
        }
        finally { dialog.Close(); }
    }

    /// <summary>A window outside the screen, never activated: the checks never take the mouse or the keyboard of the user.</summary>
    static Window Offscreen(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -32000; window.Top = -32000;
        window.ShowActivated = false; window.ShowInTaskbar = false;
        return window;
    }

    /// <summary>The drafts of the AI import, scripted: never the network.</summary>
    sealed class ScriptedDrafts(JsonObject draft) : IStratigraphyDraftSource
    {
        public Task<IReadOnlyList<JsonObject>> ExtractAsync(string path, string apiKey, string model, CancellationToken cancellation) =>
            Task.FromResult<IReadOnlyList<JsonObject>>([draft.DeepClone().AsObject()]);
    }

    /// <summary>«Scegli PDF…» answered with the file of the check: never a window of the system.</summary>
    sealed class ScriptedFile(string path) : IFileDialogService
    {
        public string? OpenFile(OpenFileRequest request) => path;
        public string? SaveFile(SaveFileRequest request) => throw new InvalidOperationException("Salvataggio non previsto.");
        public string? ChooseFolder(string? title = null) => throw new InvalidOperationException("Cartella non prevista.");
    }
}
