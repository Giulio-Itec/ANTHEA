using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeSteel(string directory)
    {
        testing = true; Directory.CreateDirectory(directory);
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static JsonObject Sheet(string module, string name) => J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
        async Task Capture(string name) { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, name + ".png"), Ui.Snapshot(this)); }
        ShowModules("Materiali"); await Capture("catalogo");
        Ui.Descendants<Button>(dashboardBody).Single(b => b.Tag?.ToString() == RebarMaterial.Module).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var view = editor?.rebarMaterial ?? throw new Exception("Modulo acciaio non aperto");
        Check(Application.Current.Windows.Count == 1 && ReferenceEquals(body.Content, moduleView), "Acciaio aperto in finestra separata");
        Check(view.Selection.SelectedItem?.ToString() == "B450C", "Default B450C mancante");
        Check(((TextBox)view.Properties.Editors["fyk_mpa"]).IsReadOnly, "Preset modificabile senza passare a Personalizzato");
        Check(Math.Abs(RebarMaterial.Evaluate(view.Input).Fyd - 450 / 1.15) < 1e-8, "fyd errato");
        await Capture("acciaio");
        double originalWidth = Width; Width = 1200; await Capture("acciaio-1200"); Width = originalWidth;
        foreach (var preset in RebarMaterial.Catalog())
        {
            view.Selection.SelectedItem = preset.Name;
            Check(view.Input.D("fyk_mpa") == preset.Input.D("fyk_mpa"), "Selezione preset " + preset.Name);
            if (preset.A5 is not null)
            {
                Check(view.Input.S("steel_eps_u") == "" && view.Status.Text.StartsWith("⚠"), "A5 usato impropriamente come εu");
                view.Properties.Set("steel_eps_u", "20");
                Check(view.Selection.SelectedItem?.ToString() == preset.Name, "Storico diventa custom definendo εu");
            }
            var native = ConcreteMaterials.Rebar(view.Input); var values = RebarMaterial.Evaluate(view.Input);
            Check(native.Fyk == values.Fy && native.Fu == values.Fu && Math.Abs(native.StrainUTension * 1000 - values.EpsilonU) < 1e-9, "Parametri calcolo differenti dalla scheda");
            var curve = RebarMaterial.Curve(view.Input);
            foreach (var p in curve) Check(Math.Abs(native.GetStress(p[0] / 1000) - p[1]) < 1e-6, "Diagramma incoerente col motore");
        }
        await Capture("acciaio-storico");
        view.Selection.SelectedItem = "Personalizzato";
        view.Properties.Set("fyk_mpa", "410,5"); view.Properties.Set("steel_fu_mpa", "550"); view.Properties.Set("steel_eps_u", "35");
        view.Properties.Set("steel_modulus_mpa", "205000"); view.Properties.Set("steel_diagramma", "Incrudente"); Commit();
        var customNative = ConcreteMaterials.Rebar(view.Input);
        foreach (var p in RebarMaterial.Curve(view.Input)) Check(Math.Abs(customNative.GetStress(p[0] / 1000) - p[1]) < 1e-6, "Diagramma incrudente incoerente col motore");
        Check(dirty && view.Status.Text.StartsWith("✓"), "Personalizzato non valido o modifiche non segnalate");
        string single = Path.Combine(directory, "acciaio.programma"); var expected = editor!.Data.DeepClone();
        Archivio.Scrivi(single, document); dirty = false; LoadFile(single); Commit();
        Check(JsonNode.DeepEquals(expected, editor!.Data), "Round trip singolo perde dati");
        ShowHome(); OpenModule(RebarMaterial.Module); Check(editor!.Data["input"].D("fyk_mpa") == 410.5, "Riprendi perde l'acciaio");
        view = editor.rebarMaterial!;
        view.Properties.Set("steel_fu_mpa", "400"); Check(view.Status.Text.Contains("fu deve"), "fu < fy accettato");
        view.Properties.Set("steel_fu_mpa", "550"); view.Properties.Set("steel_eps_u", "0,1"); Check(view.Status.Text.Contains("εu deve"), "εu inferiore a snervamento accettato");
        view.Properties.Set("steel_eps_u", "abc"); Commit(); Archivio.Scrivi(single, document); dirty = false; LoadFile(single);
        Check(editor!.Data["input"].S("steel_eps_u") == "abc" && editor.rebarMaterial!.Status.Text.StartsWith("⚠"), "Input incompleto perso alla riapertura");

        // Real project commit: update all, then only this sheet; other sections remain independent.
        editor.Dispose(); editor = null; currentSheet = null; dirty = false;
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray()));
        var project = J.Obj(("nome", "Progetto acciaio"), ("strutture", new JsonArray())); document.Array("progetti").Add(project);
        var section = CreateProjectSection(project, "Fondazione"); var elsewhere = CreateProjectSection(project, "Altra sezione");
        var steel = Sheet(RebarMaterial.Module, "Acciaio armature"); var rc = Sheet("str_palo", "Verifica c.a."); var pile = Sheet(PaloOrizzontale.Module, "Palo orizzontale");
        var chs = Sheet(MicropaloOrizzontale.Module, "Micropalo CHS"); var other = Sheet("str_palo", "Indipendente");
        foreach (var s in new[] { steel, rc, pile, chs }) section.Array("fogli").Add(s); elsewhere.Array("fogli").Add(other);
        var chsBefore = chs.DeepClone(); var otherBefore = other.DeepClone(); var rcBefore = rc["dati"]!.DeepClone();
        ShowSheet(steel); sharedChoiceForTest = () => true;
        editor!.rebarMaterial!.Selection.SelectedItem = "B450A"; Commit();
        foreach (var target in new[] { rc, pile })
        {
            var fields = ProjectSharedData.Fields(target);
            Check(RebarMaterial.Keys.All(k => ProjectSharedData.Equal(fields[k].Value, ProjectSharedData.Fields(steel)[k].Value)), "Condivisione incompleta " + target.S("nome"));
        }
        Check(JsonNode.DeepEquals(rcBefore!["combinazioni"], rc["dati"]!["combinazioni"]) && rcBefore["input"].D("cover_mm") == rc["dati"]!["input"].D("cover_mm"), "Materiale cambia carichi o copriferro");
        Check(JsonNode.DeepEquals(chsBefore, chs) && JsonNode.DeepEquals(otherBefore, other), "Acciaio trasferito a CHS o altra sezione");
        var pileEngine = new SezioneCA(pile["dati"]!["sezione"]!.AsObject());
        Check(Math.Abs(pileEngine.Fyd - RebarMaterial.Evaluate(steel["dati"]!["input"]!.AsObject()).Fyd) < 1e-9, "fyd nel palo non allineato");
        sharedChoiceForTest = () => false; editor.rebarMaterial.Selection.SelectedItem = "B450C"; Commit();
        Check(rc["dati"]!["input"].S("classe_acciaio") == "B450A" && ProjectSharedData.Differences(section).Any(d => d.Key == "classe_acciaio"), "Modifica locale non produce conflitto");
        sharedChoiceForTest = null;
        // Reverse update from the checking sheet, preserving the module's local reference.
        steel["dati"]!["riferimento"] = "Certificato di prova";
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Materiali" }, steel, RebarMaterial.Keys.ToHashSet());
        Check(steel["dati"]!.S("riferimento") == "Certificato di prova" && steel["dati"]!["input"].S("classe_acciaio") == "B450A", "Condivisione inversa incompleta");
        editor.Dispose(); editor = null; currentSheet = null;
        ShowSheet(steel); Check(editor!.rebarMaterial!.Selection.SelectedItem?.ToString() == "B450A", "Editor non riconosce l'acciaio condiviso");
        var newSteel = Sheet(RebarMaterial.Module, "Nuovo acciaio"); section.Array("fogli").Add(newSteel); InheritSectionData(newSteel, section);
        Check(newSteel["dati"]!["input"].S("classe_acciaio") == "B450A", "Nuovo acciaio non eredita la sezione");
        Commit(); ShowProjects(); await Capture("progetto-acciaio");
        string archive = Path.Combine(directory, "progetto-acciaio.programma"); Archivio.Scrivi(archive, document); dirty = false; LoadFile(archive);
        var restored = document.Array("progetti")[0]!["strutture"]![0]!["fogli"]![0]!.AsObject(); ShowSheet(restored); Commit();
        Check(editor!.Data["input"].S("classe_acciaio") == "B450A", "Riapertura progetto perde dati acciaio");
        editor.rebarMaterial!.Selection.SelectedItem = "FeB22k"; Commit();
        Check(ProjectSharedData.Limitations(restored.Parent!.Parent!.AsObject()).Any(w => w.Contains("εu")), "Acciaio incompleto non segnalato negli avvisi");
        var restoredSection = restored.Parent!.Parent!.AsObject();
        ProjectSharedData.Apply(restored, restoredSection, new HashSet<string> { "Materiali" }, keys: RebarMaterial.Keys.ToHashSet());
        var historicRc = restoredSection.Array("fogli").OfType<JsonObject>().First(s => s.S("modulo_id") == "str_palo");
        using (var probe = new SheetEditor("str_palo", historicRc["dati"]!.AsObject()))
        {
            probe.Commit();
            Check(probe.Data["input"].S("materiale_acciaio_nome") == "FeB22k" && probe.Data["input"].S("steel_eps_u") == "", "La verifica sostituisce dati storici incompleti all'apertura");
        }
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK: catalogo e apertura interna; layout 1600/1200; 6 preset e personalizzato; fyd, unità, diagramma e validazione; input incompleti conservati; salvataggio singolo e progetti; condivisione completa CA/palo nei due sensi; modifica locale; isolamento CHS e sottosezioni; eredità e avvisi.");
    }
}
