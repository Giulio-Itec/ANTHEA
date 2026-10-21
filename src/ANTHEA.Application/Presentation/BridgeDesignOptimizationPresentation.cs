using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.BridgeDesign;
using Anthea.Calculations;

namespace Anthea.Application.Presentation;

/// <summary>
/// The search of the optimum of the Bridge Design sheet as the user reads it (F5.12): the texts of the state of the search, the spans
/// suggested by the length, the options read from the limits typed by the user, the ranking, the reasons of exclusion, the selection and the
/// changes of the solution selected, the solution applied. Moved without changes from the historic view (BridgeDesignWorkspace, the page
/// «Ottimizzazione»), which calls them; the ViewModel of F5.12 reads the same functions.
/// </summary>
public static class BridgeDesignOptimizationPresentation
{
    private static string F(double n, string format = "N1") => BridgeDesignPresentation.F(n, format);

    public const string Title = "OTTIMIZZAZIONE · BRIDGE DESIGN";
    public const string Ready = "Scegli le variabili libere e avvia la ricerca.";
    public const string SelectionDefault = "Anteprima delle alternative";
    public const string BestDefault = "L’ottimo verrà individuato fra le combinazioni esplorate.";
    public const string NoSpans = "Nessun numero di campate tra 1 e 30 compatibile con le luci usuali. Rivedere la tipologia.";
    public const string SpansSuggested = "Intervallo suggerito dalle luci usuali e dalla lunghezza totale. Ostacoli e luce delle singole campate saranno controllati durante la ricerca.";
    public const string ConstraintsChanged = "Vincoli modificati: avvia la ricerca per aggiornare le alternative.";
    public const string DataChanged = "Dati o listino modificati: ripetere l’ottimizzazione.";
    public const string WaitingCalculation = "Attesa dell’aggiornamento del ponte…";
    public const string NeedsData = "Completa prima i dati del ponte.";
    public const string InvalidLimits = "Inserire numeri validi nei limiti di ricerca.";
    public const string Running = "Ricerca in corso…";
    public const string NoSolution = "Nessuna soluzione ammessa: esamina i motivi di esclusione.";
    public const string NoPreview = "Nessuna anteprima disponibile";
    public const string Interrupted = "Ricerca interrotta. Nessuna modifica applicata al ponte.";
    public const string TopInvalid = "N deve essere un intero fra 1 e 50.000. La ricerca eseguita è conservata.";
    public const string Stale = "Risultati non aggiornati: ripetere la ricerca.";
    public const string RankingNote = "Seleziona una riga per confrontare quote e disegno. L’ordine considera il punteggio non arrotondato; a parità, costo, CO₂, tipologia e campate.";
    public const string ExclusionsHeader = "Motivi di esclusione · una combinazione può avere più motivi";
    public const string ChangesTitle = "Variazioni rispetto al progetto corrente · quote adottate";

    /// <summary>The objectives of the search, in the order of <see cref="BridgeDesignModel.OptimizationObjective"/>.</summary>
    public static IReadOnlyList<string> Objectives { get; } = ["Costo minimo", "CO₂ minima", "Compromesso costo / CO₂ (50 / 50)"];

    /// <summary>The locks of «Mantieni costante»: key, label and initial state.</summary>
    public static IReadOnlyList<(string Key, string Label, bool Value)> Locks { get; } = [("family", "Tipologia", false), ("spans", "Numero campate", false),
        ("depth", "Altezza in campata", false), ("section", "Dimensioni della sezione", false), ("continuous", "Continuità", true),
        ("pier", "Schema pila e quote imposte", false), ("foundation", "Fondazione e lunghezza pali", false)];

    /// <summary>The variables of the chart of the explored variables (the third is shown first).</summary>
    public static IReadOnlyList<string> Variables { get; } = ["Costo [M€]", "CO₂ [t]", "Altezza in campata [m]", "Numero campate", "Lunghezza pali [m]", "Tipologia", "Schema pila", "Fondazione", "Continuità"];

    /// <summary>The filters of the cloud: every typology, then each one.</summary>
    public static IReadOnlyList<string> Filters() => new[] { "Tutte le tipologie" }.Concat(BridgeDesignModel.Families.Select(f => f.Name)).ToArray();

    /// <summary>
    /// «Suggerisci campate dalla lunghezza»: with the spans kept, the spans of the result; otherwise the numbers of spans from 1 to 30 whose
    /// span fits the usual spans of the typologies allowed, with the status; null minimum and maximum when none fits.
    /// </summary>
    public static (string? Minimum, string? Maximum, string? Status) SuggestSpans(BridgeDesignModel.Result r, bool keepSpans, bool keepFamily)
    {
        if (keepSpans) return (r.Spans.Length.ToString(), r.Spans.Length.ToString(), null);
        var families = keepFamily ? new[] { r.Family } : BridgeDesignModel.Families;
        var counts = Enumerable.Range(1, 30).Where(n => families.Any(f => BridgeDesignModel.HasTowers(f.Id) ? n == 3 && r.Length / 2 >= f.MinSpan && r.Length / 2 <= f.MaxSpan : r.Length / n >= f.MinSpan && r.Length / n <= f.MaxSpan)).ToArray();
        if (counts.Length == 0) return (null, null, NoSpans);
        return (counts.Min().ToString(), counts.Max().ToString(), SpansSuggested);
    }

    /// <summary>
    /// The options of the search from the choices of the user: the objective, the locks and the texts of the limits. The grid of a quantity
    /// kept is the default one. Throws <see cref="FormatException"/> or <see cref="OverflowException"/> for a limit that is not a number.
    /// </summary>
    public static BridgeDesignModel.OptimizationOptions Options(int objective, Func<string, bool> keep, string minSpans, string maxSpans, string minDepth, string maxDepth,
        string depthFrom, string depthTo, string depthStep, string pileFrom, string pileTo, string pileStep)
    {
        static double Read(string text) => double.Parse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
        return new BridgeDesignModel.OptimizationOptions
        {
            Objective = (BridgeDesignModel.OptimizationObjective)Math.Max(0, objective),
            KeepFamily = keep("family"), KeepSpans = keep("spans"), KeepDepth = keep("depth"), KeepSection = keep("section"),
            KeepPier = keep("pier"), KeepFoundation = keep("foundation"), KeepContinuity = keep("continuous"),
            MinSpans = int.Parse(minSpans), MaxSpans = int.Parse(maxSpans), MinDepth = Read(minDepth), MaxDepth = Read(maxDepth),
            DepthPercentMin = keep("depth") ? 100 : Read(depthFrom), DepthPercentMax = keep("depth") ? 115 : Read(depthTo), DepthPercentStep = keep("depth") ? 15 : Read(depthStep),
            PilePercentMin = keep("foundation") ? 100 : Read(pileFrom), PilePercentMax = keep("foundation") ? 150 : Read(pileTo), PilePercentStep = keep("foundation") ? 25 : Read(pileStep)
        };
    }

    /// <summary>The state of the search while it runs.</summary>
    public static string Progress(BridgeDesignModel.OptimizationProgress p) => $"{p.Evaluated:N0} calcolate · {p.Admissible:N0} ammesse · griglia fino a {p.Planned:N0} tentativi (prima dei duplicati).";

    /// <summary>The selection of the preview during the search.</summary>
    public static string ProvisionalSelection(BridgeDesignModel.OptimizationCandidate best) => "Migliore provvisorio · " + best.Result.Family.Name;

    /// <summary>The best of the preview during the search.</summary>
    public static string ProvisionalBest(BridgeDesignModel.OptimizationCandidate best) => $"{F(best.Result.TotalCost, "N0")} € · {F(best.Result.Carbon, "N0")} tCO₂e · graduatoria in corso";

    /// <summary>The state of the search when it ends.</summary>
    public static string Completed(BridgeDesignModel.OptimizationResult result) =>
        $"{result.Evaluated:N0} calcolate · {result.Admissible:N0} ammesse · {result.Solutions.Length:N0} geometrie distinte · {result.Evaluated - result.Admissible:N0} escluse.";

    /// <summary>The best solution explored.</summary>
    public static string Best(BridgeDesignModel.OptimizationTrial best) => $"OTTIMO ESPLORATO · {F(best.Cost!.Value, "N0")} € · {F(best.Carbon!.Value, "N0")} tCO₂e";

    /// <summary>True when <paramref name="text"/> is a valid N of «Mostra le prime N».</summary>
    public static bool TryTop(string text, out int n) => int.TryParse(text, out n) && n >= 1 && n <= 50000;

    /// <summary>The line above the ranking.</summary>
    public static string RankingHeader(BridgeDesignModel.OptimizationResult result, int n) =>
        $"{Math.Min(n, result.Solutions.Length):N0} di {result.Solutions.Length:N0} soluzioni distinte · obiettivo: {new[] { "costo", "CO₂", "compromesso 50/50" }[(int)result.Options.Objective]}. Cambiare N non ripete il calcolo.";

    /// <summary>The filters that the current design does not satisfy, null when it satisfies them.</summary>
    public static string? BaselineExclusions(BridgeDesignModel.OptimizationResult result) =>
        result.BaselineExclusions.Length > 0 ? "Il progetto corrente non soddisfa i filtri: " + string.Join("; ", result.BaselineExclusions) : null;

    public static IReadOnlyList<string> RankingColumns { get; } = ["# / tipologia", "n / d [m]", "Costo [€]", "Δ costo [€]", "CO₂ [t]", "Punteggio", "Pareto"];

    /// <summary>The first <paramref name="n"/> solutions of the ranking.</summary>
    public static string[][] RankingRows(BridgeDesignModel.OptimizationResult result, int n) => result.Solutions.Take(n).Select(c =>
        new[] { $"{c.Rank} · {BridgeDesignModel.Families.Single(f => f.Id == c.Trial.Family).Name}", $"{c.Trial.Spans} / {F(c.Trial.Depth!.Value, "N2")}", F(c.Trial.Cost!.Value, "N0"),
            F(c.Trial.Cost.Value - result.Baseline.TotalCost, "N0"), F(c.Trial.Carbon!.Value, "N0"), F(c.Score, "N3"), c.Pareto ? "Sì" : "—" }).ToArray();

    public static IReadOnlyList<string> ExclusionColumns { get; } = ["Motivo", "Occorrenze"];

    public static string[][] ExclusionRows(BridgeDesignModel.OptimizationResult result) =>
        result.Excluded.OrderByDescending(p => p.Value).Select(p => new[] { p.Key, p.Value.ToString() }).ToArray();

    /// <summary>The selection of a solution: rank, typology, Pareto.</summary>
    public static string Selection(BridgeDesignModel.OptimizationSolution c, BridgeDesignModel.Result r) =>
        $"#{c.Rank} · {r.Family.Name} · {(c.Pareto ? "frontiera Pareto" : "alternativa ammessa")}";

    public static IReadOnlyList<string> ChangeColumns { get; } = ["Variabile", "Corrente", "Selezionata", "Δ"];

    /// <summary>
    /// The changes of the solution <paramref name="c"/> (calculated as <paramref name="r"/>) with respect to the design searched
    /// (<paramref name="source"/>, its result the baseline of <paramref name="result"/>): typology, cost, CO₂, spans, pier, foundation,
    /// continuity, then the dimensions adopted.
    /// </summary>
    public static string[][] ChangeRows(JsonObject source, BridgeDesignModel.OptimizationResult result, BridgeDesignModel.OptimizationSolution c, BridgeDesignModel.Result r)
    {
        var rows = new List<string[]>();
        void Row(string label, string before, string after, string delta = "—") => rows.Add([label, before, after, delta]);
        void Numeric(string label, double before, double after, string format = "N2") => Row(label, F(before, format), F(after, format), F(after - before, format));
        Row("Tipologia", result.Baseline.Family.Name, r.Family.Name);
        Numeric("Costo [€]", result.Baseline.TotalCost, r.TotalCost, "N0"); Numeric("CO₂ [t]", result.Baseline.Carbon, r.Carbon, "N1");
        Row("Luci [m]", string.Join(" + ", result.Baseline.Spans.Select(s => F(s, "N2"))), string.Join(" + ", r.Spans.Select(s => F(s, "N2"))));
        Row("Schema pila", BridgeDesignModel.HasTowers(result.Baseline.Family.Id) ? "Antenna · 2 fusti quadrati" : source["input"].S("pier"), c.Trial.Pier); Row("Fondazione", result.Baseline.Foundation, r.Foundation);
        Row("Continuità", source["input"].B("continuous") ? "Continua" : "Indipendente", c.Data["input"].B("continuous") ? "Continua" : "Indipendente");
        var old = BridgeDesignModel.TechnicalSchedule(source, result.Baseline).ToDictionary(t => (t.Component, t.Symbol, t.Unit));
        foreach (var t in BridgeDesignModel.TechnicalSchedule(c.Data, r))
        {
            if (old.TryGetValue((t.Component, t.Symbol, t.Unit), out var b)) Numeric($"{t.Component} · {t.Symbol} [{t.Unit}]", b.Value, t.Value, t.Unit == "n." ? "N0" : t.Unit == "mm" ? "N1" : "N3");
            else Row($"{t.Component} · {t.Symbol} [{t.Unit}]", "n.a.", F(t.Value, "N3"));
        }
        return rows.ToArray();
    }

    /// <summary>
    /// «Applica soluzione selezionata»: the current design (without its alternative A) becomes the alternative A when there is none, and
    /// the inputs become those of the solution (<paramref name="candidate"/>, a copy of its data).
    /// </summary>
    public static void ApplySolution(JsonObject data, JsonObject candidate, JsonObject current)
    {
        if (data["alternative_a"] is null) data["alternative_a"] = current;
        data["input"] = candidate["input"]!.DeepClone();
    }
}
