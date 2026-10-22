using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Piles;
namespace Anthea.Application.Presentation;

public sealed record HorizontalSoilChoice(string Label, int Mode, string Row);
public sealed record HorizontalSoilEditorState(JsonObject Soil, bool Sand, int Mode,
    string[] Categories, HorizontalSoilChoice[] Choices, int SelectedChoice,
    string Parameter, string Minimum, string Maximum, string ValueField, string TabulatedValue);
public sealed record SandParameterRow(string Addensamento, double A_min, double A_max,
    double Media_software, double A_consigliato_libro, double Nh_non_immerso_N_cm3, double Nh_immerso_N_cm3);
public sealed record CohesiveParameterRow(string Terreno, string Autori, double Minimo_N_cm3,
    double Massimo_N_cm3, double Media_software_N_cm3);

/// <summary>Editor preparation and explicit user choices. Values come from the existing GPC catalog;
/// no tables or engineering rules are copied into the interface.</summary>
public static class HorizontalSoilPresentation
{
    public static string Source => ViggianiHorizontalSoil.Source;
    public static string StratificationConvention => ViggianiHorizontalSoil.StratificationConvention;
    public static SandParameterRow[] SandTable() => ViggianiHorizontalSoil.SandTable.Select(r => new SandParameterRow(
        r.Density, r.MinimumA, r.MaximumA, ViggianiHorizontalSoil.MeanA(r.Density), r.RecommendedA,
        r.DryNhNPerCm3, r.SubmergedNhNPerCm3)).ToArray();
    public static CohesiveParameterRow[] CohesiveTable() => ViggianiHorizontalSoil.CohesiveTable.Select(r => new CohesiveParameterRow(
        r.Category, r.Author, r.MinimumNPerCm3, r.MaximumNPerCm3, ViggianiHorizontalSoil.MeanNh(r.Id))).ToArray();

    // Preparation preserves the existing lazy defaults on the editor-owned layer.
    public static HorizontalSoilEditorState Prepare(JsonObject layer)
    {
        var soil = ElasticHorizontalPile.EnsureSoil(layer); ElasticHorizontalPile.InitializeMean(soil);
        int mode = ElasticHorizontalPile.Laws.IndexOf(soil.S("legge")); bool sand = layer.S("tipologia") == "Granulare";
        var clay = ViggianiHorizontalSoil.CohesiveTable.FirstOrDefault(r => r.Id == soil.S("riga_146") || r.Label == soil.S("riga_146"));
        string Category(string id) => ViggianiHorizontalSoil.CohesiveTable.Single(r => r.Id == id).Category;
        if (!sand && soil["categoria"] == null) soil["categoria"] = clay == null ? "Argilla n.c. / debolmente o.c." : Category(clay.Id);
        var modes = sand ? new[] { 5, 3, 0, 1, 2 } : soil.S("categoria") == "Argilla sovraconsolidata" ? new[] { 0, 2 } : new[] { 4, 0, 1, 2 };
        var choices = modes.SelectMany(m => m == 4
            ? ViggianiHorizontalSoil.CohesiveTable.Where(r => Category(r.Id) == soil.S("categoria")).Select(r => new HorizontalSoilChoice("14.6 · " + r.Author, m, r.Id))
            : new[] { new HorizontalSoilChoice(ElasticHorizontalPile.Laws[m], m, "") }).ToArray();
        int selected = Array.FindIndex(choices, c => c.Mode == mode && (mode != 4 || c.Row == clay?.Id));
        var srow = ViggianiHorizontalSoil.SandTable.FirstOrDefault(r => r.Density == soil.S("densita"));
        double? min = mode == 5 ? srow?.MinimumA : mode == 4 ? clay?.MinimumNPerCm3 : null,
            max = mode == 5 ? srow?.MaximumA : mode == 4 ? clay?.MaximumNPerCm3 : null;
        if (selected < 0) min = max = null;
        return new(soil, sand, mode, sand ? ViggianiHorizontalSoil.SandTable.Select(r => r.Density).ToArray() : ElasticHorizontalPile.SoilCategories,
            choices, selected, mode == 5 ? "A [−]" : mode is 3 or 4 ? "nh [N/cm³]" : mode == 2 ? "k [kN/m²]" : mode == 0 ? "kh [kN/m³]" : "nh [kN/m³]",
            min?.ToString("G5") ?? "—", max?.ToString("G5") ?? "—", mode == 5 ? "A" : mode == 4 ? "nh_tabella" : "valore",
            srow == null ? "—" : $"{srow.DryNhNPerCm3} / {srow.SubmergedNhNPerCm3}*");
    }
    public static void Select(JsonObject layer, string key, string value) => ElasticHorizontalPile.SelectSoil(layer, key, value);
    public static void SelectLaw(JsonObject layer, HorizontalSoilChoice choice)
    {
        var soil = layer["reazione_orizzontale"]!.AsObject(); soil["legge"] = ElasticHorizontalPile.Laws[choice.Mode];
        if (choice.Mode == 4) ElasticHorizontalPile.SelectSoil(layer, "riga_146", choice.Row);
        else ElasticHorizontalPile.InitializeMean(soil);
    }
    public static void RecordManualChoice(JsonObject soil, string key)
    {
        if (key is "A" or "nh_tabella" or "valore")
        { soil["origine_scelta"] = "Valore modificato dall'utente"; soil["scelta_per"] = ElasticHorizontalPile.SelectionKey(soil); }
    }
    public static bool CanAdoptMean(JsonObject soil) => soil.S("legge") == ElasticHorizontalPile.Laws[4] || soil.S("legge") == ElasticHorizontalPile.Laws[5];
    public static void AdoptMean(JsonObject soil) => ElasticHorizontalPile.SetMean(soil);
    public static void KeepValue(JsonObject soil)
    { soil["scelta_per"] = ElasticHorizontalPile.SelectionKey(soil); soil["origine_scelta"] = "Valore mantenuto esplicitamente dopo cambio fonte"; }
    public static string Description(JsonObject soil)
    {
        var sand=ViggianiHorizontalSoil.SandTable.FirstOrDefault(r=>r.Density==soil.S("densita"));var clay=ViggianiHorizontalSoil.CohesiveTable.FirstOrDefault(r=>r.Id==soil.S("riga_146")||r.Label==soil.S("riga_146"));string law=soil.S("legge"),text=law;
        if(law==ElasticHorizontalPile.Laws[5]&&sand!=null)text+=$"\nA {sand.MinimumA}–{sand.MaximumA}; media iniziale {ViggianiHorizontalSoil.MeanA(sand.Density)}; consigliato dal libro {sand.RecommendedA}. nh=A γ/1,35; sotto falda γ′=γsat−γw.";
        if(law==ElasticHorizontalPile.Laws[3]&&sand!=null)text+=$"\nnh non immerso {sand.DryNhNPerCm3}; immerso {sand.SubmergedNhNPerCm3} N/cm³. Selezione dalla falda condivisa; dettagli per quota nei risultati.";
        if(law==ElasticHorizontalPile.Laws[4]&&clay!=null)text+=$"\n{clay.Author}: {clay.MinimumNPerCm3}–{clay.MaximumNPerCm3} N/cm³; media software {ViggianiHorizontalSoil.MeanNh(clay.Id)}. Singola fonte, nessuna media tra autori.";
        if(law==ElasticHorizontalPile.Laws[0])text+="\nkh costante: argille sovraconsolidate; assegnazione esplicita.";
        text+="\n"+soil.S("origine_scelta")+"\n"+ViggianiHorizontalSoil.Source;
        if((law==ElasticHorizontalPile.Laws[4]||law==ElasticHorizontalPile.Laws[5])&&soil.S("scelta_per")!=ElasticHorizontalPile.SelectionKey(soil))text+="\nFonte cambiata: adottare la nuova media o confermare il mantenimento del valore.";return text;
    }
}
