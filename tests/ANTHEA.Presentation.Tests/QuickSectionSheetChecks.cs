using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Documents;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.QuickSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The quick RC section (17/11/2026): the module and its document (the format of the RC section), the same engine of the RC section (the outcome of
/// the sheet equals the headless calculation of <c>str_palo</c> on the same data), the ViewModel (opening, edits, choices, read-only revision,
/// reload, save and reopen, invalid data, no report) and the examples of the distribution.
/// </summary>
internal static class QuickSectionSheetChecks
{
    /// <summary>The folder of the examples of the module in the distribution.</summary>
    internal static string ExampleFolder(string root) => Path.Combine(root, "supporto/esempi/distribuzione", ConcreteQuickSection.Module);

    internal static int Run(string root, string output) => SingleThreadContext.Run(async _ =>
    {
        var expect = new Expect();
        string folder = Path.Combine(output, "sezione-rapida"); Directory.CreateDirectory(folder);
        QuickSectionSheetViewModel Open(JsonObject data, bool readOnly = false) =>
            new(new(data, readOnly, new Host(), Scripted.Services(), ModuleServices.Get(ConcreteQuickSection.Module), new CalculationLimiter(1)));

        // The module and its document.
        var module = new QuickSectionSheetModule();
        expect.That(module.Info.Area == "Strutture" && module.Info.Id == ConcreteQuickSection.Module && ModuleDirectory.Get(ConcreteQuickSection.Module) == module.Info,
            "Modulo nel gruppo Strutture del catalogo");
        expect.That(ModuleServices.Get(ConcreteQuickSection.Module) is ConcreteQuickSectionService, "Servizio di calcolo del modulo");
        var created = ModuleDirectory.CreateData(ConcreteQuickSection.Module);
        ModuleDirectory.ValidateData(ConcreteQuickSection.Module, created);
        ModuleCatalog.ValidateData(ConcreteQuickSection.SourceModule, created);
        expect.That(created["input"] is JsonObject && created["workspace_ca"] is JsonObject && ConcreteQuickSection.Action(created, "SLU") is not null
            && ConcreteQuickSection.Action(created, "SLE") is not null, "Documento nel formato della sezione in c.a. con le due sollecitazioni");
        var wrong = ConcreteQuickSection.Create(); ConcreteQuickSection.View(wrong)["metodo"] = "Tensioni ammissibili";
        expect.Throws<ArgumentException>(() => ConcreteQuickSection.Validate(wrong), "Metodo non previsto rifiutato");
        wrong = ConcreteQuickSection.Create(); wrong.Remove(ConcreteQuickSection.ViewKey);
        expect.Throws<ArgumentException>(() => ConcreteQuickSection.Validate(wrong), "Scelte della vista mancanti rifiutate");

        // The same engine: the sheet and the headless calculation of the RC section on the same data give the same numbers.
        var same = ConcreteQuickSection.Create();
        var reference = (JsonObject)same.DeepClone();
        string ultimateId = ConcreteQuickSection.Action(same, "SLU")!.S("id"), serviceId = ConcreteQuickSection.Action(same, "SLE")!.S("id");
        reference["workspace_ca"]!["dominio2d"]!["tipo"] = "Mx–My"; reference["workspace_ca"]!["dominio2d"]!["N"] = ConcreteQuickSection.Action(same, "SLU")!.S("N");
        var headless = ConcreteAnalysis.Calculate(reference);
        var quick = ConcreteQuickSection.Calculate(same);
        double? headlessRatio = J.Number(headless["domini"]?["2D:SLU"]?[ultimateId]?["Utilization"]);
        expect.That(quick.UltimateCheck?.Utilization is double ratio && headlessRatio == ratio, $"η come il calcolo della sezione in c.a.: {quick.UltimateCheck?.Utilization} / {headlessRatio}");
        double mxPlus = quick.Resistances.First(r => r.Direction == "Mx+").Moment!.Value;
        expect.That(Math.Abs(quick.UltimateCheck!.Utilization!.Value - quick.UltimateAction!.Value.Mx / mxPlus) < 0.02,
            $"Flessione retta: η a N costante coerente con MEd / MRd ({quick.UltimateCheck.Utilization} / {quick.UltimateAction.Value.Mx / mxPlus})");
        expect.That(quick.DomainGeometry.Title?.Contains("N–M", StringComparison.Ordinal) == true, "Flessione retta: grafico del dominio N–M");
        var serviceView = ConcreteQuickSection.Create(); ConcreteQuickSection.View(serviceView)["metodo"] = ConcreteQuickSection.Methods[2];
        var service = ConcreteQuickSection.Calculate(serviceView);
        double? headlessSigma = J.Number(headless["tensioni"]?["SLE"]?[serviceId]?["State"]?["sigma_cls"]);
        expect.That(service.ServiceState?.sigma_cls is double sigma && headlessSigma == sigma, $"σc come il calcolo della sezione in c.a.: {service.ServiceState?.sigma_cls} / {headlessSigma}");
        double? headlessSteel = J.Number(headless["tensioni"]?["SLE"]?[serviceId]?["State"]?["sigma_acciaio"]);
        expect.That(service.ServiceState?.sigma_acciaio == headlessSteel, "σs come il calcolo della sezione in c.a.");
        var prepared = (JsonObject)same.DeepClone(); var preparedSettings = SectionWorkspace.Prepare(prepared);
        var resistances = StructuralQueries.ConcreteQuickResistance(prepared["input"]!.AsObject(), preparedSettings, quick.UltimateAction!.Value.N, false);
        expect.That(resistances.Select(r => r.Moment).SequenceEqual(quick.Resistances.Select(r => r.Moment)) && quick.Resistances.Count == 4,
            "Resistenze a N costante come le «Resistenze rapide» della sezione in c.a.");

        // The sheet at opening: calculated, not modified.
        using var sheet = Open(ConcreteQuickSection.Create());
        int modified = 0; sheet.Modified += (_, _) => modified++;
        expect.That(await Expect.Within(sheet.PendingCalculation) && sheet.HasResults && sheet.Revision == 0 && modified == 0, "Apertura calcolata senza modificare il foglio: " + sheet.CalculationMessage);
        expect.That(sheet.Section is not null && sheet.Section.Bars.Count > 0 && sheet.SectionStress is not null, "Sezione disegnata con lo stato al punto resistente");
        expect.That(sheet.ResultValues.Any(v => v.Label.StartsWith("Mx,Rd (Mx+)", StringComparison.Ordinal)) && sheet.ResultValues.Any(v => v.Label == "η"), "Risultati SLU+: MRd ed η");
        expect.That(sheet.MaterialValues.Any(v => v.Label == "fcd" && v.Value != "—"), "Materiali con i valori del motore");
        expect.That(sheet.DomainGeometry is { Segments.Length: > 2 } && sheet.DomainActions is { Markers.Count: > 0 }, "Dominio N–M con il punto dell'azione");

        // Edits of the actions and of the materials.
        sheet.UltimateForm.Set("Mx", "250");
        expect.That(ConcreteQuickSection.Action(sheet.Data, "SLU")!.S("Mx") == "250" && modified == 1 && !sheet.HasResults, "Modifica di MxEd nel documento");
        await sheet.PendingCalculation;
        expect.That(sheet.HasResults && sheet.ResultValues.Any(v => v.Label == "Mx,Ed" && v.Value.StartsWith("250", StringComparison.Ordinal)), "Ricalcolo automatico dopo la modifica");
        sheet.MaterialsForm.Set("classe_cls", "C25/30");
        expect.That(sheet.Data["input"].S("classe_cls") == "C25/30" && J.Number(sheet.Data["input"]!["fck_mpa"]) == 25, "Classe del calcestruzzo applicata dal catalogo");
        await sheet.PendingCalculation;

        // The choices: SLU−, biaxial bending, method n, moment–curvature.
        sheet.Method = ConcreteQuickSection.Methods[1]; await sheet.PendingCalculation;
        expect.That(sheet.ResultValues.Any(v => v.Label.StartsWith("Mx,Rd (Mx−)", StringComparison.Ordinal)), "SLU−: resistenza a momento negativo");
        sheet.Bending = ConcreteQuickSection.Bendings[1];
        expect.That(!sheet.UltimateForm.Fields.Single(f => f.Key == "My").IsReadOnly && !sheet.IsStraight, "Flessione deviata: My modificabile");
        sheet.UltimateForm.Set("My", "60"); await sheet.PendingCalculation;
        expect.That(sheet.HasResults && sheet.ResultValues.Any(v => v.Label == "My,Ed") && sheet.ResultValues.Count(v => v.Label.EndsWith(",Rd", StringComparison.Ordinal)) == 4,
            "Flessione deviata: My,Ed e le quattro resistenze principali");
        sheet.Method = ConcreteQuickSection.Methods[2]; await sheet.PendingCalculation;
        expect.That(sheet.IsServiceMethod && sheet.ResultValues.Any(v => v.Label == "σc") && sheet.SectionStress is not null, "Metodo n: tensioni del motore");
        int edits = modified;
        sheet.Display = "Sezione";
        expect.That(sheet.ShowsSection && modified == edits, "Area di disegno sulla sezione: stato della vista, senza modifiche");
        sheet.Display = ConcreteQuickSection.Charts[1]; await sheet.PendingCalculation;
        expect.That(!sheet.ShowsSection && !sheet.ShowsDomain && sheet.Chart == ConcreteQuickSection.Charts[1] && sheet.CurvaturePoints.Count > 2 && modified == edits + 1,
            "Grafico momento–curvatura scelto dall'area di disegno");
        sheet.Shape = "Circolare"; await sheet.PendingCalculation;
        expect.That(sheet.GeometryForm.Fields.Any(f => f.Key == "diameter_mm") && sheet.BarsForm.Fields.Any(f => f.Key == "longitudinal_bar_count")
            && sheet.HasResults && sheet.Section?.Shape == "Circolare", "Sezione circolare: campi e calcolo");
        sheet.Title = "Prova";
        expect.That(ConcreteQuickSection.View(sheet.Data).S("titolo") == "Prova", "Titolo nel documento");
        await sheet.PendingCalculation;

        // Save and reopen.
        sheet.Commit();
        var saved = (JsonObject)sheet.Data.DeepClone();
        var document = DocumentService.CreateDocument(ConcreteQuickSection.Module); document["dati"] = saved.DeepClone();
        string file = Path.Combine(folder, "sezione-rapida.anthea"); DocumentService.Save(file, document);
        using (var reopened = Open(DocumentService.Open(file)["dati"]!.AsObject()))
        {
            await reopened.PendingCalculation;
            expect.That(JsonNode.DeepEquals(reopened.Data, saved) && JsonNode.DeepEquals(reopened.Result, sheet.Result) && reopened.Shape == "Circolare"
                && reopened.Method == ConcreteQuickSection.Methods[2] && reopened.Title == "Prova", "Salvataggio e riapertura esatti");
        }

        // Read-only revision.
        sheet.IsReadOnly = true; int before = modified;
        sheet.Shape = "Rettangolare"; sheet.Method = ConcreteQuickSection.Methods[0]; sheet.Title = "Altro";
        expect.That(JsonNode.DeepEquals(sheet.Data, saved) && modified == before && sheet.Shape == "Circolare" && sheet.UltimateForm.IsReadOnly, "Revisione in sola lettura protegge i dati");
        sheet.IsReadOnly = false;

        // Invalid data: no results, the reason in the message; reload restores.
        sheet.GeometryForm.Set("diameter_mm", "abc");
        await sheet.PendingCalculation;
        expect.That(!sheet.HasResults && sheet.Current is null, "Dati non validi: nessun risultato");
        expect.That(sheet.Section is null && sheet.DrawingMessage.Length > 0, "Dati non validi: sezione non disegnata, con il motivo: " + sheet.DrawingMessage);
        expect.That(sheet.Status.StartsWith("Verifica non disponibile", StringComparison.Ordinal) && sheet.CalculationMessage.Length > 0, "Dati non validi: motivo nello stato: " + sheet.Status);
        sheet.Reload(saved); await sheet.PendingCalculation;
        expect.That(sheet.HasResults && JsonNode.DeepEquals(sheet.Data, saved) && sheet.GeometryForm.Fields.Any(f => f.Key == "diameter_mm"), "Ricarica ripristina dati e risultati");

        // No report in this version.
        expect.Throws<InvalidOperationException>(() => sheet.Report.BuildReport("Sezione", new HashSet<string>()), "Relazione non disponibile segnalata");

        // The examples of the distribution: valid documents that calculate without errors.
        var examples = Directory.Exists(ExampleFolder(root)) ? Directory.GetFiles(ExampleFolder(root), "*.anthea") : [];
        expect.That(examples.Length >= 2, "Esempi della verifica rapida nella distribuzione");
        foreach (string path in examples)
        {
            var example = DocumentService.Open(path);
            var calculation = ModuleServices.Get(ConcreteQuickSection.Module).Calculate(example["dati"]!.AsObject());
            expect.That(example.S("modulo_id") == ConcreteQuickSection.Module && !calculation.HasError, "Esempio calcolabile: " + Path.GetFileName(path) + " " + calculation.Values.S("errore"));
        }
        return expect.Count;
    });

    /// <summary>
    /// Writes the examples of the distribution (17/11/2026), built with the code of the module: a beam (SLU+, straight bending), a column (biaxial
    /// bending) and a circular pile (method n). Run with <c>--sezione-rapida-esempi &lt;cartella&gt;</c>; the files are then copied into the distribution.
    /// </summary>
    internal static IReadOnlyList<string> WriteExamples(string target)
    {
        Directory.CreateDirectory(target);
        var written = new List<string>();
        void Example(string name, Action<JsonObject> setup)
        {
            var data = ConcreteQuickSection.Create();
            setup(data);
            ConcreteQuickSection.ApplyMaterial(data, "classe_cls"); ConcreteQuickSection.ApplyMaterial(data, "classe_acciaio");
            ConcreteQuickSection.Validate(data);
            var document = DocumentService.CreateDocument(ConcreteQuickSection.Module); document["dati"] = data;
            string path = Path.Combine(target, name + ".anthea"); DocumentService.Save(path, document); written.Add(path);
        }
        void Set(JsonObject data, params (string Key, string Value)[] values) { foreach (var (key, value) in values) data["input"]![key] = value; }
        void Act(JsonObject data, string state, string n, string mx, string my)
        {
            ConcreteQuickSection.WriteAction(data, state, "N", n); ConcreteQuickSection.WriteAction(data, state, "Mx", mx); ConcreteQuickSection.WriteAction(data, state, "My", my);
        }
        Example("trave-flessione-retta", d =>
        {
            Set(d, ("shape", "Rettangolare"), ("width_mm", "300"), ("height_mm", "500"), ("cover_mm", "30"), ("transverse_bar_diameter_mm", "8"),
                ("top_bar_count", "2"), ("top_bar_diameter_mm", "14"), ("bottom_bar_count", "4"), ("bottom_bar_diameter_mm", "20"), ("side_bar_count_per_side", "0"),
                ("classe_cls", "C25/30"), ("classe_acciaio", "B450C"));
            Act(d, "SLU", "0", "150", "0"); Act(d, "SLE", "0", "100", "0");
            ConcreteQuickSection.View(d)["titolo"] = "Trave 30×50 · flessione retta";
        });
        Example("pilastro-flessione-deviata", d =>
        {
            Set(d, ("shape", "Rettangolare"), ("width_mm", "400"), ("height_mm", "400"), ("cover_mm", "35"), ("transverse_bar_diameter_mm", "8"),
                ("top_bar_count", "3"), ("top_bar_diameter_mm", "20"), ("bottom_bar_count", "3"), ("bottom_bar_diameter_mm", "20"), ("side_bar_count_per_side", "1"),
                ("side_bar_diameter_mm", "20"), ("classe_cls", "C30/37"), ("classe_acciaio", "B450C"));
            Act(d, "SLU", "-1200", "120", "80"); Act(d, "SLE", "-900", "80", "50");
            var view = ConcreteQuickSection.View(d); view["titolo"] = "Pilastro 40×40 · flessione deviata"; view["flessione"] = ConcreteQuickSection.Bendings[1];
        });
        Example("palo-circolare-metodo-n", d =>
        {
            Set(d, ("shape", "Circolare"), ("diameter_mm", "800"), ("cover_mm", "60"), ("transverse_bar_diameter_mm", "10"),
                ("longitudinal_bar_count", "14"), ("longitudinal_bar_diameter_mm", "22"), ("classe_cls", "C25/30"), ("classe_acciaio", "B450C"));
            Act(d, "SLU", "-1500", "450", "0"); Act(d, "SLE", "-1100", "300", "0");
            var view = ConcreteQuickSection.View(d); view["titolo"] = "Palo Ø800 · metodo n"; view["metodo"] = ConcreteQuickSection.Methods[2]; view["grafico"] = ConcreteQuickSection.Charts[1];
        });
        return written;
    }

    private sealed class Host : ISheetHost
    {
        public void OpenModuleCopy(string module, JsonObject data, string name) => throw new InvalidOperationException();
        public void ReplaceSoil(JsonObject data) => throw new InvalidOperationException();
        public IReadOnlyList<SoilSource> SoilSources() => [];
        public void RequestReport() => throw new InvalidOperationException();
    }
}
