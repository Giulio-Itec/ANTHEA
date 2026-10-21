using System.Globalization;
using System.Text.Json.Nodes;
namespace Anthea.Application.Presentation;

public static class BridgeSheetPresentation
{
    public static void PrepareCalculationOptions(JsonObject data)
    {
        data["metodo_analisi"] ??= BridgeSection.CalculationMethods[0];
        data["viscosita_nl"] ??= BridgeSection.NonlinearCreepModes[0];
        data["cls_teso_nl"] ??= BridgeSection.ConcreteTensionModes[0]; // PON-2: the sheets saved before have «Trascurato»
        data["fibre_anima"] ??= 160; data["fibre_flange"] ??= 8; data["fibre_cls"] ??= 64; data["sottopassi"] ??= 8;
    }
    public static void PreparePhase(JsonObject phase)
    {
        phase["riferimento_N"] ??= BridgeSection.CommonLoadReference;
        phase["V"] ??= 0; phase["T"] ??= 0; phase["epsilon_cs"] ??= 0; phase["q_conn"] ??= 0;
    }
    /// <summary>A phase changed kind: a shrinkage starts with ψL 0,55 and φ given (F5.11, moved from the historic view).</summary>
    public static void PrepareKind(JsonObject phase)
    {
        if (phase.S("tipo") == BridgeSection.ShrinkageKind) { phase["psi"] = .55; phase["modo"] = "Da φ"; }
    }
    /// <summary>φ, ψL or n of <paramref name="target"/> (a phase or the slab properties) edited: n edited is kept, otherwise φ.</summary>
    public static void HomogenizationEdited(JsonObject target, string key) => target["modo"] = key == "n" ? "Da n" : "Da φ";
    /// <summary>A cell of the table of the actions edited: the homogenization of the phase and the start of a shrinkage.</summary>
    public static void CellEdited(JsonObject phase, string key)
    {
        if (key is "phi" or "psi" or "n") HomogenizationEdited(phase, key);
        if (key == "tipo") PrepareKind(phase);
    }
    /// <summary>
    /// The situation shown after a calculation: the last when the sheet follows the last phase, otherwise the previous one when it still
    /// exists, else the last.
    /// </summary>
    public static int PublishedSituation(bool followLatest, int previous, int count) =>
        followLatest ? count - 1 : previous >= 0 && previous < count ? previous : count - 1;
    public static bool PhaseHomogenizationNeeded(JsonObject data, string kind) => BridgeSection.HasConcrete(kind)
        || kind == "Soletta esclusa" && data.B("pioli") && BridgeSection.RequiredStandard(data) != GPC.Checkers.CompositeBridge.BridgeStandard.Ntc2018;
    public static (double N0, double N, double PhiEffective, double Phi) UpdateHomogenization(JsonObject data, JsonObject phase, bool requireFinite = true)
    {
        var h = BridgeSection.Homogenization(data, phase);
        string derived = phase.S("modo") == "Da n" ? "phi" : "n";
        double value = derived == "phi" ? h.Phi : h.N;
        if (requireFinite && (!double.IsFinite(value) || !double.IsFinite(h.PhiEffective))) throw new ArgumentException("Omogeneizzazione fuori intervallo.");
        phase[derived] = value.ToString("R", CultureInfo.InvariantCulture);
        return h;
    }
    public static double UpdateWebHeight(JsonObject data)
    {
        double clear = BridgeSection.ClearWebHeight(data);
        data["h_web"] = clear.ToString("G17", CultureInfo.InvariantCulture);
        return clear;
    }
    public static JsonObject PrepareResponse(JsonObject data)
    {
        if (data["curve_sezione"] is not JsonObject) data["curve_sezione"] = BridgeSection.ResponseDefaults();
        var response = data["curve_sezione"]!.AsObject();
        foreach (var pair in BridgeSection.ResponseDefaults()) if (!response.ContainsKey(pair.Key)) response[pair.Key] = pair.Value?.DeepClone();
        return response;
    }
    public static void MigrateViewSettings(JsonObject viewSettings)
    {
        MigrateLegacyViewSettings(viewSettings);
        if (viewSettings.D("layout_version") < 4)
        {
            foreach (string key in new[] { "risultato_0", "risultato_1" })
                if (viewSettings.ContainsKey(key)) viewSettings[key] = new[] { 3, 1, 2 }[Math.Clamp((int)viewSettings.D(key), 0, 2)];
            viewSettings["layout_version"] = 4;
        }
    }
    private static void MigrateLegacyViewSettings(JsonObject viewSettings)
    {
        if (viewSettings.D("layout_version") >= 3) return;
        if (!viewSettings.ContainsKey("tab") && !viewSettings.ContainsKey("layout_version"))
        { viewSettings["layout_version"] = 3; return; }
        if (viewSettings.D("layout_version") < 2)
        {
            var previous = (JsonObject)viewSettings.DeepClone();
            int selected = Math.Clamp((int)previous.D("tab"), 0, 3);
            // The two former phase pages share one page now; retain the active one's view.
            int[] sources = [0, selected == 2 ? 2 : 1, 3];
            int[] resultMap = [0, 1, 2, 1, 2, 2];
            for (int page = 0; page < sources.Length; page++)
            {
                int oldPage = sources[page];
                if (previous["vista_" + oldPage] is { } view) viewSettings["vista_" + page] = view.DeepClone();
                else viewSettings.Remove("vista_" + page);
                int result = Math.Clamp((int)previous.D("risultato_" + oldPage, new[] { 4, 3, 1, 0 }[oldPage]), 0, 5);
                viewSettings["risultato_" + page] = resultMap[result];
            }
            viewSettings.Remove("vista_3"); viewSettings.Remove("risultato_3");
            viewSettings["tab"] = new[] { 0, 1, 1, 2 }[selected];
            viewSettings["layout_version"] = 2;
        }
        // Merge the former phase and analysis pages, retaining the active page's view.
        int active = Math.Clamp((int)viewSettings.D("tab"), 0, 2);
        if (active == 2)
        {
            viewSettings["vista_1"] = (int)viewSettings.D("vista_2", 0);
            viewSettings["risultato_1"] = (int)viewSettings.D("risultato_2", 0);
        }
        viewSettings.Remove("vista_2"); viewSettings.Remove("risultato_2");
        viewSettings["tab"] = Math.Min(active, 1);
        viewSettings["layout_version"] = 3;
    }
    public static JsonObject SlabOptions(JsonObject settings)
    {
        settings["soletta_proprieta"] ??= J.Obj(("phi", "0"), ("psi", "1"), ("n", "6"), ("modo", "Da φ"));
        return settings["soletta_proprieta"]!.AsObject();
    }
    public static void SaveView(JsonObject settings, int page, int display, int result, bool labels, bool rebarLabels, int phase, bool followLatest)
    {
        if (page < 0 || page == 2) return;
        settings["vista_" + page] = display; settings["risultato_" + page] = result;
        settings["quote_" + page] = labels; settings["armature_" + page] = rebarLabels;
        if (phase >= 0) settings["fase"] = phase;
        settings["segui_ultima_fase"] = followLatest;
    }
    public static void PrepareReport(JsonObject data)
    {
        BridgeSection.ValidateShape(data); BridgeSection.EnsureAccessoryDefaults(data); PrepareCalculationOptions(data);
        foreach (var phase in data.Array("fasi").OfType<JsonObject>())
        {
            PreparePhase(phase);
            if (PhaseHomogenizationNeeded(data, phase.S("tipo")))
                try { UpdateHomogenization(data, phase); } catch (ArgumentException) { }
        }
        try { UpdateWebHeight(data); } catch (ArgumentException) { }
        var response = PrepareResponse(data);
        if (data["ui_mista"] is not JsonObject) data["ui_mista"] = new JsonObject();
        var settings = data["ui_mista"]!.AsObject(); bool follow = settings.B("segui_ultima_fase", !settings.ContainsKey("fase"));
        MigrateViewSettings(settings);
        try { UpdateHomogenization(data, SlabOptions(settings), false); } catch (Exception) { }
        int detail = (int)settings.D("dettaglio_sezione"); settings["dettaglio_sezione"] = detail is >= 0 and < 2 ? detail : -1;
        int page = Math.Clamp((int)settings.D("tab"), 0, 2); settings["tab"] = page;
        if (page == 2)
            response["fase"] = Math.Clamp((int)response.D("fase"), 0, Math.Max(0, data.Array("fasi").Count(p => p.B("attiva")) - 1));
        else SaveView(settings, page, page == 0 ? 2 : Math.Clamp((int)settings.D("vista_1"), 0, 2),
            Math.Clamp((int)settings.D("risultato_" + page, page == 0 ? 2 : 0), 0, 4), settings.B("quote_" + page, page == 0), settings.B("armature_" + page, page == 0), -1, follow);
    }
    public static void ApplyReportResult(JsonObject data, BridgeResult result)
    {
        var settings = data["ui_mista"]!.AsObject();
        if (settings.D("tab") == 2) return;
        int previous = (int)settings.D("fase", -1);
        settings["fase"] = settings.B("segui_ultima_fase", !settings.ContainsKey("fase")) ? result.Stages.Count - 1
            : previous >= 0 && previous < result.Stages.Count ? previous : result.Stages.Count - 1;
    }
    public static (double Factor, string Source) StressScale(JsonObject settings) => StressScale(null, settings);
    public static (double Factor, string Source) StressScale(BridgeResult? result, JsonObject settings)
    {
        var all = result?.Stages.LastOrDefault()?.Contributions;
        var phase = all?.LastOrDefault(c => c.Kind == "Composta") ?? all?.LastOrDefault(c => c.HasConcrete);
        if (settings.B("amplificazione_cls_auto", true))
            return phase is not null ? (phase.HomogenizationN, "n · " + phase.Name) : (1, "nessuna fase composta");
        double manual = settings.D("amplificazione_cls", 1);
        return (double.IsFinite(manual) && manual is >= .01 and <= 1000 ? manual : 1, "manuale");
    }
}
