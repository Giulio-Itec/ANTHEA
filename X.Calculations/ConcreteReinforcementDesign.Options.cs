using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public sealed record ConcreteDesignCheck(string Key, string Name, string Combination, double? Ratio,
    double Target, bool? Passed, string Detail);
public sealed record ConcreteDesignMetrics(double SteelArea, double LongitudinalKgPerM, double TransverseKgPerM,
    int Bars, int Diameters, double PiecesPerM)
{
    public double KgPerM => LongitudinalKgPerM + TransverseKgPerM;
}
public sealed record ConcreteDesignCandidate(int Id, JsonObject Input, JsonObject Shear, JsonObject Detailing, string Description,
    ConcreteDesignMetrics Metrics, IReadOnlyList<ConcreteDesignCheck> Checks, double Score = 0, bool Pareto = false)
{
    public bool NeedsReview => Checks.Any(c => c.Passed is null);
}
public sealed record ConcreteDesignProgress(int Evaluated, int Planned, int Accepted, ConcreteDesignCandidate? Best)
{
    public string Phase { get; init; } = "Ricerca";
    public double Seconds { get; init; }
    public int Workers { get; init; } = 1;
    public int PreliminaryChecked { get; init; }
}
public sealed record ConcreteDesignStage(string Name, int Calls, double Seconds);
public sealed record ConcreteDesignPerformance(double Seconds, double? FirstSolutionSeconds, int Workers,
    int LongitudinalCalculations, int LongitudinalReuses, int ProbeSections, IReadOnlyList<ConcreteDesignStage> Stages);
public sealed record ConcreteDesignResult(IReadOnlyList<ConcreteDesignCandidate> Candidates, int Evaluated, int Planned,
    bool Completed, IReadOnlyDictionary<string, int> Exclusions, ConcreteDesignMetrics? Baseline, string[] Notes)
{
    public IReadOnlyDictionary<string, ConcreteDesignCheck> ExclusionExamples { get; init; } = new Dictionary<string, ConcreteDesignCheck>();
    public ConcreteDesignPerformance? Performance { get; init; }
}

/// <summary>Finite, user-defined search space. Units: mm, mm², kg/m; no UI dependency.</summary>
public sealed record ConcreteDesignOptions
{
    public double[] Diameters { get; init; } = [12, 16, 20, 24];
    public int[] TopCounts { get; init; } = [2, 4, 6];
    public int[] BottomCounts { get; init; } = [2, 4, 6, 8];
    public int[] SideCounts { get; init; } = [0, 2, 4];
    public int[] CircularCounts { get; init; } = [8, 12, 16, 20, 24];
    public double[] StirrupDiameters { get; init; } = [8, 10];
    public double[] StirrupSpacings { get; init; } = [100, 150, 200, 250];
    public double[] SecondaryDiameters { get; init; } = [8, 10, 12, 16];
    public double[] SecondarySpacings { get; init; } = [100, 150, 200, 250];
    public bool Symmetric { get; init; }
    public string Stirrups { get; init; } = "Ottimizza";
    public string Objective { get; init; } = "Peso";
    public int MaxDiameterKinds { get; init; } = 3;
    public int MaxEvaluations { get; init; } = 100000;
    /// <summary>0 = automatic (at most 4, leaving CPU capacity to the UI); explicit range 1–8.</summary>
    public int MaxParallelism { get; init; }
    public int Workers => MaxParallelism == 0 ? Math.Clamp(Environment.ProcessorCount / 2, 1, 4) : Math.Min(MaxParallelism, Environment.ProcessorCount);
    public double HookAllowance { get; init; } = 20;
    public double TorsionReservePercent { get; init; } = 25;
    public double WeightCost { get; init; } = 1;
    public double BarCost { get; init; } = 1;
    public double DiameterCost { get; init; } = 1;
    public bool AnchorageConfirmed { get; init; }
    public bool TorsionLayoutConfirmed { get; init; }
    /// <summary>Motore SLE (tensioni e fessurazione) dei controlli dei candidati; null = predefinito dell'adattatore (refactoring F2.7b,
    /// commit A4). Non viene dal foglio: lo assegnano le prove e le catture.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public ServiceabilityEngine? ServiceabilityEngine { get; init; }
    public Dictionary<string, double> Targets { get; init; } = ConcreteReinforcementDesign.Targets.ToDictionary(t => t.Key, _ => 1d);
    public double Target(string key) => Targets.GetValueOrDefault(key, 1);
    public void Validate()
    {
        foreach (var list in new[] { Diameters, StirrupDiameters, SecondaryDiameters })
            if (list.Length is < 1 or > 12 || list.Any(v => !double.IsFinite(v) || v < 4 || v > 50))
                throw new ArgumentException("Diametri: da 1 a 12 valori fra 4 e 50 mm.");
        if (new[] { StirrupSpacings, SecondarySpacings }.Any(a => a.Length is < 1 or > 20 || a.Any(v => !double.IsFinite(v) || v < 20 || v > 1000)))
            throw new ArgumentException("Passi staffe: da 1 a 20 valori fra 20 e 1000 mm.");
        foreach (var (list, min) in new[] { (TopCounts, 2), (BottomCounts, 2), (SideCounts, 0), (CircularCounts, 4) })
            if (list.Length is < 1 or > 30 || list.Any(v => v < min || v > 100))
                throw new ArgumentException($"Numero barre: da 1 a 30 valori interi fra {min} e 100.");
        if (Objective is not ("Peso" or "Numero barre" or "Numero diametri" or "Compromesso") ||
            Stirrups is not ("Ottimizza" or "Mantieni" or "Senza staffe" or "Con e senza staffe"))
            throw new ArgumentException("Obiettivo o modalità staffe non riconosciuti.");
        if (MaxDiameterKinds is < 1 or > 5 || MaxEvaluations is < 1 or > 100000 || MaxParallelism is < 0 or > 8 ||
            !double.IsFinite(HookAllowance) || HookAllowance < 0 || HookAllowance > 100 ||
            !double.IsFinite(TorsionReservePercent) || TorsionReservePercent <= 0 || TorsionReservePercent >= 100 ||
            new[] { WeightCost, BarCost, DiameterCost }.Any(v => !double.IsFinite(v) || v < 0) || WeightCost + BarCost + DiameterCost <= 0)
            throw new ArgumentException("Controllare budget, diametri distinti, sviluppo staffe, riserva torsionale e pesi dell’obiettivo.");
        if (Targets.Any(t => !ConcreteReinforcementDesign.Targets.Any(s => s.Key == t.Key) || !double.IsFinite(t.Value) || t.Value <= 0 || t.Value > 1))
            throw new ArgumentException("Ogni limite η deve essere maggiore di zero e non superiore a 1.");
    }
    public static ConcreteDesignOptions Read(JsonObject o)
    {
        double[] List(string key) => o.S(key).Split([';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => SectionWorkspace.Number(s, key)).Distinct().Order().ToArray();
        int[] Counts(string key) => List(key).Select(v => v == Math.Truncate(v) && v <= int.MaxValue && v >= 0 ? (int)v : throw new ArgumentException(key + ": numeri interi richiesti.")).ToArray();
        double Value(string k) => SectionWorkspace.Number(o.S(k), k);
        var r = new ConcreteDesignOptions
        {
            Diameters = List("diametri"), TopCounts = Counts("n_superiori"), BottomCounts = Counts("n_inferiori"), SideCounts = Counts("n_laterali"), CircularCounts = Counts("n_circolari"),
            StirrupDiameters = List("diametri_staffe"), StirrupSpacings = List("passi_staffe"), Symmetric = o.B("simmetrica"), Stirrups = o.S("staffe"), Objective = o.S("obiettivo"),
            SecondaryDiameters = List("diametri_ortogonali"), SecondarySpacings = List("passi_ortogonali"),
            MaxDiameterKinds = SectionWorkspace.Subdivisions(o.S("max_diametri"), "Diametri distinti", 1, 5),
            MaxEvaluations = o.B("ricerca_completa", true) ? 100000 : SectionWorkspace.Subdivisions(o.S("max_tentativi"), "Tentativi", 1, 100000),
            MaxParallelism = o.S("parallelismo", "Automatico") == "Automatico" ? 0 : SectionWorkspace.Subdivisions(o.S("parallelismo"), "Calcoli contemporanei", 1, 8),
            HookAllowance = Value("ganci_phi"), TorsionReservePercent = Value("riserva_torsione"), WeightCost = Value("peso_peso"), BarCost = Value("peso_barre"), DiameterCost = Value("peso_diametri"),
            AnchorageConfirmed = o.B("ancoraggio_confermato"), TorsionLayoutConfirmed = o.B("torsione_confermata"),
            Targets = ConcreteReinforcementDesign.Targets.ToDictionary(t => t.Key, t => Value(t.Key))
        };
        r.Validate(); return r;
    }
}

public static partial class ConcreteReinforcementDesign
{
    public static readonly (string Key, string Name)[] Targets =
    [ ("slu", "Resistenza plastica N–Mx–My"), ("slv", "Resistenza elastica N–Mx–My"),
      ("sigma_c_rara", "Tensione CLS · rara"), ("sigma_s_rara", "Tensione acciaio · rara"), ("sigma_c_qp", "Tensione CLS · quasi permanente"),
      ("wk_freq", "Fessurazione · frequente"), ("wk_qp", "Fessurazione · quasi permanente"),
      ("vx", "Taglio Vx"), ("vy", "Taglio Vy"), ("torsione", "Torsione"), ("vt_cls", "Taglio–torsione · CLS"), ("vt_staffe", "Taglio–torsione · staffe"),
      ("ancoraggio", "Lunghezza ancoraggio / giunzione") ];
    public static JsonObject Prepare(JsonObject settings)
    {
        if (settings["calcola_armature"] is not null && settings["calcola_armature"] is not JsonObject)
            throw new ArgumentException("Impostazioni della ricerca armature non valide.");
        if (settings["calcola_armature"] is not JsonObject) settings["calcola_armature"] = new JsonObject();
        var o = settings["calcola_armature"]!.AsObject();
        void Default(string key, object value) { if (!o.ContainsKey(key)) o[key] = J.Node(value); }
        foreach (var (k, v) in new[] { ("diametri", "12;16;20;24"), ("n_superiori", "2;4;6"), ("n_inferiori", "2;4;6;8"), ("n_laterali", "0;2;4"),
            ("n_circolari", "8;12;16;20;24"), ("diametri_staffe", "8;10"), ("passi_staffe", "100;150;200;250"), ("staffe", "Ottimizza"), ("obiettivo", "Peso"),
            ("diametri_ortogonali", "8;10;12;16"), ("passi_ortogonali", "100;150;200;250"),
            ("max_diametri", "3"), ("max_tentativi", "2000"), ("ganci_phi", "20"), ("riserva_torsione", "25"), ("peso_peso", "1"), ("peso_barre", "1"), ("peso_diametri", "1"), ("mostra", "10") }) Default(k, v);
        Default("simmetrica", false); Default("ancoraggio_confermato", false); Default("torsione_confermata", false);
        // New and existing sheets default to the full permitted grid. Retain the old budget for an explicit limited run.
        Default("ricerca_completa", true);
        Default("parallelismo", "Automatico");
        foreach (var t in Targets) Default(t.Key, "1");
        return o;
    }
}
