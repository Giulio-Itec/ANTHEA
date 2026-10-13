using System.Text.Json.Nodes;

namespace X.Core;

public static partial class ProjectSharedData
{
    private const string BridgePrefix = "Ponte · ";
    private static void ExpandBridgeKeys(HashSet<string> keys)
    {
        // An activation also carries the dormant values selected by the user.
        foreach (var group in new[] {
            new[] { "plate2", "b_bottom2", "t_bottom2" },
            new[] { "predalle", "h_predalle", "rif_ferri_inf", "cover_bottom" },
            new[] { "rebars_top", "d_top", "pitch_top", "cover_top" },
            new[] { "rebars_bottom", "d_bottom", "pitch_bottom", "cover_bottom" },
            new[] { "fy_override", "fy" } })
            if (keys.Contains(BridgePrefix + group[0])) keys.UnionWith(group.Select(k => BridgePrefix + k));
    }
    private static void AddBridgeFields(string module, JsonObject data, Dictionary<string, Field> fields)
    {
        if (module != BridgeSection.Module) return;
        var defaults = BridgeSection.Defaults();
        var migrated = (JsonObject)data.DeepClone(); BridgeSection.EnsureAccessoryDefaults(migrated);
        void Add(string group, params string[] keys)
        {
            foreach (string key in keys)
                fields[BridgePrefix + key] = new(BridgePrefix + key, group, key, (data[key] ?? migrated[key] ?? defaults[key])?.DeepClone());
        }
        // The bridge catalog is distinct from ordinary RC/custom material laws: do not infer conversions.
        Add("Materiali", "classe_cls", "acciaio", "armatura", "fy_override", "fy");
        Add("Geometria", "b_cls", "h_cls", "h_trave", "t_web", "b_top", "t_top", "b_bottom", "t_bottom",
            "plate2", "b_bottom2", "t_bottom2", "predalle", "h_predalle", "rif_ferri_inf");
        Add("Armatura", "rebars_top", "d_top", "pitch_top", "cover_top", "rebars_bottom", "d_bottom", "pitch_bottom", "cover_bottom");
    }
    private static bool ActiveBridgeField(JsonObject sheet, string key)
    {
        var d = sheet["dati"];
        return key[BridgePrefix.Length..] switch
        {
            "fy" => d.B("fy_override"),
            "h_predalle" => d.B("predalle"),
            "rif_ferri_inf" => d.B("predalle") && d.B("rebars_bottom", true),
            "b_bottom2" or "t_bottom2" => d.B("plate2"),
            "d_top" or "pitch_top" or "cover_top" => d.B("rebars_top", true),
            "d_bottom" or "pitch_bottom" or "cover_bottom" => d.B("rebars_bottom", true),
            _ => true
        };
    }
    internal static string BridgeFieldLabel(string key) => "Ponte · " + (key[BridgePrefix.Length..] switch
    {
        "classe_cls" => "Classe del calcestruzzo",
        "acciaio" => "Acciaio della carpenteria",
        "armatura" => "Acciaio delle armature",
        "fy_override" => "Sovrascrivi fy",
        "fy" => "fy assegnato [MPa]",
        "b_cls" => "Larghezza soletta [mm]",
        "h_cls" => "Spessore totale soletta [mm]",
        "predalle" => "Predalle presente · solo geometria",
        "h_predalle" => "Spessore predalle [mm]",
        "rif_ferri_inf" => "Riferimento ferri inferiori",
        "h_trave" => "Altezza totale H trave [mm]",
        "t_web" => "Spessore anima [mm]",
        "b_top" => "Larghezza piattabanda superiore [mm]",
        "t_top" => "Spessore piattabanda superiore [mm]",
        "b_bottom" => "Larghezza piattabanda inferiore [mm]",
        "t_bottom" => "Spessore piattabanda inferiore [mm]",
        "plate2" => "Seconda piattabanda inferiore",
        "b_bottom2" => "Larghezza seconda piattabanda [mm]",
        "t_bottom2" => "Spessore seconda piattabanda [mm]",
        "rebars_top" => "Armatura superiore presente",
        "rebars_bottom" => "Armatura inferiore presente",
        "d_top" => "Diametro barre superiori [mm]",
        "d_bottom" => "Diametro barre inferiori [mm]",
        "pitch_top" => "Passo barre superiori [mm]",
        "pitch_bottom" => "Passo barre inferiori [mm]",
        "cover_top" => "Estradosso soletta → asse barre [mm]",
        "cover_bottom" => "Riferimento inferiore → asse barre [mm]",
        _ => key[BridgePrefix.Length..].Replace('_', ' ')
    });
}
