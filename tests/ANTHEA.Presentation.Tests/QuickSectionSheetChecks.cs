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
/// ViewModel (opening, edits, choices, read-only revision, reload, save and reopen, invalid data, reports) and the examples of the distribution.
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
            && firstCombination.S(ConcreteQuickSection.StateField) == "SLU" && ConcreteQuickSection.Action(created, "SLE") is null && ConcreteQuickSection.SelectedCombination(created) == firstCombination.S("id"),
            "Documento nel formato della sezione in c.a.: una combinazione SLU, selezionata, senza copie SLE");
        expect.That(ConcreteQuickSection.ActionComponents.All(c => J.Number(JsonValue.Create(ConcreteQuickSection.Action(created, "SLU")!.S(c))) == 0),
            "Foglio nuovo: tutte le sollecitazioni a zero");
        var combos = ConcreteQuickSection.Create();
        string second = ConcreteQuickSection.AddCombination(combos);
        expect.That(ConcreteQuickSection.Combinations(combos).Count == 2 && ConcreteQuickSection.Combinations(combos)[1].S("nome") == "Combo 2"
            && ConcreteQuickSection.SelectedCombination(combos) == second && ConcreteSheetPreparation.ReadActions(combos)["SLU"].Length == 2 && ConcreteSheetPreparation.ReadActions(combos)["SLE"].Length == 0,
            "«+»: Combo 2 SLU aggiunta e selezionata");
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

        // Null actions: the resisting moments all the same (straight: MRd at N = 0; biaxial: the principal resistances Mx,Rd and My,Rd at N = 0).
        var straightNull = ConcreteQuickSection.Summary(ConcreteQuickSection.Calculate(ConcreteQuickSection.Create()))!;
        var biaxialNull = ConcreteQuickSection.Create(); ConcreteQuickSection.View(biaxialNull)["flessione"] = "Deviata";
        var biaxialNullSummary = ConcreteQuickSection.Summary(ConcreteQuickSection.Calculate(biaxialNull))!;
        expect.That(straightNull.Value.StartsWith("MRd = ", StringComparison.Ordinal) && biaxialNullSummary.Value.StartsWith("Mx,Rd = ", StringComparison.Ordinal)
            && biaxialNullSummary.Value.Contains("My,Rd = ", StringComparison.Ordinal) && biaxialNullSummary.Detail.StartsWith("Sollecitazioni nulle", StringComparison.Ordinal)
            && biaxialNullSummary.Ductile is not null, "Sollecitazioni nulle: momenti resistenti anche in flessione deviata (" + biaxialNullSummary.Value + ")");

        // The same engine: the sheet and the headless calculation of the RC section on the same data give the same numbers.
        var same = ConcreteQuickSection.Create();
        Act(same, "-1000", "400");
        string otherId = ConcreteQuickSection.AddCombination(same); Act(same, "-2500", "700", index: 1);
        string ultimateId = ConcreteQuickSection.Combinations(same)[0].S("id"), serviceId = ultimateId;
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
        var serviceView = (JsonObject)same.DeepClone(); ConcreteQuickSection.SetCombinationState(serviceView, ultimateId, "SLE");
        var service = ConcreteQuickSection.Calculate(serviceView);
        var headlessService = ConcreteAnalysis.Calculate((JsonObject)serviceView.DeepClone());
        double? headlessSigma = J.Number(headlessService["tensioni"]?["SLE"]?[serviceId]?["State"]?["sigma_cls"]);
        expect.That(service.ServiceState?.sigma_cls is double sigma && headlessSigma == sigma, $"σc come il calcolo della sezione in c.a.: {service.ServiceState?.sigma_cls} / {headlessSigma}");
        double? headlessSteel = J.Number(headlessService["tensioni"]?["SLE"]?[serviceId]?["State"]?["sigma_acciaio"]);
        expect.That(service.ServiceState?.sigma_acciaio == headlessSteel && ConcreteQuickSection.Summary(service)?.Ductile is null, "σs come il calcolo della sezione in c.a.; nessun comportamento con il metodo n");
        // The stresses called out on the drawing (19/11/2026): for each layer the bar with the largest |σs| (bars 1–4 above, 5–10 below), with its sign.
        var serviceStresses = service.ServiceState!.tensioni_barre;
        var serviceCallouts = ConcreteQuickSection.StressCallouts(serviceView, ConcreteQuickSection.Geometry(serviceView).Geometry, service.ServiceState);
        expect.That(serviceCallouts.Count == 2 && serviceCallouts[0] is >= 0 and < 4 && serviceCallouts[1] is >= 4 and < 10
            && Math.Abs(serviceStresses[serviceCallouts[0]]) == serviceStresses.Take(4).Max(Math.Abs) && Math.Abs(serviceStresses[serviceCallouts[1]]) == serviceStresses.Skip(4).Take(6).Max(Math.Abs)
            && serviceStresses[serviceCallouts[0]] < 0 && serviceStresses[serviceCallouts[1]] > 0,
            $"Metodo n: la barra più sollecitata di ogni strato, compressa sopra e tesa sotto ({serviceStresses[serviceCallouts[0]]} / {serviceStresses[serviceCallouts[1]]} MPa)");
        expect.That(ConcreteQuickSection.StressCalloutText(-77.43) == "σs = -77,43 MPa" && ConcreteQuickSection.StressCalloutText(215.6) == "σs = 215,60 MPa",
            "Etichetta della tensione: σs con il segno, negativa in compressione: " + ConcreteQuickSection.StressCalloutText(-77.43) + " / " + ConcreteQuickSection.StressCalloutText(215.6));
        expect.That(ConcreteQuickSection.StressCallouts(serviceView, null, service.ServiceState).Count == 0 && ConcreteQuickSection.StressCallouts(serviceView, ConcreteQuickSection.Geometry(serviceView).Geometry, null).Count == 0,
            "Senza geometria o senza tensioni nessuna etichetta");
        var pile = ConcreteQuickSection.Create(); var pileInput = pile["input"]!.AsObject();
        pileInput["shape"] = ConcreteQuickSection.Circular; pileInput["diameter_mm"] = "800"; pileInput["longitudinal_bar_count"] = "14"; pileInput["longitudinal_bar_diameter_mm"] = "22";
        ConcreteQuickSection.ApplyLayers(pile); Act(pile, "-1100", "300");
        ConcreteQuickSection.SetCombinationState(pile, ConcreteQuickSection.Combinations(pile)[0].S("id"), "SLE");
        var pileState = ConcreteQuickSection.Calculate(pile).ServiceState!;
        var pileCallouts = ConcreteQuickSection.StressCallouts(pile, ConcreteQuickSection.Geometry(pile).Geometry, pileState);
        expect.That(pileCallouts.Count == 2 && pileState.tensioni_barre[pileCallouts[0]] == pileState.tensioni_barre.Take(14).Min() && pileState.tensioni_barre[pileCallouts[1]] == pileState.tensioni_barre.Take(14).Max()
            && pileState.tensioni_barre[pileCallouts[0]] < 0 && pileState.tensioni_barre[pileCallouts[1]] > 0,
            $"Sezione circolare: la barra più compressa e la più tesa ({pileState.tensioni_barre[pileCallouts[0]]} / {pileState.tensioni_barre[pileCallouts[1]]} MPa)");

        // The link of the second ring (19/11/2026): s = Ø of the link, the second ring at r₂ = r₁ − Ø₁/2 − Ø link − Ø₂/2 (by hand: 22/2 + 12 + 16/2 = 31 mm inside).
        var rings = (JsonObject)pile.DeepClone(); var ringsInput = rings["input"]!.AsObject();
        ringsInput["second_inner_enabled"] = true; ringsInput["second_inner_count"] = "10"; ringsInput["second_inner_diameter"] = "16";
        ConcreteQuickSection.View(rings)[ConcreteQuickSection.SecondRingLinkKey] = "12"; ConcreteQuickSection.ApplyLayers(rings);
        var ringBars = ConcreteQuickSection.Geometry(rings).Geometry!.Bars.Select(b => (R: Math.Sqrt(b.X * b.X + b.Y * b.Y), b.Diametro)).ToArray();
        double r1 = ringBars.Take(14).Average(b => b.R), r2 = ringBars.Skip(14).Average(b => b.R);
        expect.That(ringsInput.S("second_inner_gap") == "12" && ringBars.Length == 24 && Math.Abs(r1 - r2 - 31) < 1e-6 && ConcreteQuickSection.SecondRingLinkDiameter(rings) == 12,
            $"Staffa del secondo anello Ø12: s = 12 mm, secondo anello 31 mm più interno ({r1:0.##} − {r2:0.##} mm)");
        var older = (JsonObject)rings.DeepClone(); ConcreteQuickSection.View(older).Remove(ConcreteQuickSection.SecondRingLinkKey); older["input"]!["second_inner_gap"] = "40";
        ConcreteQuickSection.Validate(older); ConcreteQuickSection.ApplyLayers(older);
        expect.That(ConcreteQuickSection.SecondRingLink(older) == "40" && older["input"].S("second_inner_gap") == "40", "Foglio salvato prima, senza staffa del secondo anello: s letta come staffa, barre ferme");
        var ringsOff = (JsonObject)rings.DeepClone(); ringsOff["input"]!["second_inner_enabled"] = false;
        expect.That(ConcreteQuickSection.SecondRingLinkDiameter(ringsOff) == 0 && ConcreteQuickSection.SecondRingLinkDiameter(ConcreteQuickSection.Create()) == 0,
            "Nessuna staffa del secondo anello da disegnare senza secondo anello o nella sezione rettangolare");
        var ringsReport = ConcreteQuickReportAnalysis.Create(rings).Report;
        expect.That(ringsReport.Geometry.Any(v => v.Label == "Diametro staffa del secondo anello" && v.Value == "12")
            && !ringsReport.Geometry.Any(v => v.Label.Contains("Distanza libera", StringComparison.Ordinal)), "Report: la staffa del secondo anello, senza s (19/11/2026: non serve)");
        // The creep (18/11/2026): φ written where the RC section reads it; the stresses as the RC section with the same φ, n = Es/Ecm (1 + φ).
        expect.That(ConcreteQuickSection.Creep(serviceView) == "0" && service.Creep == 0 && service.EffectiveRatio is double shortTerm && Math.Abs(shortTerm - service.Preview.N) < 1e-9,
            "φ = 0: n uguale a Es/Ecm");
        var creep = (JsonObject)serviceView.DeepClone(); ConcreteQuickSection.WriteCreep(creep, "2");
        var crept = ConcreteQuickSection.Calculate(creep);
        var headlessCreep = ConcreteAnalysis.Calculate((JsonObject)creep.DeepClone());
        double? creepSigma = J.Number(headlessCreep["tensioni"]?["SLE"]?[serviceId]?["State"]?["sigma_cls"]);
        double? creepSteel = J.Number(headlessCreep["tensioni"]?["SLE"]?[serviceId]?["State"]?["sigma_acciaio"]);
        expect.That(ConcreteQuickSection.Creep(creep) == "2" && creep["workspace_ca"]!["sle_comuni"]!["phi"]!.ToString() == "2" && creep["workspace_ca"]!["sle"]!["SLE_QP"]!["phi"]!.ToString() == "2",
            "φ scritto nelle opzioni SLE della sezione in c.a.");
        expect.That(crept.ServiceState?.sigma_cls is double creptSigma && creptSigma == creepSigma && crept.ServiceState.sigma_acciaio == creepSteel && creptSigma != service.ServiceState?.sigma_cls,
            $"φ = 2: tensioni come la sezione in c.a. con lo stesso φ, diverse dal breve termine ({crept.ServiceState?.sigma_cls} / {creepSigma} / {service.ServiceState?.sigma_cls})");
        expect.That(crept.Creep == 2 && crept.EffectiveRatio is double longTerm && Math.Abs(longTerm - 3 * crept.Preview.N) < 1e-9, $"φ = 2: n = Es/Ecm · 3 ({crept.EffectiveRatio} / {crept.Preview.N})");
        var steelValues = ConcreteQuickSection.SteelValues(crept);
        expect.That(steelValues.Any(v => v.Label == "n₀" && !v.InUse) && steelValues.Single(v => v.InUse) is { Label: "n" } used && used.Note.Contains("metodo n", StringComparison.Ordinal)
            && used.Note.Contains("φ = 2", StringComparison.Ordinal) && ConcreteQuickSection.ResultValues(crept).Any(v => v.InUse && v.Label == "n (φ = 2)"), "n usato evidenziato, con φ, nei materiali e nei risultati");
        var creepUltimate = (JsonObject)same.DeepClone(); ConcreteQuickSection.WriteCreep(creepUltimate, "2");
        var creptUltimate = ConcreteQuickSection.Calculate(creepUltimate);
        expect.That(creptUltimate.UltimateCheck?.Utilization == quick.UltimateCheck?.Utilization && creptUltimate.Resistances.Select(r => r.Moment).SequenceEqual(quick.Resistances.Select(r => r.Moment))
            && !ConcreteQuickSection.ResultValues(creptUltimate).Any(v => v.InUse), "φ non cambia la verifica SLU né le resistenze");
        var prepared = (JsonObject)same.DeepClone(); var preparedSettings = SectionWorkspace.Prepare(prepared);
        var resistances = StructuralQueries.ConcreteQuickResistance(prepared["input"]!.AsObject(), preparedSettings, quick.UltimateAction!.Value.N, false);
        expect.That(resistances.Select(r => r.Moment).SequenceEqual(quick.Resistances.Select(r => r.Moment)) && quick.Resistances.Count == 4,
            "Resistenze a N costante come le «Resistenze rapide» della sezione in c.a.");
        // The elastic model of a combination (18/11/2026: a setting of the combination): the domain and the resistances of the RC section with that model.
        var elastic = (JsonObject)same.DeepClone(); ConcreteQuickSection.WriteCombinationSetting(elastic, ultimateId, ConcreteQuickSection.ModelField, "Elastico");
        var elasticResult = ConcreteQuickSection.Calculate(elastic);
        var elasticResistances = StructuralQueries.ConcreteQuickResistance(prepared["input"]!.AsObject(), preparedSettings, -1000, true);
        var elasticDocument = (JsonObject)same.DeepClone(); ConcreteQuickSection.WriteModel(elasticDocument, "Elastico");
        double? elasticHeadless = J.Number(ConcreteAnalysis.Calculate(elasticDocument)["domini"]?["3D:SLU"]?[ultimateId]?["Utilization"]);
        expect.That(elasticResult.UltimateCheck?.Utilization is double elasticRatio && elasticRatio == elasticHeadless && elasticResult.Combinations[1].Ratio == quick.Combinations[1].Ratio,
            $"Combinazione elastica: η come la sezione in c.a. con il modello elastico; le altre combinazioni invariate ({elasticResult.UltimateCheck?.Utilization} / {elasticHeadless})");
        expect.That(ConcreteQuickSection.Combinations(elastic)[0].S(ConcreteQuickSection.ModelField) == "Elastico" && elasticResult.Model == "Elastico"
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
        expect.That(sheet.CreepField.Text == "0" && sheet.SteelValues.Single(v => v.InUse).Value == sheet.SteelValues.Single(v => v.Label == "n₀").Value, "φ = 0 nel foglio nuovo: n uguale a n₀");
        sheet.CreepForm.Set(ConcreteQuickSection.CreepKey, "1.5"); await sheet.PendingCalculation;
        expect.That(ConcreteQuickSection.Creep(sheet.Data) == "1.5" && modified == 1 && sheet.SteelValues.Single(v => v.InUse).Value != sheet.SteelValues.Single(v => v.Label == "n₀").Value,
            "φ dal foglio: modifica del documento, n cambia");
        sheet.CreepForm.Set(ConcreteQuickSection.CreepKey, "0"); await sheet.PendingCalculation; modified = 0;
        expect.That(sheet.Methods.SequenceEqual(["Momento positivo", "Momento negativo", "Metodo n"]) && sheet.Method == "Momento positivo" && sheet.Models.SequenceEqual(["Plastica", "Elastica"]),
            "Scelte con i nomi chiari");
        expect.That(!sheet.GeometryForm.Fields.Any(f => f.Key == "transverse_bar_diameter_mm") && sheet.StirrupField.Key == "transverse_bar_diameter_mm", "Staffe fra le armature, non nella geometria");
        sheet.CoverHelpCommand.Execute(null);
        expect.That(messages.Shown.LastOrDefault() is { Title: ConcreteQuickSection.CoverHelpTitle } shown && shown.Message.Contains("netto", StringComparison.Ordinal), "«?» del copriferro: la spiegazione");

        // Edits of the actions, of the layers and of the materials.
        sheet.CombinationRows[0].N = "-1000"; sheet.CombinationRows[0].Mx = "400";
        expect.That(ConcreteQuickSection.Action(sheet.Data, "SLU")!.S("Mx") == "400" && ConcreteQuickSection.Action(sheet.Data, "SLE") is null && modified == 2 && !sheet.HasResults,
            "Modifica della combinazione nel documento (riga SLU)");
        await sheet.PendingCalculation;
        expect.That(sheet.HasResults && sheet.ResultValues.Any(v => v.Label == "Mx,Ed" && v.Value.StartsWith("400", StringComparison.Ordinal)) && sheet.IsDuctile == true
            && sheet.CombinationRows[0].Ratio.Length > 0 && sheet.CombinationRows[0].Passed == true, "Ricalcolo automatico dopo la modifica: η nella riga, comportamento duttile");
        expect.That(sheet.StressCallouts.Count == 0, "SLU: nessuna tensione delle barre nel disegno");
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
        // The area of the bars of each layer (19/11/2026), from the bars of the engine: 4Ø20 = 4 π 20²/4 = 1256,6 mm²; 6Ø24 = 2714,3 mm².
        expect.That(sheet.LayerRows[0].Area == "12,57" && sheet.LayerRows[1].Area == "27,14", $"As degli strati: {sheet.LayerRows[0].Area} / {sheet.LayerRows[1].Area} cm²");
        sheet.LayerRows[0].Count = "5";
        expect.That(sheet.LayerRows[0].Area == "15,71", "As dello strato aggiornata con il numero delle barre: " + sheet.LayerRows[0].Area);
        sheet.LayerRows[0].Count = "4"; await sheet.PendingCalculation;
        sheet.AddLayerCommand.Execute(null);
        expect.That(sheet.LayerRows.Count == 3 && sheet.LayerRows[2].Position == "Da sopra" && sheet.LayerRows[2].UsesDistance, "«+»: nuovo strato «Da sopra»");
        sheet.LayerRows[2].Distance = "400"; await sheet.PendingCalculation;
        expect.That(sheet.Section?.Bars.Count == 12 && sheet.HasResults, "Strato a 400 mm dall'alto disegnato e calcolato");
        sheet.LayerRows[2].Count = "abc";
        expect.That(sheet.Section is null && sheet.DrawingMessage.StartsWith("Strato 3", StringComparison.Ordinal) && sheet.LayerRows.All(r => r.Area == "—"),
            "Strato non valido: il motivo con il suo nome, nessuna area");
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

        // The choices of the selected combination (18/11/2026): negative moment, elastic model, the limit state of the row that resets them.
        JsonObject First() => ConcreteQuickSection.Combinations(sheet.Data)[0];
        sheet.Method = "Momento negativo"; await sheet.PendingCalculation;
        expect.That(First().S(ConcreteQuickSection.SignField) == "SLU−" && sheet.Method == "Momento negativo" && sheet.Summary?.Title.Contains("momento negativo", StringComparison.Ordinal) == true,
            "Momento negativo: segno della combinazione selezionata");
        sheet.Model = "Elastica"; await sheet.PendingCalculation;
        expect.That(First().S(ConcreteQuickSection.ModelField) == "Elastico" && sheet.Model == "Elastica" && sheet.Summary?.Title.Contains("elastica", StringComparison.Ordinal) == true,
            "Modello elastico della combinazione selezionata");
        sheet.CombinationRows[0].State = "SLV"; await sheet.PendingCalculation;
        expect.That(sheet.Model == "Elastica" && First().S(ConcreteQuickSection.SignField) == "" && sheet.Method == "Momento positivo" && sheet.Summary?.Title.Contains("SLV · ", StringComparison.Ordinal) == true
            && ConcreteSheetPreparation.ReadActions(sheet.Data)["SLV"].Length == 1, "SL scelto nella riga: impostazioni dello stato limite (SLV elastico, segno da Mx)");
        sheet.CombinationRows[0].State = "SLU"; await sheet.PendingCalculation;
        expect.That(sheet.Model == "Plastica" && sheet.Method == "Momento positivo" && First().S(ConcreteQuickSection.ModelField) == "Plastico", "Di nuovo SLU: plastico, segno da Mx");
        sheet.Bending = ConcreteQuickSection.Bendings[1];
        expect.That(!sheet.CombinationRows[0].MyReadOnly && !sheet.IsStraight, "Flessione deviata: My modificabile");
        expect.That(sheet.Methods.SequenceEqual(["Capacità", "Metodo n"]) && sheet.Method == "Capacità" && First().S(ConcreteQuickSection.StateField) == "SLU",
            "Flessione deviata: verifica di capacità senza segno del momento");
        sheet.CombinationRows[0].My = "60"; await sheet.PendingCalculation;
        expect.That(sheet.HasResults && sheet.ResultValues.Any(v => v.Label == "My,Ed") && sheet.ResultValues.Count(v => v.Label.EndsWith(",Rd", StringComparison.Ordinal)) == 4
            && sheet.Summary?.Value.StartsWith("Mx,Rd = ", StringComparison.Ordinal) == true && sheet.Summary.Title.Contains("flessione deviata", StringComparison.Ordinal),
            "Flessione deviata: My,Ed, le quattro resistenze principali, il punto resistente in evidenza");
        sheet.Method = "Metodo n";
        expect.That(First().S(ConcreteQuickSection.StateField) == "SLE" && sheet.CombinationRows[0].State == "SLE" && sheet.Model is null && sheet.IsServiceMethod,
            "Metodo n scelto in alto: la combinazione diventa SLE rara");
        sheet.Method = "Capacità";
        expect.That(First().S(ConcreteQuickSection.StateField) == "SLU" && sheet.Method == "Capacità" && sheet.Model == "Plastica", "Di nuovo capacità: la combinazione torna SLU plastica");
        await sheet.PendingCalculation;
        sheet.Method = "Metodo n"; await sheet.PendingCalculation;
        expect.That(sheet.IsServiceMethod && sheet.ResultValues.Any(v => v.Label == "σc") && sheet.SectionStress is not null && sheet.IsDuctile is null, "Metodo n: tensioni del motore");
        expect.That(sheet.StressCallouts.Count == sheet.LayerRows.Count && sheet.SectionStress is { } drawn
            && sheet.StressCallouts.All(c => c.Text == ConcreteQuickSection.StressCalloutText(drawn.tensioni_barre[c.Bar]) && c.Compression == drawn.tensioni_barre[c.Bar] < 0),
            "Metodo n: nel disegno una tensione per strato, con il valore del motore: " + string.Join(" · ", sheet.StressCallouts.Select(c => c.Text)));
        int edits = modified;
        sheet.Display = "Sezione";
        expect.That(sheet.ShowsSection && modified == edits, "Area di disegno sulla sezione: stato della vista, senza modifiche");
        sheet.Display = ConcreteQuickSection.Charts[1]; await sheet.PendingCalculation;
        expect.That(!sheet.ShowsSection && !sheet.ShowsDomain && sheet.Chart == ConcreteQuickSection.Charts[1] && sheet.CurvaturePoints.Count > 2 && modified == edits + 1,
            "Grafico momento–curvatura scelto dall'area di disegno");
        sheet.Shape = "Circolare"; await sheet.PendingCalculation;
        expect.That(sheet.GeometryForm.Fields.Any(f => f.Key == "diameter_mm") && sheet.BarsForm.Fields.Any(f => f.Key == "longitudinal_bar_count") && sheet.LayerRows.Count == 0
            && sheet.Data["input"]!["barre_manuali"] is null && sheet.HasResults && sheet.Section?.Shape == "Circolare", "Sezione circolare: anello del motore, senza strati");
        // The link of the second ring in the table (19/11/2026): «Ø st.» typed, s calculated and read-only.
        expect.That(sheet.BarRows[0].Link is null && sheet.BarRows[1].Link is { Key: ConcreteQuickSection.SecondRingLinkKey } && sheet.BarRows.All(r => r.Gap is null)
            && !sheet.BarsForm.Fields.Any(f => f.Key == "second_inner_gap") && sheet.SecondRingLinkDiameter == 0,
            "Secondo anello: colonna «Ø st.», niente colonna s (calcolata dal motore), nessuna staffa disegnata se spento");
        sheet.BarRows[1].Enabled!.IsChecked = true; sheet.BarsForm.Set(ConcreteQuickSection.SecondRingLinkKey, "14"); await sheet.PendingCalculation;
        expect.That(ConcreteQuickSection.View(sheet.Data).S(ConcreteQuickSection.SecondRingLinkKey) == "14" && sheet.Data["input"].S("second_inner_gap") == "14"
            && sheet.SecondRingLinkDiameter == 14 && sheet.HasResults && sheet.Section?.Bars.Count > 14,
            "Ø st. = 14: s = 14 mm nel documento per il motore, staffa disegnata, sezione calcolata");
        sheet.BarRows[1].Enabled!.IsChecked = false; await sheet.PendingCalculation;
        // The shapes (18/11/2026): rectangular, circular, DXF shown off; the flanged section no longer offered.
        expect.That(sheet.ShapeChoices.Select(c => c.Name).SequenceEqual(["Rettangolare", "Circolare", "DXF"]) && sheet.ShapeChoices.Select(c => c.Available).SequenceEqual([true, true, false])
            && sheet.ShapeChoices[2].Note.Length > 0 && sheet.ShapeChoices[2].ToString() == "DXF" && !sheet.Shapes.Contains("A T"), "Forme: Rettangolare, Circolare, DXF spento; niente sezione a T");
        int beforeShape = modified;
        sheet.Shape = "DXF"; sheet.Shape = "A T";
        expect.That(sheet.Shape == "Circolare" && modified == beforeShape, "DXF e sezione a T non si scelgono");
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

        // Excel (17/11/2026): the combinations in a work file, opened without a save window, read again after Ctrl+S in Excel.
        ConcreteQuickActionsExcel.Row[] written = [new("SLU 1", -1200, 350, 0), new("SLU 2", -800.5, -120, 40)];
        var workbook = ConcreteQuickActionsExcel.Write(written);
        expect.That(ConcreteQuickActionsExcel.Read(workbook).SequenceEqual(written), "Excel: combinazioni scritte e rilette esatte");
        var excelData = ConcreteQuickSection.Create();
        expect.That(ConcreteQuickActionsExcel.Import(excelData, workbook) == 2 && ConcreteQuickSection.Combinations(excelData).Select(r => r.S("nome")).SequenceEqual(["SLU 1", "SLU 2"])
            && ConcreteQuickSection.Combinations(excelData)[1].S("N") == "-800.5" && ConcreteQuickSection.Combinations(excelData)[1].S("My") == "40"
            && ConcreteSheetPreparation.ReadActions(excelData)["SLU"].Length == 2 && ConcreteSheetPreparation.ReadActions(excelData)["SLE"].Length == 0
            && ConcreteQuickSection.SelectedCombination(excelData) == ConcreteQuickSection.Combinations(excelData)[0].S("id"),
            "Excel: combinazioni sostituite (SLU), la prima selezionata");
        ModuleDirectory.ValidateData(ConcreteQuickSection.Module, excelData);
        expect.Throws<ArgumentException>(() => ConcreteQuickActionsExcel.Read(System.Text.Encoding.UTF8.GetBytes("x")) , "Excel: file non valido rifiutato");
        var links = new RecordingLinks();
        var excelServices = new Anthea.Presentation.Services.PresentationServices(messages, new ScriptedConfirmations(), new ScriptedFileDialogs(), new ScriptedDialogs(),
            new RecordingClipboard(), links, new RecordingWiki());
        using (var excelSheet = new QuickSectionSheetViewModel(new(ConcreteQuickSection.Create(), false, new Host(), excelServices, ModuleServices.Get(ConcreteQuickSection.Module),
            new CalculationLimiter(1))) { ExcelFolder = Path.Combine(folder, "excel") })
        {
            if (Directory.Exists(excelSheet.ExcelFolder)) Directory.Delete(excelSheet.ExcelFolder, true);
            await excelSheet.PendingCalculation;
            excelSheet.CombinationRows[0].N = "-900"; excelSheet.CombinationRows[0].Mx = "250";
            excelSheet.ExportExcelCommand.Execute(null);
            string exported = excelSheet.ExcelPath ?? "";
            expect.That(File.Exists(exported) && Path.GetDirectoryName(exported) == Path.GetFullPath(excelSheet.ExcelFolder) && links.Opened.LastOrDefault()?.LocalPath == exported
                && ConcreteQuickActionsExcel.Read(File.ReadAllBytes(exported)).SequenceEqual([new ConcreteQuickActionsExcel.Row("Combo 1", -900, 250, 0)]),
                "Esporta in Excel: file nella cartella di lavoro, senza finestra Salva, aperto con Excel");
            // The user fills the workbook in Excel and saves it (Ctrl+S).
            File.WriteAllBytes(exported, ConcreteQuickActionsExcel.Write([new("A", -1000, 300, 0), new("B", -500, 500, 0), new("C", 0, 100, 0)]));
            excelSheet.ImportExcelCommand.Execute(null); await excelSheet.PendingCalculation;
            expect.That(excelSheet.CombinationRows.Select(r => r.Name).SequenceEqual(["A", "B", "C"]) && excelSheet.CombinationRows[0].IsSelected && excelSheet.HasResults
                && excelSheet.CombinationRows.All(r => r.Ratio.Length > 0), "Importa da Excel: lo stesso file riletto senza finestra, combinazioni sostituite e calcolate");
            excelSheet.ExportExcelCommand.Execute(null);
            expect.That(excelSheet.ExcelPath == exported && ConcreteQuickActionsExcel.Read(File.ReadAllBytes(exported)).Count == 3, "Esporta di nuovo: lo stesso file, aggiornato");
            int shownBefore = messages.Shown.Count;
            using (new FileStream(exported, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                excelSheet.CombinationRows[2].Mx = "150";
                excelSheet.ImportExcelCommand.Execute(null);
                bool readWhileOpen = excelSheet.CombinationRows.Count == 3 && excelSheet.CombinationRows[2].Mx == "100" && messages.Shown.Count == shownBefore;
                excelSheet.ExportExcelCommand.Execute(null);
                expect.That(readWhileOpen && excelSheet.ExcelPath != exported && File.Exists(excelSheet.ExcelPath), "File tenuto aperto da Excel: riletto; la nuova esportazione va in un altro file");
            }
            await excelSheet.PendingCalculation;
            File.WriteAllText(excelSheet.ExcelPath!, "non è una cartella Excel");
            excelSheet.ImportExcelCommand.Execute(null);
            expect.That(messages.Shown.Count == shownBefore + 1 && messages.Shown[^1].Title == QuickSectionSheetViewModel.ExcelTitle && excelSheet.CombinationRows.Count == 3,
                "File Excel non valido: il motivo, combinazioni invariate");
            excelSheet.IsReadOnly = true;
            expect.That(!excelSheet.ImportExcelCommand.CanExecute(null) && excelSheet.ExportExcelCommand.CanExecute(null), "Sola lettura: si esporta, non si importa");
        }

        // A sheet saved with the flanged section (first version, 17/11/2026) opens with its fields and calculates.
        var tee = ConcreteQuickSection.Create();
        var teeInput = tee["input"]!.AsObject();
        teeInput["shape"] = "A T"; teeInput["flange_width_mm"] = "1200"; teeInput["flange_thickness_mm"] = "250"; teeInput["web_width_mm"] = "400"; teeInput["height_mm"] = "800";
        ConcreteQuickSection.ApplyLayers(tee); Act(tee, "-500", "300");
        using (var teeSheet = Open(tee))
        {
            await teeSheet.PendingCalculation;
            expect.That(teeSheet.Shape == "A T" && teeSheet.GeometryForm.Fields.Any(f => f.Key == "flange_width_mm") && teeSheet.HasResults && teeSheet.Section?.Bars.Count == 10
                && teeSheet.Current?.UltimateCheck is not null, "Foglio salvato con la sezione a T: campi e calcolo conservati");
        }

        // The limit states of the combinations (18/11/2026): SLU plastic, SLV elastic, SLE with the method n, stresses and cracking of the RC section;
        // the model and the sign are settings of each combination. Each row is stored in the set of its state as the RC section stores it; every ratio
        // equals the calculation of the RC section.
        var states = ConcreteQuickSection.Create();
        Act(states, "-1000", "400");
        string[] stateIds = [ConcreteQuickSection.Combinations(states)[0].S("id"), .. Enumerable.Range(0, 5).Select(_ => ConcreteQuickSection.AddCombination(states))];
        (string N, string Mx, string State)[] stateRows = [("-1000", "400", "SLU"), ("-1400", "560", "SLU"), ("-900", "620", "SLV"), ("-1000", "400", "SLE"),
            ("-850", "330", "SLE_FREQ"), ("-800", "300", "SLE_QP")];
        for (int i = 1; i < stateRows.Length; i++)
        {
            Act(states, stateRows[i].N, stateRows[i].Mx, index: i);
            ConcreteQuickSection.SetCombinationState(states, stateIds[i], stateRows[i].State);
        }
        ConcreteQuickSection.WriteServiceOption(states, ConcreteQuickSection.ExposureKey, "XC3"); ConcreteQuickSection.WriteCreep(states, "1.5");
        ModuleDirectory.ValidateData(ConcreteQuickSection.Module, states);
        var stored = ConcreteSheetPreparation.ReadActions(states);
        bool In(string set, string id) => stored[set].Any(r => r.S("id") == id);
        expect.That(ConcreteQuickSection.Combinations(states).Select(r => r.S("id")).SequenceEqual(stateIds)
            && ConcreteQuickSection.Combinations(states).Select(r => r.S(ConcreteQuickSection.StateField)).SequenceEqual(stateRows.Select(r => r.State))
            && ConcreteQuickSection.Combinations(states).Select(r => r.S(ConcreteQuickSection.ModelField)).SequenceEqual(["Plastico", "Plastico", "Elastico", "", "", ""]),
            "Stati limite: ordine della tabella conservato, stato e modello di base di ogni combinazione");
        expect.That(In("SLU", stateIds[0]) && In("SLU", stateIds[1]) && In("SLV", stateIds[2]) && !In("SLU", stateIds[2]) && In("SLE", stateIds[3]) && In("SLE_FREQ", stateIds[4])
            && In("SLE_QP", stateIds[5]) && stored["SLE"].Length == 1 && !stored["SLE"].Any(r => r.S("id").EndsWith("-n", StringComparison.Ordinal)),
            "Stati limite: righe nelle famiglie della sezione in c.a., senza copie");
        var stateHeadless = ConcreteAnalysis.Calculate((JsonObject)states.DeepClone());
        var stateQuick = ConcreteQuickSection.Calculate(states);
        double? Domain(string set, string id) => J.Number(stateHeadless["domini"]?["3D:" + set]?[id]?["Utilization"]);
        JsonNode? Stress(string set, string id) => stateHeadless["tensioni"]?[set]?[id];
        var byId = stateQuick.Combinations.ToDictionary(c => c.Id);
        expect.That(byId[stateIds[0]].Ratio == Domain("SLU", stateIds[0]) && byId[stateIds[1]].Ratio == Domain("SLU", stateIds[1]) && byId[stateIds[2]].Ratio == Domain("SLV", stateIds[2])
            && byId[stateIds[0]].Ratio is not null && byId[stateIds[1]].Ratio is not null && byId[stateIds[2]].Ratio is not null
            && byId.Values.Select(c => c.State).SequenceEqual(stateRows.Select(r => r.State)),
            $"SLU e SLV: η come la sezione in c.a. ({byId[stateIds[1]].Ratio} / {Domain("SLU", stateIds[1])}; {byId[stateIds[2]].Ratio} / {Domain("SLV", stateIds[2])})");
        expect.That(byId[stateIds[3]].Ratio == J.Number(Stress("SLE", stateIds[3])?["Ratio"]) && byId[stateIds[3]].Ratio is not null,
            $"SLE rara: σ/σlim come la sezione in c.a. ({byId[stateIds[3]].Ratio} / {Stress("SLE", stateIds[3])?["Ratio"]})");
        double? Crack(string set, string id) => J.Number(Stress(set, id)?["CrackResult"]?["Ratio"]);
        double? quasiStress = J.Number(Stress("SLE_QP", stateIds[5])?["Ratio"]);
        expect.That(byId[stateIds[4]].Ratio is double frequent && frequent == Crack("SLE_FREQ", stateIds[4])
            && byId[stateIds[5]].Ratio is double quasi && quasi == Math.Max(Crack("SLE_QP", stateIds[5])!.Value, quasiStress!.Value),
            $"SLE frequente: wk/wlim; SLE quasi permanente: il maggiore fra wk/wlim e σ/σlim, come la sezione in c.a. ({byId[stateIds[4]].Ratio} / {Crack("SLE_FREQ", stateIds[4])}; {byId[stateIds[5]].Ratio} / {Crack("SLE_QP", stateIds[5])} / {quasiStress})");
        var quasiView = (JsonObject)states.DeepClone(); ConcreteQuickSection.SelectCombination(quasiView, stateIds[5]);
        var quasiResult = ConcreteQuickSection.Calculate(quasiView);
        var quasiSummary = ConcreteQuickSection.Summary(quasiResult)!;
        expect.That(quasiResult.State == "SLE_QP" && quasiResult.Crack?.Width is double width && quasiSummary.Value.StartsWith("wk = ", StringComparison.Ordinal)
            && quasiSummary.Title.Contains("SLE quasi permanente · ", StringComparison.Ordinal) && quasiSummary.Title.EndsWith("verificata", StringComparison.Ordinal) && width == J.Number(Stress("SLE_QP", stateIds[5])?["CrackResult"]?["Width"])
            && ConcreteQuickSection.ResultValues(quasiResult).Select(v => v.Label).Intersect(["wk", "wlim", "σc limite", "n (φ = 1,5)"]).Count() == 4,
            "SLE quasi permanente selezionata: wk in evidenza, wlim, tensioni con i limiti, n con φ");
        // The two checks of the quasi-permanent combination with the same importance (19/11/2026).
        expect.That(quasiSummary.Heading == "Fessurazione" && quasiSummary.Ratio == Crack("SLE_QP", stateIds[5]) && quasiSummary.Secondary is { Heading: "Tensioni" } stressBlock
            && stressBlock.Value.StartsWith("σc = ", StringComparison.Ordinal) && stressBlock.Value.Contains("σs = ", StringComparison.Ordinal) && stressBlock.Ratio == quasiStress
            && quasiSummary.Title.EndsWith(byId[stateIds[5]].Passed == true ? "· verificata" : "· non verificata", StringComparison.Ordinal),
            "SLE quasi permanente: fessurazione e tensioni in due blocchi uguali, ognuno con il suo rapporto; esito complessivo nel titolo");
        expect.That(byId[stateIds[5]].Governing == (Crack("SLE_QP", stateIds[5]) >= quasiStress ? "w" : "σ") && byId[stateIds[5]].Ratios.Contains("wk/wlim", StringComparison.Ordinal)
            && byId[stateIds[5]].Ratios.Contains("σ/σlim", StringComparison.Ordinal) && byId[stateIds[3]].Governing == "" && byId[stateIds[4]].Governing == "",
            "Tabella: con due verifiche la sigla di quella che comanda e i due rapporti; nessuna sigla con una sola verifica");
        var frequentOnly = (JsonObject)states.DeepClone(); ConcreteQuickSection.SelectCombination(frequentOnly, stateIds[4]);
        expect.That(ConcreteQuickSection.Summary(ConcreteQuickSection.Calculate(frequentOnly))!.Secondary is null, "SLE frequente: un solo blocco (fessurazione)");
        var frequentView = (JsonObject)states.DeepClone(); ConcreteQuickSection.SelectCombination(frequentView, stateIds[4]);
        var frequentValues = ConcreteQuickSection.ResultValues(ConcreteQuickSection.Calculate(frequentView));
        expect.That(frequentValues.Any(v => v.Label == "wk") && !frequentValues.Any(v => v.Label == "σc limite"), "SLE frequente: fessurazione, tensioni senza limiti");
        var slvView = (JsonObject)states.DeepClone(); ConcreteQuickSection.SelectCombination(slvView, stateIds[2]);
        var slvResult = ConcreteQuickSection.Calculate(slvView);
        expect.That(slvResult.Model == "Elastico" && slvResult.UltimateCheck?.Utilization == byId[stateIds[2]].Ratio && ConcreteQuickSection.Summary(slvResult)!.Title.Contains("SLV · momento resistente", StringComparison.Ordinal)
            && ConcreteQuickSection.Summary(slvResult)!.Title.Contains("verifica elastica", StringComparison.Ordinal), "SLV selezionata: verifica elastica, η della tabella");
        // The model of one combination: only that combination changes; choosing its limit state again brings back the model of the state.
        var ownModel = (JsonObject)states.DeepClone(); ConcreteQuickSection.WriteCombinationSetting(ownModel, stateIds[0], ConcreteQuickSection.ModelField, "Elastico");
        var ownStates = ConcreteQuickSection.Calculate(ownModel).Combinations.ToDictionary(c => c.Id);
        expect.That(ownStates[stateIds[1]].Ratio == byId[stateIds[1]].Ratio && ownStates[stateIds[0]].Ratio != byId[stateIds[0]].Ratio,
            "Modello elastico di una combinazione SLU: cambia solo quella");
        ConcreteQuickSection.SetCombinationState(ownModel, stateIds[0], "SLU");
        expect.That(ConcreteQuickSection.Combinations(ownModel)[0].S(ConcreteQuickSection.ModelField) == "Plastico" && ConcreteQuickSection.Calculate(ownModel).Combinations[0].Ratio == byId[stateIds[0]].Ratio,
            "Stato limite scelto di nuovo: impostazioni azzerate (SLU plastico)");
        expect.Throws<ArgumentException>(() => ConcreteQuickSection.WriteCombinationSetting(ownModel, stateIds[3], ConcreteQuickSection.ModelField, "Elastico"), "Modello di una combinazione SLE rifiutato");
        var noExposure = (JsonObject)states.DeepClone(); ConcreteQuickSection.WriteServiceOption(noExposure, ConcreteQuickSection.ExposureKey, "Da scegliere");
        var unexposed = ConcreteQuickSection.Calculate(noExposure).Combinations.ToDictionary(c => c.Id);
        expect.That(unexposed[stateIds[5]].Ratio is null && unexposed[stateIds[5]].Status.Length > 0 && unexposed[stateIds[3]].Ratio == byId[stateIds[3]].Ratio,
            "Esposizione da scegliere: fessurazione senza rapporto, con il motivo; la rara non cambia (" + unexposed[stateIds[5]].Status + ")");
        ConcreteQuickSection.RemoveCombination(states, stateIds[2]);
        expect.That(!ConcreteSheetPreparation.ReadActions(states)["SLV"].Any() && ConcreteQuickSection.Combinations(states).Count == 5, "Combinazione SLV tolta");

        // A sheet of the first version, «Capacità» (SLU rows copied in SLE, the check and the model on the top): read as SLU with that model and sign,
        // or as SLE rara with the method n; the same results; written so at the first edit.
        JsonObject Legacy(string method, string model)
        {
            var old = ConcreteQuickSection.Create();
            Act(old, "-1000", "-400");
            var row = (JsonObject)old["combinazioni"]!["SLU"]![0]!.DeepClone(); row["id"] = row.S("id") + "-n";
            old["combinazioni"]!["SLE"] = new JsonArray(row);
            ConcreteQuickSection.View(old)["metodo"] = method; ConcreteQuickSection.WriteModel(old, model);
            return old;
        }
        var oldUltimate = Legacy("SLU−", "Elastico");
        string oldId = ConcreteQuickSection.Combinations(oldUltimate)[0].S("id");
        var oldRow = ConcreteQuickSection.Combinations(oldUltimate)[0];
        var oldResult = ConcreteQuickSection.Calculate(oldUltimate);
        double? oldHeadless = J.Number(ConcreteAnalysis.Calculate((JsonObject)oldUltimate.DeepClone())["domini"]?["3D:SLU"]?[oldId]?["Utilization"]);
        expect.That(oldRow.S(ConcreteQuickSection.StateField) == "SLU" && oldRow.S(ConcreteQuickSection.ModelField) == "Elastico" && oldRow.S(ConcreteQuickSection.SignField) == "SLU−"
            && ConcreteQuickSection.Combinations(oldUltimate).Count == 1 && oldResult.Method == "SLU−" && oldResult.Model == "Elastico" && oldResult.Combinations[0].Ratio == oldHeadless,
            "Foglio della prima versione: «Capacità» letta come SLU elastica a momento negativo, η come prima");
        ConcreteQuickSection.AddCombination(oldUltimate);
        var normalized = ConcreteQuickSection.Combinations(oldUltimate)[0];
        expect.That(ConcreteSheetPreparation.ReadActions(oldUltimate)["SLE"].Length == 0 && normalized.S(ConcreteQuickSection.ModelField) == "Elastico" && normalized.S(ConcreteQuickSection.SignField) == "SLU−"
            && ConcreteQuickSection.Calculate(oldUltimate).Combinations[0].Ratio == oldResult.Combinations[0].Ratio, "Prima modifica: copia SLE tolta, modello e segno conservati, η invariato");
        var oldService = Legacy("Metodo n", "Plastico");
        string oldServiceId = ConcreteQuickSection.Combinations(oldService)[0].S("id");
        var oldServiceResult = ConcreteQuickSection.Calculate(oldService);
        var oldServiceReport = ConcreteQuickReportAnalysis.Create(oldService);
        expect.That(oldServiceReport.Report.Cases.Count == 1 && oldServiceReport.Report.Cases[0].State == "SLE"
            && oldServiceReport.Results[oldServiceId].ServiceRatio == oldServiceResult.ServiceRatio, "Relazione del foglio legacy: copia -n esclusa e verifica SLE conservata");
        expect.That(ConcreteQuickSection.Combinations(oldService)[0].S(ConcreteQuickSection.StateField) == "SLE" && oldServiceResult.ServiceState is not null && oldServiceResult.Method == "Metodo n",
            "Foglio della prima versione con il metodo n: combinazione SLE rara");
        ConcreteQuickSection.WriteCombination(oldService, oldServiceId, "nome", "Rara");
        expect.That(ConcreteSheetPreparation.ReadActions(oldService)["SLE"].Single().S("id") == oldServiceId && ConcreteSheetPreparation.ReadActions(oldService)["SLU"].Length == 0
            && ConcreteQuickSection.Calculate(oldService).ServiceState?.sigma_cls == oldServiceResult.ServiceState!.sigma_cls, "Prima modifica: la riga passa fra le SLE, tensioni invariate");

        // Excel with the column SL.
        ConcreteQuickActionsExcel.Row[] withStates = [new("A", -1000, 400, 0), new("B", -900, 620, 0, "SLV"), new("C", -800, 300, 0, "SLE_QP")];
        var stateBook = ConcreteQuickActionsExcel.Write(withStates);
        expect.That(ConcreteQuickActionsExcel.Read(stateBook).SequenceEqual(withStates) && ConcreteQuickActionsExcel.State("sle fr") == "SLE_FREQ" && ConcreteQuickActionsExcel.State("") == "SLU"
            && ConcreteQuickActionsExcel.State("Capacità") == "SLU" && ConcreteQuickActionsExcel.State("Quasi permanente") == "SLE_QP", "Excel: colonna SL scritta e riletta (vuota = SLU)");
        expect.Throws<ArgumentException>(() => ConcreteQuickActionsExcel.State("SLD"), "Excel: stato limite sconosciuto rifiutato");
        // The order of the program (Nome, SL, N, Mx, My) with the drop-down list of SL (19/11/2026); the columns read by their heads.
        var sheetXml = System.Xml.Linq.XDocument.Load(new System.IO.Compression.ZipArchive(new MemoryStream(stateBook)).GetEntry("xl/worksheets/sheet1.xml")!.Open());
        System.Xml.Linq.XNamespace sx = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var heads = sheetXml.Descendants(sx + "row").First(r => (string?)r.Attribute("r") == ConcreteQuickActionsExcel.HeaderRow.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Elements(sx + "c").Select(c => string.Concat(c.Descendants(sx + "t").Select(x => x.Value))).ToArray();
        var list = sheetXml.Descendants(sx + "dataValidation").SingleOrDefault();
        expect.That(heads.SequenceEqual(["Nome", "SL", "N [kN]", "Mx [kNm]", "My [kNm]"]) && list?.Attribute("type")?.Value == "list" && list.Attribute("sqref")?.Value == "B5:B10004"
            && list.Element(sx + "formula1")?.Value == "\"SLU,SLV,SLE RA,SLE FR,SLE QP\"", "Excel: colonne nell'ordine del programma, menù a tendina per SL");
        // A workbook of the earlier layout (SL last) and one without SL: the same rows.
        byte[] Rewritten(Func<string[], string[]> layout)
        {
            using var buffer = new MemoryStream(); buffer.Write(stateBook);
            using (var zip = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Update, true))
            {
                var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!; System.Xml.Linq.XDocument xml;
                using (var s = entry.Open()) xml = System.Xml.Linq.XDocument.Load(s);
                foreach (var row in xml.Descendants(sx + "row").Where(r => int.Parse((string)r.Attribute("r")!, System.Globalization.CultureInfo.InvariantCulture) >= ConcreteQuickActionsExcel.HeaderRow))
                {
                    var cells = row.Elements(sx + "c").ToArray();
                    string[] letters = ["A", "B", "C", "D", "E"];
                    var order = layout(letters);
                    foreach (var cell in cells) cell.Remove();
                    foreach (var (cell, i) in order.Select((l, i) => (cells.FirstOrDefault(c => ((string)c.Attribute("r")!).StartsWith(l, StringComparison.Ordinal)), i)))
                        if (cell is not null) { cell.SetAttributeValue("r", letters[i] + row.Attribute("r")!.Value); row.Add(cell); }
                }
                entry.Delete(); using var output = zip.CreateEntry("xl/worksheets/sheet1.xml").Open(); xml.Save(output);
            }
            return buffer.ToArray();
        }
        expect.That(ConcreteQuickActionsExcel.Read(Rewritten(l => [l[0], l[2], l[3], l[4], l[1]])).SequenceEqual(withStates)
            && ConcreteQuickActionsExcel.Read(Rewritten(l => [l[0], l[2], l[3], l[4]])).Select(r => r.State).All(s => s == "SLU"),
            "Excel: si leggono anche il vecchio ordine (SL in fondo) e i file senza SL (tutte SLU)");
        var imported = ConcreteQuickSection.Create(); ConcreteQuickActionsExcel.Import(imported, stateBook);
        expect.That(ConcreteQuickSection.Combinations(imported).Select(r => r.S(ConcreteQuickSection.StateField)).SequenceEqual(["SLU", "SLV", "SLE_QP"])
            && ConcreteSheetPreparation.ReadActions(imported)["SLV"].Length == 1, "Excel importato: combinazioni nelle loro famiglie");

        // The sheet: the settings of the selected combination on the top, the state of a row, the exposure, the head of the ratios.
        using (var stateSheet = Open(ConcreteQuickSection.Create()))
        {
            await stateSheet.PendingCalculation;
            expect.That(stateSheet.RatioHeader == "η" && stateSheet.CombinationRows[0].State == "SLU" && stateSheet.Model == "Plastica" && stateSheet.Method == "Momento positivo"
                && stateSheet.ExposureField.SelectedChoice == "Da scegliere", "Foglio nuovo: combinazione SLU plastica, esposizione da scegliere");
            stateSheet.AddCombinationCommand.Execute(null);
            stateSheet.CombinationRows[1].Mx = "-300"; stateSheet.CombinationRows[1].State = "SLV";
            await stateSheet.PendingCalculation;
            expect.That(stateSheet.Model == "Elastica" && stateSheet.Method == "Momento negativo", "Combinazione SLV selezionata: in alto elastica, momento negativo da Mx");
            stateSheet.CombinationRows[0].IsSelected = true;
            expect.That(stateSheet.Model == "Plastica" && stateSheet.Method == "Momento positivo", "Selezione della combinazione SLU: in alto le sue impostazioni");
            stateSheet.CombinationRows[0].N = "-800"; stateSheet.CombinationRows[0].Mx = "300";
            stateSheet.CombinationRows[0].State = "SLE_QP";
            stateSheet.CreepForm.Set(ConcreteQuickSection.ExposureKey, "XC3");
            await stateSheet.PendingCalculation;
            expect.That(stateSheet.RatioHeader == "rapp." && ConcreteQuickSection.Exposure(stateSheet.Data) == "XC3" && stateSheet.CombinationRows[0].State == "SLE_QP" && stateSheet.Model is null
                && stateSheet.Method == "Metodo n" && stateSheet.Current?.State == "SLE_QP" && stateSheet.Summary?.Value.StartsWith("wk = ", StringComparison.Ordinal) == true
                && stateSheet.CombinationRows[0].Ratio.Length > 0, "Foglio: SLE quasi permanente scelta nella riga, metodo n in alto, fessurazione in evidenza: " + stateSheet.Summary?.Value);
            stateSheet.Model = "Elastica"; await stateSheet.PendingCalculation;
            expect.That(stateSheet.CombinationRows[0].State == "SLU" && stateSheet.Model == "Elastica" && stateSheet.Current?.Model == "Elastico",
                "Modello scelto in alto su una combinazione SLE: diventa SLU con quel modello");
            string reportInput = stateSheet.Data.ToJsonString();
            var reportBytes = stateSheet.Report.BuildReport("Sezione", new HashSet<string> { "oggetto", "materiali", "azioni", "riepilogo", "risultati_SLU" });
            expect.That(reportBytes.Length > 1000 && stateSheet.Data.ToJsonString() == reportInput, "Relazione dal ViewModel: Word generato senza cambiare il documento");
        }

        expect.That(module.Reports.ShortReport, "Relazione breve disponibile per la verifica rapida");
        QuickSectionReportChecks.Run(expect, folder);

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
            var view = ConcreteQuickSection.View(d); view["titolo"] = "Palo Ø800 · metodo n"; view["grafico"] = ConcreteQuickSection.Charts[1];
            ConcreteQuickSection.SetCombinationState(d, ConcreteQuickSection.Combinations(d)[0].S("id"), "SLE");
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
