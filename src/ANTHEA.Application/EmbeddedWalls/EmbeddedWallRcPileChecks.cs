using System.Text.Json.Nodes;
using Anthea.Reports;
using GPC.Checkers.Concrete.Piles;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Design.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Design.Concrete;
using GPC.Model.Geotechnics;
using GPC.Model.Standards;
using static Anthea.Application.EmbeddedWalls.EmbeddedWallAdapter;

namespace Anthea.Application.EmbeddedWalls;

/// <summary>Document and display unit mapping only. Geometry Model; section checks Concrete; strip transfer Design.</summary>
internal static class EmbeddedWallRcPileChecks
{
    private static EmbeddedWallRcPile Read(JsonObject row)
    {
        var section = new CircularRcSection(Number(row, "diameter"), Number(row, "cover"), Integer(row, "bar_count"), Number(row, "bar_diameter"),
            Number(row, "link_diameter"), Number(row, "link_spacing"), Number(row, "fck"), Number(row, "fyk"), Number(row, "es"),
            Number(row, "eps_uk"), Text(row, "material_source"), Integer(row, "outline_divisions"));
        var definition = new EmbeddedWallRcPile(Text(row, "id"), section, Number(row, "spacing") * SoilUnits.Metre,
            Flag(row, "single_row"), Text(row, "representation_source"), Flag(row, "shear_confirmed"), Number(row, "lever_factor"),
            Number(row, "creep"), Text(row, "exposure"), Number(row, "aggregate"), Optional(row, "durability_cover"), Number(row, "cover_deviation"),
            Choice<PileDetailingMode>(row, "detailing_mode"), Flag(row, "compression_restrained"), Flag(row, "end_zones_confirmed"));
        definition.Validate(); return definition;
    }
    internal static double Rigidity(JsonObject data, string section, double width)
    {
        var rows = EmbeddedWallDocument.Rows(data, "rc_piles").Where(r => Text(r, "id") == section).ToArray();
        if (rows.Length != 1) throw new ArgumentException("Per EI dal palo serve una definizione completa e univoca: " + section);
        return Read(rows[0]).FlexuralRigidity(width);
    }
    internal static IReadOnlyList<EmbeddedWallRcPileData> Calculate(JsonObject data, WallAnalysisResult analysis, double width, CancellationToken cancellation)
    {
        string standard = Text(data, "rc_pile_standard"), kindText = Text(data, "rc_pile_check_kind");
        if (standard == "Non richieste") return [];
        var ids = EmbeddedWallDocument.Rows(data, "segments").Select(r => Text(r, "section")).ToHashSet(StringComparer.Ordinal);
        EmbeddedWallRcPileData Missing(string id, string stage, string message) => new(analysis.CaseId, stage, id, null, "", null, null, null, null,
            EngineeringOutcome.NotEvaluated, message, standard + "; " + kindText, null, [], []);
        bool classified = !string.IsNullOrWhiteSpace(Text(data, "action_basis")) && Text(data, "action_basis") != "Da definire";
        if (!analysis.IsComplete) return ids.Select(id => Missing(id, analysis.FailedStage ?? "", "Storia incompleta: sezioni circolari non verificate.")).ToArray();
        if (standard != "NTC 2018" || !classified || !Enum.TryParse<EmbeddedWallSectionCheckKind>(kindText, out var kind) || !Enum.IsDefined(kind))
            return analysis.Stages.SelectMany(stage => ids.Select(id => Missing(id, stage.Id,
                "Profilo o combinazione non supportati, oppure provenienza delle azioni non dichiarata. Nessuna verifica di sezione eseguita."))).ToArray();
        var definitions = new Dictionary<string, EmbeddedWallRcPile>(StringComparer.Ordinal);
        var diagnostics = new Dictionary<string, string>(StringComparer.Ordinal); var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in EmbeddedWallDocument.Rows(data, "rc_piles"))
        {
            cancellation.ThrowIfCancellationRequested(); string id = Text(row, "id");
            if (!ids.Contains(id) || !seen.Add(id)) throw new ArgumentException("Sezione circolare duplicata o non utilizzata: " + id);
            try { definitions.Add(id, Read(row)); }
            catch (ArgumentException error) { diagnostics.Add(id, "Dati della sezione non valutabili: " + error.Message); }
        }
        var result = new List<EmbeddedWallRcPileData>();
        // A bad section does not discard the available checks of the other physical sections.
        foreach (string id in ids)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!definitions.TryGetValue(id, out var definition))
            {
                result.AddRange(analysis.Stages.Select(stage => Missing(id, stage.Id, diagnostics.GetValueOrDefault(id, "Sezione circolare non definita.")))); continue;
            }
            try
            {
                var section = definition.Section;
                // Read-only metadata from the same fixed ordinary NTC profile used by CircularRcSectionSession.
                var profile = new StandardNTC2018Concrete();
                var properties = new EmbeddedWallRcPileProperties(section.Diameter, definition.Spacing / SoilUnits.Metre, section.Cover,
                    section.BarCount, section.BarDiameter, section.LinkDiameter, section.LinkSpacing, section.BarRadius, section.ConcreteModulus, section.GrossInertia,
                    definition.SingleRowEqualSharingConfirmed && !string.IsNullOrWhiteSpace(definition.RepresentationSource)
                        ? definition.FlexuralRigidity(width) / NmmSquaredPerKilonewtonMetreSquared : null,
                    section.OutlineDivisions, section.MaterialSource + "; " + definition.RepresentationSource,
                    profile.GammaC, profile.GammaS, profile.AlphaCC);
                var rows = EmbeddedWallRcPiles.Check(analysis, width, [definition], kind, cancellation).Where(r => r.SectionId == id);
                result.AddRange(rows.Select(r => new EmbeddedWallRcPileData(r.CaseId, r.StageId, r.SectionId, r.Elevation / SoilUnits.Metre, r.Side,
                    r.Actions?.N / NewtonsPerKilonewton, r.Actions?.V / NewtonsPerKilonewton, r.Actions?.M / NmmPerKilonewtonMetre,
                    r.StripToPileFactor, r.Outcome, r.Diagnostic, standard + "; " + kind, properties,
                    Array.AsReadOnly(r.Checks.Select(c => new EmbeddedWallRcCheckData(c.Method, c.Unit switch { "Nmm" => "kNm", "N" => "kN", _ => c.Unit },
                        c.Demand / UnitFactor(c.Unit), c.Capacity / UnitFactor(c.Unit), c.Ratio, c.Outcome, c.Diagnostic ?? "")).ToArray()),
                    Array.AsReadOnly(r.ConstructionChecks.Select(c => new EmbeddedWallRcDetailData(c.Key, c.Actual, c.Limit, c.Unit,
                        c.Passed == true ? EngineeringOutcome.Satisfied : c.Passed == false ? EngineeringOutcome.NotSatisfied : EngineeringOutcome.NotEvaluated,
                        c.Reference, c.Explanation)).ToArray()))));
            }
            catch (ArgumentException error) { result.AddRange(analysis.Stages.Select(stage => Missing(id, stage.Id, "Sezione non valutabile: " + error.Message))); }
        }
        return result.AsReadOnly();
    }
    private static double UnitFactor(string unit) => unit switch { "N" => NewtonsPerKilonewton, "Nmm" => NmmPerKilonewtonMetre, _ => 1 };
}
