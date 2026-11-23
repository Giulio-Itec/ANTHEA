using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Reports;

/// <summary>Dati già calcolati, indipendenti dalla vista e dal motore. Nessuna formula è valutata dal report.</summary>
public sealed record QuickReportValue(string Label, string Value, string Unit = "", string Note = "");
public sealed record QuickReportCheck(string Kind, string Value, string Detail, double? Ratio, bool? Passed, string Limits = "");
public sealed record QuickReportCase(string Id, string Name, string State, string Model, string Sign,
    string N, string Mx, string My, string Status, double? Ratio, string Behaviour,
    IReadOnlyList<QuickReportValue> Values, IReadOnlyList<QuickReportCheck> Checks);
public sealed record QuickSectionReportData(string Shape, bool Straight, string Exposure,
    IReadOnlyList<QuickReportValue> Geometry, IReadOnlyList<QuickReportValue> Materials,
    IReadOnlyList<string[]> Reinforcement, IReadOnlyList<QuickReportCase> Cases, IReadOnlyList<string> Warnings,
    QuickShearReport? Shear = null);
public sealed record QuickShearReport(IReadOnlyList<QuickReportValue> Parameters, IReadOnlyList<QuickShearReportRow> Rows, bool UsesGeometryStirrup = false);
public sealed record QuickShearReportRow(string Name, string Condition, string N, string Action, string Axis,
    string SteelResistance, string ConcreteResistance, string Resistance, double? Ratio, bool? Passed, string Status, string Model = "")
{
    public string CheckLabel => Model.Length == 0 || Model == Condition ? Condition : Condition + " → " + Model;
}

public static class QuickSectionReport
{
    public const string Module = "str_sezione_ca_rapida";
    public const string Version = "sezione_rapida_contenuti_v1";
    public const string Brief = "sezione_rapida_breve";
    public static string StateName(string state) => state switch
    {
        "SLE" => "SLE rara", "SLE_FREQ" => "SLE frequente", "SLE_QP" => "SLE quasi permanente", _ => state
    };
    static PileReportContent Group(string key, string label, params PileReportContent[] children) => new(key, label, Items: children);
    public static IReadOnlyList<PileReportContent> Outline(IEnumerable<string>? states = null)
    {
        var present = (states ?? ["SLU", "SLV", "SLE", "SLE_FREQ", "SLE_QP"]).ToHashSet();
        return [new("intestazione", "Copertina", false), new("indice", "Indice", false),
            Group("q1", "Oggetto della verifica", new PileReportContent("oggetto", "Ambito, assi e unità di misura")),
            Group("q2", "Geometria e armature", new("geometria", "Dimensioni e armature"), new("sezione", "Sezione quotata")),
            Group("q3", "Materiali e teoria di calcolo", new("materiali", "Materiali e coefficienti"), new("teoria", "Criteri di verifica")),
            Group("q4", "Azioni e combinazioni", new PileReportContent("azioni", "Combinazioni nell’ordine del programma")),
            Group("q5", "Risultati delle verifiche", new[] { new PileReportContent("riepilogo", "Riepilogo di tutte le combinazioni") }
                .Concat(new[] { "SLU", "SLV", "SLE", "SLE_FREQ", "SLE_QP" }.Where(present.Contains)
                    .Select(s => new PileReportContent("risultati_" + s, StateName(s)))).ToArray()),
            Group("q6", "Diagrammi e approfondimenti", new("dominio", "Dominio e punto resistente"), new("tensioni", "Gradiente delle tensioni"), new("curvatura", "Momento–curvatura", false)),
            new("avvisi", "Avvisi del calcolo", false)];
    }
    public static HashSet<string> Defaults() => Outline().SelectMany(c => c.Descendants())
        .Where(c => c.Children.Count == 0 && c.DefaultSelected).Select(c => c.Key).Append(Version).ToHashSet();

    /// <summary>Casi governanti distinti per stato e verifica, più tutti i casi con esito non determinato.</summary>
    public static IReadOnlyList<(QuickReportCase Case, QuickReportCheck Check)> Governing(QuickSectionReportData data) => data.Cases
        .SelectMany(c => c.Checks.Select(k => (Case: c, Check: k))).GroupBy(p => (p.Case.State, p.Check.Kind))
        .SelectMany(g => g.Where(p => p.Check.Ratio is null || p.Check.Passed is null)
            .Concat(g.Where(p => p.Check.Ratio is not null && p.Check.Passed is not null).OrderByDescending(p => p.Check.Ratio).Take(1))).ToArray();

    public static DocxDocument Create(string title, QuickSectionReportData data, IReadOnlySet<string> options,
        IReadOnlyList<ImmagineReport>? images = null) => ItecPileReport.CreateQuickSection(title, data, options, images ?? []);
}
