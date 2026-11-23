using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Concrete;
using Anthea.Presentation.Shell.Commands;
using Anthea.Reports;

namespace Anthea.Presentation.Tests;

internal static class QuickSectionReportGuideChecks
{
    internal static void Run(Expect expect, JsonObject data, QuickSectionReportData bending, string folder)
    {
        var shear = new QuickShearReport([new("Staffa effettiva?", "No: armatura a taglio indipendente")],
            [new("Stesso nome", "Con armatura", "-100", "81", "X", "110", "210", "110", .736, true, "", "Con armatura", "uno", "1.75"),
             new("Stesso nome", "Non armata", "-200", "92", "Y", "—", "60", "60", 1.533, false, "", "Non armata", "due")],
            Actions: [new("uno", "Stesso nome", "Con armatura", "-100", "81", "42"), new("due", "Stesso nome", "Non armata", "-200", "35", "92")]);
        var report = bending with { Shear = shear };
        var outline = QuickSectionReport.Outline(bending.Cases.Select(c => c.State), shear.Rows.Select(r => r.EffectiveModel));
        expect.That(outline.Where(c => c.Children.Count > 0).Select(c => c.Label).SequenceEqual(new[] { "Introduzione", "Verifiche flessione", "Verifiche taglio" }),
            "Relazione guida: capitoli nell’ordine del documento fornito");
        expect.That(outline.Single(c => c.Key == "q1").Children.Select(c => c.Label).SequenceEqual(new[] { "Assunzioni di calcolo e criteri di verifica", "Geometria della sezione", "Combinazioni" }),
            "Relazione guida: materiali e criteri prima della geometria e delle combinazioni");
        expect.That(!new ReportSectionsDialogViewModel(QuickSectionReport.Module, data).Sections.SelectMany(s => s.Descendants()).Any(s => s.Key.StartsWith("taglio_") || s.Key == "azioni_taglio"),
            "Menu: taglio assente quando il pannello non è attivo");
        var shearData = (JsonObject)data.DeepClone(); ConcreteQuickShear.CopyCombinations(shearData);
        var action = ConcreteQuickShear.Combinations(shearData)[0];
        ConcreteQuickShear.WriteCombination(shearData, action["id"]!.ToString(), "modello", ConcreteQuickShear.WithoutLinks);
        var menu = new ReportSectionsDialogViewModel(QuickSectionReport.Module, shearData);
        var leaves = menu.Sections.SelectMany(c => c.Descendants()).Where(c => c.Children.Count == 0).ToArray();
        expect.That(leaves.Any(c => c.Key == "taglio_armata") && leaves.Any(c => c.Key == "taglio_non_armata") && leaves.Any(c => c.Key == "azioni_taglio"),
            "Menu: entrambe le verifiche a taglio, in base al modello effettivo delle righe");
        foreach (var top in menu.Sections) top.IsChecked = false;
        leaves.Single(c => c.Key == "taglio_riepilogo").IsChecked = true;
        expect.That(menu.CanExport && leaves.Where(c => c.IsChecked).Select(c => c.Key).SequenceEqual(new[] { "taglio_riepilogo" }),
            "Menu: esportazione del solo sommario a taglio, senza dipendenze dalla flessione");
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        XDocument Build(params string[] keys) => QuickSectionReport.Create("Sezione guida", report, keys.ToHashSet()).GetXml("word/document.xml");
        string Text(XDocument x) => string.Join("\n", x.Descendants(w + "t").Select(t => t.Value));
        var complete = QuickSectionReport.Create("Sezione guida", report, QuickSectionReport.Defaults());
        string fullText = Text(complete.GetXml("word/document.xml"));
        expect.That(fullText.IndexOf("Introduzione") < fullText.IndexOf("Verifiche flessione") && fullText.IndexOf("Verifiche flessione") < fullText.IndexOf("Verifiche taglio")
            && !fullText.Contains("Allegato"), "Word: stesso ordine dei capitoli del menu, senza allegato");
        expect.That(!menu.Sections.SelectMany(c => c.Descendants()).Any(c => c.Key is "q6" or "dominio" or "tensioni" or "curvatura" or "avvisi")
            && !Text(Build("riepilogo", "q6", "dominio", "tensioni", "curvatura", "avvisi")).Contains("Allegato"),
            "Allegato rimosso dal menu; le opzioni precedenti non lo ripristinano");
        expect.That(complete.GetXml("word/document.xml").Descendants(w + "pStyle").Any(p => (string?)p.Attribute(w + "val") == "Titolo3"), "Word: terzo livello per flessione/taglio e famiglie SLE");
        expect.That(!Text(Build("riepilogo")).Contains("taglio", StringComparison.OrdinalIgnoreCase), "Word: il sommario della flessione non aggiunge il taglio");
        expect.That(!Text(Build("taglio_riepilogo")).Contains("flessione", StringComparison.OrdinalIgnoreCase), "Word: il sommario del taglio non aggiunge la flessione");
        var actions = Build("azioni_taglio").Descendants(w + "tbl").Single().Elements(w + "tr").Skip(1).ToArray();
        expect.That(actions.Length == 2 && string.Join("|", actions[0].Descendants(w + "t").Select(t => t.Value)).Contains("-100|81|42")
            && string.Join("|", actions[1].Descendants(w + "t").Select(t => t.Value)).Contains("-200|35|92"),
            "Word: nomi duplicati conservati, N/Vx/Vy della tabella a taglio in ordine e senza raggruppamenti per nome");
        expect.That(Text(Build("taglio_armata")).Contains("1.75") && !Text(Build("taglio_armata")).Contains("Non verificata"), "Word: dettaglio armato con cotangente effettiva, senza casi non armati");
        expect.That(Text(Build("taglio_non_armata")).Contains("Non verificata") && !Text(Build("taglio_non_armata")).Contains("1.75"), "Word: dettaglio non armato autonomo e senza falso esito positivo");
        var invalid = report with { Shear = shear with { Rows = [shear.Rows[0] with { Passed = null, Ratio = null, Status = "Diametro mancante" }] } };
        var invalidText = Text(QuickSectionReport.Create("Sezione incompleta", invalid, new HashSet<string> { "taglio_armata" }).GetXml("word/document.xml"));
        expect.That(invalidText.Contains("Non determinata") && invalidText.Contains("Diametro mancante"), "Word: casi incompleti conservati con il motivo");
        File.WriteAllBytes(Path.Combine(folder, "relazione-guida-struttura.docx"), DocxWriter.Write(complete));
    }
}
