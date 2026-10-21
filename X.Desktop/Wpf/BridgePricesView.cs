using Anthea.Application.BridgeDesign;
using Anthea.Presentation.Modules.BridgeDesign;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using X.Core;
namespace X.Desktop;

internal static class BridgePricesView
{
    internal static Window Create(FrameworkElement owner, PriceCatalogueViewModel vm)
    {
        Window? dialog = null;
        Button Command(string label, ICommand command)
        { var b = Ui.Button(label, () => { }); b.Command = command; return b; }
        TextBox Editor(string binding)
        { var t = new TextBox { MinWidth = 180, Margin = new Thickness(4) }; t.SetBinding(TextBox.TextProperty, new Binding(binding) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); return t; }
        DataGrid Table(object source) => new() { ItemsSource = (System.Collections.IEnumerable)source, AutoGenerateColumns = false,
            CanUserAddRows = false, CanUserDeleteRows = false, EnableRowVirtualization = true, EnableColumnVirtualization = true,
            SelectionMode = DataGridSelectionMode.Single, MinHeight = 220, RowHeaderWidth = 0, Margin = new Thickness(0, 8, 0, 8) };
        void Column(DataGrid g, string key, string title, bool readOnly = false, double width = 140)
        { g.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(key) { Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, IsReadOnly = readOnly, Width = width }); }
        var mapping = Table(vm.Mappings);
        Column(mapping, "Label", "Lavorazione", true, 220); Column(mapping, "Unit", "Unità", true, 65);
        Column(mapping, "EntryCode", "Codice prezzario"); Column(mapping, "OverridePrice", "Prezzo personalizzato");
        Column(mapping, "Reason", "Motivo personalizzazione", false, 195); Column(mapping, "PreviousPrice", "Prima", true, 100);
        Column(mapping, "ResolvedPrice", "Dopo", true, 100); Column(mapping, "Status", "Esito associazione", true, 230);
        var entries = Table(vm.Entries); entries.SetBinding(DataGrid.SelectedItemProperty, new Binding(nameof(vm.SelectedEntry)) { Mode = BindingMode.TwoWay });
        Column(entries, "Code", "Codice"); Column(entries, "Description", "Descrizione", false, 280);
        entries.Columns.Add(new DataGridComboBoxColumn { Header = "Unità", ItemsSource = vm.Units,
            SelectedItemBinding = new Binding("Unit") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = 75 });
        Column(entries, "Price", "Prezzo unitario"); Column(entries, "Includes", "Lavorazioni comprese (codici separati da |)", false, 330);
        var analyses = Table(vm.Analyses); analyses.SetBinding(DataGrid.SelectedItemProperty, new Binding(nameof(vm.SelectedAnalysis)) { Mode = BindingMode.TwoWay });
        Column(analyses, "Code", "Codice"); Column(analyses, "Description", "Descrizione", false, 240);
        analyses.Columns.Add(new DataGridComboBoxColumn { Header = "Unità", ItemsSource = vm.Units,
            SelectedItemBinding = new Binding("Unit") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = 75 });
        Column(analyses, "Components", "Componenti (codice = quantità; …)", false, 360); Column(analyses, "GeneralExpenses", "Spese generali %\n(vuoto = prezzario)", false, 150);
        Column(analyses, "Profit", "Utile %\n(vuoto = prezzario)", false, 130); Column(analyses, "UnitPrice", "Prezzo unitario", true, 130);
        var breakdown = Ui.Text("", 12); breakdown.TextWrapping = TextWrapping.Wrap; breakdown.SetBinding(TextBlock.TextProperty, nameof(vm.AnalysisBreakdown));
        var preview = Ui.Text("", 16, true); preview.SetBinding(TextBlock.TextProperty, nameof(vm.CostPreview));
        var status = Ui.Text("", 12); status.TextWrapping = TextWrapping.Wrap; status.SetBinding(TextBlock.TextProperty, nameof(vm.Status));
        void Commit() { foreach (var g in new[] { mapping, entries, analyses }) { g.CommitEdit(DataGridEditingUnit.Cell, true); g.CommitEdit(DataGridEditingUnit.Row, true); } }
        void Run(Action action)
        { try { Commit(); action(); } catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException) { vm.Status = e.Message; } }
        void Import()
        {
            var file = new OpenFileDialog { Filter = "Prezzari|*.csv;*.xlsx;*.json|CSV|*.csv|Excel|*.xlsx|Catalogo ANTHEA|*.json" };
            if (file.ShowDialog(dialog) == true) Run(() => { if (new FileInfo(file.FileName).Length > 20_000_000) throw new ArgumentException("Massimo 20 MB."); vm.Import(File.ReadAllBytes(file.FileName), file.FileName); });
        }
        void Save(bool template)
        {
            var file = new SaveFileDialog { Filter = template ? "Modello CSV|*.csv" : "Catalogo ANTHEA|*.json", FileName = template ? "ANTHEA_Prezzario.csv" : "ANTHEA_Prezzario.json" };
            if (file.ShowDialog(dialog) == true) Run(() => Archivio.ScriviAtomico(file.FileName, template ? Encoding.UTF8.GetBytes(PriceCatalogueImport.Template()) : vm.ExportCatalogue()));
        }
        var keep = new CheckBox { Content = "Mantieni i prezzi personalizzati quando importi un altro prezzario", Margin = new Thickness(4, 8, 4, 8) };
        keep.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(vm.KeepOverrides)) { Mode = BindingMode.TwoWay });
        var reprice = new CheckBox { Content = "Rivaluta anche l'alternativa A con questo prezzario", Margin = new Thickness(4, 4, 4, 8) };
        reprice.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(vm.RepricePinnedAlternative)) { Mode = BindingMode.TwoWay });
        reprice.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(vm.HasPinnedAlternative)) { Converter = new BooleanToVisibilityConverter() });
        var tabs = new TabControl();
        Ui.Tab(tabs, "Associazioni e anteprima", Ui.Dock(mapping, top: Ui.Text("I prezzi personalizzati sono espressi nell'unità della lavorazione. Lasciare vuoto per usare il catalogo; zero è un prezzo esplicito.", 12), bottom: preview));
        Ui.Tab(tabs, "Voci del prezzario", Ui.Dock(entries, top: Ui.Bar(Command("+ Voce", vm.AddEntryCommand), Command("Elimina voce", vm.RemoveEntryCommand)),
            bottom: Ui.Text("Inclusioni: usare i codici delle lavorazioni, per esempio rebar sul prezzo del palo. Si applicano solo alle quantità figlie del relativo palo, mai a tutte le armature.", 12)));
        Ui.Tab(tabs, "Analisi composte", Ui.Dock(analyses,
            top: Ui.Stack(Ui.Bar(Ui.Text("Spese generali %", 12), Editor(nameof(vm.GeneralExpensesPercent)), Ui.Text("Utile d'impresa %", 12), Editor(nameof(vm.ProfitPercent)),
                    Command("+ Analisi", vm.AddAnalysisCommand), Command("Elimina analisi", vm.RemoveAnalysisCommand)),
                Ui.Text("Prezzo = componenti + spese generali sui componenti + utile su componenti e spese generali. Le quantità sono nell'unità del componente (per esempio ore di manodopera per m³); un componente può essere un'altra analisi. Il codice di un'analisi si associa alle lavorazioni come una voce.", 12)),
            bottom: breakdown));
        var revisions = new ListBox { ItemsSource = vm.History, DisplayMemberPath = "Caption", MinHeight = 200 };
        revisions.SetBinding(ListBox.SelectedItemProperty, new Binding(nameof(vm.SelectedRevision)) { Mode = BindingMode.TwoWay });
        Ui.Tab(tabs, "Revisioni", Ui.Dock(revisions, bottom: Ui.Stack(Ui.Text("Il ripristino prepara una nuova anteprima; la cronologia rimane conservata.", 12), Command("Prepara ripristino", vm.RestoreRevisionCommand))));
        var apply = Command("Applica prezzario", vm.ApplyCommand);
        var header = Ui.Stack(Ui.Text("PREZZARIO DEL PONTE", 21, true),
            Ui.Text("Catalogo sostituibile, associazioni alle lavorazioni e prezzi personalizzati. Valuta EUR, IVA esclusa. Le modifiche diventano attive soltanto con Applica.", 12),
            Ui.Bar(Ui.Text("Nome", 12), Editor(nameof(vm.CatalogueName)), Ui.Text("Edizione", 12), Editor(nameof(vm.Edition))),
            Ui.Bar(Ui.Button("Importa CSV / Excel / JSON", Import), Ui.Button("Modello CSV", () => Save(true)), Ui.Button("Esporta catalogo JSON", () => Save(false))), keep, reprice,
            Ui.Text("Excel: foglio Prezzario, intestazioni Codice · Descrizione · Unita · Prezzo · Inclusioni. Prezzi senza separatori delle migliaia.", 11, color: Ui.Muted));
        var content = Ui.Dock(tabs, top: header, bottom: Ui.Stack(status, Ui.Bar(apply, Ui.Button("Annulla", () => dialog!.Close()))));
        dialog = Ui.Dialog(owner, "Bridge Design · Gestione prezzari", content, 1350, 780);
        dialog.Background = Appearance.Surface; dialog.DataContext = vm;
        var body = (UIElement)dialog.Content; dialog.Content = null; dialog.Content = new Border { Background = Appearance.Surface, Padding = new Thickness(16), Child = body };
        vm.Applied += () => dialog.Close();
        return dialog;
    }
}
