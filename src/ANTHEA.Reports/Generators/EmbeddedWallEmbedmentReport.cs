using System.Globalization;
using System.Text;

namespace Anthea.Reports;

public static partial class EmbeddedWallReport
{
    private static void RenderEmbedment(EmbeddedWallReportData report, Action<string, bool> paragraph,
        Action<string[], IEnumerable<string[]>> table)
    {
        if (report.EmbedmentSearch is not { } search) return;
        paragraph("Ricerca dell'infissione", true); paragraph(search.Diagnostic, false);
        paragraph($"Quota di riferimento {F(search.ReferenceElevation)} m; passo {F(search.Increment)} m. Candidati valutati {search.Candidates.Count}/{search.PlannedEmbedments.Count}. "
            + $"Ricerca completata: {(search.Completed ? "sì" : "no")}; annullata: {(search.Cancelled ? "sì" : "no")}; minimo dimostrato sulla griglia: {(search.MinimumCertified ? "sì" : "no")}. Fonte: {search.Source}", false);
        paragraph("Griglia completa [m]: " + string.Join("; ", search.PlannedEmbedments.Select(d => F(d))), false);
        paragraph(search.BestToe.HasValue ? $"Candidato accettabile meno profondo: infissione {F(search.BestEmbedment)} m, quota piede {F(search.BestToe)} m. Le successive tabelle di fase e verifica si riferiscono a questo candidato. Il modello di partenza è quello riportato fra i dati assegnati."
            : "Nessun candidato accettato. Non si presentano diagrammi di una soluzione verificata.", false);
        table(["Infissione [m]", "Piede [m]", "Esito", "Diagnostica"], search.Candidates.Select(c => new[] { F(c.Embedment), F(c.ToeElevation), c.OutcomeText, c.Diagnostic }));
        foreach (var c in search.Candidates)
        {
            paragraph($"Infissione {F(c.Embedment)} m — {c.OutcomeText}", true);
            if (c.Result is { } result) paragraph("Analisi: " + result.Status + ". " + result.Diagnostic + "; impronta candidato: " + result.Fingerprint, false);
            table(["Criterio / fase", "Domanda", "Limite", "Unità", "Utilizzo", "Esito"], c.Criteria.Select(v => new[] {
                v.Label + " / " + (v.Stage ?? "tutta la storia"), F(v.Demand), F(v.Limit), v.Unit, F(v.Ratio), v.OutcomeText }));
            foreach (var v in c.Criteria) paragraph(v.Label + ": " + v.Method + ". " + v.Diagnostic, false);
        }
    }
    public static string EmbedmentCsv(EmbeddedWallReportData result)
    {
        static string Cell(object? value) => "\"" + (value is double d ? d.ToString("G17", CultureInfo.InvariantCulture) : value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("Tipo;Infissione_m;Piede_m;Caso;Fase;Criterio;Esito;Domanda;Limite;Utilizzo;Unita;Quota_m;Lato;Spostamento_mm;N_kN;V_kN;M_kNm;p_sx_kPa;p_dx_kPa;Metodo;Diagnostica;Impronta\r\n");
        void Row(params object?[] values) => text.AppendLine(string.Join(";", values.Select(Cell)));
        if (result.EmbedmentSearch is not { } search) return text.ToString();
        Row("Ricerca", null, null, result.CaseId, null, null, result.Status, null, null, null, null, null, null, null, null, null, null, null, null,
            search.Source, search.Diagnostic, result.Fingerprint);
        foreach (var c in search.Candidates)
        {
            Row("Candidato", c.Embedment, c.ToeElevation, result.CaseId, null, null, c.Outcome, null, null, null, null, null, null, null, null, null, null, null, null,
                null, c.Diagnostic, c.Result?.Fingerprint);
            foreach (var v in c.Criteria) Row("Criterio", c.Embedment, c.ToeElevation, v.CaseId, v.Stage, v.Label, v.Outcome, v.Demand, v.Limit, v.Ratio, v.Unit,
                v.Elevation, null, null, null, null, null, null, null, v.Method, v.Diagnostic, c.Result?.Fingerprint);
            foreach (var stage in c.Result?.Stages ?? [])
            foreach (var p in stage.Points) Row("Sezione", c.Embedment, c.ToeElevation, result.CaseId, stage.Id, null, c.Result!.Status, null, null, null, null,
                p.Elevation, p.Side, p.Displacement, p.Normal, p.Shear, p.Moment, p.LeftPressure, p.RightPressure, null, null, c.Result.Fingerprint);
        }
        return text.ToString();
    }
}
