using System.Text;
using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Anthea.Testing.Capture;

/// <summary>Creates frozen input files, never expected results. Run explicitly once before moving production code.</summary>
public static class F3Fixtures
{
    public static void Write(string root, string output)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new IOException("La cartella delle fixture deve essere vuota.");
        Directory.CreateDirectory(output);
        var corpus = Corpus.Build(root);
        var cases = corpus.Where(c => c.Data is not null && (c.Id.StartsWith("default") || c.Id.StartsWith("elastic") || c.Id.StartsWith("example-"))).ToList();
        foreach (string module in new[] { "geo_palo_verticale", "geo_micropalo_verticale", "str_palo" })
        {
            var sample = corpus.FirstOrDefault(c => c.Module == module && c.Data is not null &&
                (c.Id.StartsWith("regressione-") || c.Id.StartsWith("corpus-")));
            if (sample is not null) cases.Add(sample);
        }
        void Legacy(string module, string id, Action<JsonObject> alter)
        {
            var data = ModuleCatalog.CreateData(module); alter(data);
            cases.Add(new(module, id, "Fixture sintetica di migrazione F3", null, data));
        }
        Legacy("geo_palo_verticale", "legacy-nq-gamma", d => {
            d["versione"] = 1; var g = d["generali"]!.AsObject();
            g["metodo_nq"] = "Raffinata"; g.Remove("sicurezza_base"); d.Remove("efficienza");
        });
        Legacy("geo_micropalo_verticale", "legacy-peso-lineare", d => {
            d["generali"]!["peso_lineare_micropalo"] = "12.34"; d.Remove("efficienza");
        });
        Legacy("mat_calcestruzzo", "legacy-esposizioni", d => {
            d["classe"] = "C35/45"; d["esposizioni"] = new JsonArray("XC3", "XD1");
        });
        Legacy("str_palo", "legacy-workspace-segni", d => {
            // ANTHEA has always written the standard into the workspace (decision of 14/10/2026, F4-31)
            d["workspace_ca"] = new JsonObject { ["versione"] = 1, ["normativa"] = "NTC 2018" };
            d["combinazioni"] = new JsonObject { ["SLU"] = new JsonArray(new JsonObject {
                ["nome"] = "compressione storica", ["azioni"] = new JsonArray("-350", "45", "-12") }) };
        });
        Legacy(RetainingWall.Module, "legacy-global-defaults", d => {
            foreach (string key in new[] { "valley_layers", "soil_mode", "soil_split_x", "search_mode" })
                d["global_stability"]?.AsObject().Remove(key);
        });
        Legacy(HorizontalPileGroup.Module, "legacy-pile-group", d => d["versione"] = 1);
        foreach (string module in new[] { PaloOrizzontale.Module, MicropaloOrizzontale.Module })
            Legacy(module, "legacy-elastic-pending", d => {
                d["elastico"] = new JsonObject { ["EI"] = "25000", ["strati"] = new JsonArray() };
                d["vista_orizzontale"] = "elastico";
            });
        Legacy(BridgeSection.Module, "legacy-web-and-accessories", d => {
            d.Remove("h_trave"); d["h_web"] = "1800";
            foreach (string key in BridgeSection.AccessoryDefaults().Select(p => p.Key).Concat(new[] { "predalle", "h_predalle", "rif_ferri_inf" })) d.Remove(key);
        });
        Legacy(BridgeConcept.Module, "legacy-advanced-defaults", d => {
            foreach (string key in new[] { "arch_rise", "tower_height", "cable_sag", "suspender_spacing", "truss_ratio" }) d["input"]!.AsObject().Remove(key);
            foreach (string key in new[] { "concept_tension", "concept_compression", "co2_cable" }) d["assumptions"]!.AsObject().Remove(key);
        });
        Legacy(Anthea.FoundationWorkspace.FoundationDocument.Module, "legacy-soil-profile", d => {
            d.Remove("stratigrafie"); d.Remove("generali"); d.Remove("sezione");
            d["versione_fondazioni"] = 1; d["input"]!["CoverUnitWeight"] = 17; d["azioni_confermate"] = true;
        });
        Legacy(Anthea.FoundationWorkspace.FoundationDocument.Module, "confirmed-actions", d => d["azioni_confermate"] = true);
        foreach (string module in new[] { PaloOrizzontale.Module, MicropaloOrizzontale.Module })
            Legacy(module, "single-soil-layer", d => d["stratigrafie"]![0]!.AsArray().Add(PaloOrizzontale.Layer()));
        foreach (string module in new[] { PaloOrizzontale.Module, MicropaloOrizzontale.Module })
            Legacy(module, "single-soil-elastic", d => {
                d["stratigrafie"]![0]!.AsArray().Add(PaloOrizzontale.Layer());
                ElasticHorizontalPile.PrepareShared(d); d["vista_orizzontale"] = "elastico";
            });
        Legacy("str_palo", "legacy-calculation-settings", d => {
            foreach (string key in new[] { "staffe_presenti", "coefficienti_unitari", "cls_diagramma" }) d["input"]!.AsObject().Remove(key);
            d["workspace_ca"] = new JsonObject { ["versione"] = 2, ["normativa"] = "NTC 2018", ["coefficienti"] = new JsonObject() };
        });
        PileGroupF54(cases);
        MaterialsF55(cases);
        foreach (var c in cases.DistinctBy(c => c.Key))
        {
            var item = new JsonObject { ["module"] = c.Module, ["id"] = c.Id,
                ["origin"] = c.Origin, ["source"] = c.SourceFile, ["data"] = c.Data!.DeepClone() };
            File.WriteAllText(Path.Combine(output, c.Module + "--" + c.Id + ".json"), item.ToJsonString(J.Options), new UTF8Encoding(false));
        }
        Console.WriteLine($"Fixture F3: {Directory.GetFiles(output, "*.json").Length}, moduli: {cases.Select(c => c.Module).Distinct().Count()}.");
    }

    /// <summary>
    /// Pile group fixtures added before the F5.4 pilot (proposal F5, section 7.2: capture first, with the historic view): every
    /// layout, every load direction, the two cap shapes and a cap without outline, optional keys absent, extrapolations, projected
    /// rows with representative spacings per direction, warnings, and a calculation that stops with missing data. The documents
    /// are archive-valid (DocumentService.Open); the identifiers never end in "default", which the F3 project report collects.
    /// </summary>
    static void PileGroupF54(List<CaptureCase> cases)
    {
        void Added(string id, Action<JsonObject> alter)
        {
            var data = ModuleCatalog.CreateData(HorizontalPileGroup.Module); alter(data);
            cases.Add(new(HorizontalPileGroup.Module, id, "Fixture sintetica F5.4 (proposta F5 7.2, cattura prima con la vista storica)", null, data));
        }
        static JsonObject Generator(JsonObject d) => d["generatore"]!.AsObject();
        static JsonObject Directions(JsonObject d) => d["direzioni"]!.AsObject();
        // Staggered layout, rotated; all the load directions except +X; rectangular cap; Caltrans selected in −Y.
        Added("quinconce-direzioni", d => {
            d["diametro"] = 0.8;
            var g = Generator(d); g["tipo"] = "Quinconce"; g["nx"] = 4; g["ny"] = 3; g["sx_d"] = 3.5; g["sy_d"] = 3; g["rotazione"] = 15;
            var h = Directions(d); h["X"] = false; h["Y"] = true; h["X-"] = true; h["Y-"] = true; h["custom"] = true; h["angolo_custom"] = 30;
            d["basamento"] = J.Obj(("tipo", "Rettangolare"), ("bordo_d", 1.25));
            d["metodo"] = 5; d["direzione_selezionata"] = "Y-"; d["angolo"] = 270;
            HorizontalPileGroup.Regenerate(d);
        });
        // Triangular layout below 3D, projected rows with both spacings of +X and extrapolations allowed: values available with
        // warnings of explicit extrapolation and of the geometric extension; Rollins selected in +X.
        Added("triangolare-avvisi", d => {
            var g = Generator(d); g["tipo"] = "Triangolare"; g["lato"] = 4; g["sx_d"] = 2.5;
            var h = Directions(d); h["Y"] = false;
            d["estrapolazioni"] = true; d["file_proiettate"] = true;
            d["interassi"]!["X"] = J.Obj(("interasse_parallelo", 2.2), ("interasse_trasversale", 2.5));
            d["basamento"]!["bordo_d"] = 0.75; d["metodo"] = 3;
            HorizontalPileGroup.Regenerate(d);
        });
        // Hexagonal layout with projected rows and representative spacings per direction: both in +X, only S∥ in +Y (S⊥ key
        // removed, as the view removes an emptied optional field); AASHTO selected in the custom direction.
        Added("esagonale-interassi", d => {
            var g = Generator(d); g["tipo"] = "Esagonale"; g["anelli"] = 2; g["suddivisioni"] = 1; g["centro"] = true;
            var h = Directions(d); h["custom"] = true; h["angolo_custom"] = 60;
            d["file_proiettate"] = true;
            d["interassi"]!["X"] = J.Obj(("interasse_parallelo", 2.6), ("interasse_trasversale", 3));
            d["interassi"]!["Y"] = J.Obj(("interasse_parallelo", 3.2));
            d["metodo"] = 1; d["direzione_selezionata"] = "custom"; d["angolo"] = 60;
            HorizontalPileGroup.Regenerate(d);
        });
        // Pentagonal layout of an archive without the negative directions (keys and spacings); cap distance below 0.5D: no
        // outline, error of the cap. The spacings of +X stay: the view creates the object of the shown direction at opening,
        // the preparation of the project report does not (different prepared data, ui/f3-report-chapters).
        Added("pentagonale-senza-basamento", d => {
            d["diametro"] = 1.2;
            var g = Generator(d); g["tipo"] = "Pentagonale"; g["anelli"] = 1; g["suddivisioni"] = 2; g["centro"] = false;
            var h = Directions(d); h.Remove("X-"); h.Remove("Y-");
            foreach (string key in new[] { "X-", "Y-" }) d["interassi"]!.AsObject().Remove(key);
            d["basamento"]!["bordo_d"] = 0.4; d["direzione_selezionata"] = "Y"; d["angolo"] = 90;
            HorizontalPileGroup.Regenerate(d);
        });
        // Generic coordinates typed as texts with a decimal comma (normalized to numbers when the view captures the table),
        // rectangular cap; Davisson selected in the custom direction (not applicable to the irregular rows).
        Added("generica-coordinate", d => {
            Generator(d)["tipo"] = "Generica";
            var h = Directions(d); h["Y"] = false; h["custom"] = true; h["angolo_custom"] = 135;
            d["basamento"]!["tipo"] = "Rettangolare";
            d["pali"] = new JsonArray(
                J.Obj(("id", "A"), ("x", "0"), ("y", "0")), J.Obj(("id", "B"), ("x", "3,5"), ("y", "0")),
                J.Obj(("id", "C"), ("x", "7"), ("y", "0,5")), J.Obj(("id", "D"), ("x", "1,75"), ("y", "3,2")),
                J.Obj(("id", "E"), ("x", 5.25), ("y", 3.2)));
            d["metodo"] = 0; d["direzione_selezionata"] = "custom"; d["angolo"] = 135;
        });
        // Generic coordinates with two overlapping piles: the calculation stops with missing data, no result and no report.
        Added("generica-sovrapposti", d => {
            Generator(d)["tipo"] = "Generica";
            d["pali"] = new JsonArray(J.Obj(("id", "P1"), ("x", 0), ("y", 0)), J.Obj(("id", "P2"), ("x", 0.6), ("y", 0)), J.Obj(("id", "P3"), ("x", 3), ("y", 0)));
        });
    }

    /// <summary>
    /// Material fixtures added before F5.5 (proposal F5, section 7.2: capture first, with the historic views MaterialView and
    /// RebarMaterialView). Concrete: both cover methods (EC2 with the reductions of the structural class, NTC with plate and quality
    /// control), every deviation control, ground and abrasion, 100 years, the bond conditions, frost with entrained air, sea water,
    /// the classes outside the library catalogue, X0, invalid typed numbers, an archive with three exposures, a main exposure that
    /// is not the first and a key the sheet does not know. Reinforcing steel: B450A, historic steels with and without εu, a custom
    /// hardening steel with a reference, a sheet saved with the previous catalogue (DET-3) and fu lower than fyk. The values are
    /// texts as the sheets write them; the documents are archive-valid; the identifiers never end in "default".
    /// </summary>
    static void MaterialsF55(List<CaptureCase> cases)
    {
        const string origin = "Fixture sintetica F5.5 (proposta F5 7.2, cattura prima con la vista storica)";
        void Concrete(string id, Action<JsonObject> alter)
        {
            var data = ModuleCatalog.CreateData("mat_calcestruzzo"); alter(data);
            cases.Add(new("mat_calcestruzzo", id, origin, null, data));
        }
        static void Values(JsonObject d, string group, params (string Key, object Value)[] values)
        {
            foreach (var (key, value) in values) d[group]![key] = JsonValue.Create(value);
        }
        static void Exposure(JsonObject d, string code) { d["esposizione_principale"] = code; d["esposizioni"] = new JsonArray(code); }
        // EC2 2004 with the three reductions of the structural class, 100 years, measured cover (5 mm), prepared ground, XM2,
        // rough surface, Dmax 40 mm (+5 mm), good bond, S4 and a named CEM III 42,5 R; XD3, C45/55.
        Concrete("ec2-riduzioni-tolleranza", d => {
            d["classe"] = "C45/55"; Exposure(d, "XD3");
            Values(d, "numeri", ("diameter", "20"), ("aggregate", "40"), ("cementName", "CEM III/A 42,5 R · cementeria di prova"));
            Values(d, "scelte", ("coverMethod", "EC2 2004"), ("life", "100 anni"), ("deviationControl", "Misura copriferri"), ("deviationValue", "5 mm"),
                ("ground", "Terreno preparato"), ("abrasion", "XM2 · +10 mm"), ("consistency", "S4"), ("cement", "CEM III"), ("cementClass", "42,5"),
                ("cementEarly", "R"), ("bondCondition", "Buone · η₁ = 1,0"));
            Values(d, "opzioni", ("highStrength", true), ("slab", true), ("quality", true), ("rough", true));
        });
        // NTC 2018 + Circolare for a plate with quality control of the covers, accurate measure with rejection (0 mm), cast
        // directly on the ground, sea water XS3 with a class below the minimum, Dmax 16 mm, S2, CEM II 52,5.
        Concrete("ntc-piastra-controllo", d => {
            d["classe"] = "C25/30"; Exposure(d, "XS3");
            Values(d, "numeri", ("diameter", "12"), ("aggregate", "16"));
            Values(d, "scelte", ("ntcElement", "Piastra / soletta / parete"), ("deviationControl", "Misura accurata + scarto"), ("deviationValue", "0 mm"),
                ("ground", "Direttamente su terra"), ("consistency", "S2"), ("cement", "CEM II"), ("cementClass", "52,5"));
            Values(d, "opzioni", ("ntcQuality", true));
        });
        // Frost with de-icing salts: entrained air for Dmax 16 mm, S3, a cement to be specified; C35/45, bond coefficients typed.
        Concrete("gelo-aria-xf4", d => {
            d["classe"] = "C35/45"; Exposure(d, "XF4");
            Values(d, "numeri", ("aggregate", "16"), ("cementName", "CEM IV/B (P) 32,5 N"), ("bondAlpha", "0,85"), ("bondGamma", "1,2"));
            Values(d, "scelte", ("consistency", "S3"), ("cement", "Altro / da specificare"), ("abrasion", "XM1 · +5 mm"));
        });
        // X0 with a class added outside the library catalogue (C16/20): nothing prescribed for the mix.
        Concrete("x0-classe-fuori-catalogo", d => {
            d["classe"] = "C16/20"; Exposure(d, "X0");
            Values(d, "scelte", ("consistency", "S1"), ("bondCondition", "Buone · η₁ = 1,0"));
        });
        // Invalid typed numbers (diameter, Dmax, γc) with EC2 and XA2: bond, cover and Dmax to be completed, report with the error.
        Concrete("dati-incompleti", d => {
            Exposure(d, "XA2");
            Values(d, "numeri", ("diameter", "abc"), ("aggregate", ""), ("bondGamma", "0,5"));
            Values(d, "scelte", ("coverMethod", "EC2 2004"), ("cement", "CEM V"));
        });
        // Archive of a previous version: three exposures, the main one not the first, partial numbers, choices and options, a
        // deviation value missing for a measured control, and a key the sheet does not know (dropped when the sheet is saved).
        cases.Add(new("mat_calcestruzzo", "legacy-tre-esposizioni", origin, null, J.Obj(("versione_materiali", 1), ("classe", "C32/40"),
            ("esposizione_principale", "XD2"), ("esposizioni", new JsonArray("XF1", "XC4", "XD2")), ("numeri", J.Obj(("diameter", "25"))),
            ("scelte", J.Obj(("coverMethod", "EC2 2004"), ("deviationControl", "Misura copriferri"))), ("opzioni", J.Obj(("rough", true))),
            ("vista_precedente", "esposizioni"))));

        void Steel(string id, Action<JsonObject> alter, string reference = "")
        {
            var data = ModuleCatalog.CreateData(RebarMaterial.Module); alter(data["input"]!.AsObject()); data["riferimento"] = reference;
            cases.Add(new(RebarMaterial.Module, id, origin, null, data));
        }
        // Catalogue steel of class A, with a reference.
        Steel("b450a", input => RebarMaterial.Select(input, "B450A"), "DDT 45 del 12/10/2026");
        // Historic steel with εu defined by the user and the hardening diagram (allowed for historic steels).
        Steel("storico-feb38k-eps", input => { RebarMaterial.Select(input, "FeB38k"); input["steel_eps_u"] = "40"; input["steel_diagramma"] = "Incrudente"; });
        // Historic steel without εu: the properties stop with the message of the missing value, report with the error.
        Steel("storico-feb22k-incompleto", input => RebarMaterial.Select(input, "FeB22k"));
        // Custom hardening steel with values typed with the decimal comma and a reference of two lines.
        Steel("personalizzato-incrudente", input => {
            RebarMaterial.Select(input, "Personalizzato"); input["materiale_acciaio_nome"] = "Acciaio da prove";
            input["fyk_mpa"] = "430,5"; input["steel_fu_mpa"] = "560"; input["steel_modulus_mpa"] = "205000"; input["steel_eps_u"] = "60";
            input["steel_diagramma"] = "Incrudente"; input["gamma_s"] = "1,10";
        }, "Certificato n. 123/2026\nLaboratorio prove materiali");
        // A B450A saved with the previous catalogue (εu = 30 ‰, DET-3): not the preset any more, declared without rewriting it.
        Steel("catalogo-precedente-b450a", input => { RebarMaterial.Select(input, "B450A"); input["steel_eps_u"] = 30; });
        // Custom steel with fu lower than fyk: the properties stop with the message, report with the error.
        Steel("personalizzato-non-valido", input => {
            RebarMaterial.Select(input, "Personalizzato"); input["fyk_mpa"] = "500"; input["steel_fu_mpa"] = "450";
        });
    }
}
