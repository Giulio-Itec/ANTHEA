using System.Text.Json.Nodes;
using GPC.Checkers.CompositeBridge.History;
using X.Core;

/// <summary>Regression of the ANTHEA archive boundary for all three bridge methods.
/// Elastic reference: exact rectangular areas and parallel-axis theorem, no production solver.</summary>
internal static class BridgeMethodChecks
{
    internal static void Run()
    {
        int count = 0;
        string caseId = "";
        var evidence = new List<object>();
        void Check(bool ok, string name) { if (!ok) throw new Exception("Metodi ponti: " + name); count++; }
        void Near(double actual, double expected, double tolerance, string name)
        {
            Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance, $"{name}: {actual:R} / {expected:R}");
            evidence.Add(new { Case = caseId, Name = name, Expected = expected, Actual = actual, Tolerance = tolerance });
        }
        void Reject(Action action, string name)
        {
            try { action(); } catch (ArgumentException) { count++; return; }
            throw new Exception("Ingresso accettato: " + name);
        }
        JsonObject Data(int method)
        {
            var d = BridgeSection.Defaults(); d["metodo_analisi"] = BridgeSection.CalculationMethods[method];
            d["classe4"] = false; d["fibre_anima"] = 32; d["fibre_flange"] = 4;
            d["fibre_cls"] = 16; d["sottopassi"] = 4;
            return d;
        }
        void Equilibrium(HistoryStageResult s)
        {
            var f = s.Fibers.Where(x => x.Active).ToArray();
            Near(f.Sum(x => x.Stress * x.EffectiveArea), s.N, .01, "equilibrio N integrato");
            Near(-f.Sum(x => x.Stress * x.EffectiveArea * x.Fiber.Y), s.MomentAtOrigin, 10, "equilibrio M integrato");
        }
        // Default welded H: top 500x25, web 14x1800, bottom 700x30, y=0 at steel top.
        var rectangles = new[] { (b: 500d, h: 25d, y: -12.5), (b: 14d, h: 1800d, y: -925d), (b: 700d, h: 30d, y: -1840d) };
        double area = rectangles.Sum(r => r.b*r.h), yc = rectangles.Sum(r => r.b*r.h*r.y)/area;
        double inertia = rectangles.Sum(r => r.b*Math.Pow(r.h,3)/12 + r.b*r.h*Math.Pow(r.y-yc,2));
        const double elasticModulus = 210000;
        foreach (int method in new[] { 0, 1, 2 })
        foreach (var load in new[] { (n: -200d, m: 100d), (n: 150d, m: -80d) })
        {
            caseId = $"EL{method}-{(load.n < 0 ? 1 : 2)}";
            var d = Data(method);
            d["fasi"] = new JsonArray(BridgeSection.Phase("Carico", "Solo acciaio", load.n, load.m));
            string before = d.ToJsonString(); var result = BridgeSection.Calculate(d); var stage = result.Stages.Single();
            Check(before == d.ToJsonString(), "calcolo senza mutazione degli ingressi");
            // Input moment at y=0: transfer to centroid M_c=M_0+N*y_c.
            double curvature = (load.m*1e6 + load.n*1000*yc)/(elasticModulus*inertia);
            double axial = load.n*1000/(elasticModulus*area) + curvature*yc;
            if (stage.GetHistory() is { } history)
            {
                Check(history.Nonlinear == (method == 2), "dispatch storico lineare/non lineare");
                Near(history.State.TotalPlane.AxialStrain, axial, 1e-10, "deformazione assiale analitica");
                Near(history.State.TotalPlane.Curvature, curvature, 1e-13, "curvatura analitica");
                Equilibrium(history.State);
                foreach (var f in history.State.Fibers.Where(f => f.Active))
                    Near(f.Stress, elasticModulus*(axial-curvature*f.Fiber.Y), 1e-5, "tensione elastica della fibra");
                Check(stage.Shear is null, "storico non certifica taglio");
                if (method == 2) Check(stage.Points.All(p => p.Utilization is null), "non lineare senza esito SLU elastico");
                Check(result.Json()["Stages"]![0]!["History"]!["Fibers"]!.AsArray().Count > 0, "esportazione fibre storiche");
            }
            else
            {
                Check(method == 0, "dispatch cumulativo");
                foreach (var p in stage.Points.Where(p => p.Active))
                    Near(p.Stress, elasticModulus*(axial-curvature*p.Y), 1e-5, "tensione cumulativa analitica");
            }
        }
        foreach (int method in new[] { 1, 2 })
        foreach (double moment in new[] { 100d, -80d })
        {
            caseId = $"GET{method}-{(moment > 0 ? 1 : 2)}";
            var d = Data(method);
            d["fasi"] = new JsonArray(BridgeSection.Phase("Prima del getto", "Solo acciaio", m: moment),
                BridgeSection.Phase("Getto senza incremento", "Composta"));
            var stages = BridgeSection.Calculate(d).Stages;
            var first = stages[0].GetHistory()!.State; var cast = stages[1].GetHistory()!.State;
            foreach (var f in cast.Fibers.Where(f => f.ComponentId is HBridgeHistoryAnalysis.Concrete or HBridgeHistoryAnalysis.Rebars))
            {
                Check(f.Active, "attivazione soletta/armatura");
                Near(f.ActivationStrain, first.TotalPlane.At(f.Fiber.Y), 1e-12, "memoria della deformazione di getto");
                Near(f.Stress, 0, 1e-6, "getto senza tensione pregressa");
            }
            Equilibrium(cast);
            // Elastic unloading must return a steel-only section to zero, both history laws.
            caseId = $"SC{method}-{(moment > 0 ? 1 : 2)}";
            d["fasi"] = new JsonArray(BridgeSection.Phase("Carico", "Solo acciaio", m: moment),
                BridgeSection.Phase("Scarico", "Solo acciaio", m: -moment));
            var unloaded = BridgeSection.Calculate(d).Stages.Last().GetHistory()!.State;
            Near(unloaded.Fibers.Max(f => Math.Abs(f.Stress)), 0, 1e-6, "scarico elastico completo");
            Equilibrium(unloaded);
        }
        foreach (int method in new[] { 1, 2 })
        {
            caseId = $"RIT-{method}";
            var d = Data(method); var phases = new JsonArray();
            for (int i = 0; i < 24; i++) { var p = BridgeSection.ShrinkagePhase(); p["phi"] = 0; p["epsilon_cs"] = -1; phases.Add(p); }
            d["fasi"] = phases;
            var r = BridgeSection.Calculate(d); Check(r.Stages.Count == 24, "storico oltre venti fasi");
            var last = r.Stages.Last().GetHistory()!.State;
            foreach (var f in last.Fibers.Where(f => f.ComponentId == HBridgeHistoryAnalysis.Concrete))
                Near(f.ImposedStrain, -24e-6, 1e-12, "ritiro incrementale cumulato");
            Equilibrium(last);
        }
        // Two examples per response control, starting from steel-only history; exact elastic solution.
        foreach (bool bending in new[] { true, false })
        foreach (int sign in new[] { -1, 1 })
        {
            caseId = $"{(bending ? "MK" : "NE")}-{(sign < 0 ? 1 : 2)}";
            var d = Data(2); d["classe4"] = true; // Adapter must explicitly disable class 4 for response curves.
            d["fasi"] = new JsonArray(BridgeSection.Phase("Origine", "Solo acciaio"));
            var q = BridgeSection.ResponseDefaults(); q["origine"] = BridgeSection.ResponseOrigins[1]; q["fase"] = 0;
            q["tipo"] = BridgeSection.ResponseModes[bending ? 0 : 1]; q["punti"] = 4; q["sottopassi"] = 2;
            q["y"] = yc; q["n_storico"] = false; q["N"] = -50; q["k_storico"] = false; q["k"] = 0;
            q["incremento_k"] = sign*.00001; q["incremento_e"] = sign*10;
            string before = d.ToJsonString(), request = q.ToJsonString();
            var curve = BridgeSection.CalculateResponse(d, q);
            Check(curve.Completed && curve.Points.Count == 5, "curva completa con stato iniziale");
            Check(before == d.ToJsonString() && request == q.ToJsonString(), "curve non modificano ingressi");
            foreach (var point in curve.Points)
            {
                Equilibrium(point.State);
                if (bending)
                {
                    Near(point.State.N, -50000, .01, "N costante kN convertiti in N");
                    Near(point.MomentAtReference, elasticModulus*inertia*point.State.TotalPlane.Curvature, 10, "M=EIκ al baricentro");
                }
                else
                {
                    Near(point.State.TotalPlane.Curvature, 0, 1e-12, "curvatura imposta");
                    Near(point.State.N, elasticModulus*area*point.ReferenceStrain, .01, "N=EAε");
                }
            }
            Near(bending ? curve.Points.Last().State.TotalPlane.Curvature : curve.Points.Last().ReferenceStrain,
                sign*(bending ? 1e-8 : 1e-5), 1e-12, "conversione incremento 1/m o microdeformazioni");
            string csv = BridgeSection.ResponseCsv(curve);
            Check(csv.Contains("curvatura_1_mm") && csv.Contains("epsilon_plastica"), "CSV con unità e memoria plastica");
            Check(csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 1+curve.Points.Sum(p => p.State.Fibers.Count), "CSV completo");
        }
        // Post-yield reference, uniform steel tension with an explicitly perfect-plastic law.
        // sigma=fy, epsilon_p=epsilon-fy/E; this also checks runtime state serialization in CSV.
        foreach (double microstrain in new[] { 3000d, 4000d })
        {
            caseId = $"PL-{(microstrain == 3000 ? 1 : 2)}";
            var d = Data(2); d["fy_override"] = true; d["fy"] = 235;
            d["fasi"] = new JsonArray(BridgeSection.Phase("Origine", "Solo acciaio"));
            var q = BridgeSection.ResponseDefaults(); q["origine"] = BridgeSection.ResponseOrigins[1];
            q["tipo"] = BridgeSection.ResponseModes[1]; q["fase"] = 0; q["incremento_e"] = microstrain;
            q["punti"] = 4; q["sottopassi"] = 2; q["k_storico"] = false; q["k"] = 0; q["y"] = yc;
            var curve = BridgeSection.CalculateResponse(d, q);
            Check(curve.Completed, "curva oltre snervamento completata");
            var last = curve.Points.Last().State; Equilibrium(last);
            Near(last.N, 235*area, .01, "plateau N=fy A");
            foreach (var f in last.Fibers.Where(f => f.Active))
            {
                Near(f.Stress, 235, 1e-6, "plateau della fibra");
                Check(f.MaterialState is HistoryPlasticState, "stato plastico presente");
                var plastic = (HistoryPlasticState)f.MaterialState;
                Near(plastic.PlasticStrain, microstrain*1e-6-235/elasticModulus, 1e-10, "deformazione plastica analitica");
                Near(plastic.AccumulatedPlasticStrain, plastic.PlasticStrain, 1e-10, "accumulo monotono");
            }
            Near(curve.Points[0].State.Fibers.Max(f => Math.Abs(f.Stress)), 0, 1e-10, "stato iniziale immutato dopo plasticizzazione");
        }
        // Switching to instantaneous nonlinear analysis must preserve saved creep inputs.
        var instantaneous = Data(2); instantaneous["classe4"] = true;
        string saved = instantaneous.ToJsonString();
        var instantResult = BridgeSection.Calculate(instantaneous);
        Check(saved == instantaneous.ToJsonString(), "non lineare preserva phi e classe 4 salvati");
        Check(instantResult.Stages.All(s => s.GetHistory()!.State.Panels.Count == 0), "non lineare sulla sezione lorda");
        foreach (double phi in new[] { .5, 2d })
        {
            caseId = $"HOM-{(phi == .5 ? 1 : 2)}";
            var d = Data(1); var phase = BridgeSection.Phase("Composta", "Composta", -200, 100, phi);
            d["fasi"] = new JsonArray(phase);
            var a = BridgeSection.Calculate(d).Stages[0].GetHistory()!.State;
            double n = BridgeSection.Homogenization(d, phase).N; phase["modo"] = "Da n"; phase["n"] = n;
            var b = BridgeSection.Calculate(d).Stages[0].GetHistory()!.State;
            Near(a.TotalPlane.AxialStrain, b.TotalPlane.AxialStrain, 1e-12, "storico phi/n equivalente");
            Near(a.TotalPlane.Curvature, b.TotalPlane.Curvature, 1e-14, "storico phi/n curvatura");
            Check(a.Fibers.Count == b.Fibers.Count, "phi/n stessa discretizzazione");
            foreach (var (fa, fb) in a.Fibers.Zip(b.Fibers)) Near(fa.Stress, fb.Stress, 1e-7, "phi/n tensioni");
        }
        var invalid = Data(1); invalid["metodo_analisi"] = "inesistente";
        Reject(() => BridgeSection.Calculate(invalid), "metodo sconosciuto");
        foreach (string key in new[] { "fibre_anima", "fibre_flange", "fibre_cls", "sottopassi" })
        foreach (double value in new[] { 0, 1.5 })
        { var d = Data(1); d[key] = value; Reject(() => BridgeSection.Calculate(d), key); }
        foreach (string key in new[] { "punti", "sottopassi" })
        { var q = BridgeSection.ResponseDefaults(); q[key] = 1.5; Reject(() => BridgeSection.CalculateResponse(Data(2), q), key); }
        var zero = BridgeSection.ResponseDefaults(); zero["incremento_k"] = 0;
        Reject(() => BridgeSection.CalculateResponse(Data(2), zero), "curva senza incremento");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        foreach (int method in new[] { 0, 1, 2 })
        {
            bool cancelled = false;
            try { BridgeSection.Calculate(Data(method), cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "annullamento metodo " + method);
        }
        Console.WriteLine($"Metodi ponti e curve: {count} controlli superati.");
        if (Environment.GetEnvironmentVariable("BRIDGE_METHOD_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(new { Checks = count, Rows = evidence }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
