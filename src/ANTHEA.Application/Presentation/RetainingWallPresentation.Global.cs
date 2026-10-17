using System.Text.Json.Nodes;
using Anthea.Application.Walls;

namespace Anthea.Application.Presentation;

/// <summary>Global stability of the wall sheet: guidance, search, deep layers, matrix and results, as the historic view computed them.</summary>
public static partial class RetainingWallPresentation
{
    public const string GlobalResultExplanation = "Leggere prima l’Esito di ogni combinazione. F è il fattore trovato con i parametri di progetto; il tasso è η=γR/F. La verifica è soddisfatta nel dominio esplorato quando F≥γR (η≤1) e la ricerca è completa e convergente. «Minimo sul bordo», «ricerca incompleta» o «discretizzazione non convergente» richiedono di correggere/ampliare la ricerca, anche con η<1. Il cerchio disegnato è la superficie critica; le linee verticali sono i conci di terreno.";
    public const string GlobalNoResult = "Attivare la stabilità globale in Input → Terreno e completare profilo, strati e ricerca.";
    public const string GlobalMatrixEdited = "Matrice globale personalizzata: fanno fede i coefficienti di ogni riga.";

    /// <summary>
    /// What is still missing before the global calculation, in the order of the guide: activation, profile, deep layers of every
    /// column, their properties, the depth of the search, groundwater, the confirmation of the profile, the error of the last calculation.
    /// </summary>
    public static IReadOnlyList<string> GlobalReadiness(JsonObject data, string? error)
    {
        var g = data["global_stability"]!;
        var items = new List<string>();
        if (!g.B("enabled")) items.Add("Attiva la verifica globale oppure premi «Prepara dal muro».");
        if (g.Array("valley").Count < 2 || g.Array("uphill").Count < 2) items.Add("Precompila il profilo, poi correggi le quote secondo il rilievo.");
        else if (g.Array("valley").Concat(g.Array("uphill")).Any(p => new[] { "x", "y" }.Any(k => J.Number(p?[k]) is not double n || !double.IsFinite(n)))) items.Add("Completa le coordinate x e y di tutti i punti del rilievo.");
        var columns = g.S("soil_mode") == "Due colonne" ? new[] { ("monte", "layers"), ("valle", "valley_layers") } : new[] { ("profilo unico", "layers") };
        foreach (var (name, key) in columns)
        {
            var layers = g.Array(key);
            if (layers.Count == 0) { items.Add($"Inserisci gli strati profondi: {name}."); continue; }
            var bottom = J.Number(layers[^1]?["bottom"]);
            if (bottom is null || !double.IsFinite(bottom.Value) || bottom >= 0) items.Add($"La colonna {name} deve raggiungere il terreno sotto la fondazione (fondo y negativo).");
            else if (J.Number(g["depth_max"]) is double depth && depth > -bottom) items.Add($"{name}: strati fino a y={bottom:0.##} m; la ricerca a {depth:0.##} m è più profonda dei dati disponibili.");
            string[] required = g.S("condition") == "Non drenata" ? ["gamma", "gamma_sat"] : ["gamma", "gamma_sat", "phi", "c"];
            if (layers.Any(l => required.Any(k => J.Number(l?[k]) is not double n || !double.IsFinite(n)))) items.Add($"{name}: seleziona gli strati e completa le proprietà del terreno.");
            if (g.S("condition") == "Non drenata" && layers.Any(l => J.Number(l?["cu"]) is not double cu || cu <= 0)) items.Add($"{name}: inserisci cu,k > 0 per tutti gli strati non drenati.");
        }
        if (J.Number(g["depth_max"]) is not double maximum || !double.IsFinite(maximum) || maximum <= g.D("depth_min")) items.Add("Imposta una profondità massima di ricerca maggiore della minima, entro gli strati noti.");
        if (g.B("water_enabled") && g.Array("water").Count < 2) items.Add("Completa la linea di falda con almeno due punti (x;y).");
        if (!g.B("profile_confirmed")) items.Add("Controlla rilievo, terreni e falda, poi spunta «Ho controllato profilo, strati e falda del sito».");
        if (!string.IsNullOrWhiteSpace(error)) items.Add("Ultimo calcolo: " + error);
        return items;
    }

    /// <summary>The guide of the global stability: its text (the first item and how many remain), its tooltip (every item) and whether it is ready.</summary>
    public static (string Text, string ToolTip, bool Ready) GlobalGuidance(JsonObject data, string? error)
    {
        var items = GlobalReadiness(data, error);
        string text = items.Count == 0 ? "Pronto per il calcolo · premi «Calcola globale». L’esito si legge nelle Verifiche." : "Da completare · " + items[0]
            + (items.Count > 1 ? $"\nAltri {items.Count - 1} controlli da completare: vedi il suggerimento su questo messaggio." : "");
        return (text, string.Join("\n", items.Select((item, i) => $"{i + 1}. {item}")), items.Count == 0);
    }

    /// <summary>The depth and the slices of the search under the foundation, or the hint to add the deep layers.</summary>
    public static string GlobalSearchSummary(JsonObject data)
    {
        var g = data["global_stability"]!;
        return J.Number(g["depth_max"]) is double depth
            ? $"Ricerca da {g.D("depth_min"):0.##} a {depth:0.##} m sotto la fondazione · {g.D("slices"):0} conci. Limiti visibili nel disegno."
            : "Aggiungi gli strati sotto la fondazione: la profondità di ricerca si aggiornerà automaticamente.";
    }

    /// <summary>The top of a deep column: the last point of the valley profile or the first uphill, otherwise Dv or H + t.</summary>
    public static double GlobalSoilTop(JsonObject data, bool valley)
    {
        var points = data["global_stability"]!.Array(valley ? "valley" : "uphill");
        return points.Count > 0 ? (valley ? points[^1] : points[0]).D("y") : valley ? WallModel.ValleyHeight(data) : WallSheetPresentation.TotalHeight(data);
    }

    /// <summary>The thickness of a deep layer from the bottom of the layer above (or the top) and its own bottom: a number, or empty when not finite.</summary>
    public static JsonNode GlobalThickness(double upper, JsonNode? bottom)
    {
        double thickness = upper - (J.Number(bottom) ?? double.NaN);
        return double.IsFinite(thickness) ? JsonValue.Create(thickness) : JsonValue.Create("");
    }

    /// <summary>The bottoms of the deep layers from the top and the thicknesses typed; NaN from the first thickness that is not positive.</summary>
    public static IReadOnlyList<double> GlobalBottoms(double top, IEnumerable<JsonNode?> thicknesses)
    {
        var bottoms = new List<double>(); double bottom = top;
        foreach (var node in thicknesses)
        {
            double? h = J.Number(node);
            bottom = h is > 0 && double.IsFinite(h.Value) && double.IsFinite(bottom) ? bottom - h.Value : double.NaN;
            bottoms.Add(bottom);
        }
        return bottoms;
    }

    /// <summary>A new deep layer, its properties still to be typed.</summary>
    public static JsonObject NewGlobalLayer() => J.Obj(("name", "Nuovo terreno"), ("bottom", ""), ("gamma", ""), ("gamma_sat", ""), ("phi", ""), ("c", ""), ("cu", ""));

    /// <summary>A new point of a profile, its coordinates still to be typed.</summary>
    public static JsonObject NewPoint() => J.Obj(("x", ""), ("y", ""));

    // ---------------------------------------------------------------- matrix of the global combinations

    /// <summary>The display copy of a row of the global matrix: the factors and one column «a_id» for each action.</summary>
    public static JsonObject GlobalMatrixRow(JsonObject data, JsonNode row)
    {
        var flat = (JsonObject)row.DeepClone(); flat.Remove("coefficients");
        foreach (var action in data.Array("actions")) flat["a_" + action.S("id")] = row["coefficients"]?[action.S("id")]?.DeepClone();
        return flat;
    }

    /// <summary>A cell of the global matrix edited: the matrix becomes custom (its signature kept when it was automatic) and the value goes to the row.</summary>
    public static void GlobalMatrixEdit(JsonObject data, JsonNode row, JsonObject flat, string key)
    {
        var g = data["global_stability"]!;
        if (g.S("combination_mode") == "Automatiche") g["combination_signature"] = WallModel.GlobalSignature(data);
        g["combination_mode"] = "Personalizzate";
        if (key.StartsWith("a_", StringComparison.Ordinal)) row["coefficients"]![key[2..]] = flat[key]?.DeepClone(); else row[key] = flat[key]?.DeepClone();
    }

    /// <summary>The state of the global matrix under its buttons.</summary>
    public static string GlobalMatrixStatus(JsonObject data) => data["global_stability"].S("combination_mode") + " · coefficienti indipendenti dalle verifiche locali";

    /// <summary>«Genera globali»: the automatic global combinations (A2+M2+R2, SLV, accidental). Throws ArgumentException with the reason.</summary>
    public static void GenerateGlobalMatrix(JsonObject data)
    {
        var g = data["global_stability"]!; g["combinations"] = WallModel.GenerateGlobalCombinations(data); g["combination_mode"] = "Automatiche";
    }

    /// <summary>«Conferma matrice globale»: the matrix becomes custom with the current signature.</summary>
    public static void ConfirmGlobalMatrix(JsonObject data)
    {
        var g = data["global_stability"]!; g["combination_mode"] = "Personalizzate"; g["combination_signature"] = WallModel.GlobalSignature(data);
    }

    // ---------------------------------------------------------------- global results

    public static IReadOnlyList<string> GlobalColumns { get; } = ["Caso", "F", "γR", "η=γR/F", "xc [m]", "yc [m]", "R [m]", "Risolte / provate", "Esito"];

    public static IReadOnlyList<string[]> GlobalRows(SlopeResult result) => result.Cases.Select(c => new[] { c.Factors.Name, c.Critical is { } s ? F(s.Factor) : "—", F(c.Factors.R),
        c.Critical is { } q ? q.Ratio.ToString("0.000", It) : "—", c.Critical is { } a ? F(a.Circle.X) : "—", c.Critical is { } b ? F(b.Circle.Y) : "—",
        c.Critical is { } e ? F(e.Circle.Radius) : "—", $"{c.Solved}/{c.Tried}", c.Status }).ToArray();

    /// <summary>The critical surface of the combination shown: coefficients, resistance, driving force, iterations and outcome.</summary>
    public static string GlobalCriticalText(SlopeCaseResult c, SlopeSurfaceResult critical)
        => $"{c.Factors.Name} · kh={c.Factors.Kh:0.#####}, kv={c.Factors.Kv:0.#####} · γMφ/c′/cu={c.Factors.MPhi}/{c.Factors.MC}/{c.Factors.MCu} · ΣR={critical.Resistance:0.###} kN/m · D={critical.Driving:0.###} kN/m · iterazioni {critical.Iterations}, residuo {critical.Residual:G3}. Superfici senza soluzione: {c.NumericalFailures}. {c.Status}";

    public static IReadOnlyList<string> SliceColumns { get; } = ["Concio", "x₀ / x₁ [m]", "y base [m]", "α [°]", "Terreno", "W terra / muro", "Vext / Hext", "u [kPa]", "φd / cd", "N′ [kN/m]", "R / T [kN/m]", "mα"];

    public static IReadOnlyList<string[]> SliceRows(SlopeSurfaceResult critical) => critical.Slices.Select(s => new[] { s.Index.ToString(), F(s.Left) + " / " + F(s.Right), F(s.BaseY),
        F(s.Alpha), s.Soil, F(s.SoilWeight) + " / " + F(s.BodyWeight), F(s.VerticalLoad) + " / " + F(s.HorizontalLoad), F(s.U), F(s.Phi) + " / " + F(s.Cohesion),
        F(s.NormalEffective), F(s.Resistance) + " / " + F(s.Mobilized), s.MAlpha.ToString("0.0000", It) }).ToArray();

    /// <summary>The notes of the global result after the first one.</summary>
    public static string GlobalNotes(SlopeResult result) => string.Join("\n", result.Notes.Skip(1));

    /// <summary>The combination of the global stability shown after a calculation: the one shown before when it still exists, otherwise the worst.</summary>
    public static string? GlobalCombination(SlopeResult? result, string? shown)
        => result?.Cases.FirstOrDefault(c => c.Factors.Name == shown)?.Factors.Name ?? result?.Cases.OrderByDescending(c => c.Critical?.Ratio ?? double.PositiveInfinity).FirstOrDefault()?.Factors.Name;

    /// <summary>The worst combination of a global result (the one shown after the independent calculation).</summary>
    public static string WorstGlobalCombination(SlopeResult result) => result.Cases.OrderByDescending(c => c.Critical?.Ratio ?? double.PositiveInfinity).First().Factors.Name;
}
