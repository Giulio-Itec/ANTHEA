using System.Globalization;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using X.Core;

/// <summary>
/// γb of NTC 2018 Tab. 6.4.II (R3) by pile technology, read from Model through Calcolo.SicurezzaBaseNormativa (D7-d), and the rule of
/// option d2: the value of the technology for a sheet without γb, the one-off migration of the sheets saved before version 2
/// (Calcolo.AggiornaFoglio) and the same rule in calculation, report and projects (Calcolo.SicurezzaBase).
/// </summary>
internal static class PileFactorChecks
{
    static readonly (string Type, string? Driven)[] Technologies = [("Trivellato", null), ("Elica continua", null), ("Battuto", "Profilato d'acciaio"),
        ("Battuto", "Tubo d'acciaio chiuso"), ("Battuto", "Calcestruzzo prefabbricato"), ("Battuto", "Calcestruzzo gettato in opera")];

    internal static void Run()
    {
        int passed = 0;
        void Assert(bool ok, string message) { if (!ok) throw new Exception(message); passed++; }
        double Base(string type, string? driven = null, bool micro = false)
        {
            var g = Archivio.NuovoFoglio(micro ? "geo_micropalo_verticale" : "geo_palo_verticale")["generali"]!.AsObject();
            g["tipo_palo"] = type; if (driven is not null) g["sottotipo_palo_battuto"] = driven;
            return Calcolo.SicurezzaBaseNormativa(g);
        }
        Assert(Base("Trivellato") == 1.35, "γb trivellato 1,35");
        Assert(Base("Elica continua") == 1.30, "γb elica continua 1,30");
        foreach (string driven in new[] { "Profilato d'acciaio", "Tubo d'acciaio chiuso", "Calcestruzzo prefabbricato", "Calcestruzzo gettato in opera" })
            Assert(Base("Battuto", driven) == 1.15, "γb battuto 1,15: " + driven);
        Assert(Base("Trivellato", micro: true) == 1.35, "γb micropalo come trivellato");
        var fresh = Archivio.NuovoFoglio("geo_palo_verticale")["generali"]!;
        Assert(fresh.D("sicurezza_base") == Calcolo.SicurezzaBaseNormativa(fresh), "Foglio nuovo: γb della tecnologia predefinita");

        // The sheet keeps its γb and the calculation declares a value different from the technology's.
        var data = Pile("Trivellato", null); var g = data["generali"]!;
        bool Declared(JsonObject result) => result.Array("avvisi").Any(a => a!.GetValue<string>().StartsWith("γb = "));
        var bored = Calcolo.Calcola(data);
        Assert(bored.S("errore") == "" && !Declared(bored), "Trivellato con 1,35: nessun avviso");
        g["tipo_palo"] = "Battuto"; var stored = Calcolo.Calcola(data);
        Assert(stored.S("errore") == "" && Declared(stored), "Battuto con 1,35 memorizzato nel foglio nuovo: avviso");
        Assert(stored.Array("avvisi").Any(a => a!.GetValue<string>() == "γb = 1,35 diverso dal valore della NTC 2018 Tab. 6.4.II per la tecnologia del palo (1,15): il calcolo usa il valore del foglio."), "Testo dell'avviso γb");
        g["sicurezza_base"] = "1.15"; var normative = Calcolo.Calcola(data);
        Assert(normative.S("errore") == "" && !Declared(normative), "Battuto con 1,15: nessun avviso");
        Assert(g.S("sicurezza_base") == "1.15" && stored["curve"]!.ToJsonString() != normative["curve"]!.ToJsonString(), "Il calcolo usa il γb del foglio");

        // d2, version of the sheet: new sheets carry it, the validation accepts versions 1 and 2 only.
        foreach (string module in new[] { "geo_palo_verticale", "geo_micropalo_verticale" })
        {
            var created = ModuleCatalog.CreateData(module);
            Assert(created.D("versione") == Calcolo.VersioneFoglio && !Calcolo.AggiornaFoglio(created), "Foglio nuovo con la versione: " + module);
            ModuleCatalog.ValidateData(module, created); created["versione"] = 1; ModuleCatalog.ValidateData(module, created);
            created["versione"] = 3; bool rejected = false;
            try { ModuleCatalog.ValidateData(module, created); } catch (ArgumentException) { rejected = true; }
            Assert(rejected, "Versione del foglio non supportata respinta: " + module);
        }

        // d2, rule 1: without γb every technology uses the value of the table, with the results of the explicit value; the calculation
        // does not write the value into the sheet (old sheets and new sheets alike).
        foreach (var (type, driven) in Technologies)
        foreach (bool marked in new[] { true, false })
        {
            string name = type + (driven is null ? "" : " " + driven) + (marked ? ", foglio versione 2" : ", foglio precedente");
            var missing = Pile(type, driven, marked); missing["generali"]!.AsObject().Remove("sicurezza_base");
            double table = Calcolo.SicurezzaBaseNormativa(missing["generali"]);
            var explicitTable = Pile(type, driven, true); explicitTable["generali"]!["sicurezza_base"] = table.ToString("0.00", CultureInfo.InvariantCulture);
            Assert(Calcolo.SicurezzaBase(missing) == table && Calcolo.SicurezzaBaseTesto(missing) == table.ToString("0.00", CultureInfo.InvariantCulture), "γb senza chiave: " + name);
            string before = missing.ToJsonString();
            var withoutKey = Calcolo.Calcola(missing); var expected = Calcolo.Calcola(explicitTable);
            Assert(withoutKey.S("errore") == "" && JsonNode.DeepEquals(withoutKey, expected) && !Declared(withoutKey), "Risultati senza γb uguali a quelli con il valore della tabella: " + name);
            Assert(JsonNode.DeepEquals(CalculationService.Calculate("geo_palo_verticale", missing), expected), "Servizio di calcolo senza γb: " + name);
            Assert(missing.ToJsonString() == before, "Il calcolo materializza γb: " + name);
            Assert(CalculationCoefficients.Read("geo_palo_verticale", missing).Single(c => c.Key == "Geotecnica · sicurezza_base").Value!.ToString() == table.ToString("0.00", CultureInfo.InvariantCulture), "Coefficiente dei progetti senza γb: " + name);
        }

        // d2, rule 2: a sheet saved before version 2 with γb 1,35 (number or text, point or comma) or without γb takes the value of the table
        // if the pile is driven or a continuous flight auger; bored piles and micropiles keep their data. The calculation of the old sheet
        // and that of the migrated sheet coincide; the migration runs once.
        foreach (var (type, driven) in Technologies)
        foreach (JsonNode? old in new JsonNode?[] { JsonValue.Create(1.35), JsonValue.Create("1.35"), JsonValue.Create("1,35"), null })
        {
            string name = type + (driven is null ? "" : " " + driven) + " con γb " + (old?.ToJsonString() ?? "assente");
            var sheet = Pile(type, driven, false); var general = sheet["generali"]!.AsObject();
            if (old is null) general.Remove("sicurezza_base"); else general["sicurezza_base"] = old.DeepClone();
            double table = Calcolo.SicurezzaBaseNormativa(general); bool changes = table != 1.35;
            var beforeResult = Calcolo.Calcola(sheet); string untouched = sheet.ToJsonString();
            Assert(Calcolo.AggiornaFoglio(sheet) && sheet.D("versione") == Calcolo.VersioneFoglio, "Migrazione con la versione: " + name);
            if (changes) Assert(general.S("sicurezza_base") == table.ToString("0.00", CultureInfo.InvariantCulture), "Migrazione al γb della tabella: " + name);
            else Assert(old is null ? !general.ContainsKey("sicurezza_base") : JsonNode.DeepEquals(general["sicurezza_base"], old), "Trivellato invariato: " + name);
            var migrated = sheet.ToJsonString();
            Assert(!Calcolo.AggiornaFoglio(sheet) && sheet.ToJsonString() == migrated, "Migrazione idempotente: " + name);
            Assert(JsonNode.DeepEquals(beforeResult, Calcolo.Calcola(sheet)) && Calcolo.SicurezzaBase(sheet) == table, "Stessi risultati prima e dopo la migrazione: " + name);
            Assert(!Declared(beforeResult), "Foglio precedente con 1,35: nessun avviso dopo d2: " + name);
            Assert(untouched != migrated, "La migrazione scrive almeno la versione: " + name);
        }
        // Other values of the old sheets are choices and stay; so does every value of a sheet already at version 2.
        foreach (var (marked, value) in new[] { (false, "1.20"), (false, "1.15"), (true, "1.35"), (true, "1.20") })
        {
            var sheet = Pile("Battuto", "Profilato d'acciaio", marked); sheet["generali"]!["sicurezza_base"] = value; string text = sheet.ToJsonString();
            bool changed = Calcolo.AggiornaFoglio(sheet);
            Assert(sheet["generali"].S("sicurezza_base") == value && changed == !marked && (marked ? sheet.ToJsonString() == text : sheet.D("versione") == Calcolo.VersioneFoglio), $"γb {value} conservato (versione {(marked ? 2 : 1)})");
            Assert(Calcolo.SicurezzaBase(sheet) == double.Parse(value, CultureInfo.InvariantCulture), $"γb effettivo {value}");
        }
        // A 1,35 chosen after the migration on a driven pile stays 1,35, is used by the calculation and is declared.
        var chosen = Pile("Battuto", "Calcestruzzo prefabbricato", false); chosen["generali"]!.AsObject().Remove("sicurezza_base");
        Assert(Calcolo.AggiornaFoglio(chosen) && chosen["generali"].S("sicurezza_base") == "1.15", "Migrazione del battuto senza γb");
        chosen["generali"]!["sicurezza_base"] = "1.35";
        Assert(!Calcolo.AggiornaFoglio(chosen) && chosen["generali"].S("sicurezza_base") == "1.35" && Calcolo.SicurezzaBase(chosen) == 1.35, "1,35 scelto dopo la migrazione conservato");
        var chosenResult = Calcolo.Calcola(chosen);
        Assert(chosenResult.S("errore") == "" && Declared(chosenResult) && chosenResult["curve"]!.ToJsonString() != Calcolo.Calcola(Pile("Battuto", "Calcestruzzo prefabbricato", false))["curve"]!.ToJsonString(), "1,35 scelto: usato e dichiarato");

        // Micropiles: the value of the bored piles, no migration of γb, the point resistance with and without γb coincide.
        var micro = Micropile(false); micro["generali"]!.AsObject().Remove("sicurezza_base"); string microText = micro.ToJsonString();
        var microWithout = Calcolo.Calcola(micro, true); var microExplicit = Micropile(true); microExplicit["generali"]!["sicurezza_base"] = "1.35";
        Assert(microWithout.S("errore") == "" && JsonNode.DeepEquals(microWithout, Calcolo.Calcola(microExplicit, true)) && micro.ToJsonString() == microText, "Micropalo senza γb come 1,35");
        Assert(Calcolo.AggiornaFoglio(micro) && !micro["generali"]!.AsObject().ContainsKey("sicurezza_base") && micro.D("versione") == Calcolo.VersioneFoglio, "Micropalo: solo la versione");
        var microOld = Micropile(false); Assert(Calcolo.AggiornaFoglio(microOld) && microOld["generali"].S("sicurezza_base") == "1.35", "Micropalo con 1,35 invariato");
        // A micropile is recognised by its module, not by metodo_micropalo: an old FHWA sheet (no metodo_micropalo) with a stray tipo_palo
        // of a driven or CFA pile keeps 1,35 in the editor, the projects and the report.
        foreach (string type in new[] { "Battuto", "Elica continua" })
        {
            var fhwa = Micropile(false); var fg = fhwa["generali"]!.AsObject(); fg.Remove("metodo_micropalo"); fg["tipo_palo"] = type; fg["sicurezza_base"] = "1.35";
            Assert(Calcolo.SicurezzaBaseNormativa(fg, micropalo: true) == 1.35 && Calcolo.SicurezzaBase(fhwa, micropalo: true) == 1.35, "Micropalo senza metodo: γb dei trivellati, " + type);
            Assert(Calcolo.AggiornaFoglio(fhwa, micropalo: true) && fg.S("sicurezza_base") == "1.35" && fhwa.D("versione") == Calcolo.VersioneFoglio, "Micropalo senza metodo non migrato: " + type);
            var missingFhwa = Micropile(false); var mg = missingFhwa["generali"]!.AsObject(); mg.Remove("metodo_micropalo"); mg.Remove("sicurezza_base"); mg["tipo_palo"] = type;
            Assert(Calcolo.AggiornaFoglio(missingFhwa, micropalo: true) && !mg.ContainsKey("sicurezza_base") && Calcolo.SicurezzaBaseTesto(missingFhwa, micropalo: true) == "1.35", "Micropalo senza metodo e senza γb: " + type);
            var fhwaSheet = Micropile(false); fhwaSheet["generali"]!.AsObject().Remove("metodo_micropalo"); fhwaSheet["generali"]!["tipo_palo"] = type;
            Assert(CalculationCoefficients.Read("geo_micropalo_verticale", fhwaSheet).Single(c => c.Key == "Geotecnica · sicurezza_base").Value!.ToString() == "1.35", "Coefficiente dei progetti del micropalo senza metodo: " + type);
            var microSection = J.Obj(("id", "m"), ("nome", "Sezione"), ("fogli", new JsonArray(
                J.Obj(("id", "a"), ("nome", "Fonte"), ("modulo_id", "geo_micropalo_verticale"), ("dati", Micropile(true))),
                J.Obj(("id", "b"), ("nome", "Micropalo FHWA"), ("modulo_id", "geo_micropalo_verticale"), ("dati", fhwaSheet)))));
            var microSource = microSection.Array("fogli")[0]!.AsObject(); var microTarget = microSection.Array("fogli")[1]!.AsObject();
            microSource["dati"]!["generali"]!["peso_palo_favorevole"] = "0.9";
            Assert(ProjectSharedData.Apply(microSource, microSection, ["Coefficienti"], microTarget, ["Geotecnica · peso_palo_favorevole"]) == 1, "Progetto: coefficiente condiviso nel micropalo senza metodo");
            Assert(microTarget["dati"].D("versione") == Calcolo.VersioneFoglio && microTarget["dati"]!["generali"].S("sicurezza_base") == "1.35", "Progetto: micropalo senza metodo non migrato, " + type);
        }

        // Report: the coefficient table and the general data show the effective γb of an old driven sheet (1,35 or without γb), the
        // explicit 1,35 of a sheet at version 2; the report does not write into the sheet.
        foreach (var (marked, value, expected) in new[] { (false, "1.35", "1,15"), (false, (string?)null, "1,15"), (true, "1.35", "1,35") })
        {
            var sheet = Pile("Battuto", "Tubo d'acciaio chiuso", marked);
            if (value is null) sheet["generali"]!.AsObject().Remove("sicurezza_base"); else sheet["generali"]!["sicurezza_base"] = value;
            string text = sheet.ToJsonString(); var result = Calcolo.Calcola(sheet);
            var rows = ReportRows(ReportWord.Create("Prova γb", "geo_palo_verticale", sheet, result));
            Assert(rows.Contains("γb|" + expected), $"Relazione: γb {expected} nei coefficienti applicati");
            Assert(rows.Contains("sicurezza_base|" + expected), $"Relazione: γb {expected} nei dati generali");
            Assert(sheet.ToJsonString() == text, "La relazione scrive nel foglio");
        }

        // Projects: γb of an old driven sheet is compared with its effective value; a 1,35 chosen on another driven sheet and shared stays
        // 1,35 after the migration of the target.
        var drivenSource = Pile("Battuto", "Tubo d'acciaio chiuso"); drivenSource["generali"]!["sicurezza_base"] = "1.35";
        var section = J.Obj(("id", "s"), ("nome", "Sezione"), ("fogli", new JsonArray(
            J.Obj(("id", "a"), ("nome", "Battuto 1,35"), ("modulo_id", "geo_palo_verticale"), ("dati", drivenSource)),
            J.Obj(("id", "b"), ("nome", "Battuto"), ("modulo_id", "geo_palo_verticale"), ("dati", Pile("Battuto", "Profilato d'acciaio", false))))));
        var source = section.Array("fogli")[0]!.AsObject(); var target = section.Array("fogli")[1]!.AsObject(); // J.Obj stores copies
        Assert(ProjectSharedData.Fields(target)["Geotecnica · sicurezza_base"].Value!.ToString() == "1.15", "Progetto: γb effettivo del foglio precedente");
        Assert(ProjectSharedData.Differences(section).Any(d => d.Key == "Geotecnica · sicurezza_base"), "Progetto: γb confrontato tra pali battuti");
        Assert(ProjectSharedData.Apply(source, section, ["Coefficienti"], target, ["Geotecnica · sicurezza_base"]) == 1, "Progetto: γb condiviso");
        Assert(target["dati"].D("versione") == Calcolo.VersioneFoglio && target["dati"]!["generali"].S("sicurezza_base") == "1.35" && Calcolo.SicurezzaBase(target["dati"]) == 1.35, "Progetto: 1,35 ricevuto conservato");

        // Projects: γb is compared and shared only between sheets with the same execution of Tab. 6.4.II (the micropile as a bored pile);
        // a bored pile does not give its γb to a driven or CFA pile, and the other coefficients are still shared.
        foreach (var (first, second, same) in new[] {
            (("geo_palo_verticale", "Trivellato", (string?)null), ("geo_palo_verticale", "Battuto", (string?)"Profilato d'acciaio"), false),
            (("geo_palo_verticale", "Trivellato", null), ("geo_palo_verticale", "Elica continua", null), false),
            (("geo_palo_verticale", "Elica continua", null), ("geo_palo_verticale", "Battuto", "Calcestruzzo prefabbricato"), false),
            (("geo_palo_verticale", "Battuto", "Calcestruzzo gettato in opera"), ("geo_palo_verticale", "Battuto", "Profilato d'acciaio"), true),
            (("geo_micropalo_verticale", "", null), ("geo_palo_verticale", "Trivellato", null), true),
            (("geo_micropalo_verticale", "", null), ("geo_palo_verticale", "Battuto", "Profilato d'acciaio"), false) })
        {
            JsonObject Sheet((string Module, string Type, string? Driven) s, string id)
            {
                var d = s.Module == "geo_micropalo_verticale" ? Micropile(false) : Pile(s.Type, s.Driven, false);
                d["generali"]!["sicurezza_base"] = "1.20"; d["generali"]!["peso_palo_favorevole"] = id == "a" ? "0.9" : "1";
                return J.Obj(("id", id), ("nome", id + " " + s.Type), ("modulo_id", s.Module), ("dati", d));
            }
            var mixed = J.Obj(("id", "x"), ("nome", "Sezione"), ("fogli", new JsonArray(Sheet(first, "a"), Sheet(second, "b"))));
            var a = mixed.Array("fogli")[0]!.AsObject(); var b = mixed.Array("fogli")[1]!.AsObject(); b["dati"]!["generali"]!["sicurezza_base"] = "1.30";
            string name = $"{first.Item2}{first.Item3} / {second.Item2}{second.Item3}";
            var differences = ProjectSharedData.Differences(mixed);
            Assert(differences.Any(d => d.Key == "Geotecnica · sicurezza_base") == same, "Progetto: γb confrontato solo con la stessa esecuzione, " + name);
            Assert(differences.Any(d => d.Key == "Geotecnica · peso_palo_favorevole"), "Progetto: altri coefficienti confrontati, " + name);
            Assert(ProjectSharedData.Apply(a, mixed, ["Coefficienti"], b, ["Geotecnica · sicurezza_base"]) == (same ? 1 : 0) &&
                b["dati"]!["generali"].S("sicurezza_base") == (same ? "1.20" : "1.30"), "Progetto: γb condiviso solo con la stessa esecuzione, " + name);
            Assert(ProjectSharedData.Apply(a, mixed, ["Coefficienti"], b, ["Geotecnica · peso_palo_favorevole"]) == 1 && b["dati"]!["generali"].S("peso_palo_favorevole") == "0.9", "Progetto: altri coefficienti condivisi, " + name);
        }
        Console.WriteLine($"Coefficienti dei pali: {passed} controlli superati.");
    }

    /// <summary>A calculable vertical pile: new sheet (version 2) or sheet saved before d2 (no version), γb 1,35 as the old default.</summary>
    static JsonObject Pile(string type, string? driven, bool marked = true)
    {
        var data = Archivio.NuovoFoglio("geo_palo_verticale"); var g = data["generali"]!;
        g["tipo_palo"] = type; if (driven is not null) g["sottotipo_palo_battuto"] = driven;
        g["lunghezza"] = "20"; g["azione_compressione"] = "1000"; g["azione_trazione"] = "200";
        var layer = data.Array("stratigrafie")[0]!.AsArray()[0]!.AsObject();
        layer["spessore"] = "25"; layer["tipologia"] = "Granulare"; layer["addensamento"] = "Denso"; layer["angolo_attrito"] = "32"; layer["peso_specifico"] = "19";
        if (!marked) data.Remove("versione");
        return data;
    }

    /// <summary>A calculable micropile with the point contribution (where γb applies), Bustamante–Doix IGU.</summary>
    static JsonObject Micropile(bool marked)
    {
        var data = Archivio.NuovoFoglio("geo_micropalo_verticale"); var g = data["generali"]!;
        string soil = BustamanteDoix.Terreni.Keys.First();
        g["lunghezza"] = "15"; g["pressione_iniezione"] = "1"; g["profilo_chs"] = Chs.Catalogo.Keys.First(); g["considera_punta"] = true; g["percentuale_punta"] = "10";
        g["azione_compressione"] = "300"; g["azione_trazione"] = "100";
        var layer = data.Array("stratigrafie")[0]!.AsArray()[0]!.AsObject();
        layer["spessore"] = "20"; layer["terreno"] = soil; layer["alpha"] = BustamanteDoix.IntervalloAlpha(soil, "IGU")[0].ToString(CultureInfo.InvariantCulture);
        if (!marked) data.Remove("versione");
        return data;
    }

    /// <summary>The two-cell rows of the tables of a Word report, as "parameter|value".</summary>
    static HashSet<string> ReportRows(byte[] docx)
    {
        using var zip = new ZipArchive(new MemoryStream(docx)); using var stream = zip.GetEntry("word/document.xml")!.Open();
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return XDocument.Load(stream).Descendants(w + "tr").Select(r => r.Elements(w + "tc").Select(c => string.Concat(c.Descendants(w + "t").Select(t => t.Value))).ToArray())
            .Where(c => c.Length == 2).Select(c => c[0] + "|" + c[1]).ToHashSet();
    }
}
