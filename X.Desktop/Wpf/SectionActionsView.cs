using Anthea.Application.Concrete;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal static class SectionActionsView
{
    internal static Window Create(FrameworkElement owner, SectionActionsViewModel vm)
    {
        var grid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false,
            ItemsSource = vm.Rows, SelectionMode = DataGridSelectionMode.Single, EnableRowVirtualization = true, MinHeight = 250 };
        grid.SetBinding(DataGrid.SelectedItemProperty, new Binding(nameof(vm.Selected)) { Mode = BindingMode.TwoWay });
        foreach (var (key, label) in new[] { ("Element", "Elemento"), ("Name", "Nome combo"), ("State", "Stato limite"), ("N", "N [kN]"), ("Vx", "Vx [kN]"), ("Vy", "Vy [kN]"), ("T", "T [kNm]"), ("Mx", "Mx [kNm]"), ("My", "My [kNm]"), ("Origin", "Origine precedente") })
        {
            var binding = new Binding(key) { Mode = key == "Origin" ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
            if (key == "State") grid.Columns.Add(new DataGridComboBoxColumn { Header = label, ItemsSource = vm.States, SelectedItemBinding = binding, MinWidth = 145, Width = 155 });
            else grid.Columns.Add(new DataGridTextColumn { Header = label, Binding = binding, IsReadOnly = key == "Origin", MinWidth = key == "Name" ? 130 : 80, Width = new DataGridLength(key is "Name" or "Origin" ? 1.6 : 1, DataGridLengthUnitType.Star) });
        }
        var origin = grid.Columns[^1];
        void ShowOrigin() => origin.Visibility = vm.HasOrigins ? Visibility.Visible : Visibility.Collapsed;
        ShowOrigin(); vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.HasOrigins)) ShowOrigin(); };
        var status = Ui.Text("", 12); status.SetBinding(TextBlock.TextProperty, nameof(vm.Status));
        var notice = Ui.Text(vm.MigrationNotice, 12); notice.TextWrapping = TextWrapping.Wrap;
        notice.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(vm.HasOrigins)) { Converter = new BooleanToVisibilityConverter() });
        Window? dialog = null;
        void Commit() { grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true); }
        void Run(Action action) { try { Commit(); action(); } catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException) { vm.Status = e.Message; } }
        Button Command(string label, System.Windows.Input.ICommand command)
        { var button = Ui.Button(label, () => { }); button.Command = command; return button; }
        void Import(bool replace)
        {
            var file = new OpenFileDialog { Filter = "Cartella Excel|*.xlsx" };
            if (file.ShowDialog(dialog) != true) return;
            Run(() => { if (new FileInfo(file.FileName).Length > 20_000_000) throw new ArgumentException("Massimo 20 MB."); vm.Import(File.ReadAllBytes(file.FileName), replace); });
        }
        void Save(bool template)
        {
            var file = new SaveFileDialog { Filter = "Cartella Excel|*.xlsx", FileName = template ? "ANTHEA_Sollecitazioni.xlsx" : "ANTHEA_Sollecitazioni_compilate.xlsx" };
            if (file.ShowDialog(dialog) == true) Run(() => { Archivio.ScriviAtomico(file.FileName, template ? SectionActionsExcel.Template() : vm.Export()); vm.Status = "Excel salvato."; });
        }
        var commands = Ui.Bar(Command("+ Riga", vm.AddCommand), Command("Duplica", vm.DuplicateCommand), Command("Elimina", vm.RemoveCommand), Command("Vuoti a zero · riga selezionata", vm.FillMissingWithZeroCommand));
        var files = Ui.Bar(Ui.Button("Template Excel", () => Save(true)), Ui.Button("Importa e sostituisci", () => Import(true)), Ui.Button("Importa e aggiungi", () => Import(false)), Ui.Button("Esporta Excel", () => Save(false)));
        var footer = Ui.Stack(status, Ui.Bar(Ui.Button("Applica", () => { Commit(); if (vm.Apply()) dialog!.DialogResult = true; }, true), Ui.Button("Annulla", () => dialog!.DialogResult = false)));
        dialog = Ui.Dialog(owner, "Sollecitazioni della sezione", Ui.Dock(grid, top: Ui.Stack(Ui.Text(vm.Guidance, 12), notice, commands, files), bottom: footer), 1280, 720);
        dialog.Background = Appearance.Surface;
        ((FrameworkElement)dialog.Content).Margin = new Thickness(14);
        dialog.DataContext = vm;
        return dialog;
    }
}
