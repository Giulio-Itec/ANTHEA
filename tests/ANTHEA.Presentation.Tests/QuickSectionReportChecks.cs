using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Shell.Commands;
using Anthea.Reports;
using X.Core;

namespace Anthea.Presentation.Tests;

internal static class QuickSectionReportChecks
{
    internal static void Run(Expect expect, string folder)
    {
        var data = ConcreteQuickSection.Create();
        ConcreteQuickSection.ReplaceCombinations(data, [new("ULS +", -600, 180, 17), new("Rara", -400, 95, 10, "SLE"),
            new("ULS −", -300, -90, 7), new("Sismica", -400, 80, 2, "SLV"), new("Frequente", -300, 75, 8, "SLE_FREQ"),
            new("QP 1", -200, 60, 4, "SLE_QP"), new("QP 2", -350, -55, 1, "SLE_QP")]);
        ConcreteQuickSection.WriteCreep(data, "2"); ConcreteQuickSection.WriteServiceOption(data, ConcreteQuickSection.ExposureKey, "XC3");
        string before = data.ToJsonString();
        var analysis = ConcreteQuickReportAnalysis.Create(data);
        expect.That(data.ToJsonString() == before, "Il report non modifica input, selezione, ordine o modelli del foglio");
        var rows = ConcreteQuickSection.Combinations(data);
        expect.That(analysis.Report.Cases.Select(c => c.Id).SequenceEqual(rows.Select(r => r.S("id"))), "Ordine del report uguale al programma, anche con SL intercalati");
        foreach (var row in rows)
        {
            string id = row.S("id"); var selected = (JsonObject)data.DeepClone(); ConcreteQuickSection.SelectCombination(selected, id);
            var native = ConcreteQuickSection.Calculate(selected); var actual = analysis.Results[id]; var item = analysis.Report.Cases.Single(c => c.Id == id);
            expect.That(actual.UltimateCheck?.Utilization == native.UltimateCheck?.Utilization && actual.ServiceRatio == native.ServiceRatio && actual.Crack?.Ratio == native.Crack?.Ratio,
                "Verifica isolata identica a quella del foglio completo: " + item.Name);
            expect.That(item.Values.Select(v => (v.Label, v.Value, v.Unit)).SequenceEqual(ConcreteQuickSection.ResultValues(native).Select(v => (v.Label, v.Value, v.Unit))), "Tutti i ResultValues trasferiti senza arrotondamenti aggiuntivi: " + item.Name);
            var summary = ConcreteQuickSection.Summary(native)!;
            expect.That(item.Checks.Any(c => c.Value == summary.Value && c.Ratio == summary.Ratio), "Risultato e rapporto della Summary nel report: " + item.Name);
        }
        // Same actions and models in the large module; straight bending explicitly discards My.
        var largeData = (JsonObject)data.DeepClone();
        foreach (string family in SectionWorkspace.Sets)
            foreach (var row in largeData["combinazioni"]![family]!.AsArray().OfType<JsonObject>()) row["azioni"]![2] = 0;
        var large = ConcreteAnalysis.Calculate(largeData);
        File.WriteAllText(Path.Combine(folder, "confronto-modulo-completo.json"), large.ToJsonString());
        foreach (var item in analysis.Report.Cases)
        {
            var result = analysis.Results[item.Id];
            if (item.State is "SLU" or "SLV")
                expect.That(result.UltimateCheck?.Utilization == J.Number(large["domini"]?["3D:" + item.State]?[item.Id]?["Utilization"]), "η uguale a ConcreteAnalysis: " + item.Name + " · " + result.UltimateCheck?.Utilization + " / " + large["domini"]?["3D:" + item.State]?[item.Id]?["Utilization"]);
            else
                expect.That(result.ServiceState?.sigma_cls == J.Number(large["tensioni"]?[item.State]?[item.Id]?["State"]?["sigma_cls"])
                    && result.ServiceState?.sigma_acciaio == J.Number(large["tensioni"]?[item.State]?[item.Id]?["State"]?["sigma_acciaio"]), "Tensioni uguali a ConcreteAnalysis: " + item.Name);
        }
        expect.That(analysis.Report.Cases.Where(c => c.State == "SLE_QP").All(c => c.Checks.Select(k => k.Kind).SequenceEqual(new[] { "Tensioni", "Fessurazione" })), "QP: due verifiche esplicite, stesso rilievo");
        var options = QuickSectionReport.Defaults();
        expect.That(!options.Overlaps(new[] { "intestazione", "indice", "avvisi", "curvatura", QuickSectionReport.Brief }), "Default: copertina, indice, avvisi e curvatura esclusi; nessuna opzione breve nella W");
        var dialog = new ReportSectionsDialogViewModel(QuickSectionReport.Module, data);
        var chapter = dialog.Sections.Single(s => s.Key == "q5"); chapter.IsChecked = false; chapter.IsChecked = true;
        expect.That(chapter.Children.All(s => s.IsChecked), "Selezione del capitolo estesa a tutti i sottocapitoli");
        var doc = QuickSectionReport.Create("Sezione di prova", analysis.Report, options);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var xml = doc.GetXml("word/document.xml"); string text = string.Join("\n", xml.Descendants(w + "t").Select(t => t.Value));
        expect.That(analysis.Report.Cases.All(c => c.Checks.All(k => text.Contains(k.Value.Replace('\n', ' ')) || k.Value.Split('\n').All(text.Contains))), "Risultati numerici presenti nelle celle Word");
        expect.That(!xml.Descendants(w + "fldChar").Any() && !doc.GetXml("word/settings.xml").Descendants(w + "updateFields").Any(), "Word senza aggiornamento automatico di campi esterni");
        expect.That(xml.Descendants(w + "shd").Any(e => (string?)e.Attribute(w + "fill") == "E7E6E6"), "Tabelle grigio chiaro del modello ITEC");
        File.WriteAllBytes(Path.Combine(folder, "relazione-senza-figure.docx"), DocxWriter.Write(doc));
        File.WriteAllText(Path.Combine(folder, "relazione-dati.json"), System.Text.Json.JsonSerializer.Serialize(analysis.Report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        // Distinct governors for the two QP checks and an incomplete case must all survive the short report.
        var qp = analysis.Report.Cases.First(c => c.State == "SLE_QP");
        QuickReportCase Case(string id, double? stress, double? crack) => qp with { Id = id, Name = id, Checks = [new("Tensioni", "σ", "", stress, stress is null ? null : stress <= 1), new("Fessurazione", "w", "", crack, crack is null ? null : crack <= 1)] };
        var synthetic = analysis.Report with { Cases = [Case("σ governante", .9, .2), Case("w governante", .3, .8), Case("Da completare", null, null)] };
        var critical = QuickSectionReport.Governing(synthetic);
        expect.That(critical.Count == 4 && critical.Any(p => p.Check.Kind == "Tensioni" && p.Case.Id == "σ governante")
            && critical.Any(p => p.Check.Kind == "Fessurazione" && p.Case.Id == "w governante") && critical.Count(p => p.Case.Id == "Da completare") == 2, "Breve: governanti distinti, nessun esito mancante escluso");
        // Explicit model/sign overrides must survive isolation (including positive action with negative capacity selected).
        string overridden = rows[0].S("id"); ConcreteQuickSection.WriteCombinationSetting(data, overridden, ConcreteQuickSection.ModelField, "Elastico");
        ConcreteQuickSection.WriteCombinationSetting(data, overridden, ConcreteQuickSection.SignField, "SLU−");
        ConcreteQuickSection.SelectCombination(data, overridden);
        var full = ConcreteQuickSection.Calculate(data); var report = ConcreteQuickReportAnalysis.Create(data);
        expect.That(report.Results[overridden].Method == full.Method && report.Results[overridden].Model == full.Model
            && report.Results[overridden].Resistances.Select(r => r.Moment).SequenceEqual(full.Resistances.Select(r => r.Moment)), "Modello elastico e segno esplicito invariati nel report");
        foreach (string shape in new[] { "Circolare", "A T" })
        {
            var alternate = ConcreteQuickSection.Create(); alternate["input"]!["shape"] = shape;
            ConcreteQuickSection.WriteLayer(alternate, 0, "n", "3"); ConcreteQuickSection.WriteLayer(alternate, 0, "phi", "16");
            ConcreteQuickSection.WriteLayer(alternate, 1, "n", "3"); ConcreteQuickSection.WriteLayer(alternate, 1, "phi", "16");
            if (shape == "A T") { alternate["input"]!["flange_width_mm"] = "600"; alternate["input"]!["web_width_mm"] = "300"; alternate["input"]!["flange_thickness_mm"] = "150"; }
            ConcreteQuickSection.View(alternate)["flessione"] = "Deviata";
            ConcreteQuickSection.ReplaceCombinations(alternate, [new("Deviata", -450, 90, 50)]);
            var original = ConcreteQuickSection.Calculate(alternate); var exported = ConcreteQuickReportAnalysis.Create(alternate);
            expect.That(exported.Report.Shape == shape && !exported.Report.Straight && exported.Report.Cases[0].My == "50"
                && exported.Report.Cases[0].Checks[0].Value == ConcreteQuickSection.Summary(original)!.Value
                && exported.Report.Reinforcement.Count == original.Preview.Geometry.Bars.Count, "Geometria, barre e punto resistente in flessione deviata: " + shape);
        }
    }
}
