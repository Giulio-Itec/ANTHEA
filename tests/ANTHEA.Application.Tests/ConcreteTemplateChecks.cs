using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Calculations;
using Anthea.ConcreteWorkspace;
using Anthea.Reports;
using Anthea.Reports.Pdf;
using X.Core;

internal static class ConcreteTemplateChecks
{
    internal static int Run(string output)
    {
        int count = 0;
        void Check(bool good, string message) { count++; if (!good) throw new InvalidOperationException(message); }
        var evidence = new JsonArray();
        foreach (string choice in ConcreteTemplateWizard.Choices)
        {
            var document = DocumentService.CreateDocument("str_palo");
            var data = document["dati"]!.AsObject();
            ConcreteSheetPreparation.PrepareReport(data);
            data["workspace_ca"]!["taglio"]!["tipo_staffa"] = "Spirale";
            data["workspace_ca"]!["taglio"]!["rami_interni"] = "8";
            data["workspace_ca"]!["taglio"]!["alpha_x"] = "45";
            string initial = data.ToJsonString();
            var draft = ConcreteTemplateWizard.Draft(data["input"]!.AsObject());
            ConcreteTemplateWizard.Select(draft, choice);
            var preview = ConcreteTemplateWizard.Preview(draft);
            Check(data.ToJsonString() == initial, choice + ": il wizard modifica la sezione prima della conferma");
            Check(preview.Bars.Count > 0 && preview.AreaCls > 0, choice + ": sezione vuota");
            ConcreteTemplateWizard.Apply(data, draft);
            var input = data["input"]!.AsObject(); var settings = data["workspace_ca"]!.AsObject();
            Check(settings["taglio"].S("tipo_staffa") == "Staffa chiusa" &&
                settings["taglio"].S("rami_interni") == "0" && settings["taglio"].S("alpha_x") == "90",
                choice + ": schema trasversale precedente ereditato");
            var prepared = CheckerSection.PrepareModel(input, settings);
            Check(prepared.Section.Rebars.Count() == preview.Bars.Count, choice + ": anteprima e modello diversi");
            if (ConcreteTemplateAdapter.Uses(input))
            {
                Check(preview.TemplateLinks.Count > 0, choice + ": staffe mancanti");
                Check(settings["taglio"].S("parametri") == "Manuali" && settings["taglio"].S("bw_x") == "" && settings["taglio"].S("rami_y") == "",
                    choice + ": parametri del taglio precedenti ereditati");
                bool rejected = false;
                try { _ = SectionShearGeometry.Derive(new(input), true); }
                catch (ArgumentException) { rejected = true; }
                Check(rejected, choice + ": automatico non validato accettato");
            }
            string path = Path.Combine(output, "wizard-" + ConcreteTemplateWizard.Choices.IndexOf(choice) + ".json");
            DocumentService.Save(path, document);
            var restored = DocumentService.Open(path)["dati"]!.AsObject();
            var model = CheckerSection.PrepareModel(restored["input"]!.AsObject(), restored["workspace_ca"]!.AsObject());
            Check(model.Section.Area == prepared.Section.Area && model.Section.Jxx == prepared.Section.Jxx &&
                model.Section.Jyy == prepared.Section.Jyy, choice + ": proprietà diverse dopo riapertura");
            Check(model.Section.Rebars.Select(b => (b.Position.X, b.Position.Y, b.Area)).SequenceEqual(
                prepared.Section.Rebars.Select(b => (b.Position.X, b.Position.Y, b.Area))), choice + ": barre diverse dopo riapertura");
            var options = settings["sle"]!["SLE"]!.AsObject();
            var force = new ActionPoint(-500, 10, 5);
            var stressBefore = new CheckerSection(prepared, input, settings, options).Stress(force, "SLE");
            var stressAfter = new CheckerSection(model, restored["input"]!.AsObject(),
                restored["workspace_ca"]!.AsObject(), options).Stress(force, "SLE");
            Check(stressBefore.tensioni_barre.Length > 0 && stressBefore.FiberStresses.Length > 0 &&
                stressBefore.tensioni_barre.All(double.IsFinite) && stressBefore.FiberStresses.All(double.IsFinite),
                choice + ": calcolo delle tensioni vuoto");
            Check(stressBefore.tensioni_barre.SequenceEqual(stressAfter.tensioni_barre) &&
                stressBefore.FiberStresses.SequenceEqual(stressAfter.FiberStresses),
                choice + ": tensioni diverse dopo riapertura");
            // Geometry-only report fixture: no verification outcome is fabricated.
            var reportResult = J.Obj(("motore", "GPCChecker.Concrete.dll"),
                ("normativa_riferimento", settings.S("normativa")), ("dati", data.DeepClone()));
            var report = ReportConcrete.CreateDocument("Controllo geometria: " + choice, data, reportResult, ["geometria"]);
            var reportText = report.GetXml("word/document.xml").Root!.Value;
            Check(reportText.Contains(input.S("shape")), choice + ": forma assente dal report");
            if (input.S("shape") is "A I" or "A C")
                Check(reportText.Contains("Spessore ala inferiore [mm]") && reportText.Contains("800"),
                    choice + ": dimensioni delle ali assenti dal report");
            if (ConcreteTemplateAdapter.Uses(input))
                Check(reportText.Contains("Staffature tipologiche") && reportText.Contains("ripartizione del taglio"),
                    choice + ": limiti della staffatura assenti dal report");
            string reportPath = Path.ChangeExtension(path, ".docx");
            File.WriteAllBytes(reportPath, DocxWriter.Write(report));
            var pdf = DocxPdf.Render(report, "Controllo geometria: " + choice);
            Check(pdf.Pages > 0, choice + ": report PDF vuoto");
            File.WriteAllBytes(Path.ChangeExtension(path, ".pdf"), pdf.Bytes);
            // No geometry/body bounding-box approximation is substituted for the new native shape.
            if (input.S("shape") is "A L" or "A C")
                Check(model.Section.Area < preview.Width * preview.Height, choice + ": profilo aperto riempito");
            var bad = draft.DeepClone().AsObject(); bad["cover_mm"] = "10000";
            string beforeBad = data.ToJsonString();
            bool failed = false;
            try { ConcreteTemplateWizard.Apply(data, bad); } catch (ArgumentException) { failed = true; }
            Check(failed && data.ToJsonString() == beforeBad, choice + ": applicazione non atomica di geometria non valida");
            evidence.Add(J.Obj(("tipologia", choice), ("area_gpc_mm2", model.Section.Area), ("barre", preview.Bars.Count),
                ("staffe_tipologiche", preview.TemplateLinks.Count), ("persistenza_identica", true),
                ("tensioni_identiche", true)));
        }
        File.WriteAllText(Path.Combine(output, "wizard-sezioni.json"), evidence.ToJsonString(J.Options));
        return count;
    }
}
