using System.Text.Json.Nodes;
namespace Anthea.Application.Concrete;

/// <summary>Existing lazy defaults and presentation values, shared with the sheet. No independent engineering algorithm.</summary>
public static class ConcreteSheetPreparation
{
    public static void PrepareHoles(JsonObject Input)
    {
        foreach(var(k,v) in new[]{("inner_diameter_mm","500"),("inner_width_mm","300"),("inner_height_mm","400")})if(!Input.ContainsKey(k))Input[k]=v;
    }
    public static void PrepareFlange(JsonObject Input)
    {
        foreach (var (key, value) in new[] { ("flange_bottom_count", "0"), ("flange_bottom_diameter_mm", Input.S("top_bar_diameter_mm")), ("flange_bottom_offset_mm", (Input.D("cover_mm") + Input.D("transverse_bar_diameter_mm") + Input.D("top_bar_diameter_mm") / 2).ToString(System.Globalization.CultureInfo.InvariantCulture)) }) if (!Input.ContainsKey(key)) Input[key] = value;
    }
    public static void PrepareMaterials(JsonObject Input)
    {
        // Never replace saved numerical properties merely by opening an existing sheet.
        if (!Input.ContainsKey("classe_acciaio")) Input["classe_acciaio"] = "Personalizzato";
        if (!Input.ContainsKey("steel_fu_mpa")) Input["steel_fu_mpa"] = Input["fyk_mpa"]?.DeepClone();
        if (!Input.ContainsKey("steel_eps_u")) Input["steel_eps_u"] = "100";
        if (!Input.ContainsKey("steel_diagramma")) Input["steel_diagramma"] = "Elastoplastico";
        if (!Input.ContainsKey("cls_diagramma")) Input["cls_diagramma"] = ConcreteMaterials.ConcreteDiagrams[0];
    }
    public static void PrepareQuickResistance(JsonObject settings)
    {
        if (settings["resistenze_rapide"] is not JsonObject)
            settings["resistenze_rapide"] = J.Obj(("N", "0"), ("tipo", "Plastico"));
    }
    public static void PrepareLayer(JsonObject Input, string prefix, string source)
    {
            foreach (var (suffix, value) in new[] { ("count", Input.S(source + "_bar_count")), ("diameter", Input.S(source + "_bar_diameter_mm")), ("gap", "30") })
                if (!Input.ContainsKey(prefix + "_" + suffix)) Input[prefix + "_" + suffix] = value;
    }

    public sealed record HomogenizationUpdate(string NKey, string PhiKey, string Source, string Target, double? Value, double? Modulus = null, string? Error = null);
    public static IReadOnlyList<HomogenizationUpdate> SynchronizeHomogenization(JsonObject Input, JsonObject settings, string key, string? changed = null)
    {
        var updates = new List<HomogenizationUpdate>(); var options = settings["sle"]![key]!;
        foreach (var (nKey, phiKey) in new[] { ("n_armature", "phi"), ("n_trefoli", "phi_trefoli") })
        {
            bool fromN = changed == nKey || changed != phiKey && options.S(phiKey) == "" && options.S(nKey) != "";
            string source = fromN ? nKey : phiKey, target = fromN ? phiKey : nKey;
            try
            {
                double ec = ConcreteMaterials.Concrete(Input).E;
                double modulus = nKey == "n_armature" ? Input.Required("steel_modulus_mpa", strict: true)
                    : settings.Array("trefoli").FirstOrDefault()?.D("Ep", 195000) ?? settings["materiale_trefolo"].D("Ep", 195000);
                var ratio = Homogenization.Resolve(modulus, ec, fromN, SectionWorkspace.Number(options.S(source, "0"), source));
                double value = fromN ? ratio.Phi : ratio.N;
                options[target] = value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
                updates.Add(new(nKey, phiKey, source, target, value, nKey == "n_trefoli" ? modulus : null));
            }
            catch (ArgumentException error)
            {
                options[target] = "";
                updates.Add(new(nKey, phiKey, source, target, null, Error: error.Message));
            }
        }
        return updates;
    }
    public static string[] Contours { get; } = ["CLS · gradiente tensioni", "CLS · bande tensioni", "CLS · scala resistenza", "Barre · tensioni", "Barre · tasso resistenza", "Sezione · tasso resistenza", "CLS · deformazioni", "Solo geometria"];
    public static void PrepareReport(JsonObject data)
    {
        var settings = SectionWorkspace.Prepare(data); var input = data["input"]!.AsObject();
        PrepareHoles(input); PrepareMaterials(input); PrepareFlange(input);
        if (settings["materiale_trefolo"] is not JsonObject)
            settings["materiale_trefolo"] = ConcreteMaterialCatalog.Steel(true, settings.S("normativa")).First(m => m.S("nome") == "Y1860").DeepClone();
        if (settings["materiali_custom"] is not JsonArray) settings["materiali_custom"] = new JsonArray();
        foreach (var (key, source) in new[] { ("top", "top"), ("bottom", "bottom"), ("inner", "longitudinal") }) PrepareLayer(input, "second_" + key, source);
        if (settings["layout"] is not JsonObject) settings["layout"] = new JsonObject();
        PrepareQuickResistance(settings);
        foreach (string key in SectionWorkspace.Sets.Skip(2))
        {
            SynchronizeHomogenization(input, settings, key);
            var o = settings["sle"]![key]!;
            if (!Contours.Contains(o.S("contour"))) o["contour"] = Contours[0];
        }
        RefreshAutomaticShear(input, settings["taglio"]!.AsObject());
        ConcreteReinforcementDesign.Prepare(settings);
        if (input.S("shape") is not ("Rettangolare" or "Circolare")) input["foro_presente"] = false;
        foreach (string key in SectionWorkspace.Sets.Skip(2)) SynchronizeHomogenization(input, settings, key);
        foreach (string field in SectionWorkspace.SharedSleFields) settings["sle_comuni"]![field] = settings["sle"]!["SLE"]![field]?.DeepClone();
        RefreshAutomaticShear(input, settings["taglio"]!.AsObject());
        ConcreteDetailingPresentation.Calculate(input, settings, ReadActions(data)["SLU"].Concat(ReadActions(data)["SLV"])).ApplyDerivedValues(settings);
        CommitRows(data);
    }
    public static ConcreteGeometry UpdateAutomaticShear(JsonObject input, JsonObject options)
        => new(ConcreteCalculationSettings.UpdateAutomaticShear(input, options));

    public static ConcreteGeometry? RefreshAutomaticShear(JsonObject input, JsonObject options)
    {
        if (options.S("parametri", "Automatici da sezione") != "Automatici da sezione") return null;
        try { return UpdateAutomaticShear(input, options); }
        catch (ArgumentException)
        {
            foreach (string axis in new[] { "x", "y" }) foreach (string field in new[] { "bw_", "d_", "asl_" }) options[field + axis] = "";
            return null;
        }
    }
    public static Dictionary<string, JsonObject[]> ReadActions(JsonObject data) => SectionWorkspace.Sets.ToDictionary(key => key,
        key => data["combinazioni"]!.Array(key).OfType<JsonObject>().Select(item =>
        {
            var a = item.Array("azioni");
            return J.Obj(("id", item.S("id")), ("nome", item.S("nome")), ("N", a.ElementAtOrDefault(0)?.ToString() ?? ""),
                ("Mx", a.ElementAtOrDefault(1)?.ToString() ?? ""), ("My", a.ElementAtOrDefault(2)?.ToString() ?? ""), ("visible", item.B("visible", true)));
        }).ToArray());
    public static void CommitRows(JsonObject data)
    {
        foreach (var (key, rows) in ReadActions(data)) data["combinazioni"]![key] = StoreActions(rows);
        var options = data["workspace_ca"]!["taglio"]!;
        options["azioni"] = StoreShearRows(options.Array("azioni").OfType<JsonObject>());
    }
    public static JsonArray StoreActions(IEnumerable<JsonObject> rows) => new(rows.Select(row => (JsonNode)J.Obj(("id", row.S("id")), ("nome", row.S("nome")), ("azioni", new[] { row.S("N"), row.S("Mx"), row.S("My") }), ("visible", row.B("visible", true)))).ToArray());
    public static JsonArray StoreShearRows(IEnumerable<JsonObject> rows) => new(rows.Select(row => (JsonNode)J.Obj(("id",row.S("id")),("nome",row.S("nome")),("N",row.S("N")),("Mx",row.S("Mx","0")),("My",row.S("My","0")),("Vx",row.S("Vx")),("Vy",row.S("Vy")),("T",row.S("T","0")))).ToArray());
}
