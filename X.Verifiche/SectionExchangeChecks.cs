using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using X.Core;

internal static class SectionExchangeChecks
{
    internal static int Run()
    {
        int count = 0;
        void Assert(bool ok, string label) { if (!ok) throw new Exception("Excel azioni: " + label); count++; }
        void Reject(byte[] data, string label) { try { SectionActionsExcel.Read(data); } catch (ArgumentException) { count++; return; } throw new Exception("Excel accettato: " + label); }
        byte[] template = SectionActionsExcel.Template();
        Assert(SectionActionsExcel.Read(template).Rows.Count == 0, "Template senza azioni inventate");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        byte[] FileFor(string[][] rows, Action<XDocument>? change = null)
        {
            using var memory = new MemoryStream(); memory.Write(template);
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Update, true))
            {
                var entry = archive.GetEntry("xl/worksheets/sheet1.xml")!; XDocument document;
                using (var stream = entry.Open()) document = XDocument.Load(stream);
                var body = document.Root!.Element(ns + "sheetData")!;
                body.Elements(ns + "row").Where(row => (int?)row.Attribute("r") >= 7).Remove();
                for (int i = 0; i < rows.Length; i++)
                    body.Add(new XElement(ns + "row", new XAttribute("r", i + 7), rows[i].Select((value, col) => new XElement(ns + "c", new XAttribute("r", $"{(char)('A' + col)}{i + 7}"), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", value))))));
                change?.Invoke(document); entry.Delete();
                using var output = archive.CreateEntry("xl/worksheets/sheet1.xml").Open(); document.Save(output);
            }
            return memory.ToArray();
        }
        var result = SectionActionsExcel.Read(FileFor([["SLU", "A", "-125.25", "12.5", "0", "", ""], ["Rara", "B", "-100,5", "0", "10", "", ""], ["Frequente", "C", "10", "0", "0", "", ""], ["Quasi permanente", "D", "0", "0", "0", "", ""], ["Taglio", "V", "-10", "", "", "50", "-25"], ["SLV", "E", "0", "0", "0", "", ""]]));
        Assert(result.Rows.Count == 6, "Sei famiglie importate");
        Assert(result.Rows[0].N == -125.25 && result.Rows[1].N == -100.5, "Decimali e compressione negativa");
        Assert(result.Rows[2].N == 10 && result.Rows[3].N == 0, "Trazione e zero conservati");
        Assert(result.Rows[4].Family == "Taglio" && result.Rows[4].Vy == -25 && result.Rows[4].Mx is null, "Taglio distinto da pressoflessione");
        Assert(result.Rows.Select(r => r.Family).Distinct().Count() == 6, "Mappatura famiglie SLE");
        Reject(FileFor([["SLU", "A", "", "0", "0"]]), "Vuoto non diventa zero");
        Reject(FileFor([["SLU", "A", "NaN", "0", "0"]]), "NaN");
        Reject(FileFor([["SLU", "A", "Infinity", "0", "0"]]), "Infinito");
        Reject(FileFor([["SLU", "", "0", "0", "0"]]), "Nome mancante");
        Reject(FileFor([["Sconosciuta", "A", "0", "0", "0"]]), "Famiglia ignota");
        Reject(FileFor([["SLU", "A", "0", "0", "0", "5", "0"]]), "Taglio non ignorato");
        Reject(FileFor([["Taglio", "A", "0", "5", "0", "0", "0"]]), "Momento non ignorato");
        Reject(FileFor([["SLU", "A", "0", "0", "0", "", "", "Extra"]]), "Colonna extra");
        Reject(FileFor([["SLU", "A", "0", "0", "0"]], d => d.Descendants(ns + "c").First(c => (string?)c.Attribute("r") == "C6").ReplaceWith(new XElement(ns + "c", new XAttribute("r", "C6"), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", "N [N]"))))), "Unità diverse");
        void Formula(XDocument d, bool cache)
        {
            var cell = d.Descendants(ns + "c").First(c => (string?)c.Attribute("r") == "C7"); cell.RemoveNodes(); cell.SetAttributeValue("t", "n"); cell.Add(new XElement(ns + "f", "-50*2")); if (cache) cell.Add(new XElement(ns + "v", "-100"));
        }
        Reject(FileFor([["SLU", "A", "0", "0", "0"]], d => Formula(d, false)), "Formula senza cache");
        var formula = SectionActionsExcel.Read(FileFor([["SLU", "A", "0", "0", "0"]], d => Formula(d, true)));
        Assert(formula.FormulaCells == 1 && formula.Rows[0].N == -100, "Formula con cache segnalata per conferma");
        Assert(SectionActionsExcel.Read(FileFor([["", "", "", "", "", "", ""]])).Rows.Count == 0, "Righe vuote ignorate");
        Console.WriteLine($"Excel azioni: {count} controlli superati."); return count;
    }
}
