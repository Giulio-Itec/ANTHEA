using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace X.Core;

/// <summary>DOCX of the current Checker results; no analysis or rounding of stored data occurs here.</summary>
public static class ReportConcrete
{
    public static readonly (string Key, string Label)[] Sections = [("geometria", "Input: geometria, armature, staffe e trefoli"), ("materiali", "Materiali"), ("coefficienti", "Coefficienti normativi e modifiche"), ("azioni", "Tutte le sollecitazioni"), ("dominio3d", "Verifiche dominio 3D"), ("dominio2d", "Verifiche dominio 2D"), ("SLE", "SLE rara"), ("SLE_FREQ", "SLE frequente"), ("SLE_QP", "SLE quasi permanente"), ("taglio", "Verifiche a taglio"), ("sle_tutte", "SLE: stampa tutte le combinazioni (anziché inviluppo)"), ("dettagli", "Dettagli tensioni e deformazioni di barre e vertici"), ("grafici", "Grafici nelle rispettive sezioni")];
    public sealed record EnvelopeValue(string Label, string Id, double Value);
    public static KeyValuePair<string, JsonNode?>? Governing(JsonObject rows, bool cracking)
    {
        var candidates = rows.Where(kv => J.Number(cracking ? kv.Value?["CrackResult"]?["Ratio"] : kv.Value?["Ratio"]) is double n && double.IsFinite(n));
        return candidates.OrderByDescending(kv => J.Number(cracking ? kv.Value?["CrackResult"]?["Ratio"] : kv.Value?["Ratio"])).Select(kv => (KeyValuePair<string, JsonNode?>?)kv).FirstOrDefault();
    }
    public static IReadOnlyList<EnvelopeValue> Envelope(JsonObject rows, bool tendons)
    {
        var values = new List<EnvelopeValue>();
        foreach (var (prefix, material) in new[] { ("C", "CLS"), ("S", "Acciaio"), ("P", "Trefoli") }.Where(m => tendons || m.Item1 != "P"))
            foreach (bool strain in new[] { false, true }) foreach (bool minimum in new[] { true, false })
            {
                string field = (strain ? "E" + prefix.ToLowerInvariant() : prefix) + (minimum ? "Min" : "Max");
                var candidates = rows.Select(kv => (kv.Key, Value: J.Number(kv.Value?["State"]?["Response"]?[field]))).Where(v => v.Value is double n && double.IsFinite(n));
                var extreme = (minimum ? candidates.OrderBy(v => v.Value) : candidates.OrderByDescending(v => v.Value)).FirstOrDefault();
                if (extreme.Value is double value) values.Add(new($"{material} · {(strain ? "ε" : "σ")}{(minimum ? "min" : "max")} [{(strain ? "‰" : "MPa")}]", extreme.Key, value));
            }
        return values;
    }
    public static void Write(string path, string title, JsonObject data, JsonObject result, HashSet<string> options, IReadOnlyList<ImmagineReport>? images = null)
    {
        if (options.Count == 0) throw new ArgumentException("Selezionare almeno un contenuto del report.");
        if (result.S("motore") != "GPCChecker.Concrete.dll") throw new ArgumentException("Risultati Checker aggiornati non disponibili.");
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main", r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var body = new XElement(w + "body"); var input = data["input"]!; var settings = data["workspace_ca"]!;
        var selected = options.Contains("grafici") ? (images ?? []).Where(i => options.Contains(i.Categoria)).ToArray() : [];
        XElement Paragraph(string text, string style = "Normal") => new(w + "p", new XElement(w + "pPr", new XElement(w + "pStyle", new XAttribute(w + "val", style))),
            new XElement(w + "r", new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
        void P(string text, string style = "Normal") => body.Add(Paragraph(text, style));
        void Heading(string text) => P(text, "Heading1");
        void Subheading(string text) => P(text, "Heading2");
        string F(JsonNode? node) => EngineeringFormat.Number(J.Number(node));
        void Table(string[] headers, IEnumerable<string[]> source)
        {
            var rows = source.ToArray(); if (rows.Length == 0) { P("Nessun dato inserito o risultato disponibile."); return; }
            var weights = headers.Select(h => h.Contains("Esito") || h == "Avviso" ? 2.4 : h == "Grandezza" ? 2.2 : h.Contains("Combinazione") ? 1.8 : h == "Parametro" ? 1.3 : 1d).ToArray();
            var widths = weights.Select(v => (int)Math.Round(9360 * v / weights.Sum())).ToArray(); widths[^1] += 9360 - widths.Sum();
            var table = new XElement(w + "tbl", new XElement(w + "tblPr", new XElement(w + "tblW", new XAttribute(w + "w", 9360), new XAttribute(w + "type", "dxa")),
                new XElement(w + "tblLayout", new XAttribute(w + "type", "fixed")),
                new XElement(w + "tblCellMar", new[] { "top", "left", "bottom", "right" }.Select(s => new XElement(w + s, new XAttribute(w + "w", 70), new XAttribute(w + "type", "dxa")))),
                new XElement(w + "tblBorders", new[] { "top", "bottom", "insideH" }.Select(s => new XElement(w + s, new XAttribute(w + "val", "single"), new XAttribute(w + "sz", 4), new XAttribute(w + "color", "D9E0E8"))))),
                new XElement(w + "tblGrid", widths.Select(width => new XElement(w + "gridCol", new XAttribute(w + "w", width)))));
            foreach (var (cells, i) in new[] { headers }.Concat(rows).Select((cells, i) => (cells, i)))
                table.Add(new XElement(w + "tr", new XElement(w + "trPr", new XElement(w + "cantSplit"), i == 0 ? new XElement(w + "tblHeader") : null),
                    cells.Select((text, column) => {
                        var p = Paragraph(text, i == 0 ? "TableHeader" : "TableText");
                        if (J.Number(JsonValue.Create(text)) is double) p.Element(w + "pPr")!.Add(new XElement(w + "jc", new XAttribute(w + "val", "center")));
                        return new XElement(w + "tc", new XElement(w + "tcPr", new XElement(w + "tcW", new XAttribute(w + "w", widths[column]), new XAttribute(w + "type", "dxa")), new XElement(w + "vAlign", new XAttribute(w + "val", "center")), i == 0 ? new XElement(w + "shd", new XAttribute(w + "fill", "E8EFF7")) : null), p);
                    })));
            body.Add(table); P("");
        }
        void Parameters(string heading, JsonNode values, (string Key, string Label)[] fields)
        { Subheading(heading); Table(["Parametro", "Valore"], fields.Where(f => values[f.Key] is not null).Select(f => new[] { f.Label, J.Number(values[f.Key]) is double n ? EngineeringFormat.Number(n) : values.S(f.Key) })); }
        var names = SectionWorkspace.Sets.ToDictionary(family => family,
            family => data["combinazioni"]!.Array(family).GroupBy(row => row.S("id")).ToDictionary(g => g.Key, g => g.First().S("nome", g.Key)));
        string Name(string family, string id) => names[family].GetValueOrDefault(id, id);
        P("Relazione della sezione in calcestruzzo armato", "Title"); P(title, "Subtitle"); P("ANTHEA · " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        Heading("Ambito e limiti");
        P("Normativa selezionata: " + settings.S("normativa") + ". Motore GPC Checker collegato tramite DLL. Compressione negativa; geometria in mm, tensioni in MPa, deformazioni in ‰, azioni N e V in kN, momenti in kNm. Arrotondamenti solo di presentazione.");
        P(ConcreteStandards.Note(settings.S("normativa")));
        P("Questo report non attesta una verifica normativa completa. La presenza di un dominio o di tensioni calcolate non implica la conformità delle altre verifiche. I filtri e le opzioni grafiche non escludono combinazioni dai calcoli. Leggere gli esiti non determinati, fuori piano e non implementati.");
        P("Taglio: modello NTC per sezioni rettangolari/a T non precompresse; torsione, interazione biassiale e gerarchia sismica escluse. Circolare, spirale e generica richiedono un modello dedicato. Fessurazione: limiti di applicabilità riportati per combinazione; nessun esito sostitutivo nei casi non supportati.");
        if (!string.IsNullOrWhiteSpace(settings.S("nota"))) P("Nota del foglio: " + settings.S("nota"));
        if (result["errori_calcolo"] is JsonObject errors) foreach (var (key, value) in errors) P("Calcolo non disponibile — " + key + ": " + value);
        if (options.Contains("geometria"))
        {
            Heading("Input");
            Parameters("Geometria della sezione", input, new (string, string)[] { ("shape", "Forma"), ("diameter_mm", "D [mm]"), ("width_mm", "b [mm]"), ("height_mm", "h [mm]"), ("flange_width_mm", "bf [mm]"), ("web_width_mm", "bw [mm]"), ("flange_thickness_mm", "hf [mm]"), ("cover_mm", "Copriferro netto [mm]") }.Where(f => f.Item1 switch { "diameter_mm" => input.S("shape") == "Circolare", "width_mm" => input.S("shape") == "Rettangolare", "height_mm" => input.S("shape") != "Circolare", "flange_width_mm" or "web_width_mm" or "flange_thickness_mm" => input.S("shape") == "A T", _ => true }).ToArray());
            var geometry = new SezioneCA(input.AsObject());
            Figures("geometria");
            Subheading("Armature longitudinali"); Table(["ID", "x [mm]", "y [mm]", "Ø [mm]"], geometry.Bars.Select((b, i) => new[] { "B" + (i + 1).ToString("D2"), EngineeringFormat.Number(b.X), EngineeringFormat.Number(b.Y), EngineeringFormat.Number(b.Diametro) }));
            Parameters("Staffe", input, [("transverse_bar_diameter_mm", "Ø [mm]"), ("transverse_spacing_mm", "Passo [mm]")]);
            if (settings["taglio"] is JsonObject stirrups) Table(["Parametro", "Valore"], new[] { "tipo_staffa", "rami_x", "rami_y", "rami_interni" }.Select(k => new[] { k.Replace('_', ' '), stirrups.S(k) }));
            if (settings.Array("trefoli").Count > 0) { Subheading("Trefoli"); Table(["ID", "x [mm]", "y [mm]", "Ap [mm²]", "σp0 [MPa]"], settings.Array("trefoli").Select(t => new[] { t.S("id"), F(t?["x"]), F(t?["y"]), F(t?["area"]), F(t?["sigma0"]) })); }
        }
        if (options.Contains("materiali"))
        {
            Heading("Materiali");
            Parameters("Calcestruzzo", input, [("classe_cls", "Classe CLS"), ("materiale_cls_nome", "Materiale CLS custom"), ("cls_diagramma", "Diagramma CLS"), ("fck_mpa", "fck [MPa]"), ("gettato_sottile", "Riduzione NTC per getto sottile")]);
            Parameters("Acciaio per armature", input, [("materiale_acciaio_nome", "Acciaio custom"), ("fyk_mpa", "fyk [MPa]"), ("steel_modulus_mpa", "Es [MPa]"), ("steel_fu_mpa", "fu [MPa]"), ("steel_eps_u", "εu [‰]"), ("steel_diagramma", "Diagramma acciaio")]);
            if (settings.Array("trefoli").Count > 0) { Subheading("Acciaio da precompressione"); Table(["Trefolo", "Ep [MPa]", "fpyk [MPa]", "fpk [MPa]", "εpu [‰]"], settings.Array("trefoli").Select(t => new[] { t.S("id"), F(t?["Ep"]), F(t?["fpyk"]), F(t?["fpk"]), F(t?["eps_u"]) })); }
        }
        if (options.Contains("coefficienti"))
        {
            Heading("Coefficienti normativi"); var defaults = ConcreteStandards.Defaults(settings.S("normativa")); var effective = ConcreteStandards.Effective(input.AsObject(), settings.AsObject());
            Table(["Coefficiente", "Predefinito DLL", "Applicato", "Modifica"], ConcreteStandards.Coefficients.Select(c =>
            { double value = (double)effective.GetType().GetProperty(c.Key)!.GetValue(effective)!; return new[] { c.Label, F(defaults[c.Key]), EngineeringFormat.Number(value), Math.Abs(value - defaults.D(c.Key)) > 1e-12 ? "Personalizzato" : "No" }; }));
            P("I coefficienti accidentali/FRC esposti non attivano automaticamente verifiche accidentali o materiali fibrorinforzati. L’eventuale fattore NTC 0,80 per getti sottili è applicato separatamente ai parametri pertinenti.");
        }
        if (options.Contains("azioni"))
        {
            Heading("Sollecitazioni");
            foreach (string family in SectionWorkspace.Sets)
            { Subheading(SectionWorkspace.Label(family)); Table(["Combinazione", "N [kN]", "Mx [kNm]", "My [kNm]"], data["combinazioni"]!.Array(family).Select(c => new[] { c.S("nome"), F(c!.Array("azioni").ElementAtOrDefault(0)), F(c.Array("azioni").ElementAtOrDefault(1)), F(c.Array("azioni").ElementAtOrDefault(2)) })); }
            Subheading("Taglio"); Table(["Combinazione", "N [kN]", "Vx [kN]", "Vy [kN]"], settings["taglio"].Array("azioni").Select(c => new[] { c.S("nome"), F(c?["N"]), F(c?["Vx"]), F(c?["Vy"]) }));
        }
        foreach (string dimension in new[] { "3D", "2D" }) if (options.Contains("dominio" + dimension.ToLowerInvariant()))
        {
            Heading("Verifiche dominio " + dimension);
            var domainOptions = settings["dominio" + dimension.ToLowerInvariant()]!;
            Parameters("Opzioni dominio " + dimension, domainOptions, [("criterio", "Criterio"), ("strategia", "Strategia"), ("tipo", "Piano"), ("N", "N piano [kN]"), ("theta", "θ [°]"), ("assi", "Assi"), ("origine_x", "Origine x [mm]"), ("origine_y", "Origine y [mm]"), ("rotazione", "Rotazione [°]"), ("proietta", "Proiezione sul piano"), ("trazione_cls", "CLS teso"), ("angoli", "Direzioni"), ("interpolazione", "Interpolazione mesh")]);
            foreach (string family in new[] { "SLU", "SLV" })
            {
                Subheading("Dominio " + dimension + " " + SectionWorkspace.Label(family));
                var rows = result["domini"]?[dimension + ":" + family] as JsonObject ?? new();
                Table(["Combinazione", "NRd [kN]", "MxRd [kNm]", "MyRd [kNm]", "η [-]", "Esito"], rows.Select(kv => new[] { Name(family, kv.Key), F(kv.Value?["Resistance"]?["N"]), F(kv.Value?["Resistance"]?["Mx"]), F(kv.Value?["Resistance"]?["My"]), F(kv.Value?["Utilization"]), kv.Value.S("Status") }));
            }
            Figures("dominio" + dimension.ToLowerInvariant());
        }
        foreach (string family in SectionWorkspace.Sets.Skip(2)) if (options.Contains(family))
        {
            Heading("SLE " + SectionWorkspace.Label(family));
            bool hasTendons = settings.Array("trefoli").Count > 0;
            Parameters("Opzioni di verifica", settings["sle"]![family]!, new (string Key, string Label)[] { ("modello", "Analisi"), ("n_armature", "n armature"), ("phi", "φ armature"), ("n_trefoli", "n trefoli riferimento"), ("phi_trefoli", "φ trefoli"), ("trazione_cls", "CLS teso"), ("assi", "Assi"), ("esposizione", "Esposizione"), ("durata", "Durata"), ("sensibilita", "Sensibilità"), ("aderenza", "Aderenza") }.Where(f => hasTendons || f.Key is not ("n_trefoli" or "phi_trefoli")).ToArray());
            var rows = result["tensioni"]?[family] as JsonObject ?? new();
            bool stressRequired = SleCheckScope.Stress(family), crackRequired = SleCheckScope.Cracking(family, settings);
            var envelope = stressRequired ? Envelope(rows, hasTendons) : [];
            var worstStress = stressRequired ? Governing(rows, false) : null; var worstCrack = crackRequired ? Governing(rows, true) : null;
            bool all = options.Contains("sle_tutte");
            P(all ? "Stampa completa delle combinazioni." : "Inviluppo delle combinazioni calcolate: ogni estremo proviene dalla combinazione indicata. Gli estremi non costituiscono uno stato simultaneo e non sostituiscono le verifiche. Le combinazioni con esito non determinato sono riportate separatamente.");
            if (stressRequired)
            {
            Subheading("Tensioni · " + (all ? "tutte le combinazioni" : "combinazione governante"));
            var stressRows = all ? rows.ToArray() : worstStress is { } ws ? new[] { ws } : [];
            Table(["Combinazione", "σc,min [MPa]", "|σs|max [MPa]", "ησ [-]", "Esito tensioni"], stressRows.Select(kv => new[] { Name(family, kv.Key), F(kv.Value?["State"]?["sigma_cls"]), F(kv.Value?["State"]?["sigma_acciaio"]), F(kv.Value?["Ratio"]), kv.Value.S("Status") }));
            if (!all && worstStress is null) P("Nessuna combinazione governante tensionale determinabile: consultare gli esiti sotto riportati.");
            Subheading("Inviluppo tensioni e deformazioni");
            Table(["Grandezza", "Estremo", "Combinazione di origine"], envelope.Select(v => new[] { v.Label, EngineeringFormat.Number(v.Value), Name(family, v.Id) }));
            P("Compressione negativa: min e max sono estremi algebrici. Le deformazioni sono quelle incrementali native Checker, senza la deformazione iniziale dei trefoli.");
            }
            if (crackRequired)
            {
            Subheading("Fessurazione · " + (all ? "tutte le combinazioni" : "combinazione governante"));
            var crackRows = all ? rows.ToArray() : worstCrack is { } wc ? new[] { wc } : [];
            Table(["Combinazione", "wk [mm]", "Limite [mm]", "ηw [-]", "Esito fessurazione"], crackRows.Select(kv => new[] { Name(family, kv.Key), F(kv.Value?["CrackResult"]?["Width"]), F(kv.Value?["CrackResult"]?["Limit"]), F(kv.Value?["CrackResult"]?["Ratio"]), kv.Value.S("Cracking") }));
            if (!all && worstCrack is null) P("Nessuna combinazione governante per ηw determinabile: sono conservati gli esiti di decompressione, non applicabilità o calcolo non disponibile.");
            }
            if (!all)
            {
                var notices = rows.SelectMany(kv => new[] { (Check: "Tensioni", Ratio: J.Number(kv.Value?["Ratio"]), Status: kv.Value.S("Status")), (Check: "Fessurazione", Ratio: J.Number(kv.Value?["CrackResult"]?["Ratio"]), Status: kv.Value.S("Cracking")) }.Where(v => (v.Check == "Tensioni" ? stressRequired : crackRequired) && (v.Ratio is null || !double.IsFinite(v.Ratio.Value))).Select(v => new[] { Name(family, kv.Key), v.Check, v.Status })).ToArray();
                if (notices.Length > 0) { Subheading("Esiti senza tasso numerico / verifiche non determinate"); Table(["Combinazione", "Verifica", "Esito"], notices); }
                var missing = data["combinazioni"]!.Array(family).Where(c => !rows.ContainsKey(c.S("id"))).Select(c => new[] { c.S("nome"), "Risultato non disponibile" }).ToArray();
                if (missing.Length > 0) Table(["Combinazione", "Avviso"], missing);
            }
            Figures(family);
            var detailIds = envelope.Select(v => v.Id).ToHashSet();
            if (worstStress is { } stress) detailIds.Add(stress.Key); if (worstCrack is { } crack) detailIds.Add(crack.Key);
            if (options.Contains("dettagli")) foreach (var (id, outcome) in rows.Where(kv => all || detailIds.Contains(kv.Key)))
            {
                if (outcome?["State"] is not JsonObject state) continue;
                Subheading("Dettagli " + SectionWorkspace.Label(family) + " " + Name(family, id));
                if (crackRequired && outcome?["CrackResult"]?["Details"] is JsonArray crackDetails && crackDetails.Count > 0)
                {
                    Subheading("Fessurazione · coefficienti e passaggi");
                    P("Riepilogo essenziale: massimo 30 valori, fino a 6 cifre significative. NTC 2018 e Circolare 2019 § C4.1.2.2.4.5. Deformazioni adimensionali; traccia completa nel JSON.");
                    var compactCrack = outcome!["CrackResult"]!.Deserialize<Ntc2018Checks.CrackResult>()!;
                    Table(["Parametro", "Valore / unità", "Formula / origine"], CrackCalculationSummary.Values(compactCrack).Select(v => new[] {
                        v.Symbol,
                        CrackCalculationSummary.Number(v.Value) + " " + v.Unit,
                        v.Expression
                    }));
                }
                P("Deformazioni native Checker incrementali, senza εp iniziale dei trefoli. n trefoli è riferito al primo Ep presente; con moduli diversi il coefficiente φp resta comune.");
                Table(["Barra / trefolo", "σ [MPa]", "ε [‰]"], state.Array("tensioni_barre").Select((v, i) => new[] { "Armatura " + (i + 1), F(v), F(state.Array("BarStrains").ElementAtOrDefault(i)) }));
                Table(["Vertice CLS", "x [mm]", "y [mm]", "σ [MPa]", "ε [‰]"], state.Array("ConcreteVertices").Select(v => new[] { v.S("Id"), F(v?["X"]), F(v?["Y"]), F(v?["Stress"]), F(v?["Strain"]) }));
            }
        }
        if (options.Contains("taglio"))
        {
            Heading("Verifiche a taglio");
            var shear = settings["taglio"] as JsonObject ?? new();
            Parameters("Modello a taglio", shear, [("modello", "Modello"), ("bw_x", "bw,x [mm]"), ("d_x", "dx [mm]"), ("asl_x", "Asl,x [mm²]"), ("alpha_x", "αx [°]"), ("cot_x", "cot θx"), ("bw_y", "bw,y [mm]"), ("d_y", "dy [mm]"), ("asl_y", "Asl,y [mm²]"), ("alpha_y", "αy [°]"), ("cot_y", "cot θy")]);
            var rows = new List<string[]>();
            foreach (var action in shear.Array("azioni")) foreach (var (axis, index) in new[] { ("x", 0), ("y", 1) })
            {
                var check = result["taglio"]?[action.S("id")]?.AsArray().ElementAtOrDefault(index);
                rows.Add([action.S("nome") + " · " + axis, F(action?["N"]), F(action?["V" + axis]), F(check?["VRd"]), F(check?["Ratio"]), check.S("Status", "Non calcolata / modello non supportato o dati incompleti")]);
            }
            Table(["Combinazione", "N [kN]", "VEd [kN]", "VRd [kN]", "η [-]", "Esito"], rows);
            Figures("taglio");
        }
        void Figures(string category)
        {
          XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing", a = "http://schemas.openxmlformats.org/drawingml/2006/main", pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
          for (int i = 0; i < selected.Length; i++)
          {
            if (selected[i].Categoria != category) continue;
            P(selected[i].Titolo, "Caption"); long cx = 5800000, cy = 3625000;
            body.Add(new XElement(w + "p", new XElement(w + "r", new XElement(w + "drawing", new XElement(wp + "inline", new XElement(wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)), new XElement(wp + "docPr", new XAttribute("id", i + 1), new XAttribute("name", selected[i].Titolo)), new XElement(a + "graphic", new XElement(a + "graphicData", new XAttribute("uri", pic.NamespaceName), new XElement(pic + "pic", new XElement(pic + "nvPicPr", new XElement(pic + "cNvPr", new XAttribute("id", i + 1), new XAttribute("name", "image.png")), new XElement(pic + "cNvPicPr")), new XElement(pic + "blipFill", new XElement(a + "blip", new XAttribute(r + "embed", "img" + i)), new XElement(a + "stretch", new XElement(a + "fillRect"))), new XElement(pic + "spPr", new XElement(a + "xfrm", new XElement(a + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(a + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))), new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst")))))))))));
          }
        }
        body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", 11906), new XAttribute(w + "h", 16838)), new XElement(w + "pgMar", new XAttribute(w + "top", 1134), new XAttribute(w + "bottom", 1134), new XAttribute(w + "left", 1273), new XAttribute(w + "right", 1273))));
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string content) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(content); }
            Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Default Extension=\"png\" ContentType=\"image/png\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/></Types>");
            Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"doc\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            var relationships = new XElement(XName.Get("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships"));
            relationships.Add(new XElement(relationships.Name.Namespace + "Relationship", new XAttribute("Id", "styles"), new XAttribute("Type", r.NamespaceName + "/styles"), new XAttribute("Target", "styles.xml")));
            for (int i = 0; i < selected.Length; i++) { relationships.Add(new XElement(relationships.Name.Namespace + "Relationship", new XAttribute("Id", "img" + i), new XAttribute("Type", r.NamespaceName + "/image"), new XAttribute("Target", $"media/image{i}.png"))); using var stream = zip.CreateEntry($"word/media/image{i}.png").Open(); stream.Write(selected[i].Png); }
            var styles = new XElement(w + "styles");
            foreach (var (id, size, bold, before, after) in new[] { ("Normal", 21, false, 0, 100), ("Title", 36, true, 0, 100), ("Subtitle", 26, false, 0, 180), ("Heading1", 28, true, 250, 130), ("Heading2", 23, true, 180, 90), ("Caption", 21, false, 0, 100), ("TableText", 19, false, 0, 45), ("TableHeader", 19, true, 0, 45) })
                styles.Add(new XElement(w + "style", new XAttribute(w + "type", "paragraph"), new XAttribute(w + "styleId", id), new XElement(w + "name", new XAttribute(w + "val", id)), new XElement(w + "pPr", new XElement(w + "spacing", new XAttribute(w + "before", before), new XAttribute(w + "after", after)), new XElement(w + "widowControl"), id is "Heading1" or "Heading2" or "Title" or "Subtitle" or "Caption" or "TableHeader" ? new XElement(w + "keepNext") : null, id.StartsWith("Heading") ? new XElement(w + "outlineLvl", new XAttribute(w + "val", id == "Heading1" ? 0 : 1)) : null), new XElement(w + "rPr", new XElement(w + "rFonts", new XAttribute(w + "ascii", "Calibri"), new XAttribute(w + "hAnsi", "Calibri")), new XElement(w + "sz", new XAttribute(w + "val", size)), new XElement(w + "color", new XAttribute(w + "val", "000000")), bold ? new XElement(w + "b") : null)));
            Entry("word/styles.xml", styles.ToString()); Entry("word/_rels/document.xml.rels", relationships.ToString());
            Entry("word/document.xml", new XDocument(new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), new XAttribute(XNamespace.Xmlns + "r", r), body)).ToString());
        }
        Archivio.ScriviAtomico(path, memory.ToArray());
    }
}
