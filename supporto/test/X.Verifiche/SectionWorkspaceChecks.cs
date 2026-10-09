using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Cracking;
using X.Core;
using ShearCalculationDetail = GPC.Checkers.Concrete.Shear.ShearCalculationDetail;

internal static class SectionWorkspaceChecks
{
    internal static int Run()
    {
        int passed = 0;
        void Assert(bool condition, string name) { if (!condition) throw new Exception("Checker: " + name); passed++; }
        var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data);
        Assert(data["combinazioni"]!["SLU"]![0]!["azioni"]![0]!.ToString() == "-2500", "Migrazione N negativo");
        string saved = data.ToJsonString(); SectionWorkspace.Prepare(data); Assert(saved == data.ToJsonString(), "Migrazione idempotente");
        var input = data["input"]!.AsObject(); input["shape"] = "Rettangolare"; input["width_mm"] = "300"; input["height_mm"] = "500";
        input["top_bar_count"] = "2"; input["bottom_bar_count"] = "2"; input["side_bar_count_per_side"] = "0";
        input["top_bar_diameter_mm"] = "18"; input["bottom_bar_diameter_mm"] = "18"; input["cover_mm"] = "31";
        input["transverse_bar_diameter_mm"] = "10"; input["fck_mpa"] = "25";
        var options = settings["dominio3d"]!.AsObject(); options["angoli"] = "12"; options["suddivisioni_n"] = "10"; options["criterio"] = "Eccentricità costante";
        foreach (var mode in new[] { "SLU", "SLV" }) foreach (var strategy in new[] { "Iterativo", "Intersezione" })
        {
            options["strategia"] = strategy;
            var engine = new CheckerSection(input, settings, options, mode);
            var domain = engine.Domain3D();
            Assert(domain.Mesh.Vertices.All(p => double.IsFinite(p.N + p.Mx + p.My)) && domain.Mesh.Triangles.Count > 0, mode + strategy + " mesh");
            var action = new ActionPoint(-500, 50, -30); var actual = domain.Check(action);
            var native = domain.Native.CalculateForce(engine.Force(action));
            Assert(native is not null && actual.Resistance is not null && actual.Utilization is > 0, mode + strategy + " resistenza");
            Assert(Math.Abs(actual.Resistance!.Value.N - native!.NRd / 1000) < 1e-10, "Punto restituito dalla DLL");
            Assert(Math.Abs(actual.Utilization!.Value - native.CalculateWorkingRatio(domain.Native.FailureAnalysisType, engine.Force(action), 1e6, 1000)) < 1e-10, "Tasso restituito dalla DLL");
        }
        var sle = settings["sle"]!["SLE"]!.AsObject();
        foreach (var criterion in CheckerSection.Criteria)
        {
            options["criterio"] = criterion;
            var domain = new CheckerSection(input, settings, options).Domain3D();
            var action = new ActionPoint(-500, 50, -30); var result = domain.Check(action);
            var native = domain.Native.CalculateForce(domain.Section.Force(action));
            Assert(result.Utilization is > 0 && native is not null, "Percorso nativo " + criterion);
            Assert(Math.Abs(result.Utilization!.Value-native!.CalculateWorkingRatio(domain.Native.FailureAnalysisType,domain.Section.Force(action),1e6,1000))<1e-10,"Tasso nativo " + criterion);
        }
        options["criterio"] = "Eccentricità costante";
        // Same VCA_N_1 benchmark as Checker/ValidationTestLinearStressAnalysis: n=15, 300x500, 4 Ø18 at 50 mm.
        sle["phi"] = (15 * new GPC.Model.Materials.ConcreteMaterialEN1992("C25",25,GPC.Model.Materials.ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle).E / 200000 - 1).ToString("G17",System.Globalization.CultureInfo.InvariantCulture);
        sle["modello"] = "Lineare";
        var linear = new CheckerSection(input, settings, sle, engine: SleEngine.Selected).Stress(new(-500, 50, -30), "SLE");
        Assert(linear.sigma_cls < 0, "Compressione negativa nelle tensioni");
        Assert(linear.ConcreteCompressionStrength > 0 && linear.ConcreteTensionStrength > 0 && linear.BarStrengths.All(v => v > 0), "Scale contouring positive da materiali Checker");
        Assert(Math.Abs(linear.ConcreteStressLimit!.Value - 15) < 1e-12, "Limite SLE espresso in valore assoluto");
        Assert(linear.FiberStrains.Length == linear.FiberStresses.Length && linear.FiberStrains.All(double.IsFinite), "Deformazioni contouring native finite");
        Assert(linear.Response is { CMin: < 0, UsefulDepth: > 0, PMin: null }, "Riepilogo nativo completo senza trefoli inventati");
        var compression = new CheckerSection(input,settings,sle,engine:SleEngine.Selected).Stress(new(-500,0,0),"SLE_QP");
        input["gettato_sottile"]="Sì";
        var thinCompression = new CheckerSection(input,settings,sle,engine:SleEngine.Selected).Stress(new(-500,0,0),"SLE_QP");
        Assert(Math.Abs(thinCompression.Ratio!.Value/compression.Ratio!.Value-1.25)<1e-10,"Riduzione limite tensionale elemento piano sottile");
        input["gettato_sottile"]="No";
        Assert(Math.Abs(linear.sigma_cls + 11.04) / 11.04 < .05, "VCA_N_1 CLS");
        var expected = new[] { -131.6, -44.86, -33.25, 53.53 }; var actualBars = linear.tensioni_barre.Order().ToArray();
        for (int i=0;i<4;i++) Assert(Math.Abs(actualBars[i]-expected[i])/Math.Abs(expected[i]) < .05, "VCA_N_1 barra " + i);
        sle["modello"] = "Non lineare"; sle["phi"]="0";
        var nonlin = new CheckerSection(input, settings, sle, engine: SleEngine.Selected).Stress(new(-500,50,-30), "SLE");
        Assert(!nonlin.Native.LinearElasticAnalysis && linear.Native.LinearElasticAnalysis, "Scelta metodo rispettata");
        var twoOptions = settings["dominio2d"]!.AsObject(); twoOptions["tipo"]="Mx–My"; twoOptions["N"]="-500"; twoOptions["angoli"]="12";
        var two = new CheckerSection(input, settings, twoOptions).Domain2D();
        Assert(two.Segments.Count > 5 && two.Check(new(-500,50,-30)).Utilization is > 0, "2D N costante");
        Assert(two.Check(new(-600,50,-30)).Utilization is null, "Azione fuori piano esclusa");
        twoOptions["proietta"]="Sì"; two = new CheckerSection(input, settings, twoOptions).Domain2D();
        Assert(two.Check(new(-600,50,-30)).Status.Contains("proiettata"), "Proiezione esplicita");
        foreach (var angle in new[] {"0", "90", "35"})
        {
            twoOptions["tipo"]="N–M"; twoOptions["theta"]=angle; twoOptions["proietta"]="No";
            two = new CheckerSection(input, settings, twoOptions).Domain2D();
            double t=double.Parse(angle)*Math.PI/180;
            var check=two.Check(new(-500,50*Math.Cos(t),50*Math.Sin(t)));
            Assert(check.Utilization is > 0 && check.Resistance is not null, "N-M " + angle);
        }
        // Legacy rule of Ntc2018Checks.NtcK2FromCompressedBars (until 7/10/2026), kept for these checks and the historical comparisons
        // (the concrete fixtures of Checker are captures of the current behaviour since 06d97733): k2 from the bar stresses.
        Assert(!Ntc2018Checks.NtcK2FromCompressedBars, "D7-b: k2 dall'asse neutro come predefinito");
        Assert(Ntc2018Checks.CrackK2([-1, 100, 200]) == .5, "k2 legacy: flessione con una barra compressa");
        Assert(Ntc2018Checks.CrackK2([100, 150, 200]) == 1, "k2 legacy: trazione con tutte le barre tese");
        Assert(Ntc2018Checks.CrackK2([0, 100]) == 1 && Ntc2018Checks.CrackK2([-1e-12, 100]) == .5, "k2 legacy: zero non compresso, segno negativo rispettato");
        bool invalidK2 = false;
        try { Ntc2018Checks.CrackK2([double.NaN]); } catch (ArgumentException) { invalidK2 = true; }
        Assert(invalidK2, "k2 non accetta tensioni non finite");
        var traced = Ntc2018Checks.CrackWidthWithDetails(200,200000,30000,2.6,.02,16,40,150,400,false,true,.5);
        double Trace(string symbol) => traced.Details.Single(d => d.Symbol == symbol).Value!.Value;
        Assert(Math.Abs(traced.Width-.19185066666666667)<1e-12, "Traccia conserva risultato lunga durata");
        Assert(Trace("kt") == .4 && Trace("k₁") == .8 && Trace("k₂") == .5 && Trace("k₃") == 3.4 && Trace("k₄") == .425, "Coefficienti effettivi memorizzati");
        Assert(Math.Abs(Trace("αe") - 200000d/30000) < 1e-12 && Trace("s_lim") == 240, "Omogeneizzazione e soglia interasse tracciate");
        Assert(Math.Abs(Trace("Δε calcolata") - .0007053333333333333) < 1e-14 && Trace("Δε minima") == .0006, "Entrambi i candidati di deformazione tracciati");
        Assert(Math.Abs(1.7 * Trace("Δsm adottata") * Trace("εsm − εcm") - traced.Width) < 1e-14, "wk ricostruibile dai passaggi senza arrotondamenti");
        var shortTrace = Ntc2018Checks.CrackWidthWithDetails(200,200000,30000,2.6,.02,16,40,150,400,true,true,.5);
        Assert(shortTrace.Details.Single(d => d.Symbol == "εsm − εcm").Note.Contains("minimo"), "Indicato minimo governante breve durata");
        var farTrace = Ntc2018Checks.CrackWidthWithDetails(200,200000,30000,2.6,.02,16,40,500,400,false,true,.5);
        Assert(farTrace.Details.Single(d => d.Symbol == "Δsm adottata").Value == 300 && farTrace.Details.Single(d => d.Symbol == "s − s_lim").Note.Contains("s > s_lim"), "Ramo distanziato ricostruibile");
        var ntcLong = Ntc2018Checks.CrackWidth(200,200000,30000,2.6,.02,16,40,150,400,false,true,.5);
        Assert(Math.Abs(ntcLong-.19185066666666667)<1e-12,"Fessure: calcolo indipendente lunga durata");
        Assert(Math.Abs(Ntc2018Checks.CrackWidth(200,200000,30000,2.6,.02,16,40,150,400,true,true,.5)-.1632)<1e-12,"Fessure: breve durata e deformazione minima");
        Assert(Ntc2018Checks.CrackWidth(200,200000,30000,2.6,.02,16,40,500,400,false,true,.5)>ntcLong,"Spaziatura grande non usa il minimo non cautelativo");
        Assert(Math.Abs(Ntc2018Checks.CrackWidth(200,200000,30000,2.6,.02,16,40,500,400,false,true,.5)-.35972)<1e-12,"C4.1.10 distanza media 0,75(h-x)");
        Assert(Ntc2018Checks.CrackRequirement("SLE_QP","XC4",true).Kind=="Decompressione","Decompressione distinta da wk=0");
        Assert(Ntc2018Checks.CrackRequirement("SLE_FREQ","XD3",true).Kind=="Formazione fessure","Formazione distinta da decompressione");
        Assert(Ntc2018Checks.CrackRequirement("SLE_QP","XC1",false).Limit==.3,"Limite apertura NTC");
        Assert(Ntc2018Checks.CrackRequirement("SLE_FREQ","XC1",false).Limit==.4,"Limite frequente NTC");
        bool invalidCrack = false;
        try { Ntc2018Checks.CrackWidth(200,200000,30000,2.6,.02,16,double.NaN,150,400,false,true,.5); } catch(ArgumentException) { invalidCrack=true; }
        Assert(invalidCrack,"Copriferro non finito respinto");
        // The same formula checks on the functions of GPCChecker.Concrete (refactoring F2.7, commit A5), with the same independent expected
        // values; the legacy assertions above stay in tests/ConcreteLibraryAdapter.Checks/legacy-allowlist.json until F2.11.
        {
            var ntc = CrackProfiles.Resolve(ConcreteLibraryMapping.StandardFor("NTC 2018"));
            double Width(double spacing, bool shortTerm, double cover = 40, List<ShearCalculationDetail>? details = null) =>
                CrackWidthCalculator.Width(ntc, new CrackWidthInput(200, 200000, 30000, 2.6, .02, 16, cover, spacing, 400, shortTerm, true, .5), details);
            Assert(CrackWidthCalculator.K2([-1, 100, 200]) == .5, "k2 libreria (regola legacy): flessione con una barra compressa");
            Assert(CrackWidthCalculator.K2([100, 150, 200]) == 1, "k2 libreria (regola legacy): trazione con tutte le barre tese");
            Assert(CrackWidthCalculator.K2([0, 100]) == 1 && CrackWidthCalculator.K2([-1e-12, 100]) == .5, "k2 libreria: zero non compresso, segno negativo rispettato");
            bool invalidLibraryK2 = false;
            try { CrackWidthCalculator.K2([double.NaN]); } catch (ArgumentException) { invalidLibraryK2 = true; }
            Assert(invalidLibraryK2, "k2 libreria: tensioni non finite respinte");
            var steps = new List<ShearCalculationDetail>(); double width = Width(150, false, details: steps);
            double Step(string symbol) => steps.Single(d => d.Symbol == symbol).Value;
            Assert(Math.Abs(width - .19185066666666667) < 1e-12, "Fessure libreria: calcolo indipendente lunga durata");
            Assert(Step("kt") == .4 && Step("k1") == .8 && Step("k2") == .5, "Coefficienti effettivi nei passaggi della libreria");
            Assert(Math.Abs(Step("αe") - 200000d/30000) < 1e-12 && Step("s,lim") == 240, "Omogeneizzazione e soglia interasse nei passaggi della libreria");
            Assert(Math.Abs(1.7 * Step("Δsm") * Step("εsm − εcm") - width) < 1e-14, "wk della libreria ricostruibile dai passaggi senza arrotondamenti");
            Assert(Math.Abs(Width(150, true) - .1632) < 1e-12, "Fessure libreria: breve durata e deformazione minima");
            var farSteps = new List<ShearCalculationDetail>(); double farWidth = Width(500, false, details: farSteps);
            Assert(farWidth > width && farSteps.Single(d => d.Symbol == "Δsm").Value == 300, "Libreria: spaziatura grande non usa il minimo non cautelativo (Δsm = 0,75(h−x))");
            Assert(Math.Abs(farWidth - .35972) < 1e-12, "Libreria: C4.1.10 distanza media 0,75(h-x)");
            CrackRequirement Requirement(string set, string exposure, bool sensitive) => CrackRequirements.For(ntc, ConcreteLibraryMapping.RequireCombination(set), exposure, sensitive);
            Assert(Requirement("SLE_QP", "XC4", true).Criterion == CrackCriterion.Decompression, "Libreria: decompressione distinta da wk=0");
            Assert(Requirement("SLE_FREQ", "XD3", true).Criterion == CrackCriterion.CrackFormation, "Libreria: formazione distinta da decompressione");
            Assert(Requirement("SLE_QP", "XC1", false).Limit == .3, "Libreria: limite apertura NTC");
            Assert(Requirement("SLE_FREQ", "XC1", false).Limit == .4, "Libreria: limite frequente NTC");
            bool invalidLibraryCover = false;
            try { Width(150, false, double.NaN); } catch (ArgumentException) { invalidLibraryCover = true; }
            Assert(invalidLibraryCover, "Libreria: copriferro non finito respinto");
        }
        var otherNorm=(JsonObject)settings.DeepClone();otherNorm["normativa"]="EC2";bool invalidNorm=false;
        try { _=new CheckerSection(input,otherNorm,options); } catch(ArgumentException) { invalidNorm=true; }
        Assert(invalidNorm,"Normativa fuori campo non etichettata NTC");
        var shear=Ntc2018Checks.Shear(-300,100,150000,300,450,1000,30,17,450/1.15,1.5,2*Math.PI*100/4,150,90,2);
        Assert(Math.Abs(shear.VRsd-331.9160934010086)<1e-9 && Math.Abs(shear.VRcd-461.7)<1e-9,"Taglio: confronto indipendente NTC");
        Assert(Ntc2018Checks.Shear(300,100,150000,300,450,1000,30,17,450/1.15,1.5,0,150,90).Ratio is null,"Trazione senza staffe non migliora la resistenza");
        Assert(Ntc2018Checks.Shear(-3000,100,150000,300,450,1000,30,17,450/1.15,1.5,157,150,90).Ratio is null,"Compressione oltre fcd non produce falso pass");
        sle["modello"]="Lineare"; sle["esposizione"]="XC1"; sle["spaziatura_fessure"]="200";
        var crackEngine=new CheckerSection(input,settings,sle,engine:SleEngine.Selected); var crackAction=new ActionPoint(-100,50,0); var crackStress=crackEngine.Stress(crackAction,"SLE_QP");
        var crack=ConcreteServiceabilityAdapter.Cracking(crackEngine,crackStress,crackAction,input,settings,sle,"SLE_QP",SleEngine.Selected);
        Assert(crack.Details.Any(d => d.Symbol == "hc,eff") && crack.Details.Any(d => d.Symbol == "Criterio k₂") && crack.Details.Any(d => d.Symbol.StartsWith("B") && d.Symbol.EndsWith(" · σs")), "Traccia geometria, assunzioni e tensioni delle singole barre");
        Assert(J.Node(crack)?["Details"] is JsonArray { Count: > 30 }, "Passaggi esportati in JSON");
        var compact = CrackCalculationSummary.Values(crack);
        Assert(compact.Length == CrackCalculationSummary.MaxValues && compact.Select(d => d.Symbol).Distinct().Count() == compact.Length, "Riepilogo limitato a 30 valori senza duplicati");
        Assert(compact.Single(d => d.Symbol == "wk").Value == crack.Width && compact.Single(d => d.Symbol == "ηw").Value == crack.Ratio, "Riepilogo conserva risultati non arrotondati");
        Assert(!CrackCalculationSummary.Format(crack).Contains("B01 ·") && compact.Any(d => d.Symbol == "k₂"), "Riepilogo senza singole barre, con coefficienti normativi");
        var restoredCrack = System.Text.Json.JsonSerializer.Deserialize<Ntc2018Checks.CrackResult>(J.Node(crack)!.ToJsonString())!;
        Assert(CrackCalculationSummary.Values(restoredCrack).SequenceEqual(compact), "Riepilogo report identico dopo round trip JSON");
        Assert(crack.Width is >0 && crack.EffectiveArea is >0 && crack.Ratio is >0,"Fessurazione da tensioni native e mesh tagliata");
        Assert(crack.Details.Single(d => d.Symbol == "Criterio k₂").Value == .5, "k2 = 0,5 con l'asse neutro interno alla sezione (D7-b)");
        foreach (var force in new[] { new ActionPoint(100, 0, 0), new ActionPoint(100, 10, 0), new ActionPoint(200, 20, 0), new ActionPoint(-100, 50, 0) })
        {
            var nativeState = crackEngine.Stress(force, "SLE_QP");
            var checkedCrack = ConcreteServiceabilityAdapter.Cracking(crackEngine, nativeState, force, input, settings, sle, "SLE_QP", SleEngine.Selected);
            double expectedK2 = .5;
            var eps=nativeState.ConcreteVertices.Select(v=>v.Strain).ToArray();
            Assert(eps.Max()>1e-12, "Casi di prova non interamente compressi");
            if(eps.Min()>=0&&eps.Max()>0)expectedK2=(eps.Min()+eps.Max())/(2*eps.Max());
            Assert(checkedCrack.Details.Single(d => d.Symbol == "Criterio k₂").Value == expectedK2, "Criterio k2 anche per trazione pura e pressoflessione");
            if (checkedCrack.Width is > 0)
                Assert(checkedCrack.Details.Single(d => d.Symbol == "k₂").Value == expectedK2, "Il coefficiente selezionato entra nella formula wk");
        }
        var strainPlane=crackStress.Native.StrainPlane;
        double depth=crackEngine.Section.ConcreteShape.GetPoints2d().Max(p=>strainPlane.GetStrain(p))/Math.Abs(strainPlane.ChiY);
        double effectiveHeight=Math.Min(125,Math.Min(depth/3,250));
        Assert(Math.Abs(crack.EffectiveArea!.Value-300*effectiveHeight)/(300*effectiveHeight)<.01,"Ac,eff rettangolare confronto analitico");
        Assert(Math.Abs(crack.EffectiveSteel!.Value-2*Math.PI*81)<1e-6,"As,eff solo barre tese nella fascia");
        foreach(var exposure in new[]{"XC4","XD3"})
        {
            sle["esposizione"]=exposure;sle["sensibilita"]="Sensibile";
            var result=ConcreteServiceabilityAdapter.Cracking(crackEngine,crackStress,crackAction,input,settings,sle,exposure=="XC4"?"SLE_QP":"SLE_FREQ",SleEngine.Selected);
            Assert(result.Details.Any(d => d.Symbol == "σct,max") && result.Details.Any(d => d.Symbol == "σct,lim"), "Traccia ramo sezione integra " + exposure);
            var compactUncracked = CrackCalculationSummary.Values(result);
            Assert(compactUncracked.Length <= CrackCalculationSummary.MaxValues && compactUncracked.Any(d => d.Symbol == "σct,lim") && !compactUncracked.Any(d => d.Symbol == "wk"), "Riepilogo pertinente per decompressione e formazione " + exposure);
            Assert(result.Width is null && result.Ratio is null && result.Passed.HasValue,"Verifica sezione integra "+exposure);
        }
        options["assi"]="Personalizzati";options["origine_x"]="10";options["origine_y"]="20";options["rotazione"]="0";
        var axesEngine=new CheckerSection(input,settings,options);var eccentric=axesEngine.Force(new(-100,0,0));
        Assert(Math.Abs(eccentric.M1-2e6)<1e-6&&Math.Abs(eccentric.M2+1e6)<1e-6,"Eccentricità origine usa trasformazione DLL");
        options["origine_x"]="0";options["origine_y"]="0";options["rotazione"]="90";
        var rotated=new CheckerSection(input,settings,options).Force(new(-100,10,0));
        Assert(Math.Abs(rotated.M1)<1e-6&&Math.Abs(rotated.M2-1e7)<1e-6,"Rotazione assi coerente CheckerUI");
        options["assi"]="Locali";
        foreach(var shape in new[]{"Circolare","A T"})
        {
            var shaped=SezioneCA.DefaultInput();shaped["shape"]=shape;
            foreach(var mode in new[]{"SLU","SLV"})
            {
                var domain=new CheckerSection(shaped,settings,options,mode).Domain3D();
                Assert(domain.Check(new(-500,50,30)).Utilization is >0,shape+mode+" risposta DLL");
            }
        }
        settings["trefoli"]!.AsArray().Add(J.Obj(("id","T1"),("x","0"),("y","-100"),("area","150"),("sigma0","1000"),("Ep","195000"),("fpyk","1670"),("fpk","1860"),("eps_u","35")));
        var prestressed=new CheckerSection(input,settings,options).Domain3D();
        Assert(prestressed.Check(new(-500,50,30)).Utilization is >0,"Trefolo realmente incluso nel calcolo");
        using var cts=new CancellationTokenSource();cts.Cancel();bool stopped=false;
        try { prestressed.Section.Domain3D(cts.Token); } catch(OperationCanceledException) { stopped=true; }
        Assert(stopped,"Cancellazione prima del calcolo");
        Console.WriteLine($"Checker DLL e controlli NTC: {passed} controlli superati."); return passed;
    }
}

/// <summary>D7-b (7/10/2026): k₂ = 0,50 whenever the neutral axis crosses the section, for every standard
/// (Circolare 2019 C4.1.2.2.4.5, EN 1992-1-1 7.3.4(3)); wk = 0 for a fully compressed section before k₂ is chosen;
/// fully tensioned sections keep (εmax + εmin)/(2 εmax). Widths are recomputed by hand from the trace inputs.</summary>
internal static class CrackK2Checks
{
    internal static int Run()
    {
        int passed = 0;
        void Assert(bool condition, string name) { if (!condition) throw new Exception("k2 D7-b: " + name); passed++; }
        var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data);
        var input = data["input"]!.AsObject(); input["shape"] = "Rettangolare"; input["width_mm"] = "300"; input["height_mm"] = "500"; input["fck_mpa"] = "30";
        JsonArray Bars(params (double X, double Y, double Phi)[] bars) => new(bars.Select(b => (JsonNode)J.Obj(("x", b.X.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("y", b.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("phi", b.Phi.ToString(System.Globalization.CultureInfo.InvariantCulture)))).ToArray());
        var single = Bars((-100, -200, 20), (0, -200, 20), (100, -200, 20));
        var both = Bars((-100, -200, 20), (0, -200, 20), (100, -200, 20), (-100, 200, 16), (100, 200, 16));
        var sle = (JsonObject)settings["sle"]!["SLE_QP"]!.DeepClone();
        sle["modello"] = "Lineare"; sle["phi"] = "0"; sle["trazione_cls"] = "No"; sle["esposizione"] = "XC1"; sle["sensibilita"] = "Poco sensibile";
        sle["durata"] = "Lunga"; sle["aderenza"] = "Migliorata"; sle["copriferro_fessure"] = ""; sle["spaziatura_fessure"] = ""; sle["limite_fessure"] = "0.3";
        (Ntc2018Checks.CrackResult Result, CheckerStressState State, CheckerSection Engine) Crack(string code, JsonArray bars, double n, double m)
        {
            var i = (JsonObject)input.DeepClone(); i["barre_manuali"] = bars.DeepClone();
            var w = (JsonObject)settings.DeepClone(); w["normativa"] = code;
            var engine = new CheckerSection(i, w, sle, engine: SleEngine.Selected); var action = new ActionPoint(n, m, 0); var state = engine.Stress(action, "SLE_QP");
            return (ConcreteServiceabilityAdapter.Cracking(engine, state, action, i, w, sle, "SLE_QP", SleEngine.Selected), state, engine);
        }
        double V(Ntc2018Checks.CrackResult r, string symbol) => r.Details.Last(d => d.Symbol == symbol).Value!.Value;
        int Count(Ntc2018Checks.CrackResult r, string symbol) => r.Details.Count(d => d.Symbol == symbol);
        // Hand calculation of the NTC branch (Circolare C4.1.2.2.4.5 with the Δsm path of the code), inputs from the trace.
        double NtcWidth(Ntc2018Checks.CrackResult r, double k2)
        {
            double sigma = V(r, "σs (formula)"), es = V(r, "Es"), ecm = V(r, "Ecm"), fct = V(r, "fct,eff = fctm"), rho = V(r, "ρp,eff"), phi = V(r, "Øeq (formula)");
            double c = V(r, "c (formula)"), s = V(r, "s (formula)"), hx = V(r, "h − x (formula)"), kt = V(r, "kt"), k1 = V(r, "k₁");
            double strain = Math.Max((sigma - kt * fct / rho * (1 + es / ecm * rho)) / es, .6 * sigma / es);
            double near = (3.4 * c + k1 * k2 * .425 * phi / rho) / 1.7, distance = s <= 5 * (c + phi / 2) ? near : Math.Max(near, .75 * hx);
            return Math.Max(0, 1.7 * distance * strain);
        }
        bool Bending(Ntc2018Checks.CrackResult r) => V(r, "εc,min") < 0 && V(r, "εc,max") > 1e-12;
        void FlexureK2(Ntc2018Checks.CrackResult r, string name)
        {
            Assert(Count(r, "Criterio k₂") == 1 && V(r, "Criterio k₂") == .5, name + ": un solo Criterio k₂ = 0,50");
            Assert(r.Details.Single(d => d.Symbol == "Criterio k₂").Expression == "Asse neutro interno alla sezione: flessione, k₂ = 0,50 (Circolare 2019 C4.1.2.2.4.5; EN 1992-1-1 7.3.4(3))", name + ": testo del criterio");
            Assert(!r.Details.Any(d => d.Symbol.EndsWith("per k₂")), name + ": nessun conteggio di barre usato per k₂");
        }
        // Orientation with the bottom bars in tension, from the sign convention of the native checker.
        double sign = Crack("NTC 2018", single, 0, 100).State.tensioni_barre.Take(3).All(s => s > 0) ? 1 : -1;

        // (a) Singly reinforced beam in bending, NTC: no compressed bar, neutral axis inside: k2 = 0,5 (the bar rule gave 1).
        var (beam, beamState, _) = Crack("NTC 2018", single, 0, sign * 100);
        Assert(Bending(beam) && beamState.tensioni_barre.Take(3).All(s => s > 0) && Ntc2018Checks.CrackK2(beamState.tensioni_barre.Take(3).ToArray()) == 1, "(a) trave a semplice armatura inflessa, nessuna barra compressa");
        FlexureK2(beam, "(a)");
        Assert(V(beam, "k₂") == .5 && beam.Width is > 0, "(a) k₂ = 0,50 nella formula di wk");
        double handBeam = NtcWidth(beam, .5), legacyBeam = NtcWidth(beam, 1);
        Assert(Math.Abs(beam.Width!.Value - handBeam) <= 1e-12 * handBeam, $"(a) wk = {beam.Width:R} mm come il calcolo a mano {handBeam:R} mm");
        Assert(handBeam < legacyBeam, "(a) wk con k₂ = 0,50 minore di quello con k₂ = 1,00");
        Console.WriteLine($"(a) trave a semplice armatura NTC: wk = {handBeam:0.0000} mm (con k₂ = 1: {legacyBeam:0.0000} mm, {100 * (handBeam / legacyBeam - 1):0.0} %)");

        // (b) Combined compression and bending with the neutral axis inside: 0,5 with and without compressed bars; also tension and bending.
        foreach (var (bars, n, m, name) in new[] { (single, -150d, 120d, "(b) pressoflessione, semplice armatura"), (both, -300d, 150d, "(b) pressoflessione, doppia armatura"),
            (single, 60d, 100d, "(b) tensoflessione, asse neutro interno") })
        {
            var (r, state, _) = Crack("NTC 2018", bars, n, sign * m);
            Assert(Bending(r), name + ": asse neutro interno");
            FlexureK2(r, name);
            Assert(r.Width is > 0 && V(r, "k₂") == .5 && Math.Abs(r.Width!.Value - NtcWidth(r, .5)) <= 1e-12 * r.Width.Value, name + ": wk con k₂ = 0,50");
        }

        // (c) Pure compression and compression with εc,max ≤ 1e-12: wk = 0, k2 never chosen, no bending criterion in the trace.
        foreach (var (bars, n, m, name) in new[] { (both, -500d, 0d, "(c) compressione semplice"), (both, -900d, 8d, "(c) pressoflessione interamente compressa"),
            (single, -600d, 5d, "(c) compressione, semplice armatura") })
        {
            var (r, _, _) = Crack("NTC 2018", bars, n, sign * m);
            Assert(V(r, "εc,max") <= 1e-12, name + ": εc,max ≤ 1e-12");
            Assert(r.Width == 0 && r.Ratio == 0 && r.Passed == true && r.Status == "Sezione interamente compressa", name + ": wk = 0, soddisfatta");
            Assert(!r.Details.Any(d => d.Symbol is "Criterio k₂" or "k₂" || d.Symbol.EndsWith("per k₂")), name + ": nessun k₂ nella traccia");
            Assert(!r.Details.Any(d => (d.Expression + " " + d.Note).Contains("lession")), name + ": nessuna flessione dichiarata");
            Assert(!CrackCalculationSummary.Values(r).Any(d => d.Symbol == "k₂"), name + ": riepilogo senza k₂");
        }

        // (d) Fully tensioned section: dedicated branch, k2 = (εmax + εmin)/(2 εmax), unchanged.
        foreach (var (n, m) in new[] { (300d, 0d), (300d, 8d) })
        {
            var (r, state, engine) = Crack("NTC 2018", both, n, sign * m);
            var plane = state.Native.StrainPlane; var strains = engine.Geometry.Outline.Select(p => plane.GetStrain(p[0], p[1])).ToArray();
            double expected = Math.Clamp((strains.Min() + strains.Max()) / (2 * strains.Max()), .5, 1);
            Assert(V(r, "εc,min") >= 0 && r.Status.StartsWith("Interamente tesa"), $"(d) N = {n}, M = {m}: ramo interamente teso");
            Assert(Count(r, "Criterio k₂") == 1 && V(r, "Criterio k₂") == expected && V(r, "k₂ · interamente tesa") == expected && V(r, "k₂") == expected, $"(d) N = {n}, M = {m}: k₂ = (εmax + εmin)/(2 εmax)");
            Assert(expected is > .5 and <= 1, $"(d) N = {n}, M = {m}: k₂ = {expected:0.000} fra 0,5 e 1 (armatura asimmetrica: trazione non uniforme)");
            Assert(r.Width is > 0, $"(d) N = {n}, M = {m}: wk calcolato");
        }

        // (e) Other standards: k2 was already 0,5 with the neutral axis inside; now a single criterion, same numbers.
        foreach (var code in ConcreteStandards.OrdinaryNames.Where(x => x != "NTC 2018"))
        {
            var (r, _, _) = Crack(code, single, 0, sign * 100);
            Assert(Bending(r) && r.Width is > 0, code + ": wk calcolato in flessione");
            FlexureK2(r, code);
            // Model Code 2010 and DIN: sr,max without k₂, declared in the criterion and in the formula trace.
            string note = r.Details.Single(d => d.Symbol == "Criterio k₂").Note, formula = r.Details.Last(d => d.Symbol == "k₂").Expression;
            if (code is "Model Code 2010" || code.StartsWith("DIN"))
                Assert(note == $"Con {code} sr,max non contiene k₂: valore solo informativo." && formula.EndsWith("non entra in sr,max di " + code), code + ": k₂ dichiarato senza effetto su sr,max");
            else
                Assert(note.StartsWith("Entra in wk solo con il termine k₁·k₂·k₄·Øeq/ρp,eff di sr,max") && !formula.Contains("non entra"), code + ": nota del criterio sui rami in cui k₂ non entra");
            if (code is "EN 1992-1-1" or "UNI EN 1992-1-1" or "NS EN 1992-1-1")
            {
                double c = V(r, "c"), phi = V(r, "Øeq"), rho = V(r, "ρp,eff"), s = V(r, "s");
                Assert(V(r, "k₂") == .5, code + ": k₂ = 0,50 nella formula");
                if (s <= 5 * (c + phi / 2)) Assert(Math.Abs(V(r, "sr,max") - (3.4 * c + .8 * .5 * .425 * phi / rho)) < 1e-9, code + ": sr,max = 3,4c + k₁k₂k₄Ø/ρ con k₂ = 0,50");
                Assert(Math.Abs(r.Width!.Value - V(r, "sr,max") * V(r, "εsm − εcm")) < 1e-12, code + ": wk = sr,max (εsm − εcm)");
            }
        }

        // (f) Tensile bars all outside Ac,eff (singly reinforced, the unreinforced face in tension): k₂ written once, does not enter wk;
        // the summary shows no k₂.
        {
            var (r, _, _) = Crack("NTC 2018", single, 0, -sign * 100);
            Assert(Bending(r) && r.Status.StartsWith("Nessuna barra in Ac,eff") && r.Width is > 0, "(f) limite superiore senza barre tese in Ac,eff");
            FlexureK2(r, "(f)");
            Assert(r.Details.Single(d => d.Symbol == "Criterio k₂").Note.Contains("se non ci sono barre tese in Ac,eff") && !r.Details.Any(d => d.Symbol == "k₂"), "(f) k₂ non entra nel limite superiore, dichiarato nel criterio");
            Assert(!CrackCalculationSummary.Values(r).Any(d => d.Symbol == "k₂") && !CrackCalculationSummary.Format(r).Contains("k₂"), "(f) riepilogo senza k₂");
        }

        // (g) Box section in bending, inner wall governing: the band of the hole keeps the k₂ of its own strain distribution
        // (EN 1992-1-1 7.3.4(3), local areas), declared in the trace and in the summary; the section criterion stays 0,50.
        {
            var box = (JsonObject)input.DeepClone(); box["width_mm"] = "1000"; box["height_mm"] = "1000"; box["foro_presente"] = true; box["inner_width_mm"] = "600"; box["inner_height_mm"] = "600";
            var boxBars = new List<(double X, double Y, double Phi)>();
            foreach (var x in new[] { -400d, -200, 0, 200, 400 }) { boxBars.Add((x, -440, 20)); boxBars.Add((x, 440, 20)); }
            foreach (var x in new[] { -250d, 0, 250 }) { boxBars.Add((x, -340, 12)); boxBars.Add((x, 340, 12)); }
            foreach (var y in new[] { -200d, 0, 200 }) { boxBars.Add((-440, y, 16)); boxBars.Add((440, y, 16)); boxBars.Add((-340, y, 12)); boxBars.Add((340, y, 12)); }
            box["barre_manuali"] = Bars(boxBars.ToArray());
            var w = (JsonObject)settings.DeepClone(); w["normativa"] = "NTC 2018";
            var engine = new CheckerSection(box, w, sle, engine: SleEngine.Selected); var action = new ActionPoint(0, sign * 600, 0); var state = engine.Stress(action, "SLE_QP");
            var r = ConcreteServiceabilityAdapter.Cracking(engine, state, action, box, w, sle, "SLE_QP", SleEngine.Selected);
            Assert(Bending(r), "(g) cassone: asse neutro interno");
            FlexureK2(r, "(g)");
            Assert(r.Details.Single(d => d.Symbol == "Criterio k₂").Note.EndsWith("Le fasce interne dei fori usano il k₂ della propria distribuzione di deformazioni («k₂ della fascia»)."), "(g) criterio della sezione rimanda al k₂ delle fasce");
            Assert(r.Details.Single(d => d.Symbol == "Superficie governante").Expression == "Parete interna −y", "(g) governa la parete interna tesa");
            // Hand check: the band of the bottom wall goes from the hole (y = −300) to −300 − hc,eff and is entirely in tension.
            double hc = V(r, "Parete interna −y · hc,eff");
            var corners = new[] { (-300d, -300d), (300d, -300d), (-300d, -300 - hc), (300d, -300 - hc) }.Select(p => state.Native.StrainPlane.GetStrain(p.Item1, p.Item2)).ToArray();
            double bandK2 = (corners.Min() + corners.Max()) / (2 * corners.Max());
            Assert(corners.Min() > 0 && Math.Abs(V(r, "k₂ della fascia") - bandK2) < 1e-12 && bandK2 > .5, $"(g) k₂ della fascia = (εmax + εmin)/(2 εmax) ai vertici della fascia = {bandK2:R} (traccia {V(r, "k₂ della fascia"):R})");
            Assert(V(r, "k₂") == V(r, "k₂ della fascia") && Math.Abs(r.Width!.Value - NtcWidth(r, bandK2)) <= 1e-12 * r.Width.Value, "(g) wk della parete con il k₂ della fascia, calcolo a mano");
            var summaryK2 = CrackCalculationSummary.Values(r).Single(d => d.Symbol == "k₂");
            Assert(summaryK2.Value == bandK2 && summaryK2.Expression.StartsWith("k₂ della fascia: "), "(g) riepilogo: k₂ della fascia dichiarato");
            Console.WriteLine($"(g) cassone NTC, M = 600 kNm: governa la parete interna −y, k₂ della fascia = {bandK2:0.0000}, wk = {r.Width:0.0000} mm");
        }

        // (h) Neutral axis in the cover of the reinforced tensile edge: no tensile bar, wk = 0; k₂ written once with the note
        // that it does not enter, no k₂ of the formula and no k₂ in the summary.
        {
            const double moment = 80; // h − x ≈ 12,6 mm, the bottom bars are 50 mm from the edge
            var (h, _, _) = Crack("NTC 2018", both, -900, sign * moment);
            Assert(h.Status == "Asse neutro nel copriferro: nessuna barra tesa, wk = 0", "(h) N = −900 kN, M = 80 kNm: asse neutro nel copriferro");
            Assert(Bending(h) && h.Width == 0 && h.Ratio == 0 && h.Passed == true, $"(h) M = {moment} kNm: asse neutro interno alla sezione, wk = 0, soddisfatta");
            Assert(V(h, "h − d,min") > V(h, "h − x"), "(h) barra più vicina al lembo teso oltre la profondità tesa");
            FlexureK2(h, "(h)");
            Assert(h.Details.Single(d => d.Symbol == "Criterio k₂").Note.Contains("non entra se l'asse neutro è nel copriferro senza barre tese"), "(h) il criterio dichiara che k₂ non entra");
            Assert(!h.Details.Any(d => d.Symbol == "k₂"), "(h) nessun k₂ della formula");
            Assert(!CrackCalculationSummary.Values(h).Any(d => d.Symbol == "k₂") && !CrackCalculationSummary.Format(h).Contains("k₂"), "(h) riepilogo senza k₂");
            Console.WriteLine($"(h) asse neutro nel copriferro: N = −900 kN, M = {moment} kNm, h − x = {V(h, "h − x"):0.0} mm, h − d,min = {V(h, "h − d,min"):0.0} mm, wk = 0");
        }

        // (i) Bar stresses missing or not finite: with the new rule k₂ does not use them, so a fully compressed section still gives
        // wk = 0; where σs is needed the check stops with a message that does not mention k₂.
        {
            var (compressed, compressedState, compressedEngine) = Crack("NTC 2018", both, -500, 0);
            foreach (var (stresses, label) in new[] { (Array.Empty<double>(), "mancanti"), (compressedState.tensioni_barre.Select(_ => double.NaN).ToArray(), "non finite") })
            {
                var state = compressedState with { tensioni_barre = stresses };
                var action = new ActionPoint(-500, 0, 0);
                var i = (JsonObject)input.DeepClone(); i["barre_manuali"] = both.DeepClone();
                var w = (JsonObject)settings.DeepClone(); w["normativa"] = "NTC 2018";
                var r = ConcreteServiceabilityAdapter.Cracking(compressedEngine, state, action, i, w, sle, "SLE_QP", SleEngine.Selected);
                Assert(r.Width == 0 && r.Passed == true && r.Status == "Sezione interamente compressa" && compressed.Width == 0, "(i) compressione con tensioni delle barre " + label + ": wk = 0");
            }
            var (bent, bentState, bentEngine) = Crack("NTC 2018", single, 0, sign * 100);
            Assert(bent.Width is > 0, "(i) caso inflesso calcolato con le tensioni delle barre");
            string? message = null;
            try
            {
                var i = (JsonObject)input.DeepClone(); i["barre_manuali"] = single.DeepClone();
                var w = (JsonObject)settings.DeepClone(); w["normativa"] = "NTC 2018";
                ConcreteServiceabilityAdapter.Cracking(bentEngine, bentState with { tensioni_barre = bentState.tensioni_barre.Select(_ => double.NaN).ToArray() }, new ActionPoint(0, sign * 100, 0), i, w, sle, "SLE_QP", SleEngine.Selected);
            }
            catch (ArgumentException ex) { message = ex.Message; }
            Assert(message == "tensioni delle armature mancanti o non finite.", $"(i) flessione con tensioni non finite: errore senza k₂ («{message}»)");
        }

        // (j) Hollow sections in nearly uniform tension (defect found in the D7-b review, already in 733a77c): the tensile depth h − x of an
        // inner band was εmax/|∇ε| also with a numerical-noise gradient (≈ 1e-12 1/mm), giving h − x ≈ 1e11 mm and wk ≈ 1e9 mm with sparse bars
        // (EN 1992-1-1 7.3.4(3), eq. (7.14)). Now (R15, decision of the user of 7/10/2026) h − x ≤ h of the section along the gradient (x ≥ 0) and,
        // with a gradient negligible against the strain, uniform tension: k₂ = 1 and h − x = h of the section normal to the face. See also (k).
        {
            var box = (JsonObject)input.DeepClone(); box["width_mm"] = "1000"; box["height_mm"] = "1000"; box["foro_presente"] = true; box["inner_width_mm"] = "600"; box["inner_height_mm"] = "600";
            var boxBars = new List<(double X, double Y, double Phi)>();
            foreach (var x in new[] { -400d, -200, 0, 200, 400 }) { boxBars.Add((x, -440, 20)); boxBars.Add((x, 440, 20)); }
            foreach (var x in new[] { -250d, 0, 250 }) { boxBars.Add((x, -340, 12)); boxBars.Add((x, 340, 12)); }
            foreach (var y in new[] { -200d, 0, 200 }) { boxBars.Add((-440, y, 16)); boxBars.Add((440, y, 16)); boxBars.Add((-340, y, 12)); boxBars.Add((340, y, 12)); }
            box["barre_manuali"] = Bars(boxBars.ToArray());
            var ring = (JsonObject)input.DeepClone(); ring["shape"] = "Circolare"; ring["diameter_mm"] = "1000"; ring["foro_presente"] = true; ring["inner_diameter_mm"] = "600";
            var ringBars = new List<(double X, double Y, double Phi)>();
            for (int k = 0; k < 16; k++) ringBars.Add((Math.Round(440 * Math.Cos(2 * Math.PI * k / 16), 6), Math.Round(440 * Math.Sin(2 * Math.PI * k / 16), 6), 20));
            for (int k = 0; k < 8; k++) ringBars.Add((Math.Round(340 * Math.Cos(2 * Math.PI * (k + .5) / 8), 6), Math.Round(340 * Math.Sin(2 * Math.PI * (k + .5) / 8), 6), 12));
            ring["barre_manuali"] = Bars(ringBars.ToArray());
            foreach (var (section, shape, code, n, m) in new[] { (box, "cassone", "NTC 2018", 3000d, 0d), (box, "cassone", "EN 1992-1-1", 3000d, 0d), (ring, "anello", "NTC 2018", 3000d, 0d),
                (ring, "anello", "EN 1992-1-1", 3000d, 0d), (box, "cassone", "NTC 2018", 3000d, 100d), (box, "cassone", "EN 1992-1-1", 3000d, 100d) })
            {
                var w = (JsonObject)settings.DeepClone(); w["normativa"] = code;
                var options = (JsonObject)sle.DeepClone(); if (shape == "anello") options["spaziatura_fessure"] = "300";
                var engine = new CheckerSection(section, w, options, engine: SleEngine.Selected); var action = new ActionPoint(n, sign * m, 0); var state = engine.Stress(action, "SLE_QP");
                var r = ConcreteServiceabilityAdapter.Cracking(engine, state, action, section, w, options, "SLE_QP", SleEngine.Selected);
                var plane = state.Native.StrainPlane; double gradient = double.Hypot(plane.ChiX, plane.ChiY);
                var strains = engine.Geometry.Outline.Select(p => plane.GetStrain(p[0], p[1])).ToArray();
                string label = $"(j) {shape} {code}, N = {n} kN, M = {m} kNm";
                Console.WriteLine($"{label}: |∇ε| = {gradient:E3} 1/mm, εmin = {strains.Min():E6}, εmax = {strains.Max():E6}, |∇ε|·h/εmax = {gradient * 1000 / strains.Max():E3}, wk = {r.Width:G6} mm, {r.Status}");
                foreach (var d in r.Details.Where(d => d.Symbol.EndsWith("h − x (formula)") || d.Symbol.EndsWith("sr,max") || d.Symbol.EndsWith("h − x della fascia") || d.Symbol.EndsWith("k₂ della fascia")))
                    Console.WriteLine($"    {d.Symbol} = {d.Value:G8} {d.Unit}  {d.Expression}");
                Assert(strains.Min() > 0 && r.Width is double width && double.IsFinite(width) && width < 10, $"{label}: sezione interamente tesa, wk finito e realistico ({r.Width:G6} mm)");
                // Every tensile depth of the trace is within the section (h = 1000 mm along any axis direction, ≤ the diagonal along any other).
                Assert(r.Details.Where(d => d.Symbol.EndsWith("h − x (formula)") || d.Symbol.EndsWith("h − x della fascia")).All(d => d.Value <= 1000 * Math.Sqrt(2) + 1e-9), $"{label}: h − x delle fasce entro la sezione");
                Assert(r.Details.Where(d => d.Symbol.EndsWith("sr,max")).All(d => d.Value <= 1.3 * 1000 * Math.Sqrt(2) + 1e-9), $"{label}: sr,max entro 1,3 h");
                string[] bands = shape == "anello" ? ["Anello interno"] : ["Parete interna +x", "Parete interna −x", "Parete interna +y", "Parete interna −y"];
                foreach (var band in bands)
                {
                    var depth = r.Details.Single(d => d.Symbol == band + " · h − x della fascia"); var bandK2 = r.Details.Single(d => d.Symbol == band + " · k₂ della fascia");
                    if (m == 0)
                    {
                        // Noise gradient: uniform tension, h of the section normal to the face (1000 mm for both shapes), k₂ = 1.
                        Assert(gradient * 1000 / strains.Max() < 1e-8 && depth.Value == 1000 && depth.Expression == "Trazione uniforme: h della sezione normale alla faccia"
                            && bandK2.Value == 1 && bandK2.Expression == "Trazione uniforme: 1", $"{label}, {band}: trazione uniforme, h − x = h = 1000 mm, k₂ = 1");
                    }
                    else
                    {
                        // Eccentric tension, neutral axis outside the section: εmax/|∇ε| > h along the gradient (≈ y, h ≈ 1000 mm), so x = 0.
                        double[] along = engine.Geometry.Outline.Select(p => (plane.ChiX * p[0] + plane.ChiY * p[1]) / gradient).ToArray();
                        double h = along.Max() - along.Min();
                        Console.WriteLine($"    {band}: χx = {plane.ChiX:E3}, χy = {plane.ChiY:E3}, h lungo il gradiente = {h:R} mm, h − x della fascia = {depth.Value:R} mm, k₂ della fascia = {bandK2.Value:R}");
                        Assert(Math.Abs(h - 1000) < 1e-4 && strains.Max() / gradient > h && Math.Abs(depth.Value!.Value - h) <= 1e-12 * h && depth.Expression == "min[εmax/|∇ε|; h lungo il gradiente]"
                            && bandK2.Value is > .5 and < 1, $"{label}, {band}: h − x = min(εmax/|∇ε| = {strains.Max() / gradient:0} mm; {h:0.###} mm), k₂ della fascia dalle deformazioni");
                    }
                    if (code == "NTC 2018")
                    {
                        // Hand calculation of the band width (Circolare C4.1.2.2.4.5 with the Δsm path of the code), inputs from the band trace.
                        double B(string symbol) => r.Details.Single(d => d.Symbol == band + " · " + symbol).Value!.Value;
                        double sigma = B("σs (formula)"), es = B("Es"), ecm = B("Ecm"), fct = B("fct,eff = fctm"), rho = B("ρp,eff"), phi = B("Øeq (formula)"), c = B("c (formula)"), s = B("s (formula)");
                        double strain = Math.Max((sigma - .4 * fct / rho * (1 + es / ecm * rho)) / es, .6 * sigma / es);
                        double near = (3.4 * c + .8 * bandK2.Value!.Value * .425 * phi / rho) / 1.7, distance = s <= 5 * (c + phi / 2) ? near : Math.Max(near, .75 * depth.Value!.Value);
                        Assert(B("h − x (formula)") == depth.Value && Math.Abs(B("wk") - 1.7 * distance * strain) <= 1e-12 * B("wk"), $"{label}, {band}: wk = {B("wk"):0.0000} mm come il calcolo a mano con h − x = {depth.Value:0.###} mm");
                    }
                }
            }
        }

        // (k) Tensile depth h − x of the inner bands, R15 (decision of the user of 7/10/2026: "Altezza lungo il gradiente"): h − x = min[εmax/|∇ε|;
        // h of the section along the gradient]; with |∇ε|·h ≤ 1e-4 εmax uniform tension, h − x = h of the section normal to the face, k₂ = 1.
        // Interaxis 300 mm > 5 (c + Ø/2), so that h − x enters wk (NTC Δsm,distante, EN sr,max = 1,3 (h − x)). Sections: box 1000 × 1400 with a
        // hole 600 × 1000 (N = 1500 kN), the square box and the ring of (j) (N = 3000 kN); NTC 2018 and EN 1992-1-1.
        // 1. Continuity where the neutral axis enters the section (moment about x, about y for the box 1000 × 1400, along (0,6; 0,8)), bisected to
        //    1e-6 kNm. On both sides h − x of each band is εmax/|∇ε| of the band, within the height along the gradient and without the entry
        //    "h − x della fascia" (inside the section: the rule before R15). Two pairs of states: the solver states at the ends of the bisection,
        //    whose planes differ by the convergence noise of the section solver (up to ≈ 3e-5 relative on ε at the centroid in straight bending,
        //    ≈ 1e-8 with the oblique moment), so h − x and wk agree within 1e-3 there; and two planes prescribed from the last fully tensioned one,
        //    moved so that the most compressed vertex has ε = ±|∇ε|·1e-6 mm (bar stresses from the plane, CheckerSection.DescribeStress), where h − x
        //    and wk of each band agree within 1e-6 relative. The intermediate rule of 76a2062 (h normal to the face in a fully tensioned section,
        //    withdrawn) jumped here: square box with the oblique moment, +x band 1000 → 1183,68 mm, section wk +18,4 %. The outer faces change
        //    regime there (fully tensioned ↔ bending), so the section wk may jump (ring: 5,45 → 3,08 mm).
        // 2. Residual jump at the uniform-tension threshold, pinned: below it h − x = h normal to the face, above it h along the gradient, and wk of
        //    the band jumps by the ratio of the two heights (box 1000 × 1400 with the moment about x: ±x bands 1000 → 1400 mm, +40 %; about y: ±y
        //    bands 1400 → 1000 mm; square box with the oblique moment: every band 1000 mm → h along the gradient; ring: diameter → width of the
        //    polygon along the gradient, the polygon effect only).
        {
            var rect = (JsonObject)input.DeepClone(); rect["width_mm"] = "1000"; rect["height_mm"] = "1400"; rect["foro_presente"] = true; rect["inner_width_mm"] = "600"; rect["inner_height_mm"] = "1000";
            var rectBars = new List<(double X, double Y, double Phi)>();
            foreach (var x in new[] { -400d, -200, 0, 200, 400 }) { rectBars.Add((x, -640, 20)); rectBars.Add((x, 640, 20)); }
            foreach (var x in new[] { -250d, 0, 250 }) { rectBars.Add((x, -540, 12)); rectBars.Add((x, 540, 12)); }
            foreach (var y in new[] { -400d, -200, 0, 200, 400 }) { rectBars.Add((-440, y, 16)); rectBars.Add((440, y, 16)); rectBars.Add((-340, y, 12)); rectBars.Add((340, y, 12)); }
            rect["barre_manuali"] = Bars(rectBars.ToArray());
            var sq = (JsonObject)input.DeepClone(); sq["width_mm"] = "1000"; sq["height_mm"] = "1000"; sq["foro_presente"] = true; sq["inner_width_mm"] = "600"; sq["inner_height_mm"] = "600";
            var sqBars = new List<(double X, double Y, double Phi)>();
            foreach (var x in new[] { -400d, -200, 0, 200, 400 }) { sqBars.Add((x, -440, 20)); sqBars.Add((x, 440, 20)); }
            foreach (var x in new[] { -250d, 0, 250 }) { sqBars.Add((x, -340, 12)); sqBars.Add((x, 340, 12)); }
            foreach (var y in new[] { -200d, 0, 200 }) { sqBars.Add((-440, y, 16)); sqBars.Add((440, y, 16)); sqBars.Add((-340, y, 12)); sqBars.Add((340, y, 12)); }
            sq["barre_manuali"] = Bars(sqBars.ToArray());
            var ring = (JsonObject)input.DeepClone(); ring["shape"] = "Circolare"; ring["diameter_mm"] = "1000"; ring["foro_presente"] = true; ring["inner_diameter_mm"] = "600";
            var ringBars = new List<(double X, double Y, double Phi)>();
            for (int k = 0; k < 16; k++) ringBars.Add((Math.Round(440 * Math.Cos(2 * Math.PI * k / 16), 6), Math.Round(440 * Math.Sin(2 * Math.PI * k / 16), 6), 20));
            for (int k = 0; k < 8; k++) ringBars.Add((Math.Round(340 * Math.Cos(2 * Math.PI * (k + .5) / 8), 6), Math.Round(340 * Math.Sin(2 * Math.PI * (k + .5) / 8), 6), 12));
            ring["barre_manuali"] = Bars(ringBars.ToArray());
            var options = (JsonObject)sle.DeepClone(); options["spaziatura_fessure"] = "300";
            // Data, name, N (kN), half sizes of the hole (mm), heights of the section normal to the ±x and ±y faces (mm).
            var sections = new (JsonObject Data, string Name, double N, double Hw, double Hh, double B, double H)[]
                { (rect, "cassone 1000×1400", 1500, 300, 500, 1000, 1400), (sq, "cassone 1000×1000", 3000, 300, 300, 1000, 1000), (ring, "anello Ø1000", 3000, 0, 0, 1000, 1000) };
            var engines = new Dictionary<string, CheckerSection>();
            (Ntc2018Checks.CrackResult R, CheckerStressState S, CheckerSection E) Run((JsonObject Data, string Name, double N, double Hw, double Hh, double B, double H) section, string code,
                double m, double ux, double uy)
            {
                var w = (JsonObject)settings.DeepClone(); w["normativa"] = code;
                if (!engines.TryGetValue(section.Name + code, out var engine)) engines[section.Name + code] = engine = new CheckerSection(section.Data, w, options, engine: SleEngine.Selected);
                var action = new ActionPoint(section.N, sign * ux * m, sign * uy * m); var state = engine.Stress(action, "SLE_QP");
                return (ConcreteServiceabilityAdapter.Cracking(engine, state, action, section.Data, w, options, "SLE_QP", SleEngine.Selected), state, engine);
            }
            double Gradient(CheckerStressState state) => double.Hypot(state.Native.StrainPlane.ChiX, state.Native.StrainPlane.ChiY);
            double[] Strains((Ntc2018Checks.CrackResult R, CheckerStressState S, CheckerSection E) run) => run.E.Geometry.Outline.Select(p => run.S.Native.StrainPlane.GetStrain(p[0], p[1])).ToArray();
            double Along((Ntc2018Checks.CrackResult R, CheckerStressState S, CheckerSection E) run)
            {
                var plane = run.S.Native.StrainPlane; double gradient = Gradient(run.S);
                double[] along = run.E.Geometry.Outline.Select(p => (plane.ChiX * p[0] + plane.ChiY * p[1]) / gradient).ToArray();
                return along.Max() - along.Min();
            }
            double Ratio((Ntc2018Checks.CrackResult R, CheckerStressState S, CheckerSection E) run) => Gradient(run.S) * Along(run) / Strains(run).Max();
            CrackCalculationDetail? Entry(Ntc2018Checks.CrackResult r, string band, string symbol) => r.Details.SingleOrDefault(d => d.Symbol == band + " · " + symbol);
            string[] BandsOf(string name) => name.StartsWith("anello") ? ["Anello interno"] : ["Parete interna +x", "Parete interna −x", "Parete interna +y", "Parete interna −y"];
            // h − x used in wk: NTC "h − x (formula)", EN sr,max/1,3.
            double Used((Ntc2018Checks.CrackResult R, CheckerStressState S, CheckerSection E) run, string band, string code)
                => code == "NTC 2018" ? Entry(run.R, band, "h − x (formula)")!.Value!.Value : Entry(run.R, band, "sr,max")!.Value!.Value / 1.3;
            // εmax/|∇ε| of a wall band by hand: its corners, hole face at the half size of the hole, depth hc,eff, along the whole hole face.
            double Hand((Ntc2018Checks.CrackResult R, CheckerStressState S, CheckerSection E) run, string band, double hw, double hh)
            {
                var plane = run.S.Native.StrainPlane; double q = band.Contains('+') ? 1 : -1, hc = Entry(run.R, band, "hc,eff")!.Value!.Value;
                var corners = band.EndsWith("x") ? new[] { hw, hw + hc }.SelectMany(a => new[] { -hh, hh }.Select(b => (X: q * a, Y: b)))
                    : new[] { hh, hh + hc }.SelectMany(a => new[] { -hw, hw }.Select(b => (X: b, Y: q * a)));
                return corners.Max(p => plane.GetStrain(p.X, p.Y)) / Gradient(run.S);
            }
            string[] codes = ["NTC 2018", "EN 1992-1-1"];
            // Stresses of a prescribed plane, as CheckerSection.Stress describes those of the solver (private adapter, reached by reflection in this check only).
            var describe = typeof(CheckerSection).GetMethod("DescribeStress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            // 1. Entry of the neutral axis.
            int bandsChecked = 0;
            foreach (var section in sections)
                foreach (var (direction, ux, uy) in new[] { ("x", 1d, 0d), ("y", 0d, 1d), ("(0,6; 0,8)", .6, .8) })
                {
                    if (direction == "y" && section.Name != "cassone 1000×1400") continue;
                    foreach (var code in codes)
                    {
                        string label = $"(k) {section.Name} {code}, M lungo {direction}";
                        double lo = 0, hi = 500; var tensile = Run(section, code, lo, ux, uy); var bent = Run(section, code, hi, ux, uy);
                        while (Strains(bent).Min() >= 0 && hi < 64000) { lo = hi; tensile = bent; hi *= 2; bent = Run(section, code, hi, ux, uy); }
                        Assert(Strains(tensile).Min() >= 0 && Strains(bent).Min() < 0, $"{label}: l'asse neutro entra nella sezione fra {lo} e {hi} kNm");
                        while (hi - lo > 1e-6)
                        {
                            double mid = (lo + hi) / 2; var run = Run(section, code, mid, ux, uy);
                            if (Strains(run).Min() < 0) { hi = mid; bent = run; } else { lo = mid; tensile = run; }
                        }
                        Assert(!Bending(tensile.R) && Bending(bent.R), $"{label}: sezione interamente tesa a {lo:F6} kNm, inflessa a {hi:F6} kNm");
                        var plane = tensile.S.Native.StrainPlane; double gradient = Gradient(tensile.S);
                        var corner = tensile.E.Geometry.Outline.MinBy(p => plane.GetStrain(p[0], p[1]))!;
                        (Ntc2018Checks.CrackResult R, CheckerStressState S, CheckerSection E) Prescribed(double offset)
                        {
                            var native = tensile.S.Native;
                            var moved = new GPC.Checkers.Concrete.Results.StrainPlane(plane.ChiX, plane.ChiY, new GPC.Geometry.Point2d(corner[0], corner[1]), offset * gradient);
                            var result = new GPC.Checkers.Concrete.Results.StressAnalysisResult(native.ConcreteSection, native.Force, moved, native.SectionSolver, native.Standard,
                                native.LinearElasticAnalysis, native.PsiRebar, native.PsiTendon);
                            var state = (CheckerStressState)describe.Invoke(tensile.E, [result, "SLE_QP"])!;
                            var w = (JsonObject)settings.DeepClone(); w["normativa"] = code;
                            return (ConcreteServiceabilityAdapter.Cracking(tensile.E, state, new ActionPoint(section.N, sign * ux * lo, sign * uy * lo), section.Data, w, options, "SLE_QP", SleEngine.Selected), state, tensile.E);
                        }
                        var outside = Prescribed(1e-6); var inside = Prescribed(-1e-6);
                        Assert(!Bending(outside.R) && Strains(outside).Min() > 0 && Bending(inside.R), $"{label}: piani prescritti con l'asse neutro 1e-6 mm fuori e dentro");
                        Console.WriteLine($"{label}: ingresso dell'asse neutro M in [{lo:F6}; {hi:F6}] kNm, h lungo il gradiente {Along(tensile):G8} mm, wk della sezione {tensile.R.Width:G8} → {bent.R.Width:G8} mm (piani prescritti {outside.R.Width:G8} → {inside.R.Width:G8} mm)");
                        foreach (var band in BandsOf(section.Name))
                        {
                            foreach (var (pair, a, b, tolerance) in new[] { ("solutore", tensile, bent, 1e-3), ("piani prescritti", outside, inside, 1e-6) })
                            {
                                double usedOut = Used(a, band, code), usedIn = Used(b, band, code);
                                double wOut = Entry(a.R, band, "wk")!.Value!.Value, wIn = Entry(b.R, band, "wk")!.Value!.Value;
                                Console.WriteLine($"    {band}, {pair}: h − x = {usedOut:R} → {usedIn:R} mm ({usedIn / usedOut - 1:+0.0E+0;-0.0E+0}), wk = {wOut:R} → {wIn:R} mm ({wIn / wOut - 1:+0.0E+0;-0.0E+0})");
                                Assert(Entry(a.R, band, "h − x della fascia") == null && Entry(b.R, band, "h − x della fascia") == null && usedOut <= Along(a) && usedIn <= Along(b),
                                    $"{label}, {band}, {pair}: h − x entro l'altezza lungo il gradiente, senza voce ai due lati dell'ingresso");
                                if (!section.Name.StartsWith("anello"))
                                {
                                    double handOut = Hand(a, band, section.Hw, section.Hh), handIn = Hand(b, band, section.Hw, section.Hh);
                                    Assert(Math.Abs(usedOut - handOut) <= 1e-9 * handOut && Math.Abs(usedIn - handIn) <= 1e-9 * handIn,
                                        $"{label}, {band}, {pair}: h − x = εmax/|∇ε| della fascia ai due lati ({handOut:0.###} / {handIn:0.###} mm; dentro la regola prima di R15)");
                                }
                                Assert(Math.Abs(usedIn - usedOut) <= tolerance * usedOut && Math.Abs(wIn - wOut) <= tolerance * wOut,
                                    $"{label}, {band}, {pair}: h − x e wk continui all'ingresso dell'asse neutro entro {tolerance:E0} ({usedOut:G10} / {usedIn:G10} mm, {wOut:G10} / {wIn:G10} mm)");
                            }
                            bandsChecked++;
                        }
                    }
                }
            Assert(bandsChecked == 2 * (3 * 4 + 2 * 4 + 2 * 1), $"(k) fasce controllate all'ingresso dell'asse neutro: {bandsChecked}");

            // 2. Uniform-tension threshold: calibration with 10 kNm (|∇ε|·h/εmax linear in M for the cracked section in tension), then 0,5 and 2 times
            // the threshold.
            int jumps = 0;
            foreach (var (section, direction, ux, uy) in new[] { (sections[0], "x", 1d, 0d), (sections[0], "y", 0d, 1d), (sections[1], "(0,6; 0,8)", .6, .8), (sections[2], "x", 1d, 0d) })
                foreach (var code in codes)
                {
                    double r0 = Ratio(Run(section, code, 10, ux, uy));
                    double mBelow = 10 * .5e-4 / r0, mAbove = 10 * 2e-4 / r0;
                    var below = Run(section, code, mBelow, ux, uy); var above = Run(section, code, mAbove, ux, uy);
                    string label = $"(k) soglia, {section.Name} {code}, M lungo {direction}";
                    double along = Along(above);
                    Console.WriteLine($"{label}: M = {mBelow:G4} / {mAbove:G4} kNm, |∇ε|·h/εmax = {Ratio(below):E3} / {Ratio(above):E3}, h lungo il gradiente {along:G8} mm, wk = {below.R.Width:G8} / {above.R.Width:G8} mm");
                    Assert(Strains(below).Min() > 0 && Strains(above).Min() > 0 && Ratio(below) < 1e-4 && Ratio(above) > 1e-4 && Ratio(above) < 4e-4,
                        $"{label}: sezione interamente tesa, eccentricità a cavallo della soglia ({Ratio(below):E3}, {Ratio(above):E3})");
                    foreach (var band in BandsOf(section.Name))
                    {
                        double normal = section.Name.StartsWith("anello") ? 1000 : band.EndsWith("x") ? section.B : section.H;
                        var d0 = Entry(below.R, band, "h − x della fascia"); var d1 = Entry(above.R, band, "h − x della fascia");
                        var k0 = Entry(below.R, band, "k₂ della fascia"); var k1 = Entry(above.R, band, "k₂ della fascia");
                        double w0 = Entry(below.R, band, "wk")!.Value!.Value, w1 = Entry(above.R, band, "wk")!.Value!.Value;
                        Console.WriteLine($"    {band}: h − x = {d0?.Value:G8} / {d1?.Value:G8} mm, k₂ = {k0?.Value:R} / {k1?.Value:R}, wk = {w0:G8} / {w1:G8} mm ({w1 / w0 - 1:P2})");
                        Assert(d0 != null && d0.Value == normal && d0.Expression == "Trazione uniforme: h della sezione normale alla faccia" && k0!.Value == 1 && Math.Abs(Used(below, band, code) - normal) <= 1e-9 * normal,
                            $"{label}, {band}: sotto la soglia trazione uniforme, h − x = {normal} mm, k₂ = 1");
                        Assert(d1 != null && Math.Abs(d1.Value!.Value - along) <= 1e-9 * along && d1.Expression == "min[εmax/|∇ε|; h lungo il gradiente]" && k1!.Value is > 1 - 2e-4 and < 1
                                && Math.Abs(Used(above, band, code) - d1.Value.Value) <= 1e-9 * along,
                            $"{label}, {band}: sopra la soglia h − x = min(εmax/|∇ε|; h lungo il gradiente) = {along:0.###} mm");
                        // The far crack spacing governs: wk jumps by the ratio of the heights (σs and k₂ move by 1e-4 at most).
                        Assert(Math.Abs(w1 / w0 / (along / normal) - 1) <= 1e-3, $"{label}, {band}: wk {w0:G6} → {w1:G6} mm, rapporto delle altezze {along / normal:G6}");
                        if (!section.Name.StartsWith("anello") && Math.Abs(along - normal) > 1) jumps++;
                        if (section.Name.StartsWith("anello")) Assert(along <= 1000 + 1e-9 && along > 1000 * Math.Cos(Math.PI / 16), $"{label}: altezza del poligono lungo il gradiente {along:G8} mm");
                    }
                }
            // Bands with a jump: box 1000 × 1400 about x (±x) and about y (±y), square box with the oblique moment (4 bands), for NTC and EN.
            Assert(jumps == 2 * (2 + 2 + 4), $"(k) fasce con salto alla soglia della trazione uniforme: {jumps}");
        }
        Console.WriteLine($"k2 D7-b: {passed} controlli superati."); return passed;
    }
}
