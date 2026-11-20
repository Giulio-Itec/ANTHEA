using System.Text.Json.Nodes;
using Anthea.Reports;

namespace Anthea.Application.Bridge;

public sealed record BridgeQuickReportAnalysis(JsonObject Data, BridgeQuickResult Result, BridgeQuickReportData Report)
{
    public static BridgeQuickReportAnalysis Create(JsonObject source, BridgeQuickResult? current = null, CancellationToken cancellation = default)
    {
        var data = (JsonObject)source.DeepClone(); BridgeQuickSection.Validate(data);
        var result = current ?? BridgeQuickSection.Calculate(data, cancellation);
        string N(double? value) => EngineeringFormat.Number(value);
        var geometry = new List<QuickReportValue>();
        foreach (var (key, label) in new[] { ("h_trave", "Altezza totale trave H"), ("t_web", "Spessore anima"), ("b_top", "Larghezza piattabanda superiore"),
            ("t_top", "Spessore piattabanda superiore"), ("b_bottom", "Larghezza piattabanda inferiore"), ("t_bottom", "Spessore piattabanda inferiore"),
            ("b_cls", "Larghezza collaborante soletta"), ("h_cls", "Spessore soletta") }) geometry.Add(new(label, data.S(key), "mm"));
        var materials = new List<QuickReportValue>
        { new("Calcestruzzo", data.S("classe_cls")), new("Carpenteria", data.S("acciaio")), new("Armatura", data.S("armatura")), new("Normativa", data.S("normativa")) };
        var first = result.Cases.FirstOrDefault(c => c.Calculation is not null)?.Calculation;
        foreach (string material in new[] { "cls", "acciaio", "armatura" }) materials.AddRange(BridgeQuickSection.MaterialValues(first, material));
        foreach (string key in new[] { "gamma_m0", "gamma_m1", "gamma_c", "alpha_cc", "gamma_s", "eta_taglio" }) if (data.S(key).Length > 0) materials.Add(new(key, data.S(key)));
        var areas = BridgeQuickSection.BarAreas(data, first?.Geometry);
        var bars = new[] { "top", "bottom" }.Select((side, index) => new[] { index == 0 ? "Superiore" : "Inferiore", data.B("rebars_" + side) ? "sì" : "no",
            data.S("d_" + side), data.S("pitch_" + side), data.S("cover_" + side), areas[index] }).ToArray();
        var cases = new List<BridgeQuickReportCase>();
        foreach (var item in result.Cases)
        {
            cancellation.ThrowIfCancellationRequested();
            var row = BridgeQuickSection.Combinations(data).Single(r => r.S("id") == item.Id);
            var stages = item.Calculation?.Stages ?? [];
            var checks = item.Checks.Select(c => new QuickReportCheck(c.Heading, c.Value, c.Detail, c.Ratio, c.Passed)).ToList();
            if (checks.Count == 0) checks.Add(new("Esito", "Non determinato", item.Error, null, null));
            var phases = row["fasi"]!.AsArray().OfType<JsonObject>().Select(p => new[] { p.S("nome"), p.S("tipo"), p.S("Mx"), p.S("N"), p.S("V"),
                p.S("phi"), p.S("psi"), p.S("epsilon_cs") }).ToArray();
            var stresses = stages.SelectMany(s => s.Points.Where(p => p.Active).Select(p => new[] { s.Name, p.Name, N(p.Stress), N(p.Limit), N(p.Utilization) })).ToArray();
            var shear = stages.SelectMany(s => (s.Shear?.Checks ?? []).Select(c => new[] { s.Name, c.Name, N(c.Demand), N(c.Resistance), c.Unit, N(c.Ratio) })).ToArray();
            cases.Add(new(item.Id, item.Name, item.State, BridgeQuickSection.ActionKeys.Select(k => BridgeQuickSection.Action(row, k)).ToArray(), checks,
                item.Values, phases, stresses, shear, item.Ratio, item.Status));
        }
        var warnings = result.Cases.SelectMany(c => c.Warnings.Concat(c.Error.Length > 0 ? new[] { c.Name + ": " + c.Error } : [])).Distinct().ToArray();
        return new(data, result, new(BridgeQuickSection.Scope, data.S("normativa"), geometry, materials, bars, cases, warnings));
    }
}
