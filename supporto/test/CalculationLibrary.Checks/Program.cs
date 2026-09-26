using Anthea.Calculations;
using System.Text.Json.Nodes;

try
{
    int count = 0;
    void Check(bool value, string text) { if (!value) throw new Exception(text); count++; }
    void Near(double actual, double expected, string text) => Check(Math.Abs(actual - expected) <= 1e-10 * Math.Max(1, Math.Abs(expected)), text);
    void Reject(Action action, string text) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception(text); }
    var assembly = typeof(CalculationService).Assembly;
    Check(assembly.GetName().Name == "ANTHEA.Calculations", "Assembly non autonomo");
    Check(!assembly.GetReferencedAssemblies().Any(a => a.Name is "ANTHEA.Core" or "ANTHEA" or "Materiali" or "PresentationFramework" or "PresentationCore" or "WindowsBase"), "Dipendenza da archivio/UI");
    foreach (var module in ModuleCatalog.All)
    {
        var data = ModuleCatalog.CreateData(module.Id); string before = data.ToJsonString();
        ModuleCatalog.ValidateData(module.Id, data);
        Check(CalculationValidation.Coefficients(module.Id, data).Count == 0, "Coefficienti predefiniti non validi: " + module.Id);
        Check(before == data.ToJsonString(), "Validazione muta input: " + module.Id);
    }
    var h = Homogenization.Resolve(210000, 30000, false, 2); Near(h.N, 21, "n analitico"); Near(h.Phi, 2, "φ analitico");
    var p = Homogenization.Resolve(210000, 30000, true, 21); Near(p.Phi, 2, "φ da n");
    Near(Homogenization.Resolve(210000, 30000, true, 7).Phi, 0, "n istantaneo");
    Reject(() => Homogenization.Resolve(210000, 30000, true, 6), "n sotto rapporto elastico");
    Reject(() => Homogenization.Resolve(210000, 0, false, 0), "Modulo nullo");
    Reject(() => Homogenization.Resolve(double.NaN, 30000, false, 0), "Modulo NaN");
    Near(PaloOrizzontale.PassivePressureCoefficient(0), 1, "Kp φ=0");
    Near(PaloOrizzontale.PassivePressureCoefficient(30), 3, "Kp φ=30°");
    Reject(() => PaloOrizzontale.PassivePressureCoefficient(90), "Kp singolare");
    // Independent arithmetic values, not a second call to the production method.
    Near(ConcreteBond.Strength(2, 20, .7, 1, 1.5), 2.1, "Aderenza analitica");
    Near(ConcreteBond.Calculate(30, 16, 1, 1, 1.5).Fbd, 2.25 * .7 * .3 * Math.Pow(30, 2d/3) / 1.5, "Aderenza C30");
    Near(ConcreteBond.Calculate(90, 40, .7, 1, 1.5).Fbd, ConcreteBond.Calculate(60, 40, .7, 1, 1.5).Fbd, "Limite C60");
    var anchor = new ConcreteAnchorageCalculator().Calculate(new(20, 300, 2, 1.5, false, 1500, true, 100, 81));
    Check(anchor.LengthPassed && !anchor.LapClearDistancePassed && !anchor.Passed, "Verifica indipendente dell’interferro");
    Near(anchor.Alpha6, 1.5, "α6 limitato"); Near(anchor.MaximumLapClearDistance, 80, "Limite interferro");
    var material = ModuleCatalog.CreateData("mat_calcestruzzo");
    var result = CalculationService.Calculate("mat_calcestruzzo", material);
    Check(result.D("fck_mpa") == 30 && result.D("copriferro_nominale_mm") > 0, "Materiale senza UI");
    var input = SezioneCA.DefaultInput(); input["cover_mm"] = 1;
    var cover = ConcreteCoverAnalysis.Calculate(input, material, 30);
    Check(cover.Passed == false, "Copriferro insufficiente non segnalato");
    input["cover_mm"] = 0;
    Check(ConcreteCoverAnalysis.Calculate(input, material, 30).Passed == false, "Copriferro nullo deve fallire, non risultare indeterminato");
    var dataCA = SezioneCA.DefaultData(); var w = SectionWorkspace.Prepare(dataCA);
    dataCA["input"]!["gamma_c"] = 0;
    Reject(() => CalculationService.Calculate("str_palo", dataCA), "γc nullo accettato");
    dataCA["input"]!["gamma_c"] = "NaN";
    Check(CalculationValidation.Coefficients("str_palo", dataCA).Any(i => i.Path == "input/gamma_c"), "γc NaN non segnalato");
    dataCA["input"]!["gamma_c"] = 1.5; w["coefficienti"]!["GammaSPrestress"] = "non usato";
    Check(CalculationValidation.Coefficients("str_palo", dataCA).Count == 0, "Trefoli inattivi bloccano il calcolo");
    w["normativa"] = "inesistente";
    Check(CalculationValidation.Coefficients("str_palo", dataCA).Any(i => i.Path == "normativa"), "Normativa non validata");
    Reject(() => CalculationService.Calculate("inesistente", new()), "Modulo sconosciuto accettato");
    var bridge = BridgeSection.Defaults(); string old = bridge.ToJsonString();
    var bridgeResult = CalculationService.Calculate(BridgeSection.Module, bridge);
    Check(bridgeResult.S("errore") == "", "Ponte non calcolabile senza UI");
    Check(bridge.ToJsonString() == old, "Calcolo ponte muta input");
    double[][] rectangle = [[0,0],[4,0],[4,3],[0,3]];
    Near(SectionRegions.Area(rectangle), 12, "GPC area rettangolo");
    Near(SectionRegions.Area(rectangle.Reverse().ToArray()), 12, "GPC orientamento invertito");
    Near(SectionRegions.Area([]), 0, "GPC poligono vuoto");
    Near(SectionRegions.Area([[0,0],[4,0]]), 0, "GPC contorno degenere");
    Near(SectionRegions.Area(SectionRegions.Clip(rectangle, 1, 0, 2)), 6, "GPC area porzione efficace");
    var geometry = new SezioneCA(SezioneCA.DefaultInput());
    Near(SectionGeometry.BarCover(geometry, new(0, 0, Math.PI*100, 20)), 290, "GPC distanza interna");
    Near(SectionGeometry.BarCover(geometry, new(310, 0, Math.PI*100, 20)), -20, "GPC distanza esterna");
    Near(SectionGeometry.BarCover(geometry, new(300, 0, Math.PI*100, 20)), -10, "GPC barra sul bordo");
    var holed = SezioneCA.DefaultInput(); holed["foro_presente"] = true; holed["inner_width_mm"] = 100; holed["inner_height_mm"] = 100;
    Near(SectionGeometry.BarCover(new SezioneCA(holed), new(0, 0, Math.PI*100, 20)), -60, "GPC barra dentro foro");
    material["classe"] = "C12/15";
    Check(CalculationService.Calculate("mat_calcestruzzo", material).D("fck_mpa") == 12, "Classe storica C12/15 persa");
    var propsData = SezioneCA.DefaultData(); var propsSettings = SectionWorkspace.Prepare(propsData); var propsInput = propsData["input"]!.AsObject();
    var model = CheckerSection.PrepareModel(propsInput, propsSettings);
    var props = ConcreteSectionProperties.Calculate(model, propsInput, propsSettings, J.Obj(("phi", 2)));
    Near(props.Values.Single(v => v.Name == "Area").Value, 4800, "Area GPC sezione rettangolare [cm²]");
    Near(props.Values.Single(v => v.Name == "Jxx" && v.Group.StartsWith("Solo")).Value, 600d*800*800*800/12/1e4, "Jxx GPC rettangolo [cm⁴]");
    var inverse = ConcreteSectionProperties.Calculate(model, propsInput, propsSettings, J.Obj(("metodo", "Da n"), ("n", props.N)));
    Near(inverse.Phi, 2, "Proprietà GPC aggiornate da n");
    Console.WriteLine($"Completato: {count} controlli della libreria autonoma superati.");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
