using System.Text.Json.Nodes;
namespace Anthea.Application.Presentation;

/// <summary>Existing preparation shared by the interactive sheet and its autonomous report.</summary>
public static class HorizontalSheetPresentation
{
    private static readonly Dictionary<string, double> ConcreteClasses = ConcreteMaterialCatalog.MaterialSheetClasses().ToDictionary(m => m.Name, m => m.Fck);
    public static string ConcreteClass(double strength) => ConcreteClasses.FirstOrDefault(p => p.Value == strength).Key ?? "Personalizzato";
    public static void PrepareGeneral(JsonObject data)
    {
        PaloOrizzontale.ValidateShape(data); ElasticHorizontalPile.PrepareShared(data);
        foreach (var (key, value) in data.S("tipo_sezione") == "CHS" ? new JsonObject() : SezioneCA.DefaultInput())
            if (!data["sezione"]!.AsObject().ContainsKey(key)) data["sezione"]![key] = value?.DeepClone();
        var g = data["generali"]!.AsObject();
        if (!g.ContainsKey("metodo_calcolo")) g["metodo_calcolo"] = PaloOrizzontale.BromsMethod;
        g["metodo_calcolo"] = PaloOrizzontale.NormalizeMethod(g.S("metodo_calcolo"));
        g["modalita"] = "Automatica";
    }
    public static void PrepareFactors(JsonObject coefficients)
    {
        if (!coefficients.ContainsKey("verticali_indagate")) coefficients["verticali_indagate"] = "1";
        foreach (var (key, value) in new Dictionary<string, string> { ["efficienza_metodo"] = "Manuale", ["efficienza_eta"] = "1",
            ["interasse_anteriore"] = "", ["interasse_posteriore"] = "", ["interasse_sinistro"] = "", ["interasse_destro"] = "" })
            if (!coefficients.ContainsKey(key)) coefficients[key] = value;
    }
    public static void PrepareSection(JsonObject data)
    {
        var sectionData = data["sezione"]!.AsObject(); var g = data["generali"]!.AsObject();
        // Preserve the previous perfect-plastic material unless a shared material already supplies its full law.
        RebarMaterial.CompleteLegacyInput(sectionData);
        if (!sectionData.ContainsKey("cls_diagramma")) sectionData["cls_diagramma"] = "Parabola-rettangolo";
        sectionData["shape"] = "Circolare";
        sectionData["diameter_mm"] = g.D("diametro", 1) * 1000;
        sectionData["classe_cls"] = ConcreteClass(sectionData.D("fck_mpa"));
        if(sectionData["coefficienti_unitari"]==null)sectionData["coefficienti_unitari"]=false;
    }
    public static JsonObject PrepareElasticView(JsonObject data)
    {
        var e = data["elastico"]!.AsObject();
        if (e["visualizzazione"] is not JsonObject) e["visualizzazione"] = new JsonObject();
        return e["visualizzazione"]!.AsObject();
    }
    public static void PrepareReport(JsonObject data)
    {
        PrepareGeneral(data); PrepareFactors(data["verifica"]!.AsObject());
        if (data.S("tipo_sezione") != "CHS") PrepareSection(data);
        ElasticHorizontalPile.PrepareShared(data);
        if (!ElasticHorizontalPile.NeedsMigration(data))
        {
            ElasticHorizontalPile.PrepareReinforcement(data);
            PrepareElasticView(data);
        }
    }
}
