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
        VerticalPileF56(cases);
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
}
