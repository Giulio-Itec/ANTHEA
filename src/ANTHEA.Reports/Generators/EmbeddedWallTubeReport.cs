using System.Globalization;
using System.Text;
using GPC.Model.Checking.Contracts;

namespace Anthea.Reports;

public static partial class EmbeddedWallReport
{
    private static void AppendTubes(EmbeddedWallReportData result, bool allStations, Action<string, bool> paragraph,
        Action<string[], IEnumerable<string[]>> table)
    {
        var values = result.SteelTubes;
        if (values is null || values.Count == 0) return;
        paragraph("Sezioni tubolari della parete — resistenza locale N–M–V", true);
        paragraph("Azioni concomitanti per singolo tubo, N positivo a trazione. Il risultato non comprende la stabilità della membratura. "
            + (allStations ? "Tutti i tagli." : "Estratto: tagli terminali e governanti per ciascun controllo; il CSV conserva tutti i tagli."), false);
        var notes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in values.Where(r => r.Properties is not null).GroupBy(r => r.Section))
        {
            var p = section.First().Properties!;
            table(["Sezione", "D / t [mm]", "Interasse [m]", "A [mm²]", "I [mm⁴]", "EI lordo fascia [kNm²]"],
                [new[] { section.Key, F(p.Diameter) + "/" + F(p.Thickness), F(p.Spacing), F(p.Area), F(p.Inertia), F(p.EquivalentRigidity) }]);
            paragraph($"E={F(p.ElasticModulus)} MPa; fy={F(p.YieldStrength)} MPa. {p.Source}", false);
        }
        foreach (var group in values.GroupBy(r => (r.Stage, r.Section)))
        {
            paragraph($"Fase {group.Key.Stage} · sezione {group.Key.Section}", true);
            var governing = group.SelectMany(r => r.Checks.Select(c => (Row: r, Check: c))).GroupBy(x => x.Check.Method)
                .Select(g => g.OrderByDescending(x => x.Check.Outcome == EngineeringOutcome.NotSatisfied && !x.Check.Ratio.HasValue)
                    .ThenByDescending(x => x.Check.Ratio).First().Row);
            var selected = allStations ? group.ToArray() : new[] { group.First(), group.Last() }.Concat(governing).Distinct()
                .OrderByDescending(r => r.Elevation).ThenBy(r => r.Side, StringComparer.Ordinal).ToArray();
            foreach (var r in selected)
            {
                paragraph($"z={F(r.Elevation)} m, {r.Side}: N={F(r.Normal)} kN; V={F(r.Shear)} kN; M={F(r.Moment)} kNm. {r.OutcomeText}.", false);
                paragraph($"Fattore fascia→tubo={F(r.StripToMemberFactor)}; classe={r.SectionClass?.ToString() ?? "non disponibile"}; γM0={F(r.PartialFactor)}; ρ={F(r.ShearReduction)}.", false);
                table(["Controllo", "Domanda", "Capacità", "Unità", "Utilizzo", "Esito"],
                    r.Checks.Select(c => new[] { c.Method, F(c.Demand), F(c.Capacity), c.Unit, F(c.Ratio), c.OutcomeText }));
                if (notes.Add(r.Diagnostic)) paragraph(r.Diagnostic, false);
                foreach (var c in r.Checks)
                {
                    if (notes.Add(c.Reference)) paragraph(c.Reference, false);
                    if (notes.Add(c.Diagnostic)) paragraph(c.Diagnostic, false);
                }
            }
        }
    }
    public static string TubeCsv(EmbeddedWallReportData result)
    {
        static string Cell(object? value) => "\"" + (value is double d ? d.ToString("G17", CultureInfo.InvariantCulture) : value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("caso;fase;sezione;z_m;lato;N_kN;V_kN;M_kNm;fattore_fascia_tubo;classe;gammaM0;rho;asse_neutro_mm;residuo_N;metodo;domanda;capacita;unita;utilizzo;esito;fonte;diagnostica;impronta\n");
        foreach (var r in result.SteelTubes ?? [])
        foreach (var c in r.Checks.Count > 0 ? r.Checks : [new EmbeddedWallTubeCheckData("Non eseguita", r.Reference, "", 0, null, null, r.Outcome, r.Diagnostic)])
            text.AppendLine(string.Join(";", new object?[] { r.CaseId, r.Stage, r.Section, r.Elevation, r.Side, r.Normal, r.Shear, r.Moment,
                r.StripToMemberFactor, r.SectionClass, r.PartialFactor, r.ShearReduction, r.NeutralAxisDistance, r.EquilibriumResidualN,
                c.Method, c.Demand, c.Capacity, c.Unit, c.Ratio, c.Outcome, c.Reference + "; " + r.Properties?.Source,
                r.Diagnostic + " " + c.Diagnostic, result.Fingerprint }.Select(Cell)));
        return text.ToString();
    }
}
