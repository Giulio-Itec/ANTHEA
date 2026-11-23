using System.Xml.Linq;
using X.Core;

namespace Anthea.Reports;

// The ITEC document writer supplies layout, native equations, tables, static contents and embedded figures.
internal sealed partial class ItecPileReport
{
    internal static DocxDocument CreateQuickSection(string title, QuickSectionReportData data, IReadOnlySet<string> options,
        IReadOnlyList<ImmagineReport> images)
    {
        var writer = new ItecPileReport();
        writer.BuildQuickSection(title, data, options, images);
        return writer.document;
    }

    void BuildQuickSection(string title, QuickSectionReportData data, IReadOnlySet<string> options, IReadOnlyList<ImmagineReport> images)
    {
        bool brief = options.Contains(QuickSectionReport.Brief), header = !brief && options.Contains("intestazione");
        if (!header) body.RemoveNodes();
        else body.Descendants(W + "br").Where(e => (string?)e.Attribute(W + "type") == "page").Remove();
        int cover = body.Elements().Count();
        if (brief) { QuickBrief(title, data, images); FinishQuickSection(title); return; }
        foreach (var chapter in QuickSectionReport.Outline(data.Cases.Select(c => c.State)).Where(c => c.Key is not ("intestazione" or "indice")))
        {
            if (!chapter.Descendants().Any(c => options.Contains(c.Key))) continue;
            Heading(chapter.Label);
            if (chapter.Key == "q3") body.Elements().Last().Element(W + "pPr")!.Add(new XElement(W + "pageBreakBefore"));
            foreach (var part in chapter.Children.Count > 0 ? chapter.Children : new[] { chapter })
            {
                if (!options.Contains(part.Key)) continue;
                if (chapter.Children.Count > 0) Subheading(part.Label);
                switch (part.Key)
                {
                    case "oggetto":
                        Paragraph(title);
                        Paragraph("Verifica della sezione in calcestruzzo armato soggetta a sforzo normale e flessione " + (data.Straight ? "retta." : "deviata.")
                            + " Si considerano gli stati limite e le combinazioni assegnati, con i modelli resistenti indicati nelle tabelle.");
                        Paragraph("Dimensioni in mm, aree in mm², azioni N in kN e momenti in kNm, tensioni in MPa, deformazioni in ‰. "
                            + "N è positivo a trazione; le tensioni di compressione sono negative. x e y sono gli assi della sezione illustrata. "
                            + (data.Straight ? "Nella flessione retta My è ignorato: il valore utilizzato nelle verifiche è zero." : "I momenti Mx e My conservano i segni delle combinazioni assegnate."));
                        break;
                    case "geometria":
                        QuickValues("Geometria", data.Geometry);
                        Paragraph(data.Geometry.Any(v=>v.Label=="Forma" && v.Value=="DXF")
                            ? "Le coordinate delle barre sono riferite al baricentro del calcestruzzo. Posizioni e diametri provengono dal disegno; staffe non importate."
                            : "Le coordinate delle barre sono riferite agli assi della sezione. Il copriferro è netto rispetto alla faccia esterna della staffa.");
                        Table("Armatura longitudinale", ["Barra", "x [mm]", "y [mm]", "Ø [mm]", "As [mm²]"], data.Reinforcement.ToList(), preservePrecision: true);
                        break;
                    case "materiali":
                        QuickValues("Proprietà e coefficienti adottati", data.Materials);
                        if (data.Cases.Any(c => c.State.StartsWith("SLE"))) Paragraph("Classe di esposizione: " + data.Exposure + ". Il valore n indicato è quello effettivamente utilizzato nel metodo n, con il coefficiente di viscosità φ assegnato.");
                        break;
                    case "teoria": QuickTheory(data); break;
                    case "azioni":
                        Table("Azioni e modelli per combinazione", ["Combinazione / SL", "N [kN]", "Mx [kNm]", "My [kNm]", "Modello / verso"],
                            data.Cases.Select(c => new[] { c.Name + " · " + QuickSectionReport.StateName(c.State), c.N, c.Mx, c.My, c.Model + (c.Sign == "—" ? "" : " · " + c.Sign) }).ToList(), [2.4, 1, 1, 1.3, 2], true);
                        break;
                    case "riepilogo":
                        Table("Esiti delle combinazioni", ["Combinazione", "Stato limite", "Rapporto", "Esito complessivo"],
                            data.Cases.Select(c => new[] { c.Name, QuickSectionReport.StateName(c.State), QNumber(c.Ratio), c.Status }).ToList(), [2, 2, 1, 3], true);
                        break;
                    case "avvisi":
                        foreach (var warning in data.Warnings.Count > 0 ? data.Warnings : ["Nessun avviso restituito dal calcolo."]) Paragraph(warning);
                        break;
                    default:
                        if (part.Key.StartsWith("risultati_"))
                            foreach (var item in data.Cases.Where(c => "risultati_" + c.State == part.Key)) QuickCase(item);
                        else
                        {
                            var figures = images.Where(i => i.Categoria == part.Key).ToArray();
                            foreach (var figure in figures) QuickFigure(figure, part.Key == "sezione" ? 3400000 : 4100000);
                            if (figures.Length == 0) Paragraph("Figura non disponibile per le combinazioni considerate.");
                        }
                        break;
                }
            }
        }
        if (data.Shear is { } shear && options.Overlaps(new[] { "riepilogo", "risultati_SLU", "risultati_SLV" }))
        {
            Heading("Verifica a taglio");
            QuickValues("Armatura resistente a taglio", shear.Parameters);
            Paragraph("Le barre longitudinali conservano la posizione assegnata dalla geometria. Le combinazioni a taglio sono indipendenti da quelle a flessione; «Copia combinazioni» ne riprende nomi e N. Ogni riga usa il modello armato o non armato indicato; la freccia segnala un modello diverso dalla tipologia assegnata. Torsione esclusa. Ancoraggi e dettagli costruttivi restano da verificare.");
            Table("Taglio per combinazione e direzione", ["Caso / verifica", "N [kN]", "Dir.", "VEd [kN]", "VRsd [kN]", "VRcd [kN]", "VRd [kN]", "η"],
                shear.Rows.Select(r => new[] { r.Name + " · " + r.CheckLabel, r.N, r.Axis, r.Action, r.SteelResistance, r.ConcreteResistance, r.Resistance, QNumber(r.Ratio) }).ToList(), [2, 1, .6, 1, 1, 1, 1, .8], true);
            foreach (var group in shear.Rows.GroupBy(r => r.Status))
                Paragraph(group.Key + " · " + string.Join("; ", group.Select(r => r.Name + " / " + r.CheckLabel + " / " + r.Axis)) + ".");
            if (shear.Rows.Count == 0) Paragraph("Nessuna combinazione assegnata per la verifica a taglio.");
        }
        FinishQuickSection(title, cover, options.Contains("indice"));
    }

    void FinishQuickSection(string title, int cover = 0, bool index = false)
    {
        // The cover is optional; the recurring ITEC furniture and typography are not.
        // Keep compact technical tables, but let prose and headings inherit the original styles.
        foreach (var p in body.Elements().Skip(cover).Where(e => e.Name == W + "p").ToArray())
        {
            if (p.Descendants(W + "drawing").Any() || p.Descendants(HM + "oMathPara").Any()) continue;
            if (!p.Descendants(W + "t").Any(t => t.Value.Length > 0)) { p.Remove(); continue; }
            var properties = p.Element(W + "pPr");
            string? style = (string?)properties?.Element(W + "pStyle")?.Attribute(W + "val");
            if (style == "Didascalia") continue;
            properties?.Elements(W + "spacing").Remove();
            properties?.Elements(W + "jc").Remove();
            p.Descendants(W + "rPr").Elements().Where(e => e.Name == W + "sz" || e.Name == W + "szCs" || e.Name == W + "rFonts").Remove();
        }
        FinishFamily(cover, header: true, index);
        // Replace template field caches: the old sample name and page count must never leak.
        foreach (var relation in relations.Elements().Where(e => ((string?)e.Attribute("Type"))?.EndsWith("/footer", StringComparison.Ordinal) == true))
        {
            string part = "word/" + (string)relation.Attribute("Target")!;
            var footer = document.GetXml(part);
            foreach (var instruction in footer.Descendants(W + "instrText").ToArray())
            {
                string field = instruction.Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                if (field is not ("FILENAME" or "PAGE" or "SECTIONPAGES")) continue;
                var instructionRun = instruction.Parent!;
                var start = instructionRun.ElementsBeforeSelf(W + "r").Last(e => e.Descendants(W + "fldChar").Any(f => (string?)f.Attribute(W + "fldCharType") == "begin"));
                var runs = start.ElementsAfterSelf().Prepend(start).TakeWhile(e => !e.Descendants(W + "fldChar").Any(f => (string?)f.Attribute(W + "fldCharType") == "end")).ToList();
                var end = runs.Last().NextNode as XElement;
                var value = new XElement(W + "r", instructionRun.Element(W + "rPr") is { } format ? new XElement(format) : null,
                    new XElement(W + "t", field == "FILENAME" ? title : "1"));
                start.AddBeforeSelf(field == "FILENAME" ? value : new XElement(W + "fldSimple", new XAttribute(W + "instr", field), value));
                foreach (var run in runs) run.Remove();
                end?.Remove();
            }
            document.SetXml(part, footer.ToString());
        }
        // Number each content section from one, as in the retained ITEC body section.
        var xml = document.GetXml("word/document.xml");
        foreach (var section in xml.Root!.Element(W + "body")!.Elements().Skip(cover).SelectMany(e => e.DescendantsAndSelf(W + "sectPr")))
        {
            section.Elements(W + "pgNumType").Remove();
            section.Element(W + "pgMar")!.AddAfterSelf(new XElement(W + "pgNumType", new XAttribute(W + "start", "1")));
        }
        document.SetXml("word/document.xml", xml.ToString());
    }

    static string QNumber(double? value) => EngineeringFormat.Number(value);
    static string QOutcome(QuickReportCheck check) => check.Passed is bool passed ? passed ? "Verificata" : "Non verificata" : "Non determinata";
    void QuickValues(string title, IReadOnlyList<QuickReportValue> values) => Table(title, ["Grandezza", "Valore", "Unità / nota"],
        values.Select(v => new[] { v.Label, v.Value, string.Join(" · ", new[] { v.Unit, v.Note }.Where(s => s.Length > 0)) }).ToList(), [2, 1.3, 3], true);

    void QuickCase(QuickReportCase item)
    {
        Paragraph(item.Name + " · " + item.Model);
        Table("Verifiche · " + item.Name, ["Verifica", "Risultato", "Rapporto", "Esito"],
            item.Checks.Select(c => new[] { c.Kind, c.Value.Replace("\n", "; "), QNumber(c.Ratio), QOutcome(c) }).ToList(), [1.4, 3.5, 1, 1.5], true);
        foreach (var check in item.Checks) Paragraph(check.Kind + ": " + check.Detail);
        if (item.Behaviour.Length > 0) Paragraph(item.Behaviour);
        QuickValues("Valori della verifica · " + item.Name, item.Values.Where(v => v.Label is not ("N" or "NEd" or "Mx" or "Mx,Ed" or "My" or "My,Ed") && !v.Label.StartsWith("n (")).ToArray());
    }

    void QuickTheory(QuickSectionReportData data)
    {
        if (data.Cases.Any(c => c.State is "SLU" or "SLV"))
        {
            Paragraph("La resistenza è valutata sul dominio della sezione, con equilibrio e compatibilità delle deformazioni e con il modello plastico o elastico assegnato a ciascuna combinazione. Il rapporto di utilizzo η è ottenuto a sforzo normale costante; il punto resistente è riferito alla direzione dell’azione.");
            QuickEquation("eta", "η ≤ 1");
            Paragraph("Nella flessione retta si riporta il momento resistente del verso selezionato. Nella flessione deviata si riportano le componenti del punto resistente. Il comportamento è classificato sulla deformazione dell’armatura tesa al punto resistente:");
            QuickEquation("ductility", QSub("ε", "s"), " ≥ ", QSub("ε", "yd"), "  :  comportamento duttile");
            Paragraph("Se εs è inferiore a εyd il comportamento è fragile. In assenza delle deformazioni l’esito non è determinato.");
        }
        if (data.Cases.Any(c => c.State.StartsWith("SLE")))
        {
            Paragraph("Le verifiche di esercizio utilizzano il metodo n, con il coefficiente di omogeneizzazione e la viscosità adottati dal calcolo. Le tensioni sono confrontate con i limiti dello stato limite considerato.");
            QuickEquation("n", QSub("n", "0"), " = ", HFrac(QSub("E", "s"), QSub("E", "cm")));
            if (data.Cases.Any(c => c.State is "SLE" or "SLE_QP"))
            {
                Paragraph("Per il calcestruzzo si confronta il valore assoluto della tensione di compressione con il limite adottato.");
                QuickEquation("stress", "|", QSub("σ", "c,comp"), "| ≤ ", QSub("σ", "c,lim"), "     ;     ", QSub("σ", "s"), " ≤ ", QSub("σ", "s,lim"));
            }
            if (data.Cases.Any(c => c.State is "SLE_FREQ" or "SLE_QP"))
            {
                Paragraph("L’apertura delle fessure è confrontata con il limite associato alla classe di esposizione e alla combinazione. Per la quasi permanente devono essere soddisfatte entrambe le verifiche, tensioni e fessurazione.");
                QuickEquation("crack", HFrac(QSub("w", "k"), QSub("w", "lim")), " ≤ 1");
            }
        }
    }

    void QuickBrief(string title, QuickSectionReportData data, IReadOnlyList<ImmagineReport> images)
    {
        body.Add(P("SEZIONE IN CALCESTRUZZO ARMATO", "Titolo1"));
        BriefText(title + " · relazione breve", 11, 100);
        BriefText("Flessione " + (data.Straight ? "retta; My ignorato." : "deviata.") + " N e tensioni negativi a compressione. Unità: mm, kN, kNm, MPa.", 10);
        BriefHeading("1  Geometria e armature");
        string Geometry(string label) => data.Geometry.FirstOrDefault(v => v.Label == label)?.Value ?? "—";
        SmallTable(["Sezione", "b [mm]", "h [mm]", "c [mm]", "As [mm²]"],
            [[data.Shape, Geometry("Larghezza"), Geometry("Altezza"), Geometry("Copriferro netto della staffa"), Geometry("Area armatura")]], [2, 1, 1, 1, 1.5]);
        var section = images.FirstOrDefault(i => i.Categoria == "sezione");
        if (section is not null) QuickFigure(section, 2000000, brief: true);
        else BriefText("Sezione quotata non disponibile.", 10);
        string links = Geometry("Forma") == "DXF" ? "; staffe non importate dal DXF" : "; staffe Ø " + Geometry("Diametro staffa") + " mm";
        BriefText("Armatura: " + string.Join("; ", data.Reinforcement.GroupBy(r => r[3]).Select(g => g.Count() + " Ø " + g.Key + " mm")) + links + ". Posizioni nella sezione quotata e nel report completo.", 10);
        BriefHeading("2  Materiali e criteri");
        string Material(string label) => data.Materials.FirstOrDefault(v => v.Label == label)?.Value ?? "—";
        SmallTable(["Calcestruzzo", "fck [MPa]", "fcd [MPa]", "Ecm [MPa]", "γc", "αcc"],
            [[Material("Calcestruzzo"), Material("fck"), Material("fcd"), Material("Ecm"), Material("γc"), Material("αcc")]], [1.6, 1.2, 1.2, 1.5, .8, .8]);
        SmallTable(["Acciaio", "fyk [MPa]", "fyd [MPa]", "Es [MPa]", "γs", "εyd [‰]"],
            [[Material("Acciaio"), Material("fyk"), Material("fyd"), Material("Es"), Material("γs"), Material("εyd")]], [1.6, 1.2, 1.2, 1.5, .8, .8]);
        BriefText(Material("Normativa") + ". Esposizione: " + data.Exposure + ". n₀ = " + Material("n₀") + "; n = " + Material("n") + ", " + (data.Materials.FirstOrDefault(v => v.Label == "n")?.Note ?? ""), 10);
        if (data.Shear is { } shearParameters)
        {
            string ShearParameter(string label) => shearParameters.Parameters.FirstOrDefault(p => p.Label == label)?.Value ?? "—";
            BriefText("Taglio: " + ShearParameter("Modello") + "; " + (shearParameters.UsesGeometryStirrup ? "staffa geometrica effettiva" : "armatura a taglio indipendente (geometrica esclusa)")
                + ". Ø " + ShearParameter("Ø resistente") + " mm, passo " + ShearParameter("Passo resistente") + " mm; rami Vx/Vy " + ShearParameter("Rami Vx / Vy")
                + "; α " + ShearParameter("α Vx / Vy") + "°; cot θ " + ShearParameter("cot θ Vx / Vy") + ".", 10, 0);
        }
        BriefHeading("3  Casi governanti per verifica", page: true);
        BriefText("Il caso con rapporto massimo è individuato separatamente per stato limite e verifica. Gli esiti non determinati restano riportati. La quasi permanente richiede tensioni e fessurazione entrambe verificate.", 10);
        var governing = QuickSectionReport.Governing(data);
        if (data.Shear is null)
            SmallTable(["Stato / verifica", "Combinazione", "Risultato", "Rapporto / esito"], governing.Select(p => new[] {
                QuickSectionReport.StateName(p.Case.State) + "\n" + p.Check.Kind, p.Case.Name, p.Check.Value + (p.Check.Limits.Length > 0 ? "\nLimiti: " + p.Check.Limits : ""), QNumber(p.Check.Ratio) + "\n" + QOutcome(p.Check) }).ToList(), [1.7, 1.5, 3, 1.6]);
        else
            SmallTable(["SL / verifica", "Caso / modello", "N / Mx / My", "Risultato", "η / esito"], governing.Select(p => new[] {
                p.Case.State + "\n" + p.Check.Kind, p.Case.Name + "\n" + p.Case.Model, p.Case.N + "\n" + p.Case.Mx + "\n" + p.Case.My,
                p.Check.Value + (p.Check.Limits.Length > 0 ? "\nLimiti: " + p.Check.Limits : ""), QNumber(p.Check.Ratio) + "\n" + QOutcome(p.Check) }).ToList(), [1.3, 1.7, 1.2, 2.7, 1.3]);
        if (data.Shear is null)
        {
        BriefHeading("4  Azioni dei casi governanti");
        SmallTable(["Combinazione / SL", "N [kN]", "Mx [kNm]", "My [kNm]", "Modello"], governing.Select(p => p.Case).DistinctBy(c => c.Id)
            .Select(c => new[] { c.Name + "\n" + QuickSectionReport.StateName(c.State), c.N, c.Mx, c.My, c.Model }).ToList(), [2.5, 1, 1, 1.3, 1.8]);
        }
        else
        {
            BriefHeading("4  Taglio · casi governanti per verifica e direzione");
            var shearRows = data.Shear.Rows.GroupBy(r => (r.Condition, r.Model, r.Axis)).SelectMany(g => g.Where(r => r.Ratio is null).Concat(g.Where(r => r.Ratio is not null).OrderByDescending(r => r.Ratio).Take(1))).ToArray();
            SmallTable(["Caso / verifica", "N [kN]", "Dir.", "VEd [kN]", "VRd [kN]", "η / esito"], shearRows.Select(r => new[] {
                r.Name + "\n" + r.CheckLabel, r.N, r.Axis, r.Action, r.Resistance, QNumber(r.Ratio) + "\n" + (r.Passed is bool passed ? passed ? "Verificata" : "Non verificata" : "Non determinata") }).ToList(), [2, 1, .7, 1, 1, 1.8]);
            foreach (var row in shearRows.Where(r => r.Passed is null)) BriefText(row.Name + " " + row.Axis + ": " + row.Status, 10);
            if (shearRows.Length == 0) BriefText("Nessuna combinazione a taglio assegnata.", 10);
        }
        foreach (var item in governing.Where(p => p.Check.Kind == "Resistenza")) BriefText(item.Case.Name + ": " + item.Case.Behaviour, 10);
        var domain = images.FirstOrDefault(i => i.Categoria == "dominio");
        if (domain is not null && governing.Count <= 3 && data.Shear is null) QuickFigure(domain, 1900000, brief: true);
        BriefText("Criteri: η ≤ 1; tensioni entro i limiti; wk ≤ wlim. Valori di dettaglio, ipotesi e combinazioni complete nella relazione estesa."
            + (data.Shear is null ? "" : " Taglio con o senza armatura trasversale; torsione, ancoraggi e dettagli costruttivi esclusi."), 10);
    }

    static XElement QSub(string value, string subscript) => new(HM + "sSub", new XElement(HM + "e", HRun(value)), new XElement(HM + "sub", HRun(subscript)));

    /// <summary>The points of an inch and the resolution of the PNG of the formulas for the PDF: the size of a formula in points.</summary>
    const double QuickPointsPerInch = 72, QuickEquationDpi = 300;

    void QuickEquation(string name, params object[] parts)
    {
        HEq(parts);
        using var stream = typeof(ItecPileReport).Assembly.GetManifestResourceStream("Anthea.QuickSection." + name + ".png")
            ?? throw new InvalidDataException("Rappresentazione PDF della formula assente: " + name);
        using var memory = new MemoryStream(); stream.CopyTo(memory); byte[] png = memory.ToArray();
        double width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)) * QuickPointsPerInch / QuickEquationDpi;
        double height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)) * QuickPointsPerInch / QuickEquationDpi;
        string id = "quick-" + name;
        document.SetBytes("anthea/equation-" + id + ".png", png);
        var equations = document.GetXml("anthea/equations.xml");
        equations.Root!.Add(new XElement("equation", new XAttribute("id", id), new XAttribute("widthPt", width), new XAttribute("heightPt", height), new XElement(body.Elements().Last())));
        document.SetXml("anthea/equations.xml", equations.ToString());
    }

    void QuickFigure(ImmagineReport figure, long maxHeight, bool brief = false)
    {
        Image(figure);
        if (brief) body.Elements().Last().ReplaceWith(BriefParagraph("Figura " + figureNumber + "  " + figure.Titolo, 10, 100, "Didascalia", "center"));
        var picture = body.Elements().Last().PreviousNode as XElement;
        picture!.Element(W + "pPr")!.Elements(W + "jc").Remove();
        picture.Element(W + "pPr")!.Add(new XElement(W + "jc", new XAttribute(W + "val", "center")));
        XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing", a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        var extent = picture!.Descendants(wp + "extent").Single();
        long height = (long)extent.Attribute("cy")!, width = (long)extent.Attribute("cx")!;
        if (height <= maxHeight) return;
        width = width * maxHeight / height;
        extent.SetAttributeValue("cx", width); extent.SetAttributeValue("cy", maxHeight);
        foreach (var size in picture.Descendants(a + "xfrm").Elements(a + "ext")) { size.SetAttributeValue("cx", width); size.SetAttributeValue("cy", maxHeight); }
    }
}
