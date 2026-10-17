using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Calculations;

/// <summary>
/// Presentation of the retaining wall sheet moved from the historic view in F5.9 (RetainingWallPresentation): texts of the header and
/// of the status, rules of the fields, the matrix of the combinations, the global stability (guide, search, deep layers) and the
/// sections, with values written by hand. The identity of the historic view with these functions is proved by ui/check-muri-historic
/// (traces frozen before the move) and by the F3 reference.
/// </summary>
internal static class RetainingWallPresentationChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var data = RetainingWall.Defaults();
            Check(WallSheetPresentation.TotalHeight(data) == 3.45, "H + t del muro predefinito");
            var seismic = RetainingWallPresentation.SeismicStatus(data, null);
            Check(seismic.Content == "Sisma: non attivo · imposta" && !seismic.Warning
                && seismic.ToolTip.StartsWith("Apri Input → Azioni → Sisma. Abilita l’azione sismica", StringComparison.Ordinal), "Pulsante del sisma non attivo");
            data["seismic"]!["enabled"] = true; data["seismic"]!["source"] = RetainingWall.SeismicManual; data["seismic"]!["kh"] = .12; data["seismic"]!["kv"] = .04;
            seismic = RetainingWallPresentation.SeismicStatus(data, null);
            Check(seismic.Content == "Sisma: attivo · da ricalcolare" && seismic.ToolTip.Contains("Mononobe–Okabe · kh=0,12 · |kv|=0,04.", StringComparison.Ordinal), "Pulsante del sisma da ricalcolare");
            Check(RetainingWallPresentation.GlobalSetupText(data, false) == "Stabilità globale: non attiva · imposta", "Stabilità globale non attiva");
            data["global_stability"]!["enabled"] = true;
            Check(RetainingWallPresentation.GlobalSetupText(data, false) == "Stabilità globale: dati / calcolo"
                && RetainingWallPresentation.GlobalSetupText(data, true) == "Stabilità globale: calcolata · imposta", "Stabilità globale attiva");
            Check(RetainingWallPresentation.SectionSelection(null) == "Selezionare una riga strutturale o una sezione nella tabella delle sollecitazioni."
                && RetainingWallPresentation.SectionSelection(("Fusto", .45, "SLU 4")) == "Fusto · z/l=0,45 m · SLU 4"
                && RetainingWallPresentation.SectionName(("Valle", 0.123456, "SLU 4")) == "Muro · Valle · z/l 0,123 m", "Testi della sezione selezionata");
            Check(RetainingWallPresentation.StatusError("x") == "Dati da correggere: x" && RetainingWallPresentation.GlobalFailed("y") == "Stabilità globale: y"
                && RetainingWallPresentation.GlobalUpdated(2) == "Stabilità globale aggiornata · 2 combinazioni. Le verifiche locali hanno un calcolo indipendente.", "Testi dello stato");

            // Rules of the fields: cantilever and gravity, materials, cover, seismic action of the site, soils.
            data = RetainingWall.Defaults();
            var rules = RetainingWallPresentation.InputRules(data, "Q");
            bool Shown(WallInputRules r, string form, string key) => r.Fields.Last(f => f.Form == form && f.Key == key).Visible;
            bool On(WallInputRules r, string form, string key) => r.Enabled.Last(f => f.Form == form && f.Key == key).Enabled;
            Check(Shown(rules, "materials", "exposure") && !Shown(rules, "materials", "compression_rd") && Shown(rules, "materials", "fck")
                && !On(rules, "materials", "fck") && !Shown(rules, "materials", "materiale_cls_nome"), "Materiali della mensola");
            Check(rules.Forms.Contains(("material_cover", true)) && rules.Forms.Contains(("gravity_design", false)) && rules.Cards.Contains(("rebar_stem_upper", false))
                && !Shown(rules, "rebar_stem", "opposite_diameter") && Shown(rules, "rebar_stem", "bend_diameter") && !Shown(rules, "rebar_toe", "bend_diameter"), "Armature della mensola");
            Check(Shown(rules, "action", "psi0") && !On(rules, "water", "depth") && !On(rules, "valley", "free_height") && On(rules, "foundation", "delta")
                && Shown(rules, "interfaces", "wall_delta") && !Shown(rules, "interfaces", "wall_phi_cv"), "Falda, valle e attriti predefiniti");
            Check(!On(rules, "seismic", "kh") && Shown(rules, "seismic", "ag_g") && !Shown(rules, "seismic", "kh") && !Shown(rules, "seismic", "relief_height"), "Sisma escluso dal sito");
            data["family"] = "gravity"; data["gravity_design"]!["type"] = "Muratura"; data["seismic"]!["enabled"] = true; data["seismic"]!["topography"] = RetainingWall.TopographySlope;
            data["seismic"]!["slope"] = 20; data["materials"]!["classe_cls"] = "Personalizzato";
            rules = RetainingWallPresentation.InputRules(data, null);
            Check(!Shown(rules, "materials", "exposure") && Shown(rules, "materials", "compression_rd") && Shown(rules, "materials", "creep") && !Shown(rules, "materials", "fck")
                && rules.Forms.Contains(("gravity_design", true)) && rules.Forms.Contains(("zones", false)) && rules.Cards.All(c => c.Card.StartsWith("rebar_", StringComparison.Ordinal) ? !c.Visible : true)
                && !rules.Fields.Any(f => f.Form == "action"), "Gravità in muratura");
            Check(On(rules, "seismic", "kh") && Shown(rules, "seismic", "relief_height") && Shown(rules, "seismic", "site_height") && Shown(rules, "seismic", "f0"), "Sisma del sito con pendio oltre 15°");
            var global = RetainingWallPresentation.GlobalRules(data);
            Check(global.Enabled.All(e => !e.Enabled && e.Dim) && global.Cards.SequenceEqual([("Strati globali valle", false)])
                && global.Fields.Contains(("global_soil_rear", "cu", false)) && global.Fields.Contains(("global", "seismic_source", false)), "Regole della stabilità globale automatica");
            Check(RetainingWallPresentation.ActionTypeFields("Pressione laterale").SequenceEqual([("z", true), ("x", false), ("z0", true)]), "Campi dell'azione per tipo");

            // Materials of the catalogues.
            data = RetainingWall.Defaults();
            data["materials"]!["classe_cls"] = "C99/99";
            RetainingWallPresentation.ApplyMaterialChoice(data, "classe_cls");
            Check(data["materials"].S("materiale_cls_nome") == "CLS personalizzato", "Calcestruzzo fuori catalogo");
            data["materials"]!["classe_acciaio"] = "B450A";
            RetainingWallPresentation.ApplyMaterialChoice(data, "classe_acciaio");
            Check(data["materials"].S("materiale_acciaio_nome") == "B450A" && data["materials"].D("steel_eps_u") > 0, "Acciaio del catalogo applicato");

            // Matrix of the combinations: MUR-1 and MUR-2, an edit makes it custom, copy and removal.
            data = RetainingWall.Defaults();
            RetainingWallPresentation.GenerateMatrix(data);
            var first = data.Array("combinations")[3]!.AsObject();
            first.Remove("heel_surcharge"); first.Remove("rpassive");
            var flat = RetainingWallPresentation.MatrixRow(data, first);
            string id = data.Array("actions")[0].S("id");
            Check(flat.B("heel_surcharge") && flat.D("rpassive") == 1.4 && flat["a_" + id] is not null && flat["coefficients"] is null && first["heel_surcharge"] is null, "Riga della matrice (MUR-1, MUR-2)");
            flat["wall"] = 1.2; RetainingWallPresentation.MatrixEdit(data, first, flat, "wall");
            Check(data.S("combination_mode") == "Personalizzate" && first.D("wall") == 1.2 && first.S("approach") == "Personalizzato" && data.S("combination_signature").Length > 0, "Cella della matrice");
            int count = data.Array("combinations").Count;
            RetainingWallPresentation.CopyCombination(data, 0);
            Check(data.Array("combinations").Count == count + 1 && data.Array("combinations")[^1].S("name") == "SLE 1 copia " + (count + 1), "Copia della combinazione");
            Check(RetainingWallPresentation.MatrixStatus(data).StartsWith("Personalizzate · " + (count + 1) + " righe · ", StringComparison.Ordinal), "Stato della matrice");

            // Global stability: guide, search and deep layers.
            data = RetainingWall.Defaults();
            var items = RetainingWallPresentation.GlobalReadiness(data, null);
            Check(items[0] == "Attiva la verifica globale oppure premi «Prepara dal muro»." && items.Contains("Inserisci gli strati profondi: profilo unico."), "Guida della globale vuota");
            var guidance = RetainingWallPresentation.GlobalGuidance(data, "errore");
            Check(!guidance.Ready && guidance.Text.StartsWith("Da completare · Attiva", StringComparison.Ordinal) && guidance.ToolTip.EndsWith(". Ultimo calcolo: errore", StringComparison.Ordinal), "Testo della guida");
            Check(RetainingWallPresentation.GlobalSearchSummary(data) == "Aggiungi gli strati sotto la fondazione: la profondità di ricerca si aggiornerà automaticamente.", "Ricerca senza strati");
            data["global_stability"]!["depth_max"] = 6.9;
            Check(RetainingWallPresentation.GlobalSearchSummary(data) == "Ricerca da 0,5 a 6,9 m sotto la fondazione · 60 conci. Limiti visibili nel disegno.", "Ricerca con gli strati");
            Check(RetainingWallPresentation.GlobalSoilTop(data, false) == 3.45 && RetainingWallPresentation.GlobalSoilTop(data, true) == 0, "Sommità delle colonne senza profilo");
            var bottoms = RetainingWallPresentation.GlobalBottoms(3.45, [JsonValue.Create(2.45), JsonValue.Create("abc"), JsonValue.Create(1)]);
            Check(bottoms[0] == 1 && double.IsNaN(bottoms[1]) && double.IsNaN(bottoms[2]), "Fondi degli strati globali");
            Check(RetainingWallPresentation.GlobalThickness(1, JsonValue.Create(-2)).GetValue<double>() == 3 && RetainingWallPresentation.GlobalThickness(1, JsonValue.Create("")).GetValue<string>() == "",
                "Spessore degli strati globali");
            Check(RetainingWallPresentation.HistorySamples(J.Obj(("samples", new JsonArray(J.Obj(("t", 0)))))) == "1 campioni salvati · tempo [s], accelerazione verso valle [g]"
                && RetainingWallPresentation.NewHistory(2).S("name") == "Storia 3", "Accelerogrammi");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Console.WriteLine($"Presentazione dei muri: {checks} controlli");
        return checks;
    }
}
