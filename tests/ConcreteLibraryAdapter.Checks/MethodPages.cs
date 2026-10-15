using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;

/// <summary>
/// Sezione 13 delle prove dell'adattatore: attesi indipendenti degli esempi delle pagine dei metodi di Checker (docs/metodi, §10) per
/// ancoraggi e aderenza, dettagli costruttivi e momento–curvatura, sul percorso di produzione di ANTHEA (refactoring F2.8, decisione E3
/// del 15/10/2026). Gli attesi sono in attesi/pagine-metodi.json, scritto da attesi/pagine_metodi.py con le formule della norma e con i
/// numeri della pagina ricontrollati (nessun valore del programma); la suite 'ConcreteLibraryAdapter.Checks/attesi' del runner lo
/// rigenera e lo confronta. Ogni messaggio comincia con il numero della prova.
/// 13a. ca.ancoraggi §10, casi A-G nel ramo NTC, e casi AN-01/02, SO-01/02 della campagna del 25/9: adattatore ConcreteAnchorageCalculator
///      (fbd, η1, η2, lb,rqd, α6, lunghezza richiesta, interferro massimo ed esiti) e punto d'ingresso ConcreteDetailingAnalysis.Anchorage
///      (fctk,0,05 da fck, σsd = fyd se non assegnata), che rifiuta σsd > fyd (AN-01, SO-01).
/// 13b. ca.dettagli §10, profilo NTC, con ConcreteDetailingCalculator: trave 300 × 500 (interferro, copriferro, As,min, As,max, staffe,
///      trattenimento) e pilastro 400 × 400 (diametro, interasse, As,min, As,max, staffe), anche in zona di sovrapposizione (0,08 Ac).
///      Valore effettivo, limite ed esito di ogni controllo atteso; nessun altro controllo non soddisfatto.
/// 13c. ca.momento-curvatura §10 con ConcreteCurvatureAnalysis: R300x500, N = 0, 40 passi quadratici e 12 bisezioni, nei due versi
///      (θ = 0° e 180°; il lembo compresso si riconosce dalle deformazioni dei vertici di un'analisi tensionale della stessa sezione):
///      momento limite, χu, εs al limite, primo snervamento (My,1, χy,1, εc), numero dei punti e residuo su N.
/// Ancoraggi e dettagli sono formule chiuse: numeri entro 1e-9 relativo. M–χ entro le tolleranze del JSON (ramo parabolico tabellato a
/// 8 tratti calcolato dallo script, più un margine per la mesh e il residuo su N, come dichiara la pagina).
/// </summary>
internal static class MethodPageChecks
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static int Run(string root, Action<bool, string> check)
    {
        var data = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests", "ConcreteLibraryAdapter.Checks", "attesi", "pagine-metodi.json")))!;
        int count = 0;
        void Check(bool ok, string message) { check(ok, message); count++; }
        void Near(double? actual, double expected, double relative, string message)
            => Check(actual is double a && double.IsFinite(a) && Math.Abs(a - expected) <= relative * Math.Max(1, Math.Abs(expected)),
                $"{message}: {Show(actual)} invece di {expected.ToString("R", Invariant)}");
        Anchorages(data["ancoraggi"]!.AsArray(), Check, Near);
        Detailing(data["dettagli"]!.AsArray(), Check, Near);
        Curvature(data["momento_curvatura"]!.AsArray(), Check, Near);
        Check(count > 150, "13: controlli delle pagine dei metodi: " + count);
        return count;
    }

    static string Show(double? value) => value is double v ? v.ToString("R", Invariant) : "null";
    static double Num(JsonNode? node, string key) => J.Number(node?[key]) ?? throw new InvalidOperationException("pagine-metodi.json: manca " + key);
    static double At(JsonNode? node) => J.Number(node) ?? throw new InvalidOperationException("pagine-metodi.json: coordinata non numerica");
    static bool Flag(JsonNode? node, string key) => node?[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : throw new InvalidOperationException("pagine-metodi.json: manca " + key);
    static string Text(double value) => value.ToString("R", Invariant);

    /// <summary>Sezione c.a. di ANTHEA con le barre assegnate, NTC 2018 con i coefficienti di norma (αcc 0,85, γc 1,5, γs 1,15).</summary>
    static (JsonObject Input, JsonObject Settings) Section(JsonNode c, double fck, string concreteClass, Action<bool, string> check)
    {
        var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        input["shape"] = "Rettangolare"; input["width_mm"] = Text(Num(c, "b")); input["height_mm"] = Text(Num(c, "h"));
        input["fck_mpa"] = Text(fck); input["classe_cls"] = concreteClass; input["materiale_cls_nome"] = concreteClass; input["cls_diagramma"] = "Parabola-rettangolo";
        input["fyk_mpa"] = Text(Num(c, "fyk")); input["steel_modulus_mpa"] = "200000";
        input["barre_manuali"] = new JsonArray(c["barre"]!.AsArray().Select(b => (JsonNode)J.Obj(("x", At(b![0])), ("y", At(b[1])), ("phi", At(b[2])))).ToArray());
        settings["normativa"] = "NTC 2018"; settings["coefficienti"] = ConcreteStandards.Defaults("NTC 2018");
        foreach (var (k, v) in ConcreteCalculationSettings.CommonCoefficients) input[k] = settings["coefficienti"]![v]!.DeepClone();
        check(input.D("alpha_cc") == .85 && input.D("gamma_c") == 1.5 && input.D("gamma_s") == 1.15, "13: coefficienti NTC 2018 diversi da αcc 0,85, γc 1,5, γs 1,15");
        return (input, settings);
    }

    // ================================================================ 13a. ancoraggi e aderenza
    static void Anchorages(JsonArray cases, Action<bool, string> check, Action<double?, double, double, string> near)
    {
        const double Tol = 1e-9;
        int entries = 0, rejected = 0;
        foreach (var c in cases.Select(n => n!))
        {
            string id = "13a: " + c.S("id") + " (" + c.S("fonte") + ")";
            var d = c["dati"]!; var e = c["atteso"]!;
            bool lap = Flag(d, "sovrapposizione");
            var input = new AnchorageInput(Num(d, "diametro"), Num(d, "sigma"), Num(d, "fctk005"), Num(d, "gamma_c"), Flag(d, "aderenza_buona"),
                Num(d, "lunghezza"), lap, Num(d, "percentuale"), Num(d, "interferro"));
            void Same(AnchorageResult r, string path)
            {
                near(r.Fbd, Num(e, "Fbd"), Tol, $"{id}, {path}: fbd"); near(r.Eta1, Num(e, "Eta1"), Tol, $"{id}, {path}: η1"); near(r.Eta2, Num(e, "Eta2"), Tol, $"{id}, {path}: η2");
                near(r.BasicLength, Num(e, "BasicLength"), Tol, $"{id}, {path}: lb,rqd"); near(r.Alpha6, Num(e, "Alpha6"), Tol, $"{id}, {path}: α6");
                near(r.RequiredLength, Num(e, "RequiredLength"), Tol, $"{id}, {path}: lunghezza richiesta");
                if (lap) near(r.MaximumLapClearDistance, Num(e, "MaximumLapClearDistance"), Tol, $"{id}, {path}: interferro massimo 4Ø");
                check(r.LengthPassed == Flag(e, "LengthPassed"), $"{id}, {path}: esito della lunghezza {r.LengthPassed}");
                check(!lap || r.LapClearDistancePassed == Flag(e, "LapClearDistancePassed"), $"{id}, {path}: esito dell'interferro {r.LapClearDistancePassed}");
                check(r.Passed == Flag(e, "Passed"), $"{id}, {path}: esito {r.Passed}");
            }
            Same(new ConcreteAnchorageCalculator().Calculate(input), "adattatore");

            // Punto d'ingresso del modulo: fctk,0,05 da fck (AnchorageCalculator.BondFctk05), σsd = fyd quando il campo è vuoto.
            var (sectionInput, settings) = Section(J.Obj(("b", 300), ("h", 500), ("fyk", 450), ("barre", new JsonArray(new JsonArray(0, -200, 20)))),
                Num(d, "fck"), Num(d, "fck") == 25 ? "C25/30" : "C30/37", check);
            double fyd = 450 / 1.15;
            var a = settings["ancoraggi"]!.AsObject();
            a["tipo"] = lap ? "Sovrapposizione rettilinea" : "Ancoraggio rettilineo"; a["diametro"] = Text(Num(d, "diametro"));
            a["sigma"] = Num(d, "sigma") == fyd ? "" : Text(Num(d, "sigma")); a["aderenza"] = Flag(d, "aderenza_buona") ? "Buona" : "Non buona";
            a["lunghezza"] = Text(Num(d, "lunghezza")); a["percentuale"] = Text(Num(d, "percentuale")); a["interferro"] = Text(Num(d, "interferro"));
            if (Flag(c, "ingresso_completo"))
            {
                var module = ConcreteDetailingAnalysis.Anchorage(sectionInput, settings);
                near(module.Fctk05, Num(d, "fctk005"), Tol, $"{id}, punto d'ingresso: fctk,0,05 da fck");
                near(module.Stress, Num(d, "sigma"), Tol, $"{id}, punto d'ingresso: σsd");
                Same(module.Check, "punto d'ingresso");
                entries++;
            }
            else
            {
                // σsd > fyd (pagina, §4: σsd non superiore a fyd per costruzione del chiamante): il modulo rifiuta il dato.
                string message = "";
                try { ConcreteDetailingAnalysis.Anchorage(sectionInput, settings); } catch (ArgumentException ex) { message = ex.Message; }
                check(Num(d, "sigma") > fyd && message == "La tensione di ancoraggio non può superare fyd.", $"{id}, punto d'ingresso: σsd > fyd non rifiutata («{message}»)");
                rejected++;
            }
        }
        check(cases.Count == 11 && entries == 9 && rejected == 2, $"13a: casi {cases.Count}, dal punto d'ingresso {entries}, rifiutati {rejected}");
    }

    // ================================================================ 13b. dettagli costruttivi (profilo NTC)
    static void Detailing(JsonArray cases, Action<bool, string> check, Action<double?, double, double, string> near)
    {
        const double Tol = 1e-9;
        foreach (var c in cases.Select(n => n!))
        {
            string id = "13b: " + c.S("id") + " (" + c.S("fonte") + ")";
            var (input, _) = Section(c, Num(c, "fck"), "C25/30", check);
            input["cover_mm"] = Text(Num(c, "cnom"));
            var links = c["staffe"]!;
            input["transverse_bar_diameter_mm"] = Text(Num(links, "diametro")); input["transverse_spacing_mm"] = Text(Num(links, "passo"));
            var section = new SezioneCA(input);
            bool beam = c.S("id") == "trave";
            var request = new ConcreteDetailingInput(beam ? ConcreteMemberKind.Beam : ConcreteMemberKind.Column, section, Num(c, "NEd_kN"), true,
                Num(links, "diametro"), Num(links, "passo"), beam ? (int)Num(links, "braccia") : 0, Num(c, "aggregato"), Num(c, "cmin_dur"), Num(c, "delta_c"),
                Flag(c, "sovrapposizione"), 0, 0, false, true, true);
            var checks = new ConcreteDetailingCalculator().Calculate(request);
            var expected = c["controlli"]!.AsArray();
            foreach (var x in expected)
            {
                string name = x!.S("nome"), key = x.S("chiave");
                var found = checks.Where(r => r.Name == name).ToArray();
                check(found.Length == 1, $"{id}: controllo «{name}» ({key}) presente {found.Length} volte");
                var r = found[0];
                near(r.Actual, Num(x, "effettivo"), Tol, $"{id}: {name} ({key}), valore effettivo");
                near(r.Limit, Num(x, "limite"), Tol, $"{id}: {name} ({key}), limite");
                check(r.Passed == Flag(x, "esito"), $"{id}: {name} ({key}), esito {r.Passed?.ToString() ?? "in sospeso"}");
            }
            // Nessun altro controllo non soddisfatto: con le conferme date la trave è tutta soddisfatta, il pilastro fallisce solo il passo.
            var failing = checks.Where(r => r.Passed == false).Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var expectedFailing = expected.Where(x => !Flag(x, "esito")).Select(x => x!.S("nome")).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            check(failing.SequenceEqual(expectedFailing), $"{id}: controlli non soddisfatti {string.Join(", ", failing)} invece di {string.Join(", ", expectedFailing)}");
            if (beam) check(checks.All(r => r.Passed == true), $"{id}: trave con le conferme non tutta soddisfatta: {string.Join(", ", checks.Where(r => r.Passed != true).Select(r => r.Name))}");
        }
        check(cases.Count == 3, "13b: casi " + cases.Count);
    }

    // ================================================================ 13c. momento–curvatura
    static void Curvature(JsonArray cases, Action<bool, string> check, Action<double?, double, double, string> near)
    {
        // Le due direzioni del modulo: il lembo compresso di ciascuna si legge dalle deformazioni dei vertici del calcestruzzo di un'analisi
        // tensionale della stessa sezione a N = 0 con un momento moderato nella direzione θ (stesse opzioni di ConcreteCurvatureAnalysis).
        var faces = new Dictionary<string, MomentCurvatureResult>();
        var first = cases[0]!;
        var (input, settings) = Section(first, Num(first, "fck"), "C30/37", check);
        input["steel_fu_mpa"] = Text(Num(first, "fu")); input["steel_eps_u"] = Text(Num(first, "eps_uk_permille")); input["steel_diagramma"] = "Elastoplastico";
        input["cover_mm"] = "30";
        foreach (double theta in new[] { 0d, 180d })
        {
            var options = (JsonObject)settings["momento_curvatura"]!.DeepClone();
            options["N"] = Text(Num(first, "N_kN")); options["theta"] = Text(theta); options["passi"] = Text(Num(first, "passi")); options["frazione"] = "1";
            options["campionamento"] = first.S("campionamento"); options["tolleranza_n"] = Text(Num(first, "tolleranza_N_kN"));
            options["raffina_snervamento"] = Text(Num(first, "bisezioni")); options["trazione_cls"] = "No"; options["angoli"] = "16";
            var probe = (JsonObject)options.DeepClone();
            probe["criterio"] = "N costante"; probe["assi"] = "Locali"; probe["modello"] = "Non lineare"; probe["strategia"] = "Iterativo";
            var state = new CheckerSection(input, settings, probe).Stress(new ActionPoint(0, 30 * Math.Cos(theta * Math.PI / 180), 30 * Math.Sin(theta * Math.PI / 180)), "CURVA");
            double top = state.ConcreteVertices.Where(v => v.Y > 0).Max(v => v.Strain), bottom = state.ConcreteVertices.Where(v => v.Y < 0).Max(v => v.Strain);
            check((top < 0) != (bottom < 0), $"13c: θ = {theta}°, lembo compresso non riconosciuto (εsup {top} ‰, εinf {bottom} ‰)");
            string face = top < 0 ? "superiore" : "inferiore";
            check(!faces.ContainsKey(face), $"13c: θ = 0° e 180° comprimono lo stesso lembo ({face})");
            faces[face] = ConcreteCurvatureAnalysis.Calculate(input, settings, options);
        }
        foreach (var c in cases.Select(n => n!))
        {
            string face = c.S("lembo_compresso"), id = $"13c: lembo {face} compresso ({c.S("fonte")})";
            var e = c["atteso"]!; var tol = c["tolleranze"]!;
            var r = faces[face];
            near(r.LimitMoment, Num(e, "Mr_kNm"), Num(tol, "Mr_kNm"), id + ": momento limite [kNm]");
            near(r.UltimateCurvature, Num(e, "chi_u_1_m"), Num(tol, "chi_u_1_m"), id + ": χu [1/m]");
            var limit = r.Points[^1];
            check(limit.Limit && r.Points.Count(p => p.Limit) == 1, id + ": l'ultimo punto non è il punto limite");
            near(limit.SteelStrain, Num(e, "eps_s_limite"), Num(tol, "eps_s_limite"), id + ": εs al punto limite [‰]");
            check(Math.Abs(r.LimitAxialKn - Num(c, "N_kN")) <= Num(c, "tolleranza_N_kN"), $"{id}: N al punto limite {r.LimitAxialKn} kN oltre la tolleranza");
            var yielded = r.Points.First(p => p.Yielded);
            near(yielded.Moment, Num(e, "My_kNm"), Num(tol, "My_kNm"), id + ": My,1 dopo le bisezioni [kNm]");
            near(r.YieldCurvature, Num(e, "chi_y_1_m"), Num(tol, "chi_y_1_m"), id + ": χy,1 [1/m]");
            near(yielded.Curvature, Num(e, "chi_y_1_m"), Num(tol, "chi_y_1_m"), id + ": curvatura del punto di primo snervamento [1/m]");
            near(yielded.ConcreteCompressionStrain, Num(e, "eps_c_snervamento"), Num(tol, "eps_c_snervamento"), id + ": εc al primo snervamento [‰]");
            // 40 passi quadratici (41 campioni, l'ultimo è il punto limite) più il punto raffinato del primo snervamento.
            check(r.Points.Count == (int)Num(c, "passi") + 2, $"{id}: {r.Points.Count} punti invece di {(int)Num(c, "passi") + 2}");
            check(r.Points.Zip(r.Points.Skip(1)).All(p => p.Second.Moment > p.First.Moment && p.Second.Curvature > p.First.Curvature), id + ": curva non crescente");
        }
        check(cases.Count == 2 && faces.Count == 2, "13c: casi " + cases.Count);
    }
}
