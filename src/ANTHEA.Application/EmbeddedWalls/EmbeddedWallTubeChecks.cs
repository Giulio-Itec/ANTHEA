using System.Text.Json.Nodes;
using Anthea.Reports;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Design.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Steel;
using GPC.Model.Geotechnics;
using GPC.Model.Materials;
using GPC.Model.Sections;
using GPC.Model.Sections.Steel;
using GPC.Model.Standards;
using static Anthea.Application.EmbeddedWalls.EmbeddedWallAdapter;

namespace Anthea.Application.EmbeddedWalls;

/// <summary>Document mapping and unit conversion only. Geometry is in Model, resistance in Steel, strip representation in Design.</summary>
internal static class EmbeddedWallTubeChecks
{
    private static EmbeddedWallSteelTube Read(JsonObject row)
    {
        var material = new SteelMaterialEN1993("S" + Text(row, "nominal_grade"), Number(row, "elastic_modulus"), Number(row, "fy"), Number(row, "fu"));
        var section = new SteelSection(new SectionCHS(Number(row, "diameter"), Number(row, "thickness")), material);
        var definition = new EmbeddedWallSteelTube(Text(row, "id"), new CircularHollowSteelSection(section, Integer(row, "nominal_grade"),
            Text(row, "material_source"), Flag(row, "unperforated"), Flag(row, "is_bridge")), Number(row, "spacing") * SoilUnits.Metre,
            Flag(row, "single_row"), Text(row, "representation_source"));
        definition.Validate(); return definition;
    }
    internal static double Rigidity(JsonObject data, string section, double width)
    {
        var rows = EmbeddedWallDocument.Rows(data, "steel_tubes").Where(r => Text(r, "id") == section).ToArray();
        if (rows.Length != 1) throw new ArgumentException("Per EI da CHS serve una sola definizione completa della sezione " + section + ".");
        return Read(rows[0]).FlexuralRigidity(width);
    }
    internal static IReadOnlyList<EmbeddedWallTubeData> Calculate(JsonObject data, WallAnalysisResult analysis, double width, CancellationToken cancellation)
    {
        string code = Text(data, "tube_standard"); if (code == "Non richieste") return [];
        StandardEN1993p11 standard = code switch {
            "NTC 2018" => new StandardNTC2018Steel(), "EN 1993-1-1 — profilo non disponibile" => new StandardEN1993p11(),
            _ => throw new ArgumentException("Norma sezioni metalliche non riconosciuta.") };
        var kind = Choice<EmbeddedWallSteelCheckKind>(data, "tube_check_kind");
        var ids = EmbeddedWallDocument.Rows(data, "segments").Select(r => Text(r, "section")).ToHashSet(StringComparer.Ordinal);
        var defined = new HashSet<string>(StringComparer.Ordinal); var tubes = new Dictionary<string, EmbeddedWallSteelTube>(StringComparer.Ordinal);
        var diagnostics = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in EmbeddedWallDocument.Rows(data, "steel_tubes"))
        {
            cancellation.ThrowIfCancellationRequested(); string id = Text(row, "id");
            if (!ids.Contains(id) || !defined.Add(id)) throw new ArgumentException("Sezione CHS duplicata o non usata nei tratti: " + id);
            try { tubes.Add(id, Read(row)); }
            catch (ArgumentException e) { diagnostics.Add(id, "Dati della sezione CHS non valutabili: " + e.Message); }
        }
        if (!analysis.IsComplete) return Array.AsReadOnly(ids.Select(id => new EmbeddedWallTubeData(analysis.CaseId, analysis.FailedStage ?? "", id, null, "",
            null, null, null, null, null, null, null, null, null, EngineeringOutcome.NotEvaluated, "Storia incompleta: resistenza delle sezioni non verificata.", code, null, [])).ToArray());
        bool classified = !string.IsNullOrWhiteSpace(Text(data, "action_basis")) && Text(data, "action_basis") != "Da definire";
        var results = EmbeddedWallSteelTubes.Check(analysis, width, tubes.Values.ToArray(), standard, classified ? kind : EmbeddedWallSteelCheckKind.Analysis, cancellation);
        return Array.AsReadOnly(results.Select(r =>
        {
            var v = r.Verification; tubes.TryGetValue(r.SectionId, out var tube);
            var properties = tube is null ? null : new EmbeddedWallTubeProperties(tube.Section.Diameter, tube.Section.Thickness, tube.Section.Area,
                tube.Section.Inertia, tube.Spacing / SoilUnits.Metre, tube.Section.ElasticModulus, tube.Section.YieldStrength,
                tube.SingleRowEqualSharingConfirmed && !string.IsNullOrWhiteSpace(tube.RepresentationSource) ? tube.FlexuralRigidity(width) / NmmSquaredPerKilonewtonMetreSquared : null,
                tube.Section.MaterialSource + "; " + tube.RepresentationSource);
            return new EmbeddedWallTubeData(r.CaseId, r.StageId, r.SectionId, r.Elevation / SoilUnits.Metre, r.Side,
                r.NormalForce / NewtonsPerKilonewton, r.Shear / NewtonsPerKilonewton, r.Moment / NmmPerKilonewtonMetre, r.StripToMemberFactor,
                v?.SectionClass, v?.PartialFactor, v?.ShearReduction, v?.NeutralAxisDistance, v?.AxialEquilibriumResidual,
                r.Outcome, diagnostics.GetValueOrDefault(r.SectionId, r.Diagnostic), code + "; " + kind, properties,
                Array.AsReadOnly((v?.Checks ?? []).Select(c => new EmbeddedWallTubeCheckData(c.Method, c.Reference, c.Unit == "Nmm" ? "kNm" : c.Unit == "N" ? "kN" : c.Unit,
                    c.Demand / (c.Unit == "Nmm" ? NmmPerKilonewtonMetre : NewtonsPerKilonewton),
                    c.Capacity / (c.Unit == "Nmm" ? NmmPerKilonewtonMetre : NewtonsPerKilonewton), c.Utilization, c.Outcome, c.Diagnostic)).ToArray()));
        }).ToArray());
    }
}
