using System.Text.Json.Nodes;
using X.Core;

internal static class ConcreteEnhancementChecks
{
    internal static int Run()
    {
        int count = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception("CA estensioni: " + message); count++; }
        void Reject(Action action, string message) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception("CA accetta input invalido: " + message); }
        var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        Check(input.S("shape") == "Rettangolare" && input.D("transverse_spacing_mm") == 200 && input.S("classe_acciaio") == "B450C", "Nuovi valori predefiniti");
        input["shape"] = "Circolare"; // This fixture explicitly tests the circular geometry, independently of UI defaults.
        var spacing = new TensionBarSpacing();
        var circular = new SezioneCA(input);
        Check(Math.Abs(spacing.Maximum(circular, [0, 1, 2])!.Value - 2 * Math.PI * circular.BarRadius / circular.Bars.Count) < 1e-8, "Interasse circolare lungo arco fra adiacenti");
        Check(spacing.Maximum(circular, [0, 2]) is null && spacing.Maximum(circular, [0]) is null, "Non collega barre non adiacenti o singola barra");
        var layeredInput = SezioneCA.DefaultInput();
        var firstLayer = new SezioneCA(layeredInput);
        foreach (string layer in new[] { "top", "bottom" })
        {
            layeredInput["second_" + layer + "_enabled"] = true; layeredInput["second_" + layer + "_count"] = "3";
            layeredInput["second_" + layer + "_diameter"] = "16"; layeredInput["second_" + layer + "_gap"] = "35";
        }
        var twoLayers = new SezioneCA(layeredInput);
        Check(twoLayers.Bars.Count == firstLayer.Bars.Count + 6, "Secondi strati rettangolari aggiunti senza sostituire i primi");
        _ = CheckerSection.PrepareModel(layeredInput, settings);
        Check(SectionShearGeometry.Derive(twoLayers, false).Depth < SectionShearGeometry.Derive(firstLayer, false).Depth && SectionShearGeometry.Derive(twoLayers, false).SteelArea > SectionShearGeometry.Derive(firstLayer, false).SteelArea, "Taglio: d e Asl includono i secondi strati del wizard");
        Check(Math.Abs(firstLayer.Bars[0].Y - twoLayers.Bars[10].Y - (20 + 16) / 2d - 35) < 1e-8, "Distanza libera secondo strato superiore");
        layeredInput["second_top_gap"] = "900"; Reject(() => new SezioneCA(layeredInput), "Secondo strato fuori altezza utile");
        layeredInput = (JsonObject)input.DeepClone(); layeredInput["second_inner_enabled"] = true; layeredInput["second_inner_count"] = "12";
        layeredInput["second_inner_diameter"] = "20"; layeredInput["second_inner_gap"] = "40";
        var twoRings = new SezioneCA(layeredInput); _ = CheckerSection.PrepareModel(layeredInput, settings);
        Check(twoRings.Bars.Count == circular.Bars.Count + 12 && double.Hypot(twoRings.Bars[^1].X, twoRings.Bars[^1].Y) < twoRings.BarRadius, "Secondo anello interno collegato al modello Checker");
        Check(spacing.Maximum(twoRings, Enumerable.Range(0, twoRings.Bars.Count).ToArray()) is > 0, "Interasse automatico su due anelli concentrici");
        layeredInput["second_inner_gap"] = "900"; Reject(() => new SezioneCA(layeredInput), "Anello interno senza spazio");
        var tInput = (JsonObject)input.DeepClone(); tInput["shape"] = "A T";
        var originalT = new SezioneCA(tInput);
        tInput["flange_bottom_count"] = "4"; tInput["flange_bottom_diameter_mm"] = "20"; tInput["flange_bottom_offset_mm"] = "90";
        var reinforcedT = new SezioneCA(tInput);
        Check(reinforcedT.Bars.TakeLast(2).All(b => b.Y > tInput.D("height_mm") / 2 - tInput.D("flange_thickness_mm") - reinforcedT.CentroidY), "Barre laterali T raggiungono la staffa interna nell'ala");
        Check(reinforcedT.Bars.Count == originalT.Bars.Count + 4, "Fila intradosso ala aggiunge quattro barre");
        var added = reinforcedT.Bars.Skip((int)tInput.D("top_bar_count") + (int)tInput.D("bottom_bar_count")).Take(4).ToArray();
        Check(added.Max(b => b.X) - added.Min(b => b.X) > tInput.D("web_width_mm"), "Fila intradosso distribuita sull'intera ala");
        Check(Math.Abs(spacing.Maximum(reinforcedT, Enumerable.Range((int)tInput.D("top_bar_count") + (int)tInput.D("bottom_bar_count"), 4).ToArray())!.Value - (added[3].X - added[0].X) / 3) < 1e-8, "Interasse fila intradosso");
        tInput["flange_bottom_offset_mm"] = "10";
        Reject(() => new SezioneCA(tInput), "Offset intradosso viola copriferro");
        var reportRows = JsonNode.Parse("""
            {"A":{"State":{"Response":{"CMin":-12,"CMax":1,"EcMin":-0.2,"EcMax":0.01,"SMin":-30,"SMax":100,"EsMin":-0.1,"EsMax":0.5}},"Ratio":0.7,"CrackResult":{"Ratio":0.9}},
             "B":{"State":{"Response":{"CMin":-20,"CMax":0,"EcMin":-0.4,"EcMax":0.005,"SMin":-10,"SMax":150,"EsMin":-0.05,"EsMax":0.7}},"Ratio":1.2,"CrackResult":{"Ratio":0.3}},
             "invalida":{"State":null,"Ratio":null,"Status":"Non calcolata"}}
            """)!.AsObject();
        var envelope = ReportConcrete.Envelope(reportRows, false);
        Check(envelope.Count == 8 && envelope.Single(v => v.Label == "CLS · σmin [MPa]") is { Id: "B", Value: -20 }, "Inviluppo conserva segno compressione e combinazione");
        Check(envelope.Single(v => v.Label == "CLS · σmax [MPa]").Id == "A" && envelope.Single(v => v.Label == "Acciaio · εmax [‰]").Id == "B", "Estremi non simultanei mantengono origine distinta");
        Check(ReportConcrete.Governing(reportRows, false)?.Key == "B" && ReportConcrete.Governing(reportRows, true)?.Key == "A", "Governanti tensioni e fessurazione indipendenti");
        Check(ReportConcrete.Governing(new JsonObject { ["err"] = reportRows["invalida"]!.DeepClone() }, false) is null && ReportConcrete.Envelope(new(), false).Count == 0, "Nessun governante inventato se risultati mancanti");
        var options = settings["dominio3d"]!.AsObject(); options["angoli"] = "8"; options["suddivisioni_n"] = "8";
        var rectangularData = SezioneCA.DefaultData(); rectangularData["input"]!["shape"] = "Rettangolare";
        var rectangularSettings = SectionWorkspace.Prepare(rectangularData);
        var batchOptions = rectangularSettings["dominio3d"]!.AsObject();
        var batchActions = Enumerable.Range(0, 70).Select(i => new ActionPoint(-250 - i * 15, 20 + i, 10 - i)).Concat([new ActionPoint(0, 0, 0), new(-500, 0, 0), new(-250, 20, 10)]).ToArray();
        foreach (string state in new[] { "SLU", "SLV" }) foreach (string strategy in new[] { "Iterativo", "Intersezione" })
        {
            batchOptions["strategia"] = strategy;
            var batchDomain = new CheckerSection(rectangularData["input"]!.AsObject(), rectangularSettings, batchOptions, state).Domain3D();
            foreach (string criterion in CheckerSection.Criteria)
            {
                batchOptions["criterio"] = criterion; batchDomain.ConfigureVerification(batchOptions);
                var actual = batchDomain.CheckMany(batchActions);
                var expected = batchActions.Select(batchDomain.Check).ToArray();
                Check(actual.SequenceEqual(expected), $"Batch rettangolare identico al seriale (ordine, resistenze, tassi, response): {state}, {strategy}, {criterion}");
                Check(ReferenceEquals(actual[0], actual[^1]), "Azioni identiche riusano lo stesso risultato senza perdere righe");
            }
            Check(batchDomain.CheckMany([]).Length == 0, "Batch vuoto");
            Check(batchDomain.CheckMany([new(double.NaN, 1, 1), new(0, 0, 0)])[0].Utilization is null, "Azione non finita non blocca il batch");
            using var stopped = new CancellationTokenSource(); stopped.Cancel();
            try { batchDomain.CheckMany(batchActions, stopped.Token); throw new Exception("Batch ignora annullamento"); }
            catch (OperationCanceledException) { count++; }
        }
        foreach (string norm in ConcreteStandards.Names)
        {
            settings["normativa"] = norm; settings["coefficienti"] = ConcreteStandards.Defaults(norm);
            input["alpha_cc"] = settings["coefficienti"]!["AlphaCC"]!.DeepClone(); input["gamma_c"] = settings["coefficienti"]!["GammaC"]!.DeepClone(); input["gamma_s"] = settings["coefficienti"]!["GammaS"]!.DeepClone();
            var engine = new CheckerSection(input, settings, options); var lazyDomain = engine.Domain3D(); var result = lazyDomain.Check(new(-500, 100, 50));
            Check(!lazyDomain.IsMeshCreated, "Verifiche dominio senza mesh grafica " + norm);
            var lazyMesh = lazyDomain.Mesh;
            Check(lazyDomain.IsMeshCreated && ReferenceEquals(lazyMesh, lazyDomain.Mesh), "Mesh grafica creata una sola volta " + norm);
            Check(result.Resistance.HasValue && result.Utilization is > 0, "Motore disponibile " + norm);
            settings["coefficienti"]!["ServiceabilityStressConcreteCoefficientForCharacteristicCombination"] = "0.51";
            var stressEngine = new CheckerSection(input, settings, settings["sle"]!["SLE"]!.AsObject());
            var stress = stressEngine.Stress(new(-500, 5, 5), "SLE");
            Check(!stress.IsRasterCreated, "Verifica SLE senza raster " + norm);
            var secondStress = stressEngine.Stress(new(-700, 10, 10), "SLE");
            Check(!secondStress.IsRasterCreated && !stress.IsRasterCreated, "Combinazioni non visualizzate senza raster " + norm);
            Check(Math.Abs(stress.ConcreteStressLimit!.Value - .51 * input.D("fck_mpa")) < 1e-10, "Limite personalizzato passato alla DLL " + norm);
            Check(stress.ConcreteVertices.Length == engine.Geometry.Outline.Count && stress.Raster?.Stresses.All(double.IsFinite) == true, "Contouring e vertici nativi " + norm);
            var raster = stress.Raster!;
            var fresh = new CheckerSection(input, settings, settings["sle"]!["SLE"]!.AsObject()).Stress(new(-500, 5, 5), "SLE").Raster!;
            Check(stress.IsRasterCreated && ReferenceEquals(raster, stress.Raster) && !secondStress.IsRasterCreated, "Raster memorizzato per la sola combinazione richiesta " + norm);
            Check(raster.Stresses.SequenceEqual(fresh.Stresses) && raster.Strains.SequenceEqual(fresh.Strains), "Raster differito conserva lo stato dopo altre analisi " + norm);
        }
        settings["trefoli"]!.AsArray().Add(J.Obj(("id", "T1"), ("x", "9999"), ("y", "0"), ("area", "150"), ("sigma0", "1000"), ("Ep", "195000"), ("fpyk", "1670"), ("fpk", "1860"), ("eps_u", "35")));
        Reject(() => new CheckerSection(input, settings, options), "Trefolo esterno");
        var t = settings["trefoli"]![0]!; var b = new SezioneCA(input).Bars[0]; t["x"] = b.X; t["y"] = b.Y;
        Reject(() => new CheckerSection(input, settings, options), "Trefolo sovrapposto a barra");
        settings["trefoli"]!.AsArray().Clear(); input["shape"] = "Generica (da definire)";
        Reject(() => new CheckerSection(input, settings, options), "Generica non interpretata come rettangolo"); input["shape"] = "Circolare";
        settings["coefficienti"]!["GammaCAccidental"] = "0"; Reject(() => new CheckerSection(input, settings, options), "Coefficiente nullo"); settings["coefficienti"]!["GammaCAccidental"] = "1.5";
        input["steel_fu_mpa"] = "550"; input["steel_eps_u"] = "75"; input["steel_diagramma"] = "Incrudente";
        var custom = ConcreteMaterials.Rebar(input); Check(custom.Fu == 550 && custom.StrainUTension == .075, "Parametri acciaio custom");
        input["steel_fu_mpa"] = "100"; Reject(() => ConcreteMaterials.Rebar(input), "fu minore di fy");
        input["cls_diagramma"] = "Bilineare"; Check(ConcreteMaterials.Concrete(input).CompressionStressStrainDiagram.ToString() == "Bilinear", "Diagramma CLS custom");
        Check(EngineeringFormat.Number(.00000234).Contains("E") && EngineeringFormat.Number(1.234).EndsWith("23"), "Formato due decimali e numeri piccoli");
        var rows = new[] { new SectionActionsExcel.Row("SLU", "=testo non formula", -123.456789, 1.2345, 0, null, null), new SectionActionsExcel.Row("Taglio", "Taglio", -10, null, null, 20, -30) };
        var roundtrip = SectionActionsExcel.Read(SectionActionsExcel.Write(rows));
        Check(roundtrip.Rows.SequenceEqual(rows) && roundtrip.FormulaCells == 0, "Excel round trip completo con precisione e testo sicuro");
        Check(SectionActionsExcel.Read(SectionActionsExcel.Write([])).Rows.Count == 0, "Excel vuoto reimportabile");
        var scenario = SezioneCA.DefaultData(); var shared = SectionWorkspace.Prepare(scenario); var sectionInput = scenario["input"]!.AsObject();
        var domainOptions = shared["dominio3d"]!.AsObject(); domainOptions["angoli"] = "8"; domainOptions["suddivisioni_n"] = "8";
        var domain = new CheckerSection(sectionInput, shared, domainOptions).Domain3D(); var mesh = domain.Mesh; var native = domain.Native.Domain;
        foreach (string criterion in CheckerSection.Criteria)
        {
            domainOptions["criterio"] = criterion; domain.ConfigureVerification(domainOptions);
            var actual = domain.Check(new(-500, 100, 50)); var expected = new CheckerSection(sectionInput, shared, domainOptions).Domain3D().Check(new(-500, 100, 50));
            Check(ReferenceEquals(mesh, domain.Mesh) && ReferenceEquals(native, domain.Native.Domain) && actual.Utilization is double a && expected.Utilization is double e && Math.Abs(a - e) < 1e-9, "Cambio criterio senza rigenerare: " + criterion);
        }
        domainOptions["strategia"] = "Intersezione"; domain.ConfigureVerification(domainOptions);
        Check(ReferenceEquals(mesh, domain.Mesh) && ReferenceEquals(native, domain.Native.Domain), "Cambio strategia conserva dominio e mesh");
        sectionInput["shape"] = "Rettangolare"; sectionInput["top_bar_count"] = "2"; sectionInput["bottom_bar_count"] = "2"; sectionInput["side_bar_count_per_side"] = "0"; sectionInput["bottom_bar_diameter_mm"] = "20";
        var rectangular = new SezioneCA(sectionInput); var x = SectionShearGeometry.Derive(rectangular, true); var y = SectionShearGeometry.Derive(rectangular, false);
        Check(x.Bw == 800 && Math.Abs(x.Depth - 510) < 1e-9 && Math.Abs(x.SteelArea - 200 * Math.PI) < 1e-9, "Wizard rettangolare Vx");
        Check(y.Bw == 600 && Math.Abs(y.Depth - 710) < 1e-9 && Math.Abs(y.SteelArea - 200 * Math.PI) < 1e-9, "Wizard rettangolare Vy");
        sectionInput["shape"] = "A T"; var tee = new SezioneCA(sectionInput);
        Check(SectionShearGeometry.Derive(tee, true).Bw == 250 && SectionShearGeometry.Derive(tee, false).Bw == 400, "Wizard T larghezze minime ortogonali");
        sectionInput["shape"] = "Circolare"; Reject(() => SectionShearGeometry.Derive(new SezioneCA(sectionInput), true), "Nessun taglio circolare implicito");
        shared["sle_comuni"]!["esposizione"] = "XC3"; shared["sle"]!["SLE_FREQ"]!["contour"] = "Solo geometria";
        SectionWorkspace.Prepare(scenario);
        Check(SectionWorkspace.Sets.Skip(2).All(k => shared["sle"]![k].S("esposizione") == "XC3") && shared["sle"]!["SLE_FREQ"].S("contour") == "Solo geometria" && shared["sle_precedenti_unificazione"] is JsonObject, "SLE comune persistita, vista indipendente e precedente conservato");
        shared["coefficienti"] = ConcreteStandards.Defaults("NTC 2018"); shared["coefficienti"]!["GammaSPrestress"] = "non valido"; shared["sle"]!["SLE"]!["phi_trefoli"] = "non valido";
        Check(new CheckerSection(sectionInput, shared, shared["sle"]!["SLE"]!.AsObject()).Checker.SectionCheckerOptions.PsiCoefficientTendon == 0, "Opzioni trefoli nascoste non influenzano una sezione senza trefoli");
        Console.WriteLine($"Estensioni CA: {count} controlli superati."); return count;
    }
}
