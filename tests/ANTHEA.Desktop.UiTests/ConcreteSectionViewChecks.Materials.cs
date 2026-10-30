using System.IO;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anthea.Presentation.Tests;

namespace X.Desktop;

internal static partial class ConcreteSectionViewChecks
{
    private static async Task BarMaterialEditors(string root, string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(mode, false);
            using var host = new Hosted(Editors(new Services()).Open(Module, ConcreteSectionScenarios.FixtureData(root, "default")), mode != AppAppearance.Light, 1600, 1000);
            await host.Settle();
            var bars = host.View.BarsGrid;
            Choose(bars, "materiale", "B450A");
            await host.Settle();
            Check(host.Sheet.Input["barre_manuali"]![0]!["acciaio"]!["materiale_acciaio_nome"]!.ToString() == "B450A",
                mode + ": ComboBox della barra aggiorna il materiale salvato");
            File.WriteAllBytes(Path.Combine(folder, $"barre-{mode}.png"), host.Png());
            host.Sheet.Inputs.Cable.Set("x", "0", true); host.Sheet.Inputs.Cable.Set("y", "0", true);
            host.Sheet.Inputs.AddCableCommand.Execute(null);
            var tab = CompositeSectionViewChecks.Walk(host.Root).OfType<TabItem>().First(t => t.Header?.ToString() == "Trefoli / cavi");
            tab.IsSelected = true;
            await host.Settle();
            var tendons = host.View.TendonsGrid;
            Check(tendons.Columns.Take(5).Select(c => c.SortMemberPath).SequenceEqual(["id", "x", "y", "materiale", "sigma0"]),
                mode + ": materiale e tensione iniziale accanto alle coordinate");
            Choose(tendons, "materiale", "Y1770");
            var sigma = tendons.Columns.Single(c => c.SortMemberPath == "sigma0");
            Edit(tendons, sigma);
            var box = sigma.GetCellContent(tendons.Items[0]) as TextBox
                ?? throw new InvalidOperationException("Editor σp0 non trovato");
            box.Text = "900";
            box.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            tendons.CommitEdit(DataGridEditingUnit.Cell, true); tendons.CommitEdit(DataGridEditingUnit.Row, true);
            await host.Settle();
            Check(host.Sheet.Settings["trefoli"]![0]!["materiale"]!.ToString() == "Y1770"
                && host.Sheet.Settings["trefoli"]![0]!["sigma0"]!.ToString() == "900", mode + ": editor WPF del materiale e σp0 persistono entrambi");
            tendons.ScrollIntoView(tendons.Items[0], sigma); host.Layout();
            File.WriteAllBytes(Path.Combine(folder, $"trefoli-{mode}.png"), host.Png());
        }
        Appearance.Set(AppAppearance.Light, false);

        static void Edit(DataGrid grid, DataGridColumn column)
        {
            grid.SelectedIndex = 0; grid.ScrollIntoView(grid.Items[0], column); grid.UpdateLayout();
            grid.CurrentCell = new DataGridCellInfo(grid.Items[0], column);
            Check(grid.BeginEdit(), "Inizio modifica della cella " + column.Header);
            grid.UpdateLayout();
        }
        static void Choose(DataGrid grid, string key, string value)
        {
            var column = grid.Columns.Single(c => c.SortMemberPath == key);
            Check(column is DataGridComboBoxColumn && !column.IsReadOnly, "Colonna materiale con scelta modificabile");
            Edit(grid, column);
            var combo = column.GetCellContent(grid.Items[0]) as ComboBox
                ?? throw new InvalidOperationException("ComboBox del materiale non trovata");
            Check(combo.Items.Contains(value), "Materiale presente nelle scelte: " + value);
            combo.SelectedItem = value;
            combo.GetBindingExpression(Selector.SelectedItemProperty)!.UpdateSource();
            grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
        }
    }
}
