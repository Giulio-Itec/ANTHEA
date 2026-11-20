using X.Core;

namespace Anthea.Reports;

public sealed record BridgeQuickReportCase(string Id, string Name, string State, IReadOnlyList<string> Actions,
    IReadOnlyList<QuickReportCheck> Checks, IReadOnlyList<QuickReportValue> Values, IReadOnlyList<string[]> Phases,
    IReadOnlyList<string[]> Stresses, IReadOnlyList<string[]> Shear, double? Ratio, string Status);
public sealed record BridgeQuickReportData(string Scope, string Standard, IReadOnlyList<QuickReportValue> Geometry,
    IReadOnlyList<QuickReportValue> Materials, IReadOnlyList<string[]> Reinforcement, IReadOnlyList<BridgeQuickReportCase> Cases,
    IReadOnlyList<string> Warnings);

public static class BridgeQuickReport
{
    public const string Module = "str_sezione_composta_rapida", Version = "composta_rapida_contenuti_v1", Brief = "composta_rapida_breve";
    public static IReadOnlyList<PileReportContent> Outline() =>
    [
        new("intestazione", "Intestazione e copertina", false), new("indice", "Indice", false),
        new("oggetto", "Oggetto, criteri e limiti della verifica"), new("geometria", "Geometria e armature"),
        new("materiali", "Materiali, normativa e coefficienti"), new("azioni", "Azioni e combinazioni"),
        new("risultati", "Verifiche di tutte le combinazioni"), new("sezione", "Sezione quotata"),
        new("tensioni", "Tensioni totali delle combinazioni"), new("avvisi", "Avvisi del motore")
    ];
    public static HashSet<string> Defaults() => Outline().Where(c => c.DefaultSelected).Select(c => c.Key).Append(Version).ToHashSet();
    public static IReadOnlyList<(BridgeQuickReportCase Case, QuickReportCheck Check)> Governing(BridgeQuickReportData data) => data.Cases
        .SelectMany(c => c.Checks.Select(k => (Case: c, Check: k)))
        .GroupBy(p => (p.Case.State, Kind: p.Check.Kind is "Taglio · instabilità dell’anima" or "Anima · tensione equivalente" ? p.Check.Kind : "Tensioni"))
        .SelectMany(g => g.Where(p => p.Check.Ratio is null).Concat(g.Where(p => p.Check.Ratio is not null).OrderByDescending(p => p.Check.Ratio).Take(1))).ToArray();
    public static DocxDocument Create(string title, BridgeQuickReportData data, IReadOnlySet<string> options, IReadOnlyList<ImmagineReport> images) =>
        ItecPileReport.CreateQuickBridge(title, data, options, images);
}
