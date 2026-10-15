using System.Reflection;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.Stratigraphy;

static partial class Program
{
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
        var type = assembly.GetType("X.Desktop.SheetEditor")!;
        var editor = (FrameworkElement)Activator.CreateInstance(type, Flags, null, [StratigraphyDocument.Module, data, null], null)!;
        using var disposable = (IDisposable)editor;
        var host = new Window { Content = editor, Width = 1280, Height = 900, ShowActivated = false, ShowInTaskbar = false };
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
            Call(editor, "Commit"); checks++;
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
        var owner = new Window { Width = 300, Height = 200, ShowActivated = false, ShowInTaskbar = false };
        owner.Show();
        var transfer = (Window)Activator.CreateInstance(transferType, Flags, null, [owner, StratigraphyDocument.Module, data, sourceRows], null)!;
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
        var dialogType = assembly.GetType("X.Desktop.StratigraphyImportWindow")!;
        var dialog = (Window)Activator.CreateInstance(dialogType, Flags, null, [], null)!;
        try
        {
            dialog.Show(); Wait(50);
            Check(Children<Button>(dialog).Single(b => b.Content?.ToString()?.StartsWith("Sostituisci la stratigrafia") == true).IsEnabled == false, "Import cannot apply before review");
            CapturePage(dialog, Path.Combine(output, "stratigrafia-importazione.png"));
            var draft = data.DeepClone().AsObject(); draft["import_source"] = new JsonObject { ["warnings"] = "Fixture: confrontare pagine e unità", ["reviewed"] = false };
            dialogType.GetField("drafts", Flags)!.SetValue(dialog, new List<JsonObject> { draft });
            var selector = (ComboBox)dialogType.GetField("profiles", Flags)!.GetValue(dialog)!;
            selector.ItemsSource = new[] { "S1 · Bozza di prova" }; selector.SelectedIndex = 0; Wait(50);
            Check(Children<ListBox>(dialog).Single().Items.Count == 2, "AI draft preview uses actual editor");
            Check(data["import_source"] is null, "Draft changed source before applying");
            CapturePage(dialog, Path.Combine(output, "stratigrafia-anteprima.png"));
            var review = (CheckBox)dialogType.GetField("reviewed", Flags)!.GetValue(dialog)!;
            review.IsChecked = true; Call(dialog, "UpdateApply");
            Check(Children<Button>(dialog).Single(b => b.Content?.ToString()?.StartsWith("Sostituisci la stratigrafia") == true).IsEnabled, "Reviewed draft can be applied");
            var name = Children<TextBox>(dialog).Single(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Nome della stratigrafia");
            name.Text = "Bozza corretta";
            Check(review.IsChecked == false, "Editing draft invalidates review");
            Check(data["name"]!.ToString() != "Bozza corretta", "Preview edits changed original");
        }
        finally { dialog.Close(); }
    }
}
