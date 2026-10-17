using System.Text.Json.Nodes;
using Anthea.Application.Materials;
using Anthea.Application.Walls;

namespace Anthea.Application.Presentation
{
    /// <summary>
    /// What the rules of the inputs of the wall sheet show and enable for the current data (UpdateFields, or RefreshGlobal for the global
    /// stability, of the historic view): the visibility of fields, the enabled fields (faded when <c>Dim</c>), the visibility of whole forms
    /// and of cards.
    /// A form or a card that does not exist yet is skipped by the view. The values are the final ones of the sequence of the view.
    /// </summary>
    public sealed record WallInputRules(IReadOnlyList<(string Form, string Key, bool Visible)> Fields, IReadOnlyList<(string Form, string Key, bool Enabled, bool Dim)> Enabled,
        IReadOnlyList<(string Form, bool Visible)> Forms, IReadOnlyList<(string Card, bool Visible)> Cards);

    /// <summary>Inputs of the wall sheet: the rules of the fields, the materials of the catalogues, the combination matrix and the accelerograms.</summary>
    public static partial class RetainingWallPresentation
    {
        public const string ApproachTitle = "Ipotesi, approcci e tipologie previste";
        public const string MatrixEdited = "Personalizzate · modifiche conservate · i fattori delle singole azioni includono γ e ψ";
        public const string MatrixConfirmed = "Matrice personalizzata confermata";
        public const string SeismicDisabled = "Sisma escluso. Attivarlo per calcolare e verificare i casi sismici.";
        public const string SeismicManualText = "Modalità manuale: kh e |kv| sono dati assegnati. Per ricavarli da ag/g, selezionare Da parametri del sito (SLV).";

        /// <summary>The typologies planned and not yet calculated, in the text of the hypotheses of the results.</summary>
        public static string PlannedFamilies => "Tipologie predisposte: " + string.Join(", ", WallModel.Families.Where(f => !f.Available).Select(f => f.Name)) + ". Calcolo attivo soltanto per mensola e gravità.";

        /// <summary>
        /// The rules of the inputs for the data (UpdateFields, UpdateWallMaterials, UpdateSoilFields, UpdateSeismicFields, RefreshGlobal and the
        /// form of the selected action of the historic view): what each field shows and enables.
        /// </summary>
        /// <param name="data">The data of the sheet.</param>
        /// <param name="actionCategory">The nature of the selected action, null without a selected action.</param>
        public static WallInputRules InputRules(JsonObject data, string? actionCategory)
        {
            var visible = new Dictionary<(string, string), bool>(); var enabled = new Dictionary<(string, string), (bool, bool)>();
            var order = new List<(string, string)>(); var enabledOrder = new List<(string, string)>();
            void Show(string form, string key, bool show) { if (!visible.ContainsKey((form, key))) order.Add((form, key)); visible[(form, key)] = show; }
            void Enable(string form, string key, bool on, bool dim = false) { if (!enabled.ContainsKey((form, key))) enabledOrder.Add((form, key)); enabled[(form, key)] = (on, dim); }
            var forms = new List<(string, bool)>(); var cards = new List<(string, bool)>();
            bool rc = data.S("family") == "cantilever";
            foreach (var f in WallModel.MaterialFields) Show("materials", f.Key, f.Key == "gamma" || (rc ? !f.Key.EndsWith("_rd", StringComparison.Ordinal) : f.Key.EndsWith("_rd", StringComparison.Ordinal)));
            Show("materials", "exposure", rc);
            if (!rc && data["gravity_design"].S("type") == "Calcestruzzo non armato") { Show("materials", "fck", true); Show("materials", "creep", true); }
            if (!rc && data["gravity_design"].S("type") == "Muratura") Show("materials", "creep", true);
            // UpdateWallMaterials
            var m = data["materials"]!;
            bool concrete = WallModel.UsesConcrete(data);
            foreach (string key in new[] { "classe_cls", "fck", "cls_diagramma", "alpha_cc", "gamma_c", "__fcd", "__ecm" }) Show("materials", key, concrete);
            foreach (string key in new[] { "classe_acciaio", "fyk", "steel_modulus_mpa", "steel_fu_mpa", "steel_eps_u", "steel_diagramma", "gamma_s", "__fyd", "cover", "exposure" }) Show("materials", key, rc);
            Show("materials", "materiale_cls_nome", concrete && m.S("classe_cls") == "Personalizzato");
            Show("materials", "materiale_acciaio_nome", rc && m.S("classe_acciaio") == "Personalizzato");
            Enable("materials", "fck", m.S("classe_cls") == "Personalizzato", true);
            foreach (string key in new[] { "fyk", "steel_modulus_mpa", "steel_fu_mpa", "steel_eps_u" }) Enable("materials", key, m.S("classe_acciaio") == "Personalizzato", true);
            forms.Add(("material_cover", rc));
            bool ec = data["detailing"].S("cover_method") == "EC2 2004";
            foreach (string key in new[] { "cover_high_strength", "cover_slab", "cover_ec_quality" }) Show("material_cover", key, ec);
            Show("material_cover", "cover_element", !ec); Show("material_cover", "cover_quality", !ec);
            // UpdateFields
            forms.Add(("gravity_design", !rc));
            forms.Add(("detailing", rc));
            foreach (string key in new[] { "stem", "stem_upper", "toe", "heel" })
            {
                bool symmetric = data["reinforcement"]![key].B("symmetric", true);
                Show("rebar_" + key, "opposite_diameter", !symmetric); Show("rebar_" + key, "opposite_count", !symmetric);
                Show("rebar_" + key, "lap_length", key.StartsWith("stem", StringComparison.Ordinal) && data["reinforcement"].B("two_zones"));
                Show("rebar_" + key, "bend_diameter", key == "stem");
            }
            bool assigned = data["bearing_seismic"].S("source") == "Assegnata";
            Show("bearing_seismic", "ground_kh", assigned); Show("bearing_seismic", "ground_kv", assigned);
            foreach (string key in new[] { "stem", "stem_upper", "toe", "heel" })
                cards.Add(("rebar_" + key, rc && (key != "stem_upper" || data["reinforcement"].B("two_zones"))));
            forms.Add(("zones", rc));
            Show("zones", "lower_height", data["reinforcement"].B("two_zones"));
            foreach (string key in new[] { "depth", "front_head" }) Enable("water", key, data["water"].B("enabled"), true);
            // UpdateSoilFields
            Enable("valley", "free_height", data["valley"].S("height_mode") == "Assegnato");
            Enable("valley", "mobilization", data["valley"].B("passive"));
            Show("interfaces", "wall_delta", data["interfaces"].S("wall_mode") == "Assegnato");
            Show("interfaces", "wall_phi_cv", data["interfaces"].S("wall_mode") is "Gettato in opera" or "Prefabbricato liscio");
            Show("interfaces", "base_phi_cv", data["interfaces"].S("base_mode") is "Gettato in opera" or "Prefabbricato liscio");
            Enable("foundation", "delta", data["interfaces"].S("base_mode") == "Assegnato");
            // UpdateSeismicFields
            var s = data["seismic"]!;
            bool seismic = s.B("enabled"), site = s.S("source") == WallModel.SeismicSite;
            bool ssAuto = s.S("ss_mode") == WallModel.AmplificationCalculated, stAuto = s.S("st_mode") == WallModel.AmplificationCalculated;
            bool relief = s.S("topography") != WallModel.TopographyFlat;
            foreach (string key in new[] { "method", "source", "ag_g", "ss_mode", "soil_class", "f0", "ss", "st_mode", "topography", "slope", "relief_height", "site_height", "st", "kh", "kv" })
                Enable("seismic", key, seismic, true);
            foreach (string key in new[] { "kh", "kv" }) Show("seismic", key, !site);
            foreach (string key in new[] { "ag_g", "ss_mode", "st_mode" }) Show("seismic", key, site);
            Show("seismic", "soil_class", site && ssAuto); Show("seismic", "f0", site && ssAuto && s.S("soil_class") != "A"); Show("seismic", "ss", site && !ssAuto);
            Show("seismic", "topography", site && stAuto); Show("seismic", "st", site && !stAuto); Show("seismic", "slope", site && stAuto && relief);
            foreach (string key in new[] { "relief_height", "site_height" }) Show("seismic", key, site && stAuto && relief && s.D("slope") > 15);
            // The form of the selected action
            if (actionCategory is not null) foreach (string key in new[] { "psi0", "psi1", "psi2" }) Show("action", key, actionCategory == "Q");
            return new(order.Select(k => (k.Item1, k.Item2, visible[k])).ToArray(), enabledOrder.Select(k => (k.Item1, k.Item2, enabled[k].Item1, enabled[k].Item2)).ToArray(), forms, cards);
        }

        /// <summary>
        /// The rules of the global stability (RefreshGlobal of the historic view, also after a calculation or a choice of the global
        /// combination): the limits of the search disabled in the automatic mode, the properties of the deep layers for the condition, the
        /// column of the valley, the boundary of the two columns and the seismic coefficients assigned.
        /// </summary>
        public static WallInputRules GlobalRules(JsonObject data)
        {
            var g = data["global_stability"]!;
            bool automatic = g.S("search_mode") == "Automatica", drained = g.S("condition") != "Non drenata", two = g.S("soil_mode") == "Due colonne";
            bool manual = g.B("seismic") && g.S("seismic_source") == "kh e kv assegnati";
            var fields = new List<(string, string, bool)>();
            foreach (string form in new[] { "global_soil_rear", "global_soil_valley" }) fields.AddRange([(form, "phi", drained), (form, "c", drained), (form, "cu", !drained)]);
            fields.AddRange([("global", "soil_split_x", two), ("global", "seismic_source", g.B("seismic")), ("global", "kh", manual), ("global", "kv", manual)]);
            return new(fields, GlobalSearchLimits.Select(key => ("global_search", key, !automatic, true)).ToArray(), [], [("Strati globali valle", two)]);
        }

        /// <summary>The limits of the search, disabled and written by the proposal in the automatic mode.</summary>
        public static IReadOnlyList<string> GlobalSearchLimits { get; } = ["exit_min", "exit_max", "entry_min", "entry_max", "depth_min", "depth_max"];

        /// <summary>The fields of a new action form that depend on its type: z (not for the uniform surcharge), x (vertical force), z₀ (lateral pressure).</summary>
        public static IReadOnlyList<(string Key, bool Visible)> ActionTypeFields(string type) => [("z", type != "Sovraccarico uniforme"), ("x", type == "Forza verticale"), ("z0", type == "Pressione laterale")];

        public const string ActionHelp = "Quote z dal piano di posa; x dal bordo a valle. H e pressioni positivi verso valle, N in compressione, M ribaltante. Urto: forza statica equivalente assegnata, distribuita per metro. Il sovraccarico è uniforme su tutto il riempimento.";

        /// <summary>
        /// A choice of the concrete or of the steel of the catalogue: the preset of the catalogue, or a custom material with its own name.
        /// The view then shows again every field of the form.
        /// </summary>
        public static void ApplyMaterialChoice(JsonObject data, string key)
        {
            var m = data["materials"]!.AsObject();
            var catalog = key == "classe_cls" ? MaterialInputCatalog.Concrete("NTC 2018") : MaterialInputCatalog.Steel(false, "NTC 2018");
            var preset = catalog.FirstOrDefault(x => x.S("nome") == m.S(key));
            if (preset is not null) WallModel.ApplyMaterialPreset(data, preset);
            else m[key == "classe_cls" ? "materiale_cls_nome" : "materiale_acciaio_nome"] = key == "classe_cls" ? "CLS personalizzato" : "Acciaio personalizzato";
        }

        // ---------------------------------------------------------------- combination matrix

        /// <summary>
        /// The display copy of a combination for the matrix: the factors, one column «a_id» for each action, the surcharge on the heel of the
        /// rows saved before the flag (shown checked, MUR-1) and γR,e of the passive of the preset for the rows saved without it (MUR-2).
        /// </summary>
        public static JsonObject MatrixRow(JsonObject data, JsonObject combination)
        {
            var flat = (JsonObject)combination.DeepClone(); flat.Remove("coefficients");
            foreach (var a in data.Array("actions")) flat["a_" + a.S("id")] = combination["coefficients"]?[a.S("id")]?.DeepClone();
            // MUR-1: the rows saved before the flag keep the variable surcharge on the heel (shown checked, stored only when edited).
            flat["heel_surcharge"] ??= WallModel.HeelSurcharge(combination);
            // MUR-2: γR,e of the passive of the preset for the rows saved without it (the value used by the calculation).
            if (flat["rpassive"] is null && double.IsFinite(WallModel.DefaultPassiveFactor(combination.S("state")))) flat["rpassive"] = WallModel.DefaultPassiveFactor(combination.S("state"));
            return flat;
        }

        /// <summary>A cell of the matrix edited: the matrix becomes custom (its signature kept when it was automatic), the value goes to the combination and its approach becomes custom.</summary>
        public static void MatrixEdit(JsonObject data, JsonObject combination, JsonObject flat, string key)
        {
            if (data.S("combination_mode") == "Automatiche") data["combination_signature"] = WallModel.CombinationSignature(data);
            data["combination_mode"] = "Personalizzate";
            if (key.StartsWith("a_", StringComparison.Ordinal)) combination["coefficients"]![key[2..]] = flat[key]?.DeepClone(); else combination[key] = flat[key]?.DeepClone();
            combination["approach"] = "Personalizzato"; flat["approach"] = "Personalizzato";
        }

        /// <summary>The state of the matrix under its buttons.</summary>
        public static string MatrixStatus(JsonObject data) => $"{data.S("combination_mode")} · {data.Array("combinations").Count} righe · ogni colonna azione contiene il prodotto γ×ψ. «q var. mensola» non spuntato: il sovraccarico variabile resta nella spinta ed è assente sulla mensola (righe «b»). Modificare una cella passa a Personalizzate. Genera ripristina il preset NTC.";

        /// <summary>«Genera / ripristina automatiche»: the combinations of the preset, with the current signature. Throws ArgumentException with the reason.</summary>
        public static void GenerateMatrix(JsonObject data)
        {
            data["combinations"] = WallModel.GenerateCombinations(data); data["combination_mode"] = "Automatiche"; data["combination_signature"] = WallModel.CombinationSignature(data);
        }

        /// <summary>«Conferma matrice»: the matrix becomes custom with the current signature, then the actions are checked (ArgumentException with the reason).</summary>
        public static void ConfirmMatrix(JsonObject data)
        {
            data["combination_mode"] = "Personalizzate"; data["combination_signature"] = WallModel.CombinationSignature(data);
            WallModel.ValidateActions(data);
        }

        /// <summary>«Copia riga»: a custom copy of the combination at the end of the matrix.</summary>
        public static void CopyCombination(JsonObject data, int index)
        {
            var copy = (JsonObject)data.Array("combinations")[index]!.DeepClone(); copy["name"] = copy.S("name") + " copia " + (data.Array("combinations").Count + 1); copy["approach"] = "Personalizzato";
            data.Array("combinations").Add(copy); data["combination_mode"] = "Personalizzate";
        }

        /// <summary>«Elimina riga».</summary>
        public static void RemoveCombination(JsonObject data, int index)
        {
            data.Array("combinations").RemoveAt(index); data["combination_mode"] = "Personalizzate";
        }

        // ---------------------------------------------------------------- accelerograms

        /// <summary>A new accelerogram, without samples.</summary>
        public static JsonObject NewHistory(int count) => J.Obj(("enabled", true), ("name", "Storia " + (count + 1)), ("state", "SLD"), ("yield_g", ""), ("scale", 1), ("limit_mm", 20),
            ("compatible", false), ("samples", new JsonArray()));

        /// <summary>The samples of an accelerogram, under its form.</summary>
        public static string HistorySamples(JsonObject record) => $"{record.Array("samples").Count} campioni salvati · tempo [s], accelerazione verso valle [g]";

        /// <summary>A new deformable layer under the foundation.</summary>
        public static JsonObject NewServiceLayer() => J.Obj(("name", "Strato deformabile"), ("thickness", 2), ("modulus", ""));
    }
}
