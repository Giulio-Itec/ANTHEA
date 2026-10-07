using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Calculations;
using GPC.Checkers.Concrete.Shear;
using GPC.Checkers.Concrete.Torsion;
using TorsionGeometry = Anthea.Calculations.TorsionGeometry;

// Prove dell'adattatore di taglio e torsione verso GPCChecker.Concrete (refactoring F2.5-F2.6, docs/refactoring/piano.md).
//   dotnet ConcreteLibraryAdapter.Checks.dll [cartella]   (con la cartella scrive misura.json con i conteggi e gli scarti massimi)
// 1. Strato di mappatura: unità con nome, norme, nessuna costante normativa nei file di mappatura, testi senza traduzione.
// 2. Interruttore: motore predefinito atteso; il motore legacy dell'adattatore coincide bit per bit con il legacy diretto.
// 3. Equivalenza legacy → libreria sulle griglie della cattura densa (stessi generatori e semi di CheckerMigration.Capture) e sui
//    calcoli del modulo: stessi esiti, stessi rifiuti con lo stesso messaggio, stessi testi (stati, riferimenti, modelli, tracce),
//    numeri entro 1e-9 (|a − b| ≤ 1e-9 + 1e-9 · max(|a|, |b|), come le grandezze ca_fixture_* di tests/ANTHEA.Testing/tolerances.json).
//    3f: la torsione del modulo passa dall'adattatore con il motore richiesto (bit per bit, controllo che distingue i motori).
// 4. Attesi indipendenti (reference.json, benchmark e forme chiuse di taglio e torsione) sul percorso dell'adattatore, con entrambi i motori.
// Uscita 0 con la riga "PASS · …"; 1 con il primo controllo fallito.
const ShearTorsionEngine ExpectedDefault = ShearTorsionEngine.Library; // F2.5: legacy; F2.6: libreria
const double Tolerance = 1e-9;

int count = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
var italian = CultureInfo.GetCultureInfo("it-IT");
CultureInfo.CurrentCulture = italian; // cultura dell'applicazione: i messaggi con numeri formattati devono coincidere anche qui
var shearStats = new Stats("taglio"); var torsionStats = new Stats("torsione"); var geometryStats = new Stats("profilo resistente"); var moduleStats = new Stats("modulo");

(string Outcome, string Message, JsonNode? Json) Run(Func<object?> action)
{
    try { return ("ok", "", J.Node(action())); }
    catch (Exception e) { return ("error:" + e.GetType().Name, e.Message, null); }
}
// Legacy contro libreria: stesso esito e messaggio, testi uguali, numeri entro la tolleranza. Vero se il caso è un rifiuto.
bool Same(string id, Func<object?> legacy, Func<object?> library, Stats stats)
{
    var a = Run(legacy); var b = Run(library);
    Check(a.Outcome == b.Outcome, $"{id}: esito {a.Outcome} «{a.Message}» con il legacy, {b.Outcome} «{b.Message}» con la libreria");
    Check(a.Message == b.Message, $"{id}: messaggio «{a.Message}» con il legacy, «{b.Message}» con la libreria");
    if (a.Outcome != "ok") { stats.Rejected++; return true; }
    var errors = new List<string>();
    Json.Compare(a.Json, b.Json, id, stats, errors, Tolerance);
    Check(errors.Count == 0, string.Join(Environment.NewLine, errors.Take(5)));
    stats.Results++;
    return false;
}
// Motore legacy dell'adattatore contro il legacy diretto: identici bit per bit (stesso JSON) o stesso rifiuto.
void Identical(string id, Func<object?> direct, Func<object?> adapter)
{
    var a = Run(direct); var b = Run(adapter);
    Check(a.Outcome == b.Outcome && a.Message == b.Message, $"{id}: il motore legacy dell'adattatore rifiuta diversamente ({a.Message} / {b.Message})");
    Check(a.Json?.ToJsonString() == b.Json?.ToJsonString(), $"{id}: il motore legacy dell'adattatore non coincide con il legacy");
}

try
{
    string root = Root();

    // ---------------------------------------------------------------- 1. strato di mappatura
    // Nessuna costante normativa nei file di mappatura: fuori da testi e commenti solo i fattori delle conversioni con nome
    // (1000 N/kN, 1e6 N·mm/kNm) e, nell'adattatore, 0 (staffe assenti) e 2 (raggio = diametro / 2 della barra più grossa).
    // Letterali anche nella forma con il punto iniziale (.85, .9e-3), lo stile del codice legacy.
    static string[] Literals(string source)
    {
        string code = Regex.Replace(source, @"(?s)/\*.*?\*/|//[^\n]*|@""(?:[^""]|"""")*""|\$?""(?:[^""\\\n]|\\.)*""|'(?:[^'\\]|\\.)'", " ");
        // Un letterale comincia con una cifra o con il punto decimale, mai dopo una lettera, una cifra o un punto (identificatori,
        // accesso a membro); il primo ramo prende "1.5" intero, il secondo ".85".
        return Regex.Matches(code, @"(?<![\w.])(?:\d+(?:\.\d+)?|\.\d+)(?:[eE][-+]?\d+)?[dDfFmM]?(?![\w.])").Select(m => m.Value).Distinct().ToArray();
    }
    const string Sample = "double k = .85 * fck, x = a.Length, y = (.9 + 1e6) / 2.5m, w = Item2; // .7";
    Check(Literals(Sample).OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(new[] { ".85", ".9", "1e6", "2.5m" }.OrderBy(s => s, StringComparer.Ordinal)),
        "controllo dei letterali su un caso sintetico: trovati " + string.Join(", ", Literals(Sample)) + ", attesi .85, .9, 1e6, 2.5m");
    foreach (var (file, allowed) in new[] { ("X.Calculations/ConcreteLibraryMapping.cs", new[] { "1000", "1e6" }), ("X.Calculations/ConcreteShearTorsionAdapter.cs", new[] { "0", "2" }) })
    {
        var literals = Literals(File.ReadAllText(Path.Combine(root, file)));
        Check(literals.All(allowed.Contains), file + ": numeri non ammessi nel file di mappatura: " + string.Join(", ", literals.Except(allowed)));
    }
    Check(ConcreteLibraryMapping.NewtonsFromKilonewtons(1.25) == 1250 && ConcreteLibraryMapping.KilonewtonsFromNewtons(1250) == 1.25, "conversione kN ↔ N");
    Check(ConcreteLibraryMapping.NewtonMillimetresFromKilonewtonMetres(2.5) == 2.5e6 && ConcreteLibraryMapping.KilonewtonMetresFromNewtonMillimetres(2.5e6) == 2.5, "conversione kNm ↔ N·mm");
    var profiles = new Dictionary<string, (ShearProfile Shear, TorsionProfile Torsion)>
    {
        ["NTC 2018"] = (ShearProfile.Ntc2018, TorsionProfile.Ntc2018), ["Model Code 2010"] = (ShearProfile.ModelCode2010, TorsionProfile.ModelCode2010),
        ["EN 1992-1-1"] = (ShearProfile.EN1992p11, TorsionProfile.EN1992p11), ["UNI EN 1992-1-1"] = (ShearProfile.UniEN1992p11, TorsionProfile.UniEN1992p11),
        ["DIN EN 1992-1-1"] = (ShearProfile.DinEN1992p11, TorsionProfile.DinEN1992p11), ["DS EN 1992-1-1"] = (ShearProfile.DsEN1992p11, TorsionProfile.DsEN1992p11),
        ["NS EN 1992-1-1"] = (ShearProfile.NsEN1992p11, TorsionProfile.NsEN1992p11)
    };
    Check(profiles.Keys.OrderBy(k => k).SequenceEqual(ConcreteStandards.OrdinaryNames.OrderBy(k => k)), "norme ordinarie del modulo e della mappa diverse");
    foreach (var (name, expected) in profiles)
    {
        var standard = ConcreteLibraryMapping.StandardFor(name);
        Check(ShearProfiles.Resolve(standard) == expected.Shear && TorsionProfiles.Resolve(standard) == expected.Torsion, name + ": profilo della libreria");
    }
    foreach (var name in ConcreteStandards.Names.Except(ConcreteStandards.OrdinaryNames).Append("ACI 318"))
    {
        string legacy = Run(() => { ConcreteCodeChecks.RequireOrdinary(name); return null; }).Message;
        var mapped = Run(() => ConcreteLibraryMapping.StandardFor(name));
        Check(mapped.Outcome == "error:ArgumentException" && mapped.Message == legacy && legacy.Length > 0, name + ": rifiuto della norma diverso dal legacy");
    }
    // Un testo della libreria senza traduzione è un errore di programma, mai un testo inglese mostrato.
    Check(Run(() => ConcreteLibraryMapping.ShearError(new ArgumentException("Shear: new message."), "NTC 2018")).Outcome == "error:InvalidOperationException", "rifiuto del taglio non tradotto accettato");
    Check(Run(() => ConcreteLibraryMapping.TorsionError(new ArgumentException("Torsion: new message."))).Outcome == "error:InvalidOperationException", "rifiuto della torsione non tradotto accettato");

    // ---------------------------------------------------------------- 2. interruttore
    Check(ConcreteShearTorsionAdapter.Default == ExpectedDefault, $"motore predefinito {ConcreteShearTorsionAdapter.Default}, atteso {ExpectedDefault}");

    // ---------------------------------------------------------------- 3a. taglio, griglia di CheckerMigration.Capture (2016 casi)
    var shearCases = ShearGrid();
    Check(shearCases.Count == 2016, "griglia del taglio: " + shearCases.Count + " casi");
    for (int i = 0; i < shearCases.Count; i++)
    {
        var c = shearCases[i]; string id = "taglio " + i + " " + c.Standard;
        Identical(id, () => ConcreteCodeChecks.Shear(c), () => ConcreteShearTorsionAdapter.Shear(c, ShearTorsionEngine.Legacy));
        Same(id, () => ConcreteCodeChecks.Shear(c), () => ConcreteShearTorsionAdapter.Shear(c, ShearTorsionEngine.Library), shearStats);
    }
    // Messaggi con gli estremi di cot θ formattati: anche nella cultura invariante.
    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    foreach (var c in shearCases.Where(c => c.CotTheta is > 2.5 or < 1).Take(40))
        Same("taglio (cultura invariante) " + c.Standard, () => ConcreteCodeChecks.Shear(c), () => ConcreteShearTorsionAdapter.Shear(c, ShearTorsionEngine.Library), shearStats);
    CultureInfo.CurrentCulture = italian;

    // ---------------------------------------------------------------- 3b. torsione, griglia di CheckerMigration.Capture (986 casi)
    var torsionCases = TorsionGrid();
    Check(torsionCases.Count == 986, "griglia della torsione: " + torsionCases.Count + " casi");
    for (int i = 0; i < torsionCases.Count; i++)
    {
        var c = torsionCases[i]; string id = "torsione " + i;
        Identical(id, () => new ConcreteTorsionCalculator().Calculate(c), () => ConcreteShearTorsionAdapter.Torsion(c, 30, 1.5, ShearTorsionEngine.Legacy));
        Same(id, () => new ConcreteTorsionCalculator().Calculate(c), () => ConcreteShearTorsionAdapter.Torsion(c, 30, 1.5, ShearTorsionEngine.Library), torsionStats);
        // Per NTC fck e γc non entrano nelle resistenze della libreria.
        if (i % 7 == 0) Same(id + " fck 45", () => new ConcreteTorsionCalculator().Calculate(c), () => ConcreteShearTorsionAdapter.Torsion(c, 45, 1.5, ShearTorsionEngine.Library), torsionStats);
    }
    // Senza staffe chiuse: rifiuto del legacy anche quando il cot θ del taglio è diverso (il controllo dell'adattatore viene prima).
    var g0 = new TorsionGeometry(82416, 1216, 96);
    Ntc2018Checks.ShearResult Synthetic(double rsd, double rcd, double cot) => new(rsd, rcd, Math.Min(rsd, rcd), null, cot, "synthetic");
    var noLinks = new TorsionInput(40, g0, 14.17, 391.3, 0, 150, 800, 1.5, 100, 0, Synthetic(400, 900, 2), Synthetic(250, 700, 1.5));
    Check(Same("torsione senza staffe e cot diverso", () => new ConcreteTorsionCalculator().Calculate(noLinks), () => ConcreteShearTorsionAdapter.Torsion(noLinks, 30, 1.5, ShearTorsionEngine.Library), torsionStats),
        "torsione senza staffe accettata");
    // Rifiuto della sola libreria (classe oltre C90/105), non raggiungibile dal modulo: il taglio della stessa sezione rifiuta prima (prova 3d).
    var valid = torsionCases[0] with { TorqueKnM = 40 };
    Check(Run(() => ConcreteShearTorsionAdapter.Torsion(valid, 95, 1.5, ShearTorsionEngine.Library)).Message == ConcreteLibraryMapping.TorsionConcreteClassMessage, "torsione oltre C90/105");

    // ---------------------------------------------------------------- 3c. profilo resistente (contorni di CheckerMigration.Capture e varianti)
    foreach (var (name, input) in Outlines())
    {
        var section = Run(() => new SezioneCA(input));
        if (section.Outcome != "ok") continue;
        var s = new SezioneCA(input);
        Identical("profilo " + name, () => ConcreteTorsionCalculator.Geometry(s), () => ConcreteShearTorsionAdapter.TorsionGeometryOf(s, ShearTorsionEngine.Legacy));
        Same("profilo " + name, () => ConcreteTorsionCalculator.Geometry(s), () => ConcreteShearTorsionAdapter.TorsionGeometryOf(s, ShearTorsionEngine.Library), geometryStats);
    }
    Check(geometryStats.Results >= 9 && geometryStats.Rejected >= 2, $"profili: {geometryStats.Results} calcolati, {geometryStats.Rejected} rifiutati");

    // ---------------------------------------------------------------- 3d. calcolo del modulo (JSON 'taglio' e 'torsione')
    foreach (string norm in ConcreteStandards.OrdinaryNames)
        foreach (var shape in new[] { "Rettangolare", "A T", "Circolare", "Rettangolare cava", "Circolare cava" })
            foreach (var model in new[] { "Con staffe", "Senza staffe" })
                foreach (var loads in new[] { ("-300", "60", "25", "50", "80", "0"), ("200", "-40", "10", "-120", "35", "0"), ("-900", "150", "-80", "400", "-260", "0"), ("-300", "60", "25", "50", "80", "15") })
                {
                    var (input, settings, options) = Module(norm, shape);
                    options["modello"] = model;
                    var row = J.Obj(("N", loads.Item1), ("Mx", loads.Item2), ("My", loads.Item3), ("Vx", loads.Item4), ("Vy", loads.Item5), ("T", loads.Item6));
                    string id = $"modulo {norm} {shape} {model} N={loads.Item1} T={loads.Item6}";
                    Identical(id + " (motore predefinito)", () => ConcreteShearAnalysis.Calculate(input, settings, options, row, ExpectedDefault),
                        () => ConcreteShearAnalysis.Calculate(input, settings, options, row));
                    Same(id, () => ConcreteShearAnalysis.Calculate(input, settings, options, row, ShearTorsionEngine.Legacy),
                        () => ConcreteShearAnalysis.Calculate(input, settings, options, row, ShearTorsionEngine.Library), moduleStats);
                }
    foreach (var fck in new[] { "30", "95" })
    {
        var (input, settings, options) = Module("NTC 2018", "Rettangolare"); input["fck_mpa"] = fck;
        var row = J.Obj(("N", "-300"), ("Mx", "60"), ("My", "25"), ("Vx", "50"), ("Vy", "80"), ("T", "15"));
        Same("modulo NTC fck " + fck + " con torsione", () => ConcreteShearAnalysis.Calculate(input, settings, options, row, ShearTorsionEngine.Legacy),
            () => ConcreteShearAnalysis.Calculate(input, settings, options, row, ShearTorsionEngine.Library), moduleStats);
    }
    Check(moduleStats.Results > 100 && moduleStats.Rejected > 10, $"modulo: {moduleStats.Results} calcoli, {moduleStats.Rejected} rifiuti");

    // ---------------------------------------------------------------- 3f. il modulo usa per la torsione il motore richiesto
    // Per ogni motore la 'torsione' di ConcreteShearAnalysis coincide bit per bit con l'adattatore chiamato direttamente con gli stessi
    // ingressi (profilo resistente dal contorno, taglio delle due direzioni dello stesso calcolo), e un profilo rifiutato dà lo stesso
    // messaggio. Il controllo distingue i motori: in una parte dei casi le uscite Legacy e Library differiscono nelle ultime cifre, quindi
    // un modulo che aggirasse l'adattatore (o usasse un solo motore) fallirebbe qui.
    var routedStats = new Stats("torsione del modulo");
    int routed = 0, routedRejected = 0, discriminating = 0;
    foreach (var shape in new[] { "Rettangolare", "A T", "Circolare", "Rettangolare cava", "Circolare cava" })
        foreach (var loads in new[] { ("-300", "60", "25", "50", "80", "15"), ("-300", "60", "25", "900", "700", "40"), ("200", "-40", "10", "-120", "35", "-8"),
            ("-900", "150", "-80", "400", "-260", "25"), ("0", "0", "0", "0", "0", "5") })
            foreach (var cot in new[] { "1", "1.5", "2.5" })
            {
                var (input, settings, options) = Module("NTC 2018", shape);
                options["cot_torsione"] = cot;
                var row = J.Obj(("N", loads.Item1), ("Mx", loads.Item2), ("My", loads.Item3), ("Vx", loads.Item4), ("Vy", loads.Item5), ("T", loads.Item6));
                string id = $"torsione del modulo {shape} T={loads.Item6} V=({loads.Item4}; {loads.Item5}) cot {cot}";
                var outputs = new Dictionary<ShearTorsionEngine, string>();
                foreach (var engine in new[] { ShearTorsionEngine.Legacy, ShearTorsionEngine.Library })
                {
                    var module = Run(() => ConcreteShearAnalysis.Calculate(input, settings, options, row, engine));
                    var effective = (JsonObject)options.DeepClone();
                    var geometry = ConcreteCalculationSettings.UpdateAutomaticShear(input, effective);
                    var profile = Run(() => ConcreteShearTorsionAdapter.TorsionGeometryOf(geometry, engine));
                    Check(profile.Outcome == "ok" || module.Outcome != "ok", $"{id} ({engine}): profilo rifiutato «{profile.Message}» dall'adattatore, calcolato dal modulo");
                    if (module.Outcome != "ok") { outputs[engine] = module.Outcome + " " + module.Message; routedRejected++; continue; }
                    var r = ConcreteShearAnalysis.Calculate(input, settings, options, row, engine);
                    var torsionInput = new TorsionInput(SectionWorkspace.Number(row.S("T"), "T"), ConcreteShearTorsionAdapter.TorsionGeometryOf(geometry, engine),
                        ConcreteMaterials.DesignValues(input, settings).Fcd, geometry.Fyd, Math.PI * Math.Pow(input.D("transverse_bar_diameter_mm"), 2) / 4,
                        input.D("transverse_spacing_mm"), effective.Required("as_torsione"), effective.Required("cot_torsione"), row.D("Vx"), row.D("Vy"), r.Shear[0], r.Shear[1]);
                    string direct = J.Node(ConcreteShearTorsionAdapter.Torsion(torsionInput, input.Required("fck_mpa"), input.Required("gamma_c"), engine))!.ToJsonString();
                    Check(J.Node(r.Torsion)?.ToJsonString() == direct, $"{id} ({engine}): la torsione del modulo non è quella dell'adattatore con il motore {engine}");
                    var other = engine == ShearTorsionEngine.Legacy ? ShearTorsionEngine.Library : ShearTorsionEngine.Legacy;
                    if (J.Node(ConcreteShearTorsionAdapter.Torsion(torsionInput with { Geometry = ConcreteShearTorsionAdapter.TorsionGeometryOf(geometry, other) },
                        input.Required("fck_mpa"), input.Required("gamma_c"), other))!.ToJsonString() != direct) discriminating++;
                    outputs[engine] = direct; routed++;
                }
                string legacyOut = outputs[ShearTorsionEngine.Legacy], libraryOut = outputs[ShearTorsionEngine.Library];
                if (legacyOut.StartsWith("error:") || libraryOut.StartsWith("error:"))
                    Check(legacyOut == libraryOut, $"{id}: rifiuto diverso fra i motori: «{legacyOut}» / «{libraryOut}»");
                else
                {
                    var errors = new List<string>();
                    Json.Compare(JsonNode.Parse(outputs[ShearTorsionEngine.Legacy]), JsonNode.Parse(outputs[ShearTorsionEngine.Library]), id, routedStats, errors, Tolerance);
                    Check(errors.Count == 0, string.Join(Environment.NewLine, errors.Take(5)));
                    routedStats.Results++;
                }
            }
    Check(routed >= 100 && routedRejected >= 10 && discriminating > 0, $"torsione del modulo: {routed} calcoli, {routedRejected} rifiuti del profilo, {discriminating} con uscite dei due motori diverse");
    routedStats.Rejected = routedRejected / 2;

    // ---------------------------------------------------------------- 3e. calcolo headless del modulo e relazioni
    // ConcreteAnalysis (motore predefinito) contro lo stesso risultato con 'taglio', 'torsione' ed errori del motore legacy:
    // JSON entro la tolleranza con testi identici; testo delle relazioni completa e sintetica (X.Core) identico.
    int reports = 0; var analysisStats = new Stats("calcolo headless");
    foreach (var norm in new[] { "NTC 2018", "EN 1992-1-1", "Model Code 2010", "DIN EN 1992-1-1", "NS EN 1992-1-1" })
    {
        var data = SezioneCA.DefaultData(); var input = data["input"]!.AsObject(); var settings = SectionWorkspace.Prepare(data);
        settings["normativa"] = norm; settings["coefficienti"] = ConcreteStandards.Defaults(norm);
        foreach (var (k, v) in ConcreteCalculationSettings.CommonCoefficients) input[k] = settings["coefficienti"]![v]!.DeepClone();
        ConcreteCalculationSettings.Prepare(input, settings);
        var shear = settings["taglio"]!.AsObject(); ConcreteCalculationSettings.UpdateAutomaticShear(input, shear);
        shear["ancoraggio"] = "Confermato"; shear["chiusura_torsione"] = "Confermato"; shear["as_torsione"] = "1000";
        var actions = new JsonArray();
        foreach (var (id, n, mx, my, vx, vy, t) in new[] { ("T1", "-300", "60", "25", "50", "80", "0"), ("T2", "-900", "150", "-80", "400", "-260", "0"), ("T3", "200", "-40", "10", "-120", "35", "0"),
            ("T4", "-300", "60", "25", "50", "80", norm == "NTC 2018" ? "15" : "0"), ("T5", "-300", "60", "25", "900", "700", norm == "NTC 2018" ? "40" : "0"), ("T6", "-300", "60", "25", "50", "80", norm == "NTC 2018" ? "0" : "10") })
            actions.Add(J.Obj(("id", id), ("nome", "Azione " + id), ("N", n), ("Mx", mx), ("My", my), ("Vx", vx), ("Vy", vy), ("T", t)));
        shear["azioni"] = actions;
        var library = ConcreteAnalysis.Calculate(data);
        // Stesse righe con il motore legacy, sui dati preparati come in ConcreteAnalysis.
        var prepared = (JsonObject)data.DeepClone(); var preparedSettings = SectionWorkspace.Prepare(prepared); var preparedInput = prepared["input"]!.AsObject();
        var legacy = (JsonObject)library.DeepClone(); var legacyShear = legacy["taglio"]!.AsObject(); var legacyTorsion = legacy["torsione"]!.AsObject(); var legacyErrors = legacy["errori_calcolo"]!.AsObject();
        foreach (var row in preparedSettings["taglio"]!.Array("azioni").OfType<JsonObject>())
        {
            string id = row.S("id"); legacyShear.Remove(id); legacyTorsion.Remove(id); legacyErrors.Remove("Taglio/" + id);
            try
            {
                var r = ConcreteShearAnalysis.Calculate(preparedInput, preparedSettings, preparedSettings["taglio"]!.AsObject(), row, ShearTorsionEngine.Legacy);
                legacyShear[id] = J.Node(r.Shear); if (r.Torsion is not null) legacyTorsion[id] = J.Node(r.Torsion);
            }
            catch (ArgumentException e) { legacyErrors["Taglio/" + id] = e.Message; }
        }
        var errors = new List<string>();
        Json.Compare(legacy, library, "analisi " + norm, analysisStats, errors, Tolerance); analysisStats.Results++;
        Check(errors.Count == 0, string.Join(Environment.NewLine, errors.Take(5)));
        // NTC: due righe con torsione; altre norme: la riga con T diverso da zero è rifiutata (torsione accoppiata solo NTC).
        Check(library["taglio"]!.AsObject().Count >= 5 && (norm == "NTC 2018" ? library["torsione"]!.AsObject().Count == 2 : library["errori_calcolo"]!.AsObject().Any(p => p.Key.StartsWith("Taglio/"))),
            norm + ": casi del calcolo headless non rappresentativi: " + library["taglio"]!.ToJsonString().Length + " " + string.Join(",", library["torsione"]!.AsObject().Select(p => p.Key)) + " " + library["errori_calcolo"]!.ToJsonString());
        var complete = X.Core.ReportConcrete.Sections.Select(s => s.Key).ToHashSet();
        foreach (var (kind, make) in new (string, Func<JsonObject, byte[]>)[]
        {
            ("completa", r => X.Core.ReportConcrete.Create("Prova adattatore", (JsonObject)r["dati"]!.DeepClone(), (JsonObject)r.DeepClone(), complete, null, true)),
            ("sintetica", r => X.Core.ReportConcreteShort.Create("Prova adattatore", (JsonObject)r["dati"]!.DeepClone(), (JsonObject)r.DeepClone()))
        })
        {
            string a = DocxText(make(legacy)), b = DocxText(make(library));
            Check(a.Contains("Taglio") && a == b, $"{norm}: relazione {kind} diversa fra legacy e libreria: " + FirstDifference(a, b));
            reports++;
        }
    }
    Check(reports == 10, "relazioni confrontate: " + reports);

    // ---------------------------------------------------------------- 4. attesi indipendenti attraverso l'adattatore
    // Gli stessi attesi che supporto/test/ConcreteCode.Checks e X.Verifiche (--ca-module) verificano sul legacy diretto, qui sul
    // percorso di produzione (ConcreteShearTorsionAdapter) con entrambi i motori, quindi anche con quello predefinito:
    // reference.json (fib structuralcodes) letto in sola lettura, benchmark SOFiSTiK DIN, NA DS e UNI, granulometria NS, azione nulla,
    // massimo del cot θ automatico; torsione NTC in forma chiusa (eq. 4.1.35-4.1.40, come ConcreteModuleChecks e i casi TO/TV di
    // supporto/test/ValidazioneCA20260925/reference_extra.py).
    int independent = 0;
    void Near(double value, double expected, string message) { Check(double.IsFinite(value) && Math.Abs(value - expected) <= 1e-9 * Math.Max(1, Math.Abs(expected)), $"{message}: {value:G17} invece di {expected:G17}"); independent++; }
    void Reject(Action action, string message) { try { action(); } catch (ArgumentException) { independent++; count++; return; } throw new Exception(message); }
    var references = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "supporto", "test", "ConcreteCode.Checks", "reference.json")))!;
    foreach (var engine in new[] { ShearTorsionEngine.Legacy, ShearTorsionEngine.Library })
    {
        Ntc2018Checks.ShearResult Shear(ConcreteCodeChecks.ShearInput p) => ConcreteShearTorsionAdapter.Shear(p, engine);
        string tag = " (" + engine + ")";
        int rows = 0;
        foreach (var row in references["shear"]!.AsArray())
        {
            var p = row!["input"]!.Deserialize<ConcreteCodeChecks.ShearInput>()!;
            var r = Shear(p);
            Near(r.VRd, row.D("expected"), "fib shear " + p.Standard + tag);
            if (J.Number(row["steel"]) is double rs) Near(r.VRsd, rs, "fib VRds " + p.Standard + tag);
            if (J.Number(row["concrete"]) is double rc) Near(r.VRcd, rc, "fib VRdmax " + p.Standard + tag);
            Check(r.Details.Length > 0 && r.Reference.Length > 0, "traccia normativa assente " + p.Standard + tag);
            Near(Shear(p with { V = -p.V, M = -p.M }).VRd, r.VRd, "inversione delle azioni " + p.Standard + tag);
            rows++;
        }
        Check(rows >= 5, "righe di taglio di reference.json: " + rows);
        var din = new ConcreteCodeChecks.ShearInput("DIN EN 1992-1-1", 0, 343.25, 0, 150000, 300, 450, 1933.5, 30, 17, 500 / 1.15, 1.5, 200000, 128.4, 100, 90, 1, 384d / 450);
        Near(Shear(din).VRcd, 734.4, "SOFiSTiK DCE-EN6 puntone DIN" + tag);
        Reject(() => Shear(din with { CotTheta = 2 }), "DIN accetta cot oltre il limite dipendente dal taglio" + tag);
        var ds = din with { Standard = "DS EN 1992-1-1", CotTheta = 1, LeverFactor = .9 };
        Near(Shear(ds).VRcd, 405 * 300 * (.7 - 30d / 200) * 17 / 2 / 1000, "DS 5.103 NA" + tag);
        var ns = din with { Standard = "NS EN 1992-1-1", Asw = 0, Aggregate = 8 };
        Check(Shear(ns).VRd < Shear(ns with { Aggregate = 20 }).VRd, "NS granulometria non considerata" + tag);
        Reject(() => Shear(ns with { Asw = 100, N = 1000, CotTheta = 2 }), "NS forte trazione senza limite del cot" + tag);
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1 })
            Reject(() => Shear(din with { Bw = invalid }), "bw non valido accettato" + tag);
        var uni = din with { Standard = "UNI EN 1992-1-1", N = -500, CotTheta = 1, LeverFactor = .9 };
        Near(Shear(uni).VRcd, 405 * 300 * .5 * 17 / 2 / 1000, "UNI alpha_cw = 1, NA ufficiale p. 83" + tag);
        Near(Shear(uni with { N = 0 }).VRcd, Shear(uni).VRcd, "UNI: la compressione non introduce l'incremento della precompressione" + tag);
        Near(Shear(din with { V = 0, CotTheta = null }).Ratio!.Value, 0, "azione nulla, tasso nullo" + tag);
        foreach (var norm in new[] { "EN 1992-1-1", "DIN EN 1992-1-1", "DS EN 1992-1-1", "NS EN 1992-1-1", "UNI EN 1992-1-1", "Model Code 2010" })
            foreach (double alpha in new[] { 45d, 60d, 90d })
            {
                var p = din with { Standard = norm, Alpha = alpha, CotTheta = null, N = 0, V = 50 };
                double sampled = 0;
                for (int i = 0; i <= 500; i++)
                {
                    double c = .4 + i * .006;
                    try { sampled = Math.Max(sampled, Shear(p with { CotTheta = c }).VRd); } catch (ArgumentException) { }
                }
                Check(Shear(p).VRd >= sampled - 1e-7, "il cot θ automatico non dà il massimo: " + norm + " alpha " + alpha + tag); independent++;
            }

        // Torsione NTC in forma chiusa: TRcd = 2 Ak t (0,5 fcd) cot/(1+cot²), TRsd = 2 Ak (Asw/s) fyd cot, TRld = 2 Ak (ΣAl/uk) fyd / cot,
        // ΣAl richiesta = T uk / (2 Ak fyd cot); interazione [4.1.40] con VRcd e VRsd del taglio NTC in forma chiusa (staffe a 90°, N = 0).
        TorsionResult Torsion(double t, double ak, double uk, double th, double fcd, double fyd, double at, double s, double al, double cot, double vx, double vy, Ntc2018Checks.ShearResult sx, Ntc2018Checks.ShearResult sy)
            => ConcreteShearTorsionAdapter.Torsion(new(t, new(ak, uk, th), fcd, fyd, at, s, al, cot, vx, vy, sx, sy), 30, 1.5, engine);
        var shear = Shear(new("NTC 2018", 0, 50, 0, 240000, 300, 450, 0, 30, 17, 400, 1.5, 200000, 200, 150, 90, 1));
        Near(shear.VRcd, .9 * 450 * 300 * .5 * 17 * 1 / (1 + 1) / 1000, "VRcd NTC in forma chiusa" + tag);
        Near(shear.VRsd, .9 * 450 * 200 / 150.0 * 400 * 1 / 1000, "VRsd NTC in forma chiusa" + tag);
        var t = Torsion(10, 100000, 1300, 80, 17, 400, 100, 150, 1000, 1, 50, 0, shear, shear);
        Near(t.TRcd, 68, "TRcd = 2 Ak t (0,5 fcd) cot/(1+cot²)" + tag); Near(t.TRsd, 2 * 100000 * 100 / 150.0 * 400 / 1e6, "TRsd ramo singolo" + tag);
        Near(t.RequiredLongitudinalArea, 162.5, "area longitudinale richiesta a torsione" + tag);
        Near(t.ConcreteCombinedRatio!.Value, 10 / 68.0 + 50 / 516.375, "interazione monoassiale NTC [4.1.40]" + tag);
        var zeroSteel = Torsion(10, 100000, 1300, 80, 17, 400, 100, 150, 0, 1, 50, 0, shear, shear);
        Check(!zeroSteel.Passed && zeroSteel.TorsionRatio is null && J.Node(zeroSteel) is JsonObject, "torsione senza As disponibile: non soddisfatta ed esportabile" + tag); independent++;
        double fyd = 450 / 1.15;
        foreach (var (torque, vx) in new[] { (20.0, 0.0), (80.0, 0.0), (20.0, 80.0), (80.0, 180.0) })
        {
            ConcreteCodeChecks.ShearInput Axis(double v) => new("NTC 2018", 0, v, 0, 240000, 300, 450, 1000, 30, 17, fyd, 1.5, 200000, 200, 150, 90, 1);
            var r = Torsion(torque, 100000, 1300, 80, 17, fyd, 100, 150, 1000, 1, vx, 0, Shear(Axis(vx)), Shear(Axis(0)));
            double rc = 2 * 100000 * 80 * .5 * 17 / 2 / 1e6, rs = 2 * 100000 * 100 / 150.0 * fyd / 1e6, rl = 2 * 100000 * 1000 / 1300.0 * fyd / 1e6, rd = Math.Min(rc, Math.Min(rs, rl));
            double vrcd = .9 * 450 * 300 * .5 * 17 / 2 / 1000, vrsd = .9 * 450 * 200 / 150.0 * fyd / 1000;
            double et = torque / rd, ec = torque / rc + vx / vrcd, es = torque / rs + vx / vrsd;
            string id = $"torsione T = {torque}, Vx = {vx}" + tag;
            Near(r.TRcd, rc, id + " TRcd"); Near(r.TRsd, rs, id + " TRsd"); Near(r.TRld, rl, id + " TRld"); Near(r.TRd, rd, id + " TRd");
            Near(r.TorsionRatio!.Value, et, id + " ηT"); Near(r.ConcreteCombinedRatio!.Value, ec, id + " ηc"); Near(r.SteelCombinedRatio!.Value, es, id + " ηs");
            Near(r.RequiredLongitudinalArea, torque * 1e6 * 1300 / (2 * 100000 * fyd), id + " ΣAl richiesta");
            Check(r.Passed == (Math.Max(et, Math.Max(ec, es)) <= 1), id + " esito"); independent++;
        }
    }
    Check(independent > 200, "attesi indipendenti: " + independent);

    var lines = new[] { shearStats, torsionStats, geometryStats, moduleStats, routedStats, analysisStats }.Select(s => s.Line())
        .Append($"relazioni: {reports} testi identici (completa e sintetica, 5 norme)")
        .Append($"torsione del modulo: {routed} calcoli coincidenti con l'adattatore del motore richiesto, {discriminating} con uscite dei due motori diverse")
        .Append($"attesi indipendenti: {independent} controlli superati con i motori Legacy e Library").ToArray();
    foreach (var line in lines) Console.WriteLine(line);
    if (args.Length > 0)
    {
        Directory.CreateDirectory(args[0]);
        var report = new JsonObject { ["strumento"] = "ConcreteLibraryAdapter.Checks", ["motore_predefinito"] = ExpectedDefault.ToString(), ["tolleranza"] = Tolerance, ["controlli"] = count };
        foreach (var s in new[] { shearStats, torsionStats, geometryStats, moduleStats, routedStats, analysisStats }) report[s.Name] = s.Json();
        report["relazioni"] = reports;
        report["torsione_del_modulo"] = new JsonObject { ["calcoli_coincidenti_con_adattatore"] = routed, ["rifiuti"] = routedRejected, ["uscite_dei_motori_diverse"] = discriminating };
        report["attesi_indipendenti"] = independent;
        File.WriteAllText(Path.Combine(args[0], "misura.json"), report.ToJsonString(J.Options), new UTF8Encoding(false));
    }
    Console.WriteLine($"PASS · {count} controlli dell'adattatore di taglio e torsione (motore predefinito {ExpectedDefault}).");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex.Message); Console.Error.WriteLine(ex); return 1; }

// Testo di un DOCX: un paragrafo per riga, dalle sequenze w:t di word/document.xml.
static string DocxText(byte[] docx)
{
    using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(docx), System.IO.Compression.ZipArchiveMode.Read);
    using var stream = zip.GetEntry("word/document.xml")!.Open();
    var xml = System.Xml.Linq.XDocument.Load(stream);
    System.Xml.Linq.XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    return string.Join("\n", xml.Descendants(w + "p").Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))));
}

static string FirstDifference(string a, string b)
{
    var x = a.Split('\n'); var y = b.Split('\n');
    for (int i = 0; i < Math.Min(x.Length, y.Length); i++) if (x[i] != y[i]) return $"riga {i + 1}: «{x[i]}» / «{y[i]}»";
    return $"righe {x.Length} / {y.Length}";
}

static string Root()
{
    for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(d.FullName, "X.Calculations"))) return d.FullName;
    throw new DirectoryNotFoundException("Radice del repository non trovata da " + AppContext.BaseDirectory);
}

// Stessi casi e stesso seme di supporto/test/CheckerMigration.Capture (shear-legacy.csv).
static List<ConcreteCodeChecks.ShearInput> ShearGrid()
{
    var cases = new List<ConcreteCodeChecks.ShearInput>();
    string[] standards = ConcreteStandards.OrdinaryNames;
    foreach (var s in standards)
    {
        foreach (double n in new[] { -500.0, 0, 100 })
            foreach (double v in new[] { 80.0, 250 })
                foreach (double d in new[] { 460.0, 700, 900 })
                    foreach (double fck in new[] { 30.0, 75 })
                        cases.Add(new(s, n, v, 120, 300 * (d + 40), 300, d, 1256, fck, .85 * fck / 1.5, 391.3, 1.5, 200000, 0, 1));
        cases.Add(new(s, 0, 0, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 0, 1));
        cases.Add(new(s, -200, 120, 80, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 0, 1, Aggregate: 10));
        foreach (double spacing in new[] { 150.0, 300 })
            foreach (double alpha in new[] { 90.0, 45 })
                foreach (double? cot in new double?[] { null, 1, 2.5 })
                    foreach (double n in new[] { -500.0, 0, 200 })
                        foreach (double v in new[] { 150.0, 600 })
                            foreach (double fck in new[] { 30.0, 75 })
                                cases.Add(new(s, n, v, 150, 150000, 300, 460, 1256, fck, .85 * fck / 1.5, 391.3, 1.5, 200000, 100.53, spacing, alpha, cot));
        cases.Add(new(s, 0, 100, 0, 150000, 300, 460, 1256, 95, 53.8, 391.3, 1.5, 200000, 100.53, 150));
        cases.Add(new(s, 0, 100, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150, 30));
        cases.Add(new(s, 0, 100, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150, CotTheta: 3));
        cases.Add(new(s, 0, 100, 0, 150000, 300, 460, 0, 30, 17, 391.3, 1.5, 200000, 100.53, 150));
        cases.Add(new(s, 0, 100, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150, LeverFactor: 1));
        cases.Add(new(s, 1500, 400, 0, 150000, 300, 460, 1256, 30, 17, 391.3, 1.5, 200000, 100.53, 150));
    }
    var random = new Random(20261001);
    double U(double a, double b) => a + (b - a) * random.NextDouble();
    for (int i = 0; i < 700; i++)
    {
        double fck = new[] { 20.0, 25, 30, 35, 40, 45, 50, 55, 60, 70, 80, 90 }[random.Next(12)];
        double bw = U(150, 1000), d = U(150, 1500), area = bw * (d + U(30, 80)) * U(1, 1.6);
        double asw = random.NextDouble() < .3 ? 0 : U(50, 600);
        double? cot = random.NextDouble() < .6 ? null : U(.8, 3.2);
        cases.Add(new(standards[i % standards.Length], U(-2000, 800), U(0, 1500), U(0, 600), area, bw, d, U(200, 8000), fck, .85 * fck / 1.5,
            new[] { 391.3, 450 / 1.15 }[random.Next(2)], 1.5, 200000, asw, U(50, 400), U(45, 90), cot,
            new[] { .9, .75, .6 }[random.Next(3)], new[] { 8.0, 16, 20, 32 }[random.Next(4)], U(-200, 200)));
    }
    return cases;
}

// Stessi casi e stesso seme di supporto/test/CheckerMigration.Capture (torsion-legacy.csv); taglio sintetico.
static List<TorsionInput> TorsionGrid()
{
    var cases = new List<TorsionInput>();
    static Ntc2018Checks.ShearResult Shear(double rsd, double rcd, double cot) => new(rsd, rcd, Math.Min(rsd, rcd), null, cot, "synthetic");
    var shapes = new[] { new TorsionGeometry(82416, 1216, 96), new TorsionGeometry(256256, 2064, 184), new TorsionGeometry(Math.PI * 375 * 375, Math.PI * 750, 250) };
    int k = 0;
    foreach (var g in shapes)
        foreach (double t in new[] { 0.0, 25, -60, 180 })
            foreach (double leg in new[] { 50.27, 113.1 })
                foreach (double s in new[] { 100.0, 250 })
                    foreach (double al in new[] { 0.0, 1200 })
                        foreach (double cot in new[] { 1, 1.8, 2.5 })
                            foreach (var (vx, vy) in new[] { (0.0, 0.0), (120.0, -80.0) })
                            {
                                double fcd = k++ % 2 == 0 ? .85 * 25 / 1.5 : .85 * 45 / 1.5;
                                cases.Add(new(t, g, fcd, 391.3, leg, s, al, cot, vx, vy, Shear(400, 900, cot), Shear(250, 700, cot)));
                            }
    var g0 = shapes[0];
    cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, .9, 0, 0, Shear(400, 900, .9), Shear(250, 700, .9)));
    cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, 2.6, 0, 0, Shear(400, 900, 2.6), Shear(250, 700, 2.6)));
    cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, 1.5, 100, 0, Shear(400, 900, 2), Shear(250, 700, 1.5)));
    cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, 1.5, 0, 100, Shear(400, 900, 1.5), Shear(250, 700, 2)));
    cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, 1.5, 0, 0, Shear(400, 900, 2), Shear(250, 700, 2)));
    cases.Add(new(40, g0, 14.17, 391.3, 0, 150, 800, 1.5, 0, 0, Shear(400, 900, 1.5), Shear(250, 700, 1.5)));
    cases.Add(new(40, g0, 14.17, 391.3, 50.27, 0, 800, 1.5, 0, 0, Shear(400, 900, 1.5), Shear(250, 700, 1.5)));
    cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, -1, 1.5, 0, 0, Shear(400, 900, 1.5), Shear(250, 700, 1.5)));
    cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, 1.5, 100, 0, Shear(400, 0, 1.5), Shear(250, 700, 1.5)));
    cases.Add(new(40, g0, 14.17, 391.3, 50.27, 150, 800, 1.5, 0, 100, Shear(0, 700, 1.5), Shear(0, 700, 1.5)));
    var random = new Random(20261002);
    double U(double a, double b) => a + (b - a) * random.NextDouble();
    for (int i = 0; i < 400; i++)
    {
        double area = U(2e4, 2e6), t = U(40, 400), cot = U(1, 2.5);
        double cx = random.NextDouble() < .1 ? U(1, 2.5) : cot, cy = random.NextDouble() < .1 ? U(1, 2.5) : cot;
        double vx = random.NextDouble() < .3 ? 0 : U(-800, 800), vy = random.NextDouble() < .3 ? 0 : U(-800, 800);
        cases.Add(new(random.NextDouble() < .1 ? 0 : U(-400, 400), new TorsionGeometry(area, 4 * Math.Sqrt(area) * U(1, 1.5), t), U(8, 40), new[] { 391.3, 450 / 1.15, 500 / 1.15 }[random.Next(3)],
            U(28, 314), U(50, 300), random.NextDouble() < .1 ? 0 : U(0, 6000), cot, vx, vy,
            Shear(U(0, 1500), random.NextDouble() < .05 ? 0 : U(100, 2500), cx), Shear(U(0, 1500), U(100, 2500), cy)));
    }
    return cases;
}

// Contorni di supporto/test/CheckerMigration.Capture (torsion-geometry-legacy.csv) e varianti con copriferri e fori al limite.
static IEnumerable<(string Name, JsonObject Input)> Outlines()
{
    JsonObject With(Action<JsonObject> edit) { var input = SezioneCA.DefaultInput(); edit(input); return input; }
    yield return ("R300x500", With(i => { i["width_mm"] = "300"; i["height_mm"] = "500"; i["top_bar_count"] = "2"; i["bottom_bar_count"] = "3"; i["side_bar_count_per_side"] = "0";
        i["top_bar_diameter_mm"] = "16"; i["bottom_bar_diameter_mm"] = "20"; i["cover_mm"] = "30"; i["transverse_bar_diameter_mm"] = "8"; }));
    yield return ("R600x800", With(i => { }));
    yield return ("R200x200", With(i => { i["width_mm"] = "200"; i["height_mm"] = "200"; i["top_bar_count"] = "2"; i["bottom_bar_count"] = "2"; i["side_bar_count_per_side"] = "0";
        i["top_bar_diameter_mm"] = "16"; i["bottom_bar_diameter_mm"] = "16"; i["cover_mm"] = "30"; i["transverse_bar_diameter_mm"] = "8"; }));
    yield return ("R160x160", With(i => { i["width_mm"] = "160"; i["height_mm"] = "160"; i["top_bar_count"] = "2"; i["bottom_bar_count"] = "2"; i["side_bar_count_per_side"] = "0";
        i["top_bar_diameter_mm"] = "12"; i["bottom_bar_diameter_mm"] = "12"; i["cover_mm"] = "40"; i["transverse_bar_diameter_mm"] = "8"; }));
    yield return ("C1000", With(i => { i["shape"] = "Circolare"; }));
    yield return ("C400", With(i => { i["shape"] = "Circolare"; i["diameter_mm"] = "400"; i["longitudinal_bar_count"] = "8"; i["longitudinal_bar_diameter_mm"] = "16";
        i["cover_mm"] = "40"; i["transverse_bar_diameter_mm"] = "8"; }));
    yield return ("R600x800H", With(i => { i["foro_presente"] = true; i["inner_width_mm"] = "300"; i["inner_height_mm"] = "400"; i["side_bar_count_per_side"] = "0"; }));
    yield return ("R1200x1000H", With(i => { i["width_mm"] = "1200"; i["height_mm"] = "1000"; i["foro_presente"] = true; i["inner_width_mm"] = "700"; i["inner_height_mm"] = "500";
        i["side_bar_count_per_side"] = "0"; i["cover_mm"] = "40"; i["top_bar_diameter_mm"] = "20"; i["bottom_bar_diameter_mm"] = "20"; }));
    yield return ("C1000H", With(i => { i["shape"] = "Circolare"; i["foro_presente"] = true; i["inner_diameter_mm"] = "500"; }));
    yield return ("T1200x800", With(i => { i["shape"] = "A T"; }));
    foreach (var cover in new[] { "20", "45", "70" })
    {
        yield return ("R250x400 c" + cover, With(i => { i["width_mm"] = "250"; i["height_mm"] = "400"; i["cover_mm"] = cover; i["side_bar_count_per_side"] = "0"; }));
        yield return ("R800x800H c" + cover, With(i => { i["width_mm"] = "800"; i["height_mm"] = "800"; i["foro_presente"] = true; i["inner_width_mm"] = "560"; i["inner_height_mm"] = "500";
            i["side_bar_count_per_side"] = "0"; i["cover_mm"] = cover; }));
        yield return ("C600H c" + cover, With(i => { i["shape"] = "Circolare"; i["diameter_mm"] = "600"; i["foro_presente"] = true; i["inner_diameter_mm"] = "420"; i["cover_mm"] = cover; }));
    }
}

// Sezione del modulo come in supporto/test/ConcreteCode.Checks, con le conferme richieste dal taglio e dalla torsione.
static (JsonObject Input, JsonObject Settings, JsonObject Options) Module(string norm, string shape)
{
    var data = SezioneCA.DefaultData(); var input = data["input"]!.AsObject(); var settings = SectionWorkspace.Prepare(data);
    settings["normativa"] = norm; settings["coefficienti"] = ConcreteStandards.Defaults(norm);
    foreach (var (k, v) in ConcreteCalculationSettings.CommonCoefficients) input[k] = settings["coefficienti"]![v]!.DeepClone();
    input["shape"] = shape.Replace(" cava", "");
    if (shape.EndsWith("cava")) { input["foro_presente"] = true; input["inner_width_mm"] = "200"; input["inner_height_mm"] = "300"; input["inner_diameter_mm"] = "400"; }
    ConcreteCalculationSettings.Prepare(input, settings);
    var options = settings["taglio"]!.AsObject();
    options["modello_circolare"] = "Parametri assegnati"; options["z_d"] = "0.75"; ConcreteCalculationSettings.UpdateAutomaticShear(input, options);
    options["ancoraggio"] = "Confermato"; options["chiusura_torsione"] = "Confermato"; options["as_torsione"] = "1000";
    return (input, settings, options);
}

sealed class Stats(string name)
{
    public string Name { get; } = name;
    public int Results, Rejected, Numbers, Different;
    public double MaxRelative;
    public string Worst = "";
    public void Number(string path, double a, double b)
    {
        Numbers++;
        if (a == b) return;
        Different++;
        double relative = Math.Abs(a - b) / Math.Max(Math.Abs(a), Math.Abs(b));
        if (relative > MaxRelative) { MaxRelative = relative; Worst = $"{path}: {a.ToString("R", CultureInfo.InvariantCulture)} → {b.ToString("R", CultureInfo.InvariantCulture)}"; }
    }
    public string Line() => $"{Name}: {Results} risultati, {Rejected} rifiuti uguali; {Numbers} numeri, {Different} diversi, scarto relativo massimo {MaxRelative.ToString("G3", CultureInfo.InvariantCulture)}{(Worst.Length > 0 ? " (" + Worst + ")" : "")}";
    public JsonObject Json() => new() { ["risultati"] = Results, ["rifiuti"] = Rejected, ["numeri"] = Numbers, ["numeri_diversi"] = Different, ["scarto_relativo_massimo"] = MaxRelative, ["caso_peggiore"] = Worst };
}

static class Json
{
    // Strutture e testi identici; numeri entro abs + rel · max(|a|, |b|) con abs = rel = tolleranza.
    public static void Compare(JsonNode? a, JsonNode? b, string path, Stats stats, List<string> errors, double tolerance)
    {
        switch (a)
        {
            case null:
                if (b is not null) errors.Add(path + ": null contro " + b.ToJsonString());
                return;
            case JsonObject oa:
                if (b is not JsonObject ob) { errors.Add(path + ": oggetto contro " + b?.ToJsonString()); return; }
                if (!oa.Select(p => p.Key).SequenceEqual(ob.Select(p => p.Key))) { errors.Add(path + ": chiavi " + string.Join(",", oa.Select(p => p.Key)) + " contro " + string.Join(",", ob.Select(p => p.Key))); return; }
                foreach (var (key, value) in oa) Compare(value, ob[key], path + "." + key, stats, errors, tolerance);
                return;
            case JsonArray aa:
                if (b is not JsonArray ba || aa.Count != ba.Count) { errors.Add(path + ": vettore di " + aa.Count + " contro " + b?.ToJsonString()); return; }
                for (int i = 0; i < aa.Count; i++) Compare(aa[i], ba[i], path + "[" + i + "]", stats, errors, tolerance);
                return;
            case JsonValue va:
                if (b is JsonValue vb && va.TryGetValue<double>(out var x) && vb.TryGetValue<double>(out var y))
                {
                    stats.Number(path, x, y);
                    if (!(Math.Abs(x - y) <= tolerance + tolerance * Math.Max(Math.Abs(x), Math.Abs(y)))) errors.Add($"{path}: {x:R} contro {y:R}");
                }
                else if (va.ToJsonString() != b?.ToJsonString()) errors.Add(path + ": " + va.ToJsonString() + " contro " + b?.ToJsonString());
                return;
        }
    }
}
