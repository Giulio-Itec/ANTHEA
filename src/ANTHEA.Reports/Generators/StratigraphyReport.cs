using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Anthea.Reports;

/// <summary>Data sheet only. Caller validates the editable document before export.</summary>
public static class StratigraphyReport
{
    public static DocxDocument Create(string title, JsonObject data)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body = new XElement(w + "body");
        void Paragraph(string text, bool heading = false)
        {
            body.Add(new XElement(w + "p", new XElement(w + "pPr",
                new XElement(w + "spacing", new XAttribute(w + "after", heading ? 160 : 90)),
                heading ? new XElement(w + "keepNext") : null),
                new XElement(w + "r", new XElement(w + "rPr",
                    new XElement(w + "rFonts", new XAttribute(w + "ascii", "Arial"), new XAttribute(w + "hAnsi", "Arial")),
                    new XElement(w + "sz", new XAttribute(w + "val", heading ? 28 : 21)), heading ? new XElement(w + "b") : null),
                    new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text))));
        }
        static string Value(JsonObject row, string key) => string.IsNullOrWhiteSpace(row[key]?.ToString()) ? "non disponibile" : row[key]!.ToString();
        Paragraph(title, true);
        Paragraph("ANTHEA · Scheda dei dati stratigrafici");
        Paragraph("Scelta per le verifiche: " + (data["scenario"]?.ToString() ?? "Medi") + ". I due insiemi restano distinti; nessuna sostituzione dei minimi mancanti.");
        Paragraph(Value(data, "name"), true); Paragraph(Value(data, "description"));
        Paragraph("Profondità in metri, positive verso il basso, origine al piano campagna. Strati orizzontali contigui.");
        Paragraph("Quota altimetrica piano campagna [m]: " + Value(data, "ground_elevation"));
        Paragraph("Falda: " + Value(data, "water_state") + (data["water_state"]?.ToString() == "Assegnata" ? "; profondità [m]: " + Value(data, "water_depth") : ""));
        int index = 0;
        foreach (var layer in data["layers"]!.AsArray().OfType<JsonObject>())
        {
            Paragraph($"Strato {++index} · {Value(layer, "name")}", true);
            Paragraph(Value(layer, "description"));
            Paragraph("Famiglia prevalente: " + (layer["soil_type"]?.ToString() ?? "Non classificato") + ". Retino grafico convenzionale ANTHEA, senza attribuzione di parametri.");
            Paragraph(layer["infinite"]!.GetValue<bool>() ? "Estensione indefinita in profondità" : "Spessore [m]: " + Value(layer, "thickness"));
            Paragraph("Valori medi", true);
            Paragraph("γ [kN/m³]: " + Value(layer, "gamma") + "; γsat [kN/m³]: " + Value(layer, "gamma_sat"));
            Paragraph("φ′ [°]: " + Value(layer, "phi") + "; c′ [kPa]: " + Value(layer, "c") + "; cu [kPa]: " + Value(layer, "cu"));
            Paragraph("Qualificazione: " + Value(layer, "qualification") + ". Provenienza: " + Value(layer, "source"));
            if (!string.IsNullOrWhiteSpace(layer["notes"]?.ToString())) Paragraph("Note: " + layer["notes"]);
            if (layer["minimum"] is JsonObject minimum)
            {
                Paragraph("Valori minimi", true);
                Paragraph("γ [kN/m³]: " + Value(minimum, "gamma") + "; γsat [kN/m³]: " + Value(minimum, "gamma_sat"));
                Paragraph("φ′ [°]: " + Value(minimum, "phi") + "; c′ [kPa]: " + Value(minimum, "c") + "; cu [kPa]: " + Value(minimum, "cu"));
                Paragraph("Qualificazione: " + Value(minimum, "qualification") + ". Provenienza: " + Value(minimum, "source"));
                Paragraph("Note minimi: " + Value(minimum, "notes"));
            }
            else Paragraph("Valori minimi: non assegnati.");
        }
        Paragraph("Ambito e limiti", true);
        if (data["import_source"] is JsonObject source)
            Paragraph("Importazione AI " + (source["reviewed"] is JsonValue reviewed && reviewed.TryGetValue<bool>(out var flag) && flag ? "revisionata" : "da revisionare") + ": " + Value(source, "file") + "; SHA-256: " + Value(source, "sha256") + "; modello: " + Value(source, "model") + "; data: " + Value(source, "date") + ". " + Value(source, "warnings"));
        Paragraph("Scheda di input, anche incompleta. Non costituisce una verifica geotecnica. Nessuna correlazione automatica o applicazione di coefficienti parziali. I valori non disponibili restano distinti da zero.");
        Paragraph("Un unico livello di falda: esclusi acquiferi multipli, falde sospese e condizioni artesiane. Lo strato indefinito è un’ipotesi di modello, non un’estensione dell’indagine.");
        body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", 11906), new XAttribute(w + "h", 16838)),
            new XElement(w + "pgMar", new XAttribute(w + "top", 1134), new XAttribute(w + "bottom", 1134), new XAttribute(w + "left", 1134), new XAttribute(w + "right", 1134))));
        var package = new DocxDocument();
        package.SetXml("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        package.SetXml("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
        package.SetXml("word/document.xml", new XDocument(new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), body)).ToString());
        return package;
    }
}
