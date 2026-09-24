using System.Text.Json.Nodes;
using X.Core;

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
        var linear = new CheckerSection(input, settings, sle).Stress(new(-500, 50, -30), "SLE");
        Assert(linear.sigma_cls < 0, "Compressione negativa nelle tensioni");
        Assert(linear.ConcreteCompressionStrength > 0 && linear.ConcreteTensionStrength > 0 && linear.BarStrengths.All(v => v > 0), "Scale contouring positive da materiali Checker");
        Assert(Math.Abs(linear.ConcreteStressLimit!.Value - 15) < 1e-12, "Limite SLE espresso in valore assoluto");
        Assert(linear.FiberStrains.Length == linear.FiberStresses.Length && linear.FiberStrains.All(double.IsFinite), "Deformazioni contouring native finite");
        Assert(linear.Response is { CMin: < 0, UsefulDepth: > 0, PMin: null }, "Riepilogo nativo completo senza trefoli inventati");
        var compression = new CheckerSection(input,settings,sle).Stress(new(-500,0,0),"SLE_QP");
        input["gettato_sottile"]="Sì";
        var thinCompression = new CheckerSection(input,settings,sle).Stress(new(-500,0,0),"SLE_QP");
        Assert(Math.Abs(thinCompression.Ratio!.Value/compression.Ratio!.Value-1.25)<1e-10,"Riduzione limite tensionale elemento piano sottile");
        input["gettato_sottile"]="No";
        Assert(Math.Abs(linear.sigma_cls + 11.04) / 11.04 < .05, "VCA_N_1 CLS");
        var expected = new[] { -131.6, -44.86, -33.25, 53.53 }; var actualBars = linear.tensioni_barre.Order().ToArray();
        for (int i=0;i<4;i++) Assert(Math.Abs(actualBars[i]-expected[i])/Math.Abs(expected[i]) < .05, "VCA_N_1 barra " + i);
        sle["modello"] = "Non lineare"; sle["phi"]="0";
        var nonlin = new CheckerSection(input, settings, sle).Stress(new(-500,50,-30), "SLE");
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
        Assert(Ntc2018Checks.CrackK2([-1, 100, 200]) == .5, "k2 flessione con una barra compressa");
        Assert(Ntc2018Checks.CrackK2([100, 150, 200]) == 1, "k2 trazione con tutte le barre tese");
        Assert(Ntc2018Checks.CrackK2([0, 100]) == 1 && Ntc2018Checks.CrackK2([-1e-12, 100]) == .5, "k2: zero non compresso, segno negativo rispettato");
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
        var otherNorm=(JsonObject)settings.DeepClone();otherNorm["normativa"]="EC2";bool invalidNorm=false;
        try { _=new CheckerSection(input,otherNorm,options); } catch(ArgumentException) { invalidNorm=true; }
        Assert(invalidNorm,"Normativa fuori campo non etichettata NTC");
        var shear=Ntc2018Checks.Shear(-300,100,150000,300,450,1000,30,17,450/1.15,1.5,2*Math.PI*100/4,150,90,2);
        Assert(Math.Abs(shear.VRsd-331.9160934010086)<1e-9 && Math.Abs(shear.VRcd-461.7)<1e-9,"Taglio: confronto indipendente NTC");
        Assert(Ntc2018Checks.Shear(300,100,150000,300,450,1000,30,17,450/1.15,1.5,0,150,90).Ratio is null,"Trazione senza staffe non migliora la resistenza");
        Assert(Ntc2018Checks.Shear(-3000,100,150000,300,450,1000,30,17,450/1.15,1.5,157,150,90).Ratio is null,"Compressione oltre fcd non produce falso pass");
        sle["modello"]="Lineare"; sle["esposizione"]="XC1"; sle["spaziatura_fessure"]="200";
        var crackEngine=new CheckerSection(input,settings,sle); var crackAction=new ActionPoint(-100,50,0); var crackStress=crackEngine.Stress(crackAction,"SLE_QP");
        var crack=Ntc2018Checks.Cracking(crackEngine,crackStress,crackAction,input,settings,sle,"SLE_QP");
        Assert(crack.Details.Any(d => d.Symbol == "hc,eff") && crack.Details.Any(d => d.Symbol == "Criterio k₂") && crack.Details.Any(d => d.Symbol.StartsWith("B") && d.Symbol.EndsWith(" · σs")), "Traccia geometria, assunzioni e tensioni delle singole barre");
        Assert(J.Node(crack)?["Details"] is JsonArray { Count: > 30 }, "Passaggi esportati in JSON");
        var compact = CrackCalculationSummary.Values(crack);
        Assert(compact.Length == CrackCalculationSummary.MaxValues && compact.Select(d => d.Symbol).Distinct().Count() == compact.Length, "Riepilogo limitato a 30 valori senza duplicati");
        Assert(compact.Single(d => d.Symbol == "wk").Value == crack.Width && compact.Single(d => d.Symbol == "ηw").Value == crack.Ratio, "Riepilogo conserva risultati non arrotondati");
        Assert(!CrackCalculationSummary.Format(crack).Contains("B01 ·") && compact.Any(d => d.Symbol == "k₂"), "Riepilogo senza singole barre, con coefficienti normativi");
        var restoredCrack = System.Text.Json.JsonSerializer.Deserialize<Ntc2018Checks.CrackResult>(J.Node(crack)!.ToJsonString())!;
        Assert(CrackCalculationSummary.Values(restoredCrack).SequenceEqual(compact), "Riepilogo report identico dopo round trip JSON");
        Assert(crack.Width is >0 && crack.EffectiveArea is >0 && crack.Ratio is >0,"Fessurazione da tensioni native e mesh tagliata");
        Assert(crack.Details.Single(d => d.Symbol == "Criterio k₂").Value == Ntc2018Checks.CrackK2(crackStress.tensioni_barre), "k2 scelto da tutte le barre del risultato nativo");
        foreach (var force in new[] { new ActionPoint(100, 0, 0), new ActionPoint(100, 10, 0), new ActionPoint(200, 20, 0), new ActionPoint(-100, 50, 0) })
        {
            var nativeState = crackEngine.Stress(force, "SLE_QP");
            var checkedCrack = Ntc2018Checks.Cracking(crackEngine, nativeState, force, input, settings, sle, "SLE_QP");
            double expectedK2 = Ntc2018Checks.CrackK2(nativeState.tensioni_barre);
            var eps=nativeState.ConcreteVertices.Select(v=>v.Strain).ToArray();
            if(eps.Min()>=0&&eps.Max()>0)expectedK2=(eps.Min()+eps.Max())/(2*eps.Max());
            Assert(checkedCrack.Details.Single(d => d.Symbol == "Criterio k₂").Value == expectedK2, "Criterio k2 anche per trazione pura e pressoflessione");
            if (checkedCrack.Width is > 0)
                Assert(checkedCrack.Details.Single(d => d.Symbol == "k₂").Value == expectedK2, "Il coefficiente selezionato entra nella formula wk");
        }
        var strainPlane=crackStress.Native.StrainPlane;
        double depth=crackEngine.Section.Shape.GetPoints2d().Max(p=>strainPlane.GetStrain(p))/Math.Abs(strainPlane.ChiY);
        double effectiveHeight=Math.Min(125,Math.Min(depth/3,250));
        Assert(Math.Abs(crack.EffectiveArea!.Value-300*effectiveHeight)/(300*effectiveHeight)<.01,"Ac,eff rettangolare confronto analitico");
        Assert(Math.Abs(crack.EffectiveSteel!.Value-2*Math.PI*81)<1e-6,"As,eff solo barre tese nella fascia");
        foreach(var exposure in new[]{"XC4","XD3"})
        {
            sle["esposizione"]=exposure;sle["sensibilita"]="Sensibile";
            var result=Ntc2018Checks.Cracking(crackEngine,crackStress,crackAction,input,settings,sle,exposure=="XC4"?"SLE_QP":"SLE_FREQ");
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
