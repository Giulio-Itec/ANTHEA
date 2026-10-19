using System.Text;

namespace Anthea.Reports;

public static partial class EmbeddedWallReport
{
    private static string HistoriesCsv(EmbeddedWallReportData result, Func<EmbeddedWallReportData, string> render)
    {
        if (result.Cases is not { Count: > 0 } histories) throw new InvalidDataException("Elenco dei risultati di caso vuoto.");
        var text = new StringBuilder(); string? header = null;
        foreach (var history in histories)
        {
            if (history.Cases is not null) throw new InvalidDataException("Le storie non possono contenere altri gruppi di storie.");
            string csv = render(history); int end = csv.IndexOf('\n');
            if (end < 0) throw new InvalidDataException("Intestazione CSV mancante.");
            string currentHeader = csv[..(end + 1)];
            if (header is null) { header = currentHeader; text.Append(header); }
            else if (header != currentHeader) throw new InvalidDataException("Colonne CSV incoerenti fra casi.");
            text.Append(csv[(end + 1)..]);
        }
        return text.ToString();
    }
}
