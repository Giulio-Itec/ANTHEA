using System.IO;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.BridgeDesign;
using Anthea.Presentation.Modules.BridgeDesign;
namespace X.Desktop;
public sealed partial class MainWindow
{
    internal async Task SmokeBridgePrices(string directory)
    {
        Directory.CreateDirectory(directory); int checks = 0; var previous = Appearance.Current;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        try
        {
            foreach (var mode in Enum.GetValues<AppAppearance>())
            {
                Appearance.Set(mode, false);
                var vm = new PriceCatalogueViewModel(BridgeDesignModel.Defaults());
                var dialog = BridgePricesView.Create(this, vm);
                try
                {
                    dialog.Show(); await Dispatcher.InvokeAsync(() => dialog.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    var tabs = Ui.Descendants<TabControl>(dialog).Single();
                    // Mappings, catalogue entries, composite analyses (F4-45) and revisions.
                    Check(tabs.Items.Count == 4 && vm.CanApply, "Price editor tabs/preview unavailable");
                    var grid = Ui.Descendants<DataGrid>(dialog).Single(); Check(grid.Items.Count == 16, "Work mappings not bound");
                    var codeColumn = (DataGridTextColumn)grid.Columns[2]; Check(codeColumn.Binding is System.Windows.Data.Binding { Mode: System.Windows.Data.BindingMode.TwoWay }, "Editable work code not bound");
                    vm.Mappings.Single(m => m.WorkCode == "concrete_deck").EntryCode = "MISSING";
                    Check(!vm.CanApply, "Missing mapped price still applies");
                    vm.Mappings.Single(m => m.WorkCode == "concrete_deck").EntryCode = "concrete_deck";
                    Check(vm.CanApply, "Price correction did not recover preview");
                    File.WriteAllBytes(Path.Combine(directory, "prices-" + mode + ".png"), Ui.Snapshot(dialog));
                    tabs.SelectedIndex = 1; dialog.UpdateLayout();
                    var entries = Ui.Descendants<DataGrid>(dialog).Single(); int count = entries.Items.Count;
                    vm.AddEntryCommand.Execute(null); dialog.UpdateLayout(); Check(entries.Items.Count == count + 1, "Add catalogue entry not bound");
                    vm.RemoveEntryCommand.Execute(null); Check(entries.Items.Count == count, "Remove catalogue entry not bound");
                    File.WriteAllBytes(Path.Combine(directory, "catalogue-" + mode + ".png"), Ui.Snapshot(dialog));
                    // Composite analyses (F4-45): table bound to the view model; the first analysis makes the price-list percentages explicit.
                    tabs.SelectedIndex = 2; dialog.UpdateLayout();
                    var analyses = Ui.Descendants<DataGrid>(dialog).Single(); int analysisCount = analyses.Items.Count;
                    vm.AddAnalysisCommand.Execute(null); dialog.UpdateLayout();
                    Check(analyses.Items.Count == analysisCount + 1 && vm.GeneralExpensesPercent == "15" && vm.ProfitPercent == "10", "Add composite analysis not bound");
                    File.WriteAllBytes(Path.Combine(directory, "analyses-" + mode + ".png"), Ui.Snapshot(dialog));
                    vm.RemoveAnalysisCommand.Execute(null); dialog.UpdateLayout();
                    Check(analyses.Items.Count == analysisCount && vm.CanApply, "Remove composite analysis not bound");
                    vm.ApplyCommand.Execute(null); Check(vm.Result is not null && !dialog.IsVisible, "Apply did not close owned draft");
                }
                finally { dialog.Close(); }
            }
        }
        finally { Appearance.Set(previous, false); }
        File.WriteAllText(Path.Combine(directory, "bridge-prices-checks.txt"), $"PASS {checks} WPF controls");
    }
}
