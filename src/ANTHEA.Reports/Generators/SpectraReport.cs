using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Anthea.Reports;

/// <summary>Report composition only: all engineering quantities and spectral ordinates are supplied by the application service.</summary>
public static class SpectraReport
{
    // OpenXML layout dimension (twips), independent of engineering units.
    private const int TableWidthTwips = 9600;
    public static DocxDocument Create(string title, JsonObject input, JsonObject result)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body = new XElement(w + "body");
        string V(JsonNode? n) => n?.ToString() == "NaN" ? "Non disponibile" : n is JsonValue v && v.TryGetValue<double>(out var d) ? d.ToString("G10", CultureInfo.GetCultureInfo("it-IT")) : n?.ToString() ?? "";
        XElement P(string text, bool heading = false) => new(w + "p", new XElement(w + "pPr", new XElement(w + "spacing", new XAttribute(w + "after", 100)), heading ? new XElement(w + "keepNext") : null), new XElement(w + "r", new XElement(w + "rPr", new XElement(w + "rFonts", new XAttribute(w + "ascii", "Arial"), new XAttribute(w + "hAnsi", "Arial")), new XElement(w + "sz", new XAttribute(w + "val", heading ? 26 : 18)), heading ? new XElement(w + "b") : null), new XElement(w + "t", text)));
        void Table(string[] headers, IEnumerable<string[]> rows)
        {
            int width = TableWidthTwips / headers.Length;
            XElement Row(string[] values, bool header = false) => new(w + "tr", header ? new XElement(w + "trPr", new XElement(w + "tblHeader")) : null, values.Select(value => new XElement(w + "tc", new XElement(w + "tcPr", new XElement(w + "tcW", new XAttribute(w + "w", width), new XAttribute(w + "type", "dxa")), header ? new XElement(w + "shd", new XAttribute(w + "fill", "E8EEF4")) : null), P(value))));
            var table = new XElement(w + "tbl", new XElement(w + "tblPr", new XElement(w + "tblW", new XAttribute(w + "w", 9600), new XAttribute(w + "type", "dxa")), new XElement(w + "tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(side => new XElement(w + side, new XAttribute(w + "val", "single"), new XAttribute(w + "sz", 4), new XAttribute(w + "color", "BCC7D3"))))), new XElement(w + "tblGrid", headers.Select(_ => new XElement(w + "gridCol", new XAttribute(w + "w", width)))), Row(headers, true), rows.Select(row => Row(row)));
            body.Add(table, P(""));
        }
        body.Add(P(title, true), P("ANTHEA · Spettri NTC e isolamento sismico"), P(V(result["reference"])), P("Località: " + V(input["location"]) + "; longitudine " + V(input["longitude"]) + "°; latitudine " + V(input["latitude"]) + "°. Interpolazione: " + V(input["method"]) + "."), P("Origine parametri degli stati limite: " + V(result["hazard_source"]) + "."));
        body.Add(P("Dati di progetto", true), P("VN = " + V(input["nominal"]) + " anni; CU = " + V(input["use"]) + "; sottosuolo " + V(input["soil"]) + "; topografia " + V(input["topography"]) + "; h/H = " + V(input["height"]) + "."), P("Spettro: " + V(input["mode"]) + "; ξ = " + V(input["damping"]) + "%; q0 = " + V(input["q0"]) + "; regolare in altezza: " + V(input["regular"]) + "; q verticale = " + V(input["qv"]) + "."));
        body.Add(P("Reticolo e pericolosità di base", true));
        Table(["Nodo", "Long. [°]", "Lat. [°]", "Peso"], result["site"]!["Nodes"]!.AsArray().Select(n => new[] { V(n!["Id"]), V(n["Longitude"]), V(n["Latitude"]), V(n["Weight"]) }));
        Table(["TR [anni]", "ag [g]", "F0", "Tc* [s]"], result["site"]!["Periods"]!.AsArray().Select(n => new[] { V(n!["ReturnYears"]), V(n["Parameters"]!["AgG"]), V(n["Parameters"]!["F0"]), V(n["Parameters"]!["TcStarSeconds"]) }));
        foreach (var s in result["states"]!.AsArray().OfType<JsonObject>())
        {
            body.Add(P("Stato limite " + V(s["State"]), true), P("VR = " + V(s["ReferenceYears"]) + " anni; TR = " + V(s["ReturnYears"]) + " anni."));
            Table(["ag [g]", "F0", "Tc* [s]", "SS", "CC", "ST"], [new[] { V(s["Parameters"]!["AgG"]), V(s["Parameters"]!["F0"]), V(s["Parameters"]!["TcStarSeconds"]), V(s["Ss"]), V(s["Cc"]), V(s["St"]) }]);
            Table(["η", "q", "TB [s]", "TC [s]", "TD [s]", "Fv"], [new[] { V(s["Eta"]), V(s["Q"]), V(s["Tb"]), V(s["Tc"]), V(s["Td"]), V(s["VerticalF"]) }]);
            var h = s["Horizontal"]!.AsArray(); var v = s["Vertical"]!.AsArray(); var r = s["Reference"]!.AsArray();
            Table(["T or. [s]", "Sa or. [g]", "T vert. [s]", "Sa vert. [g]", "T rif. [s]", "Sa rif. [g]"], Enumerable.Range(0, h.Count).Select(i => new[] { V(h[i]!["PeriodSeconds"]), V(h[i]!["AccelerationG"]), V(v[i]!["PeriodSeconds"]), V(v[i]!["AccelerationG"]), V(r[i]!["PeriodSeconds"]), V(r[i]!["AccelerationG"]) }));
        }
        if (result["isolation"] is JsonArray isolation)
        {
            body.Add(P("Isolamento sismico", true), P("NTC 2018 §7.10.5.3.2. Da T ≥ 0,8 Tis si adotta lo smorzamento dell’isolamento. La risposta a Tis è valutata analiticamente; non si assume il campione precedente della tabella Excel."), P("M = " + V(input["mass"]) + " kN·s²/m; ξ struttura = " + V(input["structure_damping"]) + "%; ξ isolamento = " + V(input["isolation_damping"]) + "%; fattori di rigidezza SLE/SLU = " + V(input["factor_sle"]) + " / " + V(input["factor_slu"]) + "."), P("Tis = 2π√[M/(1000 K)]; F = M · 9,81 · Sa(Tis); d = F/K. K in kN/mm; Sa in g; d in mm."));
            Table(["Gruppo", "Numero", "k singolo [kN/mm]"], new[] { 1, 2 }.Select(i => new[] { V(input[$"group{i}_name"]), V(input[$"group{i}_count"]), V(input[$"group{i}_k"]) }));
            Table(["SL", "K [kN/mm]", "Tis [s]", "Sa [g]", "F [kN]", "d [mm]"], isolation.Select(s => new[] { V(s!["State"]), V(s["StiffnessKnPerMm"]), V(s["PeriodSeconds"]), V(s["AccelerationG"]), V(s["ForceKn"]), V(s["DisplacementMm"]) }));
            foreach (var s in isolation) { body.Add(P(V(s!["State"]) + ": Sa del campione Excel = " + V(s["ExampleSampleAccelerationG"]) + " g (solo confronto).")); Table(["Gruppo", "Numero", "Forza singola [kN]"], s["BearingForces"]!.AsArray().Select(g => new[] { V(g!["Name"]), V(g["Count"]), V(g["ForcePerBearingKn"]) })); }
            body.Add(P("Ambito: spettro e sistema equivalente traslazionale, con Tis non superiore a 4 s in ogni stato limite (NTC 2018 §3.2.3.2). Oltre 4 s occorre una definizione specifica dell’azione sismica. Non sono comprese le verifiche di regolarità, torsione/eccentricità, dispositivi, risposta verticale, spostamenti di progetto e requisiti di applicabilità del modello strutturale."));
        }
        if (result["pendulum"] is JsonObject p) body.Add(P("Pendolo", true), P("N = " + V(input["normal_force"]) + " kN; μ = " + V(input["friction"]) + "; d = " + V(input["displacement"]) + " mm; R = " + V(input["radius"]) + " mm; g = 9806 mm/s²."), P("Fμ = " + V(p["FrictionForceKn"]) + " kN; Ke = " + V(p["EffectiveStiffnessKnPerMm"]) + " kN/mm; ξ = " + V(p["DampingPercent"]) + "%; T = " + V(p["PeriodSeconds"]) + " s."));
        if (input["import_source"] is JsonObject source) body.Add(P("Importazione", true), P(V(source["file"]) + "; SHA-256 " + V(source["sha256"]) + ". " + V(source["mode"]) + "."));
        body.Add(P("Convenzioni del riferimento", true), P("La parte Spettri conserva il comportamento del riferimento NTC 2008: VR minimo 35 anni, limiti dei tempi di ritorno dell’Excel, reticolo e catalogo comunale originali, soglia 0,2 ag per SLV/SLC e convenzione verticale del foglio. Il confronto numerico non estende automaticamente il campo normativo del riferimento."));
        body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", 11906), new XAttribute(w + "h", 16838)), new XElement(w + "pgMar", new XAttribute(w + "top", 1134), new XAttribute(w + "bottom", 1134), new XAttribute(w + "left", 1134), new XAttribute(w + "right", 1134))));
        var package = new DocxDocument();
        package.SetXml("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        package.SetXml("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
        package.SetXml("word/document.xml", new XDocument(new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), body)).ToString()); return package;
    }
}
