using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Anthea.Reports;

/// <summary>Rendering of the same immutable snapshot used by the sheet. No solver calls or engineering formulas.</summary>
public static class EmbeddedWallReport
{
    private static string F(double? value) => value?.ToString("G7", CultureInfo.GetCultureInfo("it-IT")) ?? "non disponibile";
    public static DocxDocument Create(string title, EmbeddedWallReportData result, bool allStations = false)
    {
        ArgumentNullException.ThrowIfNull(result);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body = new XElement(w + "body");
        XElement Paragraph(string text, bool heading = false) => new(w + "p", new XElement(w + "pPr",
            new XElement(w + "spacing", new XAttribute(w + "after", heading ? 140 : 60)), heading ? new XElement(w + "keepNext") : null),
            new XElement(w + "r", new XElement(w + "rPr", new XElement(w + "rFonts", new XAttribute(w + "ascii", "Arial"), new XAttribute(w + "hAnsi", "Arial")),
                new XElement(w + "sz", new XAttribute(w + "val", heading ? 24 : 18)), heading ? new XElement(w + "b") : null), new XElement(w + "t", text)));
        void P(string text, bool heading = false) => body.Add(Paragraph(text, heading));
        void Table(string[] headers, IEnumerable<string[]> rows)
        {
            var table = new XElement(w + "tbl", new XElement(w + "tblPr", new XElement(w + "tblW", new XAttribute(w + "w", 9638), new XAttribute(w + "type", "dxa"))));
            table.Add(new XElement(w + "tblGrid", headers.Select(_ => new XElement(w + "gridCol", new XAttribute(w + "w", 9638 / headers.Length)))));
            void Row(string[] values, bool header) => table.Add(new XElement(w + "tr", new XElement(w + "trPr", new XElement(w + "cantSplit"), header ? new XElement(w + "tblHeader") : null),
                values.Select(v => new XElement(w + "tc", new XElement(w + "tcPr", new XElement(w + "tcW", new XAttribute(w + "w", 9638 / headers.Length), new XAttribute(w + "type", "dxa"))), Paragraph(v)))));
            Row(headers, true); foreach (var row in rows) Row(row, false); body.Add(table);
        }
        P(title, true);
        P(result.Name + " · Caso " + result.CaseId); P("Azioni: " + result.ActionBasis);
        P(result.Complete ? "Storia completata. La convergenza non attesta tutte le verifiche dell'opera." : "ANALISI INCOMPLETA — sono riportate soltanto le fasi concluse.", true);
        if (!result.Complete) P(result.Diagnostic);
        foreach (string note in result.Scope) P(note);
        P("Dati assegnati", true);
        var input = JsonNode.Parse(result.InputJson)!.AsObject();
        // Document keys are shown with their explicit unit contract, without recomputing derived data.
        string Label(string key) => Labels.GetValueOrDefault(key, key);
        Table(["Dato", "Valore"], input.Where(p => p.Value is not JsonArray && p.Value is not JsonObject && p.Key != "schema")
            .Select(p => new[] { Label(p.Key), p.Value?.ToString() ?? "non assegnato" }));
        foreach (var entry in input.Where(p => p.Value is JsonArray))
        {
            var rows = entry.Value!.AsArray().OfType<JsonObject>().ToArray(); if (rows.Length == 0) continue;
            P(Label(entry.Key), true);
            for (int i = 0; i < rows.Length; i++) P((i + 1) + ". " + string.Join("; ", rows[i].Select(p => Label(p.Key) + ": " + (p.Value?.ToString() ?? "non assegnato"))));
        }
        foreach (var stage in result.Stages)
        {
            P("Fase " + stage.Id, true);
            P($"Incrementi {stage.Increments}; iterazioni {stage.Iterations}. Residuo forze {F(stage.ForceResidualN)} N; residuo momenti {F(stage.MomentResidualNmm)} Nmm.");
            P(allStations ? "Tutte le sezioni campionate." : "Estratto: estremi campionati e sezioni terminali. Ogni riga mantiene le azioni concomitanti; il CSV conserva tutte le sezioni.");
            IEnumerable<EmbeddedWallPointData> points = stage.Points;
            if (!allStations && stage.Points.Count > 0) points = new[] { stage.Points.First(), stage.Points.Last(), stage.Points.MinBy(p => p.Moment)!, stage.Points.MaxBy(p => p.Moment)!,
                stage.Points.MinBy(p => p.Shear)!, stage.Points.MaxBy(p => p.Shear)!, stage.Points.MinBy(p => p.Displacement)!, stage.Points.MaxBy(p => p.Displacement)! }.Distinct().OrderByDescending(p => p.Elevation);
            Table(["z [m] / lato", "N [kN]", "V [kN]", "M [kNm]", "u [mm]", "p sx / dx [kPa]"], points.Select(p =>
                new[] { F(p.Elevation) + " " + p.Side, F(p.Normal), F(p.Shear), F(p.Moment), F(p.Displacement), F(p.LeftPressure) + " / " + F(p.RightPressure) }));
            if (stage.Supports.Count > 0) Table(["Sostegno", "Forza elemento [kN]", "Reazione fascia [kN]", "Riferimento [mm]", "Limite plastico"], stage.Supports.Select(s =>
                new[] { s.Id, F(s.AxialForce), F(s.HorizontalReaction), F(s.ReferenceDisplacement), s.AtCapacity ? "raggiunto" : "non raggiunto" }));
        }
        P("Verifiche delle sezioni", true);
        if (result.Checks.Count == 0) P("Nessuna verifica di sezione eseguita.");
        else
        {
            P("Riepilogo per fase, sezione e metodo: tutti gli esiti non valutati e il controllo governante per utilizzo. Le unità di domanda e capacità sono quelle native dichiarate.");
            foreach (var group in result.Checks.GroupBy(c => (c.Stage, c.Section, c.Method)))
            {
                var governing = group.OrderByDescending(c => c.Outcome == "NotSatisfied").ThenByDescending(c => c.Ratio).First();
                foreach (var c in group.Where(c => c.Outcome == "NotEvaluated").Take(1).Append(governing).Distinct())
                    P($"{c.Stage} · {c.Section} · z={F(c.Elevation)} m ({c.Side}) · {c.Method}: {c.Outcome}; domanda {F(c.Demand)}, capacità {F(c.Capacity)} {c.Unit}; utilizzo {F(c.Ratio)}. N={F(c.Normal)} kN, V={F(c.Shear)} kN, M={F(c.Moment)} kNm. {c.Diagnostic}");
            }
        }
        P("Impronta della revisione: " + result.Fingerprint);
        P("Documento generato dal risultato mostrato nell'interfaccia, senza ricalcolo.");
        body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", 11906), new XAttribute(w + "h", 16838)),
            new XElement(w + "pgMar", new XAttribute(w + "top", 1134), new XAttribute(w + "bottom", 1134), new XAttribute(w + "left", 1134), new XAttribute(w + "right", 1134))));
        var package = new DocxDocument();
        package.SetXml("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        package.SetXml("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
        package.SetXml("word/document.xml", new XDocument(new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), body)).ToString());
        return package;
    }
    public static string Csv(EmbeddedWallReportData result)
    {
        static string Cell(object? value) => "\"" + (value is double d ? d.ToString("G17", CultureInfo.InvariantCulture) : value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("Caso;Fase;Sezione;Quota_m;Lato;u_mm;N_kN;V_kN;M_kNm;p_sx_kPa;p_dx_kPa;Stato;Impronta\r\n");
        foreach (var s in result.Stages) foreach (var p in s.Points)
            text.AppendLine(string.Join(";", new object[] { result.CaseId, s.Id, p.Section, p.Elevation, p.Side, p.Displacement, p.Normal, p.Shear, p.Moment, p.LeftPressure, p.RightPressure, result.Status, result.Fingerprint }.Select(Cell)));
        return text.ToString();
    }
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["name"]="Nome", ["case_id"]="Caso", ["action_basis"]="Origine delle azioni", ["top"]="Quota superiore [m]", ["bottom"]="Quota inferiore [m]", ["width"]="Larghezza (m generale, mm sezione c.a.)",
        ["mesh"]="Lunghezza massima elemento [m]", ["check_kind"]="Tipo verifica", ["substeps"]="Incrementi", ["segments"]="Tratti e rigidezze", ["left_soils"]="Terreno a sinistra", ["right_soils"]="Terreno a destra",
        ["supports"]="Sostegni", ["stages"]="Fasi", ["activations"]="Attivazioni", ["loads"]="Carichi", ["rc_sections"]="Armature", ["section"]="Sezione", ["ei"]="EI [kNm²]", ["weight"]="Peso lineare [kN/m]",
        ["source"]="Fonte", ["gamma"]="γ [kN/m³]", ["gamma_sat"]="γsat [kN/m³]", ["phi"]="φ′ [°]", ["c"]="c′ [kPa]", ["cu"]="cu [kPa]", ["k0"]="K0", ["kh"]="kh [N/mm³]", ["drainage"]="Condizione", ["stiffness_source"]="Fonte kh",
        ["elevation"]="Quota [m]", ["kind"]="Tipo", ["stiffness"]="EA/L [kN/m]", ["angle"]="Inclinazione [°]", ["direction"]="Verso resistente", ["spacing"]="Interasse [m]", ["capacity"]="Capacità [kN]",
        ["id"]="Identificativo", ["left_ground"]="Terreno sx [m]", ["right_ground"]="Terreno dx [m]", ["left_water"]="Falda sx [m]", ["right_water"]="Falda dx [m]", ["left_surcharge"]="Sovraccarico sx [kPa]", ["right_surcharge"]="Sovraccarico dx [kPa]",
        ["distributed_load"]="Carico distribuito [kN/m]", ["stiffness_factor"]="Fattore EI", ["stage"]="Fase", ["support"]="Sostegno", ["prestress"]="Pretensione [kN]", ["horizontal"]="Forza orizzontale [kN]", ["moment"]="Coppia [kNm]", ["downward"]="Forza verso il basso [kN]",
        ["fix_top_u"]="Traslazione testa bloccata", ["fix_top_rotation"]="Rotazione testa bloccata", ["fix_bottom_u"]="Traslazione piede bloccata", ["fix_bottom_rotation"]="Rotazione piede bloccata",
        ["thickness"]="Spessore [mm]", ["cover"]="Copriferro [mm]", ["back_bars"]="Barre retro", ["back_diameter"]="Diametro retro [mm]", ["front_bars"]="Barre fronte", ["front_diameter"]="Diametro fronte [mm]",
        ["fck"]="fck [MPa]", ["fyk"]="fyk [MPa]", ["es"]="Es [MPa]", ["eps_uk"]="εuk", ["creep"]="Viscosità", ["exposure"]="Esposizione"
    };
}
