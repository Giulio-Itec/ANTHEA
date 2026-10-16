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
        VerticalPileF56(cases);
        ShallowFoundationF59b(cases);
        RetainingWallF59(cases);
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

    /// <summary>
    /// Vertical pile and micropile fixtures added before the F5.6 preparation (proposal F5, section 7.2: capture first, with the
    /// historic view of SheetEditor/GeoEditor): driven, bored and continuous flight auger piles, groundwater with and without buoyancy,
    /// several surveys with granular, cohesive and excluded layers, the efficiency methods, a stratigraphy shorter than the pile,
    /// saved chart visibility, warnings (γb of the sheet, efficiency), IRS and IGU micropiles with tip, inclination and grouting start,
    /// and two calculations that stop with missing data. Documents as the sheet writes them: texts for the typed values (also with a
    /// decimal comma), JSON booleans for the options. The identifiers never end in "default", which the F3 project report collects.
    /// </summary>
    static void VerticalPileF56(List<CaptureCase> cases)
    {
        const string Pile = "geo_palo_verticale", Micro = "geo_micropalo_verticale";
        void Added(string module, string id, Action<JsonObject, JsonObject> alter)
        {
            var data = ModuleCatalog.CreateData(module); alter(data, data["generali"]!.AsObject());
            cases.Add(new(module, id, "Fixture sintetica F5.6 (proposta F5 7.2, cattura prima con la vista storica)", null, data));
        }
        static JsonObject Layer(string type, string density, string thickness, string gamma, string saturated, string phi, string cohesion = "0", string undrained = "0", string nc = "9", bool lateral = true) =>
            J.Obj(("spessore", thickness), ("tipologia", type), ("addensamento", density), ("peso_specifico", gamma), ("peso_specifico_saturo", saturated),
                ("angolo_attrito", phi), ("coesione_efficace", cohesion), ("coesione_non_drenata", undrained), ("nc", nc), ("laterale_attiva", lateral));
        static JsonObject Efficiency(string method, string nx, string ny, string sx = "", string sy = "", string etaC = "1", string etaT = "1") =>
            J.Obj(("metodo", method), ("numero_pali_x", nx), ("numero_pali_y", ny), ("interasse_x", sx), ("interasse_y", sy), ("eta_compressione", etaC), ("eta_trazione", etaT));
        static string Base(JsonObject g, bool micro) => Calcolo.SicurezzaBaseNormativa(g, micro).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        string[] soils = BustamanteDoix.Terreni.Keys.ToArray();
        string Alpha(string soil, string injection) => (injection == "IRS" ? BustamanteDoix.Terreni[soil].IRS : BustamanteDoix.Terreni[soil].IGU)[0]
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        JsonObject MicroLayer(string soil, string thickness, string injection, bool lateral = true) =>
            J.Obj(("spessore", thickness), ("terreno", soil), ("alpha", Alpha(soil, injection)), ("laterale_attiva", lateral));

        // Driven pile (precast) with groundwater and buoyancy, two surveys of granular and cohesive layers (undrained branch below the
        // water table), ξ of three investigated verticals, Converse–Labarre group; γb of the technology (driven: 1,15).
        Added(Pile, "battuto-falda-sondaggi", (d, g) => {
            g["tipo_palo"] = "Battuto"; g["sottotipo_palo_battuto"] = "Calcestruzzo prefabbricato";
            g["diametro"] = "0.6"; g["lunghezza"] = "15"; g["azione_compressione"] = "1200"; g["azione_trazione"] = "250";
            g["presenza_falda"] = true; g["profondita_falda"] = "2,5"; g["considera_sottospinta"] = true; g["verticali_indagate"] = "3";
            g["sicurezza_base"] = Base(g, false);
            d["efficienza"] = Efficiency("Converse-Labarre", "3", "2", "1.8", "2.1");
            d["stratigrafie"] = new JsonArray(
                new JsonArray(Layer("Granulare", "Sciolto", "4", "18", "20", "30"), Layer("Coesivo", "Denso", "6", "19", "20", "24", "10", "80"),
                    Layer("Granulare", "Denso", "10", "20", "22", "36")),
                new JsonArray(Layer("Granulare", "Denso", "8", "19.5", "21", "34"), Layer("Coesivo", "Sciolto", "12", "18.5", "19.5", "22", "5", "60", "7")));
        });
        // Continuous flight auger pile in cohesive soil, water table at the ground surface without buoyancy, a first layer excluded from
        // the shaft, Feld group, γb of the sheet different from the technology (warning), traction action not given.
        Added(Pile, "elica-coesivo-avvisi", (d, g) => {
            g["tipo_palo"] = "Elica continua"; g["diametro"] = "0.8"; g["lunghezza"] = "18"; g["azione_compressione"] = "900"; g["azione_trazione"] = "";
            g["presenza_falda"] = true; g["profondita_falda"] = "0"; g["considera_sottospinta"] = false; g["verticali_indagate"] = "≥10";
            g["sicurezza_base"] = "1.50"; g["sicurezza_laterale_compressione"] = "1.20";
            d["efficienza"] = Efficiency("Feld", "2", "3");
            d["stratigrafie"] = new JsonArray(new JsonArray(Layer("Granulare", "Sciolto", "3", "17", "19", "28", lateral: false),
                Layer("Coesivo", "Sciolto", "9", "18", "19", "22", "5", "45"), Layer("Coesivo", "Denso", "12", "19", "20.5", "25", "15", "120", "8")));
        });
        // Bored pile longer than the surveys (results limited to the available depth, warning of the sheet), three surveys, user
        // defined efficiency and the chart visibility saved in the sheet (design curves of traction hidden, mean compression shown).
        Added(Pile, "copertura-insufficiente", (d, g) => {
            g["diametro"] = "1.2"; g["lunghezza"] = "22"; g["azione_compressione"] = "2500"; g["azione_trazione"] = "600"; g["verticali_indagate"] = "2";
            d["efficienza"] = Efficiency("Definita dall'utente", "1", "1", etaC: "0.85", etaT: "0,9");
            d["stratigrafie"] = new JsonArray(
                new JsonArray(Layer("Granulare", "Denso", "8", "19", "21", "33"), Layer("Granulare", "Denso", "6", "20", "22", "37")),
                new JsonArray(Layer("Granulare", "Sciolto", "5", "18", "20", "30"), Layer("Granulare", "Denso", "11", "20", "21", "35")),
                new JsonArray(Layer("Granulare", "Denso", "16", "19.5", "21.5", "34")));
            d["visibilita_grafici"] = J.Obj(("progetto_drenante_trazione", false), ("progetto_non_drenante_trazione", false), ("media_drenante_compressione", true));
        });
        // Complete geometry with a cohesive layer below the water table without Cu: the calculation stops with «Dati da completare».
        Added(Pile, "dati-incompleti", (d, g) => {
            g["diametro"] = "0.8"; g["lunghezza"] = "12"; g["azione_compressione"] = "700";
            g["presenza_falda"] = true; g["profondita_falda"] = "3";
            d["stratigrafie"] = new JsonArray(new JsonArray(Layer("Granulare", "Denso", "4", "19", "21", "32"),
                J.Obj(("spessore", "10"), ("tipologia", "Coesivo"), ("addensamento", "Denso"), ("peso_specifico", "19"), ("peso_specifico_saturo", "20"),
                    ("angolo_attrito", "24"), ("coesione_efficace", "10"), ("coesione_non_drenata", ""), ("nc", "9"), ("laterale_attiva", true))));
        });
        // IRS micropile with tip (10%), inclined 15°, grouted from 2 m along the axis, CHS tube, two surveys (one layer excluded),
        // Converse–Labarre group; γb of the technology.
        Added(Micro, "irs-punta-inclinato", (d, g) => {
            g["tipo_iniezione"] = "IRS"; g["diametro"] = "0.3"; g["lunghezza"] = "14"; g["profilo_chs"] = "CHS 139.7 × 8"; g["pressione_iniezione"] = "2.5";
            g["inclinazione"] = "15"; g["inizio_aderenza"] = "2"; g["considera_punta"] = true; g["percentuale_punta"] = "10";
            g["azione_compressione"] = "450"; g["azione_trazione"] = "200"; g["verticali_indagate"] = "2";
            g["sicurezza_base"] = Base(g, true);
            d["efficienza"] = Efficiency("Converse-Labarre", "2", "2", "1", "1,2");
            d["stratigrafie"] = new JsonArray(
                new JsonArray(MicroLayer(soils[0], "3", "IRS", lateral: false), MicroLayer(soils[2], "6", "IRS"), MicroLayer(soils[^1], "8", "IRS")),
                new JsonArray(MicroLayer(soils[1], "5", "IRS"), MicroLayer(soils[3], "12", "IRS")));
        });
        // IGU micropile without tip, surveys shorter than the micropile (results limited to the available length), Feld group, three
        // surveys and the chart visibility saved in the sheet.
        Added(Micro, "igu-copertura-avvisi", (d, g) => {
            g["tipo_iniezione"] = "IGU"; g["diametro"] = "0.22"; g["lunghezza"] = "16"; g["profilo_chs"] = "CHS 114.3 × 8"; g["pressione_iniezione"] = "3";
            g["azione_compressione"] = "380"; g["azione_trazione"] = "";
            d["efficienza"] = Efficiency("Feld", "3", "1");
            d["stratigrafie"] = new JsonArray(
                new JsonArray(MicroLayer(soils[1], "4", "IGU"), MicroLayer(soils[2], "6", "IGU")),
                new JsonArray(MicroLayer(soils[0], "12", "IGU")),
                new JsonArray(MicroLayer(soils[3], "5", "IGU"), MicroLayer(soils[^1], "5", "IGU")));
            d["visibilita_grafici"] = J.Obj(("progetto_trazione", false), ("minima_compressione", true));
        });
        // Tip contribution above 15% of the shaft: the calculation stops with «Dati da completare».
        Added(Micro, "dati-incompleti", (d, g) => {
            g["diametro"] = "0.25"; g["lunghezza"] = "10"; g["profilo_chs"] = "CHS 114.3 × 6.3"; g["pressione_iniezione"] = "2";
            g["considera_punta"] = true; g["percentuale_punta"] = "20"; g["azione_compressione"] = "300";
            d["stratigrafie"] = new JsonArray(new JsonArray(MicroLayer(soils[2], "12", "IGU")));
        });
    }
    /// <summary>
    /// Shallow foundation fixtures added before the F5.9b preparation (proposal F5, section 7.2: capture first, with the historic
    /// view FoundationWorkspace): every method (EC7, Terzaghi, Meyerhof, Brinch Hansen, Vesić), the three shapes, the drained and
    /// undrained conditions, the characteristic comparison without the A1 confirmation, the two reductions for compressibility,
    /// groundwater above and below the base, eccentric actions and partial contact, actions beyond the resistant domain (views not
    /// available), two surveys with the second one selected, a shared concrete of the catalogue and a custom one, the inputs collapsed
    /// in the sheet (the visit opens the groups again), a version 1 archive with groundwater and the base at the ground surface, and
    /// two calculations that stop: an incomplete layer and the half-space not assumed. Documents as the sheet writes them: texts for
    /// the typed values (also with a decimal comma), indices for the choices, JSON booleans for the options. The identifiers never end
    /// in "default", which the F3 project report collects.
    /// </summary>
    static void ShallowFoundationF59b(List<CaptureCase> cases)
    {
        const string Module = Anthea.FoundationWorkspace.FoundationDocument.Module;
        void Added(string id, Action<JsonObject, JsonObject, JsonArray> alter)
        {
            var data = ModuleCatalog.CreateData(Module); alter(data, data["input"]!.AsObject(), data["stratigrafie"]![0]!.AsArray());
            cases.Add(new(Module, id, "Fixture sintetica F5.9b (proposta F5 7.2, cattura prima con la vista storica)", null, data));
        }
        // The keys of FoundationMaterials.NewLayer, in its order; the typed values are texts, as the table of the sheet writes them.
        static JsonObject Layer(string name, string thickness, string type, string gamma, string saturated, string phi, string cohesion, string undrained) =>
            J.Obj(("name", name), ("spessore", thickness), ("tipologia", type), ("peso_specifico", gamma), ("peso_specifico_saturo", saturated),
                ("angolo_attrito", phi), ("coesione_efficace", cohesion), ("coesione_non_drenata", undrained));

        // Terzaghi on a strip (actions per metre), local shear reduction, groundwater below the base, rough base (the only one of
        // Terzaghi in the library); cover and bearing layer typed with the decimal comma.
        Added("terzaghi-nastriforme-falda", (d, i, layers) => {
            i["Method"] = 1; i["Shape"] = 1; i["Compression"] = 1;
            i["Width"] = "1,8"; i["Depth"] = "1,2"; i["Normal"] = "320";
            d["nome_combinazione"] = "SLU · A1 · nastro"; d["azioni_confermate"] = true;
            layers[0]!["spessore"] = "1,2"; layers[0]!["peso_specifico"] = "17,5"; layers[0]!["peso_specifico_saturo"] = "19";
            layers[1]!["angolo_attrito"] = "32"; layers[1]!["coesione_efficace"] = "5";
            d["generali"]!["presenza_falda"] = true; d["generali"]!["profondita_falda"] = "2,5";
        });
        // Brinch Hansen, undrained, on a cohesive bearing layer: additive corrections; characteristic comparison without the A1
        // confirmation (hidden); eccentric actions along both sides; a custom concrete typed by its fck.
        Added("hansen-non-drenato-caratteristico", (d, i, layers) => {
            i["Method"] = 3; i["Condition"] = 1; i["Profile"] = 1;
            i["Width"] = "2,4"; i["Length"] = "3,6"; i["Depth"] = "1,5"; i["Normal"] = "900"; i["MomentX"] = "45"; i["MomentY"] = "80";
            d["nome_combinazione"] = "Caratteristica · confronto";
            layers[0]!["spessore"] = "1,5";
            layers[1] = Layer("Argilla limosa", "12", "Coesivo", "19", "20", "24", "8", "60");
            var section = d["sezione"]!.AsObject();
            section["classe_cls"] = "Personalizzato"; section["materiale_cls_nome"] = "Personalizzato"; section["fck_mpa"] = "28"; section["cls_diagramma"] = "Bilineare";
        });
        // Meyerhof with a large eccentricity along B (partial contact: compressed area smaller than the base), external surcharge,
        // depth factors excluded; the inputs collapsed in the sheet and three groups closed (vista_fondazioni).
        Added("meyerhof-eccentrica-input-raccolti", (d, i, layers) => {
            i["Method"] = 2; i["IncludeDepthFactors"] = false;
            i["Length"] = "2,5"; i["Normal"] = "800"; i["MomentY"] = "300"; i["Surcharge"] = "10";
            d["azioni_confermate"] = true;
            d["vista_fondazioni"] = J.Obj(("input_raccolti", true), ("metodo", false), ("Azioni", false), ("stratigrafia", false));
        });
        // Vesić on a centred circle, reduction for the relative density (drained sand, c′ = 0), groundwater above the base, two surveys
        // with the second one selected (γsat left empty: it follows γ); the shared concrete of the catalogue chosen as the editor of the
        // sheet writes it.
        Added("vesic-circolare-due-sondaggi", (d, i, layers) => {
            i["Method"] = 4; i["Shape"] = 2; i["Compression"] = 2; i["RelativeDensity"] = "0,35";
            i["Width"] = "3"; i["Depth"] = "0,8"; i["Normal"] = "1500";
            d["azioni_confermate"] = true;
            d["stratigrafie"]!.AsArray().Add(new JsonArray(Layer("Riporto", "0,8", "Granulare", "18", "19", "28", "0", "75"),
                Layer("Sabbia limosa", "15", "Granulare", "18,5", "", "29", "0", "75")));
            var general = d["generali"]!; general["presenza_falda"] = true; general["profondita_falda"] = "0,5"; general["sondaggio_fondazione"] = 1;
            var section = d["sezione"]!.AsObject(); section["classe_cls"] = "C25/30";
            foreach (var (key, value) in ConcreteMaterialCatalog.Concrete().First(m => m.S("nome") == "C25/30").Where(p => p.Key != "nome")) section[key] = value?.DeepClone();
        });
        // EC7 with the resultant outside the base: no resistant domain, ratios undefined, views of the results not available.
        Added("ec7-fuori-dominio", (d, i, layers) => {
            i["MomentY"] = "660"; d["nome_combinazione"] = "SLU · A1 · fuori dominio"; d["azioni_confermate"] = true;
        });
        // A layer with a thickness that is not a number: the calculation stops, no result, no report; the profile is not drawn.
        Added("strato-incompleto", (d, i, layers) => {
            i["Width"] = "2,5"; d["azioni_confermate"] = true;
            layers[1]!["tipologia"] = "Coesivo"; layers[1]!["spessore"] = "abc";
        });
        // The bearing layer is not assumed as a homogeneous half-space: the library refuses the calculation.
        Added("semispazio-non-assunto", (d, i, layers) => {
            i["Normal"] = "450"; d["azioni_confermate"] = true; d["generali"]!["strato_portante_semiinfinito"] = false;
        });
        // Version 1 archive with groundwater and the base at the ground surface (D = 0 typed as a text): the migration creates only
        // the bearing layer and moves the groundwater to the common data.
        Added("legacy-falda-piano-campagna", (d, i, layers) => {
            d.Remove("stratigrafie"); d.Remove("generali"); d.Remove("sezione"); d["versione_fondazioni"] = 1;
            i["Depth"] = "0"; i["WaterEnabled"] = true; i["WaterDepth"] = "0,6"; i["FrictionAngle"] = "34"; i["UnitWeight"] = "19"; i["SaturatedUnitWeight"] = "21";
            d["azioni_confermate"] = true;
        });
    }

    /// <summary>
    /// Retaining wall fixtures added before the preparation of F5.9 (proposal F5, section 7.2: capture first, with the historic view
    /// RetainingWallWorkspace): the two families with a calculation (cantilever with two zones and different faces, gravity in plain
    /// concrete and in masonry) and a family without a calculation; seismic from the site (Mononobe–Okabe, slope) and assigned (Wood);
    /// two soil columns, the passive in front with an assigned free height and automatic friction; actions of every type
    /// and nature, disabled and not drawn; a custom combination matrix with the variable surcharge excluded from the heel; global
    /// stability drained in two columns with groundwater and undrained with the seismic action of the site (search to refine); the advanced options
    /// (settlements, displacements, seismic bearing assigned, Newmark accelerogram, details of the bars, cover EC2, custom materials);
    /// warnings (checks not satisfied, steel of the catalogue with other values, cover below the minimum, no toe); and three calculations
    /// that stop: a stratigraphy that does not reach the foundation with an empty value, a custom matrix whose actions changed and a
    /// family not yet available. Documents as the sheet writes them (numbers for the typed values, JSON booleans for the options). The
    /// identifiers never end in "default", which the F3 project report collects.
    /// </summary>
    static void RetainingWallF59(List<CaptureCase> cases)
    {
        void Added(string id, Action<JsonObject> alter)
        {
            var data = ModuleCatalog.CreateData(RetainingWall.Module); alter(data);
            cases.Add(new(RetainingWall.Module, id, "Fixture sintetica F5.9 (proposta F5 7.2, cattura prima con la vista storica)", null, data));
        }
        static JsonObject Layer(string name, double thickness, double gamma, double saturated, double phi) =>
            J.Obj(("name", name), ("thickness", thickness), ("gamma", gamma), ("gamma_sat", saturated), ("phi", phi));
        static JsonObject Action(JsonObject d, string type, string name, string category, double value, params (string Key, object? Value)[] values)
        {
            var a = RetainingWall.NewAction(d, type); a["name"] = name; a["category"] = category; a["value"] = value;
            foreach (var (key, v) in values) a[key] = J.Node(v);
            return a;
        }
        static void Steel(JsonObject d, string name) => RetainingWall.ApplyMaterialPreset(d, ConcreteMaterialCatalog.Steel(false, "NTC 2018").Single(m => m.S("nome") == name));
        static void Concrete(JsonObject d, string name) => RetainingWall.ApplyMaterialPreset(d, ConcreteMaterialCatalog.Concrete().Single(m => m.S("nome") == name));
        static void Site(JsonObject d, string method, Action<JsonNode>? site = null)
        {
            var s = d["seismic"]!; s["enabled"] = true; s["source"] = RetainingWall.SeismicSite; s["method"] = method;
            s["ag_g"] = .2; s["soil_class"] = "C"; s["f0"] = 2.5; site?.Invoke(s);
            // kh and kv derived from the site, as the sheet writes them (the chapter of the project report does not derive them).
            var derived = RetainingWall.DeriveSeismic(d)!; s["kh"] = derived.Kh; s["kv"] = derived.Kv;
        }
        static void Global(JsonObject d, int grid, int slices, int refinements)
        {
            RetainingWall.PrepareGlobalProfile(d);
            var g = d["global_stability"]!; g["enabled"] = true; g["profile_confirmed"] = true;
            g["search_mode"] = "Assegnata"; g["depth_min"] = .1; g["grid"] = grid; g["slices"] = slices; g["refinements"] = refinements;
        }

        // Cantilever with the seismic action from the site at the SLV (Mononobe–Okabe, class C, slope of 20° higher than 15° and 40 m
        // high, the wall at 30 m: St > 1), two zones of reinforcement with the stem faces different and the upper zone lighter; the
        // default surcharge on the heel gives the twin rows «b» of the automatic matrix.
        Added("mensola-sisma-sito-pendio", d => {
            Site(d, "Mononobe–Okabe", s => { s["topography"] = RetainingWall.TopographySlope; s["slope"] = 20; s["relief_height"] = 40; s["site_height"] = 30; });
            var r = d["reinforcement"]!; r["two_zones"] = true; r["lower_height"] = 1.4;
            r["stem"]!["symmetric"] = false; r["stem"]!["opposite_diameter"] = 12; r["stem"]!["opposite_count"] = 5; r["stem"]!["lap_length"] = 700;
            r["stem_upper"]!["diameter"] = 12; r["stem_upper"]!["count"] = 5;
            d["geometry"]!["height"] = 3.6; d["geometry"]!["heel"] = 2.1;
        });
        // Cantilever with the seismic action assigned (Wood, rigid wall) and actions of every type and nature: horizontal force G2 in a
        // correlated group with a lateral pressure, a centred vertical force and a moment of a barrier in the group of the default
        // surcharge, an impact (accidental), a disabled action and one not drawn.
        Added("mensola-wood-azioni", d => {
            var s = d["seismic"]!; s["enabled"] = true; s["source"] = RetainingWall.SeismicManual; s["method"] = "Wood semplificato"; s["kh"] = .12; s["kv"] = .06;
            var actions = d.Array("actions");
            actions.Add(Action(d, "Forza orizzontale", "Spinta di un parapetto", "G2", 4, ("z", 3.2), ("group", "parapetto"), ("psi0", 1), ("psi1", 1), ("psi2", 1)));
            actions.Add(Action(d, "Pressione laterale", "Pressione del parapetto", "G2", 2.5, ("z", 3.45), ("z0", 2.45), ("group", "parapetto"), ("psi0", 1), ("psi1", 1), ("psi2", 1)));
            actions.Add(Action(d, "Forza verticale", "Carico della barriera", "Q", 6, ("z", 3.45), ("x", 1.1), ("group", "legacy")));
            actions.Add(Action(d, "Momento", "Momento della barriera", "Q", 3, ("z", 3.45), ("visible", false), ("group", "legacy")));
            actions.Add(Action(d, "Urto", "Urto di un veicolo", "A", 50, ("z", 3.2), ("psi0", 0), ("psi1", 0), ("psi2", 0)));
            actions.Add(Action(d, "Sovraccarico uniforme", "Sovraccarico di cantiere", "Q", 5, ("enabled", false)));
        });
        // Gravity in plain concrete with two different layers uphill and one in front, the passive in front with a mobilized fraction and
        // an assigned free height, automatic friction on the wall (cast in place) and on the base (precast).
        Added("gravita-cls-passiva", d => {
            d["family"] = "gravity";
            var g = d["geometry"]!; g["stem_base"] = 2.2; g["stem_top"] = .55; g["toe"] = .3; g["heel"] = .3;
            d["gravity_design"]!["type"] = "Calcestruzzo non armato"; Concrete(d, "C25/30");
            d["layers"] = new JsonArray(Layer("Riporto", 1.5, 17, 19, 28), Layer("Sabbia limosa", 8, 18.5, 20, 31));
            var v = d["valley"]!; v["height_mode"] = "Assegnato"; v["free_height"] = 2.6; v["passive"] = true; v["mobilization"] = .4;
            v["layers"] = new JsonArray(Layer("Sabbia limosa", 8, 18.5, 20, 31));
            var f = d["interfaces"]!; f["wall_mode"] = "Gettato in opera"; f["wall_phi_cv"] = 28; f["base_mode"] = "Prefabbricato liscio"; f["base_phi_cv"] = 30;
        });
        // Gravity in masonry, too slender for the default actions: checks not satisfied; the effective length and the eccentricity typed.
        Added("gravita-muratura-avvisi", d => {
            d["family"] = "gravity";
            var g = d["geometry"]!; g["height"] = 2.4; g["stem_base"] = 1.1; g["stem_top"] = .5; g["slab"] = .4; g["toe"] = .1; g["heel"] = .1;
            var p = d["gravity_design"]!; p["type"] = "Muratura"; p["fk"] = 4.5; p["fvk0"] = .1; p["fvk_limit"] = 1.2; p["gamma_m"] = 2.5;
            p["confidence"] = 1.2; p["elastic_modulus"] = 4500; p["eccentricity"] = 5; p["effective_height"] = 5;
        });
        // Custom matrix: the generated rows confirmed, then the variable surcharge excluded from the heel in the first SLU row, a factor of
        // the soil changed, γR,e of the passive typed, a row copied and one disabled (the rows of the matrix as the sheet writes them).
        Added("matrice-personalizzata", d => {
            d["valley"]!["passive"] = true; d["valley"]!["mobilization"] = .3;
            var rows = RetainingWall.GenerateCombinations(d);
            var first = rows.OfType<JsonObject>().First(r => r.S("state") == "SLU");
            first["heel_surcharge"] = false; first["soil"] = 1.35; first["rpassive"] = 1.2; first["approach"] = "Personalizzato";
            var copy = (JsonObject)first.DeepClone(); copy["name"] = first.S("name") + " copia " + (rows.Count + 1); copy["heel_surcharge"] = true;
            rows.Add(copy);
            rows.OfType<JsonObject>().Last(r => r.S("state") == "SLE")["enabled"] = false;
            d["combinations"] = rows; d["combination_mode"] = "Personalizzate"; d["combination_signature"] = RetainingWall.CombinationSignature(d);
        });
        // Global stability drained in two columns with a groundwater line of its own (below the foundation under the wall, rising
        // uphill), assigned search with the default grid.
        Added("globale-drenata-due-colonne", d => {
            d["layers"]![0]!["thickness"] = 25; d["valley"]!["layers"]![0]!["thickness"] = 12;
            Global(d, 9, 60, 4);
            var g = d["global_stability"]!; g["water_enabled"] = true;
            g["water"] = new JsonArray(J.Obj(("x", -13.8), ("y", -.8)), J.Obj(("x", 3), ("y", -.5)), J.Obj(("x", 16.8), ("y", .6)));
        });
        // Global stability undrained on a single profile (cu of the deep layers) with the seismic action at the SLV from the site, the same
        // site of the local checks (homogeneous dry soil); a coarse search: the outcome asks for more slices.
        Added("globale-non-drenata-sisma", d => {
            Site(d, "Mononobe–Okabe");
            d["layers"]![0]!["thickness"] = 25; d["valley"]!["layers"]![0]!["thickness"] = 25;
            Global(d, 7, 40, 3);
            var g = d["global_stability"]!; g["condition"] = "Non drenata"; g["soil_mode"] = "Profilo unico"; g["seismic"] = true; g["seismic_source"] = "Da sito · βs=0,38";
            foreach (var layer in g.Array("layers").OfType<JsonObject>()) layer["cu"] = 70;
            g["combination_mode"] = "Automatiche";
        });
        // Advanced options: settlements and displacements with two deformable layers, the seismic bearing with assigned accelerations
        // (seismic assigned, Mononobe–Okabe), a Newmark accelerogram, the details of the bars with good bond, cover EC2 with the
        // reductions, a custom concrete and the steel B450A, a longer panel for the bar schedule.
        Added("opzioni-avanzate", d => {
            var s = d["seismic"]!; s["enabled"] = true; s["source"] = RetainingWall.SeismicManual; s["kh"] = .1; s["kv"] = .05;
            var o = d["serviceability"]!; o["settlement"] = true; o["displacement"] = true; o["removed_pressure"] = 15; o["rigid_base"] = true;
            o["horizontal_stiffness"] = 40000;
            o["layers"] = new JsonArray(J.Obj(("name", "Limo sabbioso"), ("thickness", 3), ("modulus", 12000)), J.Obj(("name", "Ghiaia"), ("thickness", 5), ("modulus", 40000)));
            var samples = new JsonArray();
            for (int i = 0; i <= 60; i++) samples.Add(J.Obj(("t", i * .02), ("a_g", Math.Round(.25 * Math.Sin(i * .3), 4))));
            o["histories"] = new JsonArray(J.Obj(("enabled", true), ("name", "Storia SLV 1"), ("state", "SLV"), ("yield_g", .08), ("scale", 1.2), ("limit_mm", 50), ("compatible", true), ("samples", samples)));
            var b = d["bearing_seismic"]!; b["source"] = "Assegnata"; b["ground_kh"] = .15; b["ground_kv"] = .07; b["model_factor"] = 1;
            var t = d["detailing"]!; t["enabled"] = true; t["good_bond"] = true; t["lap_percent"] = 50; t["lap_clear"] = 40;
            t["cover_method"] = "EC2 2004"; t["cover_high_strength"] = true; t["cover_slab"] = true; t["cover_ec_quality"] = true;
            var m = d["materials"]!; m["classe_cls"] = "Personalizzato"; m["materiale_cls_nome"] = "CLS di progetto"; m["fck"] = 32;
            Steel(d, "B450A");
            d["bar_schedule"]!["panel_length"] = 6; d["bar_schedule"]!["tie_cut_length"] = 900;
        });
        // Warnings: no toe and a short heel (sliding and overturning not satisfied), steel saved with the name of the catalogue and other
        // values (DET-3), cover below the minimum for XC4.
        Added("mensola-avvisi", d => {
            var g = d["geometry"]!; g["toe"] = 0; g["heel"] = .9;
            Steel(d, "B450A"); d["materials"]!["steel_eps_u"] = 30;
            d["materials"]!["exposure"] = "XC4"; d["materials"]!["cover"] = 20;
        });
        // A stratigraphy that does not reach the foundation and an empty value in front: the calculation stops, no result, no report.
        Added("stratigrafia-incompleta", d => {
            d["layers"]![0]!["thickness"] = 2;
            d["valley"]!["layers"]![0]!["phi"] = "";
        });
        // A custom matrix confirmed before an action changed: the calculation asks to generate the combinations again.
        Added("matrice-da-rigenerare", d => {
            d["combinations"] = RetainingWall.GenerateCombinations(d); d["combination_mode"] = "Personalizzate";
            d["combination_signature"] = RetainingWall.CombinationSignature(d);
            d.Array("actions")[0]!["psi0"] = .6;
        });
        // A family planned for future developments: the sheet opens, no calculation.
        Added("tipologia-prevista", d => d["family"] = "semigravity");
    }
}
