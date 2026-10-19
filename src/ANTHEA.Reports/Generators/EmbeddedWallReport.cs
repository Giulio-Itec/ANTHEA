using System.Globalization;
using System.Collections.Frozen;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using GPC.Model.Checking.Contracts;

namespace Anthea.Reports;

/// <summary>Rendering of the same immutable snapshot used by the sheet. No solver calls or engineering formulas.</summary>
public static class EmbeddedWallReport
{
    private const int TableWidthTwips = 9638;
    private static string F(double? value) => value?.ToString("G7", CultureInfo.GetCultureInfo("it-IT")) ?? "non disponibile";
    public static string DisplayLabel(string key) => Labels.GetValueOrDefault(key, key);
    public static string OutcomeText(EngineeringOutcome outcome) => outcome switch
    { EngineeringOutcome.Satisfied => "Soddisfatto", EngineeringOutcome.NotSatisfied => "Non soddisfatto", EngineeringOutcome.NotEvaluated => "Non valutato", _ => outcome.ToString() };
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
            var table = new XElement(w + "tbl", new XElement(w + "tblPr", new XElement(w + "tblW", new XAttribute(w + "w", TableWidthTwips), new XAttribute(w + "type", "dxa"))));
            table.Add(new XElement(w + "tblGrid", headers.Select(_ => new XElement(w + "gridCol", new XAttribute(w + "w", TableWidthTwips / headers.Length)))));
            void Row(string[] values, bool header) => table.Add(new XElement(w + "tr", new XElement(w + "trPr", new XElement(w + "cantSplit"), header ? new XElement(w + "tblHeader") : null),
                values.Select(v => new XElement(w + "tc", new XElement(w + "tcPr", new XElement(w + "tcW", new XAttribute(w + "w", TableWidthTwips / headers.Length), new XAttribute(w + "type", "dxa"))), Paragraph(v)))));
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
        foreach (var hydraulic in result.Hydraulics ?? [])
        {
            P("Filtrazione — fase " + hydraulic.Stage, true);
            P($"Esito {hydraulic.Status}; iterazioni {hydraulic.Iterations}; nodi {hydraulic.Nodes.Count}; elementi {hydraulic.Elements.Count}. {hydraulic.Diagnostic}");
            if (!hydraulic.Complete) continue;
            P($"Portata entrante {F(hydraulic.InflowLitresPerSecondPerMetre)} L/s/m; uscente {F(hydraulic.OutflowLitresPerSecondPerMetre)} L/s/m; bilancio {F(hydraulic.BalanceLitresPerSecondPerMetre)} L/s/m. Residuo nodale massimo {F(hydraulic.ResidualSquareMillimetresPerSecond)} mm²/s.");
            P("Portate riferite a un metro ortogonale alla sezione. Il CSV idraulico conserva tutti i valori nodali e i flussi degli elementi; qui sono riportati gli estremi nodali con la loro posizione.");
            var nodes = new[] { hydraulic.Nodes.MinBy(n => n.Head)!, hydraulic.Nodes.MaxBy(n => n.Head)!, hydraulic.Nodes.MinBy(n => n.Pressure)!, hydraulic.Nodes.MaxBy(n => n.Pressure)! }.Distinct();
            Table(["Nodo", "x [m]", "z [m]", "h [m]", "u [kPa]"], nodes.Select(n => new[] { n.Index.ToString(), F(n.X), F(n.Z), F(n.Head), F(n.Pressure) }));
        }
        P("Verifiche idrauliche locali UPL / HYD", true);
        if ((result.HydraulicChecks?.Count ?? 0) == 0) P("Nessuna verifica UPL/HYD richiesta.");
        foreach (var check in result.HydraulicChecks ?? [])
        {
            P($"{check.Stage} · {check.Element} · {check.Method} · {check.OutcomeText}", true);
            P($"Domanda {F(check.Demand)}, capacità {F(check.Capacity)} {check.Unit}; utilizzo {F(check.Ratio)}. {check.Diagnostic}");
            P(check.Reference); P("Provenienza e applicabilità: " + check.Source);
            if (check.Coefficients.Count > 0) P("Coefficienti e parametri: " + string.Join("; ", check.Coefficients.Select(p => Label(p.Key) + "=" + F(p.Value))));
            if (check.Values.Count > 0) Table(["Dato utilizzato", "Valore", "Unità"], check.Values.Select(v => new[] { v.Name, F(v.Value), v.Unit }));
            if (check.Actions.Count > 0)
            {
                P("Risultanti UPL positive verso l'alto; pesi totali negativi. La resistenza aggiuntiva Rd è già di progetto.");
                Table(["Azione", "Categoria", "Fk [kN]", "γ", "Fd [kN]"], check.Actions.Select(a => new[] { a.Id, a.CategoryText, F(a.CharacteristicUpwardForce), F(a.Factor), F(a.DesignUpwardForce) }));
                foreach (var action in check.Actions) P(action.Id + ": " + action.Source);
            }
        }
        P("Stabilità globale", true);
        if ((result.GlobalChecks?.Count ?? 0) == 0) P("Nessuna verifica globale richiesta.");
        foreach (var g in result.GlobalChecks ?? [])
        {
            P($"{g.Stage} · {g.Id} · {g.Combination} · {g.OutcomeText}", true);
            P($"Domanda γR={F(g.Demand)}; capacità F={F(g.Capacity)}; utilizzo conclusivo={F(g.Ratio)}. Stato della ricerca: {g.SearchStatus}.");
            P(g.Reference); P(g.Source); P(g.Diagnostic);
            P($"Superfici provate {g.Tried}, ammissibili {g.Valid}, risolte {g.Solved}; fallimenti numerici {g.Failures}. Minimo al bordo: {(g.Boundary ? "sì" : "no")}; F con doppio numero di conci: {F(g.RefinedFactor)}; residuo: {F(g.Residual)}.");
            if (g.Coefficients.Count > 0) P("Coefficienti: " + string.Join("; ", g.Coefficients.Select(c => Label(c.Key) + "=" + F(c.Value))));
            if (g.Actions.Count > 0)
            {
                Table(["Azione", "γ", "ψ", "γ·ψ"], g.Actions.Select(a => new[] { a.Id, F(a.PartialFactor), F(a.Psi), F(a.CombinedFactor) }));
                foreach (var a in g.Actions) P(a.Id + " · " + a.Category + " · " + (a.Unfavourable ? "sfavorevole" : "favorevole") + ": " + a.Source);
            }
            if (g.Circle is { } circle) P($"Cerchio critico nelle coordinate globali (valle a sinistra, quota testa zero): centro ({F(circle.X)}; {F(circle.Y)}) m, raggio {F(circle.Radius)} m; estremi x={F(circle.Left)} / {F(circle.Right)} m.");
            if (allStations && g.Slices.Count > 0)
                Table(["Concio", "x sx / dx [m]", "z base [m]", "u [kPa]", "N′ [kN/m]", "R / mobil. [kN/m]"],
                    g.Slices.Select(s => new[] { s.Index.ToString(), F(s.Left) + " / " + F(s.Right), F(s.BaseY), F(s.Pressure), F(s.NormalEffective), F(s.Resistance) + " / " + F(s.Mobilized) }));
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
        P("Verifiche dei sostegni metallici", true);
        if ((result.SteelSupports?.Count ?? 0) == 0) P("Nessuna verifica dei sostegni metallici eseguita.");
        var steelNotes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in result.SteelSupports ?? [])
        {
            P($"{s.Stage} · {s.Support} · {s.Kind} · {s.OutcomeText}", true);
            P($"N={F(s.NormalForce)} kN, positivo a trazione, per singolo elemento. Rigidezza assiale assegnata nell'analisi: {F(s.AnalysedAxialStiffness)} kN/m. Classe a compressione: {s.CompressionClass?.ToString() ?? "non applicabile/non disponibile"}. Limite costitutivo raggiunto: {(s.AtModelCapacity ? "sì" : "no")}.");
            P(s.Source); if (steelNotes.Add(s.Diagnostic)) P(s.Diagnostic);
            foreach (var c in s.Checks)
            {
                P($"{c.Method}: {c.OutcomeText}. Domanda {F(c.Demand)} kN, capacità {F(c.Capacity)} kN, utilizzo {F(c.Ratio)}, γM={F(c.PartialFactor)}.");
                if (c.CriticalForce.HasValue) P($"Ncr={F(c.CriticalForce)} kN; λbar={F(c.NormalizedSlenderness)}, snellezza geometrica={F(c.GeometricSlenderness)}; curva {c.Curve}, χ={F(c.Reduction)}.");
            }
            foreach (string note in s.Checks.Select(c => c.Reference + ". " + c.Diagnostic).Distinct(StringComparer.Ordinal)) P(note);
        }
        P("Verifiche delle sezioni", true);
        if (result.Checks.Count == 0) P("Nessuna verifica di sezione eseguita.");
        else
        {
            P("Riepilogo per fase, sezione e metodo: tutti gli esiti non valutati e il controllo governante per utilizzo. Le unità di domanda e capacità sono quelle native dichiarate.");
            foreach (var group in result.Checks.GroupBy(c => (c.Stage, c.Section, c.Method)))
            {
                var governing = group.OrderByDescending(c => c.Outcome == EngineeringOutcome.NotSatisfied).ThenByDescending(c => c.Ratio).First();
                foreach (var c in group.Where(c => c.Outcome == EngineeringOutcome.NotEvaluated).Take(1).Append(governing).Distinct())
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
    public static string SteelCsv(EmbeddedWallReportData result)
    {
        static string Cell(object? value) => "\"" + (value is double d ? d.ToString("G17", CultureInfo.InvariantCulture) : value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("Caso;Fase;Sostegno;Tipo;N_kN_trazione_positiva;Rigidezza_kN_m;LimiteCostitutivo;Classe;Metodo;Domanda_kN;Capacita_kN;Utilizzo;Esito;gammaM;Ncr_kN;LambdaBar;Snellezza;Chi;Curva;Riferimento;Fonte;Diagnostica;Impronta\r\n");
        foreach (var s in result.SteelSupports ?? [])
        {
            var checks = s.Checks.Select(c => (EmbeddedWallSteelCheckData?)c).DefaultIfEmpty();
            foreach (var c in checks) text.AppendLine(string.Join(";", new object?[] { s.CaseId, s.Stage, s.Support, s.Kind, s.NormalForce,
                s.AnalysedAxialStiffness, s.AtModelCapacity, s.CompressionClass, c?.Method, c?.Demand, c?.Capacity, c?.Ratio,
                c?.Outcome ?? s.Outcome, c?.PartialFactor, c?.CriticalForce, c?.NormalizedSlenderness, c?.GeometricSlenderness, c?.Reduction,
                c?.Curve, c?.Reference, s.Source, s.Diagnostic + " " + c?.Diagnostic, result.Fingerprint }.Select(Cell)));
        }
        return text.ToString();
    }
    public static string HydraulicCsv(EmbeddedWallReportData result)
    {
        static string Cell(object? value) => "\"" + (value is double d ? d.ToString("G17", CultureInfo.InvariantCulture) : value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("Caso;Fase;Tipo;Nodo_A;Nodo_B;Nodo_C;x_m;z_m;h_m;u_kPa;grad_h_x;grad_h_z;q_x_m_s;q_z_m_s;k_rel;Drenante;Stato;Impronta\r\n");
        foreach (var h in result.Hydraulics ?? [])
        {
            foreach (var n in h.Nodes) text.AppendLine(string.Join(";", new object?[] { result.CaseId, h.Stage, "Nodo", n.Index, null, null, n.X, n.Z, n.Head, n.Pressure, null, null, null, null, null, n.Draining, h.Status, result.Fingerprint }.Select(Cell)));
            foreach (var e in h.Elements) text.AppendLine(string.Join(";", new object?[] { result.CaseId, h.Stage, "Elemento", e.A, e.B, e.C, null, null, null, null, e.GradientX, e.GradientZ, e.FluxX, e.FluxZ, e.RelativeConductivity, null, h.Status, result.Fingerprint }.Select(Cell)));
        }
        return text.ToString();
    }
    public static string HydraulicChecksCsv(EmbeddedWallReportData result)
    {
        static string Cell(object? value) => "\"" + (value is double d ? d.ToString("G17", CultureInfo.InvariantCulture) : value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("Caso;Fase;Elemento;Metodo;Domanda;Capacita;Unita;Utilizzo;Esito;Riferimento;Fonte;Diagnostica;Impronta\r\n");
        foreach (var c in result.HydraulicChecks ?? [])
            text.AppendLine(string.Join(";", new object?[] { c.CaseId, c.Stage, c.Element, c.Method, c.Demand, c.Capacity, c.Unit, c.Ratio, c.Outcome, c.Reference, c.Source, c.Diagnostic, result.Fingerprint }.Select(Cell)));
        return text.ToString();
    }
    public static string GlobalCsv(EmbeddedWallReportData result)
    {
        static string Cell(object? value) => "\"" + (value is double d ? d.ToString("G17", CultureInfo.InvariantCulture) : value?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
        var text = new StringBuilder("Caso;Fase;Verifica;Combinazione;StatoRicerca;Esito;gammaR;F;UtilizzoConclusivo;UtilizzoTentativo;Concio;xSx_m;xDx_m;zBase_m;zTop_m;Wterreno_kN_m;Wparete_kN_m;V_kN_m;H_kN_m;u_kPa;N_eff_kN_m;R_kN_m;Mobilitata_kN_m;Diagnostica;Impronta\r\n");
        foreach (var g in result.GlobalChecks ?? [])
        {
            object?[] Prefix() => [g.CaseId, g.Stage, g.Id, g.Combination, g.SearchStatus, g.Outcome, g.Demand, g.Capacity, g.Ratio, g.TrialRatio];
            text.AppendLine(string.Join(";", Prefix().Concat(new object?[13]).Concat(new object?[] { g.Diagnostic, result.Fingerprint }).Select(Cell)));
            foreach (var s in g.Slices) text.AppendLine(string.Join(";", Prefix().Concat(new object?[] { s.Index, s.Left, s.Right, s.BaseY, s.TopY, s.SoilWeight, s.BodyWeight,
                s.VerticalLoad, s.HorizontalLoad, s.Pressure, s.NormalEffective, s.Resistance, s.Mobilized, g.Diagnostic, result.Fingerprint }).Select(Cell)));
        }
        return text.ToString();
    }
    private static readonly FrozenDictionary<string, string> Labels = new Dictionary<string, string>()
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
        ["fck"]="fck [MPa]", ["fyk"]="fyk [MPa]", ["es"]="Es [MPa]", ["eps_uk"]="εuk", ["creep"]="Viscosità", ["exposure"]="Esposizione",
        ["hydraulic_mode"]="Pressioni interstiziali", ["hydraulic_left_extent"]="Estensione dominio sx [m]", ["hydraulic_right_extent"]="Estensione dominio dx [m]",
        ["hydraulic_bottom"]="Quota fondo impermeabile [m]", ["hydraulic_thickness"]="Spessore parete impermeabile [m]", ["hydraulic_mesh"]="Passo mesh idraulica [m]", ["hydraulic_iterations"]="Limite iterazioni idrauliche",
        ["ignore_suction"]="Suzione trascurata nel trasferimento meccanico", ["hydraulic_left"]="Strati idraulici sx", ["hydraulic_right"]="Strati idraulici dx",
        ["kxx"]="Kxx [m/s]", ["kzz"]="Kzz [m/s]", ["kxz"]="Kxz [m/s]", ["alpha"]="Van Genuchten α [1/m]", ["n"]="Van Genuchten n",
        ["hydraulic_weight_mode"]="Criterio pesi del terreno", ["hydraulic_standard"]="Norma idraulica", ["hydraulic_situation"]="Situazione idraulica",
        ["hydraulic_upl"]="Corpi UPL", ["hydraulic_actions"]="Azioni caratteristiche UPL", ["hydraulic_hyd"]="Punti e colonne HYD",
        ["water_resultant"]="Origine risultante acqua", ["x_start"]="x iniziale [m]", ["x_end"]="x finale [m]", ["x"]="x [m]", ["z"]="z [m]", ["z_bottom"]="z inferiore [m]",
        ["body_width"]="Larghezza corpo [m]", ["top_pressure"]="Pressione superiore [kPa]", ["water_category"]="Categoria acqua", ["rd"]="Rd già di progetto [kN]", ["rd_source"]="Fonte e coefficienti di Rd",
        ["check"]="Verifica", ["category"]="Categoria", ["upward_force"]="Forza caratteristica positiva in alto [kN]", ["method"]="Metodo", ["field_source"]="Origine dato HYD", ["gradient"]="Gradiente ascendente",
        ["pressure"]="Pressione interstiziale [kPa]", ["hydrostatic_pressure"]="Pressione idrostatica [kPa]", ["effective_stress"]="Tensione efficace senza filtrazione [kPa]",
        ["vertical_flow"]="Terreno saturo e flusso prevalentemente verticale", ["outflow_confirmed"]="Frontiera di efflusso confermata",
        ["gammaG1_unfavourable"]="γG1 sfavorevole", ["gammaG1_favourable"]="γG1 favorevole", ["gammaG2_unfavourable"]="γG2 sfavorevole", ["gammaG2_favourable"]="γG2 favorevole",
        ["gammaQ_unfavourable"]="γQ sfavorevole", ["gammaQ_favourable"]="γQ favorevole", ["gammaM_phi_for_Rd"]="γM tanφ per Rd", ["gammaM_c_for_Rd"]="γM c per Rd", ["gammaM_cu_for_Rd"]="γM cu per Rd",
        ["gammaR"]="γR", ["criticalGradient"]="Gradiente critico", ["waterUnitWeight"]="γw [N/mm³]",
        ["global_checks"]="Stabilità globale", ["global_actions"]="Selezione azioni globali", ["standard"]="Norma", ["situation"]="Situazione",
        ["global_thickness"]="Spessore parete continua [m]", ["left_extent"]="Estensione terreno sx [m]", ["right_extent"]="Estensione terreno dx [m]",
        ["valley_direction"]="Verso valle (+1 destra, −1 sinistra)", ["characteristic_confirmed"]="Dati caratteristici confermati", ["undrained"]="Non drenato",
        ["exit_min"]="Uscita minima [m]", ["exit_max"]="Uscita massima [m]", ["entry_min"]="Ingresso minimo [m]", ["entry_max"]="Ingresso massimo [m]",
        ["depth_min"]="Profondità minima sotto testa [m]", ["depth_max"]="Profondità massima sotto testa [m]", ["grid"]="Nodi di ricerca per direzione",
        ["slices"]="Conci", ["refinements"]="Raffinamenti", ["unfavourable"]="Sfavorevole", ["psi"]="Fattore di combinazione ψ",
        ["gammaM_phi"]="γM tanφ", ["gammaM_c"]="γM c′", ["gammaM_cu"]="γM cu", ["soilWeightFactor"]="Fattore peso terreno", ["bodyWeightFactor"]="Fattore peso parete",
        ["steel_members"]="Acciaio dei sostegni", ["steel_standard"]="Norma acciaio sostegni", ["steel_check_kind"]="Combinazione acciaio sostegni",
        ["family"]="Famiglia della sezione", ["diameter"]="Diametro [mm]", ["formation"]="Formazione", ["nominal_grade"]="Grado nominale S",
        ["fy"]="fy per spessore effettivo [MPa]", ["fu"]="fu per spessore effettivo [MPa]", ["elastic_modulus"]="E [MPa]", ["material_source"]="Fonte materiale",
        ["net_area"]="Area netta [mm²]", ["net_area_source"]="Fonte area netta", ["length_1"]="L0 piano 1 [m]", ["length_2"]="L0 piano 2 [m]",
        ["length_source"]="Schema dei vincoli", ["axial_only"]="Sola azione assiale centrata confermata", ["is_bridge"]="Elemento di ponte"
    }.ToFrozenDictionary();
}
