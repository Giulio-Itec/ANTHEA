using System.Text.Json.Nodes;
using X.Core;
try
{
    if (args.Length == 2 && args[0] == "--project-calculations") { ProjectCalculationChecks.Run(args[1]); return 0; }
    if (args.Length == 1 && args[0] == "--ca-data") { ConcreteDataChecks.Run(); return 0; }
    if (args.Length == 1 && args[0] == "--bridge") { try { BridgeSectionChecks.Run(); return 0; } catch (Exception ex) { Console.Error.WriteLine(ex); return 1; } }
    if (args.Length is 2 or 3 && args[0] == "--software")
    {
        Action<string> trace = message =>
        {
            Console.WriteLine(message);
            if (args.Length == 3) File.AppendAllText(args[2], message + Environment.NewLine);
        };
        SoftwareChecks.Run(JsonNode.Parse(File.ReadAllText(args[1]))!.AsArray(), trace,
            args.Length == 3 ? Path.GetDirectoryName(Path.GetFullPath(args[2])) : null); return 0;
    }
    if (args.Length == 2 && args[0] == "--micropalo") { MicropileChecks.Run(JsonNode.Parse(File.ReadAllText(args[1]))!.AsArray()); return 0; }
    if (args.Length == 1 && args[0] == "--coesione") { CohesionChecks.Run(); return 0; }
    if (args.Length == 1 && args[0] == "--gamma-sat") { SaturatedWeightChecks.Run(); return 0; }
    if (args.Length == 1 && args[0] == "--horizontal") { Console.WriteLine($"Palo orizzontale: {HorizontalChecks.Run()} controlli superati."); HorizontalChsChecks.Run(); return 0; }
    if (args.Length == 1 && args[0] == "--checker") { SectionWorkspaceChecks.Run(); SectionExchangeChecks.Run(); ConcreteEnhancementChecks.Run(); ConcreteDataChecks.Run(); return 0; }
    if (args.Length == 1 && args[0] == "--ca-module") { ConcreteModuleChecks.Run(); return 0; }
    if (args.Length == 2 && args[0] == "--ca-benchmark") { ConcreteBenchmark.Run(args[1]); return 0; }
    bool referenceOnly = args.Length >= 2 && args[0] == "--reference-only";
    if (referenceOnly) args = args.Skip(1).ToArray();

    if (args.Length == 3 && args[0] == "--calcola")
    {
        var doc = JsonNode.Parse(File.ReadAllText(args[1]))!; var data = doc["dati"] ?? doc; var module = doc.S("modulo_id");
        var result = CalculationService.Calculate(module, data.AsObject());
        File.WriteAllText(args[2], result.ToJsonString(J.Options)); return result.S("errore") == "" ? 0 : 1;
    }
    if (args.Length == 4 && args[0] == "--attesi")
    {
        // Results of the cases whose name contains the filter, with the same calculations of the comparison: they regenerate the expected
        // values when a behaviour is deliberately changed (supporto/scripts/aggiorna_attesi.py merges them into casi_confronto.json).
        var all = JsonNode.Parse(File.ReadAllText(args[1]))!.AsArray(); var output = new JsonObject();
        foreach (var c in all.OfType<JsonObject>().Where(c => c.S("nome").Contains(args[2]))) output[c.S("nome")] = Actual(c);
        File.WriteAllText(args[3], output.ToJsonString()); Console.WriteLine($"{output.Count} casi calcolati."); return 0;
    }
    Console.WriteLine("ANTHEA — verifiche C#");
    if (args.Length == 0) { Console.WriteLine("Specificare il percorso del file casi_confronto.json."); return 2; }
    var cases = JsonNode.Parse(File.ReadAllText(args[0]))!.AsArray(); int count = 0, numbers = 0, failed = 0; double maxAbs = 0, maxRel = 0; string maxPath = "";
    void Compare(JsonNode? a, JsonNode? b, string path)
    {
        if (a is null || b is null) { if (a is not null || b is not null) throw new Exception(path + ": null diverso"); return; }
        if (a is JsonObject ao)
        {
            if (b is not JsonObject bo) throw new Exception(path + ": oggetto atteso");
            foreach (var (k, v) in ao) { if (k == "checks") continue; if (!bo.ContainsKey(k)) throw new Exception(path + "." + k + ": campo assente"); Compare(v, bo[k], path + "." + k); }
            return;
        }
        if (a is JsonArray ar)
        {
            if (b is not JsonArray br || ar.Count != br.Count) throw new Exception(path + ": lunghezza diversa");
            for (int i = 0; i < ar.Count; i++) Compare(ar[i], br[i], path + "[" + i + "]"); return;
        }
        if (a is JsonValue av && av.TryGetValue<double>(out double x) && b is JsonValue bv && bv.TryGetValue<double>(out double y))
        {
            numbers++; double abs = Math.Abs(x - y), rel = abs / Math.Max(1, Math.Abs(x)); if (abs > maxAbs) { maxAbs = abs; maxPath = path; }
            maxRel = Math.Max(maxRel, rel);
            // Errori double tra runtime; nessuna tolleranza percentuale ingegneristica.
            if (!double.IsFinite(y) || abs > 1e-8 + 1e-10 * Math.Abs(x)) throw new Exception($"{path}: Python={x:R}; C#={y:R}; delta={abs:R}"); return;
        }
        if (a.ToJsonString() != b.ToJsonString()) throw new Exception(path + ": valore diverso: " + a + " / " + b);
    }
    foreach (var item in cases)
    {
        try
        {
            var c = item!; JsonNode actual = Actual(c);
            if (c.B("atteso_errore")) { if (actual.S("errore") == "") throw new Exception("Errore atteso, calcolo accettato."); }
            else Compare(c["atteso"], actual, c.S("nome")); count++;
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + item.S("nome") + ": " + ex.Message); failed++; }
    }
    int softwareChecks = 0;
    if (!referenceOnly)
    {
        try { softwareChecks = SoftwareChecks.Run(cases); } catch (Exception ex) { failed++; Console.WriteLine("FAIL software: " + ex); }
        try { softwareChecks += SectionWorkspaceChecks.Run(); } catch (Exception ex) { failed++; Console.WriteLine("FAIL workspace CA: " + ex); }
        try { softwareChecks += SectionExchangeChecks.Run(); } catch (Exception ex) { failed++; Console.WriteLine("FAIL Excel azioni: " + ex); }
        try { softwareChecks += ConcreteEnhancementChecks.Run(); } catch (Exception ex) { failed++; Console.WriteLine("FAIL estensioni CA: " + ex); }
        try { softwareChecks += HorizontalChecks.Run(); } catch (Exception ex) { failed++; Console.WriteLine("FAIL palo orizzontale: " + ex); }
    }
    var report = J.Obj(("casi_superati", count), ("controlli_software_superati", softwareChecks), ("casi_falliti", failed), ("valori_numerici_confrontati", numbers), ("massimo_delta_assoluto", maxAbs), ("massimo_delta_relativo_scalato", maxRel), ("percorso_massimo_delta", maxPath), ("tolleranza_assoluta", 1e-8), ("tolleranza_relativa", 1e-10));
    Console.WriteLine(report.ToJsonString(J.Options)); if (args.Length > 1) File.WriteAllText(args[1], report.ToJsonString(J.Options)); return failed == 0 ? 0 : 1;
}
catch (Exception ex)
{
    // Emit failures explicitly instead of relying on process-wide exception handlers.
    Console.Error.WriteLine(ex);
    return 1;
}

// The C# result of a comparison case, by type
static JsonNode Actual(JsonNode c)
{
    string kind = c.S("tipo"); var input = c["input"]!;
    switch (kind)
    {
        case "palo": case "micropalo": return Calcolo.Calcola(input, kind == "micropalo");
        case "efficienza": return Calcolo.Efficienza(input);
        case "nq": return Nq.Dettaglio(input.D("phi"), input.D("rapporto"), input.B("grande"));
        case "chs": return Chs.Peso(input.S("profilo"), input.D("diametro"), input.D("gamma"));
        case "bd": return BustamanteDoix.Parametro(input.S("terreno"), input.S("iniezione"), input.D("pressione"), input.D("alpha"));
        case "sezione":
            var engine = new SezioneCA(input["parametri"]!.AsObject(), (int)input.D("nr", 28), (int)input.D("na", 96));
            return engine.Analyze(input.D("n"), input.D("mx"), input.D("my"));
        case "elastica":
            var e = new SezioneCA(input["parametri"]!.AsObject(), (int)input.D("nr", 28), (int)input.D("na", 96));
            return J.Node(SezioneElastica.Tensioni(e, input.D("coeff_n"), input.D("n"), input.D("mx"), input.D("my")))!;
        case "resistenza_elastica":
            var re = new SezioneCA(input["parametri"]!.AsObject(), (int)input.D("nr", 28), (int)input.D("na", 96));
            var (m, st) = SezioneElastica.Resistenza(re, input.D("coeff_n"), input.D("n"), input.D("direction")); return J.Obj(("moment", m), ("state", st));
        case "dominio":
            var de = new SezioneCA(input["parametri"]!.AsObject(), 12, 36); return J.Obj(("nm", Domini.NM(de, input.S("mode"), input.D("coeff_n"))), ("mm", Domini.MM(de, input.S("mode"), input.D("coeff_n"), input.D("n"), 12)));
        default: throw new Exception("Tipo sconosciuto: " + kind);
    }
}
