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
            new[] { "rebars_top", "d_top", "pitch_top", "cover_top" },
            new[] { "rebars_bottom", "d_bottom", "pitch_bottom", "cover_bottom" },
            new[] { "fy_override", "fy" } })
            if (keys.Contains(BridgePrefix + group[0])) keys.UnionWith(group.Select(k => BridgePrefix + k));
    }
    private static void AddBridgeFields(string module, JsonObject data, Dictionary<string, Field> fields)
    {
        if (module != BridgeSection.Module) return;
        var defaults = BridgeSection.Defaults();
        void Add(string group, params string[] keys)
        {
            foreach (string key in keys)
                fields[BridgePrefix + key] = new(BridgePrefix + key, group, key, (data[key] ?? defaults[key])?.DeepClone());
        }
        // The bridge catalog is distinct from ordinary RC/custom material laws: do not infer conversions.
        Add("Materiali", "classe_cls", "acciaio", "armatura", "fy_override", "fy");
        Add("Geometria", "b_cls", "h_cls", "h_web", "t_web", "b_top", "t_top", "b_bottom", "t_bottom",
            "plate2", "b_bottom2", "t_bottom2");
        Add("Armatura", "rebars_top", "d_top", "pitch_top", "cover_top", "rebars_bottom", "d_bottom", "pitch_bottom", "cover_bottom");
    }
    private static bool ActiveBridgeField(JsonObject sheet, string key)
    {
        var d = sheet["dati"];
        return key[BridgePrefix.Length..] switch
        {
            "fy" => d.B("fy_override"),
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
        "h_cls" => "Spessore soletta [mm]",
        "h_web" => "Altezza anima [mm]",
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
        "cover_bottom" => "Intradosso soletta → asse barre [mm]",
        _ => key[BridgePrefix.Length..].Replace('_', ' ')
    });
}
