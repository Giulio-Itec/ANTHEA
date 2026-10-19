using System.Globalization;
using System.Text;

namespace Anthea.Reports;

public static partial class EmbeddedWallReport
{
    private static void AppendAnchors(EmbeddedWallReportData result, Action<string, bool> paragraph,
        Action<string[], IEnumerable<string[]>> table)
    {
        if ((result.AnchorChecks?.Count ?? 0) == 0) return;
        paragraph("Sfilamento dei tiranti", true); var notes = new HashSet<string>(StringComparer.Ordinal);
        var evidence = new Dictionary<(string Case, string Support), EmbeddedWallAnchorData>();
        foreach (var c in result.AnchorChecks ?? [])
        {
            paragraph($"{c.Stage} · {c.Support} · {c.OutcomeText}", true);
            paragraph($"Ed={F(c.Demand)} kN; Rad={F(c.Capacity)} kN; utilizzo={F(c.Ratio)}. Forze per singolo tirante.", false);
            paragraph($"Media={F(c.Mean)} kN; minimo={F(c.Minimum)} kN; Rak={F(c.Characteristic)} kN; ξ medio/minimo={F(c.MeanFactor)}/{F(c.MinimumFactor)}; γR={F(c.ResistanceFactor)}.", false);
            if (notes.Add(c.Reference + c.Source)) paragraph(c.Reference + ". " + c.Source, false);
            if (notes.Add(c.Diagnostic)) paragraph(c.Diagnostic, false);
            if (evidence.TryGetValue((c.CaseId, c.Support), out var previous)
                && previous.Samples.SequenceEqual(c.Samples) && previous.Segments.SequenceEqual(c.Segments))
            {
                paragraph($"Indagini, tratti e fonti invariati rispetto alla fase {previous.Stage}, tirante {c.Support}, riportati sopra.", false);
                continue;
            }
            evidence[(c.CaseId, c.Support)] = c;
            table(["Profilo / prova", "Ra [kN]", "Campo applicabile", "Fonte"], c.Samples.Select(s => new[] { s.Id, F(s.Resistance), s.Applicable ? "sì" : "no", s.Source }));
            foreach (var s in c.Samples.Where(s => !string.IsNullOrWhiteSpace(s.Diagnostic))) paragraph(s.Id + ": " + s.Diagnostic, false);
            if (c.Segments.Count > 0)
            {
                table(["Profilo / strato", "Asse da/a [m]", "pl / pi [MPa]", "α / Ds [mm]", "Curva / qs [MPa]", "Ra [kN]"],
                    c.Segments.Select(s => new[] { s.Profile + "/" + s.Layer, F(s.Start) + "/" + F(s.End), F(s.LimitPressure) + "/" + F(s.InjectionPressure),
                        F(s.DiameterFactor) + "/" + F(s.BulbDiameter), s.Curve + "/" + F(s.UnitResistance), F(s.Resistance) }));
                foreach (string source in c.Segments.Select(s => s.Source).Distinct(StringComparer.Ordinal))
                    if (notes.Add(source)) paragraph("Fonte pressiometrica: " + source, false);
            }
        }
    }

    public static string AnchorCsv(EmbeddedWallReportData result)
    {
        if (result.Cases is not null) return HistoriesCsv(result, AnchorCsv);
        static string Cell(object? value) => "\"" + (value is double d ? d.ToString("G17", CultureInfo.InvariantCulture) : value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("tipo;caso;fase;tirante;Ed_kN;media_kN;minimo_kN;Rak_kN;Rad_kN;utilizzo;xi_media;xi_minimo;gammaR;esito;profilo;strato;inizio_m;fine_m;pl_MPa;pi_MPa;alpha;Ds_mm;curva;qs_MPa;Ra_kN;applicabile;fonte;diagnostica;impronta\n");
        void Row(object?[] values) => text.AppendLine(string.Join(";", values.Select(Cell)));
        foreach (var c in result.AnchorChecks ?? [])
        {
            Row(["verifica", c.CaseId, c.Stage, c.Support, c.Demand, c.Mean, c.Minimum, c.Characteristic, c.Capacity, c.Ratio,
                c.MeanFactor, c.MinimumFactor, c.ResistanceFactor, c.Outcome, null, null, null, null, null, null, null, null, null, null, null, null,
                c.Reference + "; " + c.Source, c.Diagnostic, result.Fingerprint]);
            foreach (var s in c.Samples) Row(["campione", c.CaseId, c.Stage, c.Support, null, null, null, null, null, null,
                null, null, null, null, s.Id, null, null, null, null, null, null, null, null, null, s.Resistance, s.Applicable, s.Source, s.Diagnostic, result.Fingerprint]);
            foreach (var s in c.Segments) Row(["tratto", c.CaseId, c.Stage, c.Support, null, null, null, null, null, null,
                null, null, null, null, s.Profile, s.Layer, s.Start, s.End, s.LimitPressure, s.InjectionPressure, s.DiameterFactor, s.BulbDiameter, s.Curve, s.UnitResistance,
                s.Resistance, null, s.Source, null, result.Fingerprint]);
        }
        return text.ToString();
    }
}
