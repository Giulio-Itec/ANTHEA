using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>Read-only report projection. Only compatible, equal inputs in related scopes merge.</summary>
public sealed class ProjectReportPlan
{
    public sealed record CommonValue(JsonObject Section, JsonObject Source, ProjectSharedData.Field Field, JsonObject[] Sheets);
    public sealed record InputValue(string Group, string Label, JsonNode? Value);
    public JsonObject Root { get; }
    public JsonObject[] Sheets { get; }
    public List<CommonValue> Common { get; } = [];
    public Dictionary<JsonObject, HashSet<string>> SharedKeys { get; } = [];
    public Dictionary<JsonObject, Dictionary<string, ProjectSharedData.Field>> Fields { get; }
    public List<ProjectSharedData.Difference> Conflicts { get; }

    public ProjectReportPlan(JsonObject section)
    {
        Root = section; Sheets = ProjectSharedData.SubtreeSheets(section).ToArray();
        var context = ProjectSharedData.ContextSheets(section).ToArray();
        Fields = context.ToDictionary(s => s, s => ProjectSharedData.Fields(s).Where(p => Relevant(s, p.Value)).ToDictionary());
        foreach (var sheet in Sheets) SharedKeys[sheet] = [];
        Conflicts = ProjectSharedData.Differences(section);
        var blocked = Conflicts.SelectMany(d => new[] { (d.First, d.Key), (d.Second, d.Key) }).ToHashSet();
        var edges = new Dictionary<(JsonObject, string), HashSet<JsonObject>>();
        foreach (var (a, b) in ProjectSharedData.ComparisonPairs(section))
            foreach (var (left, right) in ProjectSharedData.ComparableFields(a, b))
            {
                if (!Fields[a].ContainsKey(left.Key) || !Fields[b].ContainsKey(left.Key) || blocked.Contains((a, left.Key)) || blocked.Contains((b, left.Key)) ||
                    !ProjectSharedData.Equal(left.Value, right.Value)) continue;
                foreach (var (from, to) in new[] { (a, b), (b, a) })
                {
                    if (!edges.TryGetValue((from, left.Key), out var adjacent)) edges[(from, left.Key)] = adjacent = [];
                    adjacent.Add(to);
                }
            }
        var visited = new HashSet<(JsonObject, string)>();
        foreach (var sheet in context) foreach (var field in Fields[sheet].Values)
        {
            if (!edges.ContainsKey((sheet, field.Key)) || !visited.Add((sheet, field.Key))) continue;
            var members = new HashSet<JsonObject> { sheet }; var queue = new Queue<JsonObject>(); queue.Enqueue(sheet);
            while (queue.TryDequeue(out var next)) foreach (var other in edges[(next, field.Key)])
                if (members.Add(other)) { visited.Add((other, field.Key)); queue.Enqueue(other); }
            var included = Sheets.Where(members.Contains).ToArray(); if (included.Length == 0) continue;
            // Context order starts at the highest ancestor; the source value is equal in every member.
            var source = context.First(members.Contains); var owner = (JsonObject)source.Parent!.Parent!;
            var location = ProjectSharedData.Sections(section).Contains(owner) ? owner : section;
            Common.Add(new(location, source, Fields[source][field.Key], included));
            foreach (var member in included) SharedKeys[member].Add(field.Key);
        }
    }

    private static bool Relevant(JsonObject sheet, ProjectSharedData.Field field)
    {
        if (field.Value is null || field.Value is JsonArray { Count: 0 } || field.Value.ToString() == "") return false;
        var data = sheet["dati"]!; string key = field.Key;
        string shape = sheet.S("modulo_id") == "str_palo" ? data["input"].S("shape") : "Circolare";
        if (key == "diameter_mm" && shape != "Circolare" || key == "width_mm" && shape != "Rettangolare" ||
            key == "height_mm" && shape == "Circolare" || key is "flange_width_mm" or "web_width_mm" or "flange_thickness_mm" && shape != "A T") return false;
        if ((key.StartsWith("top_") || key.StartsWith("bottom_") || key.StartsWith("side_")) && shape == "Circolare") return false;
        if (key.StartsWith("flange_bottom_") && shape != "A T") return false;
        if (key.StartsWith("second_"))
        {
            string layer = key.Split('_')[1];
            var input = data[sheet.S("modulo_id") == "str_palo" ? "input" : "sezione"];
            if (!input.B("second_" + layer + "_enabled") || layer == "inner" && shape != "Circolare" || layer != "inner" && shape == "Circolare") return false;
        }
        if (key == "profondita_falda" && !data["generali"].B("presenza_falda")) return false;
        return true;
    }

    public IEnumerable<InputValue> LocalInputs(JsonObject sheet)
    {
        var all = ProjectSharedData.Fields(sheet); var consumed = all.Values.Select(f => Path(f)).ToHashSet();
        foreach (var field in Fields[sheet].Values.Where(f => !SharedKeys[sheet].Contains(f.Key)))
            yield return new(field.Group, Label(field.Key), field.Value);
        string module = sheet.S("modulo_id"); var data = sheet["dati"]!.AsObject();
        string[] roots = module == "str_palo" ? ["input"] : module == "mat_calcestruzzo" ? ["classe", "numeri", "scelte", "opzioni"] :
            module == RebarMaterial.Module ? ["input", "riferimento"] : ["generali", "sezione", "verifica", "efficienza", "stratigrafie"];
        foreach (string root in roots) foreach (var row in Walk(data[root], root))
        {
            if (consumed.Any(p => row.Path == p || row.Path.StartsWith(p + "/", StringComparison.Ordinal))) continue;
            if (row.Path.EndsWith("/shared_layer_id") || row.Path.EndsWith("/metodo_nq_precedente")) continue;
            string key = row.Path.Split('/').Last();
            if (module is "str_palo" or PaloOrizzontale.Module && row.Path.StartsWith(module == "str_palo" ? "input/" : "sezione/") &&
                key is "axial_force_kn" or "moment_x_knm" or "moment_y_knm" or "apply_minimum_eccentricity" or "minimum_eccentricity_mm") continue;
            if (module == PaloOrizzontale.Module && row.Path == "sezione/diameter_mm") continue;
            if (module.StartsWith("geo_") && row.Path == "generali/momento_resistente" && data["generali"].S("origine_momento") != "Manuale") continue;
            if (module == "geo_micropalo_verticale" && row.Path.StartsWith("generali/") && key is "tipo_palo" or "sottotipo_palo_battuto" or "metodo_nq" or "presenza_falda" or "profondita_falda" or "considera_sottospinta") continue;
            if (row.Path == "generali/sottotipo_palo_battuto" && data["generali"].S("tipo_palo") != "Battuto") continue;
            yield return new(root == "stratigrafie" ? "Terreno specifico del foglio" : "Dati specifici del foglio", PathLabel(row.Path), row.Value);
        }
    }
    private static string Path(ProjectSharedData.Field field)
    {
        if (!field.Key.StartsWith("Strato · ")) return field.Path;
        var parts = field.Key[9..].Split('/'); return $"stratigrafie/{int.Parse(parts[0]) - 1}/{int.Parse(parts[1]) - 1}/{parts[2]}";
    }
    public static IEnumerable<(string Path, JsonNode? Value)> Walk(JsonNode? node, string path)
    {
        if (node is JsonObject obj)
        { foreach (var (key, value) in obj) foreach (var item in Walk(value, path + "/" + key)) yield return item; }
        else if (node is JsonArray array)
        { for (int i = 0; i < array.Count; i++) foreach (var item in Walk(array[i], path + "/" + i)) yield return item; }
        else if (node is not null && node.ToString() != "") yield return (path, node);
    }
    public static string PathLabel(string path)
    {
        var parts = path.Split('/');
        if (parts[0] == "stratigrafie" && parts.Length == 4)
            return $"Sondaggio {int.Parse(parts[1]) + 1} · Strato {int.Parse(parts[2]) + 1} · {Label(parts[3])}";
        return string.Join(" · ", parts.Select(p => int.TryParse(p, out int i) ? (i + 1).ToString() : Label(p)));
    }
    public static string Label(string key) => key.StartsWith("Strato · ") ? ProjectSharedData.SoilFieldLabel(key) :
        key.StartsWith("Scheda CLS · ") ? ConcreteLabel(key.Split('/').Last()) :
        key.StartsWith("CHS · ") ? "CHS · " + Label(key[6..]) :
        key.StartsWith("Staffe · ") ? "Staffe · " + Label(key[9..]) : key switch
    {
        "esposizione" => "Classe di esposizione", "shape" => "Forma della sezione", "diameter_mm" => "Diametro [mm]",
        "width_mm" => "Larghezza [mm]", "height_mm" => "Altezza [mm]", "cover_mm" => "Copriferro netto [mm]",
        "longitudinal_bar_count" => "Numero barre longitudinali", "longitudinal_bar_diameter_mm" => "Diametro barre longitudinali [mm]",
        "transverse_bar_diameter_mm" => "Diametro staffe [mm]", "transverse_spacing_mm" => "Passo staffe [mm]",
        "barre_manuali" => "Disposizione manuale delle barre", "trefoli" => "Trefoli",
        "fyk_mpa" => "Snervamento acciaio fyk [MPa]", "gamma_c" => "Coefficiente γc", "gamma_s" => "Coefficiente γs", "alpha_cc" => "Coefficiente αcc",
        "classe_acciaio" => "Classe acciaio", "materiale_acciaio_nome" => "Nome acciaio", "steel_modulus_mpa" => "Modulo elastico Es [MPa]",
        "steel_fu_mpa" => "Resistenza fu [MPa]", "steel_eps_u" => "Deformazione ultima εu [‰]", "steel_diagramma" => "Diagramma acciaio",
        "diametro" => "Diametro [m]", "lunghezza" or "lunghezza_micropalo" => "Lunghezza [m]", "perforazione_mm" => "Diametro perforazione [mm]",
        "profondita_falda" => "Profondità falda [m]", "azione_orizzontale" => "Azione orizzontale [kN]", "azione_assiale" => "Azione assiale [kN]",
        "carico_compressione" => "Carico di compressione [kN]", "carico_trazione" => "Carico di trazione [kN]", "spessore" => "Spessore [m]",
        "peso_specifico" or "peso_specifico_saturo" => key.Replace('_', ' ') + " [kN/m³]", "angolo_attrito" => "Angolo di attrito [°]",
        "coesione_efficace" or "coesione_non_drenata" => key.Replace('_', ' ') + " [kPa]", "input" => "Sezione", "classe" => "Classe CLS",
        "eccentricita" => "Eccentricità [m]", "momento_resistente" => "Momento resistente [kNm]", "inclinazione" => "Inclinazione [°]",
        "presenza_falda" => "Presenza falda", "verticali_indagate" => "Verticali indagate", "cls_diagramma" => "Diagramma CLS",
        "gettato_sottile" => "Riduzione per getto sottile", "classe_cls" => "Classe CLS", "materiale_cls_nome" => "Nome CLS",
        "apply_pile_requirements" => "Requisiti specifici dei pali", "dissipative_zone" => "Zona dissipativa",
        "second_inner_count" => "Numero barre della corona interna", "second_inner_diameter" => "Diametro barre della corona interna [mm]", "second_inner_gap" => "Distanza della corona interna [mm]",
        "second_top_count" => "Numero barre superiori del secondo strato", "second_top_diameter" => "Diametro barre superiori del secondo strato [mm]", "second_top_gap" => "Distanza del secondo strato superiore [mm]",
        "second_bottom_count" => "Numero barre inferiori del secondo strato", "second_bottom_diameter" => "Diametro barre inferiori del secondo strato [mm]", "second_bottom_gap" => "Distanza del secondo strato inferiore [mm]",
        "second_inner_enabled" => "Corona interna attiva", "second_top_enabled" => "Secondo strato superiore attivo", "second_bottom_enabled" => "Secondo strato inferiore attivo",
        "fy_chs_mpa" => "Snervamento fy [MPa]", "gamma_m0" => "Coefficiente γM0", "diametro_chs_mm" => "Diametro esterno [mm]", "spessore_chs_mm" => "Spessore [mm]",
        "modo_chs" => "Modalità di definizione", "profilo_chs" => "Profilo tubolare", "schema_interno" => "Schema interno", "rotazione_staffa" => "Rotazione [°]",
        "peso_specifico_palo" => "Peso specifico del palo [kN/m³]", "azione_compressione" => "Azione di compressione [kN]", "azione_trazione" => "Azione di trazione [kN]",
        "pressione_iniezione" => "Pressione di iniezione [MPa]", "inizio_aderenza" => "Inizio aderenza [m]", "alpha" => "Coefficiente α", "nc" => "Coefficiente Nc",
        "passo" => "Passo dei diagrammi [m]", "generali" => "Dati generali", "sezione" => "Sezione",
        _ => key.Replace('_', ' ')
    };
    private static string ConcreteLabel(string key) => key switch
    {
        "diameter" => "Diametro della barra di riferimento [mm]", "aggregate" => "Dimensione massima aggregato [mm]",
        "bondAlpha" => "Coefficiente di trazione αct", "bondGamma" => "Coefficiente parziale γc per aderenza", "cementName" => "Designazione del cemento",
        "ntcElement" => "Tipo di elemento", "coverMethod" => "Criterio del copriferro", "life" => "Vita utile di progetto",
        "deviationControl" => "Controllo di esecuzione", "deviationValue" => "Tolleranza di posa Δcdev", "ground" => "Superficie di getto", "abrasion" => "Strato per abrasione",
        "bondCondition" => "Condizioni di aderenza", "consistency" => "Consistenza al getto", "cement" => "Famiglia del cemento", "cementClass" => "Classe del cemento",
        "cementEarly" => "Resistenza iniziale del cemento", "highStrength" => "Riduzione EC2 per classe di resistenza", "slab" => "Riduzione EC2 per geometria a piastra",
        "quality" => "Controllo speciale della produzione", "rough" => "Superficie irregolare", "ntcQuality" => "Controllo qualità dei copriferri", _ => Label(key)
    };
}
