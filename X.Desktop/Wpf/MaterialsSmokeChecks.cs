using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeMaterials(string directory)
    {
        testing = true; Directory.CreateDirectory(directory);
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        ShowModules("Materiali");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "materiali-catalogo.png"), Ui.Snapshot(this));
        var open = Ui.Descendants<Button>(dashboardBody).Single(b => b.Tag?.ToString() == "mat_calcestruzzo");
        open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var active = editor?.materials ?? throw new Exception("Scheda materiali non aperta");
        Check(Application.Current.Windows.Count == 1 && ReferenceEquals(body.Content, moduleView), "Materiali non integrato nella finestra principale");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        active.Check(); Commit();
        Check(dirty, "Modifiche materiali non segnalate");
        var expected = (JsonObject)editor!.Data.DeepClone();
        ShowHome(); OpenModule("mat_calcestruzzo");
        Check(ReferenceEquals(active, editor.materials), "Riprendi perde la scheda");
        File.WriteAllBytes(Path.Combine(directory, "materiali-scheda.png"), Ui.Snapshot(this));
        double previousWidth = Width; Width = 1200;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "materiali-scheda-1200.png"), Ui.Snapshot(this));
        Width = previousWidth;
        var independent = new Materiali.MaterialView();
        independent.RestoreState(Archivio.NuovoFoglio("mat_calcestruzzo"));
        Check(!JsonNode.DeepEquals(expected, independent.CaptureState()), "Nuove schede ereditano modifiche di altre schede");
        string single = Path.Combine(directory, "materiale.programma");
        Archivio.Scrivi(single, document);
        dirty = false; LoadFile(single); Commit();
        Check(JsonNode.DeepEquals(expected, editor!.Data), "Riapertura materiale singolo perde input");
        editor.Dispose(); editor = null; currentSheet = null;
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray()));
        var project = J.Obj(("nome", "Progetto materiali"), ("strutture", new JsonArray())); document.Array("progetti").Add(project);
        var section = CreateProjectSection(project, "Opera"); var nested = CreateProjectSection(section, "Materiali");
        var sheet = J.Obj(("nome", "CLS fondazioni"), ("modulo_id", "mat_calcestruzzo"), ("dati", expected));
        section.Array("fogli").Add(sheet);
        ShowProjects(); ShowSheet(sheet);
        Check(JsonNode.DeepEquals(expected, editor!.materials!.CaptureState()), "Apertura materiale nel progetto perde dati");
        ShowProjects(); MoveProjectSheet(sheet, nested);
        Check(ReferenceEquals(sheet.Parent, nested["fogli"]), "Spostamento materiale non riuscito");
        string archive = Path.Combine(directory, "progetto.programma"); Archivio.Scrivi(archive, document);
        dirty = false; LoadFile(archive);
        var loaded = document.Array("progetti")[0]!["strutture"]![0]!["strutture"]![0]!["fogli"]![0]!.AsObject();
        ShowSheet(loaded); Commit();
        Check(JsonNode.DeepEquals(expected, editor!.Data), "Round trip materiale nel progetto");
        // Invalid, unfinished numeric input must be preserved rather than replaced with defaults.
        var invalid = editor.materials!.CaptureState(); invalid["numeri"]!["diameter"] = "abc";
        invalid["scelte"]!["deviationControl"] = "Misura accurata + scarto"; invalid["scelte"]!["deviationValue"] = "0 mm";
        editor.materials.RestoreState(invalid); Commit();
        var restored = new Materiali.MaterialView(); restored.RestoreState(editor.Data);
        Check(JsonNode.DeepEquals(invalid, restored.CaptureState()), "Input incompleti o tolleranza non conservati");
        ShowProjects(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "materiali-progetti.png"), Ui.Snapshot(this));
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK: catalogo, icone, apertura interna, benchmark originali, modifiche, ripresa, salvataggio singolo e progetti, spostamento tra sottosezioni, riapertura, conservazione input incompleti e tolleranza.");
    }
}
