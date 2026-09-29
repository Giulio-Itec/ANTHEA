using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

internal static class SeismicSoilChecks
{
    internal static int Run(string directory)
    {
        var log = new List<string>();
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); log.Add("OK " + message); }
        void Near(double a, double b, string message) => Check(Math.Abs(a - b) < 1e-9, message);
        void Reject(Action action, string message) { try { action(); } catch (ArgumentException) { Check(true, message); return; } throw new Exception("Accettato: " + message); }
        var d = RetainingWall.Example("gravity"); var s = d["seismic"]!;
        s["enabled"] = true; s["source"] = RetainingWall.SeismicSite; s["ag_g"] = .2; s["f0"] = 2.5; s["soil_class"] = "C";
        s["topography"] = RetainingWall.TopographySlope; s["slope"] = 20; s["relief_height"] = 100; s["site_height"] = 50;
        var derived = RetainingWall.DeriveSeismic(d)!;
        Near(derived.Ss, 1.4, "Ss categoria C da F₀ e ag/g"); Near(derived.St, 1.1, "St T2 a metà altezza"); Near(derived.AmaxG, .308, "amax/g include Ss e St");
        Near(derived.Kh, .11704, "kh SLV con βm=0,38"); Near(derived.Kv, .05852, "kv metà di kh"); Near(derived.KhOverturning, .17556, "Ribaltamento con βm maggiorato del 50%");
        s["method"] = "Wood semplificato"; var wood = RetainingWall.DeriveSeismic(d)!;
        Near(wood.Beta, 1, "Wood vincolato non riduce accelerazione"); Near(wood.KhOverturning, wood.Kh, "βm ribaltamento limitato a uno");
        s["method"] = "Mononobe–Okabe";
        foreach (var (category, expected) in new[] { ("A", 1d), ("B", 1.2), ("C", 1.4), ("D", 1.65), ("E", 1.45) })
        { s["soil_class"] = category; Near(RetainingWall.DeriveSeismic(d)!.Ss, expected, "Ss categoria " + category); }
        s["soil_class"] = "D"; s["ag_g"] = .8; Near(RetainingWall.DeriveSeismic(d)!.Ss, .9, "Ss D limite inferiore");
        s["ag_g"] = .01; Near(RetainingWall.DeriveSeismic(d)!.Ss, 1.8, "Ss D limite superiore"); s["ag_g"] = .2; s["soil_class"] = "C";
        s["topography"] = RetainingWall.TopographyRidge; s["slope"] = 31; s["site_height"] = 100; Near(RetainingWall.DeriveSeismic(d)!.St, 1.4, "St T4 in cresta");
        s["slope"] = 30; Near(RetainingWall.DeriveSeismic(d)!.St, 1.2, "St T3 in cresta"); s["site_height"] = 0; Near(RetainingWall.DeriveSeismic(d)!.St, 1, "St alla base");
        s["slope"] = 15; Near(RetainingWall.DeriveSeismic(d)!.St, 1, "T1 fino a 15 gradi");
        s["slope"] = 25; s["relief_height"] = 30; s["site_height"] = 30; Near(RetainingWall.DeriveSeismic(d)!.St, 1, "Rilievo non maggiore di 30 m");
        s["site_height"] = 31; Reject(() => RetainingWall.DeriveSeismic(d), "Quota oltre la cresta rifiutata"); s["site_height"] = 15;
        s["f0"] = ""; Reject(() => RetainingWall.DeriveSeismic(d), "F₀ mancante non sostituito con valore presunto"); s["f0"] = 2.5;
        s["ag_g"] = 20; Reject(() => RetainingWall.DeriveSeismic(d), "ag/g percentuale non accettato come rapporto"); s["ag_g"] = .2;
        s["ss_mode"] = RetainingWall.AmplificationAssigned; s["ss"] = 1.3; s["st_mode"] = RetainingWall.AmplificationAssigned; s["st"] = 1.2;
        Near(RetainingWall.DeriveSeismic(d)!.AmaxG, .312, "Amplificazioni assegnate disponibili");
        var result = RetainingWall.Calculate(d); var cases = result.Cases.Where(c => c.State == "SISMA").ToArray();
        Check(cases.Length == 4 && cases.Count(c => c.Factors.S("purpose") == "Ribaltamento") == 2, "Quattro casi distinti ±kv generali e ribaltamento");
        Check(cases.All(c => c.Factors.D("rslide") == 1 && c.Factors.D("rover") == 1 && c.Factors.D("rbearing") == 1.2), "Coefficienti SLV tabella 7.11.III");
        Check(result.Checks.Where(c => c.Name == "Ribaltamento" && cases.Any(sc => sc.Name == c.Combination)).All(c => c.Combination.Contains("Ribaltamento")), "Verifica ribaltamento usa i casi dedicati");
        Check(result.Structural.All(c => !c.Combination.Contains("Ribaltamento")), "Casi di ribaltamento separati dalle verifiche strutturali");
        Check(cases.All(c => c.BearingResistance is null), "Portanza sismica resta esplicitamente indisponibile");
        var custom = (JsonObject)result.Input.DeepClone(); custom["combination_mode"] = "Personalizzate"; custom["combination_signature"] = RetainingWall.CombinationSignature(custom);
        custom["seismic"]!["ag_g"] = .25; Reject(() => RetainingWall.Calculate(custom), "Variazione del sito invalida la matrice personalizzata");
        byte[] report = ReportRetainingWall.Create("Sisma da parametri del sito", result); File.WriteAllBytes(Path.Combine(directory, "sisma-sito.docx"), report);
        using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(report)))
        using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open())) Check(reader.ReadToEnd().Contains("Dati del sito per il calcolo"), "Relazione comprende i dati sismici del sito");
        var wall = RetainingWall.Defaults(); wall.Array("layers")[0]!["thickness"] = 4; var bottom = RetainingWall.Layer(); bottom["name"] = "Sabbia profonda"; bottom["thickness"] = 8; bottom["phi"] = 35; wall.Array("layers").Add(bottom);
        wall["water"]!["enabled"] = true; wall["water"]!["depth"] = 2;
        var profile = SoilProfileTransfer.Extract(RetainingWall.Module, wall); var pile = SoilProfileTransfer.Create("geo_palo_verticale", profile);
        Check(pile.Array("stratigrafie")[0]!.AsArray().Count == 2 && pile["stratigrafie"]![0]![1].D("spessore") == 8, "Trasferimento mantiene tutti gli strati anche sotto il muro");
        Near(pile["stratigrafie"]![0]![1].D("angolo_attrito"), 35, "Trasferimento φ′ senza conversioni improprie");
        Check(pile["generali"].B("presenza_falda") && pile["generali"].D("profondita_falda") == 2, "Trasferimento falda e stessa origine delle quote");
        Check(pile["generali"].S("lunghezza") == "" && pile["stratigrafie"]![0]![0].S("addensamento") == "", "Dati specifici del palo non inventati");
        var imported = SoilProfileTransfer.Apply(RetainingWall.Module, wall, SoilProfileTransfer.Extract("geo_palo_verticale", pile));
        Check(J.Equivalent(imported["layers"], wall["layers"]), "Andata e ritorno muro-palo conserva nomi e parametri");
        pile["stratigrafie"]![0]![0]!["angolo_attrito"] = 32; Near(wall["layers"]![0].D("phi"), 30, "Copia indipendente dal terreno sorgente");
        var horizontal = SoilProfileTransfer.Create(PaloOrizzontale.Module, profile); var micro = SoilProfileTransfer.Create(MicropaloOrizzontale.Module, profile);
        Check(horizontal["stratigrafie"]![0]!.AsArray().Count == 2 && micro["stratigrafie"]![0]!.AsArray().Count == 2, "Profilo riutilizzabile nei pali orizzontali");
        var multi = (JsonObject)pile.DeepClone(); multi.Array("stratigrafie").Add(pile["stratigrafie"]![0]!.DeepClone());
        var replaced = SoilProfileTransfer.Apply("geo_palo_verticale", multi, profile, 1);
        Near(replaced["stratigrafie"]![0]![0].D("angolo_attrito"), 32, "Importazione non sostituisce altri sondaggi");
        var cohesive = (JsonObject)profile.DeepClone(); cohesive["layers"]![0]!["tipologia"] = "Coesivo";
        Reject(() => SoilProfileTransfer.Apply(RetainingWall.Module, wall, cohesive), "Terreno coesivo non convertito silenziosamente per i muri");
        Check(!SoilProfileTransfer.Supports("geo_micropalo_verticale"), "Bustamante mantiene lo schema specifico");
        var reopened = JsonNode.Parse(profile.ToJsonString())!.AsObject(); Check(J.Equivalent(SoilProfileTransfer.Create("geo_palo_verticale", reopened), SoilProfileTransfer.Create("geo_palo_verticale", profile)), "Profilo portatile riapribile senza perdita dei dati");
        File.WriteAllText(Path.Combine(directory, "Terreno.anthea-terreno.json"), profile.ToJsonString(J.Options));
        File.WriteAllLines(Path.Combine(directory, "sisma-terreno-test.txt"), log); Console.WriteLine($"PASS {log.Count} sisma/terreno"); return log.Count;
    }
}
