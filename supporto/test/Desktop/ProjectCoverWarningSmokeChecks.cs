using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckCoverWarnings(string directory)
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static JsonObject Sheet(string module, string name) => J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name),
            ("modulo_id", module), ("dati", Archivio.NuovoFoglio(module)));
        editor?.Dispose(); editor = null; currentSheet = null;
        document = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "progetti"), ("progetti", new JsonArray()));
        var project = J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", "Controllo copriferro"), ("fogli", new JsonArray()), ("strutture", new JsonArray()));
        document.Array("progetti").Add(project);
        var section = CreateProjectSection(project, "Palo P1");
        var material = Sheet("mat_calcestruzzo", "CLS di riferimento");
        var rc = Sheet("str_palo", "Verifica CA"); var pile = Sheet(PaloOrizzontale.Module, "Palo orizzontale");
        foreach (var sheet in new[] { material, rc, pile }) section.Array("fogli").Add(sheet);
        material["dati"] = J.Obj(("classe", "C30/37"), ("esposizione_principale", "XC1"),
            ("numeri", J.Obj(("diameter", "40"), ("aggregate", "20"))));
        rc["dati"]!["input"]!["shape"] = "Circolare";
        rc["dati"]!["input"]!["longitudinal_bar_diameter_mm"] = "16";
        pile["dati"]!["sezione"]!["longitudinal_bar_diameter_mm"] = "16";
        ProjectSharedData.Apply(material, section, new HashSet<string> { "Materiali" });
        ProjectSharedData.Apply(rc, section, new HashSet<string> { "Materiali", "Geometria", "Armatura" }, pile);
        rc["dati"]!["input"]!["cover_mm"] = "35";
        pile["dati"]!["sezione"]!["cover_mm"] = "35";
        var before = document.DeepClone();
        var failures = new List<string>();
        if (CoverChecks(section).Any(c => c.Passed != false || !c.Text.Contains("NON RISPETTATO")))
            failures.Add("Copriferro 35 mm accettato nonostante i 50 mm mostrati in Materiali (barra di riferimento 40 mm, barre reali 16 mm)");
        rc["dati"]!["workspace_ca"]!["sle_comuni"]!["esposizione"] = "XC4";
        rc["dati"]!["input"]!["cover_mm"] = "15";
        if (CoverChecks(section, rc).Single() is not { Passed: false } conflict || !conflict.Text.Contains("NON RISPETTATO"))
            failures.Add("Il conflitto di esposizione nasconde il mancato rispetto del minimo di Materiali");
        rc["dati"]!["workspace_ca"]!["sle_comuni"]!["esposizione"] = "XC1";
        rc["dati"]!["input"]!["cover_mm"] = "35";
        Check(JsonNode.DeepEquals(before, document), "Il controllo modifica i dati del progetto");
        if (failures.Count > 0) throw new Exception(string.Join("\n", failures));

        rc["dati"]!["workspace_ca"]!["dettagli_costruttivi"] = J.Obj(("vita_durabilita", "100"));
        Check(CoverChecks(section, rc).Single() is { Passed: false } low && low.Text.Contains("vita utile"), "Conflitto durabilità nasconde il minimo");
        rc["dati"]!["input"]!["cover_mm"] = "50";
        Check(CoverChecks(section, rc).Single().Passed is null, "Dati in conflitto producono un esito positivo");
        rc["dati"]!["workspace_ca"]!.AsObject().Remove("dettagli_costruttivi");
        rc["dati"]!["input"]!["barre_manuali"] = new JsonArray();
        Check(CoverChecks(section, rc).Single().Passed is null, "Armatura incompleta produce un esito positivo");
        rc["dati"]!["input"]!["cover_mm"] = "35";
        Check(CoverChecks(section, rc).Single().Passed == false, "Armatura incompleta nasconde il minimo di Materiali");
        rc["dati"]!["input"]!.AsObject().Remove("barre_manuali");

        foreach (var sheet in new[] { rc, pile, material })
        {
            ShowSheet(sheet);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
            Check(sharedStatus.IsVisible && sharedStatus.Text.Contains("NON RISPETTATO") && sharedStatus.Text.Contains("50 mm"),
                "Avviso Materiali non visibile in " + sheet.S("nome"));
            if (ReferenceEquals(sheet, material))
                Check(Ui.Descendants<TextBlock>(editor!.materials!).Any(t => t.Text == "cnom = 50 mm"), "Il riferimento non coincide con il valore mostrato in Materiali");
        }
        // Editing the material must update the warning immediately, before saving/committing.
        var numbers = (Dictionary<string, TextBox>)typeof(Materiali.MaterialView).GetField("numbers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor!.materials)!;
        numbers["diameter"].Text = "16";
        Check(!sharedStatus.Text.Contains("NON RISPETTATO"), "Avviso obsoleto dopo riduzione del minimo");
        numbers["diameter"].Text = "40";
        Check(sharedStatus.Text.Contains("NON RISPETTATO"), "Modifica del minimo non segnalata in tempo reale");
        Commit(); ShowProjects(); ShowProjectOverview(section);
        Check(SectionReportWarnings(section).Count(s => s.Contains("NON RISPETTATO")) == 2, "Avvisi assenti dal riepilogo/report");
        var header = new StackPanel(); AddCoherenceBadge(header, section);
        Check(AutomationProperties.GetName(header.Children.OfType<Button>().Single()).Contains("avvisi"), "Avvisi assenti dal badge");
        Exception? dialogError = null;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<Window>().Single(w => w != this && w.Title.StartsWith("Confronto"));
            try
            {
                Check(Ui.Descendants<TextBlock>(dialog).Count(t => t.Text.Contains("NON RISPETTATO")) == 2, "Avvisi assenti dal confronto");
                dialog.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "copriferro-minimo-confronto.png"), Ui.Snapshot(dialog));
            }
            catch (Exception error) { dialogError = error; }
            finally { dialog.Close(); }
        }));
        ShowCoherence(section); if (dialogError is not null) throw dialogError;

        // Peer conflicts must be visible from both sheets, independently of their order.
        rc["dati"]!["input"]!["cover_mm"] = "50";
        foreach (var sheet in new[] { rc, pile })
        {
            editor?.Dispose(); editor = null; currentSheet = null; ShowSheet(sheet);
            Check(sharedStatus.Text.Contains("Dati diversi tra i fogli") && sharedStatus.Text.Contains(SharedFieldLabel("cover_mm")), "Copriferro diverso non segnalato in " + sheet.S("nome"));
        }
        editor?.Dispose(); editor = null; currentSheet = null;
        pile["dati"]!["sezione"]!["cover_mm"] = "50";
        Check(CoverChecks(section).All(c => c.Passed == true), "Copriferro al limite non rispettato");
        // Larger actual bars must still control when more demanding than the material reference.
        material["dati"]!["numeri"]!["diameter"] = "16";
        rc["dati"]!["input"]!["longitudinal_bar_diameter_mm"] = "40";
        rc["dati"]!["input"]!["cover_mm"] = "49";
        Check(CoverChecks(section, rc).Single().Passed == false, "Barre reali maggiori non considerate");
        rc["dati"]!["input"]!["cover_mm"] = "50";
        Check(CoverChecks(section, rc).Single().Passed == true, "Barre reali: soglia esatta respinta");
        material["dati"]!["numeri"]!["aggregate"] = "abc";
        Check(CoverChecks(section, rc).Single().Passed is null, "Materiale incompleto segnalato come rispettato");
        material["dati"]!["numeri"]!["aggregate"] = "20";
        // The same checks must apply through empty parent groups and survive reopening.
        section.Array("fogli").Remove(material); project.Array("fogli").Add(material);
        rc["dati"]!["input"]!["cover_mm"] = "20";
        ShowSheet(rc);
        Check(sharedStatus.Text.Contains("NON RISPETTATO"), "Materiale superiore ignorato");
        Commit();
        var filename = Path.Combine(directory, "copriferro-minimo.programma"); Archivio.Scrivi(filename, document);
        var reopened = Archivio.Leggi(filename);
        Check(CoverChecks(reopened.Array("progetti")[0]!.AsObject()).Any(c => c.Passed == false), "Avviso perso alla riapertura");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "copriferro-minimo-scheda.png"), Ui.Snapshot(this));
        File.WriteAllText(Path.Combine(directory, "copriferro-minimo.txt"), "OK: minimo Materiali, barre effettive, conflitto esposizione, soglia, input incompleti, avviso nei due fogli e in Materiali, modifiche live, badge, confronto, report, gerarchia, persistenza.");
        editor?.Dispose(); editor = null; currentSheet = null; sharedBaseline = null;
    }
}
