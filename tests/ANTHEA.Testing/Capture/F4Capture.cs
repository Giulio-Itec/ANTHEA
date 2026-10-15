using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Testing.Normalization;
using X.Core;

namespace Anthea.Testing.Capture;

/// <summary>Frozen behaviour before F4 ports. Uses the common canonical writer and provenance manifest, never new expected calculations.</summary>
public sealed class F4Capture(string root, string output)
{
    readonly TextNormalizer text = new([(root, "<RADICE>"), (output, "<USCITA>")]);
    readonly JsonArray cases = [], timings = [];
    readonly Dictionary<string, int> counts = new(StringComparer.Ordinal);
    readonly HashSet<string> identities = new(StringComparer.Ordinal);
    int rejected;

    public void Run()
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new IOException("La cattura F4 non sovrascrive una cartella esistente.");
        Directory.CreateDirectory(Path.Combine(output, "raw"));
        var started = DateTimeOffset.Now; var watch = Stopwatch.StartNew();
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("it-IT");
        Walls(); Schedules(); WallDesign(); Piles(); SectionDesign();
        var options = new CaptureOptions(root, output, "F4-before-port", null, null, false, "it-IT");
        var manifest = Manifest.Build(options, output, cases, counts, 0, 0, started, watch.Elapsed);
        manifest["strumento"] = "ANTHEA.Testing/F4";
        manifest["rifiuti_attesi"] = rejected;
        manifest["non_coperto"] = new JsonArray("Bridge Design: sospeso dall’utente", "Rendering WPF: suite di equivalenza separate", "Validazione normativa: attesi indipendenti nelle suite di libreria");
        manifest["diagnostica_separata"] = new JsonArray("Performance della ricerca CA (tempi e contatori strumentali)", "armature.tratti[*].tempi_ms del palo", "Tempi esterni per caso");
        const string pileFixture = "tests/fixtures/f3/inputs/geo_palo_orizzontale--single-soil-elastic.json";
        manifest["fixture_palo"] = new JsonObject { ["file"] = pileFixture, ["sha256"] = Corpus.Sha256(Path.Combine(root, pileFixture)) };
        if (manifest["librerie"]?["coerenti_con_manifest"]?.GetValue<bool>() != true)
            throw new InvalidOperationException("Cattura F4 con DLL diverse dal manifest.");
        File.WriteAllText(Path.Combine(output, "manifest.json"), manifest.ToJsonString(J.Options), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, "raw", "timings.json"), timings.ToJsonString(J.Options), new UTF8Encoding(false));
        Console.WriteLine($"F4: {cases.Count} casi, {rejected} rifiuti attesi, {watch.Elapsed.TotalSeconds:F1} s; " + string.Join(", ", counts.Select(p => $"{p.Key}={p.Value}")));
    }

    void Case(string scope, string id, JsonObject input, Func<JsonObject, JsonNode?> calculate,
        Type? expectedError = null, string? message = null, bool report = false, JsonNode? configuration = null)
    {
        string key = scope + "/" + id;
        if (!identities.Add(key)) throw new InvalidOperationException("Caso duplicato: " + key);
        string before = input.ToJsonString(); var guids = new GuidMap(); CanonicalJson.Register(input, guids);
        Write("inputs/" + key + ".json", configuration is null ? input : new JsonObject { ["document"] = input.DeepClone(), ["configuration"] = configuration.DeepClone() }, guids);
        var watch = Stopwatch.StartNew(); JsonNode? value; string status;
        try
        {
            value = calculate(input); status = "ok";
            if (expectedError is not null) throw new InvalidOperationException(key + ": il rifiuto atteso non si è verificato.");
        }
        catch (Exception ex) when (expectedError is not null && expectedError.IsInstanceOfType(ex))
        {
            if (message is not null && !ex.Message.Contains(message, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(key + ": rifiuto diverso dall’atteso: " + ex.Message, ex);
            value = new JsonObject { ["type"] = ex.GetType().FullName, ["message"] = ex.Message }; status = "refused"; rejected++;
        }
        if (input.ToJsonString() != before) throw new InvalidOperationException(key + ": input modificato dal servizio.");
        Write("results/" + key + ".json", new JsonObject { ["status"] = status, ["value"] = value }, guids);
        counts[scope] = counts.GetValueOrDefault(scope) + 1;
        cases.Add(new JsonObject { ["modulo"] = scope, ["caso"] = id, ["origine"] = "F4 input sintetico derivato da API/fixture esistenti",
            ["file"] = "tests/ANTHEA.Testing/Capture/F4Capture.cs", ["esito"] = status, ["input_invariato"] = true });
        timings.Add(new JsonObject { ["case"] = key, ["seconds"] = watch.Elapsed.TotalSeconds });
        if (report && status == "ok")
        {
            // Reuse the product report builder; DOCX bytes are raw evidence, extracted text is compared.
            var result = RetainingWall.Calculate(input);
            byte[] bytes = ReportRetainingWall.Create("F4 · " + id, result);
            string path = Path.Combine(output, "raw", scope + "-" + id + ".docx"); File.WriteAllBytes(path, bytes);
            string target = Path.Combine(output, "reports", key + ".txt"); Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, DocxText.Extract(bytes, text, guids), new UTF8Encoding(false));
        }
    }

    void Write(string relative, JsonNode? node, GuidMap guids)
    {
        string path = Path.Combine(output, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        CanonicalJson.WriteFile(path, node, guids, text);
    }

    static JsonObject Wall(string family = "cantilever") => RetainingWall.Example(family);
    static JsonNode? WallResult(JsonObject d)
    {
        var r = RetainingWall.Calculate(d);
        if (r.Cases.Count == 0 || r.Structural.Count == 0) throw new InvalidOperationException("Muro senza casi o verifiche strutturali.");
        return CanonicalJson.FromObject(r);
    }

    void Walls()
    {
        foreach (string exposure in new[] { "XC1", "XC3", "XC4", "XD1", "XS1", "XF4" })
            foreach (int fck in new[] { 25, 35, 45 })
            {
                var d = Wall(); d["materials"]!["exposure"] = exposure; d["materials"]!["fck"] = fck;
                d["materials"]!["classe_cls"] = "Personalizzato"; d["materials"]!["cover"] = fck == 25 ? 30 : fck == 35 ? 50 : 60;
                Case("walls", exposure + "-C" + fck, d, WallResult, report: exposure == "XC1" && fck == 35);
            }
        foreach (bool two in new[] { false, true }) foreach (bool symmetric in new[] { false, true }) foreach (bool details in new[] { false, true })
        {
            var d = Wall(); d["reinforcement"]!["two_zones"] = two; d["detailing"]!["enabled"] = details;
            d["reinforcement"]!["lower_height"] = d["geometry"].D("height") / 2;
            foreach (string zone in new[] { "stem", "stem_upper", "toe", "heel" })
            { var a = d["reinforcement"]![zone]!; a["symmetric"] = symmetric; a["opposite_count"] = 5; a["opposite_diameter"] = 12; }
            Case("walls", $"zones-{two}-symmetric-{symmetric}-details-{details}", d, WallResult);
        }
        foreach (string material in new[] { "Muratura", "Calcestruzzo non armato", "Resistenze assegnate" }) foreach (double e in new[] { 0d, 30d })
        {
            var d = Wall("gravity"); d["gravity_design"]!["type"] = material; d["gravity_design"]!["eccentricity"] = e;
            d["gravity_design"]!["fk"] = 5; d["gravity_design"]!["fvk0"] = .15; d["gravity_design"]!["fvk_limit"] = .5;
            d["gravity_design"]!["elastic_modulus"] = 3000;
            Case("walls", $"gravity-{Array.IndexOf(new[] { "Muratura", "Calcestruzzo non armato", "Resistenze assegnate" }, material)}-e{e}", d, WallResult, report: e == 0);
        }
        var wrong = Wall(); wrong["geometry"]!["height"] = -1;
        Case("walls", "invalid-height", wrong, WallResult, typeof(ArgumentException));
        wrong = Wall("gravity"); wrong["gravity_design"]!["type"] = "Calcestruzzo non armato"; wrong["gravity_design"]!["effective_height"] = 1;
        Case("walls", "invalid-effective-height", wrong, WallResult, typeof(ArgumentException), "2H");
        using var stop = new CancellationTokenSource(); stop.Cancel();
        Case("walls", "cancelled", Wall(), d => CanonicalJson.FromObject(RetainingWall.Calculate(d, stop.Token)), typeof(OperationCanceledException));
    }

    void Schedules()
    {
        for (int i = 0; i < 30; i++)
        {
            var d = Wall(); bool two = i % 2 == 0; d["reinforcement"]!["two_zones"] = two;
            d["reinforcement"]!["lower_height"] = d["geometry"].D("height") / 2;
            var s = d["bar_schedule"]!; s["panel_length"] = 2 + i % 6; s["stock_length"] = i % 3 == 0 ? 6 : 12;
            s["end_cover"] = 25 + 5 * (i % 5); s["ties_per_m"] = 2 + i % 4; s["tie_cut_length"] = i % 3 == 0 ? 0 : 900;
            Case("schedules", "panel-" + i.ToString("D2"), d, x =>
            {
                var r = RetainingWall.CalculateBarSchedule(x);
                if (r.Bars.Count == 0 || r.Bars.Any(b => b.Quantity <= 0)) throw new InvalidOperationException("Distinta vuota o quantità non positiva.");
                return new JsonObject { ["schedule"] = CanonicalJson.FromObject(r), ["csv"] = ReportRetainingWall.BarScheduleCsv(r) };
            });
        }
        var wrong = Wall(); wrong["bar_schedule"]!["panel_length"] = 0;
        Case("schedules", "invalid-panel", wrong, d => CanonicalJson.FromObject(RetainingWall.CalculateBarSchedule(d)), typeof(ArgumentException));
        Case("schedules", "unsupported-family", Wall("gravity"), d => CanonicalJson.FromObject(RetainingWall.CalculateBarSchedule(d)), typeof(ArgumentException), "mensola");
    }

    void WallDesign()
    {
        for (int i = 0; i < 12; i++)
        {
            var d = Wall(); d["detailing"]!["max_count"] = 4 + i % 3; d["detailing"]!["max_diameter"] = 12 + 4 * (i % 3);
            d["detailing"]!["target_ratio"] = i % 2 == 0 ? 1 : .75;
            d["reinforcement"]!["two_zones"] = i >= 6; d["reinforcement"]!["lower_height"] = d["geometry"].D("height") / 2;
            Case("wall-design", "catalog-" + i.ToString("D2"), d, x => CanonicalJson.FromObject(RetainingWall.DesignReinforcement(x)));
        }
        var wrong = Wall(); wrong["detailing"]!["max_count"] = 4.5;
        Case("wall-design", "invalid-count", wrong, d => CanonicalJson.FromObject(RetainingWall.DesignReinforcement(d)), typeof(ArgumentException), "intero");
        Case("wall-design", "unsupported-family", Wall("gravity"), d => CanonicalJson.FromObject(RetainingWall.DesignReinforcement(d)), typeof(ArgumentException), "mensola");
        using var stop = new CancellationTokenSource(); stop.Cancel();
        Case("wall-design", "cancelled", Wall(), d => CanonicalJson.FromObject(RetainingWall.DesignReinforcement(d, stop.Token)), typeof(OperationCanceledException));
    }

    JsonObject Pile()
    {
        string fixture = Path.Combine(root, "tests/fixtures/f3/inputs/geo_palo_orizzontale--single-soil-elastic.json");
        var d = JsonNode.Parse(File.ReadAllText(fixture))!["data"]!.AsObject(); ElasticHorizontalPile.PrepareShared(d);
        d["generali"]!["azione_orizzontale"] = 1; d["elastico"]!["passo"] = 1;
        d["elastico"]!["dettagli"]!["azioni_progetto"] = true; d["elastico"]!["dettagli"]!["taglio_confermato"] = true;
        return d;
    }

    JsonNode PileResult(JsonObject d)
    {
        var response = ElasticHorizontalPile.CalculateResponse(d);
        var result = ElasticHorizontalPile.CompleteReinforcement(d, response, maximumParallelism: 1);
        var diagnostic = new JsonArray();
        foreach (var segment in result["armature"]!.Array("tratti").OfType<JsonObject>())
        {
            if (segment.Remove("tempi_ms", out var time)) diagnostic.Add(time);
        }
        timings.Add(new JsonObject { ["pile_diagnostics"] = diagnostic });
        return new JsonObject { ["result"] = result, ["suggested_segments"] = ElasticHorizontalPile.ProposeSegments(d, result) };
    }

    void Piles()
    {
        for (int law = 0; law < 6; law++) foreach (bool water in new[] { false, true })
        {
            var d = Pile(); var layer = d["stratigrafie"]![0]![0]!; var soil = layer["reazione_orizzontale"]!.AsObject();
            layer["tipologia"] = law == 4 ? "Coesivo" : "Granulare";
            soil["legge"] = ElasticHorizontalPile.Laws[law]; soil["valore"] = 10000; soil["nh_tabella"] = .2;
            soil["categoria"] = law == 4 ? "Argilla n.c. o lievemente o.c." : "";
            soil["scelta_per"] = ElasticHorizontalPile.SelectionKey(soil);
            d["generali"]!["presenza_falda"] = water; d["generali"]!["profondita_falda"] = 2;
            Case("piles", $"law-{law}-water-{water}", d, PileResult);
        }
        for (int i = 0; i < 8; i++)
        {
            var d = Pile(); d["generali"]!["azione_orizzontale"] = i % 2 == 0 ? 1 : -1;
            d["generali"]!["azione_assiale"] = i * -10;
            d["elastico"]!["dettagli"]!["sisma_testa"] = i >= 4;
            d["elastico"]!["tratti"] = new JsonArray(ElasticHorizontalPile.NewSegment("T1", 4), ElasticHorizontalPile.NewSegment("T2", null));
            d["sezione"]!["tipo_trasversale"] = i % 3 == 0 ? "Spirale" : i % 3 == 1 ? "Staffe singole" : "Da definire";
            Case("piles", "segments-" + i, d, PileResult);
        }
        var design = Pile();
        Case("piles", "design-segment", design, d => ElasticHorizontalPile.DesignSegment(d, ElasticHorizontalPile.CalculateResponse(d), 0, [16], [24], [10], [150]));
        var wrong = Pile(); wrong["generali"]!["lunghezza"] = -1;
        Case("piles", "invalid-length", wrong, PileResult, typeof(ArgumentException));
        wrong = Pile(); wrong["elastico"]!["dettagli"]!["azioni_progetto"] = false;
        Case("piles", "design-not-confirmed", wrong, d => ElasticHorizontalPile.DesignSegment(d, ElasticHorizontalPile.CalculateResponse(d), 0, [16], [24], [10], [150]), typeof(ArgumentException), "Confermare");
        using var stop = new CancellationTokenSource(); stop.Cancel();
        Case("piles", "cancelled", Pile(), d => ElasticHorizontalPile.CalculateResponse(d, stop.Token), typeof(OperationCanceledException));
    }

    static JsonObject Section()
    {
        var data = SezioneCA.DefaultData(); var ws = SectionWorkspace.Prepare(data); var input = data["input"]!;
        input["cover_mm"] = "40"; input["width_mm"] = "400"; input["height_mm"] = "600"; input["diameter_mm"] = "600";
        ws["dettagli_costruttivi"]!["elemento"] = "Trave"; ws["sle_comuni"]!["esposizione"] = "XC1"; ws["ancoraggi"]!["lunghezza"] = "2000";
        foreach (string set in SectionWorkspace.Sets) data["combinazioni"]![set] = new JsonArray();
        data["combinazioni"]!["SLU"] = new JsonArray(J.Obj(("id", "f4-u1"), ("nome", "Flessione"), ("azioni", new[] { "0", "100", "20" })));
        return data;
    }

    JsonNode DesignResult(JsonObject d, ConcreteDesignOptions options, CancellationToken cancellation = default)
    {
        var result = ConcreteReinforcementDesign.Optimize(d, options, cancellation);
        var value = CanonicalJson.FromObject(result)!.AsObject();
        if (value.Remove("Performance", out var performance)) timings.Add(new JsonObject { ["design_diagnostics"] = performance });
        // Business counts (evaluated/planned/exclusions), candidates, ranking and checks remain in the comparison.
        return value;
    }

    void SectionDesign()
    {
        var small = new ConcreteDesignOptions { Diameters = [16, 20], TopCounts = [3, 4], BottomCounts = [3, 4], SideCounts = [2],
            CircularCounts = [8, 12], StirrupDiameters = [8], StirrupSpacings = [100, 150], MaxEvaluations = 1000, MaxParallelism = 1 };
        foreach (int workers in new[] { 1, 4 }) foreach (string shape in new[] { "Rettangolare", "Circolare" })
        {
            var d = Section(); d["input"]!["shape"] = shape;
            d["workspace_ca"]!["dettagli_costruttivi"]!["elemento"] = shape == "Circolare" ? "Pilastro" : "Trave";
            d["workspace_ca"]!["taglio"]!["modello_circolare"] = "Parametri assegnati";
            var options = small with { MaxParallelism = workers };
            Case("section-design", shape + "-workers-" + workers, d, x =>
            {
                var r = DesignResult(x, options);
                if (r["Candidates"]!.AsArray().Count == 0 || r["Completed"]!.GetValue<bool>() != true) throw new InvalidOperationException("Ricerca di riferimento senza candidati o incompleta.");
                return r;
            }, configuration: CanonicalJson.FromObject(options));
        }
        var impossible = Section(); impossible["combinazioni"]!["SLU"]![0]!["azioni"] = new JsonArray("0", "100000", "0");
        Case("section-design", "no-solution", impossible, d =>
        {
            var r = DesignResult(d, small); if (r["Candidates"]!.AsArray().Count != 0) throw new InvalidOperationException("Richiesta impossibile accettata."); return r;
        }, configuration: CanonicalJson.FromObject(small));
        Case("section-design", "budget-limited", Section(), d =>
        {
            var r = DesignResult(d, small with { MaxEvaluations = 1 });
            if (r["Completed"]!.GetValue<bool>() || r["Evaluated"]!.GetValue<int>() != 1) throw new InvalidOperationException("Budget non rispettato."); return r;
        }, configuration: CanonicalJson.FromObject(small with { MaxEvaluations = 1 }));
        Case("section-design", "invalid-diameter", Section(), d => DesignResult(d, small with { Diameters = [double.NaN] }), typeof(ArgumentException), "Diametri", configuration: CanonicalJson.FromObject(small with { Diameters = [double.NaN] }));
        var missing = Section(); missing["workspace_ca"]!["sle_comuni"]!["esposizione"] = "Da scegliere";
        Case("section-design", "missing-exposure", missing, d => DesignResult(d, small), typeof(ArgumentException), configuration: CanonicalJson.FromObject(small));
        using var stop = new CancellationTokenSource(); stop.Cancel();
        Case("section-design", "cancelled", Section(), d => DesignResult(d, small, stop.Token), typeof(OperationCanceledException), configuration: CanonicalJson.FromObject(small));
    }
}
