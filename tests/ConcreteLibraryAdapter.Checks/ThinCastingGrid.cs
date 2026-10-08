using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Calculations;

/// <summary>
/// Prova 7a del refactoring F2.7 (commit A3, percorso P12 di §6.1 del progetto F2.7): fattore dei getti sottili, sostituzione diretta
/// senza motore. Il fattore viene da ThinCasting della libreria attraverso la mappatura (ConcreteLibraryMapping.ThinCastingFactor) e vale
/// per il limite SLE del calcestruzzo e per αcc (CheckerSection) e per fcd (ConcreteMaterials.DesignValues), rilievo M14.
/// 1. Griglia: 9 norme × 'gettato_sottile' ∈ {«Sì», «No», «si», assente}, analisi lineare, più NTC 2018 «Sì» e «No» non lineari. Limiti SLE
///    (rara e quasi permanente), tassi e stati, αcc e fcd della sezione, fcd di DesignValues uguali bit per bit all'espressione legacy
///    di prima di A3, scritta qui come atteso: fattore 0,8 con normativa «NTC 2018» e 'gettato_sottile' = «Sì», altrimenti 1
///    (CheckerSection.cs:77 e ConcreteMaterials.cs:11 a 955b48b).
/// 2. Ordine dei rifiuti: il fattore si calcola dopo ConcreteStandards.Effective; con assi non validi e norma sconosciuta il primo rifiuto
///    resta quello degli assi, con assi validi quello della norma (come prima di A3).
/// 3. Fonte unica: nei sorgenti di X.Calculations, X.Core e X.Desktop la regola non è più scritta in linea (nessun nome di norma e nessun
///    numero decimale nelle istruzioni che leggono 'gettato_sottile'); la leggono solo CheckerSection e DesignValues, entrambe con il
///    fattore della mappatura, e ThinCasting della libreria si chiama solo dalla mappatura.
/// </summary>
static class ThinCastingGrid
{
    public sealed record Counts(int Cases, int Reduced, int Rejections, int Readers);

    public static Counts Run(string root, Action<bool, string> check)
    {
        int cases = 0, reduced = 0, rejections = 0;
        var action = new ActionPoint(-500, 50, 0); // compressione e flessione: CLS compresso in entrambe le combinazioni con limite
        var grid = ConcreteStandards.Names.SelectMany(name => new[] { "Sì", "No", "si", null }.Select(flag => (name, flag, linear: true)))
            .Concat(new[] { ("NTC 2018", (string?)"Sì", false), ("NTC 2018", "No", false) });
        CheckerSectionModel? model = null;
        foreach (var (name, flag, linear) in grid)
        {
            var (input, settings) = Section(name, flag);
            model ??= CheckerSection.PrepareModel(input, settings); // geometria e materiali uguali in tutta la griglia
            double legacy = name == "NTC 2018" && flag == "Sì" ? .8 : 1; // espressione legacy (atteso)
            if (legacy != 1) reduced++;
            string id = $"7a {name} · gettato_sottile {flag ?? "assente"} · {(linear ? "lineare" : "non lineare")}";
            var errors = new List<string>();
            // Uguaglianza bit per bit (null con null).
            void Same(double? actual, double? expected, string what)
            {
                if (actual.HasValue != expected.HasValue || actual.HasValue && BitConverter.DoubleToInt64Bits(actual!.Value) != BitConverter.DoubleToInt64Bits(expected!.Value))
                    errors.Add($"{what} {actual:R} invece di {expected:R}");
            }

            var effective = ConcreteStandards.Effective(input, settings);
            var concrete = ConcreteMaterials.Concrete(input); var steel = ConcreteMaterials.Rebar(input);
            // fcd dei calcoli di taglio, torsione, muri, pali e sezione orizzontale.
            Same(ConcreteMaterials.DesignValues(input, settings).Fcd, Math.Abs(concrete.CalculateFcd(effective)) * legacy, "fcd di DesignValues");

            var options = (JsonObject)settings["sle"]!["SLE"]!.DeepClone();
            options["modello"] = linear ? "Lineare" : "Non lineare";
            var engine = new CheckerSection(model, input, settings, options);
            var reducedStandard = ConcreteStandards.Effective(input, settings); reducedStandard.AlphaCC *= legacy;
            Same(engine.Checker.StandardModelCode2010.AlphaCC, effective.AlphaCC * legacy, "αcc della sezione");
            double phi = engine.Checker.SectionCheckerOptions.PsiCoefficientRebar, phiT = engine.Checker.SectionCheckerOptions.PsiCoefficientTendon;
            double steelLimit = effective.ServiceabilityStressSteelCoefficientForCharacteristicCombination * Math.Abs(steel.Fyk);
            // Tasso del CLS della regola legacy (CheckerSection.cs:192, :194 a 955b48b): vertici compressi, rapporto diviso per il fattore.
            double ConcreteRatio(IEnumerable<(double tension, double workingRatio)> points)
                => points.Where(p => p.tension < 0).Select(p => p.workingRatio / legacy).DefaultIfEmpty(0).Max();
            string Status(double? ratio) => ratio is null ? "Stato tensionale calcolato" : ratio <= 1 ? "Entro limiti tensionali" : "Oltre limiti tensionali";
            foreach (var set in new[] { "SLE", "SLE_QP", "SLE_FREQ" })
            {
                var state = engine.Stress(action, set); var native = state.Native;
                double? limit = set switch
                {
                    "SLE" => effective.ServiceabilityStressConcreteCoefficientForCharacteristicCombination * Math.Abs(concrete.Fck) * legacy,
                    "SLE_QP" => effective.ServiceabilityStressConcreteCoefficientForQuasiPermanentCombination * Math.Abs(concrete.Fck) * legacy,
                    _ => null
                };
                double? ratio = set switch
                {
                    "SLE" => Math.Max(ConcreteRatio((linear ? native.ConcreteServiceabilityCharacteristicCheck(phi) : native.ConcreteServiceabilityCharacteristicCheck()).Select(p => (p.tension, p.workingRatio))),
                        (linear ? native.SteelServiceabilityCharacteristicCheck(phi, phiT) : native.SteelServiceabilityCharacteristicCheck()).Max(p => p.workingRatio)),
                    "SLE_QP" => ConcreteRatio((linear ? native.ConcreteServiceabilityQuasiPermanentCheck(phi) : native.ConcreteServiceabilityQuasiPermanentCheck()).Select(p => (p.tension, p.workingRatio))),
                    _ => null
                };
                Same(state.ConcreteStressLimit, limit, set + ": limite del CLS");
                Same(state.SteelStressLimit, steelLimit, set + ": limite dell'acciaio");
                Same(state.Ratio, ratio, set + ": tasso");
                if (state.Status != Status(ratio)) errors.Add($"{set}: stato «{state.Status}» invece di «{Status(ratio)}»");
                Same(state.ConcreteCompressionStrength, Math.Abs(concrete.CalculateFcd(reducedStandard)), set + ": fcd della sezione");
                if (set != "SLE_FREQ" && !(state.Ratio > 0)) errors.Add(set + ": nessun tasso, griglia non significativa");
            }
            check(errors.Count == 0, id + ": " + string.Join("; ", errors));
            cases++;
        }
        check(cases == 38 && reduced == 2, $"7a: {cases} casi, {reduced} con riduzione (attesi 38 e 2)");

        // ---- ordine dei rifiuti
        static string Rejection(Func<object?> run)
        {
            try { run(); return "nessun rifiuto"; }
            catch (Exception e) { return e.GetType().Name + ": " + e.Message; }
        }
        const string Axes = "ArgumentException: Sistema di riferimento non riconosciuto.";
        foreach (var flag in new[] { "Sì", "No", "si", null })
        {
            foreach (var (norm, unknown) in new[] { ("ACI 318", true), ("NTC 2018", false) })
            {
                var (input, settings) = Section(norm, flag);
                // Con una norma nota, rifiuto di Effective per un coefficiente personalizzato non valido (non letto dalla geometria).
                if (!unknown) settings["coefficienti"] = new JsonObject { ["GammaCE"] = "-1" };
                string effective = unknown ? "ArgumentException: Normativa non disponibile nelle DLL: ACI 318" : "ArgumentException: γcE · modulo: inserire un valore positivo.";
                var valid = (JsonObject)settings["sle"]!["SLE"]!.DeepClone(); valid["modello"] = "Lineare";
                var axes = (JsonObject)valid.DeepClone(); axes["assi"] = "Polari";
                string id = $"7a rifiuti {norm} · gettato_sottile {flag ?? "assente"}";
                foreach (var (what, actual, expected) in new[]
                {
                    ("assi non validi", Rejection(() => new CheckerSection(model!, input, settings, axes)), Axes),
                    ("assi non validi, sezione da preparare", Rejection(() => new CheckerSection(input, settings, axes)), Axes),
                    ("assi validi", Rejection(() => new CheckerSection(model!, input, settings, valid)), effective),
                    ("DesignValues", Rejection(() => ConcreteMaterials.DesignValues(input, settings)), effective)
                })
                {
                    check(actual == expected, $"{id}, {what}: «{actual}» invece di «{expected}»");
                    rejections++;
                }
            }
        }
        Console.WriteLine($"7a griglia: {cases} casi ({reduced} con riduzione) e {rejections} rifiuti uguali all'espressione legacy");

        // ---- fonte unica
        var read = new Regex(@"\bS\(""gettato_sottile""[^)]*\)\s*==\s*""Sì""");
        var decimals = new Regex(@"(?<![\w.])\d*\.\d+");
        var readers = new List<string>();
        foreach (var folder in new[] { "X.Calculations", "X.Core", "X.Desktop" })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Split('/').Any(part => part is "bin" or "obj")) continue;
                string text = File.ReadAllText(file);
                foreach (var line in text.Split('\n').Where(l => read.IsMatch(l)))
                {
                    readers.Add(relative);
                    check(!line.Contains("\"NTC 2018\"") && !decimals.IsMatch(line), "7a: regola dei getti sottili in linea in " + relative + ": " + line.Trim());
                }
                check(!text.Contains("ThinCasting.Factor(") || relative == "X.Calculations/ConcreteLibraryMapping.cs", "7a: ThinCasting della libreria chiamata fuori dalla mappatura: " + relative);
            }
        var expectedReaders = new[] { "X.Calculations/CheckerSection.cs", "X.Calculations/ConcreteMaterials.cs" };
        check(readers.OrderBy(r => r, StringComparer.Ordinal).SequenceEqual(expectedReaders), "7a: letture di 'gettato_sottile' nei calcoli: " + string.Join(", ", readers));
        foreach (var reader in readers)
            check(File.ReadAllText(Path.Combine(root, reader)).Contains("ConcreteLibraryMapping.ThinCastingFactor("), "7a: " + reader + " non usa il fattore della mappatura");
        return new(cases, reduced, rejections, readers.Count);
    }

    /// <summary>Sezione predefinita del modulo con la norma e il valore di 'gettato_sottile' (null = chiave assente).</summary>
    static (JsonObject Input, JsonObject Settings) Section(string name, string? flag)
    {
        var data = SezioneCA.DefaultData(); var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        settings["normativa"] = name;
        if (flag is null) input.Remove("gettato_sottile"); else input["gettato_sottile"] = flag;
        return (input, settings);
    }
}
