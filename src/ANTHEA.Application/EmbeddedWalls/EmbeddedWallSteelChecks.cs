using System.Text.Json.Nodes;
using Anthea.Reports;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Design.EmbeddedWalls;
using GPC.Geometry;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Steel;
using GPC.Model.Geotechnics;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using static Anthea.Application.EmbeddedWalls.EmbeddedWallAdapter;

namespace Anthea.Application.EmbeddedWalls;

/// <summary>Input mapping, unit conversion and invocation only. Section resistance is calculated by GPCChecker.Steel.</summary>
public static class EmbeddedWallSteelChecks
{
    public static IReadOnlyList<EmbeddedWallSteelData> Calculate(JsonObject data, WallAnalysisResult analysis, CancellationToken cancellation)
    {
        string code = Text(data, "steel_standard"); if (code == "Non richieste") return [];
        StandardEN1993p11 standard = code switch
        {
            "NTC 2018" => new StandardNTC2018Steel(),
            "EN 1993-1-1 — profilo non disponibile" => new StandardEN1993p11(),
            _ => throw new ArgumentException("Norma delle membrature metalliche non riconosciuta.")
        };
        var kind = Choice<EmbeddedWallSteelCheckKind>(data, "steel_check_kind");
        var definitions = new List<EmbeddedWallSteelSupport>(); var diagnostics = new Dictionary<string, string>(StringComparer.Ordinal);
        var knownIds = EmbeddedWallDocument.Rows(data, "supports").Select(s => Text(s, "id")).ToHashSet(StringComparer.Ordinal);
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in EmbeddedWallDocument.Rows(data, "steel_members"))
        {
            cancellation.ThrowIfCancellationRequested(); string id = Text(row, "support");
            if (!knownIds.Contains(id) || !declared.Add(id)) throw new ArgumentException("Definizione metallica duplicata o riferita a un sostegno inesistente: " + id);
            try
            {
                ISectionShape shape = Text(row, "family") switch
                {
                    "CHS" => new SectionCHS(Number(row, "diameter"), Number(row, "thickness")),
                    "Tondo pieno" => new SectionCircular(Number(row, "diameter")),
                    _ => throw new ArgumentException("Famiglia della sezione metallica non supportata.")
                };
                var material = new SteelMaterialEN1993("S" + Text(row, "nominal_grade"), Number(row, "elastic_modulus"), Number(row, "fy"), Number(row, "fu"));
                var section = new SteelSection(shape, material, formedType: Choice<Section.FormedTypes>(row, "formation"));
                definitions.Add(new(id, new AxialSteelMember(section, Integer(row, "nominal_grade"), Text(row, "material_source"), Flag(row, "axial_only"),
                    Optional(row, "length_1") * SoilUnits.Metre, Optional(row, "length_2") * SoilUnits.Metre, Text(row, "length_source"),
                    Optional(row, "net_area"), Text(row, "net_area_source"), Flag(row, "is_bridge"))));
            }
            catch (ArgumentException e) { diagnostics.Add(id, "Dati della membratura non valutabili: " + e.Message); }
        }
        string basis = Text(data, "action_basis");
        bool basisMissing = string.IsNullOrWhiteSpace(basis) || basis == "Da definire";
        if (!analysis.IsComplete)
            return Array.AsReadOnly(knownIds.OrderBy(id => id, StringComparer.Ordinal).Select(id => new EmbeddedWallSteelData(analysis.CaseId,
                analysis.FailedStage ?? "", id, "", null, 0, false, null, EngineeringOutcome.NotEvaluated,
                "Storia meccanica incompleta: verifiche dei sostegni non eseguite.", code, [])).ToArray());
        var results = EmbeddedWallSteel.CheckSupports(analysis, definitions, standard,
            basisMissing ? EmbeddedWallSteelCheckKind.Analysis : kind, cancellation);
        return Array.AsReadOnly(results.Select(r => new EmbeddedWallSteelData(r.CaseId, r.StageId, r.SupportId, r.Kind.ToString(),
            r.NormalForce / NewtonsPerKilonewton, r.AnalysedAxialStiffness, r.AtModelCapacity, r.Verification?.CompressionClass, r.Outcome,
            basisMissing ? "Dichiarare provenienza e coefficienti delle azioni della combinazione; nessuna verifica su azioni non classificate."
                : diagnostics.GetValueOrDefault(r.SupportId, r.Diagnostic),
            code + "; " + kind + (r.Verification is { } v ? "; " + v.Member.MaterialSource : ""),
            Array.AsReadOnly((r.Verification?.Checks ?? []).Select(c => new EmbeddedWallSteelCheckData(c.Method, c.Reference,
                c.Demand / NewtonsPerKilonewton, c.Capacity / NewtonsPerKilonewton, c.Utilization, c.Outcome, c.PartialFactor,
                c.CriticalForce / NewtonsPerKilonewton, c.NormalizedSlenderness, c.GeometricSlenderness, c.Reduction, c.Curve, c.Diagnostic)).ToArray()))).ToArray());
    }
}
