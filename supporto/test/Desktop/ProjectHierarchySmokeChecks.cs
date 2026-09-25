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
    internal async Task SmokeHierarchy(string directory)
    {
        testing = true; Directory.CreateDirectory(directory);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static JsonObject Sheet(string module, string name) => J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
        async Task Capture(string name) { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, name + ".png"), Ui.Snapshot(this)); }
        TreeViewItem Find(JsonObject value)
        {
            TreeViewItem? Search(ItemsControl control)
            {
                foreach (TreeViewItem node in control.Items) { if (ReferenceEquals(node.Tag, value)) return node; if (Search(node) is TreeViewItem found) return found; }
                return null;
            }
            return Search(tree) ?? throw new Exception("Nodo non trovato " + value.S("nome"));
        }
        void Drop(JsonObject destination, string format, object value)
        {
            var node = Find(destination);
            var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
                [new DataObject(format, value), DragDropKeyStates.None, DragDropEffects.Copy | DragDropEffects.Move, node, new Point(8, 8)], null)!;
            args.RoutedEvent = DragDrop.DropEvent; node.RaiseEvent(args);
            Check(args.Effects != DragDropEffects.None, "Drop rifiutato sul gruppo superiore");
        }
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray()));
        // Deliberately use an old project with no fogli array at its root.
        var project = J.Obj(("nome", "Ponte"), ("strutture", new JsonArray())); document.Array("progetti").Add(project);
        var upper = CreateProjectSection(project, "Pali"); var child = CreateProjectSection(upper, "Spalla A"); var deep = CreateProjectSection(child, "Fondazioni");
        var other = CreateProjectSection(project, "Travi");
        var unrelatedProject = J.Obj(("nome", "Altro progetto"), ("strutture", new JsonArray())); document.Array("progetti").Add(unrelatedProject);
        var unrelated = CreateProjectSection(unrelatedProject, "Sezione indipendente"); var untouched = Sheet("str_palo", "Verifica indipendente"); unrelated.Array("fogli").Add(untouched);
        var untouchedBefore = untouched.DeepClone();
        ShowProjects();
        Drop(project, ModuleDragFormat, "mat_calcestruzzo");
        var cls = project.Array("fogli")[0]!.AsObject(); cls["nome"] = "CLS di progetto";
        Check(ReferenceEquals(((TreeViewItem)Find(project).Items[0]).Tag, cls), "Scheda del progetto collocata dopo i gruppi");
        Drop(upper, ModuleDragFormat, RebarMaterial.Module);
        var steel = upper.Array("fogli")[0]!.AsObject(); steel["nome"] = "Acciaio pali";
        AddSheetTo("str_palo", upper, false); var reference = upper.Array("fogli")[1]!.AsObject(); reference["nome"] = "Sezione di riferimento";
        var input = reference["dati"]!["input"]!; input["shape"] = "Circolare"; input["diameter_mm"] = "1200"; input["cover_mm"] = "60";
        AddSheetTo(PaloOrizzontale.Module, deep, false); var pile = deep.Array("fogli")[0]!.AsObject(); pile["nome"] = "Capacità orizzontale";
        AddSheetTo("str_palo", deep, false); var ca = deep.Array("fogli")[1]!.AsObject(); ca["nome"] = "Verifica fondazione";
        AddSheetTo("str_palo", other, false); var independent = other.Array("fogli")[0]!.AsObject(); independent["nome"] = "Verifica trave";
        Check(pile["dati"]!["generali"].D("diametro") == 1.2 && ca["dati"]!["input"].D("diameter_mm") == 1200, "Geometria non ereditata attraverso gruppo vuoto");
        Check(ca["dati"]!["input"].D("fck_mpa") == 30 && ca["dati"]!["workspace_ca"]!["sle_comuni"].S("esposizione") == "XC1", "Materiale del progetto non ereditato");
        Check(independent["dati"]!["input"].S("shape") == "Rettangolare", "Ramo parallelo eredita la geometria dei pali");
        ShowProjects();
        var upperNode = Find(upper); Check(upperNode.Items.Cast<TreeViewItem>().Take(2).All(n => ((JsonObject)n.Tag).ContainsKey("modulo_id")), "Schede non immediatamente sotto la sezione");
        var fullSnapshot = document.DeepClone();
        _ = ProjectSharedData.Differences(project); _ = ProjectSharedData.Limitations(project); _ = CoverChecks(project);
        Check(JsonNode.DeepEquals(fullSnapshot, document), "Confronto gerarchico modifica il documento");
        Check(!CoverChecks(deep).Any(c => c.Text.Contains("aggiungere una scheda")), "Copriferro ignora il CLS superiore");

        // Editing the parent automatically propagates through all depths, including an empty group.
        ShowSheet(cls); var material = editor!.materials!.CaptureState(); material["classe"] = "C40/50";
        editor.materials.RestoreState(material); sharedChoiceForTest = () => throw new Exception("Richiesta conferma sulla propagazione del riferimento superiore"); Commit();
        foreach (var target in new[] { reference, ca, pile, independent }) Check(ProjectSharedData.Fields(target)["CLS · fck [MPa]"].Value!.GetValue<double>() == 40 || ProjectSharedData.Text(ProjectSharedData.Fields(target)["CLS · fck [MPa]"].Value) == "40", "CLS non propagato a " + target.S("nome"));
        Check(JsonNode.DeepEquals(untouchedBefore, untouched), "Aggiornamento oltre il progetto");
        ShowSheet(steel); editor!.rebarMaterial!.Selection.SelectedItem = "B450A"; Commit();
        Check(ca["dati"]!["input"].S("classe_acciaio") == "B450A" && pile["dati"]!["sezione"].S("classe_acciaio") == "B450A" && independent["dati"]!["input"].S("classe_acciaio") == "B450C", "Acciaio non propagato solo al proprio ramo");
        ShowSheet(reference); editor!.Data["input"]!["diameter_mm"] = "1400"; editor.Data["input"]!["cover_mm"] = "65";
        editor.Data["input"]!["longitudinal_bar_count"] = "20"; Commit();
        Check(pile["dati"]!["generali"].D("diametro") == 1.4 && ca["dati"]!["input"].D("cover_mm") == 65 && ca["dati"]!["input"].D("longitudinal_bar_count") == 20, "Geometria/armatura non propagate");
        Check(independent["dati"]!["input"].D("cover_mm") == 70, "Propagazione invasa sezione sorella");
        sharedChoiceForTest = null;
        // A lower edit cannot rewrite or broadcast over its ancestor's reference.
        ShowSheet(ca); editor!.Data["input"]!["cover_mm"] = "42"; editor.Data["input"]!["fck_mpa"] = "25"; Commit();
        Check(reference["dati"]!["input"].D("cover_mm") == 65 && pile["dati"]!["sezione"].D("cover_mm") == 65 && cls["dati"].S("classe") == "C40/50", "Foglio inferiore ha comandato sui riferimenti");
        Check(ProjectSharedData.Differences(deep).Any(d => ReferenceEquals(d.First, cls) && ReferenceEquals(d.Second, ca)), "Conflitto con antenato invisibile nella sottosezione");
        Check(sharedStatus.Text.Contains("Dati diversi tra i fogli"), "Avviso gerarchico assente nella scheda");
        var badge = new StackPanel(); AddCoherenceBadge(badge, child);
        Check(System.Windows.Automation.AutomationProperties.GetName(badge.Children.OfType<Button>().Single()).Contains("Differenze"), "Gruppo vuoto nasconde conflitti discendenti");
        Exception? dialogError = null;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<Window>().Single(w => w != this && w.Title.StartsWith("Confronto"));
            try
            {
                dialog.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "conflitti-gerarchici.png"), Ui.Snapshot(dialog));
                Button Align(JsonObject sheet, string key) => Ui.Descendants<Button>(dialog).Single(b => b.Tag is ValueTuple<JsonObject, string> t && ReferenceEquals(t.Item1, sheet) && t.Item2 == key);
                Check(!Align(ca, "cover_mm").IsEnabled, "Sottosezione selezionabile contro il riferimento superiore");
                Check(Align(reference, "cover_mm").IsEnabled, "Riferimento superiore non utilizzabile");
                Align(reference, "cover_mm").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Align(cls, "CLS · fck [MPa]").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception ex) { dialogError = ex; }
            finally { dialog.Close(); }
        }));
        ShowCoherence(deep); if (dialogError is not null) throw dialogError;
        Check(ca["dati"]!["input"].D("cover_mm") == 65 && ca["dati"]!["input"].D("fck_mpa") == 40, "Uniformazione gerarchica incompleta");
        // If the intermediate reference disagrees but the leaf already matches the root,
        // the root must still appear as an enabled resolution in the leaf's dialog.
        reference["dati"]!["input"]!["fck_mpa"] = "35";
        dialogError = null;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<Window>().Single(w => w != this && w.Title.StartsWith("Confronto"));
            try
            {
                var rootAlign = Ui.Descendants<Button>(dialog).Single(b => b.Tag is ValueTuple<JsonObject, string> t && ReferenceEquals(t.Item1, cls) && t.Item2 == "CLS · fck [MPa]");
                Check(rootAlign.IsEnabled, "Riferimento superiore coerente con foglio locale assente dal conflitto intermedio");
                rootAlign.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception ex) { dialogError = ex; }
            finally { dialog.Close(); }
        }));
        ShowCoherence(deep); if (dialogError is not null) throw dialogError;
        Check(reference["dati"]!["input"].D("fck_mpa") == 40, "Riferimento intermedio non riallineato");
        // Reordering never changes authority; moving a sheet preserves inputs and changes its scope.
        var beforeOrder = ProjectSharedData.Differences(project).Select(d => (d.Key, d.Left, d.Right)).OrderBy(x => x.Key).ToArray();
        MoveProjectSheet(reference, upper, steel, false);
        Check(beforeOrder.SequenceEqual(ProjectSharedData.Differences(project).Select(d => (d.Key, d.Left, d.Right)).OrderBy(x => x.Key)), "Riordino cambia i conflitti");
        var movedBefore = pile["dati"]!.DeepClone(); Drop(other, SheetDragFormat, pile);
        Check(!ProjectSharedData.AncestorReferences(pile, "cover_mm").Any() && pile["dati"]!["sezione"].S("classe_acciaio") == independent["dati"]!["input"].S("classe_acciaio"), "Spostamento cambia dati o conserva vecchia autorità");
        Drop(upper, SheetDragFormat, pile); Check(ReferenceEquals(pile.Parent, upper["fogli"]), "Impossibile riportare una scheda nel gruppo superiore");
        ShowProjects(); await Capture("progetti-gerarchici");
        string archive = Path.Combine(directory, "gerarchia.programma"); Archivio.Scrivi(archive, document);
        var expected = document.DeepClone(); dirty = false; LoadFile(archive);
        Check(JsonNode.DeepEquals(expected, document), "Salvataggio/riapertura cambia gerarchia o dati");
        var restoredRoot = document.Array("progetti")[0]!.AsObject();
        Check(ProjectSharedData.AncestorReferences(ProjectSharedData.SubtreeSheets(restoredRoot).Single(s => s.S("nome") == "Verifica fondazione"), "CLS · fck [MPa]").Single().S("nome") == "CLS di progetto", "Autorità non ripristinata");

        // Multiple conflicting references at one level must not be resolved by array order.
        var ambiguity = J.Obj(("nome", "Ambiguità"), ("fogli", new JsonArray()), ("strutture", new JsonArray()));
        var m1 = Sheet("mat_calcestruzzo", "CLS 1"); var m2 = Sheet("mat_calcestruzzo", "CLS 2"); m2["dati"]!["classe"] = "C50/60";
        ambiguity.Array("fogli").Add(m1); ambiguity.Array("fogli").Add(m2);
        var branch = CreateProjectSection(ambiguity, "Livello inferiore");
        foreach (bool reverse in new[] { false, true })
        {
            if (reverse) { ambiguity.Array("fogli").Remove(m1); ambiguity.Array("fogli").Add(m1); }
            var target = Sheet("str_palo", "Nuova verifica"); branch.Array("fogli").Add(target); InheritSectionData(target, branch);
            Check(target["dati"]!["input"].D("fck_mpa") == 35 && ProjectSharedData.Differences(branch).Any(d => d.Key == "CLS · fck [MPa]"), "Ambiguità risolta scegliendo un riferimento casuale");
            branch.Array("fogli").Remove(target);
        }
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK: drop su progetto e sezioni con figli; schede prima dei sottogruppi; eredità a più livelli e gruppi vuoti; precedenza superiore; CLS, acciaio, geometria, armatura; propagazione automatica; conflitti e uniformazione gerarchici; avvisi copriferro; rami/progetti separati; riordino, spostamento, salvataggio e riapertura; riferimenti ambigui non scelti automaticamente.");
    }
}
