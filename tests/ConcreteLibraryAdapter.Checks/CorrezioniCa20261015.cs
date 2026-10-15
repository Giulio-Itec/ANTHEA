using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;

/// <summary>
/// Prova 4c: integrazione in ANTHEA delle correzioni lato calcestruzzo delle librerie dell'analisi dei bug del 15/10/2026
/// (docs/refactoring/analisi-bug-20261015.md): SLU-1, SLU-2, SLU-4, FES-1, FES-2, ARM-3, DET-3 e i coefficienti unitari del foglio c.a. del
/// palo in ancoraggi e dettagli; seguiti del 16/10/2026: coefficienti unitari nel primo snervamento del M–χ e punto disegnato del grafico
/// N–M con la proiezione. Attesi indipendenti dallo script attesi/correzioni_ca_20261015.py (modello a fibre, formule di norma) o calcolati a
/// mano, citati accanto a ogni valore; i confronti fra due percorsi della libreria (2D contro 3D) sono dichiarati come tali.
/// </summary>
internal static class CorrezioniCa20261015
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    static JsonObject Workspace() => new() { ["normativa"] = "NTC 2018", ["trefoli"] = new JsonArray() };

    /// <summary>Rettangolo 300×500 C25/30, B450C del catalogo, copriferro 30, staffe Ø8: barre superiori e inferiori della disposizione automatica.</summary>
    static JsonObject Rect(int top, double topDiameter, int bottom, double bottomDiameter)
    {
        var input = SezioneCA.DefaultCatalogInput();
        input["shape"] = "Rettangolare"; input["width_mm"] = "300"; input["height_mm"] = "500";
        input["top_bar_count"] = top.ToString(Invariant); input["top_bar_diameter_mm"] = topDiameter.ToString(Invariant);
        input["bottom_bar_count"] = bottom.ToString(Invariant); input["bottom_bar_diameter_mm"] = bottomDiameter.ToString(Invariant);
        input["side_bar_count_per_side"] = "0"; input["cover_mm"] = "30"; input["transverse_bar_diameter_mm"] = "8";
        input["fck_mpa"] = "25"; input["cls_diagramma"] = "Parabola-rettangolo";
        return input;
    }

    static JsonObject Options2D(double theta) => new()
    {
        ["tipo"] = "N–M", ["theta"] = theta.ToString(Invariant), ["N"] = "0", ["angoli"] = "64", ["assi"] = "Locali", ["proietta"] = "No", ["trazione_cls"] = "No"
    };

    static JsonObject Options3D(string criterion) => new()
    {
        ["criterio"] = criterion, ["assi"] = "Locali", ["strategia"] = "Iterativo", ["angoli"] = "64", ["trazione_cls"] = "No"
    };

    static CheckerDomain3D Domain3D(JsonObject input, string criterion, string state)
    {
        var options = Options3D(criterion);
        var domain = new CheckerSection(input, Workspace(), options, state).Domain3D();
        domain.ConfigureVerification(options);
        return domain;
    }

    public static int Run(Action<bool, string> check)
    {
        int count = 0;
        void Near(double? value, double expected, double relative, string message)
        {
            check(value is double v && double.IsFinite(v) && Math.Abs(v - expected) <= relative * Math.Max(1e-12, Math.Abs(expected)),
                $"{message}: {value?.ToString("G17", Invariant) ?? "null"} invece di {expected.ToString("G17", Invariant)}");
            count++;
        }
        void Is(bool condition, string message) { check(condition, message); count++; }

        // ------------------------------------------------------------ SLU-1: dominio 2D N–M con θ qualsiasi
        // CheckerSection.Domain2D usa sempre il meridiano della direzione del momento (CalculateDomainConstantMomentsRatio). Attesi: η del modello a
        // fibre indipendente (correzioni_ca_20261015.py, NTC 2018 §4.1.2.3.4) entro l'1%, e la verifica 3D a eccentricità costante della libreria
        // (stesso tasso di lavoro, percorso diverso) entro l'1%. Prima del 15/10: θ = 90° dava η 0,0127 «Entro il dominio» con R = 80 604 kN.
        var symmetric = Rect(2, 16, 2, 16);
        var plastic3D = Domain3D(symmetric, "Eccentricità costante", "SLU");
        foreach (var (theta, action, expected) in new (double, ActionPoint, double)[]
        {
            (90, new(-1000, 0, 200), 2.4778), (270, new(-1000, 0, -200), 2.4778), (90, new(-1000, 0, 60), 0.6775),
            (45, new(-1000, 120, 120), 1.5811), (30, new(-500, 100 * Math.Cos(Math.PI / 6), 100 * Math.Sin(Math.PI / 6)), 0.8266),
            (180, new(-1000, -250, 0), 1.3803), (0, new(-1000, 250, 0), 1.3803)
        })
        {
            string tag = $"SLU-1 θ = {theta}° N {action.N} Mx {action.Mx:0.###} My {action.My:0.###}";
            var two = new CheckerSection(symmetric, Workspace(), Options2D(theta), "SLU").Domain2D().Check(action);
            var three = plastic3D.Check(action);
            Near(two.Utilization, expected, .01, tag + ": η 2D contro il modello a fibre");
            Near(two.Utilization, three.Utilization ?? double.NaN, .01, tag + ": η 2D contro la verifica 3D a eccentricità costante");
            Is(two.Status == (expected <= 1 ? "Entro il dominio" : "Fuori dominio"), tag + ": stato «" + two.Status + "»");
            // Segni: NRd di compressione come NEd, momento resistente nel verso dell'azione e senza componente trasversale.
            var r = two.Resistance!.Value;
            double moment = Math.Sqrt(action.Mx * action.Mx + action.My * action.My), along = (r.Mx * action.Mx + r.My * action.My) / moment,
                transverse = (r.Mx * action.My - r.My * action.Mx) / moment;
            Is(r.N < 0 && along > 0 && Math.Abs(transverse) <= 1e-3 * Math.Abs(along), $"{tag}: segni o direzione di Rd ({r.N:0.##}; {r.Mx:0.###}; {r.My:0.###})");
        }
        // Sezione non simmetrica rispetto al piano di flessione (2Ø16 sopra, 3Ø20 sotto), θ = 90°: prima il meridiano dell'asse neutro fisso,
        // con Mx trasversale fino a 42 kNm e i rami rimescolati (η 0,0095); atteso a fibre con l'asse neutro ruotato e Mx = 0: η 1,9673.
        {
            var asymmetric = Rect(2, 16, 3, 20);
            var action = new ActionPoint(-1000, 0, 200);
            var two = new CheckerSection(asymmetric, Workspace(), Options2D(90), "SLU").Domain2D().Check(action);
            Near(two.Utilization, 1.9673, .01, "SLU-1 2Ø16/3Ø20 θ = 90°: η 2D contro il modello a fibre");
            Near(two.Utilization, Domain3D(asymmetric, "Eccentricità costante", "SLU").Check(action).Utilization ?? double.NaN, .01,
                "SLU-1 2Ø16/3Ø20 θ = 90°: η 2D contro la verifica 3D a eccentricità costante");
            Is(Math.Abs(two.Resistance!.Value.Mx) <= 1e-3 * Math.Abs(two.Resistance.Value.My), "SLU-1 2Ø16/3Ø20 θ = 90°: Mx trasversale in Rd");
        }
        // SLV (dominio elastico): il meridiano della direzione del momento ora c'è anche per θ obliqui (prima «sezione del dominio assente»).
        // Nessun modello indipendente del dominio elastico: confronto fra i due percorsi della libreria, entro l'1%.
        {
            var elastic3D = Domain3D(symmetric, "Eccentricità costante", "SLV");
            foreach (var (theta, action) in new (double, ActionPoint)[] { (90, new(-1000, 0, 60)), (45, new(-1000, 40, 40)), (0, new(-1000, 80, 0)) })
            {
                var two = new CheckerSection(symmetric, Workspace(), Options2D(theta), "SLV").Domain2D().Check(action);
                Near(two.Utilization, elastic3D.Check(action).Utilization ?? double.NaN, .01, $"SLU-1 SLV θ = {theta}°: η 2D contro la verifica 3D elastica");
            }
        }
        // Grafico del dominio 2D N–M con «proietta» (seguito di SLU-1, 16/10/2026): il punto Ed disegnato è quello verificato. La verifica proietta
        // l'azione fuori piano sul meridiano di θ, Mθ = Mx cos θ + My sin θ con N invariato (assi locali); prima il grafico disegnava
        // sign(Mx)·√(Mx² + My²), un punto non verificato. Attesi a mano: Ed = (Mθ; N) e, a eccentricità costante, Ed = η·Rd (retta dall'origine per
        // Rd); l'azione nel piano resta dov'era.
        foreach (var (theta, action, expected, projected) in new (double, ActionPoint, double, bool)[]
        {
            (30, new(-500, -30, 120), -30 * Math.Cos(Math.PI / 6) + 120 * Math.Sin(Math.PI / 6), true), // 34,0192; prima −123,693, dall'altra parte
            (90, new(-800, 40, 60), 60, true),                                                          // prima 72,111
            (0, new(-500, -20, 90), -20, true),                                                         // prima −92,195
            (45, new(-600, 80, 10), 90 / Math.Sqrt(2), true),                                           // 63,6396; prima 80,623
            (30, new(-500, 100 * Math.Cos(Math.PI / 6), 100 * Math.Sin(Math.PI / 6)), 100, false)        // nel piano: invariato
        })
        {
            string tag = $"grafico N–M θ = {theta}° con proiezione, azione ({action.N}; {action.Mx:0.###}; {action.My:0.###})";
            var options = Options2D(theta); options["proietta"] = "Sì";
            var row = J.Obj(("id", "a"), ("nome", "A"), ("N", action.N.ToString("R", Invariant)), ("Mx", action.Mx.ToString("R", Invariant)), ("My", action.My.ToString("R", Invariant)));
            var result = new ConcreteAnalysisSession().Domain(symmetric, Workspace(), options, "SLU", false, [row]);
            var outcome = result.Checks["a"];
            Is(outcome.Status.EndsWith("azione proiettata") == projected && outcome.Utilization is > 0 && outcome.Resistance is not null, $"{tag}: stato «{outcome.Status}»");
            var chart = Anthea.Application.Concrete.ConcreteDomainChart.Actions(options, result.Two, [row], "a", result.Checks);
            var ed = chart.Markers.Single(m => m.Label == "Ed"); var rd = chart.Markers.Single(m => m.Label == "Rd");
            Near(ed.X, expected, 1e-9, tag + ": Mθ del punto disegnato");
            Near(ed.Y, action.N, 1e-9, tag + ": N del punto disegnato");
            double eta = outcome.Utilization ?? double.NaN;
            Near(ed.X, eta * rd.X, 1e-9, tag + ": Ed = η·Rd (Mθ)");
            Near(ed.Y, eta * rd.Y, 1e-9, tag + ": Ed = η·Rd (N)");
            Is(chart.Lines.Count == 2 && chart.Lines[0].B.SequenceEqual(new[] { ed.X, ed.Y }) && chart.Lines[1].A.SequenceEqual(new[] { ed.X, ed.Y })
                && chart.Lines[1].B.SequenceEqual(new[] { rd.X, rd.Y }), tag + ": linee origine–Ed–Rd sul punto disegnato");
        }

        // ------------------------------------------------------------ SLU-2: esiti tipizzati a N assegnato
        // Campo di N in forma chiusa (correzioni_ca_20261015.py): NRd,c = −[fcd (Ac − As) + As fyd] = −2428,3121 kN, NRd,t = As fyd = 314,7056 kN.
        {
            const double compression = -2428.3121, tension = 314.7056;
            var constantN = Domain3D(symmetric, "N costante", "SLU");
            foreach (var (action, outside, expected) in new (ActionPoint, bool, double?)[]
            {
                // Domanda puramente assiale fuori dal campo: punto a eccentricità costante, η = |N / NRd,lim| (forma chiusa, entro lo 0,5% del dominio).
                (new(-3000, 0, 0), true, 3000 / -compression), (new(400, 0, 0), true, 400 / tension),
                (new(-3000, 50, 0), true, null), (new(-3000, 50, 20), true, null), (new(400, 10, 0), true, null), (new(-2600, 5, 0), true, null),
                // Momento nullo con N nel campo: η = 0.
                (new(-1000, 0, 0), false, 0), (new(200, 0, 0), false, 0)
            })
            {
                string tag = $"SLU-2 N costante ({action.N}; {action.Mx}; {action.My})";
                var r = constantN.Check(action);
                Is(r.Status == (outside ? ConcreteLibraryMapping.AxialForceOutsideDomainStatus : ConcreteLibraryMapping.ZeroMomentDemandStatus), $"{tag}: stato «{r.Status}»");
                Is(outside ? r.Utilization > 1 : r.Utilization == 0, $"{tag}: η {r.Utilization}");
                if (expected is double e && outside) Near(r.Utilization, e, .005, tag + ": η = |N/NRd,lim|");
                if (outside)
                {
                    Near(r.AxialForceOutsideRange?.Compression, compression, 1e-3, tag + ": NRd,c");
                    Near(r.AxialForceOutsideRange?.Tension, tension, 1e-3, tag + ": NRd,t");
                }
                else Is(r.AxialForceOutsideRange is null && r.Resistance is not null && r.LimitState is not null, tag + ": punto dichiarato");
            }
            // Le stesse domande fuori campo con «N e Mx costanti» e «N e My costanti» (sottocaso di F2-17: esito della libreria, da confermare).
            foreach (var criterion in new[] { "N e Mx costanti", "N e My costanti" })
            {
                var r = Domain3D(symmetric, criterion, "SLU").Check(new(-3000, 50, 20));
                Is(r.Status == ConcreteLibraryMapping.AxialForceOutsideDomainStatus && r.Utilization > 1, $"SLU-2 {criterion} (−3000; 50; 20): «{r.Status}» η {r.Utilization}");
            }
            // Percorso della scheda (CheckMany) e riepilogo: prima C1, C2 e C3 senza η e «DA COMPLETARE» con η governante 0,26 su C4.
            var checks = constantN.CheckMany([new(-1000, 0, 0), new(-3000, 0, 0), new(-3000, 50, 0), new(-1000, 50, 0)]);
            var summary = VerificationSummary.Create("3D · SLU", checks.Length, checks.Select((c, i) => ("C" + (i + 1), c.Utilization, (bool?)null)));
            Is(summary.Status == "NON VERIFICATA" && summary.Missing == 0 && summary.Governing is "C2" or "C3", $"SLU-2 riepilogo «{summary.Status}», mancanti {summary.Missing}, governante {summary.Governing}");
            // Resistenza a N assegnato (e palo di Broms, stessa funzione): il campo invece di «non convergente».
            foreach (double axial in new[] { -3000d, 400 })
            {
                var directions = SectionMomentResistance.Calculate(symmetric, Workspace(), axial, false);
                Is(directions.All(d => d.Moment is null && d.Status.StartsWith("N fuori dal campo resistente [NRd,c; NRd,t] = [")),
                    $"SLU-2 resistenza a N = {axial}: «{directions[0].Status}»");
            }
            var inside = SectionMomentResistance.Calculate(symmetric, Workspace(), -1000, false);
            // MRd,x(N = −1000 kN) del modello a fibre indipendente del verificatore: 191,93 kNm (scratchpad bug/sezione-slu-ncost/ref.py), entro l'1%.
            Near(inside[0].Moment, 191.93, .01, "SLU-2 resistenza a N = −1000: MRd,x+");
            // Punto limite del M–χ con N fuori dal campo: rifiuto con il campo, non con il residuo su N del punto a eccentricità costante.
            string message = "";
            try { ConcreteCurvatureAnalysis.Calculate(symmetric, Workspace(), Curvature(-3000)); }
            catch (ArgumentException ex) { message = ex.Message; }
            Is(message.StartsWith("Punto limite non disponibile: N fuori dal campo resistente [NRd,c; NRd,t] = ["), "SLU-2 M–χ con N = −3000: «" + message + "»");
        }

        // ------------------------------------------------------------ SLU-4: ε0 del punto limite del M–χ al baricentro
        // 300×500 C25/30 4Ø16, N = −1000 kN, θ = 0, 10 passi lineari. Atteso a fibre (correzioni_ca_20261015.py): ε0 = −0,5032 ‰ (prima −3,5 ‰,
        // la deformazione al vertice pivot). Per costruzione, dal vertice più compresso (±150; ±250) al baricentro: ε0 [‰] = −εc [‰] + |χx| · 0,15 m
        // + |χy| · 0,25 m (χ in 1/m, εc accorciamento del vertice, positivo) per ogni riga, limite compreso.
        {
            var curve = ConcreteCurvatureAnalysis.Calculate(symmetric, Workspace(), Curvature(-1000));
            var limit = curve.Points.Single(p => p.Limit);
            Near(limit.Epsilon0, -0.5032, .015, "SLU-4 ε0 del punto limite contro il modello a fibre");
            foreach (var p in curve.Points)
                Near(p.Epsilon0, -p.ConcreteCompressionStrain + Math.Abs(p.GradientX) * 150 + Math.Abs(p.GradientY) * 250, 1e-9,
                    $"SLU-4 riga {(p.Limit ? "limite" : "M = " + p.Moment.ToString("0.###", Invariant))}: ε0 = −εc + |χx| b/2 + |χy| h/2");
        }

        // ------------------------------------------------------------ FES-2 e FES-1: fessurazione con le barre libere
        Crack(Near, Is);

        // ------------------------------------------------------------ ARM-3: staffe della T sull'anima
        // bf 1200, bw 300, hf 200, h 700, copriferro 40, staffe Ø8/150 a due bracci, ganci 20 Ø (predefinito): sviluppo 1808 mm, 4,756053 kg/m
        // (correzioni_ca_20261015.py); prima 3608 mm e 9,491061 kg/m con la larghezza dell'ala.
        {
            var input = SezioneCA.DefaultCatalogInput();
            input["shape"] = "A T"; input["flange_width_mm"] = "1200"; input["web_width_mm"] = "300"; input["flange_thickness_mm"] = "200"; input["height_mm"] = "700";
            input["cover_mm"] = "40"; input["transverse_bar_diameter_mm"] = "8"; input["transverse_spacing_mm"] = "150";
            var metrics = ConcreteReinforcementDesign.Metrics(new SezioneCA(input), J.Obj(("rami_x", "2"), ("rami_y", "2")), J.Obj(("elemento", "Trave")), new ConcreteDesignOptions());
            Near(metrics.TransverseKgPerM, 4.756053, 1e-6, "ARM-3 staffe della T");
        }

        // ------------------------------------------------------------ DET-3: B450A del catalogo
        // NTC 2018 Tab. 11.3.Ic ed EN 1992-1-1 Tab. C.1 (classe A): εuk ≥ 2,5 % = 25 ‰; B450C 75 ‰.
        {
            var catalog = RebarMaterial.Catalog();
            Is(catalog.Single(p => p.Name == "B450A").Input.D("steel_eps_u") == 25, "DET-3 preset B450A: εu " + catalog.Single(p => p.Name == "B450A").Input.S("steel_eps_u"));
            Is(catalog.Single(p => p.Name == "B450C").Input.D("steel_eps_u") == 75, "DET-3 preset B450C: εu");
            var fresh = (JsonObject)catalog.Single(p => p.Name == "B450A").Input.DeepClone(); fresh["gamma_s"] = "1.15";
            Is(RebarMaterial.Match(fresh)?.Name == "B450A" && RebarMaterial.CatalogueDifference(fresh) is null, "DET-3 preset B450A riconosciuto");
            // Scheda salvata con il catalogo precedente (εu = 30 ‰): non riscritta, non più il preset, differenza dichiarata.
            var saved = (JsonObject)fresh.DeepClone(); saved["steel_eps_u"] = 30;
            Is(RebarMaterial.Match(saved) is null, "DET-3 scheda B450A con εu 30: riconosciuta come preset");
            string? difference = RebarMaterial.CatalogueDifference(saved);
            Is(difference == "Valori salvati diversi dal catalogo B450A: εu = 30 ‰ (catalogo 25 ‰). Il calcolo usa i valori salvati; riselezionare B450A per adottare quelli del catalogo.",
                "DET-3 differenza: «" + difference + "»");
            Is(RebarMaterial.Evaluate(saved).EpsilonU == 30 && saved.D("steel_eps_u") == 30, "DET-3 valore salvato usato e non riscritto");
        }

        // ------------------------------------------------------------ foglio c.a. del palo con coefficienti unitari: ancoraggi e dettagli
        // Stessa classe di TAG-2: γc e fyd della norma effettiva (γc = 1, fyd = fyk). Attesi (correzioni_ca_20261015.py): NTC 2018 §4.1.2.1.1.4,
        // fbd = 2,25 fctk,0,05/γc, lb,rqd = Ø fyd/(4 fbd) con Ø24; §4.1.6.1.2, As,min = max(0,10 NEd/fyd; 0,003 Ac) con NEd = 10 000 kN.
        foreach (bool unit in new[] { true, false })
        {
            var (input, settings) = PileSheet(unit);
            string tag = unit ? "foglio del palo, coefficienti unitari" : "foglio del palo, coefficienti ordinari";
            var a = settings["ancoraggi"]!.AsObject();
            a["tipo"] = "Ancoraggio rettilineo"; a["diametro"] = "24"; a["sigma"] = ""; a["aderenza"] = "Buona"; a["lunghezza"] = "600";
            var anchorage = ConcreteDetailingAnalysis.Anchorage(input, settings);
            Near(anchorage.Stress, unit ? 450 : 391.304347826087, 1e-12, tag + ": σsd = fyd");
            Near(anchorage.GammaC, unit ? 1 : 1.5, 0, tag + ": γc");
            Near(anchorage.Check.Fbd, unit ? 5.055691 : 3.370461, 1e-6, tag + ": fbd");
            Near(anchorage.Check.BasicLength, unit ? 534.051642 : 696.589099, 1e-6, tag + ": lb,rqd");
            settings["dettagli_costruttivi"]!["elemento"] = "Pilastro";
            var details = ConcreteDetailingAnalysis.Calculate(input, settings, [J.Obj(("nome", "C1"), ("stato", "SLU"), ("N", "-10000"))]);
            Near(details.Checks.Single(c => c.Name == "Armatura longitudinale minima").Limit, unit ? 2222.222222 : 2555.555556, 1e-9, tag + ": As,min = 0,10 NEd/fyd");
        }

        // ------------------------------------------------------------ foglio c.a. del palo con coefficienti unitari: primo snervamento del M–χ
        // Seguito del 16/10/2026, stessa classe di TAG-2: la legge dell'acciaio usa fyd della norma effettiva, il primo snervamento della curva
        // εyd = fyd/Es della stessa norma. Attesi (correzioni_ca_20261015.py, modello a strisce): 300×500 C25/30 4Ø16, N = 0, θ = 0; unitari
        // εyd = 450/200 000 = 2,25 ‰, My = 76,4192 kNm, χy = 6,1483e-3 1/m (prima εyd = 391,30/200 000 = 1,9565 ‰ anche con i coefficienti
        // unitari: My 66,4926 kNm, χy 5,3395e-3 1/m); ordinari εyd = 1,9565 ‰, My = 65,2912 kNm, χy = 5,6821e-3 1/m.
        foreach (bool unit in new[] { true, false })
        {
            var (input, settings) = PileSheet(unit, Rect(2, 16, 2, 16));
            string tag = "M–χ " + (unit ? "foglio del palo, coefficienti unitari" : "foglio del palo, coefficienti ordinari");
            var curve = ConcreteCurvatureAnalysis.Calculate(input, settings, Curvature(0));
            double yieldStrain = unit ? 2.25 : 450 / 1.15 / 200000 * 1000; // ‰
            int first = curve.Points.ToList().FindIndex(p => p.Yielded);
            Is(first > 0, $"{tag}: primo snervamento dopo la prima riga ({first})");
            if (first <= 0) continue;
            var yielded = curve.Points[first]; var before = curve.Points[first - 1];
            // Riga del primo snervamento raffinata con 12 bisezioni: la barra più deformata appena oltre εyd, la riga precedente sotto.
            Is(yielded.SteelStrain >= yieldStrain && yielded.SteelStrain <= yieldStrain + 1e-3, $"{tag}: εs al primo snervamento {yielded.SteelStrain:G9} ‰ (εyd {yieldStrain:G9} ‰)");
            Is(before.SteelStrain < yieldStrain && !before.Yielded, $"{tag}: riga precedente εs {before.SteelStrain:G9} ‰ non snervata");
            Near(Math.Abs(yielded.Moment), unit ? 76.4192 : 65.2912, .005, tag + ": My contro il modello a strisce");
            Near(curve.YieldCurvature, unit ? 6.1483e-3 : 5.6821e-3, .005, tag + ": χy contro il modello a strisce");
        }
        return count;
    }

    static JsonObject Curvature(double axial) => J.Obj(("N", axial.ToString(Invariant)), ("theta", "0"), ("passi", "10"), ("frazione", "1"), ("campionamento", "Lineare"),
        ("trazione_cls", "No"), ("angoli", "64"), ("tolleranza_n", "1"), ("raffina_snervamento", "12"));

    /// <summary>Foglio c.a. di approfondimento del palo come lo crea ElasticHorizontalPile.CreateConcreteSheet: sezione predefinita 600×800 C35/45
    /// (oppure i campi di <paramref name="section"/>), NTC 2018; con i coefficienti unitari la normativa_custom di ConcreteStandards.PileWorkspace
    /// (tutti i γ e αcc = 1).</summary>
    static (JsonObject Input, JsonObject Settings) PileSheet(bool unit, JsonObject? section = null)
    {
        var data = SezioneCA.DefaultData(); var input = data["input"]!.AsObject();
        if (section is not null) foreach (var (k, v) in section) input[k] = v?.DeepClone();
        var settings = SectionWorkspace.Prepare(data);
        settings["normativa"] = "NTC 2018"; settings["coefficienti"] = ConcreteStandards.Defaults("NTC 2018");
        foreach (var (k, v) in ConcreteCalculationSettings.CommonCoefficients) input[k] = settings["coefficienti"]![v]!.DeepClone();
        ConcreteCalculationSettings.Prepare(input, settings);
        input["coefficienti_unitari"] = unit;
        if (unit)
        {
            var custom = ConcreteStandards.PileWorkspace(input);
            settings["coefficienti"] = custom["coefficienti"]!.DeepClone(); settings["normativa_custom"] = custom["normativa_custom"]!.DeepClone();
        }
        return (input, settings);
    }

    /// <summary>
    /// FES-2 (copriferro della formula dalle barre efficaci) e FES-1 (asse neutro nel copriferro di una barra del lembo teso) sul percorso del
    /// modulo (CheckerSection.Stress e ConcreteServiceabilityAdapter.Cracking), con le regole del prodotto e, per confronto, con quelle precedenti.
    /// </summary>
    static void Crack(Action<double?, double, double, string> near, Action<bool, string> @is)
    {
        var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data);
        var input = (JsonObject)data["input"]!.DeepClone();
        input["shape"] = "Rettangolare"; input["width_mm"] = "300"; input["height_mm"] = "500"; input["fck_mpa"] = "30";
        input["cover_mm"] = "30"; input["transverse_bar_diameter_mm"] = "8"; input["staffe_presenti"] = "Sì";
        JsonArray Bars(params (double X, double Y, double Phi)[] bars)
            => new(bars.Select(b => (JsonNode)new JsonObject { ["x"] = b.X.ToString("R", Invariant), ["y"] = b.Y.ToString("R", Invariant), ["phi"] = b.Phi.ToString("R", Invariant) }).ToArray());
        Ntc2018Checks.CrackResult Run(JsonArray bars, double n, double mx, string code)
        {
            var i = (JsonObject)input.DeepClone(); i["barre_manuali"] = bars.DeepClone();
            var w = (JsonObject)settings.DeepClone(); w["normativa"] = code;
            var sle = (JsonObject)settings["sle"]!["SLE_QP"]!.DeepClone();
            sle["modello"] = "Lineare"; sle["phi"] = "0"; sle["trazione_cls"] = "No"; sle["esposizione"] = "XC1"; sle["sensibilita"] = "Poco sensibile";
            sle["durata"] = "Lunga"; sle["aderenza"] = "Migliorata"; sle["copriferro_fessure"] = ""; sle["spaziatura_fessure"] = "";
            sle["limite_fessure"] = code == "NTC 2018" ? "" : "0.3";
            var engine = new CheckerSection(i, w, sle);
            var action = new ActionPoint(n, mx, 0);
            return ConcreteServiceabilityAdapter.Cracking(engine, engine.Stress(action, "SLE_QP"), action, i, w, sle, "SLE_QP");
        }
        double? Detail(Ntc2018Checks.CrackResult r, string symbol) => r.Details.LastOrDefault(d => d.Symbol == symbol)?.Value;
        // Verso di Mx che tende il lembo inferiore.
        var probe = new CheckerSection(Merge(input, Bars((-100, -200, 20), (0, -200, 20), (100, -200, 20))), settings, (JsonObject)settings["sle"]!["SLE_QP"]!.DeepClone());
        double sign = probe.Stress(new(0, 100, 0), "SLE_QP").tensioni_barre.All(t => t > 0) ? 1 : -1;
        // FES-2: 3Ø20 libere con il centro a 70 mm dal lembo teso (copriferro reale 60), 2Ø16 a 50 mm dal lembo compresso, N = 0, Mx = 98 kNm.
        // Atteso EN (7.8)-(7.11) (correzioni_ca_20261015.py): c = 60, sr,max = 345,594 mm, wk = 0,36132 mm, non soddisfatta (wlim 0,3); prima
        // c = 38 (nominale 30 + 8) e wk = 0,28311 mm, soddisfatta. NTC (C4.1.2.2.4.5) con le stesse formule: stesso wk.
        foreach (string code in new[] { "NTC 2018", "EN 1992-1-1" })
        {
            var deep = Bars((-100, -180, 20), (0, -180, 20), (100, -180, 20), (-100, 200, 16), (100, 200, 16));
            var r = Run(deep, 0, sign * 98, code);
            near(r.Width, 0.36132, 1e-4, $"FES-2 {code}: wk con le barre a 70 mm");
            near(Detail(r, "c"), 60, 1e-9, $"FES-2 {code}: c dalle barre");
            @is(r.Passed == false && r.Details.Any(d => d.Symbol == "c" && d.Expression == "Minimo copriferro libero delle barre efficaci verso la faccia tesa"),
                $"FES-2 {code}: esito o testo del copriferro");
            Ntc2018Checks.CrackResult previous;
            using (PreviousCrackRules.Use()) previous = Run(deep, 0, sign * 98, code);
            near(previous.Width, 0.28311, 1e-4, $"FES-2 {code}: wk con le regole precedenti (copriferro nominale)");
            // Barre al copriferro nominale (centro a 48 mm): c = 38 in entrambi i casi, wk = 0,25865 mm.
            var nominal = Bars((-100, -202, 20), (0, -202, 20), (100, -202, 20), (-100, 200, 16), (100, 200, 16));
            near(Run(nominal, 0, sign * 98, code).Width, 0.25865, 1e-4, $"FES-2 {code}: wk con le barre al copriferro nominale");
        }
        // FES-1: 3Ø16 in alto (y = +204), 2Ø12 laterali a 200 mm dal lembo inferiore teso, nessuna barra inferiore; N = −600 kN, Mx = 86 kNm.
        // h − x = 171,43 mm (verifica indipendente del verificatore, attesi.py); la barra più vicina dista 200 mm > a,c = 30 + 8 + 12/2 = 44 mm
        // (gradiente normale alla faccia): zona tesa senza armatura, non soddisfatta. Prima (soglia h/2): wk = 0 «asse neutro nel copriferro».
        foreach (string code in new[] { "NTC 2018", "EN 1992-1-1" })
        {
            var side = Bars((-100, 204, 16), (0, 204, 16), (100, 204, 16), (-106, -50, 12), (106, -50, 12));
            var r = Run(side, -600, sign * 86, code);
            @is(r.Passed == false && r.Width is null && r.Status == "Nessuna armatura tesa · non soddisfatta: zona tesa senza armatura, apertura delle fessure non controllata",
                $"FES-1 {code}: «{r.Status}» wk {r.Width}");
            near(Detail(r, "h − x"), 171.43, 1e-3, $"FES-1 {code}: h − x");
            near(Detail(r, ConcreteLibraryMapping.ProjectedCoverSymbol), 44, 1e-6, $"FES-1 {code}: a,c (rientro poligonale della libreria)");
            Ntc2018Checks.CrackResult previous;
            using (PreviousCrackRules.Use()) previous = Run(side, -600, sign * 86, code);
            @is(previous.Passed == true && previous.Width == 0 && previous.Details.All(d => d.Symbol != ConcreteLibraryMapping.ProjectedCoverSymbol),
                $"FES-1 {code}: regole precedenti «{previous.Status}»");
        }
    }

    static JsonObject Merge(JsonObject input, JsonArray bars) { var copy = (JsonObject)input.DeepClone(); copy["barre_manuali"] = bars.DeepClone(); return copy; }
}
