using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeProjects(string directory)
    {
        testing = true; Directory.CreateDirectory(directory);
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray()));
        var project = J.Obj(("nome", "Progetto di prova"), ("strutture", new JsonArray()));
        document.Array("progetti").Add(project);
        var main = CreateProjectSection(project, "Opera principale");
        var first = CreateProjectSection(main, "Spalla A");
        var deep = CreateProjectSection(first, "Fondazioni");
        var other = CreateProjectSection(main, "Spalla B");
        var sheet = J.Obj(("nome", "Palo 1"), ("modulo_id", "geo_palo_verticale"), ("dati", Archivio.NuovoFoglio("geo_palo_verticale")));
        first.Array("fogli").Add(sheet);
        ShowProjects();
        var pn = (TreeViewItem)tree.Items[0]; var mn = (TreeViewItem)pn.Items[0]; var sn = (TreeViewItem)mn.Items[0];
        var fn = (TreeViewItem)sn.Items[1]; fn.IsSelected = true;
        Check(ReferenceEquals(body.Content, dashboardViewport), "La selezione apre il foglio e impedisce il trascinamento");
        fn.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent });
        Check(ReferenceEquals(currentSheet, sheet), "Doppio clic non apre il foglio");
        editor!.SetGeometryForSmoke("1.25"); ShowProjects();
        pn = (TreeViewItem)tree.Items[0]; mn = (TreeViewItem)pn.Items[0]; sn = (TreeViewItem)mn.Items[0];
        var destination = (TreeViewItem)sn.Items[0];
        var drop = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, [new DataObject(SheetDragFormat, sheet), DragDropKeyStates.None,
            DragDropEffects.Move, destination, new Point(10, 10)], null)!; drop.RoutedEvent = DragDrop.DropEvent;
        destination.RaiseEvent(drop);
        Check(drop.Effects == DragDropEffects.Move && first.Array("fogli").Count == 0 && ReferenceEquals(deep.Array("fogli")[0], sheet), "Drop non sposta il foglio nella sottosezione");
        Check(sheet["dati"]!["generali"].S("diametro") == "1.25", "Spostamento perde input modificati");
        MoveProjectSheet(sheet, other); MoveProjectSheet(sheet, other);
        Check(other.Array("fogli").Count == 1 && deep.Array("fogli").Count == 0, "Spostamento duplica il foglio");
        string file = Path.Combine(directory, "progetti.programma"); Archivio.Scrivi(file, document);
        Check(JsonNode.DeepEquals(Archivio.Leggi(file), document), "Round trip gerarchia non riuscito");
        sheet["modulo_id"] = "inesistente";
        bool rejected = false; try { Archivio.Valida(document); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Validazione non controlla i fogli annidati"); sheet["modulo_id"] = "geo_palo_verticale";
        var legacy = (JsonObject)document.DeepClone();
        legacy.Array("progetti")[0]!["strutture"] = new JsonArray(J.Obj(("nome", "Vecchia struttura"), ("fogli", new JsonArray())));
        Archivio.Valida(legacy);
        ShowProjects(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "progetti.png"), Ui.Snapshot(this));
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK: sottosezioni, selezione, doppio clic, drop, conservazione input, spostamenti senza duplicati, salvataggio/riapertura, validazione ricorsiva e archivi precedenti.");
    }
}
