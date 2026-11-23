using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Serialization;

namespace Anthea.Application.Concrete;

/// <summary>The independent shear actions table, using the established quick-section workbook layout.</summary>
public static class ConcreteQuickShearExcel
{
    public const string SheetName = "Taglio";
    private const int SheetRowsMargin = 50;
    private const int FirstDataRow = ConcreteQuickActionsExcel.HeaderRow + 1;
    public static IReadOnlyList<string> Headers { get; } = ["Nome", "Verifica", "N [kN]", "Vx [kN]", "Vy [kN]"];
    public sealed record Row(string Name, double N, double Vx, double Vy, string Condition = ConcreteQuickShear.WithLinks);
    public static void Validate(IReadOnlyList<Row> rows)
    {
        if (rows.Count == 0) throw new ArgumentException("Nessuna combinazione a taglio: nessuna modifica.");
        if (rows.Count > ConcreteQuickActionsExcel.MaximumRows) throw new ArgumentException("Massimo 10.000 combinazioni.");
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name)) throw new ArgumentException("Nome combinazione mancante.");
            if (!double.IsFinite(row.N) || !double.IsFinite(row.Vx) || !double.IsFinite(row.Vy)) throw new ArgumentException("Sollecitazione non finita.");
            if (!ConcreteQuickShear.Choices.Any(c => c.Code == row.Condition)) throw new ArgumentException("Tipologia di verifica a taglio non valida.");
        }
    }
    public static byte[] Export(JsonObject data) => Write(ConcreteQuickShear.Combinations(data).Select(r =>
        new Row(r.S("nome"), ConcreteEditor.Number(r.S("N", "0"), "N"), ConcreteEditor.Number(r.S("Vx", "0"), "Vx"),
            ConcreteEditor.Number(r.S("Vy", "0"), "Vy"), ConcreteQuickShear.Condition(data, r))).ToArray());
    public static byte[] Write(IReadOnlyList<Row> rows)
    {
        Validate(rows);
        var template = ConcreteQuickActionsExcel.Write(rows.Select(r => new ConcreteQuickActionsExcel.Row(r.Name, r.N, r.Vx, r.Vy)).ToArray());
        using var stream = new MemoryStream(); stream.Write(template); stream.Position = 0;
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            void Update(string path, Action<XDocument> edit)
            {
                var entry = zip.GetEntry(path)!;
                XDocument xml; using (var source = entry.Open()) xml = XDocument.Load(source);
                edit(xml); entry.Delete(); using var target = zip.CreateEntry(path).Open(); xml.Save(target);
            }
            Update("xl/workbook.xml", xml => xml.Descendants(ns + "sheet").Single().SetAttributeValue("name", SheetName));
            Update("xl/worksheets/sheet1.xml", xml =>
            {
                void Text(string cell, string value) => xml.Descendants(ns + "c").Single(c => (string?)c.Attribute("r") == cell).Descendants(ns + "t").Single().Value = value;
                Text("A1", "ANTHEA · sezione in c.a. rapida · combinazioni a taglio");
                Text("A2", "Una riga per combinazione. N negativo a compressione; N, Vx e Vy sono azioni di progetto in kN. Verifica: con armatura a taglio oppure sezione non armata a taglio. Salvare (Ctrl+S) e premere Importa da Excel nel pannello Taglio.");
                for (int i = 0; i < Headers.Count; i++) Text(((char)('A' + i)) + "4", Headers[i]);
                for (int i = 0; i < rows.Count; i++) Text("B" + (FirstDataRow + i), ConcreteQuickShear.Label(rows[i].Condition));
                var validation = xml.Descendants(ns + "dataValidation").Single();
                validation.SetAttributeValue("errorTitle", "Verifica a taglio"); validation.SetAttributeValue("promptTitle", "Verifica a taglio");
                validation.SetAttributeValue("error", "Scegliere una delle due tipologie di verifica.");
                validation.SetAttributeValue("prompt", "Vuoto = verifica con armatura a taglio");
                validation.Element(ns + "formula1")!.Value = "\"" + string.Join(",", ConcreteQuickShear.Choices.Select(c => c.Label)) + "\"";
                xml.Descendants(ns + "col").Single(c => (string?)c.Attribute("min") == "2").SetAttributeValue("width", 38);
            });
        }
        var bytes = stream.ToArray(); _ = Read(bytes); return bytes;
    }
    public static IReadOnlyList<Row> Read(byte[] bytes)
    {
        WorkbookCells.Sheet sheet;
        try { sheet = WorkbookCells.Read(bytes, SheetName, ConcreteQuickActionsExcel.MaximumRows + SheetRowsMargin); }
        catch (Exception error) when (error is InvalidDataException or System.Xml.XmlException)
        { throw new ArgumentException("Il file non è una cartella di lavoro Excel (.xlsx) valida.", error); }
        Dictionary<string, int>? columns = null;
        var rows = new List<Row>();
        foreach (var row in sheet.Rows)
        {
            if (columns is null)
            {
                var found = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var (column, text) in row.Cells)
                    if (Headers.Contains(text.Trim(), StringComparer.OrdinalIgnoreCase)) found.TryAdd(text.Trim(), column);
                if (Headers.All(found.ContainsKey)) columns = found;
                continue;
            }
            string Field(string header) => row.Cells.GetValueOrDefault(columns[header])?.Trim() ?? "";
            if (Headers.All(h => Field(h).Length == 0)) continue;
            try
            {
                string name = Field("Nome"), condition = Field("Verifica");
                string code = condition.Length == 0 ? ConcreteQuickShear.WithLinks : ConcreteQuickShear.Choices.FirstOrDefault(c =>
                    new[] { c.Code, c.Label, c.ShortLabel }.Contains(condition, StringComparer.OrdinalIgnoreCase))?.Code
                    ?? throw new ArgumentException("Tipologia di verifica non riconosciuta: «" + condition + "».");
                double Number(string header) => Field(header).Length == 0 ? 0 : ConcreteEditor.Number(Field(header), header);
                rows.Add(new(name, Number("N [kN]"), Number("Vx [kN]"), Number("Vy [kN]"), code));
            }
            catch (ArgumentException error) { throw new ArgumentException("Riga Excel " + row.Number + ": " + error.Message, error); }
            if (rows.Count > ConcreteQuickActionsExcel.MaximumRows) throw new ArgumentException("Massimo 10.000 combinazioni.");
        }
        if (columns is null) throw new ArgumentException("Nel foglio Taglio servono le colonne " + string.Join(", ", Headers) + ".");
        Validate(rows); return rows;
    }
}
