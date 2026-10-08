using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Calculations;
using Materiali;
using X.Core;
using ProbeEntry = Anthea.Calculations.ConcreteDurabilityAdapter.ProbeEntry;

/// <summary>
/// Sezione 11 delle prove dell'adattatore: durabilità, copriferri, scheda Materiali e validazione di progetto (refactoring F2.9,
/// docs/refactoring/piano.md; progetto in supporto/artefatti/refactoring/f27-f28-progetto/F29-progetto.md del checkout principale).
/// I due motori danno gli stessi numeri bit per bit: la prova che li distingue è la sonda dell'adattatore (operazione e motore di ogni
/// chiamata) con la scansione dei sorgenti, non le uscite. Ogni messaggio comincia con il numero della prova (11a…11i), così una prova
/// negativa dice quale prova fallisce. Nessuna tolleranza: numeri confrontati sulla forma "R" (bit per bit), testi identici.
/// 11a. Mappatura: nessun letterale numerico nei file della mappatura e dell'adattatore; ogni rifiuto della libreria raggiungibile dalle
///      facciate è provocato e ha la traduzione del legacy; un testo inventato dà InvalidOperationException.
/// 11b. Interruttore atteso; il motore Legacy dell'adattatore è il nucleo legacy diretto, bit per bit (griglia della cattura densa,
///      griglia degli errori anche combinati, catalogo e funzioni per codice).
/// 11c. Libreria contro legacy attraverso le facciate con il motore esplicito, sugli stessi insiemi, in it-IT e nella cultura invariante:
///      stessi numeri, stesso tipo di eccezione e stesso messaggio; a ogni chiamata la sonda registra il motore richiesto.
/// 11d. Catalogo e funzioni per codice uguali con i due motori; etichette delle classi minime (F2.9-D13); sonda del catalogo.
/// 11e. Sonda sui percorsi headless con il motore predefinito: ogni percorso ha voci, tutte con il predefinito.
/// 11f. Punti d'ingresso con il parametro del motore (F2.9-D2): testi e numeri identici con i due motori, sonda del motore richiesto.
/// 11g. Stato statico: nessun campo o proprietà statica scrivibile, nessuna collezione statica modificabile.
/// 11h. Attesi indipendenti (valori a mano), con entrambi i motori.
/// 11i. Scansione dei sorgenti con l'elenco ammesso legacy-allowlist-durabilita.json.
/// </summary>
internal static class DurabilityChecks
{
    /// <summary>Motore predefinito atteso: Legacy fino all'interruttore (passi E2-E4), Library dal passo E5.</summary>
    public const DurabilityEngine ExpectedDefault = DurabilityEngine.Legacy;
    const string Adapter = nameof(ConcreteDurabilityAdapter);
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    static readonly ImmutableArray<DurabilityEngine> Engines = [DurabilityEngine.Legacy, DurabilityEngine.Library];

    /// <summary>Esegue tutte le prove anche quando una fallisce, poi segnala insieme i fallimenti: una prova negativa mostra così ogni
    /// prova che la rileva.</summary>
    public static JsonObject Run(string root, Action<bool, string> check)
    {
        var report = new JsonObject { ["motore_predefinito"] = ConcreteDurabilityAdapter.Default.ToString() };
        var failures = new List<string>();
        foreach (var (key, test) in new (string, Func<JsonObject>)[]
        {
            ("11a_mappatura", () => MappingLayer(root, check)), ("11b_motore_legacy", () => LegacyEngine(check)),
            ("11c_libreria_contro_legacy", () => Equivalence(check)), ("11d_catalogo", () => Catalog(check)),
            ("11e_sonda_percorsi", () => Paths(root, check)), ("11f_punti_di_ingresso", () => EntryPoints(root, check)),
            ("11g_stato_statico", () => StaticState(check)), ("11h_attesi_indipendenti", () => Independent(root, check)),
            ("11i_scansione", () => Scan(root, check))
        })
        {
            CultureInfo.CurrentCulture = Italian;
            try { report[key] = test(); }
            catch (Exception e) { failures.Add(e.Message); ConcreteDurabilityAdapter.StopProbe(); }
        }
        CultureInfo.CurrentCulture = Italian;
        foreach (var (key, value) in report) Console.WriteLine($"durabilità {key}: {value?.ToJsonString()}");
        check(failures.Count == 0, "prove di durabilità fallite: " + string.Join(" || ", failures));
        return report;
    }

    // ================================================================ strumenti
    /// <summary>Forma esatta di un'uscita: numeri in formato "R" (bit per bit), testi tra virgolette; tipi non previsti sono un errore.</summary>
    static string Show(object? value) => value switch
    {
        null => "null",
        double d => d.ToString("R", Invariant),
        int i => i.ToString(Invariant),
        bool b => b ? "true" : "false",
        string s => "«" + s + "»",
        CoverLine l => $"{l.Exposure}:S{l.StructuralClass}:{Show(l.Durability)}",
        CoverResult r => $"cmin,b={Show(r.Bond)};cmin,dur={Show(r.Durability)};cmin={Show(r.Minimum)};cnom={Show(r.Nominal)};righe=[{string.Join("|", r.Lines.Select(Show))}]",
        NtcCoverResult n => $"ambiente={Show(n.Environment)};gruppo={n.Severity};Cmin={Show(n.Cmin)};C0={Show(n.C0)};tabella={Show(n.TableCover)};vita={Show(n.LifeExtra)};" +
            $"classe={Show(n.LowStrengthExtra)};qualità={Show(n.QualityReduction)};{Show(n.Cover)}",
        Exposure e => $"{e.Code};{Show(e.Description)};{Show(e.MaxRatio)};{e.MinStrength};{e.MinCement};{Show(e.MinAir)};{e.CoverColumn}",
        ValueTuple<double?, int?> t => $"({Show(t.Item1)};{Show(t.Item2)})",
        ImmutableArray<Exposure> a => "[" + string.Join("|", a.Select(Show)) + "]",
        ConcreteCoverResult c => $"adottato={Show(c.Adopted)};materiale={Show(c.MaterialMinimum)};richiesto={Show(c.Required)};Ø={Show(c.MaximumBarDiameter)};errore={Show(c.ReinforcementError)};esito={Show(c.Passed)}",
        JsonNode node => node.ToJsonString(),
        IEnumerable<string> texts => "[" + string.Join("|", texts.Select(Show)) + "]",
        _ => throw new InvalidOperationException("Show: tipo non previsto " + value.GetType())
    };

    static string Outcome(Func<object?> call)
    {
        try { return "ok " + Show(call()); }
        catch (Exception e) { return "error:" + e.GetType().Name + " " + e.Message; }
    }

    /// <summary>Esegue la chiamata con una sonda attiva e restituisce l'esito e le voci registrate.</summary>
    static (string Outcome, IReadOnlyList<ProbeEntry> Entries) Probed(Func<object?> call)
    {
        var probe = ConcreteDurabilityAdapter.StartProbe();
        try { return (Outcome(call), probe.Entries); }
        finally { ConcreteDurabilityAdapter.StopProbe(); }
    }

    static string Describe(IEnumerable<ProbeEntry> entries)
    {
        var list = entries.ToList();
        return list.Count == 0 ? "nessuna voce" : string.Join(", ", list.Take(6).Select(e => e.Adapter + "." + e.Operation + "/" + e.Engine)) + (list.Count > 6 ? $" … ({list.Count})" : "");
    }

    /// <summary>Autoverifica della sonda prima di ogni prova che la usa: chiamate note con i due motori, anche da attività parallele;
    /// una sonda che non riceve nulla fa fallire la prova invece di farla passare.</summary>
    static void ProbeSelfCheck(string test, Action<bool, string> check)
    {
        var probe = ConcreteDurabilityAdapter.StartProbe();
        try
        {
            ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Legacy);
            ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Library);
            Parallel.For(0, 4, new ParallelOptions { MaxDegreeOfParallelism = 2 }, _ => ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Library));
            var e = probe.Entries;
            check(e.Count == 6 && e[0] == new ProbeEntry(Adapter, "Severity", DurabilityEngine.Legacy) && e.Skip(1).All(x => x == new ProbeEntry(Adapter, "Severity", DurabilityEngine.Library)),
                test + ": autoverifica della sonda fallita: " + Describe(e));
        }
        finally { ConcreteDurabilityAdapter.StopProbe(); }
        var after = ConcreteDurabilityAdapter.StartProbe(); ConcreteDurabilityAdapter.StopProbe();
        ConcreteDurabilityAdapter.Severity("XC1", DurabilityEngine.Library);
        check(after.Entries.Count == 0, test + ": la sonda registra dopo l'arresto");
    }

    /// <summary>Confronto dei due motori attraverso una chiamata con il motore esplicito: stesso esito e, a ogni chiamata, solo voci della
    /// sonda con il motore richiesto (almeno <paramref name="minimumEntries"/>).</summary>
    sealed class Comparison(string test, Action<bool, string> check)
    {
        public int Results, Rejections, Calls;
        public void Same(string id, Func<DurabilityEngine, object?> call, int minimumEntries = 1)
        {
            var outcomes = new string[Engines.Length];
            for (int i = 0; i < Engines.Length; i++)
            {
                var engine = Engines[i];
                var (outcome, entries) = Probed(() => call(engine));
                check(entries.Count >= minimumEntries && entries.All(e => e.Adapter == Adapter && e.Engine == engine),
                    $"{test}: {id}: la sonda non registra solo il motore {engine} richiesto ({Describe(entries)})");
                check(!outcome.Contains("senza traduzione"), $"{test}: {id}: testo senza traduzione «{outcome}»");
                outcomes[i] = outcome; Calls++;
            }
            check(outcomes[0] == outcomes[1], $"{test}: {id}: legacy «{outcomes[0]}», libreria «{outcomes[1]}»");
            if (outcomes[0].StartsWith("ok")) Results++; else Rejections++;
        }
        public JsonObject Json() => new() { ["risultati_uguali"] = Results, ["rifiuti_uguali"] = Rejections, ["chiamate_con_sonda"] = Calls };
    }

    static Exposure[] Set(params string[] codes)
    {
        var catalog = ConcreteDurabilityAdapter.Exposures(DurabilityEngine.Legacy);
        return codes.Select(code => catalog.Single(e => e.Code == code)).ToArray();
    }

    static readonly CoverInput Basic = new(50, false, false, false, 16, 20, 10, false, 0, 0);

    /// <summary>Griglia di supporto/test/CheckerMigration.Capture (DurabilityCapture): 24 insiemi di esposizioni (uno ripetuto) × 7 fck ×
    /// 6 opzioni, gli stessi della fixture durability-legacy.csv di Checker.</summary>
    static List<(string Id, Exposure[] Set, double Fck, CoverInput P, int V)> CaptureGrid()
    {
        var exposures = ConcreteDurabilityAdapter.Exposures(DurabilityEngine.Legacy);
        var sets = CaptureSets();
        var cases = new List<(string, Exposure[], double, CoverInput, int)>();
        int n = 0;
        foreach (var set in sets)
            foreach (double fck in new[] { 20.0, 25, 30, 35, 40, 45, 50 })
                for (int k = 0; k < 6; k++)
                {
                    int v = n++;
                    var p = new CoverInput(v % 4 == 3 ? 100 : 50, v % 3 == 1, v % 5 == 2, v % 7 == 4, new[] { 12.0, 16, 20, 32 }[v % 4], new[] { 16.0, 20, 40 }[v % 3],
                        new[] { 10.0, 5, 0 }[v % 3], v % 6 == 5, new[] { 0, 5, 10, 15 }[v % 4], new[] { 0, 40, 75 }[(v / 3) % 3]);
                    cases.Add(($"{string.Join("+", set.Select(e => e.Code))} fck {fck} opzioni {v}", set, fck, p, v));
                }
        return cases;
    }

    static Exposure[][] CaptureSets()
    {
        var exposures = ConcreteDurabilityAdapter.Exposures(DurabilityEngine.Legacy);
        return exposures.Select(e => new[] { e }).Concat(new[] { new[] { "XC4", "XF2" }, new[] { "XC4", "XS3", "XF4" }, new[] { "XD1", "XA2" }, new[] { "XF1", "XF3" }, new[] { "X0", "XC1" }, new[] { "XF4" } }
            .Select(c => Set(c))).ToArray();
    }

    /// <summary>Griglia degli errori, anche combinati: insiemi vuoto, X0 combinata, sola XF (senza corrosione), validi; fck, geometria,
    /// vita, abrasione, getto e Cmin pertinente non validi uno alla volta e insieme. L'ordine dei controlli conta (prova negativa n28).</summary>
    static IEnumerable<(string Id, Exposure[] Set, double Fck, CoverInput P, double? Cmin)> ErrorGrid()
    {
        var sets = new (string Name, Exposure[] Set)[] { ("vuoto", []), ("X0+XC1", Set("X0", "XC1")), ("XF2", Set("XF2")), ("XC1", Set("XC1")), ("XD3+XF4", Set("XD3", "XF4")) };
        foreach (var (name, set) in sets)
            foreach (double fck in new[] { double.NaN, 5, 30, double.PositiveInfinity })
                foreach (double diameter in new[] { 16, -1 })
                    foreach (double aggregate in new[] { 20, double.NaN })
                        foreach (double deviation in new[] { 10, -1 })
                            foreach (int life in new[] { 50, 75 })
                                foreach (int abrasion in new[] { 0, 7 })
                                    foreach (int ground in new[] { 0, 30 })
                                        foreach (double? cmin in new double?[] { null, 50, double.NaN })
                                            yield return ($"{name} fck {fck} Ø {diameter} Dmax {aggregate} Δc {deviation} vita {life} abrasione {abrasion} getto {ground} Cmin {Show(cmin)}",
                                                set, fck, new CoverInput(life, false, false, false, diameter, aggregate, deviation, false, abrasion, ground), cmin);
    }

    static readonly double[] AirAggregates = [16, 32, 8, 20, 12, 25, 0, double.NaN, double.NegativeInfinity];

    // ================================================================ 11a. strato di mappatura
    static JsonObject MappingLayer(string root, Action<bool, string> check)
    {
        // Nessun letterale numerico, compresi quelli con il punto iniziale (.5): i numeri normativi stanno nella libreria.
        const string Sample = "double k = .85 * fck, x = a.Length, y = (.9 + 1e6) / 2.5m, w = Item2, c = En1992p11; // .7";
        check(Literals(Sample).OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(new[] { ".85", ".9", "1e6", "2.5m" }.OrderBy(s => s, StringComparer.Ordinal)),
            "11a: controllo dei letterali su un caso sintetico: " + string.Join(", ", Literals(Sample)));
        foreach (string file in new[] { "X.Calculations/ConcreteLibraryMapping.Durability.cs", "X.Calculations/ConcreteDurabilityAdapter.cs" })
        {
            var literals = Literals(File.ReadAllText(Path.Combine(root, file)));
            check(literals.Length == 0, "11a: " + file + ": letterali numerici non ammessi: " + string.Join(", ", literals));
        }
        // Ogni rifiuto della libreria raggiungibile dalle facciate (progetto, §6.4), provocato con i due motori: testo del legacy.
        var unknown = Set("XC1")[0] with { Code = "XZ9" };
        var sites = new (string Site, Func<DurabilityEngine, object?> Call, string Expected)[]
        {
            ("ExposureClasses.cs:84 esposizioni vuote, Cover", e => Durability.Cover([], 30, Basic, e), "Selezionare almeno una classe di esposizione."),
            ("ExposureClasses.cs:84 esposizioni vuote, NtcCover", e => NtcCover.Calculate([], 30, Basic, false, false, null, e), "Selezionare almeno una classe di esposizione."),
            ("ExposureClasses.cs:84 esposizioni vuote, ValidateExposure", e => { Durability.ValidateExposure([], e); return null; }, "Selezionare almeno una classe di esposizione."),
            ("ExposureClasses.cs:84 esposizioni vuote, MinimumConcrete.Required", e => MinimumConcrete.Required([], e), "Selezionare almeno una classe di esposizione."),
            ("ExposureClasses.cs:84 esposizioni vuote, AtecapMix.Required", e => AtecapMix.Required([], e), "Selezionare almeno una classe di esposizione."),
            ("ExposureClasses.cs:84 esposizioni vuote, AtecapMix.Air", e => AtecapMix.Air([], 16, e), "Selezionare almeno una classe di esposizione."),
            ("ExposureClasses.cs:85 X0 combinata, Cover", e => Durability.Cover(Set("X0", "XC1"), 30, Basic, e), "X0 non è combinabile con altre esposizioni."),
            ("ExposureClasses.cs:85 X0 combinata, NtcCover", e => NtcCover.Calculate(Set("X0", "XC1"), 30, Basic, false, false, null, e), "X0 non è combinabile con altre esposizioni."),
            ("ExposureClasses.cs:85 X0 combinata, AtecapMix.Required", e => AtecapMix.Required(Set("XC1", "X0"), e), "X0 non è combinabile con altre esposizioni."),
            ("ExposureClasses.cs:78 codice sconosciuto, NtcCover.Severity", e => NtcCover.Severity("XZ9", e), ConcreteLibraryMapping.DurabilityUnknownExposureMessage),
            ("ExposureClasses.cs:78 codice sconosciuto, AtecapMix.Limits", e => AtecapMix.Limits("XZ9", e), ConcreteLibraryMapping.DurabilityUnknownExposureMessage),
            ("ExposureClasses.cs:78 codice sconosciuto, MinimumConcrete.Fck", e => MinimumConcrete.Fck("XZ9", e), ConcreteLibraryMapping.DurabilityUnknownClassMessage),
            ("ExposureClasses.cs:78 codice sconosciuto, MinimumConcrete.Required", e => MinimumConcrete.Required([unknown], e), ConcreteLibraryMapping.DurabilityUnknownClassMessage),
            ("ExposureClasses.cs:78 codice sconosciuto, AtecapMix.Required", e => AtecapMix.Required([unknown], e), ConcreteLibraryMapping.DurabilityUnknownExposureMessage),
            ("ExposureClasses.cs:78 codice sconosciuto, NtcCover", e => NtcCover.Calculate([unknown], 30, Basic, false, false, null, e), ConcreteLibraryMapping.DurabilityUnknownExposureMessage),
            ("CoverRequirements.cs:145 fck, Cover", e => Durability.Cover(Set("XC1"), 5, Basic, e), "Resistenza del calcestruzzo non valida."),
            ("CoverRequirements.cs:145 fck, NtcCover", e => NtcCover.Calculate(Set("XC1"), 95, Basic, false, false, null, e), "Resistenza del calcestruzzo non valida."),
            ("CoverRequirements.cs:146-148 dati, Cover", e => Durability.Cover(Set("XC1"), 30, Basic with { Diameter = -1 }, e), "Controllare diametro, Dmax, tolleranza e condizioni di getto."),
            ("CoverRequirements.cs:146-148 dati, NtcCover", e => NtcCover.Calculate(Set("XC1"), 30, Basic with { Ground = 30 }, false, false, null, e), "Controllare diametro, Dmax, tolleranza e condizioni di getto."),
            ("CoverRequirements.cs:156 Cmin pertinente", e => NtcCover.Calculate(Set("XC1"), 30, Basic, false, false, 50, e), "Cmin pertinente: indicare fck tra 12 MPa e C0 = 35 MPa."),
            ("CoverRequirements.cs:175 esposizione alla corrosione", e => Durability.Cover(Set("XF2"), 30, Basic, e), "Per il copriferro aggiungere l'esposizione alla corrosione (XC, XD o XS); XF/XA da sole non definiscono cmin,dur."),
            ("ExposureClasses.cs:132 Dmax", e => AtecapMix.Air(Set("XF2"), 0, e), "Dmax non valido."),
            ("F2.9-D13 etichetta di una classe minima sconosciuta", e => MinimumConcrete.Label(28, e), ConcreteLibraryMapping.DurabilityLabelMessage)
        };
        foreach (var (site, call, expected) in sites)
        {
            string legacy = Outcome(() => call(DurabilityEngine.Legacy)), library = Outcome(() => call(DurabilityEngine.Library));
            check(library == "error:ArgumentException " + expected, $"11a: {site}: con la libreria «{library}», atteso il rifiuto «{expected}»");
            check(legacy == library, $"11a: {site}: legacy «{legacy}», libreria «{library}»");
        }
        // Un testo della libreria senza traduzione è un errore di programma, mai un testo inglese mostrato.
        foreach (var invented in new ArgumentException[] { new("Durability: new message."), new ArgumentOutOfRangeException("profile") })
        {
            string outcome = Outcome(() => ConcreteLibraryMapping.DurabilityError(invented, ConcreteLibraryMapping.DurabilityUnknownExposureMessage));
            check(outcome.StartsWith("error:InvalidOperationException ") && outcome.Contains("senza traduzione nello strato di mappatura"), "11a: testo inventato accettato: " + outcome);
        }
        check(Outcome(() => ConcreteLibraryMapping.DurabilityEnvironment(3)).StartsWith("error:InvalidOperationException "), "11a: gruppo ambientale NTC inventato accettato");
        return new JsonObject { ["file_senza_letterali"] = 2, ["rifiuti_provocati"] = sites.Length };
    }

    /// <summary>Letterali numerici fuori da commenti, stringhe e caratteri, anche nella forma con il punto iniziale; mai dentro un
    /// identificatore o dopo un accesso a membro.</summary>
    static string[] Literals(string source)
    {
        string code = Regex.Replace(source, @"(?s)/\*.*?\*/|//[^\n]*|@""(?:[^""]|"""")*""|\$?""(?:[^""\\\n]|\\.)*""|'(?:[^'\\]|\\.)'", " ");
        return Regex.Matches(code, @"(?<![\w.])(?:\d+(?:\.\d+)?|\.\d+)(?:[eE][-+]?\d+)?[dDfFmM]?(?![\w.])").Select(m => m.Value).Distinct().ToArray();
    }

    // ================================================================ 11b. interruttore e motore legacy
    static JsonObject LegacyEngine(Action<bool, string> check)
    {
        check(ConcreteDurabilityAdapter.Default == ExpectedDefault, $"11b: motore predefinito {ConcreteDurabilityAdapter.Default}, atteso {ExpectedDefault}");
        int identical = 0;
        void Identical(string id, Func<object?> direct, Func<object?> adapter)
        {
            string a = Outcome(direct), b = Outcome(adapter);
            check(a == b, $"11b: {id}: legacy diretto «{a}», motore Legacy dell'adattatore «{b}»");
            identical++;
        }
        const DurabilityEngine L = DurabilityEngine.Legacy;
        foreach (var (id, set, fck, p, v) in CaptureGrid())
        {
            Identical("EC2 " + id, () => DurabilityLegacy.Durability.Cover(set, fck, p), () => ConcreteDurabilityAdapter.Cover(set, fck, p, L));
            foreach (bool plate in new[] { false, true })
            {
                bool quality = v % 2 == 1;
                Identical($"NTC {id} piastra {plate}",
                    () => { int? pertinent = v % 5 == 3 ? DurabilityLegacy.MinimumConcrete.Required(set) : null; return DurabilityLegacy.NtcCover.Calculate(set, fck, p, plate, quality, pertinent); },
                    () => { int? pertinent = v % 5 == 3 ? ConcreteDurabilityAdapter.MinimumStrength(set, L) : null; return ConcreteDurabilityAdapter.NtcCover(set, fck, p, plate, quality, pertinent, L); });
            }
        }
        foreach (var (id, set, fck, p, cmin) in ErrorGrid())
        {
            if (cmin is null) Identical("errori EC2 " + id, () => DurabilityLegacy.Durability.Cover(set, fck, p), () => ConcreteDurabilityAdapter.Cover(set, fck, p, L));
            foreach (bool plate in new[] { false, true })
                Identical($"errori NTC {id} piastra {plate}", () => DurabilityLegacy.NtcCover.Calculate(set, fck, p, plate, false, cmin), () => ConcreteDurabilityAdapter.NtcCover(set, fck, p, plate, false, cmin, L));
        }
        var sets = CaptureSets().Append([]).ToArray();
        foreach (var set in sets)
        {
            string id = string.Join("+", set.Select(e => e.Code));
            Identical("validazione " + id, () => { DurabilityLegacy.Durability.ValidateExposure(set); return null; }, () => { ConcreteDurabilityAdapter.Validate(set, L); return null; });
            Identical("classe minima " + id, () => DurabilityLegacy.MinimumConcrete.Required(set), () => ConcreteDurabilityAdapter.MinimumStrength(set, L));
            Identical("composizione " + id, () => DurabilityLegacy.AtecapMix.Required(set), () => ConcreteDurabilityAdapter.Mix(set, L));
            foreach (double dmax in AirAggregates) Identical($"aria {id} Dmax {dmax}", () => DurabilityLegacy.AtecapMix.Air(set, dmax), () => ConcreteDurabilityAdapter.Air(set, dmax, L));
        }
        Identical("catalogo", () => DurabilityLegacy.Durability.Exposures, () => ConcreteDurabilityAdapter.Exposures(L));
        foreach (string? code in Codes())
        {
            Identical("gruppo NTC " + code, () => DurabilityLegacy.NtcCover.Severity(code!), () => ConcreteDurabilityAdapter.Severity(code!, L));
            Identical("classe minima " + code, () => DurabilityLegacy.MinimumConcrete.Fck(code!), () => ConcreteDurabilityAdapter.MinimumFck(code!, L));
            Identical("limiti " + code, () => DurabilityLegacy.AtecapMix.Limits(code!), () => ConcreteDurabilityAdapter.MixLimits(code!, L));
        }
        foreach (int fck in Labels()) Identical("etichetta " + fck, () => DurabilityLegacy.MinimumConcrete.Label(fck), () => ConcreteDurabilityAdapter.Label(fck, L));
        foreach (var (id, e, fck, p) in StructuralClassCases())
            Identical("classe strutturale " + id, () => DurabilityLegacy.Durability.StructuralClass(e, fck, p), () => ConcreteDurabilityAdapter.StructuralClass(e, fck, p, L));
        return new JsonObject { ["motore_predefinito_atteso"] = ExpectedDefault.ToString(), ["chiamate_identiche_al_legacy_diretto"] = identical };
    }

    /// <summary>I 18 codici del catalogo, un codice sconosciuto, un codice vuoto, uno in minuscolo e null.</summary>
    static IEnumerable<string?> Codes() => ConcreteDurabilityAdapter.Exposures(DurabilityEngine.Legacy).Select(e => (string?)e.Code).Concat(new string?[] { "XZ9", "", "xc1", null });

    /// <summary>Valori di fck per le etichette: le classi minime del catalogo e alcuni valori che non lo sono.</summary>
    static IEnumerable<int> Labels() => new[] { 0, -1, 12, 16, 20, 25, 28, 30, 32, 35, 40, 45, 90 };

    static IEnumerable<(string Id, Exposure E, double Fck, CoverInput P)> StructuralClassCases()
    {
        foreach (var e in ConcreteDurabilityAdapter.Exposures(DurabilityEngine.Legacy))
            foreach (double fck in new[] { 20.0, 30, 35, 40, 45, 50 })
                for (int k = 0; k < 16; k++)
                    yield return ($"{e.Code} fck {fck} opzioni {k}", e, fck, Basic with { Life = (k & 1) == 0 ? 50 : 100, StrengthReduction = (k & 2) != 0, Slab = (k & 4) != 0, Quality = (k & 8) != 0 });
    }

    // ================================================================ 11c. libreria contro legacy
    static JsonObject Equivalence(Action<bool, string> check)
    {
        ProbeSelfCheck("11c", check);
        var grid = new Comparison("11c", check);
        foreach (var (id, set, fck, p, v) in CaptureGrid())
        {
            grid.Same("EC2 " + id, e => Durability.Cover(set, fck, p, e));
            foreach (bool plate in new[] { false, true })
            {
                bool quality = v % 2 == 1;
                grid.Same($"NTC {id} piastra {plate}", e => { int? pertinent = v % 5 == 3 ? MinimumConcrete.Required(set, e) : null; return NtcCover.Calculate(set, fck, p, plate, quality, pertinent, e); });
            }
        }
        foreach (var set in CaptureSets().Append([]))
        {
            string id = string.Join("+", set.Select(e => e.Code));
            grid.Same("validazione " + id, e => { Durability.ValidateExposure(set, e); return null; });
            grid.Same("classe minima " + id, e => MinimumConcrete.Required(set, e));
            grid.Same("composizione " + id, e => AtecapMix.Required(set, e));
            foreach (double dmax in AirAggregates) grid.Same($"aria {id} Dmax {dmax}", e => AtecapMix.Air(set, dmax, e));
        }
        foreach (var (id, e, fck, p) in StructuralClassCases()) grid.Same("classe strutturale " + id, engine => Durability.StructuralClass(e, fck, p, engine));
        // Errori anche combinati, nella cultura dell'applicazione e in quella invariante (C0 è formattato nel messaggio di Cmin).
        var errors = new Comparison("11c", check);
        foreach (var culture in new[] { Italian, Invariant })
        {
            CultureInfo.CurrentCulture = culture;
            foreach (var (id, set, fck, p, cmin) in ErrorGrid())
            {
                if (cmin is null) errors.Same($"errori EC2 {id} ({culture.Name})", e => Durability.Cover(set, fck, p, e));
                foreach (bool plate in new[] { false, true })
                    errors.Same($"errori NTC {id} piastra {plate} ({culture.Name})", e => NtcCover.Calculate(set, fck, p, plate, false, cmin, e));
            }
        }
        CultureInfo.CurrentCulture = Italian;
        check(grid.Results > 2000 && grid.Rejections > 400 && errors.Rejections > 10000 && errors.Results >= 16,
            $"11c: insiemi poco rappresentativi: {grid.Results} risultati e {grid.Rejections} rifiuti, errori {errors.Results} risultati e {errors.Rejections} rifiuti");
        return new JsonObject { ["griglia_della_cattura_e_combinazioni"] = grid.Json(), ["griglia_degli_errori_it_IT_e_invariante"] = errors.Json() };
    }

    // ================================================================ 11d. catalogo
    static JsonObject Catalog(Action<bool, string> check)
    {
        ProbeSelfCheck("11d", check);
        var legacy = ConcreteDurabilityAdapter.Exposures(DurabilityEngine.Legacy);
        var library = ConcreteDurabilityAdapter.Exposures(DurabilityEngine.Library);
        check(legacy.Length == 18 && library.Length == legacy.Length, $"11d: catalogo di {legacy.Length} e {library.Length} esposizioni");
        for (int i = 0; i < Math.Min(legacy.Length, library.Length); i++)
            check(legacy[i] == library[i], $"11d: esposizione {i}: legacy {Show(legacy[i])}, libreria {Show(library[i])}");
        // Il catalogo della facciata passa dall'adattatore con il motore predefinito (prova negativa n30).
        var (_, facade) = Probed(() => Durability.Exposures);
        check(facade.Count == 1 && facade[0] == new ProbeEntry(Adapter, "Exposures", ExpectedDefault), "11d: il catalogo della facciata non passa dall'adattatore con il motore predefinito: " + Describe(facade));
        var codes = new Comparison("11d", check);
        foreach (string? code in Codes())
        {
            codes.Same("gruppo NTC " + code, e => NtcCover.Severity(code!, e));
            codes.Same("classe minima " + code, e => MinimumConcrete.Fck(code!, e));
            codes.Same("limiti " + code, e => AtecapMix.Limits(code!, e));
        }
        foreach (int fck in Labels()) codes.Same("etichetta " + fck, e => MinimumConcrete.Label(fck, e));
        // F2.9-D13: la libreria può restituire solo le classi minime del catalogo; per le altre lo stesso rifiuto di prima.
        var minimum = legacy.Select(e => MinimumConcrete.Fck(e.Code, DurabilityEngine.Library)).Distinct().OrderBy(x => x).ToArray();
        check(minimum.SequenceEqual(new[] { 12, 25, 30, 32, 35 }), "11d: classi minime del catalogo " + string.Join(", ", minimum));
        foreach (var engine in Engines)
            foreach (int fck in new[] { 28, 0, 90 })
                check(Outcome(() => MinimumConcrete.Label(fck, engine)) == "error:ArgumentException " + ConcreteLibraryMapping.DurabilityLabelMessage, $"11d: etichetta di {fck} accettata con il motore {engine}");
        return new JsonObject { ["esposizioni"] = legacy.Length, ["funzioni_per_codice_ed_etichette"] = codes.Json() };
    }

    // ================================================================ 11e. sonda sui percorsi headless
    static JsonObject Paths(string root, Action<bool, string> check)
    {
        ProbeSelfCheck("11e", check);
        var corpus = CorpusDocuments(root);
        check(corpus.Count(d => d.Module == "mat_calcestruzzo") == 14 && corpus.Count(d => d.Module == RetainingWall.Module) == 3 && corpus.Count(d => d.Module == "str_palo") == 3,
            "11e: documenti del corpus di durabilità (B6): " + string.Join(", ", corpus.Select(d => d.Id)));
        int paths = 0, entriesTotal = 0;
        void OnPath(string id, Func<object?> call, bool requireEntries = true)
        {
            var probe = ConcreteDurabilityAdapter.StartProbe();
            string outcome;
            try { outcome = Run(call); }
            finally { ConcreteDurabilityAdapter.StopProbe(); }
            var entries = probe.Entries;
            if (requireEntries) check(entries.Count > 0, $"11e: {id}: nessuna voce della sonda ({outcome})");
            check(entries.All(e => e.Adapter == Adapter && e.Engine == ExpectedDefault), $"11e: {id}: voci con un motore diverso dal predefinito {ExpectedDefault}: {Describe(entries.Where(e => e.Engine != ExpectedDefault))}");
            paths++; entriesTotal += entries.Count;
        }
        // Facciate chiamate senza motore.
        var p = Basic;
        OnPath("facciata Durability.Exposures", () => Durability.Exposures.Length);
        OnPath("facciata Durability.ValidateExposure", () => { Durability.ValidateExposure(Set("XC1")); return null; });
        OnPath("facciata Durability.StructuralClass", () => Durability.StructuralClass(Set("XC4")[0], 40, p with { StrengthReduction = true }));
        OnPath("facciata Durability.Cover", () => Durability.Cover(Set("XC4", "XD1"), 40, p));
        OnPath("facciata NtcCover.Severity", () => NtcCover.Severity("XD3"));
        OnPath("facciata NtcCover.Calculate", () => NtcCover.Calculate(Set("XC3"), 28, p, false, false, 30));
        OnPath("facciata MinimumConcrete.Fck", () => MinimumConcrete.Fck("XF1"));
        OnPath("facciata MinimumConcrete.Required", () => MinimumConcrete.Required(Set("XC4", "XD1")));
        OnPath("facciata MinimumConcrete.Label", () => MinimumConcrete.Label(32));
        OnPath("facciata AtecapMix.Limits", () => AtecapMix.Limits("XS3"));
        OnPath("facciata AtecapMix.Required", () => AtecapMix.Required(Set("XC4", "XF2")));
        OnPath("facciata AtecapMix.Air", () => AtecapMix.Air(Set("XF4"), 25));
        // Scheda Materiali headless (CalculationService, mat_calcestruzzo) e minimo della scheda.
        foreach (var (name, state) in MaterialStates(root))
        {
            OnPath("CalculationService mat_calcestruzzo " + name, () => CalculationService.Calculate("mat_calcestruzzo", (JsonObject)state.DeepClone()), !name.Contains("diametro"));
            OnPath("MaterialCover.Required " + name, () => MaterialCover.Required(state, 30), !name.Contains("diametro"));
        }
        // Copriferro della sezione e validazione di progetto.
        foreach (var (name, state) in MaterialStates(root).Where(s => !s.Name.Contains("diametro")))
            OnPath("ConcreteCoverAnalysis " + name, () => ConcreteCoverAnalysis.Calculate(SezioneCA.DefaultInput(), state, 30));
        foreach (var (name, section, durability) in ProjectScenarios())
        {
            OnPath("ProjectValidation.CoverChecks " + name, () => ProjectValidation.CoverChecks(section).Select(c => c.Text).ToArray(), durability);
            OnPath("ProjectValidation.Warnings " + name, () => ProjectValidation.Warnings(section), durability);
        }
        // Muri a mensola: calcolo, progetto delle armature e minimo del copriferro.
        foreach (var (name, wall) in Walls(root))
        {
            OnPath("RetainingWall.Calculate " + name, () => { RetainingWall.Calculate((JsonObject)wall.DeepClone()); return null; });
            OnPath("RetainingWall.DesignReinforcement " + name, () => RetainingWall.DesignReinforcement((JsonObject)wall.DeepClone()));
            OnPath("RetainingWall.RequiredCover " + name, () => RetainingWall.RequiredCover((JsonObject)wall.DeepClone()));
        }
        // Dettagli della sezione c.a. e ricerca piccola del progetto delle armature (sequenziale e con due attività).
        foreach (var (name, data) in DetailingSections(root))
            OnPath("ConcreteDetailingAnalysis " + name, () => Detailing(data));
        foreach (int workers in new[] { 1, 2 })
            OnPath($"ConcreteReinforcementDesign.Optimize ({workers} attività)", () => ConcreteReinforcementDesign.Optimize(DesignFixture(), SmallDesign with { MaxParallelism = workers }).Evaluated);
        return new JsonObject { ["percorsi"] = paths, ["voci_della_sonda"] = entriesTotal };
    }

    static string Run(Func<object?> call)
    {
        try { var value = call(); return "ok"; }
        catch (Exception e) { return "error:" + e.GetType().Name + " " + e.Message; }
    }

    // ================================================================ 11f. punti d'ingresso con il motore esplicito
    static JsonObject EntryPoints(string root, Action<bool, string> check)
    {
        ProbeSelfCheck("11f", check);
        var material = new Comparison("11f", check);
        foreach (var (name, state) in MaterialStates(root))
            foreach (double fck in new[] { 20.0, 30, 45 })
            {
                int entries = name.Contains("diametro") ? 0 : 1;
                material.Same($"MaterialCover.Required {name} fck {fck}", e => MaterialCover.Required(state, fck, e), entries);
                material.Same($"MaterialCover.Required {name} fck {fck} Ø 25", e => MaterialCover.Required(state, fck, 25, e));
            }
        foreach (var (name, wall) in Walls(root))
        {
            var data = (JsonObject)wall.DeepClone(); RetainingWall.Upgrade(data);
            var state = RetainingWall.CoverMaterialState(data);
            material.Same("MaterialCover.Required muro " + name, e => MaterialCover.Required(state, data["materials"].D("fck"), RetainingWall.MaximumBarDiameter(data), e));
        }
        var cover = new Comparison("11f", check);
        foreach (var (name, state) in MaterialStates(root).Where(s => !s.Name.Contains("diametro")))
            foreach (var (label, input) in SectionInputs())
                cover.Same($"ConcreteCoverAnalysis {name} {label}", e => ConcreteCoverAnalysis.Calculate(input, state, 30, e));
        var project = new Comparison("11f", check);
        var texts = new JsonObject();
        foreach (var (name, section, durability) in ProjectScenarios())
        {
            int entries = durability ? 1 : 0;
            project.Same("ProjectValidation.CoverChecks " + name, e => ProjectValidation.CoverChecks(section, null, e).Select(c => c.Text + " · " + Show(c.Passed)).ToArray(), entries);
            project.Same("ProjectValidation.Warnings " + name, e => ProjectValidation.Warnings(section, e), entries);
            texts[name] = new JsonArray(ProjectValidation.CoverChecks(section).Select(c => (JsonNode)(c.Text + " · " + Show(c.Passed))).ToArray());
        }
        check(project.Results >= 20, "11f: scenari di progetto: " + project.Results);
        return new JsonObject { ["MaterialCover"] = material.Json(), ["ConcreteCoverAnalysis"] = cover.Json(), ["ProjectValidation"] = project.Json(), ["testi_della_validazione_di_progetto"] = texts };
    }

    // ================================================================ dati delle prove 11e e 11f
    static JsonObject MaterialState(string cls, string exposure, string method, string element = "Trave / pilastro", string life = "50 anni", string control = "Ordinario",
        string deviation = "10 mm", string ground = "Casseratura", string abrasion = "Nessuno", string diameter = "16", string aggregate = "20",
        bool highStrength = false, bool slab = false, bool quality = false, bool rough = false, bool ntcQuality = false)
        => J.Obj(("versione_materiali", 1), ("classe", cls), ("esposizione_principale", exposure), ("esposizioni", new JsonArray(exposure)),
            ("numeri", J.Obj(("diameter", diameter), ("aggregate", aggregate))),
            ("scelte", J.Obj(("coverMethod", method), ("ntcElement", element), ("life", life), ("deviationControl", control), ("deviationValue", deviation), ("ground", ground), ("abrasion", abrasion))),
            ("opzioni", J.Obj(("highStrength", highStrength), ("slab", slab), ("quality", quality), ("rough", rough), ("ntcQuality", ntcQuality))));

    const string Ntc = "NTC + Circ. 2019", Ec2 = "EC2 2004";

    /// <summary>Documenti del corpus di durabilità della baseline B6 (tests/ANTHEA.Testing/corpus/verifica-durabilita-*.json), applicati
    /// ai default come fa la cattura (Corpus.Build): 14 schede Materiali (M1-M14), 3 muri a mensola (W1-W3), 3 sezioni c.a. (S1-S3).</summary>
    static List<(string Id, string Module, JsonObject Data)> CorpusDocuments(string root)
    {
        static void Apply(JsonObject target, JsonObject values)
        {
            foreach (var (key, value) in values)
            {
                if (key != "combinazioni" && value is JsonObject child && target[key] is JsonObject existing) Apply(existing, child);
                else target[key] = value?.DeepClone();
            }
        }
        var documents = new List<(string, string, JsonObject)>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "tests", "ANTHEA.Testing", "corpus"), "verifica-durabilita-*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            var definition = JsonNode.Parse(File.ReadAllText(file, Encoding.UTF8))!.AsObject();
            string module = definition["moduleId"]!.GetValue<string>();
            var data = ModuleCatalog.CreateData(module);
            Apply(data, definition["overrides"]!.AsObject());
            ModuleCatalog.ValidateData(module, data);
            documents.Add((Path.GetFileNameWithoutExtension(file)["verifica-durabilita-".Length..], module, data));
        }
        return documents;
    }

    /// <summary>Stati della scheda Materiali: il default, ogni esposizione con i due criteri, varianti di elemento, vita, controllo,
    /// getto, abrasione, superficie e rifiuti (dati non numerici, esposizione sconosciuta, aggregato fuori intervallo), più le schede
    /// M1-M14 del corpus di B6.</summary>
    static IEnumerable<(string Name, JsonObject State)> MaterialStates(string root)
    {
        foreach (var (id, _, data) in CorpusDocuments(root).Where(d => d.Module == "mat_calcestruzzo")) yield return ("corpus " + id, data);
        yield return ("default", ModuleCatalog.CreateData("mat_calcestruzzo"));
        foreach (var e in ConcreteDurabilityAdapter.Exposures(DurabilityEngine.Legacy))
        {
            yield return ("NTC " + e.Code, MaterialState("C30/37", e.Code, Ntc));
            yield return ("EC2 " + e.Code, MaterialState("C30/37", e.Code, Ec2));
        }
        yield return ("NTC XS3 piastra 100 anni qualità", MaterialState("C45/55", "XS3", Ntc, "Piastra / soletta / parete", "100 anni", ntcQuality: true));
        yield return ("EC2 XC4 riduzioni", MaterialState("C40/50", "XC4", Ec2, highStrength: true, slab: true, quality: true));
        yield return ("EC2 XC2 getto su terra XM3", MaterialState("C30/37", "XC2", Ec2, ground: "Direttamente su terra", abrasion: "XM3 · +15 mm", diameter: "32", aggregate: "40", rough: true));
        yield return ("NTC XC1 misura copriferri", MaterialState("C25/30", "XC1", Ntc, control: "Misura copriferri", deviation: "5 mm"));
        yield return ("NTC XC2 misura accurata terreno preparato", MaterialState("C30/37", "XC2", Ntc, control: "Misura accurata + scarto", deviation: "0 mm", ground: "Terreno preparato"));
        yield return ("NTC XA3 C90/105", MaterialState("C90/105", "XA3", Ntc));
        yield return ("NTC X0 C12/15", MaterialState("C12/15", "X0", Ntc));
        yield return ("NTC XF1 C30/37", MaterialState("C30/37", "XF1", Ntc));
        yield return ("NTC XD1 abrasione XM1 superficie irregolare", MaterialState("C40/50", "XD1", Ntc, abrasion: "XM1 · +5 mm", rough: true, aggregate: "40"));
        yield return ("esposizione XZ9", MaterialState("C30/37", "XZ9", Ntc));
        yield return ("aggregato fuori intervallo", MaterialState("C30/37", "XC1", Ntc, aggregate: "0"));
        yield return ("diametro abc", MaterialState("C30/37", "XC1", Ntc, diameter: "abc"));
    }

    /// <summary>Ingressi della sezione c.a. per il copriferro: default, copriferro insufficiente o nullo, barre grosse, senza barre.</summary>
    static IEnumerable<(string Label, JsonObject Input)> SectionInputs()
    {
        JsonObject With(Action<JsonObject> edit) { var input = SezioneCA.DefaultInput(); edit(input); return input; }
        yield return ("default", SezioneCA.DefaultInput());
        yield return ("copriferro 1 mm", With(i => i["cover_mm"] = 1));
        yield return ("copriferro 0", With(i => i["cover_mm"] = 0));
        yield return ("barre Ø32", With(i => { i["top_bar_diameter_mm"] = "32"; i["bottom_bar_diameter_mm"] = "32"; i["cover_mm"] = "45"; }));
        yield return ("senza barre", With(i => { i["top_bar_count"] = "0"; i["bottom_bar_count"] = "0"; i["side_bar_count_per_side"] = "0"; }));
    }

    /// <summary>Progetti costruiti con ProjectDocuments (scenari di P5): nome, contenitore da validare e se la validazione raggiunge la
    /// durabilità (con una scheda Materiali e dati leggibili).</summary>
    static IEnumerable<(string Name, JsonObject Section, bool Durability)> ProjectScenarios()
    {
        static (JsonObject Project, JsonObject Material) WithMaterial(JsonObject state)
        {
            var project = ProjectDocuments.AddProject(ProjectDocuments.CreateArchive());
            var material = ProjectDocuments.AddSheet(project, "mat_calcestruzzo");
            material["dati"] = state;
            return (project, material);
        }
        {
            var project = ProjectDocuments.AddProject(ProjectDocuments.CreateArchive());
            ProjectDocuments.AddSheet(project, "str_palo");
            yield return ("senza scheda Materiali", project, false);
        }
        {
            var (project, _) = WithMaterial(MaterialState("C35/45", "XC4", Ntc));
            ProjectDocuments.AddSheet(project, "str_palo");
            yield return ("sezione c.a. coerente", project, true);
        }
        {
            var (project, _) = WithMaterial(MaterialState("C35/45", "XC4", Ntc));
            var rc = ProjectDocuments.AddSheet(project, "str_palo");
            rc["dati"]!["input"]!["fck_mpa"] = 40;
            yield return ("dati in conflitto", project, true);
        }
        {
            var (project, _) = WithMaterial(MaterialState("C35/45", "XD3", Ntc));
            var rc = ProjectDocuments.AddSheet(project, "str_palo");
            rc["dati"]!["input"]!["cover_mm"] = "20";
            yield return ("copriferro insufficiente", project, true);
        }
        {
            var (project, _) = WithMaterial(MaterialState("C35/45", "XC1", Ntc, diameter: "8"));
            var rc = ProjectDocuments.AddSheet(project, "str_palo");
            rc["dati"]!["input"]!["top_bar_diameter_mm"] = "32"; rc["dati"]!["input"]!["cover_mm"] = "33";
            yield return ("Ø effettivo maggiore della scheda", project, true);
        }
        {
            var (project, _) = WithMaterial(MaterialState("C30/37", "XS1", Ntc, "Piastra / soletta / parete", ntcQuality: true));
            ProjectDocuments.AddSheet(project, RetainingWall.Module);
            yield return ("muro a mensola", project, true);
        }
        {
            var (project, _) = WithMaterial(MaterialState("C30/37", "XC2", Ec2, highStrength: true));
            ProjectDocuments.AddSheet(project, PaloOrizzontale.Module);
            yield return ("palo orizzontale", project, true);
        }
        {
            var (project, _) = WithMaterial(MaterialState("C30/37", "XC1", Ntc, aggregate: "abc"));
            ProjectDocuments.AddSheet(project, "str_palo");
            yield return ("materiale non valido", project, true);
        }
        {
            var (project, _) = WithMaterial(MaterialState("C30/37", "XF2", Ec2));
            ProjectDocuments.AddSheet(project, "str_palo");
            yield return ("EC2 senza esposizione alla corrosione", project, true);
        }
        {
            var (project, _) = WithMaterial(MaterialState("C30/37", "XC3", Ntc));
            var branch = ProjectDocuments.AddSection(project);
            ProjectDocuments.AddSheet(branch, "str_palo");
            ProjectDocuments.AddSheet(branch, RetainingWall.Module);
            yield return ("scheda a un livello superiore", project, true);
        }
    }

    /// <summary>Muri a mensola: il default del modulo e RetainingWall.Example("cantilever"), come nella baseline B3, e W1-W3 del corpus di B6.</summary>
    static IEnumerable<(string Name, JsonObject Wall)> Walls(string root)
    {
        yield return ("default", ModuleCatalog.CreateData(RetainingWall.Module));
        yield return ("esempio a mensola", RetainingWall.Example("cantilever"));
        foreach (var (id, _, data) in CorpusDocuments(root).Where(d => d.Module == RetainingWall.Module)) yield return ("corpus " + id, data);
    }

    /// <summary>Sezioni c.a. NTC 2018 con i dettagli costruttivi, più S1-S3 del corpus di B6 (preparate come nel calcolo).</summary>
    static IEnumerable<(string Name, JsonObject Data)> DetailingSections(string root)
    {
        foreach (var (id, _, data) in CorpusDocuments(root).Where(d => d.Module == "str_palo")) { SectionWorkspace.Prepare(data); yield return ("corpus " + id, data); }
        JsonObject Section(string kind, string exposure, string life = "50", string quality = "No", string aggregate = "20", string deviation = "10")
        {
            var data = SezioneCA.DefaultData(); var ws = SectionWorkspace.Prepare(data);
            var d = ws["dettagli_costruttivi"]!;
            d["elemento"] = kind; d["vita_durabilita"] = life; d["qualita_copriferro"] = quality; d["aggregato"] = aggregate; d["delta_c"] = deviation;
            ws["sle_comuni"]!["esposizione"] = exposure;
            return data;
        }
        yield return ("trave XC4 100 anni qualità", Section("Trave", "XC4", "100", "Sì", "25", "5"));
        yield return ("soletta piena XD3", Section("Soletta piena", "XD3"));
        yield return ("pilastro XC1", Section("Pilastro", "XC1"));
        yield return ("parete XF4", Section("Parete", "XF4"));
        yield return ("esposizione da scegliere", Section("Trave", "Da scegliere"));
    }

    static object? Detailing(JsonObject data)
    {
        var copy = (JsonObject)data.DeepClone();
        var actions = new[] { "SLU", "SLV" }.SelectMany(set => copy["combinazioni"]?[set] as JsonArray ?? []).OfType<JsonObject>()
            .Select(row => J.Obj(("N", row["azioni"]![0]))).ToArray();
        var r = ConcreteDetailingAnalysis.Calculate(copy["input"]!.AsObject(), copy["workspace_ca"]!.AsObject(), actions);
        return r.Durability is null ? r.DurabilityError : Show(r.Durability);
    }

    static readonly ConcreteDesignOptions SmallDesign = new()
    {
        Diameters = [16, 20], TopCounts = [3, 4], BottomCounts = [3, 4], SideCounts = [2], CircularCounts = [8, 12], StirrupDiameters = [8],
        StirrupSpacings = [150, 200], MaxEvaluations = 16
    };

    /// <summary>Sezione del progetto delle armature come supporto/test/ConcreteDesign.Checks (trave XC1, flessione).</summary>
    static JsonObject DesignFixture()
    {
        var data = SezioneCA.DefaultData(); var ws = SectionWorkspace.Prepare(data); var input = data["input"]!;
        input["shape"] = "Rettangolare"; input["cover_mm"] = "40"; input["width_mm"] = "400"; input["height_mm"] = "600";
        ws["dettagli_costruttivi"]!["elemento"] = "Trave"; ws["sle_comuni"]!["esposizione"] = "XC1";
        ws["taglio"]!["modello_circolare"] = "Parametri assegnati";
        ws["ancoraggi"]!["lunghezza"] = "2000";
        foreach (string set in SectionWorkspace.Sets) data["combinazioni"]![set] = new JsonArray();
        data["combinazioni"]!["SLU"] = new JsonArray(J.Obj(("id", "u1"), ("nome", "Flessione"), ("azioni", new[] { "0", "100", "20" })));
        return data;
    }

    // ================================================================ 11g. stato statico
    /// <summary>Eccezioni dichiarate della 11g: nome completo del campo → motivo.</summary>
    static readonly ImmutableDictionary<string, string> DeclaredStatic = ImmutableDictionary.CreateRange(new Dictionary<string, string>
    {
        ["Materiali.DurabilityLegacy+Durability.Covers"] = "tabella privata int[,] del prospetto 4.4N del nucleo legacy, scade in F2.11",
        ["Anthea.Calculations.ConcreteDurabilityAdapter.CurrentProbe"] = "sonda diagnostica AsyncLocal, nulla in produzione (F2.9-D14), da unificare in F2.11"
    });

    /// <summary>Dictionary del taglio e della torsione nella classe parziale di mappatura (F2.5-F2.6): li rende immutabili F2.7 (A2).</summary>
    static readonly ImmutableHashSet<string> ShearTorsionMappingFields = ["ShearExpressions", "ShearMessages", "TorsionMessages", "TorsionGeometryMessages"];

    static JsonObject StaticState(Action<bool, string> check)
    {
        var types = new List<Type> { typeof(Durability), typeof(NtcCover), typeof(MinimumConcrete), typeof(AtecapMix), typeof(MaterialCover), typeof(DurabilityLegacy), typeof(ConcreteDurabilityAdapter) };
        types.AddRange(typeof(DurabilityLegacy).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute))));
        int checkedMembers = 0; var declaredSeen = new HashSet<string>();
        void Field(Type type, FieldInfo field)
        {
            string id = type.FullName + "." + field.Name;
            checkedMembers++;
            if (DeclaredStatic.ContainsKey(id)) { declaredSeen.Add(id); return; }
            check(field.IsLiteral || field.IsInitOnly, "11g: campo statico scrivibile " + id);
            check(!Mutable(field.FieldType, field.IsLiteral ? null : field.GetValue(null)), $"11g: collezione statica modificabile {id} ({field.FieldType})");
        }
        foreach (var type in types)
        {
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(f => !f.Name.Contains('<')))
                Field(type, field);
            foreach (var property in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                string id = type.FullName + "." + property.Name;
                checkedMembers++;
                check(property.SetMethod is null, "11g: proprietà statica scrivibile " + id);
                check(!Mutable(property.PropertyType, property.GetValue(null)), $"11g: proprietà statica con collezione modificabile {id} ({property.PropertyType})");
            }
        }
        // Mappatura: tutti i campi della classe parziale; quelli di taglio e torsione modificabili sono dichiarati (F2.7 A2).
        var mapping = typeof(ConcreteLibraryMapping).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(f => !f.Name.Contains('<')).ToArray();
        check(mapping.Count(f => f.Name.StartsWith("Durability")) >= 5, "11g: campi della mappatura di durabilità non trovati");
        foreach (var field in mapping.Where(f => !ShearTorsionMappingFields.Contains(f.Name))) Field(typeof(ConcreteLibraryMapping), field);
        // Array dei copriferri dei muri (RetainingWall.Materials.cs), letti da CompleteMaterialInput, CoverMaterialState e ProjectWallFields.
        foreach (string name in new[] { "CoverChoiceDefaults", "CoverFlags", "CoverSheetPaths" })
        {
            var field = typeof(RetainingWall).GetField(name, BindingFlags.Static | BindingFlags.Public);
            check(field is not null, "11g: campo dei copriferri dei muri non trovato: " + name);
            check(field!.IsInitOnly && !Mutable(field.FieldType, field.GetValue(null)), $"11g: RetainingWall.{name} scrivibile o modificabile ({field.FieldType})");
            checkedMembers++;
        }
        check(declaredSeen.SetEquals(DeclaredStatic.Keys), "11g: eccezioni dichiarate non trovate: " + string.Join(", ", DeclaredStatic.Keys.Except(declaredSeen)));
        return new JsonObject { ["membri_statici_controllati"] = checkedMembers, ["eccezioni_dichiarate"] = new JsonArray(DeclaredStatic.Keys.Order().Select(k => (JsonNode)k).ToArray()) };
    }

    /// <summary>Collezione modificabile: array, collezioni che non siano immutabili (System.Collections.Immutable, System.Collections.Frozen),
    /// anche quando il tipo dichiarato è un'interfaccia di sola lettura costruita su un array.</summary>
    static bool Mutable(Type declared, object? value)
    {
        static bool Immutable(Type t) => t.Namespace is "System.Collections.Immutable" or "System.Collections.Frozen";
        static bool Collection(Type t) => t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t);
        if (declared.IsArray) return true;
        if (Collection(declared) && !Immutable(declared) && !(declared.IsInterface && value is not null && Immutable(value.GetType()))) return true;
        return value is not null && value.GetType().IsArray;
    }

    // ================================================================ 11h. attesi indipendenti
    static JsonObject Independent(string root, Action<bool, string> check)
    {
        int count = 0;
        foreach (var engine in Engines)
        {
            void Hand(double actual, double expected, string what) { check(actual == expected, $"11h: {what} ({engine}): {Show(actual)} invece di {Show(expected)} (valore a mano)"); count++; }
            Exposure[] E(params string[] codes) => codes.Select(c => ConcreteDurabilityAdapter.Exposures(engine).Single(e => e.Code == c)).ToArray();
            // Pagina del metodo ca.durabilita-copriferri di Checker, §10, esempio 1: NTC 2018, trave in XC3, C28/35, 50 anni, staffe Ø8,
            // dg = 20 mm, Δcdev = 10 mm, elemento non a piastra, senza controllo di qualità.
            var p1 = new CoverInput(50, false, false, false, 8, 20, 10, false, 0, 0);
            foreach (var (cmin, dur, nominal) in new (double?, double, double)[] { (30, 30, 40), (28, 25, 35), (null, 25, 35) })
            {
                var r = NtcCover.Calculate(E("XC3"), 28, p1, false, false, cmin, engine);
                string row = "esempio 1, Cmin " + Show(cmin);
                Hand(r.Severity, 0, row + ": gruppo"); Hand(r.C0, 35, row + ": C0"); Hand(r.TableCover, 25, row + ": tabella");
                Hand(r.Cover.Durability, dur, row + ": cmin,dur"); Hand(r.Cover.Minimum, dur, row + ": cmin"); Hand(r.Cover.Nominal, nominal, row + ": cnom");
            }
            Hand(MinimumConcrete.Required(E("XC3"), engine), 30, "esempio 1, classe minima NTC (UNI 11104)");
            check(MinimumConcrete.Label(30, engine) == "C30/37", $"11h: esempio 1, etichetta della classe minima ({engine})"); count++;
            Hand(E("XC3").Max(e => e.MinStrength), 30, "esempio 1, EN 206 F.1: classe");
            Hand(E("XC3")[0].MaxRatio!.Value, .55, "esempio 1, EN 206 F.1: a/c"); Hand(E("XC3")[0].MinCement, 280, "esempio 1, EN 206 F.1: cemento");
            var mix = AtecapMix.Required(E("XC3"), engine);
            Hand(mix.Ratio!.Value, .55, "esempio 1, UNI 11104:2016: a/c"); Hand(mix.Cement!.Value, 320, "esempio 1, UNI 11104:2016: cemento");
            // Esempio 2: XC4 + XD1, C40/50, staffe Ø12, dg = 20 mm, Δcdev = 10 mm.
            var p2 = new CoverInput(50, true, false, false, 12, 20, 10, false, 0, 0);
            foreach (var (label, p, cls, xc4, xd1, nominal) in new[] { ("EN con riduzione", p2, 3, 25.0, 30.0, 40.0), ("EN senza riduzione", p2 with { StrengthReduction = false }, 4, 30.0, 35.0, 45.0),
                ("EN 100 anni con riduzione", p2 with { Life = 100 }, 5, 35.0, 40.0, 50.0) })
            {
                var c = Durability.Cover(E("XC4", "XD1"), 40, p, engine);
                string row = "esempio 2, " + label;
                check(c.Lines.Length == 2 && c.Lines[0].Exposure == "XC4" && c.Lines[1].Exposure == "XD1", $"11h: {row}: righe ({engine})"); count++;
                Hand(c.Lines[0].StructuralClass, cls, row + ": classe strutturale XC4"); Hand(c.Lines[1].StructuralClass, cls, row + ": classe strutturale XD1");
                Hand(c.Lines[0].Durability, xc4, row + ": XC4"); Hand(c.Lines[1].Durability, xd1, row + ": XD1");
                Hand(c.Durability, xd1, row + ": cmin,dur"); Hand(c.Minimum, xd1, row + ": cmin"); Hand(c.Nominal, nominal, row + ": cnom");
            }
            int pertinent = MinimumConcrete.Required(E("XC4", "XD1"), engine);
            Hand(pertinent, 32, "esempio 2, classe minima NTC (UNI 11104 XC4)");
            var n = NtcCover.Calculate(E("XC4", "XD1"), 40, p2, false, false, pertinent, engine);
            Hand(n.Severity, 1, "esempio 2, NTC: gruppo"); Hand(n.C0, 40, "esempio 2, NTC: C0"); Hand(n.Cmin, 32, "esempio 2, NTC: Cmin"); Hand(n.TableCover, 30, "esempio 2, NTC: tabella");
            Hand(n.Cover.Durability, 30, "esempio 2, NTC: cmin,dur"); Hand(n.Cover.Minimum, 30, "esempio 2, NTC: cmin"); Hand(n.Cover.Nominal, 40, "esempio 2, NTC: cnom");
            Hand(E("XC4", "XD1").Max(e => e.MinStrength), 30, "esempio 2, EN 206 F.1: classe");
        }
        // Controlli di riferimento di supporto/test/Shared/DurabilityReferenceChecks.cs (valori a mano, prima Durability.Check e
        // NtcCover.Check), con ciascun motore: la sonda conferma che tutte le chiamate usano il motore richiesto.
        int reference = 0;
        foreach (var engine in Engines)
        {
            var (outcome, entries) = Probed(() => { DurabilityReferenceChecks.CheckDurability(engine); DurabilityReferenceChecks.CheckNtcCover(engine); return null; });
            check(outcome == "ok null", $"11h: DurabilityReferenceChecks con il motore {engine}: {outcome}");
            check(entries.Count > 20 && entries.All(e => e.Engine == engine), $"11h: DurabilityReferenceChecks con il motore {engine}: voci della sonda {Describe(entries.Where(e => e.Engine != engine))}");
            reference++;
        }
        // Il documento M1 del corpus di B6 è l'esempio 1 nella scheda Materiali (C28/35, XC3, trave, Ø8, Dmax 20, Δcdev 10): cnom = 40 mm,
        // con il calcolo headless (motore predefinito) e con MaterialCover.Required per ciascun motore.
        var m1 = CorpusDocuments(root).Single(d => d.Id.StartsWith("m01")).Data;
        var sheet = CalculationService.Calculate("mat_calcestruzzo", (JsonObject)m1.DeepClone());
        check(sheet.D("fck_mpa") == 28 && sheet.D("copriferro_nominale_mm") == 40, $"11h: documento M1 di B6: fck {sheet.D("fck_mpa")}, cnom {sheet.D("copriferro_nominale_mm")} invece di 28 e 40 mm (valore a mano)");
        foreach (var engine in Engines)
            check(MaterialCover.Required(m1, 28, engine) == 40, $"11h: documento M1 di B6 con il motore {engine}: cnom {MaterialCover.Required(m1, 28, engine)} invece di 40 mm (valore a mano)");
        return new JsonObject
        {
            ["documento_M1_di_B6"] = "cnom 40 mm (esempio 1)",
            ["controlli_di_riferimento_per_motore"] = reference,
            ["valori_a_mano_della_pagina_del_metodo"] = count,
            ["righe_non_raggiungibili_da_ANTHEA"] = "esempio 2: profilo DS e classi minime EN e DS (la scheda offre solo NTC + Circolare 2019 e EC2 2004; ANTHEA non chiama ExposureClasses.MinimumStrength, registro R17)"
        };
    }

    // ================================================================ 11i. scansione dei sorgenti
    /// <summary>Cartelle scansionate: codice di produzione (da E2) e prove (da E3).</summary>
    static readonly ImmutableArray<string> ProductionFolders = ["X.Calculations", "X.Core", "X.Desktop", "X.Materiali"];
    static readonly ImmutableArray<string> TestFolders = ["supporto/test", "tests"];
    static readonly ImmutableHashSet<string> OwnFiles = ["X.Calculations/ConcreteDurabilityAdapter.cs", "X.Calculations/ConcreteLibraryMapping.Durability.cs", "X.Calculations/Materials/DurabilityLegacy.cs"];

    static readonly (string Name, Regex Pattern, bool ProductionOnly)[] ScanRules =
    [
        ("legacy", new(@"\bDurabilityLegacy\b", RegexOptions.CultureInvariant), false),
        ("motore-legacy", new(@"\bDurabilityEngine\s*\.\s*Legacy\b", RegexOptions.CultureInvariant), true),
        ("libreria-durabilita", new(@"\bGPC\s*\.\s*Checkers\s*\.\s*Concrete\s*\.\s*Durability\b|\b(?:CoverRequirements|ExposureClasses|ExposureClass|DurabilityProfiles?|StrengthRequirement)\b", RegexOptions.CultureInvariant), false),
        ("classe-minima-per-profilo", new(@"\bExposureClasses\s*\.\s*MinimumStrength\b", RegexOptions.CultureInvariant), false)
    ];

    static JsonObject Scan(string root, Action<bool, string> check)
    {
        var allowlist = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests", "ConcreteLibraryAdapter.Checks", "legacy-allowlist-durabilita.json")))!["voci"]!.AsArray()
            .Select(v => (File: v!["file"]!.GetValue<string>(), Rule: v["regola"]!.GetValue<string>(), Lines: v["righe"]?.GetValue<int>(), WholeFile: v["tutto_il_file"]?.GetValue<bool>() == true,
                Texts: v["testo"]?.AsArray().Select(t => t!.GetValue<string>()).ToArray() ?? [])).ToList();
        var hits = new Dictionary<(string File, string Rule), List<string>>();
        int files = 0;
        foreach (var (folder, production) in ProductionFolders.Select(f => (f, true)).Concat(TestFolders.Select(f => (f, false))))
            foreach (string path in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (relative.Split('/').Any(part => part is "bin" or "obj") || OwnFiles.Contains(relative)) continue;
                files++;
                var lines = CodeOnly(File.ReadAllText(path)).Split('\n');
                foreach (var (name, pattern, productionOnly) in ScanRules)
                {
                    if (productionOnly && !production) continue;
                    foreach (string line in lines.Where(l => pattern.IsMatch(l)))
                    {
                        if (!hits.TryGetValue((relative, name), out var list)) hits[(relative, name)] = list = [];
                        list.Add(line.Trim());
                    }
                }
            }
        foreach (var ((file, rule), lines) in hits)
        {
            var entry = allowlist.FirstOrDefault(a => a.File == file && a.Rule == rule);
            check(entry.File is not null, $"11i: {file}: uso vietato ({rule}) fuori dall'elenco ammesso: {lines[0]}");
            if (entry.WholeFile) continue;
            check(entry.Lines == lines.Count, $"11i: {file}: {lines.Count} righe con '{rule}', {entry.Lines} nell'elenco ammesso: {string.Join(" | ", lines)}");
            foreach (string line in lines) check(entry.Texts.Any(line.Contains), $"11i: {file}: riga '{rule}' non prevista dall'elenco ammesso: {line}");
        }
        foreach (var entry in allowlist)
            check(hits.ContainsKey((entry.File, entry.Rule)), $"11i: voce dell'elenco ammesso senza riscontro: {entry.File} ({entry.Rule})");
        return new JsonObject { ["file_scansionati"] = files, ["voci_dell_elenco_ammesso"] = allowlist.Count, ["righe_ammesse"] = hits.Values.Sum(l => l.Count) };
    }

    /// <summary>Codice senza commenti, stringhe e caratteri, con le stesse righe.</summary>
    static string CodeOnly(string source)
    {
        const string Pattern = @"/\*[\s\S]*?\*/|//[^\n]*|\$*""""""[\s\S]*?""""""|(?:\$@|@\$|@)""(?:[^""]|"""")*""|\$?""(?:[^""\\\n]|\\.)*""|'(?:[^'\\\n]|\\.)'";
        return Regex.Replace(source.Replace("\r\n", "\n"), Pattern, m => new string('\n', m.Value.Count(c => c == '\n')) + " ");
    }
}
