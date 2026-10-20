using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.ConcreteWorkspace;
using Anthea.Reports;
using X.Core;

internal static class ConcreteStadiumChecks
{
    internal static int Run(string output)
    {
        int count = 0;
        void Check(bool ok, string message) { count++; if (!ok) throw new InvalidOperationException(message); }
        var document = DocumentService.CreateDocument("str_palo"); var data = document["dati"]!.AsObject();
        ConcreteSheetPreparation.PrepareReport(data);
        var input = data["input"]!.AsObject(); var settings = data["workspace_ca"]!.AsObject();
        var draft = ConcreteTemplateWizard.Draft(input);
        ConcreteTemplateWizard.Select(draft, ConcreteTemplateWizard.Choices[7]);
        foreach (var (key, value) in new[] {
            ("width_mm", "5000"), ("height_mm", "2500"), ("cover_mm", "50"),
            ("transverse_bar_diameter_mm", "12"), ("transverse_spacing_mm", "150"),
            ("wizard_stadium_curve_count", "17"), ("wizard_stadium_curve_diameter_mm", "32"),
            ("wizard_stadium_straight_count", "8"), ("wizard_stadium_straight_diameter_mm", "25") })
            draft[key] = value;
        ConcreteTemplateWizard.Apply(data, draft);
        settings["dettagli_costruttivi"]!["elemento"] = "Pilastro";
        settings["dettagli_costruttivi"]!["barre_trattenute"] = "Da confermare";
        settings["taglio"]!["azioni"] = new JsonArray();
        var actions = new[] { J.Obj(("nome", "Pilastro 5000 × 2500"), ("N", "-5000")) };
        var section = new SezioneCA(input);
        Check(section.Bars.Count == 50 && section.Bars.Count(b => b.Diametro == 32) == 34 &&
            section.Bars.Count(b => b.Diametro == 25) == 16, "Capsula: conteggi o diametri dei due tratti.");
        var details = ConcreteDetailingAnalysis.Calculate(input, settings, actions);
        Check(details.MaximumCompression == 5000 && details.Checks.Count >= 8, "Capsula: dettagli non calcolati.");
        var spacing = details.Checks.Single(c => c.Name == "Interasse longitudinale");
        Check(spacing.Passed == true && Math.Abs(spacing.Actual!.Value - 2500d / 9) < .1 &&
            spacing.Limit == 300 && spacing.Explanation.Contains("percorso chiuso"), "Capsula: interasse lungo il contorno.");
        Check(details.Checks.Any(c => c.Name == "Barre trattenute dalle staffe" && c.Passed is null),
            "Capsula: staffa perimetrale scambiata per conferma del trattenimento.");
        var shown = ConcreteDetailingPresentation.Calculate(input, settings, actions);
        Check(shown.Checks.Count == details.Checks.Count && shown.DetailsText.Contains("5000"),
            "Capsula: risultati mancanti nella presentazione.");
        string file = Path.Combine(output, "capsula-pilastro-5000x2500.anthea");
        DocumentService.Save(file, document);
        var restored = DocumentService.Open(file)["dati"]!.AsObject();
        Check(restored["input"]![ConcreteSectionArchive.Key]!.S("stato") == "modello",
            "Capsula: manca l'archivio nativo GPC.");
        var actual = new SezioneCA(restored["input"]!.AsObject());
        Check(actual.Bars.SequenceEqual(section.Bars), "Capsula: barre modificate alla riapertura.");
        var after = ConcreteDetailingAnalysis.Calculate(restored["input"]!.AsObject(),
            restored["workspace_ca"]!.AsObject(), actions);
        Check(details.Checks.SequenceEqual(after.Checks), "Capsula: dettagli modificati alla riapertura.");
        var result = J.Obj(("motore", "GPCChecker.Concrete.dll"), ("normativa_riferimento", "NTC 2018"), ("dati", data.DeepClone()));
        var report = ReportConcrete.CreateDocument("Capsula 5000 × 2500", data, result, ["geometria"]);
        string text = report.GetXml("word/document.xml").Root!.Value;
        Check(text.Contains("Barre per arco, tangenti incluse") && text.Contains("Barre interne per rettilineo") &&
            text.Contains("Ø barre curve [mm]") && text.Contains("Ø barre rettilinei [mm]"), "Capsula: ricetta non presente nel report.");

        var manual = input.DeepClone().AsObject();
        manual["barre_manuali"] = new JsonArray(section.Bars.Select(b => (JsonNode)J.Obj(("x", b.X), ("y", b.Y), ("phi", b.Diametro))).ToArray());
        manual["barre_manuali"]![0]!["x"] = "0"; manual["barre_manuali"]![0]!["y"] = "0";
        var moved = ConcreteDetailingAnalysis.Calculate(manual, settings, actions);
        Check(moved.Checks.Single(c => c.Name == "Interasse longitudinale").Passed is null &&
            moved.Checks.Count == details.Checks.Count, "Capsula: barra manuale fuori percorso non segnalata.");

        var legacy = input.DeepClone().AsObject();
        foreach (string key in legacy.Select(p => p.Key).Where(k => k.StartsWith("wizard_stadium_")).ToArray()) legacy.Remove(key);
        legacy["wizard_type"] = "Capsula · armatura radiale";
        var legacySection = new SezioneCA(legacy);
        var legacyDraft = ConcreteTemplateWizard.Draft(legacy);
        Check(legacyDraft.S("wizard_stadium_layout") == ConcreteTemplateAdapter.StadiumLegacy &&
            new SezioneCA(legacyDraft).Bars.SequenceEqual(legacySection.Bars),
            "Capsula storica: aprire il wizard cambia le armature.");
        Check(ConcreteDetailingAnalysis.Calculate(legacy, settings, actions).Checks.Count >= 8,
            "Capsula storica: dettagli ancora rifiutati.");
        File.WriteAllText(Path.Combine(output, "capsula-dettagli.json"), J.Node(details).ToJsonString(J.Options));
        return count;
    }
}
