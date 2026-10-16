using System.Globalization;
using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Application.Presentation;

/// <summary>Kind of a figure of the report of a vertical pile sheet.</summary>
public enum VerticalPileFigureKind
{
    /// <summary>The capacity chart as the sheet shows it.</summary>
    Capacity,
    /// <summary>The reference chart (Nq) as the sheet shows it.</summary>
    Reference,
    /// <summary>The Bustamante–Doix abacus of one stratigraphy (micropile).</summary>
    Abacus,
    /// <summary>The profile of one stratigraphy.</summary>
    Profile,
    /// <summary>The profile of every stratigraphy.</summary>
    Profiles
}

/// <summary>
/// A figure of the report of a vertical pile sheet: what to draw, its caption and category for ReportWord, the stratigraphy it
/// belongs to and the size of the image in pixels.
/// </summary>
public sealed record VerticalPileFigurePlan(VerticalPileFigureKind Kind, string Caption, string Category, int Survey, int Width, int Height)
{
    /// <summary>The stratigraphy of the figure for ReportWord: only the profiles of one stratigraphy are placed with it.</summary>
    public int? ReportIndex => Kind == VerticalPileFigureKind.Profile ? Survey : null;
}

/// <summary>
/// Texts, tables and rules of the vertical pile and micropile sheet (geo_palo_verticale, geo_micropalo_verticale), moved in F5.6
/// without changes from the historic view (the generic branch of SheetEditor and GeoEditor) and from the figures of its report
/// (ModuleFigureRenderer): the historic view and VerticalPileSheetViewModel call them. Engineering values stay in the engines and in
/// <see cref="VerticalPileCharts"/>/<see cref="VerticalPilePreview"/>; here only what the sheet shows of them.
/// </summary>
public static class VerticalPilePresentation
{
    /// <summary>Status before the first request.</summary>
    public const string StatusToVerify = "Dati da verificare · premere Calcola";
    /// <summary>Status after an edit, at once replaced by <see cref="StatusPending"/> (the automatic recalculation).</summary>
    public const string StatusChanged = "Dati modificati · premere Calcola";
    /// <summary>Status while the automatic recalculation waits for its delay or for the previous calculation.</summary>
    public const string StatusPending = "Aggiornamento automatico in attesa…";
    /// <summary>Status while the engine works.</summary>
    public const string StatusRunning = "Calcolo in corso…";
    /// <summary>Status of a result of the current data.</summary>
    public const string StatusCompleted = "Calcolo completato · risultati riferiti ai dati correnti";
    /// <summary>Text of the JSON of the results after an edit.</summary>
    public const string RawChanged = "Dati modificati · risultati da ricalcolare";

    /// <summary>Status of a calculation stopped by missing data.</summary>
    public static string StatusIncomplete(string missing) => "Dati da completare: " + missing;

    /// <summary>Status of a calculation that failed.</summary>
    public static string StatusFailed(string message) => "Errore: " + message;

    /// <summary>The warnings of a result: the warning of a stratigraphy shorter than the pile first, then those of the engine.</summary>
    public static string Warnings(string engine, JsonObject result) => result.B("copertura_completa") ? engine
        : "ATTENZIONE: stratigrafia insufficiente; risultati limitati alla quota disponibile, non alla punta richiesta.\n" + engine;

    // ------------------------------------------------------------------ forms

    /// <summary>The fields of the general form enabled by the options of the sheet; the others are always enabled.</summary>
    public static IReadOnlyList<(string Key, bool Enabled)> GeneralFieldStates(JsonNode? general, bool micro) => micro
        ? [("percentuale_punta", general.B("considera_punta"))]
        : [("sottotipo_palo_battuto", general.S("tipo_palo") == "Battuto"), ("profondita_falda", general.B("presenza_falda")),
            ("considera_sottospinta", general.B("presenza_falda"))];

    /// <summary>The fields of the normative form enabled by the options of the sheet: γb only with the tip of the micropile.</summary>
    public static IReadOnlyList<(string Key, bool Enabled)> NormativeFieldStates(JsonNode? general, bool micro) => micro
        ? [("sicurezza_base", general.B("considera_punta"))] : [];

    /// <summary>The field <paramref name="key"/> of the efficiency form is shown with the method <paramref name="method"/> (the method is always shown).</summary>
    public static bool EfficiencyFieldVisible(string key, string method) => key.StartsWith("numero_", StringComparison.Ordinal) ? method is "Feld" or "Converse-Labarre"
        : key.StartsWith("interasse", StringComparison.Ordinal) ? method == "Converse-Labarre" : method == "Definita dall'utente";

    /// <summary>The value of a normative coefficient after «Reset»: γb of the technology, the others of a new sheet.</summary>
    public static string NormativeResetValue(string key, JsonNode general, JsonObject defaults, bool micro) =>
        key == "sicurezza_base" ? VerticalPilePreview.NormativeBase(general, micro) : defaults["generali"].S(key);

    // ------------------------------------------------------------------ verification

    /// <summary>Columns of the verification card.</summary>
    public static string[] VerificationColumns() => ["Condizione", "NEd [kN]", "Rd [kN]", "Util.", "Esito"];

    /// <summary>Note under the verification card.</summary>
    public const string VerificationNote = "Valori in kN · Util. = NEd / Rd";

    /// <summary>Rows of the verification card: design resistance and action at the tip for each condition, utilization and outcome.</summary>
    public static List<string[]> Verification(JsonObject? result, bool micro)
    {
        var rows = new List<string[]>();
        foreach (var (key, label) in micro ? new[] { ("compressione", "Compressione"), ("trazione", "Trazione") } : new[]
        {
            ("drenante_compressione", "Comp – Dre"), ("non_drenante_compressione", "Comp – Non Dre"),
            ("drenante_trazione", "Tra – Dre"), ("non_drenante_trazione", "Tra – Non Dre")
        })
        {
            double? rd = null, ed = null;
            if (result?.B("copertura_completa") == true)
            {
                var resistance = result["curve"]?[key]?["progetto"] as JsonArray;
                var actions = VerticalPileCharts.Actions(result, key);
                rd = J.Number((resistance?.LastOrDefault() as JsonArray)?.ElementAtOrDefault(1));
                ed = J.Number((actions?.LastOrDefault() as JsonArray)?.ElementAtOrDefault(1));
            }
            bool valid = rd is double r && double.IsFinite(r) && r >= 0 && ed is double n && double.IsFinite(n) && n >= 0;
            string outcome = micro && result is not null && !result.B("copertura_completa") ? "Verifica incompleta: stratigrafia insufficiente" : micro && rd.HasValue && !ed.HasValue ? "Azione non inserita" : !valid ? "Da verificare" : ed <= rd ? "Verifica soddisfatta" : "Verifica non soddisfatta";
            rows.Add([label, ed?.ToString("N1") ?? "—", rd?.ToString("N1") ?? "—",
                valid && rd > 0 ? (100 * ed / rd)?.ToString("N1") + "%" : "—", outcome]);
        }
        return rows;
    }

    // ------------------------------------------------------------------ tables

    /// <summary>First item of «Tabelle e dettagli»: the capacity at the available depth.</summary>
    public const string CapacitySummaryTitle = "Capacità portante — riepilogo";

    /// <summary>Footnote of the capacity at the available depth.</summary>
    public const string CapacitySummaryFootnote = "Ramo governante (media o minimo) scelto separatamente per compressione e trazione.";

    /// <summary>Depth (pile) or length along the axis (micropile) reached by the stratigraphies, in m.</summary>
    public static double AvailableDepth(JsonObject result) => result.D("profondita_massima");

    /// <summary>Note above the capacity at the available depth.</summary>
    public static string CapacitySummaryNote(JsonObject result, bool micro) =>
        $"Compressione e trazione · quota disponibile {(micro ? "s" : "z")} = {AvailableDepth(result).ToString("0.0", CultureInfo.GetCultureInfo("it-IT"))} m"
        + (result.B("copertura_completa") ? "" : " · copertura incompleta");

    /// <summary>The tables of «Tabelle e dettagli» after the capacity: the summary at the available depth, then the details of the engine.</summary>
    public static List<Tabella> Tables(JsonObject result, bool micro)
    {
        var tables = Tabelle.Crea(result, micro); var summary = new List<string[]>();
        foreach (var (key, c) in result["curve"]!.AsObject())
        {
            var p = c!.Array("progetto")[^1]!; double rd = p[1]!.GetValue<double>(); var a = VerticalPileCharts.Actions(result, key)?.LastOrDefault(); double? ed = J.Number(a?[1]);
            summary.Add([key.Replace('_', ' '), Tabelle.F(p[0]), Tabelle.F(rd), ed.HasValue ? Tabelle.F(ed.Value) : "—", !result.B("copertura_completa") ? "Verifica incompleta: stratigrafia insufficiente" : ed.HasValue ? ed <= rd ? "Verifica soddisfatta" : "Verifica non soddisfatta" : "Azione non inserita"]);
        }
        tables.Insert(0, new("Riepilogo alla quota disponibile", ["Verifica", micro ? "s [m]" : "z [m]", "Rd [kN]", "Ed [kN]", "Esito"], summary));
        return tables;
    }

    /// <summary>A cell of the tables of the sheet: a number with one decimal (Italian format), any other text unchanged.</summary>
    public static string DetailCell(string value)
    {
        var culture = CultureInfo.GetCultureInfo("it-IT");
        return double.TryParse(value, NumberStyles.Float, culture, out double number) && double.IsFinite(number) ? number.ToString("0.0", culture) : value;
    }

    // ------------------------------------------------------------------ charts

    /// <summary>Title of the capacity chart with a result (before the first result the chart keeps its own).</summary>
    public static string CapacityTitle(bool micro) => micro ? "Capacità portante del micropalo" : "Capacità portante del palo";

    /// <summary>Vertical axis of the capacity chart with a result.</summary>
    public static string CapacityAxis(bool micro) => micro ? "Lungo asse s [m]" : "Profondità z [m]";

    /// <summary>Message of the capacity chart without curves and without a result.</summary>
    public static string CapacityEmptyMessage(JsonObject data) => data["generali"].D("lunghezza") > 0 ? "Completare i dati · calcolo automatico" : "Lunghezza: inserire un numero valido";

    /// <summary>Message of the capacity chart with a result and no curve chosen.</summary>
    public const string NoCurveSelected = "Nessuna curva selezionata";

    /// <summary>Text above the curves: the length of the pile when the values at L are available.</summary>
    public static string EndValues(JsonObject data, JsonObject? result) => result?.B("copertura_completa") == true
        ? $"Valori a L={data["generali"].S("lunghezza")} m [kN]" : "Valori a L non disponibili";

    /// <summary>Label of the check box of a curve: its name and its value at L when available.</summary>
    public static string CurveChoice(string name, IReadOnlyList<double[]> points, JsonObject? result) =>
        name + ": " + (result?.B("copertura_completa") == true && points.Count > 0 ? points[^1][0].ToString("N1") : "—");

    // ------------------------------------------------------------------ report

    /// <summary>
    /// The figures of the report of the sheet, in their order: the capacity chart; the abacus of every stratigraphy (micropile) or the
    /// Nq chart (pile); the profile of every stratigraphy; the whole profile. Captions, categories and sizes as the report has them.
    /// </summary>
    public static IReadOnlyList<VerticalPileFigurePlan> Figures(JsonObject data, bool micro, string capacityTitle, string referenceTitle)
    {
        int surveys = data.Array("stratigrafie").Count;
        var figures = new List<VerticalPileFigurePlan> { new(VerticalPileFigureKind.Capacity, capacityTitle, "grafico_capacita", -1, 1200, 750) };
        if (micro)
            for (int i = 0; i < surveys; i++) figures.Add(new(VerticalPileFigureKind.Abacus, $"Abachi Bustamante–Doix del sondaggio {i + 1}", "grafico_nq", i, 1200, 750));
        else figures.Add(new(VerticalPileFigureKind.Reference, referenceTitle, "grafico_nq", -1, 1200, 750));
        for (int i = 0; i < surveys; i++) figures.Add(new(VerticalPileFigureKind.Profile, $"Profilo del sondaggio {i + 1}", "stratigrafia", i, 720, micro ? 600 : 900));
        figures.Add(new(VerticalPileFigureKind.Profiles, "Profilo stratigrafico", "grafico_profilo", -1, 1200, 750));
        return figures;
    }
}
