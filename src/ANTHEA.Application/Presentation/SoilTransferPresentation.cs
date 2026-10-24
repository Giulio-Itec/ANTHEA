using System.Text.Json.Nodes;
using Anthea.Application.Stratigraphy;
using X.Core;

namespace Anthea.Application.Presentation;

/// <summary>
/// The preview of a soil profile in the soil transfer dialog «Terreno · invio e riutilizzo»: moved without changes in F5.6 from the
/// Refresh of the historic window (src/ANTHEA.Desktop/Wpf/SoilTransferDialog.cs), which calls it, so that the shared ViewModel
/// (ANTHEA.Presentation, Modules/Shared/SoilTransferDialogViewModel) shows the same table and the same description.
/// </summary>
public static class SoilTransferPresentation
{
    /// <summary>Columns of the preview of a stratigraphy document (medi and minimi of each layer).</summary>
    public static IReadOnlyList<string> StratigraphyColumns { get; } = ["Strato / insieme", "Famiglia", "Da z [m]", "A z [m]", "γ", "γsat", "φ′", "c′", "cu"];

    /// <summary>Columns of the preview of a profile of a checking module.</summary>
    public static IReadOnlyList<string> ProfileColumns { get; } = ["Strato", "Tipo", "Da z [m]", "A z [m]", "γ", "γsat", "φ′", "c′", "cu"];

    /// <summary>The preview of <paramref name="profile"/>: the rows of its table and the description under the commands.</summary>
    public static SoilTransferPreview Preview(JsonObject profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile["stratigraphy"] is JsonObject document)
        {
            var rows = new List<string[]>(); double top = 0;
            foreach (var layer in StratigraphyDocument.Layers(document).OfType<JsonObject>())
            {
                bool infinite = StratigraphyDocument.Infinite(layer);
                double bottom = top + (StratigraphyDocument.Number(layer["thickness"], "Spessore") ?? 0);
                string[] Values(JsonObject values, string set) => [layer.S("name") + " · " + set, layer.S("soil_type"), WallSheetPresentation.Number(top), infinite ? "Indefinito" : WallSheetPresentation.Number(bottom), values.S("gamma"), values.S("gamma_sat"), values.S("phi"), values.S("c"), values.S("cu")];
                rows.Add(Values(layer, "Medi"));
                if (layer["minimum"] is JsonObject minimum) rows.Add(Values(minimum, "Minimi"));
                top = bottom;
            }
            string description = "Stratigrafia completa: copia con medi, minimi, fonti e retini. Per i moduli di verifica scegliere un insieme; per entrambi inviare due fogli distinti. Falda e spessori devono essere definiti; nessuna sostituzione di dati mancanti.";
            description += "\nFalda: " + document.S("water_state") + (document.S("water_state") == "Assegnata" ? " a " + document.S("water_depth") + " m" : "") + ". Pesi in kN/m³; angoli in gradi; coesioni in kPa. Stesso piano di riferimento, senza traslazione di quota.";
            return new SoilTransferPreview(true, rows, description);
        }
        double depth = 0;
        var table = profile.Array("layers").Select((r, i) =>
        { double top = depth; depth += r.D("spessore"); return new[] { r.S("name", "Strato " + (i + 1)), r.S("tipologia"), WallSheetPresentation.Number(top), WallSheetPresentation.Number(depth), r.S("peso_specifico"), r.S("peso_specifico_saturo"), r.S("angolo_attrito"), r.S("coesione_efficace"), r.S("coesione_non_drenata") }; }).ToArray();
        string text = profile.S("datum") + $". Profondità indagata: {depth:0.##} m. " + (profile["water"].B("enabled") ? $"Falda a z={profile["water"].D("depth"):0.##} m." : "Falda assente.")
            + "\nPesi in kN/m³; angoli in gradi; coesioni in kPa. Le profondità vengono copiate senza traslazione: usare lo stesso riferimento geometrico nel foglio di destinazione.";
        return new SoilTransferPreview(false, table, text);
    }
}

/// <summary>The preview of a soil profile: a stratigraphy document or a profile, the rows of its table (nine cells each) and the description.</summary>
public sealed record SoilTransferPreview(bool Stratigraphy, IReadOnlyList<string[]> Rows, string Description)
{
    /// <summary>The columns of the table.</summary>
    public IReadOnlyList<string> Columns => Stratigraphy ? SoilTransferPresentation.StratigraphyColumns : SoilTransferPresentation.ProfileColumns;
}
