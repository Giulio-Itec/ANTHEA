using System.Globalization;
using System.Text;
using Anthea.Serialization;
using Anthea.Calculations;
using Microsoft.VisualBasic.FileIO;
namespace Anthea.Application.BridgeDesign;

public sealed record PriceEntry(string Code, string Description, string Unit, decimal? Price, string[] Includes);
public sealed record PriceCatalogue(string Name, string Edition, string Currency, PriceEntry[] Entries);
public sealed record WorkPriceMapping(string WorkCode, string EntryCode, decimal? Override, string Reason);

/// <summary>Catalogue interchange only. Units are resolved by the calculation adapter.</summary>
public static class PriceCatalogueImport
{
    public static readonly string[] Headers = ["Codice", "Descrizione", "Unita", "Prezzo", "Inclusioni"];
    public static PriceCatalogue Csv(byte[] bytes, string name, string edition = "Importato")
    {
        if (bytes.Length > 20_000_000) throw new ArgumentException("Prezzario troppo grande (massimo 20 MB).");
        string text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        string delimiter = text.Split('\n')[0].Contains(';') ? ";" : ",";
        using var parser = new TextFieldParser(new StringReader(text)) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(delimiter); var rows = new List<string[]>();
        while (!parser.EndOfData)
        {
            if (rows.Count >= 100001) throw new ArgumentException("Massimo 100.000 voci per importazione.");
            try { rows.Add(parser.ReadFields() ?? []); }
            catch (MalformedLineException e) { throw new ArgumentException("CSV non valido alla riga " + parser.ErrorLineNumber + ": " + e.Message, e); }
        }
        return Read(rows, name, edition);
    }
    public static PriceCatalogue Excel(byte[] bytes, string name, string edition = "Importato")
    {
        var table = WorkbookCells.Read(bytes, "Prezzario");
        var rows = table.Rows.Select(r =>
        {
            if (r.Cells.Any(c => c.Key > Headers.Length && !string.IsNullOrWhiteSpace(c.Value))) throw new ArgumentException("Prezzario: dati oltre la colonna E.");
            return Enumerable.Range(1, Headers.Length).Select(k => r.Cells.GetValueOrDefault(k) ?? "").ToArray();
        });
        return Read(rows, name, edition);
    }
    private static PriceCatalogue Read(IEnumerable<string[]> rows, string name, string edition)
    {
        bool header = false; var entries = new List<PriceEntry>(); int number = 0;
        foreach (var row in rows)
        {
            number++;
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            if (!header)
            {
                if (row.Length != Headers.Length || !row.Select((v, k) => string.Equals(v.Trim(), Headers[k], StringComparison.OrdinalIgnoreCase)).All(v => v))
                    throw new ArgumentException("Intestazioni: " + string.Join("; ", Headers));
                header = true; continue;
            }
            if (row.Length != Headers.Length) throw new ArgumentException($"Riga {number}: sono richieste cinque colonne.");
            try { entries.Add(new(row[0].Trim(), row[1].Trim(), row[2].Trim(), Number(row[3]), row[4].Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))); }
            catch (ArgumentException e) { throw new ArgumentException($"Riga {number}: {e.Message}", e); }
        }
        if (!header || entries.Count == 0) throw new ArgumentException("Prezzario privo di voci.");
        var result = new PriceCatalogue(name, edition, "EUR", entries.ToArray()); Validate(result); return result;
    }
    public static decimal? Number(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if ((!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value)
            && !decimal.TryParse(text, NumberStyles.Float, CultureInfo.GetCultureInfo("it-IT"), out value)) || value < 0)
            throw new ArgumentException("Prezzo non valido: usare un numero non negativo, senza separatori delle migliaia.");
        return value;
    }
    public static void Validate(PriceCatalogue catalogue)
    {
        if (string.IsNullOrWhiteSpace(catalogue.Name) || string.IsNullOrWhiteSpace(catalogue.Edition) || catalogue.Currency != "EUR") throw new ArgumentException("Nome, edizione e valuta EUR sono richiesti.");
        if (catalogue.Entries is null || catalogue.Entries.Length == 0) throw new ArgumentException("Prezzario privo di voci.");
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in catalogue.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.Code) || string.IsNullOrWhiteSpace(e.Description) || !codes.Add(e.Code) || e.Price is < 0 || e.Includes is null) throw new ArgumentException("Codice/descrizione assenti, codice duplicato o prezzo negativo: " + e.Code);
            BridgePricing.ConvertPrice(0, e.Unit, e.Unit);
        }
    }
    public static string Template() => string.Join(";", Headers) + "\r\n" + string.Join("\r\n", BridgeDesignModel.Rates.Select(r => r.Key + ";" + r.Label + ";" + r.Unit[2..] + ";;"));
}
