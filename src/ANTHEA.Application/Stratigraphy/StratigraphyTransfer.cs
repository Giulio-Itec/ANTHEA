using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Application.Stratigraphy;

/// <summary>Explicit data adaptation only: no soil correlations or design coefficients.</summary>
public static class StratigraphyTransfer
{
    private static readonly (string Document, string Legacy)[] Keys =
    [ ("gamma", "peso_specifico"), ("gamma_sat", "peso_specifico_saturo"),
      ("phi", "angolo_attrito"), ("c", "coesione_efficace"), ("cu", "coesione_non_drenata") ];

    public static JsonObject ToLegacy(JsonObject packet, string scenario)
    {
        SoilProfileTransfer.Validate(packet);
        if (scenario is not ("Medi" or "Minimi")) throw new ArgumentException("Scegliere Medi o Minimi per il foglio di verifica; per entrambi creare due fogli distinti.");
        var source = packet["stratigraphy"]!.AsObject();
        if (source.S("water_state") == "Non nota") throw new ArgumentException("Definire la falda prima del trasferimento al modulo di verifica.");
        var selected = source.DeepClone().AsObject(); selected["scenario"] = scenario;
        // The portable legacy contract requires gamma and phi. Other missing values remain missing.
        var data = StratigraphyDocument.ForVerification(selected, "gamma", "phi")[scenario];
        var rows = new JsonArray();
        foreach (var layer in StratigraphyDocument.Layers(data).OfType<JsonObject>())
        {
            if (StratigraphyDocument.Infinite(layer)) throw new ArgumentException("Il modulo di destinazione richiede spessori finiti: definire l’estensione dell’ultimo strato in una copia della stratigrafia.");
            if (layer.S("behavior") is not ("Granulare" or "Coesivo")) throw new ArgumentException(layer.S("name") + ": scegliere il comportamento Granulare o Coesivo; la litologia non lo determina automaticamente.");
            var row = new JsonObject { ["name"] = layer["name"]?.DeepClone(),
                ["spessore"] = StratigraphyDocument.Number(layer["thickness"], "Spessore"), ["tipologia"] = layer.S("behavior") };
            foreach (var key in Keys) row[key.Legacy] = StratigraphyDocument.Number(layer[key.Document], key.Document);
            rows.Add(row);
        }
        var result = new JsonObject { ["formato"] = "ANTHEA.Terreno", ["versione"] = 1,
            ["source_module"] = StratigraphyDocument.Module, ["source_survey"] = 1,
            ["datum"] = "z=0: piano campagna della stratigrafia; nessuna traslazione di quota",
            ["layers"] = rows, ["water"] = new JsonObject { ["enabled"] = data.S("water_state") == "Assegnata",
                ["depth"] = StratigraphyDocument.Number(data["water_depth"], "Falda") } };
        SoilProfileTransfer.Validate(result); return result;
    }

    public static JsonObject ToDocument(JsonObject packet)
    {
        SoilProfileTransfer.Validate(packet);
        JsonObject data;
        if (packet["stratigraphy"] is JsonObject full) data = full.DeepClone().AsObject();
        else
        {
            data = StratigraphyDocument.Create();
            data["water_state"] = packet["water"].B("enabled") ? "Assegnata" : "Non considerata";
            data["water_depth"] = packet["water"]?["depth"]?.DeepClone();
            var rows = new JsonArray();
            foreach (var row in packet.Array("layers").OfType<JsonObject>())
            {
                var layer = StratigraphyDocument.NewLayer();
                layer["name"] = string.IsNullOrWhiteSpace(row.S("name")) ? "Strato " + (rows.Count + 1) : row.S("name");
                layer["thickness"] = row["spessore"]?.DeepClone(); layer["behavior"] = row.S("tipologia");
                foreach (var key in Keys) layer[key.Document] = row[key.Legacy]?.DeepClone();
                layer["source"] = "Foglio ANTHEA: " + packet.S("source_sheet", packet.S("source_module"));
                layer["notes"] = "Parametri importati dall’insieme unico del modulo: verificare la qualificazione come valori medi. Nessun minimo ricostruito.";
                rows.Add(layer);
            }
            data["layers"] = rows;
        }
        data["id"] = Guid.NewGuid().ToString("N");
        foreach (var layer in StratigraphyDocument.Layers(data).OfType<JsonObject>()) layer["id"] = Guid.NewGuid().ToString("N");
        data["soil_transfer"] = new JsonObject { ["source_sheet"] = packet["source_sheet"]?.DeepClone(),
            ["source_module"] = packet["source_module"]?.DeepClone(), ["source_id"] = packet["source_id"]?.DeepClone() };
        StratigraphyDocument.Validate(data); return data;
    }
}
