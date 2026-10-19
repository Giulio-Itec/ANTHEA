using System.Globalization;
using System.Text;

namespace Anthea.Reports;

public static partial class EmbeddedWallReport
{
    private static void RenderEnvelope(EmbeddedWallReportData report, Action<string, bool> paragraph,
        Action<string[], IEnumerable<string[]>> table)
    {
        if (report.Envelope is not { } envelope) return;
        paragraph("Inviluppi delle fasi — estremi con azioni concomitanti", true); paragraph(envelope.Diagnostic, false);
        paragraph($"Fascia {F(envelope.TributaryWidth)} m; quote {F(envelope.Top)} / {F(envelope.Bottom)} m. "
            + "I valori N–V–M di ciascuna riga appartengono alla stessa fase, sezione e quota del valore governante. "
            + "Non costituiscono una nuova combinazione; le verifiche sono quelle delle singole fasi. Il CSV conserva tutte le stazioni e i due lati delle discontinuità.", false);
        foreach (var history in envelope.Histories)
            paragraph("Caso " + history.CaseId + "; fasi incluse: " + string.Join(", ", history.IncludedStages)
                + (history.Complete ? ". Storia completa." : ". Storia incompleta; fase interrotta: " + history.FailedStage + ". " + history.Diagnostic), false);
        foreach (var series in envelope.Series)
        {
            paragraph(series.Label, true);
            table(["Estremo", "Valore [" + series.Unit + "]", "Caso / fase", "Quota / lato [m]", "N [kN]", "V [kN]", "M [kNm]"],
                new[] { ("Minimo", series.Minimum), ("Massimo", series.Maximum) }.Select(v => new[] { v.Item1, F(v.Item2.Value),
                    v.Item2.CaseId + " / " + v.Item2.Stage, F(v.Item2.Point.Elevation) + " / " + v.Item2.Point.Side,
                    F(v.Item2.Point.Normal), F(v.Item2.Point.Shear), F(v.Item2.Point.Moment) }));
        }
    }
    public static string EnvelopeCsv(EmbeddedWallReportData result)
    {
        static string Cell(object? v) => "\"" + (v is double d ? d.ToString("G17", CultureInfo.InvariantCulture) : v?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("Grandezza;Unita;Estremo;Quota_m;Lato;Caso;Fase;Sezione;Valore;N_kN;V_kN;M_kNm;u_mm;Rotazione_rad;p_sx_kPa;p_dx_kPa;q_netto_kN_m;Completo;Impronta\r\n");
        if (result.Envelope is not { } envelope) return text.ToString();
        foreach (var series in envelope.Series) foreach (var station in series.Stations)
        foreach (var bound in new[] { ("Minimo", station.Minimum), ("Massimo", station.Maximum) })
        {
            var v = bound.Item2; var p = v.Point;
            object?[] row = [series.Label, series.Unit, bound.Item1, station.Elevation, station.Side, v.CaseId, v.Stage, p.Section, v.Value,
                p.Normal, p.Shear, p.Moment, p.Displacement, p.Rotation, p.LeftPressure, p.RightPressure, p.NetDistributedLoad, envelope.Complete, result.Fingerprint];
            text.AppendLine(string.Join(";", row.Select(Cell)));
        }
        return text.ToString();
    }
}
