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
        HorizontalPileF57(cases);
        StratigraphyF56b(cases);
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
    /// Horizontal pile and micropile fixtures added before the F5.7 preparation (proposal F5, section 7.2: capture first, with the
    /// historic view HorizontalWorkspace), all on the capacity tab (the elastic response is F5.8): Broms with the head restrained (short and
    /// intermediate mechanisms) and with the head free, the stratified method on mixed sequences, groundwater with its unit weight, eccentricity and axial force,
    /// several surveys, the efficiency of Reese and Van Impe with spacings and the manual one, ξ of more investigated verticals, the
    /// moment from the c.a. section (second inner ring, unit coefficients), from the CHS section (catalogue and manual tube) and manual,
    /// and two calculations that stop with missing data (mixed sequence with Broms, a survey shorter than the pile). Documents as the
    /// sheet writes them: texts for the typed values (also with a decimal comma), JSON booleans for the options. The identifiers never
    /// end in "default", which the F3 project report collects.
    /// </summary>
    static void HorizontalPileF57(List<CaptureCase> cases)
    {
        void Added(string module, string id, Action<JsonObject, JsonObject, JsonObject, JsonObject> alter)
        {
            var data = ModuleCatalog.CreateData(module);
            alter(data, data["generali"]!.AsObject(), data["sezione"]!.AsObject(), data["verifica"]!.AsObject());
            cases.Add(new(module, id, "Fixture sintetica F5.7 (proposta F5 7.2, cattura prima con la vista storica)", null, data));
        }
        static JsonObject Layer(string type, string thickness, string gamma, string saturated, string phi, string undrained = "0") =>
            J.Obj(("tipologia", type), ("spessore", thickness), ("peso_specifico", gamma), ("peso_specifico_saturo", saturated),
                ("angolo_attrito", phi), ("coesione_non_drenata", undrained), ("coesione_efficace", "0"));
        static void Efficiency(JsonObject v, string front, string back, string left, string right)
        {
            v["efficienza_metodo"] = "Reese & Van Impe (foglio)"; v["efficienza_eta"] = "1";
            v["interasse_anteriore"] = front; v["interasse_posteriore"] = back; v["interasse_sinistro"] = left; v["interasse_destro"] = right;
        }

        // A short pile in cohesive soils with Broms and the head restrained (short mechanism in the first survey, intermediate in the
        // second), groundwater with its unit weight, axial force, ξ of three verticals, efficiency of Reese and Van Impe; moment of the
        // c.a. section with a second inner ring of bars.
        Added(PaloOrizzontale.Module, "broms-coesivo-impedita", (d, g, s, v) => {
            g["diametro"] = "0.8"; g["lunghezza"] = "3"; g["vincolo"] = "Impedita";
            g["azione_orizzontale"] = "350"; g["azione_assiale"] = "800"; g["presenza_falda"] = true; g["profondita_falda"] = "2"; g["gamma_acqua"] = "10";
            s["longitudinal_bar_count"] = "18"; s["longitudinal_bar_diameter_mm"] = "26"; s["cover_mm"] = "60";
            s["second_inner_enabled"] = true; s["second_inner_count"] = "8"; s["second_inner_diameter"] = "20"; s["second_inner_gap"] = "30";
            v["verticali_indagate"] = "3"; Efficiency(v, "2.4", "2,4", "3", "3");
            d["stratigrafie"] = new JsonArray(
                new JsonArray(Layer("Coesivo", "4", "18", "19", "0", "40"), Layer("Coesivo", "10", "19", "20", "0", "90")),
                new JsonArray(Layer("Coesivo", "14", "18.5", "19.5", "0", "60")));
        });
        // Stratified method on a mixed sequence (experimental model) with groundwater, head free, manual moment with its source, unit
        // coefficients of the section, manual efficiency and ξ of at least ten verticals.
        Added(PaloOrizzontale.Module, "stratificato-misto-manuale", (d, g, s, v) => {
            g["diametro"] = "1.2"; g["lunghezza"] = "16"; g["metodo_calcolo"] = PaloOrizzontale.StratifiedMethod;
            g["azione_orizzontale"] = "600"; g["presenza_falda"] = true; g["profondita_falda"] = "3,5";
            g["origine_momento"] = "Manuale"; g["momento_resistente"] = "2500"; g["provenienza_momento"] = "Verifica della sezione esterna";
            s["coefficienti_unitari"] = true;
            v["verticali_indagate"] = "≥10"; v["efficienza_eta"] = "0,85";
            d["stratigrafie"] = new JsonArray(new JsonArray(Layer("Granulare", "3", "18", "20", "28"), Layer("Coesivo", "5", "19", "20", "0", "50"),
                Layer("Granulare", "10", "19", "21", "34")));
        });
        // Broms with a mixed sequence in the second survey and a first survey shorter than the pile: the calculation stops with «Dati da
        // completare» while the moment of the section is shown.
        Added(PaloOrizzontale.Module, "dati-incompleti", (d, g, s, v) => {
            g["diametro"] = "1"; g["lunghezza"] = "12"; g["azione_orizzontale"] = "250";
            d["stratigrafie"] = new JsonArray(new JsonArray(Layer("Granulare", "8", "18", "20", "32")),
                new JsonArray(Layer("Granulare", "5", "18", "20", "30"), Layer("Coesivo", "9", "19", "20", "0", "70")));
        });
        // Manual CHS tube with grout weight, head restrained, groundwater, two granular surveys, ξ of two verticals, efficiency of Reese
        // and Van Impe.
        Added(MicropaloOrizzontale.Module, "chs-manuale-impedita", (d, g, s, v) => {
            g["diametro"] = "0.3"; g["lunghezza"] = "9"; g["vincolo"] = "Impedita"; g["azione_orizzontale"] = "60"; g["azione_assiale"] = "150";
            g["presenza_falda"] = true; g["profondita_falda"] = "1,5";
            s["modo_chs"] = "Manuale"; s["diametro_chs_mm"] = "168.3"; s["spessore_chs_mm"] = "10"; s["gamma_iniezione"] = "22";
            v["verticali_indagate"] = "2"; Efficiency(v, "0.9", "1.2", "0.9", "0,9");
            d["stratigrafie"] = new JsonArray(new JsonArray(Layer("Granulare", "4", "18", "20", "30"), Layer("Granulare", "6", "19", "21", "35")),
                new JsonArray(Layer("Granulare", "10", "18.5", "20.5", "32")));
        });
        // Stratified method on a mixed sequence, eccentricity, the moment of a CHS of the catalogue and manual efficiency.
        Added(MicropaloOrizzontale.Module, "stratificato-chs-catalogo", (d, g, s, v) => {
            g["diametro"] = "0.25"; g["lunghezza"] = "12"; g["eccentricita"] = "0.3"; g["metodo_calcolo"] = PaloOrizzontale.StratifiedMethod;
            g["azione_orizzontale"] = "40";
            s["profilo_chs"] = "CHS 193.7 × 12.5";
            v["efficienza_eta"] = "0.9";
            d["stratigrafie"] = new JsonArray(new JsonArray(Layer("Coesivo", "4", "18", "19", "0", "35"), Layer("Granulare", "8", "19", "21", "33")));
        });
        // Manual CHS tube without thickness: section and capacity stop with missing data.
        Added(MicropaloOrizzontale.Module, "dati-incompleti", (d, g, s, v) => {
            g["diametro"] = "0.24"; g["lunghezza"] = "10"; g["azione_orizzontale"] = "50";
            s["modo_chs"] = "Manuale"; s["diametro_chs_mm"] = "139.7"; s["spessore_chs_mm"] = "";
            d["stratigrafie"] = new JsonArray(new JsonArray(Layer("Granulare", "10", "18", "20", "31")));
        });
    }

    /// <summary>
    /// The first fixtures of the stratigraphy (geo_stratigrafia), added before the F5.6b preparation (proposal F5, section 7.2: capture
    /// first, with the historic view StratigraphyWorkspace in SheetEditor); until then the module had none. They cover a complete profile
    /// with the water table and four finite layers of both behaviours, mean values only and no warning (transferable to the verification
    /// modules); means and minima with «Entrambi», a last layer of indefinite extent, an unknown water table and every warning of the sheet
    /// (incomplete parameters, γsat lower than γ, minima incomplete or greater than the means); the values as the sheet writes them (texts
    /// with the decimal comma, empty texts, «Minimi», water not considered, a layer without behaviour, a lower-case colour); a document of
    /// the first schema without the later keys (choice, family, behaviour, minima) with the provenance of a reviewed AI import; and a
    /// stratigraphy received from a vertical pile sheet (StratigraphyTransfer.ToDocument). Fixed identifiers; the documents are
    /// archive-valid (StratigraphyDocument.Validate, which the sheet runs at every commit: an invalid document cannot be captured); the
    /// identifiers never end in "default", which the F3 project report collects.
    /// </summary>
    static void StratigraphyF56b(List<CaptureCase> cases)
    {
        const string module = Anthea.Application.Stratigraphy.StratigraphyDocument.Module;
        const string origin = "Fixture sintetica F5.6b (proposta F5 7.2, cattura prima con la vista storica)";
        static string Id(int n) => new Guid(0x56b00000 + n, 0x0f56, 0x4b00, 0x80, 0, 0, 0, 0, 0, 0, (byte)n).ToString("N");
        static JsonObject Minimum(object? gamma, object? saturated, object? phi, object? c, object? cu, string source, string qualification, string notes) =>
            J.Obj(("gamma", gamma), ("gamma_sat", saturated), ("phi", phi), ("c", c), ("cu", cu), ("source", source), ("qualification", qualification), ("notes", notes));
        // The keys of StratigraphyDocument.NewLayer, in its order; a null behaviour is left out (documents before the field).
        static JsonObject Layer(int id, string name, string description, string color, string? soil, string? behavior, object? thickness,
            object? gamma, object? saturated, object? phi, object? c, object? cu, string source, string qualification, string notes, JsonObject? minimum = null, bool withMinimum = true)
        {
            var layer = J.Obj(("id", Id(id)), ("name", name), ("description", description), ("color", color));
            if (soil is not null) layer["soil_type"] = soil;
            if (behavior is not null) layer["behavior"] = behavior;
            layer["thickness"] = J.Node(thickness); layer["infinite"] = thickness is null;
            foreach (var (key, value) in new[] { ("gamma", gamma), ("gamma_sat", saturated), ("phi", phi), ("c", c), ("cu", cu) }) layer[key] = J.Node(value);
            layer["source"] = source; layer["qualification"] = qualification; layer["notes"] = notes;
            if (withMinimum) layer["minimum"] = minimum;
            return layer;
        }
        void Added(string id, JsonObject data)
        {
            Anthea.Application.Stratigraphy.StratigraphyDocument.Validate(data);
            cases.Add(new(module, id, origin, null, data));
        }
        // Complete profile: water table at 2,5 m, four finite layers (granular and cohesive), means only, qualifications of every kind.
        Added("profilo-medi-falda", J.Obj(("schema", 2), ("scenario", "Medi"), ("id", Id(1)), ("name", "Sondaggio S1 · via dei Tigli"),
            ("description", "Sondaggio a carotaggio continuo S1, campagna 2025"), ("ground_elevation", 112.4), ("water_state", "Assegnata"), ("water_depth", 2.5),
            ("layers", new JsonArray(
                Layer(2, "Riporto antropico", "Terreno di riporto eterogeneo", "#B29C7C", "Riporto", "Granulare", 1.2, 17, 19, 28, 0, 0, "Relazione geologica, tab. 2", "Interpretato", ""),
                Layer(3, "Sabbia limosa", "Sabbia fine limosa, mediamente addensata", "#D5BE92", "Sabbia", "Granulare", 3.8, 18.5, 20, 32, 0, 0, "Prove SPT S1, p. 14", "Misurato", "Parametri da prove in sito."),
                Layer(4, "Limo argilloso", "Limo argilloso grigio, consistente", "#A7B7C7", "Limo", "Coesivo", 4, 19, 19.5, 26, 8, 65, "Prove di laboratorio C2, p. 22", "Caratteristico", ""),
                Layer(5, "Argilla", "Argilla limosa sovraconsolidata", "#8E9AAF", "Argilla", "Coesivo", 7.5, 19.8, 20.4, 24, 15, 120, "Prove triassiali C4", "Misurato", "Fine sondaggio a 16,5 m.")))));
        // «Entrambi»: complete minima, minima incomplete and greater than the means, γsat lower than γ, a last layer of indefinite extent
        // without cu and without minima, the water table not known.
        Added("minimi-entrambi-indefinito", J.Obj(("schema", 2), ("scenario", "Entrambi"), ("id", Id(10)), ("name", "Profilo P2 · medi e minimi"),
            ("description", "Valori medi e minimi distinti dalla relazione geotecnica"), ("ground_elevation", null), ("water_state", "Non nota"), ("water_depth", null),
            ("layers", new JsonArray(
                Layer(11, "Ghiaia sabbiosa", "", "#C9B27C", "Ghiaia", "Granulare", 2, 19, 21, 36, 0, 0, "Tab. 5, valori medi", "Caratteristico", "",
                    Minimum(18, 20, 33, 0, 0, "Tab. 5, valori minimi", "Caratteristico", "Minimi da prove penetrometriche")),
                Layer(12, "Torba", "Torba e limo organico", "#6B5B45", "Terreno organico", "Coesivo", 1.5, 14, 13.5, 22, 5, 25, "Prove C3", "Misurato", "",
                    Minimum(13, null, 24, 4, null, "", "Non qualificato", "")),
                Layer(13, "Calcare fratturato", "Calcare fratturato in banchi", "#9A9A8E", "Roccia", "Da definire", null, 24, 25, 40, 50, null, "Stima di letteratura", "Interpretato",
                    "Estensione del substrato non indagata.")))));
        // The values as the sheet writes them: texts, the decimal comma, empty texts (no value), «Minimi», water not considered, a layer
        // without the behaviour key, a lower-case colour, a description of two lines.
        Added("testi-digitati", J.Obj(("schema", 2), ("scenario", "Minimi"), ("id", Id(20)), ("name", "Stratigrafia digitata"),
            ("description", "Riga 1\nRiga 2, profondità 3,5 m"), ("ground_elevation", ""), ("water_state", "Non considerata"), ("water_depth", ""),
            ("layers", new JsonArray(
                Layer(21, "Strato A", "", "#a7b7c7", "Altro", null, "1,5", "18,5", "", "30", "0", "", "", "Non qualificato", "Valori digitati con la virgola",
                    Minimum("17,5", "19", "27,5", "0", "0", "Stima", "Interpretato", "")),
                Layer(22, "Strato B", "Limo", "#D5BE92", "Non classificato", "Coesivo", "2", "19", "20", "25", "10", "40", "Tab. 1", "Misurato", "",
                    Minimum("18", "19,5", "22", "5", "30", "Tab. 1, minimi", "Misurato", "Note sui minimi"))))));
        // A document of the first schema (no choice, family, behaviour or minima keys), water at the ground surface, a last layer of
        // indefinite extent, and the provenance of a reviewed AI import (fixed date, no key: the key is never saved).
        var imported = J.Obj(("schema", 1), ("id", Id(30)), ("name", "S3 · bozza AI revisionata"),
            ("description", "Sondaggio S3\nRelazione geologica 2024, p. 5-9\nProfilo estratto e revisionato"), ("ground_elevation", 87), ("water_state", "Assegnata"), ("water_depth", 0),
            ("layers", new JsonArray(
                Layer(31, "Limo sabbioso", "Limo sabbioso nocciola", "#D5BE92", null, null, 6, 18, 19, 29, 2, null, "relazione-geologica.pdf\nphi: p. 7, tabella 3", "Non qualificato",
                    "\nGeometria: p. 6, colonna stratigrafica", withMinimum: false),
                Layer(32, "Ghiaia", "Ghiaia in matrice sabbiosa", "#C9B27C", null, null, null, 20, 22, 38, 0, null, "relazione-geologica.pdf\nphi: p. 8", "Non qualificato",
                    "\nGeometria: p. 6, base non raggiunta", withMinimum: false))));
        imported["import_source"] = J.Obj(("file", "relazione-geologica.pdf"), ("sha256", "8F3C2A1B0E9D8C7B6A5F4E3D2C1B0A9F8E7D6C5B4A3F2E1D0C9B8A7F6E5D4C3B"),
            ("model", "modello-di-prova"), ("date", "2026-09-30T08:15:00.0000000+00:00"), ("warnings", "Controllare le unità della tabella 3"), ("reviewed", true));
        Added("schema1-importata", imported);
        // A stratigraphy received from a vertical pile sheet (StratigraphyTransfer.ToDocument): the layer texts of the pile, the behaviour
        // from its type, the notes of the transfer, γsat empty in the second layer, and the provenance of the transfer.
        const string transferNote = "Parametri importati dall’insieme unico del modulo: verificare la qualificazione come valori medi. Nessun minimo ricostruito.";
        var transferred = J.Obj(("schema", 2), ("scenario", "Medi"), ("id", Id(40)), ("name", "Stratigrafia"), ("description", ""), ("ground_elevation", null),
            ("water_state", "Assegnata"), ("water_depth", "2,5"),
            ("layers", new JsonArray(
                Layer(41, "Strato 1", "", "#D5BE92", "Non classificato", "Granulare", "4", "18", "20", "30", "0", "0", "Foglio ANTHEA: Palo P1", "Non qualificato", transferNote),
                Layer(42, "Strato 2", "", "#D5BE92", "Non classificato", "Coesivo", "6", "19", "", "24", "10", "80", "Foglio ANTHEA: Palo P1", "Non qualificato", transferNote))));
        transferred["soil_transfer"] = J.Obj(("source_sheet", "Palo P1"), ("source_module", "geo_palo_verticale"), ("source_id", Id(43)));
        Added("trasferita-da-palo", transferred);
    }
}
