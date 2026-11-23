using System.Text.Json.Nodes;
using Anthea.Reports;
using X.Core;

namespace Anthea.Application.Concrete;

/// <summary>Adattatore del report: richiama le verifiche esistenti su copie isolate, senza modificare il foglio.</summary>
public sealed record ConcreteQuickReportAnalysis(JsonObject Data, QuickSectionReportData Report,
    IReadOnlyDictionary<string, ConcreteQuickResult> Results)
{
    public static ConcreteQuickReportAnalysis Create(JsonObject source, ConcreteQuickResult? current = null,
        bool curvature = false, CancellationToken cancellation = default)
    {
        var data = (JsonObject)source.DeepClone();
        ConcreteQuickSection.Validate(data);
        var rows = ConcreteQuickSection.Combinations(data);
        var results = new Dictionary<string, ConcreteQuickResult>();
        var cases = new List<QuickReportCase>();
        var warnings = new List<string>();
        foreach (var row in rows)
        {
            cancellation.ThrowIfCancellationRequested();
            string id = row.S("id"), state = row.S(ConcreteQuickSection.StateField);
            // Retain one normalized action, with its own model/sign settings. All mechanics remain in Calculate.
            var copy = (JsonObject)data.DeepClone();
            var action = (JsonObject)row.DeepClone(); action["stato"] = state;
            var sets = new JsonObject();
            foreach (string key in SectionWorkspace.Sets)
                sets[key] = ConcreteSheetPreparation.StoreActions(key == state ? [action] : []);
            copy["combinazioni"] = sets;
            if (state is "SLU" or "SLV")
            {
                ConcreteQuickSection.WriteCombinationSetting(copy, id, ConcreteQuickSection.ModelField, row.S(ConcreteQuickSection.ModelField));
                ConcreteQuickSection.WriteCombinationSetting(copy, id, ConcreteQuickSection.SignField, row.S(ConcreteQuickSection.SignField));
            }
            ConcreteQuickSection.SelectCombination(copy, id);
            ConcreteQuickSection.View(copy)["grafico"] = ConcreteQuickSection.Charts[curvature ? 1 : 0];
            var result = current?.SelectedId == id && (!curvature || current.Curvature is not null)
                ? current : ConcreteQuickSection.Calculate(copy, cancellation);
            results.Add(id, result);
            var summary = ConcreteQuickSection.Summary(result)!;
            var outcome = result.Combinations.FirstOrDefault(c => c.Id == id);
            var checks = new List<QuickReportCheck>();
            QuickReportCheck Check(string kind, ConcreteQuickSummary s) => new(kind, s.Value, s.Detail, s.Ratio, s.Passed,
                kind == "Tensioni" && result.ServiceState is { } stress
                    ? "|σc,comp| " + EngineeringFormat.Number(stress.ConcreteStressLimit) + " MPa; σs " + EngineeringFormat.Number(stress.SteelStressLimit) + " MPa" : "");
            if (state == "SLE_QP")
            {
                checks.Add(summary.Secondary is { } stresses ? Check("Tensioni", stresses)
                    : new("Tensioni", "—", result.ServiceStatus, null, null));
                checks.Add(Check("Fessurazione", summary));
            }
            else checks.Add(Check(state is "SLU" or "SLV" ? "Resistenza" : state == "SLE_FREQ" ? "Fessurazione" : "Tensioni", summary));
            cases.Add(new(id, row.S("nome"), state, state is "SLU" or "SLV" ? ConcreteQuickSection.ModelLabel(result.Model) : "Metodo n",
                result.Straight && state is "SLU" or "SLV" ? ConcreteQuickSection.MethodLabel(result.Method) : "—",
                row.S("N"), row.S("Mx"), result.Straight ? "0 (ignorato)" : row.S("My"), outcome?.Status ?? "Non disponibile", outcome?.Ratio,
                state is "SLU" or "SLV" ? summary.Behaviour : "", ConcreteQuickSection.ResultValues(result).Select(Value).ToArray(), checks));
            warnings.AddRange(result.Errors.Select(e => row.S("nome") + ": " + e));
        }
        var first = results.Values.FirstOrDefault() ?? current ?? ConcreteQuickSection.Calculate(data, cancellation);
        var input = data["input"]!.AsObject(); var geometry = first.Preview.Geometry;
        string N(double? n) => EngineeringFormat.Number(n);
        var dimensions = new List<QuickReportValue> { new("Forma", geometry.Shape), new("Larghezza", N(geometry.Width), "mm"),
            new("Altezza", N(geometry.Height), "mm"), new("Area calcestruzzo", N(geometry.AreaCls), "mm²"), new("Area armatura", N(geometry.AreaSteel), "mm²") };
        foreach (var (key, label, unit) in new[] { ("cover_mm", "Copriferro netto della staffa", "mm"), ("transverse_bar_diameter_mm", "Diametro staffa", "mm") })
            if (geometry.Shape != ConcreteQuickSection.Dxf && input[key] is not null) dimensions.Add(new(label, input.S(key), unit));
        if (geometry.Shape == ConcreteQuickSection.Dxf && input[Anthea.ConcreteWorkspace.ConcreteFreeSectionAdapter.Key] is JsonObject dxf)
        {
            dimensions.Add(new("Disegno DXF", dxf.S("file")));
            dimensions.Add(new("Unità del disegno originale", dxf.S("unita_origine")));
            dimensions.Add(new("Fori", geometry.Holes.Count.ToString()));
            dimensions.Add(new("Coordinate", "Riferite al baricentro del calcestruzzo; geometria e barre conservate nel documento"));
            dimensions.Add(new("Staffe", "Non importate dal DXF"));
        }
        // 19/11/2026: the link of the second ring of the circular section, which places the second ring.
        if (ConcreteQuickSection.SecondRingLinkDiameter(data) > 0)
            dimensions.Add(new("Diametro staffa del secondo anello", ConcreteQuickSection.SecondRingLink(data), "mm"));
        var materials = new List<QuickReportValue>();
        foreach (var (key, label, unit) in new[] { ("classe_cls", "Calcestruzzo", ""), ("classe_acciaio", "Acciaio", ""),
            ("fck_mpa", "fck", "MPa"), ("fyk_mpa", "fyk", "MPa"), ("steel_modulus_mpa", "Es", "MPa"),
            ("cls_diagramma", "Legame calcestruzzo", ""), ("steel_diagramma", "Legame acciaio", ""), ("steel_eps_u", "εuk", "‰") })
            if (input[key] is not null) materials.Add(new(label, input.S(key), unit));
        var settings = SectionWorkspace.Prepare(data);
        var standard = Anthea.Calculations.ConcreteStandards.Effective(input, settings);
        materials.Add(new("Normativa", Anthea.Calculations.ConcreteStandards.ResultName(settings)));
        materials.Add(new("γc", N(standard.GammaC))); materials.Add(new("γs", N(standard.GammaS))); materials.Add(new("αcc", N(standard.AlphaCC)));
        materials.AddRange(ConcreteQuickSection.MaterialValues(data, first).Select(Value));
        // Coordinates and diameters are the geometry consumed by the engine, including legacy T sections and circular rings.
        var bars = geometry.Bars.Select((b, i) => new[] { (i + 1).ToString(), N(b.X), N(b.Y), N(b.Diametro), N(b.Area) }).ToArray();
        QuickShearReport? shear = null;
        if (ConcreteQuickShear.Enabled(data))
        {
            var options = ConcreteQuickShear.Options(data);
            var parameters = new List<QuickReportValue>
            {
                new("Modello", "Per combinazione"),
                new("Staffa effettiva?", ConcreteQuickShear.UsesGeometryLink(data) ? "Sì: diametro della staffa geometrica" : "No: armatura a taglio indipendente; la staffa geometrica non contribuisce"),
                new("Ø resistente", ConcreteQuickShear.LinkDiameter(data), "mm"), new("Passo resistente", options.S(ConcreteQuickShear.Spacing), "mm"),
                new("Rami Vx / Vy", options.S("rami_x") + " / " + options.S("rami_y")),
                new("α Vx / Vy", options.S("alpha_x") + " / " + options.S("alpha_y"), "°"),
                new("cot θ Vx / Vy", (options.S("cot_x") is { Length: > 0 } cx ? cx : "auto") + " / " + (options.S("cot_y") is { Length: > 0 } cy ? cy : "auto")),
                new("Parametri geometrici", options.S("parametri")), new("Ancoraggio Asl", options.S("ancoraggio"))
            };
            if (geometry.Shape == ConcreteQuickSection.Circular) { parameters.Add(new("Modello circolare", options.S("modello_circolare"))); parameters.Add(new("z/d", options.S("z_d"))); }
            if (options.S("parametri") == "Manuali")
                foreach (string axis in new[] { "x", "y" }) foreach (string field in new[] { "bw", "d", "asl" }) parameters.Add(new(field + " V" + axis, options.S(field + "_" + axis), field == "asl" ? "mm²" : "mm"));
            var shearRows = new List<QuickShearReportRow>();
            foreach (var item in ConcreteQuickShear.Calculate(data, cancellation))
            {
                var action = ConcreteQuickShear.Combinations(data).Single(r => r.S("id") == item.Id);
                if (item.Error.Length > 0) shearRows.Add(new(item.Name, ConcreteQuickShear.ShortLabel(item.Condition), action.S("N"), action.S("Vx", "0") + " / " + action.S("Vy", "0"), "X/Y", "—", "—", "—", null, null, item.Error, ConcreteQuickShear.ShortLabel(item.Model)));
                foreach (var direction in item.Directions)
                    shearRows.Add(new(item.Name, ConcreteQuickShear.ShortLabel(item.Condition), action.S("N"), direction.Action, direction.Axis, direction.SteelResistance, direction.ConcreteResistance,
                        direction.Resistance, direction.Ratio, direction.Passed, direction.Status, ConcreteQuickShear.ShortLabel(item.Model)));
            }
            shear = new(parameters, shearRows, ConcreteQuickShear.UsesGeometryLink(data));
        }
        var report = new QuickSectionReportData(geometry.Shape, first.Straight, ConcreteQuickSection.Exposure(data), dimensions, materials, bars, cases, warnings.Distinct().ToArray(), shear);
        return new(data, report, results);
    }
    static QuickReportValue Value(ConcreteQuickValue value) => new(value.Label, value.Value, value.Unit, value.Note);
}
