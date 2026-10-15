using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using X.Core;

namespace X.Desktop;
public sealed partial class MainWindow
{
    internal async Task SmokeSectionActions(string directory)
    {
        Directory.CreateDirectory(directory);
        document = DocumentService.CreateDocument("str_palo"); currentSheet = null;
        var data = document["dati"]!.AsObject();
        var vm = new SectionActionsViewModel(data);
        vm.Import(SectionActionsExcel.Write([new("SLU", "C1", -100, 10, 5, 20, 30, 0), new("SLV", "C1", -90, 8, 3, 12, 24, 0, "P02"), new("SLE", "Rara 1", -60, 6, 2, 8, 10, 0)]), true);
        if (!vm.Apply()) throw new Exception(vm.Status);
        document["dati"] = vm.Result!.DeepClone(); ShowSheet(document); await editor!.CalculateAsync();
        await editor.VerifySectionActions(directory);
        dirty = false;
    }
}
internal sealed partial class SheetEditor
{
    internal Task VerifySectionActions(string directory) => concrete!.VerifySectionActions(directory);
}
internal sealed partial class ConcreteWorkspace
{
    internal async Task VerifySectionActions(string directory)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        Check(UnifiedActions && actions["SLU"].Count == 1 && actions["SLV"].Count == 1 && actions["SLE"].Count == 1, "Instradamento nelle schede");
        Check(shearGrid!.Rows.Count == 2 && shearGrid.IsReadOnly, "Taglio/torsione devono leggere SLU e SLV");
        Check(shearGrid.Rows.Any(r => r.Values.S("elemento") == "P02"), "Elemento perso in vista");
        Check(shearResults.Count == 2 && domainResults["3D:SLU"].Count == 1 && domainResults["3D:SLV"].Count == 1, "Verifiche non alimentate");
        Commit(); Check(settings["taglio"]!.Array("azioni").Count == 0 && Data["combinazioni"]!["SLU"]![0]!.Array("azioni").Count == 6, "Commit duplica/perde dati");
        var panel = domainPanels[0]; tabs.SelectedIndex = 1;
        Check(panel.Mode.ItemTemplate is null && panel.Form.Editors.ContainsKey("modello_resistente"), "Criterio non separato dallo stato limite");
        var first = checker3D["SLU"]; panel.Form.Set("modello_resistente", "Elastico"); await CalculateAllAsync();
        Check(!ReferenceEquals(first, checker3D["SLU"]), "Cambio criterio non invalida dominio");
        panel.Form.Set("modello_resistente", "Plastico"); await CalculateAllAsync();
        UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "dominio-slu.png"), Ui.Snapshot(Window.GetWindow(this)));
        tabs.SelectedIndex = 4; UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "taglio-slu-slv.png"), Ui.Snapshot(Window.GetWindow(this)));
        var vm = new SectionActionsViewModel(Data); var dialog = SectionActionsView.Create(this, vm);
        try
        {
            dialog.Show(); dialog.UpdateLayout();
            var grid = Ui.Descendants<DataGrid>(dialog).Single();
            Check(grid.Items.Count == 3 && grid.Columns[2] is DataGridComboBoxColumn, "Griglia Excel non collegata");
            Check(grid.Columns[^1].Visibility == Visibility.Collapsed, "Origine legacy mostrata per nuovi dati");
            vm.Selected = vm.Rows[0]; vm.DuplicateCommand.Execute(null); dialog.UpdateLayout(); Check(grid.Items.Count == 4, "Binding comando/righe");
            File.WriteAllBytes(Path.Combine(directory, "editor-sollecitazioni.png"), Ui.Snapshot(dialog));
        }
        finally { dialog.Close(); }
        File.WriteAllBytes(Path.Combine(directory, "report.docx"), BuildReport("Azioni unificate", ["azioni", "dominio3d", "taglio"]));
        var copy = (JsonObject)Data.DeepClone(); using var reopened = new ConcreteWorkspace(copy); reopened.Commit();
        Check(JsonNode.DeepEquals(Data["combinazioni"], copy["combinazioni"]), "Riapertura perde componenti");
        File.WriteAllText(Path.Combine(directory, "section-actions-checks.txt"), $"PASS {checks} controlli WPF");
    }
}
