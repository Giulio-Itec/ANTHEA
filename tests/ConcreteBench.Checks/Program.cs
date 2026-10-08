using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

// Banco F2.1 del refactoring (docs/refactoring/f2.1-banco.md, voci «casi vicini alla soglia» e «test con sezioni in forma chiusa»).
//   dotnet ConcreteBench.Checks.dll [cartella]   (con la cartella scrive misura.json: conteggi, scarti massimi, soglie trovate)
// Motori confrontati: il legacy di X.Calculations (CheckerSection.Stress e i limiti di DescribeStress, Ntc2018Checks.Cracking,
// SectionMomentResistance, CheckerDomain3D, ConcreteCodeChecks.Shear) e GPCChecker.Concrete di lib/Checker chiamato direttamente
// (SectionCheckerModelCode2010, StressLimitCheck, SectionCrackCheck, SectionSolver.CalculateDomainPoint; il taglio attraverso
// ConcreteShearTorsionAdapter con il motore Library). Ingressi comuni: la sezione del Model costruita da CheckerSection.PrepareModel
// (mappatura geometria e materiali) e la norma di ConcreteStandards.Effective; il resto del percorso della libreria non passa dal legacy.
// A. Forme chiuse: attesi indipendenti di attesi/attesi.json (attesi/genera_attesi.py, Python, mai uscite del codice) per la sezione
//    elastica fessurata (flessione semplice e composta, rettangoli e poligoni regolari) e per la resistenza a rottura con stress-block
//    e parabola-rettangolo. Tolleranze fissate prima delle misure: 1e-3 sulle tensioni e sulla posizione dell'asse neutro (1e-3 h),
//    5e-3 su MRd (integrazione della libreria con punti di Gauss su una mesh fissa, non tagliata lungo i salti della legge σ-ε;
//    parabola della libreria discretizzata in 8 tratti). I due motori fra loro: 1e-9.
// B. Soglie: per ogni ramo un parametro di carico o di geometria trovato per bisezione sul legacy; i due motori a λ*(1 ± 1e-4),
//    λ*(1 ± 1e-6), λ*(1 ± 1e-9) e a λ*: stessi esiti, numeri entro 1e-9 (|a − b| ≤ 1e-9 + 1e-9 · max(|a|, |b|)), e il ramo atteso
//    ai due lati a ± 1e-4 (sopra il rumore del solutore di sezione).
// Esito, soglie e divergenze: esito.md.
// Una divergenza registrata (divergenze.json) è riportata e non fa fallire; ogni altra fa fallire con uscita 1.
// Uscita 0 con la riga "PASS · …".
const double EngineTolerance = 1e-9;
const double SleTolerance = 1e-3;
const double UlsTolerance = 5e-3;

var invariant = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
int count = 0;
var problems = new List<string>();
var registered = new List<string>();
var measures = new JsonObject();
var thresholds = new JsonArray();
var closedForms = new JsonArray();
var maxima = new Dictionary<string, double>();
void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
void Max(string key, double value) { maxima[key] = Math.Max(maxima.GetValueOrDefault(key), value); }
bool CloseTo(double a, double b, double tol = EngineTolerance) => Math.Abs(a - b) <= tol + tol * Math.Max(Math.Abs(a), Math.Abs(b));
string F(double v) => v.ToString("R", invariant);

string root = Root();
var divergences = LoadDivergences(Path.Combine(root, "tests", "ConcreteBench.Checks", "divergenze.json"));
// Scarto rispetto a un atteso indipendente: entro la tolleranza conta come controllo; fuori, divergenza registrata o problema.
void Expect(string id, string quantity, double actual, double expected, double scale, double tolerance)
{
    double deviation = Math.Abs(actual - expected) / scale;
    Max(quantity, deviation);
    closedForms.Add(J.Obj(("id", id), ("grandezza", quantity), ("calcolato", actual), ("atteso", expected), ("scarto", deviation)));
    if (deviation <= tolerance) { count++; return; }
    string key = id + " · " + quantity;
    string text = $"{key}: {actual:G10} invece di {expected:G10} (scarto {deviation:G3} della scala {scale:G6}, tolleranza {tolerance:G3})";
    if (divergences.Contains(key)) registered.Add(text); else problems.Add(text);
}
// Accordo dei due motori su una soglia: in disaccordo, divergenza registrata (chiave = testo prima di ": ") o problema.
void Agree(bool ok, string message)
{
    if (ok) { count++; return; }
    string key = message.Split(": ", 2)[0];
    if (divergences.Contains(key)) registered.Add(message); else problems.Add(message);
}

try
{
    var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "attesi", "attesi.json")))!.AsObject();
    Check(expected["strumento"]!.GetValue<string>() == "genera_attesi.py", "attesi.json non generato da genera_attesi.py");

    // ------------------------------------------------------------ A1. SLE, sezione elastica fessurata
    int? sign = null; // segno di Mx di ANTHEA per il momento positivo degli attesi (che comprime il lembo superiore)
    int sleStates = 0;
    foreach (var c in expected["sle"]!.AsArray().Select(n => n!.AsObject()))
    {
        string id = c["id"]!.GetValue<string>();
        var spec = Spec.From(expected["sezioni"]![c["sezione"]!.GetValue<string>()]!.AsObject());
        var p = Prep.Build(spec, c.D("fck"), c["normativa"]!.GetValue<string>(), c.D("alpha_cc"), c.D("gamma_c"), c.D("gamma_s"));
        var options = p.Options("SLE");
        var engine = new CheckerSection(p.Input, p.Settings, options);
        var library = p.Library(true, 0, false);
        foreach (var a in c["azioni"]!.AsArray().Select(n => n!.AsObject()))
        {
            double m = a.D("M_kNm");
            // Prima azione: il segno di ANTHEA che dà il lembo meno deformato degli attesi; poi lo stesso segno per tutte.
            if (sign is null)
            {
                var probe = engine.Stress(new ActionPoint(c.D("N_kN"), m, 0), "SLE");
                sign = spec.LessStrainedEdge(probe.Native.StrainPlane) == a["lembo_meno_deformato"]!.GetValue<string>() ? 1 : -1;
            }
            var action = new ActionPoint(c.D("N_kN"), sign.Value * m, 0);
            string at = id + " M=" + F(m);
            var legacy = engine.Stress(action, "SLE");
            var force = Prep.Force(library.Local, action);
            var mapped = engine.Force(action);
            Check(force.N == mapped.N && force.M1 == mapped.M1 && force.M2 == mapped.M2, at + ": forze della libreria diverse da quelle del legacy");
            var state = library.Checker.GetTensionAnalysisResult(force);
            var rare = StressLimitCheck.Evaluate(state, ServiceabilityCombination.Characteristic);
            var qp = StressLimitCheck.Evaluate(state, ServiceabilityCombination.QuasiPermanent);
            double[] libraryBars = state.GetRebarsTension(0, 0).Select(b => b.tension).ToArray();
            // Lembo meno deformato e asse neutro dai piani dei due motori.
            Check(spec.LessStrainedEdge(legacy.Native.StrainPlane) == a["lembo_meno_deformato"]!.GetValue<string>(), at + ": lembo meno deformato del legacy");
            Check(spec.LessStrainedEdge(state.StrainPlane) == a["lembo_meno_deformato"]!.GetValue<string>(), at + ": lembo meno deformato della libreria");
            var bars = a["sigma_s"]!.AsArray().Select(v => v!.GetValue<double>()).ToArray();
            double steelScale = bars.Max(Math.Abs), concreteScale = Math.Max(Math.Abs(a.D("sigma_c_min")), 1);
            foreach (var (name, sigmaC, barStresses, plane, ratioRare, ratioQp) in new[] {
                ("legacy", legacy.sigma_cls, legacy.tensioni_barre, legacy.Native.StrainPlane, legacy.Ratio, engine.Stress(action, "SLE_QP").Ratio),
                ("libreria", rare.ConcreteMinStress, libraryBars, state.StrainPlane, rare.Ratio, qp.Ratio) })
            {
                Expect(at + " " + name, "SLE σc,min", sigmaC, a.D("sigma_c_min"), concreteScale, SleTolerance);
                Check(barStresses.Length == bars.Length, at + " " + name + ": numero di barre");
                for (int i = 0; i < bars.Length; i++) Expect(at + " " + name + " B" + (i + 1), "SLE σs", barStresses[i], bars[i], steelScale, SleTolerance);
                Expect(at + " " + name, "SLE yn", spec.NeutralAxis(plane), a.D("yn_mm"), spec.Height, SleTolerance);
                Expect(at + " " + name, "SLE rapporto rara", ratioRare!.Value, a.D("rapporto_rara"), 1, SleTolerance);
                Expect(at + " " + name, "SLE rapporto qp", ratioQp!.Value, a.D("rapporto_qp"), 1, SleTolerance);
            }
            // Limiti NTC 2018 §4.1.2.2.5.1 (0,60 fck, 0,45 fck, 0,80 fyk) del legacy e della libreria.
            var limits = c["limiti"]!.AsObject();
            Check(CloseTo(legacy.ConcreteStressLimit!.Value, -limits.D("sigma_c_rara")) && CloseTo(rare.ConcreteLimit!.Value, -limits.D("sigma_c_rara")), at + ": limite 0,60 fck");
            Check(CloseTo(legacy.SteelStressLimit, limits.D("sigma_s")) && rare.SteelPoints.All(s => CloseTo(s.Limit, limits.D("sigma_s"))), at + ": limite 0,80 fyk");
            Check(CloseTo(qp.ConcreteLimit!.Value, -limits.D("sigma_c_qp")), at + ": limite 0,45 fck");
            // I due motori fra loro: stesso solutore, stessi numeri.
            Check(CloseTo(legacy.sigma_cls, rare.ConcreteMinStress) && legacy.tensioni_barre.Zip(libraryBars).All(z => CloseTo(z.First, z.Second))
                && CloseTo(legacy.Ratio!.Value, rare.Ratio!.Value), at + ": legacy e libreria diversi");
            sleStates++;
        }
    }
    Check(sign is not null && sleStates == 19, "stati SLE: " + sleStates);
    measures["segno_Mx"] = sign!.Value;
    measures["sle_forme_chiuse"] = sleStates;

    // ------------------------------------------------------------ A2. SLU, resistenza a rottura a N assegnato
    int ulsStates = 0;
    foreach (var c in expected["slu"]!.AsArray().Select(n => n!.AsObject()))
    {
        string id = c["id"]!.GetValue<string>();
        var spec = Spec.From(expected["sezioni"]![c["sezione"]!.GetValue<string>()]!.AsObject());
        var p = Prep.Build(spec, c.D("fck"), c["normativa"]!.GetValue<string>(), c.D("alpha_cc"), c.D("gamma_c"), c.D("gamma_s"), c["diagramma"]!.GetValue<string>());
        double axial = c.D("N_kN");
        var legacy = SectionMomentResistance.Calculate(p.Input, p.Settings, axial, false);
        var library = p.Library(false, 0, false);
        library.Checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
        foreach (var a in c["azioni"]!.AsArray().Select(n => n!.AsObject()))
        {
            double mrd = a.D("MRd_kNm"); string at = id + " " + a["segno"]!.GetValue<string>();
            double direction = sign.Value * Math.Sign(mrd);
            var principal = legacy.Single(r => r.Direction == (direction > 0 ? "Mx+" : "Mx−"));
            var point = library.Checker.SectionSolver.CalculateDomainPoint([Prep.Force(library.Local, new ActionPoint(axial, direction, 0))])[0];
            Check(point is not null, at + ": punto resistente della libreria assente");
            double libraryMoment = point!.MxRd / 1e6;
            // Tolleranza su N del legacy (SectionMomentResistance.AxialToleranceKn): max(1 kN; 1e-6 |N|).
            Expect(at + " libreria", "SLU |NRd − N| / tolleranza del legacy", Math.Abs(point.NRd / 1000 - axial) / SectionMomentResistance.AxialToleranceKn(axial), 0, 1, 1);
            Max("SLU |MyRd| / |MxRd|", Math.Abs(point.MyRd / point.MxRd));
            if (principal.Moment is null)
            {
                string key = at + " legacy · SLU resistenza assente";
                string text = $"{key}: «{principal.Status}»; libreria NRd = {point.NRd / 1000:G10} kN, MxRd = {libraryMoment:G10} kNm, MyRd = {point.MyRd / 1e6:G6} kNm, atteso {mrd:G10} kNm";
                if (divergences.Contains(key)) registered.Add(text); else problems.Add(text);
                Expect(at + " libreria", "SLU MRd " + c["diagramma"], Math.Abs(libraryMoment), Math.Abs(mrd), Math.Abs(mrd), UlsTolerance);
                ulsStates++;
                continue;
            }
            Expect(at + " legacy", "SLU MRd " + c["diagramma"], Math.Abs(principal.Moment!.Value), Math.Abs(mrd), Math.Abs(mrd), UlsTolerance);
            Expect(at + " libreria", "SLU MRd " + c["diagramma"], Math.Abs(libraryMoment), Math.Abs(mrd), Math.Abs(mrd), UlsTolerance);
            Check(CloseTo(principal.Moment!.Value, libraryMoment), at + $": legacy {principal.Moment} e libreria {libraryMoment} diversi");
            ulsStates++;
        }
    }
    Check(ulsStates == 25, "stati SLU: " + ulsStates);
    measures["slu_forme_chiuse"] = ulsStates;

    // ------------------------------------------------------------ B. soglie
    var r1 = Spec.From(expected["sezioni"]!["R1"]!.AsObject()); var r2 = Spec.From(expected["sezioni"]!["R2"]!.AsObject());
    var r3 = Spec.From(expected["sezioni"]!["R3"]!.AsObject()); var c1 = Spec.From(expected["sezioni"]!["C1"]!.AsObject());
    int s = sign.Value; // Mx = s · M comprime il lembo superiore
    // Punti attorno alla soglia: i due motori devono coincidere in tutti; il ramo atteso si controlla a ±1e-4, sopra il rumore del solutore
    // di sezione (soluzione elastica fessurata con precisione relativa di circa 1e-6, vedi misura.json).
    double[] around = [-1e-4, -1e-6, -1e-9, 0, 1e-9, 1e-6, 1e-4];
    const double Side = 1e-4;
    void Threshold(string id, string parameter, double value, string note) => thresholds.Add(J.Obj(("id", id), ("parametro", parameter), ("valore", value), ("nota", note)));

    // B1. SLE: rapporto tensionale ≈ 1 (lineare: soglia esatta dalla proporzionalità; non lineare: bisezione).
    int stressPoints = 0;
    foreach (var (id, spec, standard, fck, n, m, set, thin, linear) in new[] {
        ("B1-cls-rara", r2, "NTC 2018", 35.0, -800.0, 150.0, "SLE", false, true), ("B1-acciaio-rara", r1, "NTC 2018", 30.0, 0.0, -100.0, "SLE", false, true),
        ("B1-cls-qp", r2, "NTC 2018", 35.0, -800.0, 150.0, "SLE_QP", false, true), ("B1-getto-sottile-qp", r2, "NTC 2018", 35.0, -800.0, 150.0, "SLE_QP", true, true),
        ("B1-getto-sottile-rara", r1, "NTC 2018", 30.0, -300.0, 120.0, "SLE", true, true), ("B1-EN-rara", r2, "EN 1992-1-1", 35.0, -800.0, 150.0, "SLE", false, true),
        ("B1-MC-qp", c1, "Model Code 2010", 30.0, -500.0, 100.0, "SLE_QP", false, true), ("B1-DIN-rara", r1, "DIN EN 1992-1-1", 30.0, 0.0, 100.0, "SLE", false, true),
        ("B1-non-lineare-rara", r2, "NTC 2018", 35.0, 0.0, 200.0, "SLE", false, false), ("B1-non-lineare-qp", r1, "NTC 2018", 30.0, -300.0, 120.0, "SLE_QP", false, false) })
    {
        var p = Prep.Build(spec, fck, standard, .85, 1.5, 1.15, thin: thin);
        var options = p.Options(set, linear);
        var engine = p.Engine(options);
        var library = p.Library(linear, 0, false);
        var combination = Prep.Combination(set);
        double reduction = thin ? .8 : 1;
        (double Legacy, double Library, string Status) Ratio(double k)
        {
            var action = new ActionPoint(k * n, s * k * m, 0);
            var a = engine.Stress(action, set);
            var b = StressLimitCheck.Evaluate(library.Checker.GetTensionAnalysisResult(Prep.Force(library.Local, action)), combination, reduction);
            return (a.Ratio!.Value, b.Ratio!.Value, a.Status);
        }
        // Stima dalla proporzionalità (esatta in teoria per l'analisi lineare: asse neutro fisso con l'eccentricità), poi bisezione sul legacy.
        double estimate = 1 / Ratio(1).Legacy;
        if (linear) Max("SLE non proporzionalità |rapporto(1/rapporto(1)) − 1|", Math.Abs(Ratio(estimate).Legacy - 1));
        double lo = .98 * estimate, hi = 1.02 * estimate;
        while (Ratio(hi).Legacy <= 1) hi *= 1.02;
        while (Ratio(lo).Legacy > 1) lo *= .9;
        double star = Bisect(k => Ratio(k).Legacy - 1, lo, hi);
        Threshold(id, "λ (azione × λ)", star, linear ? "bisezione; stima 1/rapporto(1) = " + F(estimate) : "bisezione");
        foreach (double d in around)
        {
            var (a, b, status) = Ratio(star * (1 + d));
            string at = $"{id} λ*(1{d:+0.0E+0;-0.0E+0;+0})";
            Agree(CloseTo(a, b), $"{at}: rapporto {a:R} legacy, {b:R} libreria");
            Agree((a <= 1) == (b <= 1), at + ": esito diverso");
            Agree(status == (a <= 1 ? "Entro limiti tensionali" : "Oltre limiti tensionali"), at + ": stato del legacy " + status);
            if (Math.Abs(d) >= Side) Agree((a <= 1) == (d < 0), $"{at}: esito {a:R} dal lato sbagliato della soglia");
            stressPoints++;
        }
    }

    // B2-B9. Fessurazione: ampiezza ≈ limite e cambi di ramo, legacy contro SectionCrackCheck.
    int crackPoints = 0;
    var crackOutcomes = new SortedDictionary<string, int>();
    // Confronto di uno stato: stessi numeri, stesso esito, stesso caso senza ampiezza.
    (Ntc2018Checks.CrackResult Legacy, SectionCrackResult Library) Crack(Prep p, JsonObject options, string set, ActionPoint action, string at)
    {
        var legacy = p.LegacyCrack(options, set, action);
        var library = p.LibraryCrack(options, set, action);
        bool Same(double? x, double? y) => x is null ? y is null : y is double yy && CloseTo(x.Value, yy);
        Agree(Same(legacy.Width, library.Width), $"{at}: wk {legacy.Width} legacy, {library.Width} libreria ({legacy.Status} / {library.Status})");
        Agree(Same(legacy.Limit, library.Limit) && Same(legacy.Ratio, library.Ratio), $"{at}: wlim o rapporto diversi ({legacy.Ratio} / {library.Ratio})");
        Agree(legacy.Passed == library.Passed, $"{at}: esito {legacy.Passed} legacy, {library.Passed} libreria ({legacy.Status} / {library.Status})");
        Agree(Same(legacy.EffectiveArea, library.EffectiveArea) && Same(legacy.EffectiveSteel, library.EffectiveSteel), $"{at}: Ac,eff o As,eff diversi");
        Agree(legacy.Regions.Length == library.Regions.Count && legacy.Regions.Zip(library.Regions).All(z => Same(z.First.Width, z.Second.Width) && CloseTo(z.First.Area, z.Second.Area)),
            $"{at}: regioni diverse");
        if (legacy.Width is null && legacy.Passed is null) Agree(LegacyOutcome(legacy.Status) == library.Outcome, $"{at}: esito «{legacy.Status}» contro {library.Outcome}");
        string kind = Kind(legacy.Status);
        crackOutcomes[kind] = crackOutcomes.GetValueOrDefault(kind) + 1;
        crackPoints++;
        return (legacy, library);
    }
    JsonObject CrackOptions(Prep p, string set, string exposure, string sensitivity = "Poco sensibile", string limit = "", string cover = "40", string spacing = "", string duration = "Lunga")
    {
        var o = p.Options(set);
        o["esposizione"] = exposure; o["sensibilita"] = sensitivity; o["durata"] = duration; o["aderenza"] = "Migliorata";
        o["copriferro_fessure"] = cover; o["spaziatura_fessure"] = spacing; o["limite_fessure"] = limit;
        return o;
    }
    double Trace(Ntc2018Checks.CrackResult r, string symbol) => r.Details.Last(d => d.Symbol == symbol).Value!.Value;
    // Valuta i due motori attorno alla soglia; 'side' controlla il ramo ai due lati (prima e dopo la soglia).
    void Around(string id, Func<double, (Prep P, JsonObject Options, string Set, ActionPoint Action)> at, double star, Action<double, Ntc2018Checks.CrackResult, SectionCrackResult>? side = null)
    {
        foreach (double d in around)
        {
            var (p, options, set, action) = at(star * (1 + d));
            var (legacy, library) = Crack(p, options, set, action, $"{id} λ*(1{d:+0.0E+0;-0.0E+0;+0})");
            if (Math.Abs(d) >= Side) side?.Invoke(d, legacy, library);
        }
    }

    // B2. wk ≈ wlim su tutte le norme ordinarie (limite numerico della norma o di progetto), flessione semplice di R3.
    foreach (var standard in ConcreteStandards.OrdinaryNames)
    {
        var p = Prep.Build(r3, 30, standard, .85, 1.5, 1.15);
        string set = "SLE_QP", limit = standard == "Model Code 2010" ? "0.3" : "";
        var options = CrackOptions(p, set, "XC3", limit: limit);
        (Prep, JsonObject, string, ActionPoint) State(double k) => (p, options, set, new ActionPoint(0, s * k, 0));
        double wlim = Crack(p, options, set, new ActionPoint(0, s * 100, 0), standard + " prova").Legacy.Limit!.Value;
        double star = Bisect(k => p.LegacyCrack(options, set, new ActionPoint(0, s * k, 0)).Width!.Value - wlim, 10, 400);
        Threshold("B2-wk-wlim " + standard, "Mx (kNm)", s * star, "wk = wlim = " + F(wlim) + " mm");
        Around("B2-wk-wlim " + standard, State, star, (d, legacy, _) => Agree(legacy.Passed == d < 0, standard + ": esito dal lato sbagliato di wk = wlim"));
    }

    // B3. εsm − εcm: soglia fra il minimo 0,6 σs/Es (DIN, EN, NTC) o (1 − kt) σs/Es (MC) e il valore calcolato; flessione semplice di R3.
    // Ramo dai numeri della traccia del legacy: (σs − kt fct,eff (1 + αe ρ)/ρ)/Es contro βmin σs/Es; stima con la proporzionalità
    // σs* = kt fct,eff (1 + αe ρ) / (ρ (1 − βmin)) da M = 1 kNm, poi bisezione.
    foreach (var standard in new[] { "NTC 2018", "EN 1992-1-1", "Model Code 2010", "DS EN 1992-1-1" })
    {
        var p = Prep.Build(r3, 30, standard, .85, 1.5, 1.15);
        string limit = standard == "Model Code 2010" ? "0.3" : "";
        var options = CrackOptions(p, "SLE_QP", "XC3", limit: limit);
        var unit = Crack(p, options, "SLE_QP", new ActionPoint(0, s, 0), standard + " 1 kNm").Legacy;
        double sigma = Trace(unit, "σs"), rho = Trace(unit, "ρp,eff"), alpha = Trace(unit, "αe"), kt = Trace(unit, "kt"), beta = Trace(unit, "β minimo deformazione");
        double fct = ((ConcreteMaterialEuropeanCommon)p.LibrarySection().ConcreteMaterial).Fctm;
        double estimate = kt * fct * (1 + alpha * rho) / (rho * (1 - beta)) / sigma;
        double Branch(double k)
        {
            var r = p.LegacyCrack(options, "SLE_QP", new ActionPoint(0, s * k, 0));
            double sk = Trace(r, "σs"), rk = Trace(r, "ρp,eff");
            return (sk - kt * fct / rk * (1 + alpha * rk)) - beta * sk;
        }
        double star = Bisect(Branch, .99 * estimate, 1.01 * estimate);
        // Proporzionalità della soluzione lineare fessurata fra 1 kNm e la soglia (σs letto dalla traccia).
        Max("SLE non proporzionalità σs(λ)/(λ σs(1 kNm)) − 1", Math.Abs(Trace(p.LegacyCrack(options, "SLE_QP", new ActionPoint(0, s * star, 0)), "σs") / (star * sigma) - 1));
        Threshold("B3-deformazione-minima " + standard, "Mx (kNm)", s * star, "bisezione; stima dalla proporzionalità " + F(s * estimate));
        Around("B3-deformazione-minima " + standard, k => (p, options, "SLE_QP", new ActionPoint(0, s * k, 0)), star, (d, legacy, _) =>
        {
            double strain = Trace(legacy, "εsm − εcm"), sigmaS = Trace(legacy, "σs"), es = 200000;
            Agree(d < 0 ? CloseTo(strain, beta * sigmaS / es) : strain > beta * sigmaS / es * (1 + 1e-12),
                $"B3 {standard} {d:+0.0E+0;-0.0E+0}: ramo di εsm − εcm dal lato sbagliato (εsm − εcm = {strain:R}, βmin σs/Es = {beta * sigmaS / es:R}, σs = {sigmaS:R})");
        });
    }

    // B4. Interasse s = 5 (c + Ø/2) (EN (7.14) 1,3 (h − x), NTC 0,75 (h − x)): interasse e copriferro assegnati, s* = 5 (40 + 10) = 250 mm.
    foreach (var standard in new[] { "NTC 2018", "EN 1992-1-1", "UNI EN 1992-1-1", "DIN EN 1992-1-1", "NS EN 1992-1-1" })
    {
        var p = Prep.Build(r3, 30, standard, .85, 1.5, 1.15);
        const double star = 5 * (40 + 20 / 2.0);
        var widths = new List<double>();
        Around("B4-interasse " + standard, k => (p, CrackOptions(p, "SLE_QP", "XC3", spacing: F(k)), "SLE_QP", new ActionPoint(0, s * 120, 0)), star,
            (d, legacy, _) => widths.Add(legacy.Width!.Value));
        Threshold("B4-interasse " + standard, "s (mm)", star, "5 (c + Ø/2)");
        Agree(widths.Count == 2 && widths[1] > widths[0] * (1 + 1e-6), standard + ": nessun salto di wk a s = 5 (c + Ø/2): " + string.Join(", ", widths));
    }

    // B5-B8. Asse neutro che sale con la compressione (Mx fisso, N variabile, R3 NTC): hc,eff da 2,5 (h − d) a (h − x)/3; barre tese fuori da
    // Ac,eff (limite superiore (7.14)); asse neutro sotto le barre (copriferro, wk = 0); sezione interamente compressa.
    {
        var p = Prep.Build(r3, 30, "NTC 2018", .85, 1.5, 1.15);
        var options = CrackOptions(p, "SLE_QP", "XC3");
        (Prep, JsonObject, string, ActionPoint) State(double n) => (p, options, "SLE_QP", new ActionPoint(-n, s * 100, 0));
        Ntc2018Checks.CrackResult Legacy(double n) => p.LegacyCrack(options, "SLE_QP", new ActionPoint(-n, s * 100, 0));
        // Rami in ordine di compressione crescente: 0 barre tese in Ac,eff, 1 nessuna barra in Ac,eff, 2 asse neutro nel copriferro, 3 sezione compressa.
        int Stage(double n) => Legacy(n).Status switch
        {
            var t when t.StartsWith("Sezione interamente compressa") => 3, var t when t.StartsWith("Asse neutro nel copriferro") => 2,
            var t when t.StartsWith("Nessuna barra in Ac,eff") => 1, _ => 0
        };
        double hc = Bisect(n => { var r = Legacy(n); return Trace(r, "Candidato 1 hc,eff") - Trace(r, "Candidato 2 hc,eff"); }, 1, 300);
        Threshold("B5-hc,eff", "N (kN, compressione)", -hc, "2,5 (h − d) = (h − x)/3");
        Around("B5-hc,eff", State, hc, (d, legacy, _) => Agree(CloseTo(Trace(legacy, "hc,eff"), d < 0 ? Trace(legacy, "Candidato 1 hc,eff") : Trace(legacy, "Candidato 2 hc,eff")), "B5: ramo di hc,eff"));
        Agree(Stage(3000) == 3, "B5-B8: la sezione non è interamente compressa con N = −3000 kN");
        foreach (var (stage, id, note) in new[] { (1, "B6-barre-fuori-Ac,eff", "hc,eff = h − d delle barre tese"), (2, "B7-asse-neutro-alle-barre", "asse neutro al livello delle barre tese"),
            (3, "B8-sezione-compressa", "asse neutro al lembo teso") })
        {
            double star = Bisect(n => Stage(n) >= stage ? 1 : -1, hc, 3000);
            Threshold(id, "N (kN, compressione)", -star, note);
            Around(id, State, star, (d, legacy, _) =>
            {
                int reached = Stage(star * (1 + d));
                Agree(d < 0 ? reached == stage - 1 : reached == stage, $"{id}: ramo {reached} ({legacy.Status})");
                if (stage >= 2 && d > 0) Agree(legacy.Width == 0 && legacy.Passed == true, id + ": wk = 0 atteso");
            });
        }
    }

    // B9. Asse neutro al lembo compresso con trazione crescente (R2 armata sui due lembi, Mx fisso): da flessione (k2 = 0,5) a trazione
    // su tutta la sezione (k2 = (ε1 + ε2)/(2 ε1)); NTC ed EN.
    foreach (var standard in new[] { "NTC 2018", "EN 1992-1-1" })
    {
        var p = Prep.Build(r2, 35, standard, .85, 1.5, 1.15);
        var options = CrackOptions(p, "SLE_QP", "XC3");
        (Prep, JsonObject, string, ActionPoint) State(double n) => (p, options, "SLE_QP", new ActionPoint(n, s * 60, 0));
        var vertices = p.LibrarySection().ConcreteShape.GetPoints2d();
        double star = Bisect(n => vertices.Min(p.Engine(options).Stress(new ActionPoint(n, s * 60, 0), "SLE_QP").Native.StrainPlane.GetStrain), 1, 5000);
        Threshold("B9-trazione-totale " + standard, "N (kN, trazione)", star, "εc,min = 0 al lembo compresso");
        Around("B9-trazione-totale " + standard, State, star, (d, legacy, library) =>
            Agree(d < 0 ? library.K2 == .5 : library.K2 != .5, $"B9 {standard}: k2 {library.K2} dal lato sbagliato ({legacy.Status})"));
    }

    // B10. Decompressione (NTC XD1 armature sensibili, quasi permanente: σct,max ≤ 0) e formazione delle fessure (XD3 sensibili,
    // frequente: σct,max ≤ fctm/1,2) sulla sezione omogeneizzata interamente reagente; N fisso, Mx variabile.
    foreach (var (id, set, exposure) in new[] { ("B10-decompressione", "SLE_QP", "XD1"), ("B10-formazione", "SLE_FREQ", "XD3") })
    {
        var p = Prep.Build(r2, 35, "NTC 2018", .85, 1.5, 1.15);
        var options = CrackOptions(p, set, exposure, "Sensibile");
        (Prep, JsonObject, string, ActionPoint) State(double m) => (p, options, set, new ActionPoint(-600, s * m, 0));
        double star = Bisect(m => p.LegacyCrack(options, set, new ActionPoint(-600, s * m, 0)).Passed == true ? -1 : 1, 1, 2000);
        Threshold(id, "Mx (kNm)", s * star, set + " " + exposure + " sensibile");
        Around(id, State, star, (d, legacy, _) => Agree(legacy.Passed == d < 0, id + ": esito dal lato sbagliato " + legacy.Status));
    }

    // B11. SLU: rapporto ≈ 1 sul dominio 3D del modulo (CheckerDomain3D.Check) contro SectionSolver.CalculateDomainPoint della libreria.
    int ulsPoints = 0;
    foreach (var (spec, axial, label) in new[] { (r1, -500.0, "R1"), (c1, -800.0, "C1") })
    {
        var p = Prep.Build(spec, 30, "NTC 2018", .85, 1.5, 1.15);
        var options = J.Obj(("criterio", "N costante"), ("assi", "Locali"), ("strategia", "Iterativo"), ("modello", "Non lineare"));
        var domain = new CheckerSection(p.Input, p.Settings, options, "SLU").Domain3D();
        domain.ConfigureVerification(options);
        var library = p.Library(false, 0, false);
        library.Checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
        foreach (double direction in new[] { 1.0, -1.0 })
        {
            var unitForce = Prep.Force(library.Local, new ActionPoint(axial, direction, 0));
            double mrd = library.Checker.SectionSolver.CalculateDomainPoint([unitForce])[0]!.MxRd / 1e6;
            Threshold("B11-SLU " + label + (direction > 0 ? " Mx+" : " Mx−"), "Mx (kNm)", mrd, "N = " + F(axial) + " kN");
            foreach (double d in around)
            {
                var action = new ActionPoint(axial, mrd * (1 + d), 0); string at = $"B11 {label} {direction} λ*(1{d:+0.0E+0;-0.0E+0;+0})";
                var legacy = domain.CheckMany([action])[0]; // percorso del modulo (ConcreteAnalysisSession.Domain)
                var force = Prep.Force(library.Local, action);
                var point = library.Checker.SectionSolver.CalculateDomainPoint([force])[0]!;
                double ratio = point.CalculateWorkingRatio(SectionSolver.FailureAnalysisTypes.ConstantN, force, 1e6, 1000);
                Agree(legacy.Utilization is double lr && CloseTo(lr, ratio), $"{at}: rapporto {legacy.Utilization} legacy, {ratio} libreria");
                Agree(legacy.Status == (ratio <= 1 ? "Entro il dominio" : "Fuori dominio"), at + ": stato " + legacy.Status);
                if (Math.Abs(d) >= Side) Agree((ratio <= 1) == d < 0, $"{at}: rapporto {ratio:R} dal lato sbagliato");
                ulsPoints++;
            }
        }
    }

    // B12. Taglio: soglie dei rami di ConcreteCodeChecks.Shear (legacy) contro SectionShearCalculator attraverso l'adattatore.
    int shearPoints = 0;
    void Shear(string at, ConcreteCodeChecks.ShearInput c)
    {
        (string Outcome, Ntc2018Checks.ShearResult? R) Run(ShearTorsionEngine e)
        { try { return ("ok", ConcreteShearTorsionAdapter.Shear(c, e)); } catch (ArgumentException ex) { return ("rifiuto: " + ex.Message, null); } }
        var a = Run(ShearTorsionEngine.Legacy); var b = Run(ShearTorsionEngine.Library);
        Agree(a.Outcome == b.Outcome, $"{at}: {a.Outcome} legacy, {b.Outcome} libreria");
        if (a.R is { } x && b.R is { } y)
        {
            Agree(CloseTo(x.VRsd, y.VRsd) && CloseTo(x.VRcd, y.VRcd) && CloseTo(x.VRd, y.VRd) && CloseTo(x.CotTheta, y.CotTheta), $"{at}: resistenze {x.VRd} / {y.VRd}");
            Agree(x.Ratio is null ? y.Ratio is null : y.Ratio is double yr && CloseTo(x.Ratio.Value, yr), $"{at}: rapporti {x.Ratio} / {y.Ratio}");
            Agree(x.Status == y.Status, $"{at}: stato «{x.Status}» / «{y.Status}» (rapporti {x.Ratio:R} / {y.Ratio:R}, VRd {x.VRd:R} / {y.VRd:R} kN, VEd {c.V:R} kN)");
        }
        shearPoints++;
    }
    void ShearAround(string id, Func<double, ConcreteCodeChecks.ShearInput> at, double star, Action<double, Ntc2018Checks.ShearResult>? side = null)
    {
        foreach (double d in around)
        {
            var c = at(star * (1 + d));
            Shear($"{id} λ*(1{d:+0.0E+0;-0.0E+0;+0})", c);
            if (Math.Abs(d) >= Side && side is not null) side(d, ConcreteCodeChecks.Shear(c));
        }
    }
    foreach (var standard in ConcreteStandards.OrdinaryNames)
    {
        var plain = new ConcreteCodeChecks.ShearInput(standard, -100, 80, 60, 300 * 500, 300, 450, 942.5, 30, .85 * 30 / 1.5, 391.3, 1.5, 200000, 0, 150);
        var links = plain with { Asw = 100.53, V = 200 };
        // η ≈ 1 senza e con staffe: VEd* per bisezione sul legacy (con MC2010 e DIN la resistenza dipende anche da VEd).
        foreach (var (kind, c) in new[] { ("senza staffe", plain), ("con staffe", links) })
        {
            double vrd = ConcreteCodeChecks.Shear(c).VRd;
            // VEd uguale alla resistenza calcolata dal legacy con lo stesso VEd (η = 1 esatto se VRd non dipende da VEd).
            Shear($"B12-η-esatto {standard} {kind}", c with { V = ConcreteCodeChecks.Shear(c with { V = vrd }).VRd });
            double v = Bisect(x => ConcreteCodeChecks.Shear(c with { V = x }).Ratio <= 1 ? -1 : 1, .5 * vrd, 2 * vrd);
            Threshold($"B12-η {standard} {kind}", "VEd (kN)", v, "VEd = VRd(VEd); VRd a VEd di partenza " + F(vrd));
            ShearAround($"B12-η {standard} {kind}", x => c with { V = x }, v, (d, r) => Agree((r.Ratio <= 1) == d < 0, $"B12-η {standard} {kind} {d:+0.0E+0;-0.0E+0}: esito del taglio dal lato sbagliato (rapporto {r.Ratio:R})"));
        }
        // Senza staffe: minimo vmin contro la formula con ρl (bisezione su Asl); tetti di k (d = 200 mm), di ρl (0,02) e di σcp (0,2 fcd).
        if (standard != "Model Code 2010")
        {
            double asl = Bisect(x => { var r = ConcreteCodeChecks.Shear(plain with { Asl = x }); return r.VRsd - r.VRcd; }, 1, 2700);
            Threshold("B12-vmin " + standard, "Asl (mm²)", asl, "VRd,c formula = minimo");
            ShearAround("B12-vmin " + standard, x => plain with { Asl = x }, asl, (d, r) => Agree((r.VRsd > r.VRcd) == d > 0, standard + ": ramo di vmin"));
            ShearAround("B12-k " + standard, x => plain with { D = x, Area = 300 * (x + 50) }, 200);
            ShearAround("B12-ρl " + standard, x => plain with { Asl = x }, .02 * 300 * 450);
            ShearAround("B12-σcp " + standard, x => plain with { N = x }, -.2 * (.85 * 30 / 1.5) * 300 * 500 / 1000);
        }
        // Con staffe e cot θ ottimizzato: limite superiore di cot θ (bisezione su Asw).
        var auto = links with { CotTheta = null, N = 0 };
        double top = ConcreteCodeChecks.Shear(auto with { Asw = 1e-3 }).CotTheta;
        double asw = Bisect(x => ConcreteCodeChecks.Shear(auto with { Asw = x }).CotTheta < top - 1e-9 ? 1 : -1, 1e-3, 5000);
        Threshold("B12-cot-max " + standard, "Asw (mm²)", asw, "cot θ ottimo = " + F(top));
        ShearAround("B12-cot-max " + standard, x => auto with { Asw = x }, asw, (d, r) => Agree((r.CotTheta < top - 1e-9) == d > 0, standard + ": ramo di cot θ"));
    }
    // NTC: αc ai cambi di ramo σcp = 0,25 fcd, 0,5 fcd e fcd (N di compressione sull'area 300 × 500).
    {
        var ntc = new ConcreteCodeChecks.ShearInput("NTC 2018", 0, 200, 60, 300 * 500, 300, 450, 942.5, 30, 17, 391.3, 1.5, 200000, 100.53, 150);
        foreach (double fraction in new[] { .25, .5, 1 })
            ShearAround("B12-αc NTC σcp = " + F(fraction) + " fcd", x => ntc with { N = x }, -fraction * 17 * 300 * 500 / 1000);
    }

    measures["soglie"] = thresholds;
    measures["punti"] = J.Obj(("sle", stressPoints), ("fessurazione", crackPoints), ("slu", ulsPoints), ("taglio", shearPoints));
    measures["esiti_fessurazione"] = J.Node(crackOutcomes);
    measures["scarti_massimi_forme_chiuse"] = J.Node(maxima.OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Value));
    measures["forme_chiuse"] = closedForms;
    measures["divergenze_registrate"] = J.Node(registered);
    measures["problemi"] = J.Node(problems);
    measures["controlli"] = count;
    if (args.Length > 0)
    {
        Directory.CreateDirectory(args[0]);
        var document = J.Obj(("strumento", "ConcreteBench.Checks"), ("attesi_sha256", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "attesi", "attesi.json"))))), ("misure", measures));
        File.WriteAllText(Path.Combine(args[0], "misura.json"), document.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }), new UTF8Encoding(false));
    }
    foreach (var text in registered) Console.WriteLine("DIVERGENZA REGISTRATA · " + text);
    foreach (var (key, value) in maxima.OrderBy(k => k.Key)) Console.WriteLine($"scarto massimo {key}: {value:G3}");
    if (problems.Count > 0)
    {
        foreach (var text in problems) Console.Error.WriteLine("DIVERGENZA NON REGISTRATA · " + text);
        Console.Error.WriteLine($"FAIL · {problems.Count} divergenze non registrate su {count} controlli");
        return 1;
    }
    Console.WriteLine($"PASS · {count} controlli: {sleStates} stati SLE e {ulsStates} resistenze SLU in forma chiusa; soglie: {stressPoints} SLE, {crackPoints} fessurazione, "
        + $"{ulsPoints} SLU, {shearPoints} taglio; {registered.Count} divergenze registrate.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    foreach (var text in problems) Console.Error.WriteLine("DIVERGENZA NON REGISTRATA · " + text);
    return 1;
}

// Bisezione su un cambio di segno di f fra lo e hi, fino alla precisione della macchina.
static double Bisect(Func<double, double> f, double lo, double hi)
{
    double flo = f(lo), fhi = f(hi);
    if (Math.Sign(flo) == Math.Sign(fhi) || flo == 0 || fhi == 0) throw new Exception($"bisezione: nessun cambio di segno fra {lo} ({flo}) e {hi} ({fhi})");
    for (int i = 0; i < 200 && hi - lo > 1e-15 * Math.Max(Math.Abs(lo), Math.Abs(hi)); i++)
    {
        double mid = (lo + hi) / 2, fm = f(mid);
        if (fm == 0) return mid;
        if (Math.Sign(fm) == Math.Sign(flo)) { lo = mid; flo = fm; } else hi = mid;
    }
    return (lo + hi) / 2;
}
static string Kind(string status) => status.Split(new[] { ':', '·', '(' }, 2)[0].Trim();
// Stati del legacy senza ampiezza e senza esito: lo stesso esito della libreria (mappa di CrackMigrationTests).
static CrackOutcome? LegacyOutcome(string status)
{
    if (status.StartsWith("Non richiesta")) return CrackOutcome.NotRequired;
    if (status.StartsWith("Selezionare la classe")) return CrackOutcome.MissingExposure;
    if (status.StartsWith("Selezionare wlim") || status.StartsWith("Selezionare esposizione")) return CrackOutcome.MissingDesignLimit;
    if (status.StartsWith("wk richiede")) return CrackOutcome.RequiresLinearCrackedAnalysis;
    if (status.StartsWith("Asse neutro non")) return CrackOutcome.NeutralAxisUndetermined;
    if (status.StartsWith("Nessuna armatura tesa")) return CrackOutcome.NoTensileReinforcement;
    return null;
}
static HashSet<string> LoadDivergences(string path)
{
    if (!File.Exists(path)) return [];
    var node = JsonNode.Parse(File.ReadAllText(path))!;
    return node["divergenze"]!.AsArray().Select(d => d!["chiave"]!.GetValue<string>()).ToHashSet();
}
static string Root()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "build", "ci.ps1"))) return dir.FullName;
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "build", "ci.ps1"))) return dir.FullName;
    throw new Exception("radice del repository non trovata");
}

// Sezione degli attesi: contorno centrato, barre (x, y, Ø).
sealed record Spec(string Shape, double B, double H, double D, int Sides, double[][] Bars)
{
    public static Spec From(JsonObject o) => new(o["forma"]!.GetValue<string>(), o.D("b_mm"), o.D("h_mm"), o.D("D_mm"), o["lati"] is null ? 0 : o["lati"]!.GetValue<int>(),
        o["barre"]!.AsArray().Select(b => b!.AsArray().Select(v => v!.GetValue<double>()).ToArray()).ToArray());
    public double Height => Shape == "Rettangolare" ? H : D;
    double Top => Height / 2;
    // Lembo con la deformazione minore (il più compresso o il meno teso), sulla verticale per il baricentro.
    public string LessStrainedEdge(StrainPlane plane) => plane.GetStrain(new Point2d(0, Top)) < plane.GetStrain(new Point2d(0, -Top)) ? "superiore" : "inferiore";
    // Quota dell'asse neutro sulla verticale per il baricentro (piano lineare: anche fuori dalla sezione).
    public double NeutralAxis(StrainPlane plane)
    {
        double top = plane.GetStrain(new Point2d(0, Top)), bottom = plane.GetStrain(new Point2d(0, -Top));
        return Top - Height * top / (top - bottom);
    }
}

// Sezione di ANTHEA (ingresso JSON del modulo c.a.) e, dagli stessi ingressi, il percorso diretto della libreria.
sealed class Prep
{
    public JsonObject Input { get; private init; } = new();
    public JsonObject Settings { get; private init; } = new();
    bool Thin { get; init; }
    static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    public static Prep Build(Spec s, double fck, string standard, double alphaCc, double gammaC, double gammaS, string diagram = "Parabola-rettangolo", bool thin = false)
    {
        var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        input["shape"] = s.Shape; input["foro_presente"] = false;
        if (s.Shape == "Rettangolare") { input["width_mm"] = F(s.B); input["height_mm"] = F(s.H); }
        else { input["diameter_mm"] = F(s.D); input["circular_sides"] = s.Sides.ToString(CultureInfo.InvariantCulture); }
        input["barre_manuali"] = new JsonArray(s.Bars.Select(b => (JsonNode)J.Obj(("x", b[0]), ("y", b[1]), ("phi", b[2]))).ToArray());
        input["fck_mpa"] = F(fck); input["fyk_mpa"] = "450"; input["steel_modulus_mpa"] = "200000"; input["steel_fu_mpa"] = "450"; input["steel_eps_u"] = "75";
        input["steel_diagramma"] = "Elastoplastico"; input["cls_diagramma"] = diagram;
        input["alpha_cc"] = F(alphaCc); input["gamma_c"] = F(gammaC); input["gamma_s"] = F(gammaS); input["gettato_sottile"] = thin ? "Sì" : "No";
        input["cover_mm"] = "30"; input["transverse_bar_diameter_mm"] = "10"; input["staffe_presenti"] = "Sì";
        settings["normativa"] = standard; settings["coefficienti"] = ConcreteStandards.Defaults(standard);
        ConcreteCalculationSettings.Prepare(input, settings);
        return new Prep { Input = input, Settings = settings, Thin = thin };
    }
    public JsonObject Options(string set, bool linear = true)
    {
        var o = (JsonObject)Settings["sle"]![set]!.DeepClone();
        o["modello"] = linear ? "Lineare" : "Non lineare"; o["phi"] = "0"; o["trazione_cls"] = "No"; o["angoli"] = "32";
        return o;
    }
    public static ServiceabilityCombination Combination(string set) => set == "SLE" ? ServiceabilityCombination.Characteristic
        : set == "SLE_QP" ? ServiceabilityCombination.QuasiPermanent : ServiceabilityCombination.Frequent;
    public static ResultBeamForces Force(CoordinateSystem local, ActionPoint a) => new(a.N * 1000, 0, 0, 0, a.Mx * 1e6, a.My * 1e6, local);
    public ReinforcedConcreteSection LibrarySection() => CheckerSection.PrepareModel(Input, Settings).Section;
    // Motori riusati fra le valutazioni (la costruzione della mesh è la parte lenta): uno per insieme di opzioni.
    readonly Dictionary<string, CheckerSection> engines = new();
    readonly Dictionary<(bool, double, bool), (ReinforcedConcreteSection, CoordinateSystem, SectionCheckerModelCode2010, StandardModelCode2010)> libraries = new();
    public CheckerSection Engine(JsonObject options)
    {
        string key = options.ToJsonString();
        if (!engines.TryGetValue(key, out var engine)) engines[key] = engine = new CheckerSection(Input, Settings, options);
        return engine;
    }
    public Ntc2018Checks.CrackResult LegacyCrack(JsonObject options, string set, ActionPoint action)
    {
        var engine = Engine(options);
        return Ntc2018Checks.Cracking(engine, engine.Stress(action, set), action, Input, Settings, options, set);
    }
    public (ReinforcedConcreteSection Section, CoordinateSystem Local, SectionCheckerModelCode2010 Checker, StandardModelCode2010 Standard) Library(bool linear, double psi, bool tension)
    {
        if (libraries.TryGetValue((linear, psi, tension), out var cached)) return cached;
        var model = CheckerSection.PrepareModel(Input, Settings);
        var standard = ConcreteStandards.Effective(Input, Settings);
        if (Thin && Settings.S("normativa") == "NTC 2018") standard.AlphaCC *= .8; // getti sottili NTC: 0,8 sulla resistenza del calcestruzzo, come il legacy
        var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(model.Local, SectionSolver.FailureAnalysisTypes.ConstantN, SectionSolver.FailureDomainTypes.Plastic,
            linear ? SectionSolver.StressAnalysisTypes.Linear : SectionSolver.StressAnalysisTypes.NonLinear, psi, 0, tension, 32);
        var result = (model.Section, model.Local, new SectionCheckerModelCode2010(new SectionCheckerAttribute(model.Section, null, null), options, standard, tension), standard);
        libraries[(linear, psi, tension)] = result;
        return result;
    }
    public SectionCrackResult LibraryCrack(JsonObject options, string set, ActionPoint action)
    {
        bool linear = options.S("modello") == "Lineare", tension = options.S("trazione_cls") == "Sì";
        double psi = SectionWorkspace.Number(options.S("phi"), "phi");
        var library = Library(linear, psi, tension);
        var state = library.Checker.GetTensionAnalysisResult(Force(library.Local, action));
        var concrete = (ConcreteMaterialEuropeanCommon)library.Section.ConcreteMaterial;
        double? Optional(string key) => options.S(key).Trim() == "" ? null : SectionWorkspace.Number(options.S(key), key);
        double Uncracked()
        {
            var u = Library(true, psi, true);
            var r = u.Checker.GetTensionAnalysisResult(Force(u.Local, action));
            return r.GetConcreteVerticesTension(r.PsiRebar ?? 0).Max(v => v.tension);
        }
        var input = new SectionCrackInput(library.Standard, Combination(set), options.S("esposizione") == "Da scegliere" ? null : options.S("esposizione"),
            options.S("sensibilita") == "Sensibile", Optional("limite_fessure"), CrackSectionGeometry.From(library.Section), state.StrainPlane,
            SectionCrackInput.OrdinaryBarStresses(state, library.Section), linear, tension, false, library.Section.Rebars.First().RebarMaterial.E, concrete.Ecm, concrete.Fctm,
            options.S("durata") == "Breve", options.S("aderenza") == "Migliorata", Input.D("cover_mm") + Input.D("transverse_bar_diameter_mm"),
            Optional("copriferro_fessure"), Optional("spaziatura_fessure"), Uncracked);
        return SectionCrackCheck.Evaluate(input);
    }
}
