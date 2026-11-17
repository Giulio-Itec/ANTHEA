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
/// the sheet equals the headless calculation of <c>str_palo</c> on the same data), the layers of bars, the resistance model, the behaviour, the
/// ViewModel (opening, edits, choices, read-only revision, reload, save and reopen, invalid data, no report) and the examples of the distribution.
/// </summary>
internal static class QuickSectionSheetChecks
{
    /// <summary>The folder of the examples of the module in the distribution.</summary>
    internal static string ExampleFolder(string root) => Path.Combine(root, "supporto/esempi/distribuzione", ConcreteQuickSection.Module);

    /// <summary>Writes N, Mx and My of the combination <paramref name="index"/> (the first by default).</summary>
    private static void Act(JsonObject data, string n, string mx, string my = "0", int index = 0)
    {
        string id = ConcreteQuickSection.Combinations(data)[index].S("id");
        ConcreteQuickSection.WriteCombination(data, id, "N", n); ConcreteQuickSection.WriteCombination(data, id, "Mx", mx); ConcreteQuickSection.WriteCombination(data, id, "My", my);
    }

    internal static int Run(string root, string output) => SingleThreadContext.Run(async _ =>
    {
        var expect = new Expect();
        string folder = Path.Combine(output, "sezione-rapida"); Directory.CreateDirectory(folder);
        var messages = new ScriptedMessages();
        QuickSectionSheetViewModel Open(JsonObject data, bool readOnly = false) =>
            new(new(data, readOnly, new Host(), Scripted.Services(messages), ModuleServices.Get(ConcreteQuickSection.Module), new CalculationLimiter(1)));

        // The module and its document: actions at zero, two layers of bars written as the bars by hand of the RC section, plastic model.
        var module = new QuickSectionSheetModule();
        expect.That(module.Info.Area == "Strutture" && module.Info.Id == ConcreteQuickSection.Module && ModuleDirectory.Get(ConcreteQuickSection.Module) == module.Info,
            "Modulo nel gruppo Strutture del catalogo");
        expect.That(ModuleServices.Get(ConcreteQuickSection.Module) is ConcreteQuickSectionService, "Servizio di calcolo del modulo");
        var created = ModuleDirectory.CreateData(ConcreteQuickSection.Module);
        ModuleDirectory.ValidateData(ConcreteQuickSection.Module, created);
        ModuleCatalog.ValidateData(ConcreteQuickSection.SourceModule, created);
        var firstCombination = ConcreteQuickSection.Combinations(created).Single();
        expect.That(created["input"] is JsonObject && created["workspace_ca"] is JsonObject && firstCombination.S("nome") == "Combo 1"
            && ConcreteQuickSection.Action(created, "SLE")?.S("id") == firstCombination.S("id") + "-n" && ConcreteQuickSection.SelectedCombination(created) == firstCombination.S("id"),
            "Documento nel formato della sezione in c.a.: una combinazione, selezionata, copiata nelle righe SLE");
        expect.That(new[] { "SLU", "SLE" }.All(s => ConcreteQuickSection.ActionComponents.All(c => J.Number(JsonValue.Create(ConcreteQuickSection.Action(created, s)!.S(c))) == 0)),
            "Foglio nuovo: tutte le sollecitazioni a zero");
        var combos = ConcreteQuickSection.Create();
        string second = ConcreteQuickSection.AddCombination(combos);
        expect.That(ConcreteQuickSection.Combinations(combos).Count == 2 && ConcreteQuickSection.Combinations(combos)[1].S("nome") == "Combo 2"
            && ConcreteQuickSection.SelectedCombination(combos) == second && ConcreteQuickSection.Combinations(combos).Count == ConcreteSheetPreparation.ReadActions(combos)["SLE"].Length,
            "«+»: Combo 2 aggiunta, selezionata e copiata nelle righe SLE");
        ConcreteQuickSection.RemoveCombination(combos, second);
        ConcreteQuickSection.RemoveCombination(combos, ConcreteQuickSection.Combinations(combos)[0].S("id"));
        expect.That(ConcreteQuickSection.Combinations(combos).Count == 1 && ConcreteQuickSection.SelectedCombination(combos) == ConcreteQuickSection.Combinations(combos)[0].S("id"),
            "«✕»: combinazione tolta, l'ultima resta, la selezione torna sulla prima");
        expect.That(ConcreteQuickSection.Layers(created).Count == 2 && created["input"]!["barre_manuali"] is JsonArray { Count: 10 } && ConcreteQuickSection.Model(created) == "Plastico",
            "Foglio nuovo: due strati (4 + 6 barre) scritti come barre del motore, modello plastico");
        var wrong = ConcreteQuickSection.Create(); ConcreteQuickSection.View(wrong)["metodo"] = "Tensioni ammissibili";
        expect.Throws<ArgumentException>(() => ConcreteQuickSection.Validate(wrong), "Metodo non previsto rifiutato");
        wrong = ConcreteQuickSection.Create(); wrong.Remove(ConcreteQuickSection.ViewKey);
        expect.Throws<ArgumentException>(() => ConcreteQuickSection.Validate(wrong), "Scelte della vista mancanti rifiutate");

        // The layers: the bars of «Sopra» and «Sotto» at c + Ø link + Ø / 2 from the edges, spread between the inner faces of the links.
        var layered = ConcreteQuickSection.Create();
        var bars = layered["input"]!["barre_manuali"]!.AsArray().Select(b => (X: J.Number(b!["x"])!.Value, Y: J.Number(b["y"])!.Value, Phi: J.Number(b["phi"])!.Value)).ToArray();
        double h = 800, b = 600, c = 70, link = 10;
        expect.That(bars.Take(4).All(p => Math.Abs(p.Y - (h / 2 - c - link - 10)) < 1e-9) && bars.Skip(4).All(p => Math.Abs(p.Y + h / 2 - c - link - 12) < 1e-9)
            && Math.Abs(bars[0].X + (b / 2 - c - link - 10)) < 1e-9 && Math.Abs(bars[3].X - (b / 2 - c - link - 10)) < 1e-9, "Strati Sopra e Sotto a copriferro e staffa dai bordi");
        ConcreteQuickSection.AddLayer(layered); ConcreteQuickSection.WriteLayer(layered, 2, "d", "400"); ConcreteQuickSection.WriteLayer(layered, 2, "phi", "16");
        expect.That(ConcreteQuickSection.ApplyLayers(layered) is null && layered["input"]!["barre_manuali"]!.AsArray().Skip(10).All(p => J.Number(p!["y"]) == h / 2 - 400),
            "Strato «Da sopra» alla distanza d dal bordo superiore");
        ConcreteQuickSection.WriteLayer(layered, 2, "posizione", "Da sotto"); ConcreteQuickSection.WriteLayer(layered, 2, "d", "100");
        expect.That(ConcreteQuickSection.ApplyLayers(layered) is null && layered["input"]!["barre_manuali"]!.AsArray().Skip(10).All(p => J.Number(p!["y"]) == 100 - h / 2),
            "Strato «Da sotto» alla distanza d dal bordo inferiore");
        var old = ConcreteQuickSection.Create(); ConcreteQuickSection.Layers(old)[0]!["posizione"] = "Sopra"; ConcreteQuickSection.Layers(old)[1]!["posizione"] = "Sotto";
        ConcreteQuickSection.Validate(old);
        expect.That(ConcreteQuickSection.ApplyLayers(old) is null && JsonNode.DeepEquals(old["input"]!["barre_manuali"], ConcreteQuickSection.Create()["input"]!["barre_manuali"])
            && ConcreteQuickSection.LayerPosition("Sopra") == "Superiore", "Nomi della prima versione («Sopra», «Sotto») letti come Superiore e Inferiore");
        ConcreteQuickSection.WriteLayer(layered, 2, "posizione", "Da sopra"); ConcreteQuickSection.WriteLayer(layered, 2, "d", "20");
        expect.That(ConcreteQuickSection.ApplyLayers(layered) is { } tooHigh && tooHigh.StartsWith("Strato 3", StringComparison.Ordinal) && layered["input"]!["barre_manuali"] is JsonArray { Count: 0 },
            "Strato fuori dal copriferro rifiutato con il suo nome");
        expect.Throws<ArgumentException>(() => ConcreteQuickSection.Calculate(layered), "Calcolo rifiutato con uno strato non valido");
        ConcreteQuickSection.RemoveLayer(layered, 2);
        expect.That(ConcreteQuickSection.ApplyLayers(layered) is null && ConcreteQuickSection.Layers(layered).Count == 2, "Strato tolto");

        // The same engine: the sheet and the headless calculation of the RC section on the same data give the same numbers.
        var same = ConcreteQuickSection.Create();
        Act(same, "-1000", "400");
        string otherId = ConcreteQuickSection.AddCombination(same); Act(same, "-2500", "700", index: 1);
        string ultimateId = ConcreteQuickSection.Combinations(same)[0].S("id"), serviceId = ultimateId + "-n";
        ConcreteQuickSection.SelectCombination(same, ultimateId);
        var headless = ConcreteAnalysis.Calculate((JsonObject)same.DeepClone());
        var quick = ConcreteQuickSection.Calculate(same);
        double? headlessRatio = J.Number(headless["domini"]?["3D:SLU"]?[ultimateId]?["Utilization"]);
        expect.That(quick.UltimateCheck?.Utilization is double ratio && headlessRatio == ratio, $"η come il calcolo della sezione in c.a.: {quick.UltimateCheck?.Utilization} / {headlessRatio}");
        expect.That(quick.Combinations.Count == 2 && quick.Combinations.All(c => c.Ratio is double r && J.Number(headless["domini"]?["3D:SLU"]?[c.Id]?["Utilization"]) == r)
            && quick.SelectedId == ultimateId, "η di ogni combinazione come il calcolo della sezione in c.a.");
        var otherSelected = (JsonObject)same.DeepClone(); ConcreteQuickSection.SelectCombination(otherSelected, otherId);
        var other = ConcreteQuickSection.Calculate(otherSelected);
        expect.That(other.UltimateAction?.N == -2500 && other.UltimateCheck?.Utilization == quick.Combinations[1].Ratio && other.Resistances[0].Moment != quick.Resistances[0].Moment,
            "Combinazione selezionata: azione, verifica e resistenze a N costante della combinazione scelta");
        double mxPlus = quick.Resistances.First(r => r.Direction == "Mx+").Moment!.Value;
        expect.That(Math.Abs(quick.UltimateCheck!.Utilization!.Value - quick.UltimateAction!.Value.Mx / mxPlus) < 0.02,
            $"Flessione retta: η a N costante coerente con MEd / MRd ({quick.UltimateCheck.Utilization} / {quick.UltimateAction.Value.Mx / mxPlus})");
        expect.That(quick.DomainGeometry.Title?.Contains("N–M", StringComparison.Ordinal) == true, "Flessione retta: grafico del dominio N–M");
        var summary = ConcreteQuickSection.Summary(quick)!;
        expect.That(summary.Value.StartsWith("MRd = ", StringComparison.Ordinal) && summary.Detail.StartsWith("η = ", StringComparison.Ordinal) && summary.Title.Contains("momento positivo", StringComparison.Ordinal)
            && summary.Ductile == true && quick.YieldStrain is > 1 and < 3, "Riquadro: MRd, η, comportamento duttile con εyd del motore (" + summary.Behaviour + ")");
        var serviceView = (JsonObject)same.DeepClone(); ConcreteQuickSection.View(serviceView)["metodo"] = ConcreteQuickSection.Methods[2];
        var service = ConcreteQuickSection.Calculate(serviceView);
        double? headlessSigma = J.Number(headless["tensioni"]?["SLE"]?[serviceId]?["State"]?["sigma_cls"]);
        expect.That(service.ServiceState?.sigma_cls is double sigma && headlessSigma == sigma, $"σc come il calcolo della sezione in c.a.: {service.ServiceState?.sigma_cls} / {headlessSigma}");
        double? headlessSteel = J.Number(headless["tensioni"]?["SLE"]?[serviceId]?["State"]?["sigma_acciaio"]);
        expect.That(service.ServiceState?.sigma_acciaio == headlessSteel && ConcreteQuickSection.Summary(service)?.Ductile is null, "σs come il calcolo della sezione in c.a.; nessun comportamento con il metodo n");
        var creep = (JsonObject)serviceView.DeepClone(); creep["workspace_ca"]!["sle_comuni"]!["phi"] = "2";
        expect.That(ConcreteQuickSection.Calculate(creep).ServiceState?.sigma_cls == service.ServiceState?.sigma_cls && creep["workspace_ca"]!["sle_comuni"]!["phi"]!.ToString() == "2",
            "Metodo n a breve termine: φ del documento ignorato, documento invariato");
        var prepared = (JsonObject)same.DeepClone(); var preparedSettings = SectionWorkspace.Prepare(prepared);
        var resistances = StructuralQueries.ConcreteQuickResistance(prepared["input"]!.AsObject(), preparedSettings, quick.UltimateAction!.Value.N, false);
        expect.That(resistances.Select(r => r.Moment).SequenceEqual(quick.Resistances.Select(r => r.Moment)) && quick.Resistances.Count == 4,
            "Resistenze a N costante come le «Resistenze rapide» della sezione in c.a.");
        // The elastic model: the model of the RC section for the domain and the elastic resistances.
        var elastic = (JsonObject)same.DeepClone(); ConcreteQuickSection.WriteModel(elastic, "Elastico");
        var elasticResult = ConcreteQuickSection.Calculate(elastic);
        var elasticResistances = StructuralQueries.ConcreteQuickResistance(prepared["input"]!.AsObject(), preparedSettings, -1000, true);
        expect.That(elastic["workspace_ca"]!["modello_resistente"]!["SLU"]!.ToString() == "Elastico" && elasticResult.Model == "Elastico"
            && elasticResistances.Select(r => r.Moment).SequenceEqual(elasticResult.Resistances.Select(r => r.Moment))
            && elasticResult.Resistances.First(r => r.Direction == "Mx+").Moment < mxPlus && elasticResult.UltimateCheck?.Utilization > quick.UltimateCheck.Utilization,
            "Verifica elastica: il modello della sezione in c.a., resistenze elastiche minori di quelle plastiche");

        // The sheet at opening: calculated, not modified, actions at zero.
        using var sheet = Open(ConcreteQuickSection.Create());
        int modified = 0; sheet.Modified += (_, _) => modified++;
        expect.That(await Expect.Within(sheet.PendingCalculation) && sheet.HasResults && sheet.Revision == 0 && modified == 0, "Apertura calcolata senza modificare il foglio: " + sheet.CalculationMessage);
        expect.That(sheet.Section is not null && sheet.Section.Bars.Count == 10 && sheet.Summary?.Value.StartsWith("MRd = ", StringComparison.Ordinal) == true
            && sheet.Summary.Detail.StartsWith("Sollecitazioni nulle", StringComparison.Ordinal) && sheet.Errors.Count == 0 && sheet.Current?.UltimateCheck is null,
            "Sollecitazioni nulle: MRd in evidenza, nessuna verifica e nessun avviso: " + sheet.Summary?.Value);
        expect.That(sheet.MaterialValues.Any(v => v.Label == "fcd" && v.Value != "—") && sheet.MaterialValues.Any(v => v.Label == "εyd" && v.Value != "—"), "Materiali con i valori del motore, εyd compreso");
        expect.That(sheet.Methods.SequenceEqual(["Momento positivo", "Momento negativo", "Metodo n"]) && sheet.Method == "Momento positivo" && sheet.Models.SequenceEqual(["Plastica", "Elastica"]),
            "Scelte con i nomi chiari");
        expect.That(!sheet.GeometryForm.Fields.Any(f => f.Key == "transverse_bar_diameter_mm") && sheet.StirrupField.Key == "transverse_bar_diameter_mm", "Staffe fra le armature, non nella geometria");
        sheet.CoverHelpCommand.Execute(null);
        expect.That(messages.Shown.LastOrDefault() is { Title: ConcreteQuickSection.CoverHelpTitle } shown && shown.Message.Contains("netto", StringComparison.Ordinal), "«?» del copriferro: la spiegazione");

        // Edits of the actions, of the layers and of the materials.
        sheet.CombinationRows[0].N = "-1000"; sheet.CombinationRows[0].Mx = "400";
        expect.That(ConcreteQuickSection.Action(sheet.Data, "SLU")!.S("Mx") == "400" && ConcreteQuickSection.Action(sheet.Data, "SLE")!.S("Mx") == "400" && modified == 2 && !sheet.HasResults,
            "Modifica della combinazione nel documento (righe SLU e SLE)");
        await sheet.PendingCalculation;
        expect.That(sheet.HasResults && sheet.ResultValues.Any(v => v.Label == "Mx,Ed" && v.Value.StartsWith("400", StringComparison.Ordinal)) && sheet.IsDuctile == true
            && sheet.CombinationRows[0].Ratio.Length > 0 && sheet.CombinationRows[0].Passed == true, "Ricalcolo automatico dopo la modifica: η nella riga, comportamento duttile");
        sheet.AddCombinationCommand.Execute(null);
        expect.That(sheet.CombinationRows.Count == 2 && sheet.CombinationRows[1].IsSelected && !sheet.CombinationRows[0].IsSelected, "«+ Aggiungi combinazione»: Combo 2 selezionata");
        sheet.CombinationRows[1].N = "-500"; sheet.CombinationRows[1].Mx = "900"; await sheet.PendingCalculation;
        expect.That(sheet.HasResults && sheet.Summary?.Detail.StartsWith("η = ", StringComparison.Ordinal) == true && sheet.ResultValues.Any(v => v.Label == "Mx,Ed" && v.Value.StartsWith("900", StringComparison.Ordinal))
            && sheet.CombinationRows.All(r => r.Ratio.Length > 0), "Risultati della combinazione selezionata, η di tutte le righe");
        sheet.CombinationRows[0].IsSelected = true; await sheet.PendingCalculation;
        expect.That(sheet.SelectedCombinationId == sheet.CombinationRows[0].Id && sheet.ResultValues.Any(v => v.Label == "Mx,Ed" && v.Value.StartsWith("400", StringComparison.Ordinal)),
            "Selezione della prima riga: i suoi risultati");
        sheet.CombinationRows[1].RemoveCommand.Execute(null); await sheet.PendingCalculation;
        expect.That(sheet.CombinationRows.Count == 1 && !sheet.CombinationRows[0].RemoveCommand.CanExecute(null), "«✕»: combinazione tolta, l'ultima non si toglie");
        sheet.AddLayerCommand.Execute(null);
        expect.That(sheet.LayerRows.Count == 3 && sheet.LayerRows[2].Position == "Da sopra" && sheet.LayerRows[2].UsesDistance, "«+»: nuovo strato «Da sopra»");
        sheet.LayerRows[2].Distance = "400"; await sheet.PendingCalculation;
        expect.That(sheet.Section?.Bars.Count == 12 && sheet.HasResults, "Strato a 400 mm dall'alto disegnato e calcolato");
        sheet.LayerRows[2].Count = "abc";
        expect.That(sheet.Section is null && sheet.DrawingMessage.StartsWith("Strato 3", StringComparison.Ordinal), "Strato non valido: il motivo con il suo nome");
        sheet.LayerRows[2].RemoveCommand.Execute(null); await sheet.PendingCalculation;
        expect.That(sheet.LayerRows.Count == 2 && sheet.Section?.Bars.Count == 10 && sheet.HasResults, "«✕»: strato tolto");
        sheet.MaterialsForm.Set("classe_cls", "C25/30");
        expect.That(sheet.Data["input"].S("classe_cls") == "C25/30" && J.Number(sheet.Data["input"]!["fck_mpa"]) == 25 && sheet.MaterialsForm["fck_mpa"].Text == "25",
            "Classe del calcestruzzo applicata dal catalogo e mostrata");
        await sheet.PendingCalculation;
        sheet.MaterialsForm.Set("fck_mpa", "28");
        expect.That(sheet.Data["input"].S("classe_cls") == "Personalizzato" && sheet.MaterialsForm["classe_cls"].SelectedChoice == "Personalizzato", "fck scritto a mano: calcestruzzo «Personalizzato»");
        sheet.MaterialsForm.Set("fyk_mpa", "500");
        expect.That(sheet.Data["input"].S("classe_acciaio") == "Personalizzato" && sheet.MaterialsForm["classe_acciaio"].SelectedChoice == "Personalizzato", "fyk scritto a mano: acciaio «Personalizzato»");
        await sheet.PendingCalculation;
        expect.That(sheet.HasResults && sheet.ConcreteValues.Any(v => v.Label == "fcd") && sheet.SteelValues.Any(v => v.Label == "fyd" && v.Value.StartsWith("434", StringComparison.Ordinal)),
            "Valori del motore con i materiali personalizzati (fyd = 500 / 1,15)");
        sheet.MaterialsForm.Set("classe_cls", "C25/30"); sheet.MaterialsForm.Set("classe_acciaio", "B450C");
        expect.That(J.Number(sheet.Data["input"]!["fck_mpa"]) == 25 && J.Number(sheet.Data["input"]!["fyk_mpa"]) == 450 && sheet.MaterialsForm["fyk_mpa"].Text == "450",
            "Classe scelta di nuovo: valori del catalogo");
        await sheet.PendingCalculation;

        // The choices: negative moment, elastic model, biaxial bending, method n, moment–curvature.
        sheet.Method = "Momento negativo"; await sheet.PendingCalculation;
        expect.That(ConcreteQuickSection.View(sheet.Data).S("metodo") == "SLU−" && sheet.Summary?.Title.Contains("momento negativo", StringComparison.Ordinal) == true, "Momento negativo: codice SLU− nel documento");
        sheet.Model = "Elastica"; await sheet.PendingCalculation;
        expect.That(ConcreteQuickSection.Model(sheet.Data) == "Elastico" && sheet.Summary?.Title.Contains("elastica", StringComparison.Ordinal) == true, "Verifica elastica scelta");
        sheet.Model = "Plastica"; sheet.Method = "Momento positivo"; await sheet.PendingCalculation;
        sheet.Bending = ConcreteQuickSection.Bendings[1];
        expect.That(!sheet.CombinationRows[0].MyReadOnly && !sheet.IsStraight, "Flessione deviata: My modificabile");
        sheet.CombinationRows[0].My = "60"; await sheet.PendingCalculation;
        expect.That(sheet.HasResults && sheet.ResultValues.Any(v => v.Label == "My,Ed") && sheet.ResultValues.Count(v => v.Label.EndsWith(",Rd", StringComparison.Ordinal)) == 4
            && sheet.Summary?.Value.StartsWith("Mx,Rd = ", StringComparison.Ordinal) == true, "Flessione deviata: My,Ed, le quattro resistenze principali, il punto resistente in evidenza");
        sheet.Method = "Metodo n"; await sheet.PendingCalculation;
        expect.That(sheet.IsServiceMethod && sheet.ResultValues.Any(v => v.Label == "σc") && sheet.SectionStress is not null && sheet.IsDuctile is null, "Metodo n: tensioni del motore");
        int edits = modified;
        sheet.Display = "Sezione";
        expect.That(sheet.ShowsSection && modified == edits, "Area di disegno sulla sezione: stato della vista, senza modifiche");
        sheet.Display = ConcreteQuickSection.Charts[1]; await sheet.PendingCalculation;
        expect.That(!sheet.ShowsSection && !sheet.ShowsDomain && sheet.Chart == ConcreteQuickSection.Charts[1] && sheet.CurvaturePoints.Count > 2 && modified == edits + 1,
            "Grafico momento–curvatura scelto dall'area di disegno");
        sheet.Shape = "Circolare"; await sheet.PendingCalculation;
        expect.That(sheet.GeometryForm.Fields.Any(f => f.Key == "diameter_mm") && sheet.BarsForm.Fields.Any(f => f.Key == "longitudinal_bar_count") && sheet.LayerRows.Count == 0
            && sheet.Data["input"]!["barre_manuali"] is null && sheet.HasResults && sheet.Section?.Shape == "Circolare", "Sezione circolare: anello del motore, senza strati");
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
                && reopened.Method == "Metodo n" && reopened.Title == "Prova", "Salvataggio e riapertura esatti");
        }

        // Read-only revision.
        sheet.IsReadOnly = true; int before = modified;
        sheet.Shape = "Rettangolare"; sheet.Method = "Momento positivo"; sheet.Model = "Elastica"; sheet.Title = "Altro";
        expect.That(JsonNode.DeepEquals(sheet.Data, saved) && modified == before && sheet.Shape == "Circolare" && sheet.CombinationRows[0].IsReadOnly && !sheet.AddCombinationCommand.CanExecute(null),
            "Revisione in sola lettura protegge i dati");
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
    /// Writes the examples of the distribution (17/11/2026), built with the code of the module: a beam (positive moment, straight bending), a column
    /// (biaxial bending, a layer at mid height) and a circular pile (method n). Run with <c>--sezione-rapida-esempi &lt;cartella&gt;</c>; the files are
    /// then copied into the distribution.
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
            if (ConcreteQuickSection.ApplyLayers(data) is { } error) throw new InvalidOperationException(name + ": " + error);
            ConcreteQuickSection.Validate(data);
            var document = DocumentService.CreateDocument(ConcreteQuickSection.Module); document["dati"] = data;
            string path = Path.Combine(target, name + ".anthea"); DocumentService.Save(path, document); written.Add(path);
        }
        void Set(JsonObject data, params (string Key, string Value)[] values) { foreach (var (key, value) in values) data["input"]![key] = value; }
        void Layers(JsonObject data, params (string Position, string D, string N, string Phi)[] layers) =>
            ConcreteQuickSection.View(data)[ConcreteQuickSection.LayersKey] = new JsonArray(layers.Select(l => (JsonNode?)new JsonObject { ["posizione"] = l.Position, ["d"] = l.D, ["n"] = l.N, ["phi"] = l.Phi }).ToArray());
        Example("trave-flessione-retta", d =>
        {
            Set(d, ("shape", "Rettangolare"), ("width_mm", "300"), ("height_mm", "500"), ("cover_mm", "30"), ("transverse_bar_diameter_mm", "8"),
                ("classe_cls", "C25/30"), ("classe_acciaio", "B450C"));
            Layers(d, ("Superiore", "", "2", "14"), ("Inferiore", "", "4", "20"));
            Act(d, "0", "150"); ConcreteQuickSection.AddCombination(d); Act(d, "0", "-60", index: 1);
            ConcreteQuickSection.SelectCombination(d, ConcreteQuickSection.Combinations(d)[0].S("id"));
            ConcreteQuickSection.View(d)["titolo"] = "Trave 30×50 · flessione retta";
        });
        Example("pilastro-flessione-deviata", d =>
        {
            Set(d, ("shape", "Rettangolare"), ("width_mm", "400"), ("height_mm", "400"), ("cover_mm", "35"), ("transverse_bar_diameter_mm", "8"),
                ("classe_cls", "C30/37"), ("classe_acciaio", "B450C"));
            Layers(d, ("Superiore", "", "3", "20"), ("Da sopra", "200", "2", "20"), ("Inferiore", "", "3", "20"));
            Act(d, "-1200", "120", "80"); ConcreteQuickSection.AddCombination(d); Act(d, "-600", "150", "40", index: 1);
            ConcreteQuickSection.SelectCombination(d, ConcreteQuickSection.Combinations(d)[0].S("id"));
            var view = ConcreteQuickSection.View(d); view["titolo"] = "Pilastro 40×40 · flessione deviata"; view["flessione"] = ConcreteQuickSection.Bendings[1];
        });
        Example("palo-circolare-metodo-n", d =>
        {
            Set(d, ("shape", "Circolare"), ("diameter_mm", "800"), ("cover_mm", "60"), ("transverse_bar_diameter_mm", "10"),
                ("longitudinal_bar_count", "14"), ("longitudinal_bar_diameter_mm", "22"), ("classe_cls", "C25/30"), ("classe_acciaio", "B450C"));
            Act(d, "-1100", "300");
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
