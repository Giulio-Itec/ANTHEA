using System.Collections.Immutable;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Anthea.Serialization;

namespace Anthea.Application.Concrete;

/// <summary>
/// The combinations of the quick RC section in Excel (17/11/2026): a workbook with one sheet, <see cref="SheetName"/>, a title, a note and the
/// table «Nome», «N [kN]», «Mx [kNm]», «My [kNm]», one row per combination, as many as wanted. Values only: no formula, macro or external link
/// is written, and the reading (<see cref="WorkbookCells"/>) takes the values saved by Excel. An empty force is zero.
/// </summary>
public static class ConcreteQuickActionsExcel
{
    /// <summary>The name of the sheet of the combinations.</summary>
    public const string SheetName = "Sollecitazioni";

    /// <summary>The heads that a workbook must have (in any column; the column SL is optional).</summary>
    public static ImmutableArray<string> Headers { get; } = ["Nome", "N [kN]", "Mx [kNm]", "My [kNm]"];

    /// <summary>The heads of the written workbook, from column A, in the order of the table of the program.</summary>
    public static ImmutableArray<string> Layout { get; } = ["Nome", StateHeader, "N [kN]", "Mx [kNm]", "My [kNm]"];

    /// <summary>The title of the sheet, in A1.</summary>
    public const string Title = "ANTHEA · sezione in c.a. rapida · sollecitazioni";

    /// <summary>The note of the sheet, in A2.</summary>
    public const string Note = "Una riga per combinazione, quante se ne vogliono. N negativo a compressione; My conta solo con la flessione deviata; "
        + "una forza vuota vale 0. SL: scegliere dal menù a tendina (vuoto = SLU). Salvare (Ctrl+S) e premere «Importa da Excel» in ANTHEA.";

    /// <summary>The most combinations of a workbook.</summary>
    public const int MaximumRows = 10000;

    /// <summary>The most rows of the sheet that are read: the combinations, the title, the note and some empty rows.</summary>
    private const int SheetRows = 10050;

    /// <summary>The row of the heads of the table.</summary>
    public const int HeaderRow = 4;

    /// <summary>A combination of the workbook, with its limit state (<see cref="ConcreteQuickSection.LimitStates"/>; SLU by default).</summary>
    public sealed record Row(string Name, double N, double Mx, double My, string State = "SLU");

    /// <summary>The head of the column of the limit state (in B in the written workbook, optional when reading).</summary>
    public const string StateHeader = "SL";

    /// <summary>The combinations of the sheet <paramref name="data"/>, as the workbook writes them.</summary>
    public static IReadOnlyList<Row> Rows(JsonObject data) => ConcreteQuickSection.Combinations(data).Select(r =>
    {
        string name = r.S("nome");
        double Value(string key) => ConcreteEditor.Number(r.S(key), name + " · " + key);
        return new Row(name, Value("N"), Value("Mx"), Value("My"), r.S(ConcreteQuickSection.StateField));
    }).ToArray();

    /// <summary>
    /// The limit state of a cell of the column SL: empty (and «Capacità» of the first version) is SLU; the labels of the table (SLE RA, SLE FR, SLE QP), the codes of the RC section
    /// (SLE, SLE_FREQ, SLE_QP) and the names rara, frequente, quasi permanente are read, without regard to case.
    /// </summary>
    public static string State(string text)
    {
        string t = text.Trim().ToUpperInvariant().Replace('.', ' ').Replace("  ", " ");
        if (t.Length == 0 || t == "CAPACITÀ" || t == "CAPACITA") return "SLU";
        return t switch
        {
            "SLU" => "SLU", "SLV" => "SLV",
            "SLE" or "SLE RA" or "SLE RARA" or "RARA" => "SLE",
            "SLE_FREQ" or "SLE FR" or "SLE FREQUENTE" or "FREQUENTE" => "SLE_FREQ",
            "SLE_QP" or "SLE QP" or "SLE QUASI PERMANENTE" or "QUASI PERMANENTE" => "SLE_QP",
            _ => throw new ArgumentException("Stato limite non riconosciuto: «" + text.Trim() + "» (SLU, SLV, SLE RA, SLE FR, SLE QP).")
        };
    }

    /// <summary>The workbook of the combinations of <paramref name="data"/>.</summary>
    public static byte[] Export(JsonObject data) => Write(Rows(data));

    /// <summary>The workbook of <paramref name="rows"/>; it is read back before it is returned.</summary>
    public static byte[] Write(IReadOnlyList<Row> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count > MaximumRows) throw new ArgumentException("Massimo 10.000 combinazioni per esportazione.");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        static string Column(int index) => ((char)('A' + index)).ToString();
        XElement Text(string reference, string text, int style) => new(ns + "c", new XAttribute("r", reference), new XAttribute("s", style), new XAttribute("t", "inlineStr"),
            new XElement(ns + "is", new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
        XElement Number(string reference, double value)
        {
            if (!double.IsFinite(value)) throw new ArgumentException("Sollecitazione non finita.");
            return new(ns + "c", new XAttribute("r", reference), new XAttribute("s", 4), new XElement(ns + "v", value.ToString("R", CultureInfo.InvariantCulture)));
        }
        // The columns in the order of the table of the program: Nome, SL, N, Mx, My (19/11/2026).
        var body = new XElement(ns + "sheetData",
            new XElement(ns + "row", new XAttribute("r", 1), Text("A1", Title, 1)),
            new XElement(ns + "row", new XAttribute("r", 2), Text("A2", Note, 3)),
            new XElement(ns + "row", new XAttribute("r", HeaderRow), Layout.Select((h, i) => Text(Column(i) + HeaderRow, h, 2))));
        for (int i = 0; i < rows.Count; i++)
        {
            int r = HeaderRow + 1 + i;
            var row = rows[i];
            body.Add(new XElement(ns + "row", new XAttribute("r", r), Text("A" + r, row.Name, 0), Text("B" + r, ConcreteQuickSection.LimitStateLabel(row.State), 0),
                Number("C" + r, row.N), Number("D" + r, row.Mx), Number("E" + r, row.My)));
        }
        int last = Math.Max(HeaderRow + 1, HeaderRow + rows.Count);
        // The column SL as a drop-down list of the limit states, for the rows of the combinations.
        var stateList = new XElement(ns + "dataValidations", new XAttribute("count", 1),
            new XElement(ns + "dataValidation", new XAttribute("type", "list"), new XAttribute("allowBlank", 1), new XAttribute("showInputMessage", 1),
                new XAttribute("showErrorMessage", 1), new XAttribute("errorTitle", "Stato limite"), new XAttribute("error", "Scegliere " + string.Join(", ", ConcreteQuickSection.LimitStateLabels) + "."),
                new XAttribute("promptTitle", "Stato limite"), new XAttribute("prompt", "Vuoto = SLU"),
                new XAttribute("sqref", "B" + (HeaderRow + 1) + ":B" + (HeaderRow + MaximumRows)),
                new XElement(ns + "formula1", "\"" + string.Join(",", ConcreteQuickSection.LimitStateLabels) + "\"")));
        var sheet = new XDocument(new XElement(ns + "worksheet", new XAttribute("xmlns", ns.NamespaceName), new XAttribute(XNamespace.Xmlns + "r", rel.NamespaceName),
            new XElement(ns + "dimension", new XAttribute("ref", "A1:E" + last)),
            new XElement(ns + "sheetViews", new XElement(ns + "sheetView", new XAttribute("workbookViewId", 0),
                new XElement(ns + "pane", new XAttribute("ySplit", HeaderRow), new XAttribute("topLeftCell", "A" + (HeaderRow + 1)), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")),
                new XElement(ns + "selection", new XAttribute("pane", "bottomLeft"), new XAttribute("activeCell", "A" + (HeaderRow + 1)), new XAttribute("sqref", "A" + (HeaderRow + 1))))),
            new XElement(ns + "sheetFormatPr", new XAttribute("defaultRowHeight", 15)),
            new XElement(ns + "cols",
                new XElement(ns + "col", new XAttribute("min", 1), new XAttribute("max", 1), new XAttribute("width", 26), new XAttribute("customWidth", 1)),
                new XElement(ns + "col", new XAttribute("min", 2), new XAttribute("max", 2), new XAttribute("width", 12), new XAttribute("customWidth", 1)),
                new XElement(ns + "col", new XAttribute("min", 3), new XAttribute("max", 5), new XAttribute("width", 14), new XAttribute("customWidth", 1))),
            body, stateList));
        var bytes = Package(new Dictionary<string, XDocument>
        {
            ["[Content_Types].xml"] = XDocument.Parse(ContentTypes),
            ["_rels/.rels"] = XDocument.Parse(PackageRelations),
            ["xl/workbook.xml"] = XDocument.Parse(Workbook),
            ["xl/_rels/workbook.xml.rels"] = XDocument.Parse(WorkbookRelations),
            ["xl/styles.xml"] = XDocument.Parse(Styles),
            ["xl/worksheets/sheet1.xml"] = sheet
        });
        _ = Read(bytes);
        return bytes;
    }

    /// <summary>
    /// The combinations of a workbook: the rows under the heads <see cref="Headers"/> of the sheet <see cref="SheetName"/>, without the empty
    /// ones. The columns are found by their heads, in any order (the order of the program, Nome, SL, N, Mx, My, and the earlier one with SL last
    /// or without it); the other columns are ignored, an empty force is zero, an empty SL is SLU. At least one combination.
    /// </summary>
    public static IReadOnlyList<Row> Read(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        WorkbookCells.Sheet sheet;
        try { sheet = WorkbookCells.Read(bytes, SheetName, SheetRows); }
        catch (Exception error) when (error is InvalidDataException or System.Xml.XmlException)
        {
            throw new ArgumentException("Il file non è una cartella di lavoro Excel (.xlsx) valida.", error);
        }
        var rows = new List<Row>();
        Dictionary<string, int>? columns = null;
        foreach (var row in sheet.Rows)
        {
            var cells = row.Cells;
            string Cell(int column) => cells.GetValueOrDefault(column)?.Trim() ?? "";
            if (columns is null)
            {
                // The row of the heads: every head of Headers in some column; SL where it is, when it is.
                var found = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var (column, text) in cells.OrderBy(c => c.Key))
                    if (Headers.Append(StateHeader).Contains(text.Trim(), StringComparer.OrdinalIgnoreCase)) found.TryAdd(text.Trim(), column);
                if (Headers.All(found.ContainsKey)) columns = found;
                continue;
            }
            string Field(string head) => columns.TryGetValue(head, out int column) ? Cell(column) : "";
            if (Headers.Append(StateHeader).All(h => Field(h).Length == 0)) continue;
            string context = "Riga Excel " + row.Number + ": ";
            string name = Field(Headers[0]);
            if (name.Length == 0) throw new ArgumentException(context + "nome della combinazione mancante.");
            double Value(string head)
            {
                string text = Field(head);
                try { return text.Length == 0 ? 0 : ConcreteEditor.Number(text, head); }
                catch (ArgumentException error) { throw new ArgumentException(context + error.Message, error); }
            }
            string limitState;
            try { limitState = State(Field(StateHeader)); }
            catch (ArgumentException error) { throw new ArgumentException(context + error.Message, error); }
            rows.Add(new Row(name, Value(Headers[1]), Value(Headers[2]), Value(Headers[3]), limitState));
            if (rows.Count > MaximumRows) throw new ArgumentException("Massimo 10.000 combinazioni per importazione.");
        }
        if (columns is null) throw new ArgumentException("Intestazioni non riconosciute nel foglio " + SheetName + ": servono le colonne " + string.Join(", ", Headers) + ".");
        if (rows.Count == 0) throw new ArgumentException("Nessuna combinazione nel foglio " + SheetName + ": nessuna modifica.");
        return rows;
    }

    /// <summary>Replaces the combinations of <paramref name="data"/> with those of the workbook; returns how many.</summary>
    public static int Import(JsonObject data, byte[] bytes)
    {
        var rows = Read(bytes);
        ConcreteQuickSection.ReplaceCombinations(data, rows);
        return rows.Count;
    }

    private static byte[] Package(IReadOnlyDictionary<string, XDocument> parts)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            foreach (var (name, document) in parts)
            {
                using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                document.Save(stream, SaveOptions.DisableFormatting);
            }
        }
        return buffer.ToArray();
    }

    private const string ContentTypes = """
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
        </Types>
        """;

    private const string PackageRelations = """
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private const string Workbook = """
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets><sheet name="Sollecitazioni" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """;

    private const string WorkbookRelations = """
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    // Styles: 0 normal, 1 title (bold, 13), 2 head (bold, grey fill, line below, centred), 3 note (italic, grey), 4 number.
    private const string Styles = """
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="4">
            <font><sz val="11"/><name val="Calibri"/><family val="2"/></font>
            <font><b/><sz val="13"/><name val="Calibri"/><family val="2"/></font>
            <font><b/><sz val="11"/><name val="Calibri"/><family val="2"/></font>
            <font><i/><sz val="10"/><color rgb="FF595959"/><name val="Calibri"/><family val="2"/></font>
          </fonts>
          <fills count="3">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFE7EEF5"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="2">
            <border><left/><right/><top/><bottom/><diagonal/></border>
            <border><left/><right/><top/><bottom style="thin"><color rgb="FF7F8FA0"/></bottom><diagonal/></border>
          </borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="5">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>
            <xf numFmtId="0" fontId="2" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center"/></xf>
            <xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1"/>
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
          </cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;
}
