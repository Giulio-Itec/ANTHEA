using System.Globalization;
using System.Text;
using GPC.Model.Checking.Contracts;

namespace Anthea.Reports;

public static partial class EmbeddedWallReport
{
    private static void AppendRcPiles(EmbeddedWallReportData result, bool allStations, Action<string, bool> paragraph,
        Action<string[], IEnumerable<string[]>> table)
    {
        var values = result.RcPiles; if (values is null || values.Count == 0) return;
        paragraph("Pali circolari in c.a. — verifiche delle sezioni", true);
        paragraph("Azioni concomitanti per singolo palo, N positivo a trazione. Resistenza nominale/SLE distinti dai dettagli costruttivi. "
            + (allStations ? "Tutti i tagli." : "Tagli terminali e governanti per ogni controllo; il CSV conserva tutti i tagli."), false);
        var notes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in values.Where(r => r.Properties is not null).GroupBy(r => r.Section))
        {
            var p = section.First().Properties!;
            table(["Sezione", "D [mm]", "Interasse [m]", "Barre / staffe [mm]", "Ec [MPa]", "EI lordo fascia [kNm²]"],
                [new[] { section.Key, F(p.Diameter), F(p.Spacing), p.BarCount + "φ" + F(p.BarDiameter) + "; φ" + F(p.LinkDiameter) + "/" + F(p.LinkSpacing), F(p.ElasticModulus), F(p.EquivalentRigidity) }]);
            paragraph($"Copriferro esterno staffa {F(p.Cover)} mm; raggio corona {F(p.BarRadius)} mm; contorno numerico {p.OutlineDivisions} lati. {p.Source}", false);
            paragraph($"Profilo Italia NTC 2018, c.a. ordinario statico: γc={F(p.GammaC)}; γs={F(p.GammaS)}; αcc={F(p.AlphaCC)} per SLU. "
                + "Per SLE si utilizzano i limiti del profilo per la combinazione selezionata.", false);
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
                paragraph($"z={F(r.Elevation)} m, {r.Side}: N={F(r.Normal)} kN; V={F(r.Shear)} kN; M={F(r.Moment)} kNm. Esito locale: {r.OutcomeText}.", false);
                if (notes.Add(r.Reference)) paragraph(r.Reference, false);
                paragraph("Fattore fascia→palo=" + F(r.StripToMemberFactor), false);
                table(["Controllo", "Domanda", "Capacità", "Unità", "Utilizzo", "Esito"],
                    r.Checks.Select(c => new[] { c.Method, F(c.Demand), F(c.Capacity), c.Unit, F(c.Ratio), c.OutcomeText }));
                if (notes.Add(r.Diagnostic)) paragraph(r.Diagnostic, false);
                foreach (var c in r.Checks) if (!string.IsNullOrEmpty(c.Diagnostic) && notes.Add(c.Diagnostic)) paragraph(c.Diagnostic, false);
            }
            var details = group.SelectMany(r => r.Details).Distinct().ToArray();
            if (details.Length == 0) continue;
            paragraph("Dettagli costruttivi — esiti separati", true);
            table(["Regola", "Valore", "Limite", "Unità", "Esito"], details.Select(d => new[] { d.Label, F(d.Actual), F(d.Limit), d.Unit, d.OutcomeText }));
            foreach (var d in details) if (notes.Add(d.Reference + " " + d.Diagnostic)) paragraph(d.Reference + ": " + d.Diagnostic, false);
        }
    }

    public static string RcPileCsv(EmbeddedWallReportData result)
    {
        if (result.Cases is not null) return HistoriesCsv(result, RcPileCsv);
        static string Cell(object? value) => "\"" + (value is double d ? d.ToString("G17", CultureInfo.InvariantCulture) : value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("caso;fase;sezione;z_m;lato;N_kN;V_kN;M_kNm;fattore_fascia_palo;tipo;metodo;domanda_o_valore;capacita_o_limite;unita;utilizzo;esito;fonte;diagnostica;impronta\n");
        foreach (var r in result.RcPiles ?? [])
        {
            void Row(string type, string method, double? demand, double? capacity, string unit, double? ratio, EngineeringOutcome outcome, string source, string diagnostic)
                => text.AppendLine(string.Join(";", new object?[] { r.CaseId, r.Stage, r.Section, r.Elevation, r.Side, r.Normal, r.Shear, r.Moment, r.StripToMemberFactor,
                    type, method, demand, capacity, unit, ratio, outcome, source + "; " + r.Properties?.Source, r.Diagnostic + " " + diagnostic, result.Fingerprint }.Select(Cell)));
            if (r.Checks.Count == 0) Row("Sezione", "Non eseguita", null, null, "", null, r.Outcome, r.Reference, r.Diagnostic);
            foreach (var c in r.Checks) Row("Sezione", c.Method, c.Demand, c.Capacity, c.Unit, c.Ratio, c.Outcome, r.Reference, c.Diagnostic);
            foreach (var d in r.Details) Row("Dettagli", d.Method, d.Actual, d.Limit, d.Unit, null, d.Outcome, d.Reference, d.Diagnostic);
        }
        return text.ToString();
    }
}
