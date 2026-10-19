using System.Text.Json.Nodes;
using Anthea.Reports;
using GPC.Checkers.Geotechnics.Anchors;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Design.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics;
using GPC.Model.Geotechnics.Anchors;
using GPC.Model.Geotechnics.EmbeddedWalls;
using GPC.Model.Standards;
using static Anthea.Application.EmbeddedWalls.EmbeddedWallAdapter;

namespace Anthea.Application.EmbeddedWalls;

/// <summary>Document/provenance mapping and conversions. All bond predictions and coefficients are in GPC.</summary>
internal static class EmbeddedWallAnchorChecks
{
    internal static IReadOnlyList<EmbeddedWallAnchorData> Calculate(JsonObject data, WallAnalysisResult analysis, CancellationToken cancellation)
    {
        string code = Text(data, "anchor_standard"); if (code == "Non richieste") return [];
        if (code is not ("NTC 2018" or "EN 1997-1 — profilo non disponibile")) throw new ArgumentException("Norma sfilamento non riconosciuta.");
        var rows = EmbeddedWallDocument.Rows(data, "anchor_checks").ToArray();
        var layers = EmbeddedWallDocument.Rows(data, "anchor_layers").ToArray();
        var tests = EmbeddedWallDocument.Rows(data, "anchor_tests").ToArray();
        var supports = EmbeddedWallDocument.Rows(data, "supports").Where(s => Text(s, "kind") != "Strut").Select(s => Text(s, "id")).ToHashSet(StringComparer.Ordinal);
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
            if (!supports.Contains(Text(row, "support")) || !declared.Add(Text(row, "support")))
                throw new ArgumentException("Verifica geotecnica duplicata o riferita a un tirante inesistente.");
        foreach (var row in layers.Concat(tests))
            if (!declared.Contains(Text(row, "support"))) throw new ArgumentException("Indagine/prova riferita a una verifica di sfilamento inesistente.");
        var definitions = new List<EmbeddedWallAnchor>(); var diagnostics = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            cancellation.ThrowIfCancellationRequested(); string id = Text(row, "support");
            try
            {
                var evidence = Choice<GroundAnchorEvidence>(row, "evidence");
                var profiles = layers.Where(l => Text(l, "support") == id).GroupBy(l => Text(l, "profile"), StringComparer.Ordinal).Select(group =>
                    new GroundAnchorBond(group.Key, Number(row, "free_length") * SoilUnits.Metre, Number(row, "bond_length") * SoilUnits.Metre,
                        Number(row, "drill_diameter"), Number(row, "minimum_cover") * SoilUnits.Metre, Choice<GroundAnchorInjection>(row, "injection"),
                        group.Select(l => new GroundAnchorBondLayer(Number(l, "axis_start") * SoilUnits.Metre, Number(l, "axis_end") * SoilUnits.Metre,
                            Choice<GroundAnchorSoilKind>(l, "soil_kind"), Number(l, "limit_pressure"), Number(l, "injection_pressure"),
                            Number(l, "diameter_factor"), Text(l, "pressure_source"))), Text(row, "execution_source"), Text(row, "applicability_source"))).ToArray();
                var samples = tests.Where(t => Text(t, "support") == id).Select(t => new GroundAnchorResistanceSample(Text(t, "test_id"),
                    Number(t, "resistance") * NewtonsPerKilonewton, Text(t, "source"))).ToArray();
                var sampleIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var sample in samples)
                    if (string.IsNullOrWhiteSpace(sample.Id) || !sampleIds.Add(sample.Id) || !double.IsFinite(sample.Resistance)
                        || sample.Resistance <= 0 || string.IsNullOrWhiteSpace(sample.Source))
                        throw new ArgumentException("Ogni prova richiede identificativo univoco, resistenza positiva e fonte documentata.");
                if (evidence == GroundAnchorEvidence.CalculatedProfiles && samples.Length > 0 || evidence == GroundAnchorEvidence.MeasuredAnchorTests && profiles.Length > 0)
                    throw new ArgumentException("Non mescolare capacità calcolate e misurate nella stessa popolazione.");
                definitions.Add(new(id, Flag(row, "permanent"), evidence, profiles, samples, Text(row, "experimental_source"), Text(row, "stage_source")));
            }
            catch (ArgumentException e) { diagnostics[id] = "Dati del bulbo non valutabili: " + e.Message; }
        }
        if (!analysis.IsComplete || code != "NTC 2018")
            return Array.AsReadOnly(supports.Select(id => Missing(analysis.CaseId, analysis.FailedStage ?? "", id,
                !analysis.IsComplete ? "Storia meccanica incompleta: sfilamento non verificato." : "Profilo di sfilamento richiesto non disponibile.", code)).ToArray());
        bool basis = Flag(data, "anchor_a1m1") && Text(data, "action_basis") is not ("" or "Da definire");
        var values = EmbeddedWallAnchors.Check(analysis, definitions, new StandardNTC2018Geotechnics(),
            Choice<GeotechnicalSituation>(data, "anchor_situation"), basis, cancellation);
        return Array.AsReadOnly(values.Select(v =>
        {
            var c = v.Verification;
            var samples = v.Predictions.Count > 0
                ? v.Predictions.Select(p => new EmbeddedWallAnchorSample(p.Input.Id, p.Resistance / NewtonsPerKilonewton, p.Applicable,
                    p.Input.ExecutionSource + "; " + p.Input.ApplicabilitySource, string.Join(" ", p.Diagnostics))).ToArray()
                : (c?.Samples ?? []).Select(s => new EmbeddedWallAnchorSample(s.Id, s.Resistance / NewtonsPerKilonewton, true, s.Source, "")).ToArray();
            var segments = v.Predictions.SelectMany(p => p.Segments.Select(s => new EmbeddedWallAnchorSegment(p.Input.Id, s.Layer,
                s.Start / SoilUnits.Metre, s.End / SoilUnits.Metre, s.LimitPressure, s.InjectionPressure, s.DiameterFactor, s.BulbDiameter,
                s.Curve, s.UnitResistance, s.Resistance / NewtonsPerKilonewton, s.PressureSource))).ToArray();
            return new EmbeddedWallAnchorData(v.CaseId, v.StageId, v.SupportId, v.NormalForce / NewtonsPerKilonewton,
                c?.MeanResistance / NewtonsPerKilonewton, c?.MinimumResistance / NewtonsPerKilonewton, c?.CharacteristicResistance / NewtonsPerKilonewton,
                c?.DesignResistance / NewtonsPerKilonewton, c?.Utilization, c?.Profile?.MeanCorrelationFactor, c?.Profile?.MinimumCorrelationFactor,
                c?.Profile?.ResistanceFactor, v.Outcome, c?.Profile?.Reference ?? code,
                (c?.Profile?.Evidence.ToString() ?? "") + "; " + (c?.ExperimentalSource ?? ""), diagnostics.GetValueOrDefault(v.SupportId, v.Diagnostic),
                Array.AsReadOnly(samples), Array.AsReadOnly(segments));
        }).ToArray());
    }
    private static EmbeddedWallAnchorData Missing(string caseId, string stage, string support, string diagnostic, string source)
        => new(caseId, stage, support, null, null, null, null, null, null, null, null, null, EngineeringOutcome.NotEvaluated, source, "", diagnostic, [], []);
}
