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
        var fn = (TreeViewItem)sn.Items[0]; fn.IsSelected = true;
        Check(ReferenceEquals(body.Content, dashboardViewport), "La selezione apre il foglio e impedisce il trascinamento");
        var thumbnail = ((StackPanel)fn.Header).Children.OfType<Button>().Single();
        thumbnail.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(ReferenceEquals(currentSheet, sheet), "Miniatura non apre il foglio");
        editor!.SetGeometryForSmoke("1.25");
        Check(backToOverview.Content.ToString() == "← Torna al progetto", "Pulsante di ritorno non contestuale");
        backToOverview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(ReferenceEquals(body.Content, dashboardViewport) && ReferenceEquals(currentSheet, sheet), "Ritorno non conserva la scheda");

        pn = (TreeViewItem)tree.Items[0]; mn = (TreeViewItem)pn.Items[0]; sn = (TreeViewItem)mn.Items[0];
        var destination = (TreeViewItem)sn.Items[1];
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
        ShowProjects();
        // Creation is immediate, with unique placeholder names and no naming dialogs.
        int count = document.Array("progetti").Count;
        AddProject(); AddProject();
        Check(document.Array("progetti").Count == count + 2, "Creazione immediata progetto");
        Check(document.Array("progetti").Select(p => p.S("nome")).Distinct().Count() == count + 2, "Nomi provvisori duplicati");
        AddStructureTo(main); AddStructureTo(main);
        Check(main.Array("strutture").Select(p => p.S("nome")).Distinct().Count() == main.Array("strutture").Count, "Nomi sezioni duplicati");
        var projectItem = (TreeViewItem)tree.Items[0]; var mainItem = (TreeViewItem)projectItem.Items[0];
        double Font(TreeViewItem node) => ((StackPanel)node.Header).Children.OfType<TextBlock>().Single().FontSize;
        Check(Font(projectItem) > Font(mainItem) && Font(mainItem) > Font((TreeViewItem)mainItem.Items[0]), "Gerarchia grafica non progressiva");
        BeginProjectRename(mainItem);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var rename = ((StackPanel)mainItem.Header).Children.OfType<TextBox>().Single();
        rename.Text = "Opera rinominata";
        rename.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(rename), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
        Check(main.S("nome") == "Opera rinominata", "Rinomina inline non confermata");
        BeginProjectRename(mainItem);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        rename = ((StackPanel)mainItem.Header).Children.OfType<TextBox>().Single(); rename.Text = "Da annullare";
        rename.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(rename), 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
        Check(main.S("nome") == "Opera rinominata", "Escape non annulla rinomina");
        Archivio.Scrivi(file, document); Check(JsonNode.DeepEquals(Archivio.Leggi(file), document), "Nomi e gerarchia non persistono");
        AddSheetTo("str_palo", other, false);
        AddSheetTo("str_palo", other, false);
        var ca1 = other.Array("fogli")[1]!.AsObject(); var ca2 = other.Array("fogli")[2]!.AsObject();
        Check(ca1.S("nome") != ca2.S("nome"), "Nomi automatici schede duplicati");
        MoveProjectSheet(ca2, other, sheet, false);
        Check(ReferenceEquals(other.Array("fogli")[0], ca2), "Riordino verso inizio");
        MoveProjectSheet(ca2, other, ca1, true);
        Check(ReferenceEquals(other.Array("fogli")[2], ca2), "Riordino verso fine");
        ShowProjects(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        var rootItem = (TreeViewItem)tree.Items[0];
        Check(rootItem.ContextMenu.Items.OfType<MenuItem>().Select(m => m.Header?.ToString()).SequenceEqual(new[] { "Rinomina", "Elimina" }), "Menu gruppo contiene azioni extra");
        var otherItem = (TreeViewItem)((TreeViewItem)rootItem.Items[0]).Items[1];
        var caItem = (TreeViewItem)otherItem.Items[1];
        var caButton = ((StackPanel)caItem.Header).Children.OfType<Button>().Single();
        Check(caButton.Content is Viewbox { Width: 36, Height: 36, ClipToBounds: true }, "Miniatura CA non scalata");
        // Exercise the row drop handler, including order within the same list.
        var rowDrop = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
            [new DataObject(SheetDragFormat, ca2), DragDropKeyStates.None, DragDropEffects.Move, caItem, new Point(1, 1)], null)!;
        rowDrop.RoutedEvent = DragDrop.DropEvent; caItem.RaiseEvent(rowDrop);
        Check(rowDrop.Effects == DragDropEffects.Move && ReferenceEquals(other.Array("fogli")[1], ca2), "Drop riga non riordina");
        MoveProjectSheet(ca2, deep, null);
        Check(ReferenceEquals(deep.Array("fogli")[0], ca2), "Spostamento tra gruppi dopo riordino");
        Archivio.Scrivi(file, document); Check(JsonNode.DeepEquals(Archivio.Leggi(file), document), "Ordine non conservato nel file");
        ShowProjects(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        // Drag the group header in both directions, retaining complete subtrees and data.
        TreeViewItem Find(JsonObject value)
        {
            TreeViewItem? Search(ItemsControl parent)
            {
                foreach (TreeViewItem node in parent.Items)
                {
                    if (ReferenceEquals(node.Tag, value)) return node;
                    if (Search(node) is TreeViewItem found) return found;
                }
                return null;
            }
            return Search(tree) ?? throw new Exception("Nodo non trovato: " + value.S("nome"));
        }
        DragEventArgs SectionEvent(JsonObject source, TreeViewItem target, RoutedEvent routedEvent, bool after = false)
        {
            var header = (FrameworkElement)target.Header;
            var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                [new DataObject(SectionDragFormat, source), DragDropKeyStates.None, DragDropEffects.Move, header,
                 new Point(8, after ? header.ActualHeight - 1 : 1)], null)!;
            args.RoutedEvent = routedEvent; target.RaiseEvent(args); return args;
        }
        async Task Reorder(JsonObject source, JsonObject target, bool after, bool capture = false)
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
            var node = Find(target);
            var over = SectionEvent(source, node, DragDrop.DragOverEvent, after);
            Check(over.Effects == DragDropEffects.Move && projectSectionDrop is { } cue && cue.Adorner.After == after, "Indicatore riordino sezioni assente o errato");
            if (capture) File.WriteAllBytes(Path.Combine(directory, "trascinamento-sezioni.png"), Ui.Snapshot(this));
            var args = SectionEvent(source, node, DragDrop.DropEvent, after);
            Check(args.Effects == DragDropEffects.Move && projectSectionDrop is null, "Drop sezione rifiutato o indicatore residuo");
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
            var siblings = (JsonArray)source.Parent!;
            Check(siblings.IndexOf(source) == siblings.IndexOf(target) + (after ? 1 : -1), "Posizione della sezione errata");
            var parent = siblings.Parent as JsonObject;
            var displayed = (ReferenceEquals(parent, document) ? tree.Items : Find(parent!).Items).Cast<TreeViewItem>()
                .Select(n => (JsonObject)n.Tag).Where(n => !n.ContainsKey("modulo_id"));
            Check(displayed.SequenceEqual(siblings.OfType<JsonObject>()), "Ordine delle sezioni a video diverso dal documento");
            Check(tree.SelectedItem is TreeViewItem selected && ReferenceEquals(selected.Tag, source), "Sezione spostata non selezionata");
        }
        var firstSnapshot = first.DeepClone(); var otherSnapshot = other.DeepClone();
        var activeEditor = editor;
        var conflictsBefore = ProjectSharedData.Differences(main).Select(d => (d.Key, d.Left, d.Right)).OrderBy(d => d.Key).ThenBy(d => d.Left).ThenBy(d => d.Right).ToArray();
        await Reorder(first, other, true, true);
        Check(JsonNode.DeepEquals(first, firstSnapshot) && JsonNode.DeepEquals(other, otherSnapshot), "Riordino altera schede o sottosezioni");
        Check(ReferenceEquals(currentSheet, sheet) && ReferenceEquals(editor, activeEditor), "Riordino perde la scheda attiva");
        Check(conflictsBefore.SequenceEqual(ProjectSharedData.Differences(main).Select(d => (d.Key, d.Left, d.Right)).OrderBy(d => d.Key).ThenBy(d => d.Left).ThenBy(d => d.Right)), "Riordino sezioni altera i conflitti");
        await Reorder(first, other, false);
        var secondDeep = CreateProjectSection(first, "Fondazioni secondarie"); RefreshTree();
        await Reorder(secondDeep, deep, false); await Reorder(secondDeep, deep, true);
        var otherProject = document.Array("progetti")[1]!.AsObject();
        await Reorder(project, otherProject, true); await Reorder(project, otherProject, false);
        // Invalid targets and cancellation must leave the archive unchanged.
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        var beforeInvalid = document.DeepClone();
        foreach (var pair in new[] { (first, first), (first, deep), (deep, other), ((JsonObject)first.DeepClone(), other), (first, sheet) })
        {
            var args = SectionEvent(pair.Item1, Find(pair.Item2), DragDrop.DropEvent);
            Check(args.Effects == DragDropEffects.None && JsonNode.DeepEquals(beforeInvalid, document), "Drop non valido ha modificato la gerarchia");
        }
        var targetNode = Find(other); SectionEvent(first, targetNode, DragDrop.DragOverEvent);
        Check(projectSectionDrop is not null, "Indicatore prima dell'annullamento assente");
        SectionEvent(first, targetNode, DragDrop.DragLeaveEvent);
        Check(projectSectionDrop is null && JsonNode.DeepEquals(beforeInvalid, document), "Annullamento lascia indicatore o cambia documento");
        // Pending edits are committed even if reordering while an editor is retained.
        editor!.SetGeometryForSmoke("1.35");
        await Reorder(other, first, false);
        Check(sheet["dati"]!["generali"].D("diametro") == 1.35, "Riordino perde modifiche in sospeso");
        Archivio.Scrivi(file, document); var expectedOrder = document.DeepClone(); dirty = false; LoadFile(file);
        Check(JsonNode.DeepEquals(expectedOrder, document), "Ordine sezioni o contenuti persi alla riapertura");
        ShowProjects(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "progetti.png"), Ui.Snapshot(this));
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK: inserimento senza dialoghi, riordino e drop sulle righe, miniatura CA scalata, menu gruppi, creazione immediata, gerarchia visiva, rinomina inline e annullamento, miniature, ritorno al progetto, sottosezioni, selezione, drop, conservazione input, spostamenti senza duplicati, salvataggio/riapertura, validazione ricorsiva e archivi precedenti. Riordino sezioni e progetti tramite eventi WPF: prima/dopo, livelli annidati, indicatore e annullamento, drop non validi, contenuti e scheda attiva conservati, conflitti invariati, modifiche in sospeso, ordine persistente.");
    }
}
