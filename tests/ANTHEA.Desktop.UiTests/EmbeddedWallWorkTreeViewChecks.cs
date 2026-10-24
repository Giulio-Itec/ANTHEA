using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Anthea.Presentation.Modules.EmbeddedWalls;
using X.Desktop.Views.EmbeddedWalls;

namespace X.Desktop;

internal static partial class EmbeddedWallViewChecks
{
    private static async Task CheckWorkTree(FrameworkElement view, EmbeddedWallSheetViewModel sheet,
        Func<Task> settle, Action<bool, string> check, Action<string> capture)
    {
        var tree = Visual(view).OfType<TreeView>().Single(t => t.Name == "WorkTreeView");
        var pane = Visual(view).OfType<Border>().Single(t => t.Name == "WorkTreePane");
        var rail = Visual(view).OfType<Border>().Single(t => t.Name == "WorkTreeRail");
        var grid = (Grid)pane.Parent;
        var sketch = Visual(view).OfType<EmbeddedWallModelSketch>().Single();
        string before = sheet.Data.ToJsonString(); long revision = sheet.Revision;
        check(tree.Items.Count >= 5 && pane.IsVisible && !rail.IsVisible, "Albero di lavoro o gruppi iniziali assenti.");
        var soil = sheet.Tables.Single(t => t.Key == "left_soils").Rows[0];
        var item = Visual(tree).OfType<TreeViewItem>().Single(t => t.DataContext is EmbeddedWallWorkNode n && n.Row == soil);
        item.IsSelected = true; await settle();
        check(sheet.InspectorTable?.Key == "left_soils" && sheet.InspectorTable.Selected == soil,
            "Selezione WPF dell'albero non apre la riga.");
        var selected = (EmbeddedWallWorkNode)item.DataContext;
        check(selected.IsSelected && ReferenceEquals(tree.SelectedItem, selected), "Selezione WPF e ViewModel disallineati.");
        sheet.WorkspaceTab = 3; await settle();
        item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            { RoutedEvent = Mouse.PreviewMouseUpEvent, Source = item });
        await settle();
        check(sheet.WorkspaceTab == 0 && sheet.InspectorTable?.Selected == soil,
            "Un nuovo clic sulla voce già selezionata non torna dai risultati all'editor.");
        capture("albero-modello");
        var soils = sheet.WorkTree.Single(n => n.Title == "Terreni");
        soils.IsExpanded = false; await settle();
        var soilContainer = Visual(tree).OfType<TreeViewItem>().Single(t => ReferenceEquals(t.DataContext, soils));
        check(!soilContainer.IsExpanded, "Chiusura del ramo non applicata alla vista.");
        grid.ColumnDefinitions[0].Width = new GridLength(340); await settle();
        double openWidth = sketch.ActualWidth;
        var close = Visual(pane).OfType<Button>().Single(b => ReferenceEquals(b.Command, sheet.ToggleWorkTreeCommand));
        close.Command!.Execute(close.CommandParameter); await settle();
        check(!pane.IsVisible && rail.IsVisible && sheet.IsWorkTreeCollapsed, "Il pulsante non richiude il pannello.");
        check(grid.ColumnDefinitions[0].ActualWidth <= 39 && sketch.ActualWidth > openWidth + 250,
            "La chiusura non libera spazio dopo il ridimensionamento del pannello.");
        capture("albero-richiudibile");
        var open = Visual(rail).OfType<Button>().Single();
        open.Command!.Execute(open.CommandParameter); await settle();
        check(pane.IsVisible && !rail.IsVisible && Math.Abs(grid.ColumnDefinitions[0].ActualWidth - 340) < 1,
            "La riapertura perde la larghezza scelta.");
        check(!soils.IsExpanded, "La riapertura perde i rami richiusi.");
        soils.IsExpanded = true; grid.ColumnDefinitions[0].Width = new GridLength(270); await settle();
        Appearance.Set(AppAppearance.Dark, false); await settle(); capture("albero-scuro");
        Appearance.Set(AppAppearance.VeryDark, false); await settle(); capture("albero-molto-scuro");
        Appearance.Set(AppAppearance.Light, false); await settle();
        check(sheet.Data.ToJsonString() == before && sheet.Revision == revision && sheet.Current is null,
            "L'albero o il cambio aspetto modifica gli input o avvia un calcolo.");
        sheet.EditModelCommand.Execute("general"); await settle();
    }
}
