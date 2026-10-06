using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace X.Core;

/// <summary>Serializes the existing wall result; never recalculates or replaces missing checks.</summary>
public static partial class ReportRetainingWall
{
    private static string F(double d) => d.ToString("0.###", CultureInfo.GetCultureInfo("it-IT"));
    private static string[] Cells(RetainingWall.Check c)
    {
        string R(double d) => d.ToString("G17", CultureInfo.GetCultureInfo("it-IT"));
        return [c.Name, c.Combination, R(c.Demand), c.Resistance is double r ? R(r) : "—", c.Unit, c.Ratio is double q ? R(q) : "—", c.Status];
    }
    public static IEnumerable<RetainingWall.Check> Envelope(IEnumerable<RetainingWall.Check> checks) => checks.GroupBy(c => System.Text.RegularExpressions.Regex.Replace(c.Name, @" z=\d+\.\d+ m", "")).Select(g => g.OrderByDescending(c => c.Ratio ?? double.PositiveInfinity).First());
    public static string Csv(RetainingWall.Result r)
    {
        string Row(IEnumerable<string> cells) => string.Join(";", cells.Select(c => "\"" + c.Replace("\"", "\"\"") + "\""));
        return string.Join("\r\n", new[] { Row(["Verifica", "Combinazione", "Ed", "Rd", "Unità", "Ed/Rd", "Esito"]) }
            .Concat(r.Checks.Concat(r.Structural).Select(c => Row(Cells(c))))) + "\r\n";
    }
    public sealed record Figure(string Caption, byte[] Png, double AspectRatio);
    public static byte[] Create(string title, RetainingWall.Result result, IReadOnlyList<Figure>? figures = null)
    {
        var doc = new WallDocument(); var d = result.Input;
        doc.P("ANTHEA · " + title, true); doc.P("Muri di sostegno · " + RetainingWall.Families.Single(f => f.Id == d.S("family")).Name);
        doc.P("Campo di applicazione e verifiche richieste", true);
        foreach (var note in result.Notes) doc.P(note);
        if (figures is not null) foreach (var figure in figures) doc.Image(figure);
        doc.P("Geometria e dati di ingresso", true);
        foreach (var (group, fields) in new[] { ("geometry", RetainingWall.GeometryFields), ("foundation", RetainingWall.FoundationFields), ("loads", RetainingWall.LoadFields), ("materials", RetainingWall.MaterialFields) }.Where(x => x.Item1 != "loads" || d.D("version") == 1))
            doc.Table(["Parametro", "Valore", "Unità"], fields.Where(f => group != "materials" || f.Key == "gamma" || (d.S("family") == "gravity" ? f.Key.EndsWith("_rd") : !f.Key.EndsWith("_rd"))).Select(f => new[] { f.Label + " · " + f.Symbol, d[group].S(f.Key), f.Unit }), [5, 1, 1]);
        if (RetainingWall.UsesConcrete(d))
        {
            var material = RetainingWall.MaterialSectionInput(d);
            doc.P("Materiali del verificatore delle sezioni in calcestruzzo", true);
            doc.P("Cataloghi e legami GPC condivisi con il verificatore c.a. Le proprietà qui riportate sono quelle effettivamente usate dal muro e conservate nell’invio della sezione.");
            var keys = new[] { ("materiale_cls_nome", "Calcestruzzo"), ("cls_diagramma", "Diagramma CLS"), ("alpha_cc", "αcc"), ("gamma_c", "γc"),
                ("materiale_acciaio_nome", "Acciaio per armature"), ("steel_modulus_mpa", "Es [MPa]"), ("steel_fu_mpa", "fu [MPa]"), ("steel_eps_u", "εu [‰]"), ("steel_diagramma", "Diagramma acciaio"), ("gamma_s", "γs") };
            doc.Table(["Proprietà", "Valore"], keys.Take(d.S("family") == "cantilever" ? keys.Length : 4).Select(p => new[] { p.Item2, material.S(p.Item1) }), [4, 3]);
            if (d.S("family") == "cantilever")
            {
                doc.P("Durabilità e copriferro", true);
                var state = RetainingWall.CoverMaterialState(d);
                doc.Table(["Parametro", "Valore"], new[] { new[] { "Esposizione", d["materials"].S("exposure") }, new[] { "Copriferro adottato [mm]", d["materials"].S("cover") }, new[] { "Aggregato [mm]", d["detailing"].S("aggregate") }, new[] { "Vita utile [anni]", d["detailing"].S("life") }, new[] { "Tolleranza [mm]", d["detailing"].S("cover_deviation") } }
                    .Concat(state["scelte"]!.AsObject().Select(p => new[] { p.Key, p.Value?.ToString() ?? "" }))
                    .Concat(state["opzioni"]!.AsObject().Select(p => new[] { p.Key, p.Value?.ToString() ?? "" })), [4, 3]);
            }
        }
        if (d.D("version") >= 2)
        {
            doc.P("Azioni assegnate per metro di sviluppo", true);
            doc.Table(["Nome / tipo / natura", "Valore / unità", "z₀ / z₁ / x [m]", "ψ₀ / ψ₁ / ψ₂ / gruppo"], d.Array("actions").Select(a => new[] { a.S("name") + "\n" + a.S("type") + " · " + a.S("category") + (a.B("enabled") ? "" : " · esclusa"), a.S("value") + " " + RetainingWall.ActionUnit(a.S("type")), a.S("type") == "Sovraccarico uniforme" ? "Tutto il riempimento" : a.S("z0") + " / " + a.S("z") + " / " + a.S("x"), a.S("psi0") + " / " + a.S("psi1") + " / " + a.S("psi2") + " / " + a.S("group") }), [3, 1.7, 1.5, 2]);
            doc.P("z delle azioni dal piano di posa, x dal bordo a valle. H verso valle, N a compressione, momento ribaltante. Urto: azione statica equivalente assegnata. Visibilità nel disegno indipendente dall’abilitazione al calcolo.");
        }
        doc.P("Stratigrafia di monte (dalla sommità)", true);
        doc.Table(["Nome", "Δz [m]", "γ [kN/m³]", "γsat [kN/m³]", "φ′ [°]"], d.Array("layers").Select(l => new[] { l.S("name"), l.S("thickness"), l.S("gamma"), l.S("gamma_sat"), l.S("phi") }), [3, 1, 1, 1, 1]);
        doc.P("Stratigrafia di valle (dalla superficie di valle)", true);
        doc.Table(["Nome", "Δz [m]", "γ [kN/m³]", "γsat [kN/m³]", "φ′ [°]"], RetainingWall.ValleyLayers(d).Select(l => new[] { l.S("name"), l.S("thickness"), l.S("gamma"), l.S("gamma_sat"), l.S("phi") }), [3, 1, 1, 1, 1]);
        doc.P($"Dv={F(RetainingWall.ValleyHeight(d))} m; Hlib={F(d["geometry"].D("height") + d["geometry"].D("slab") - RetainingWall.ValleyHeight(d))} m. Colonne collegate: {d["valley"].B("linked")}. Passiva richiesta: {d["valley"].B("passive")}; frazione={d["valley"].S("mobilization")}.");
        doc.P("Origine degli attriti", true);
        doc.Table(["Parametro", "Input"], d["interfaces"]!.AsObject().Select(p => new[] { p.Key, p.Value?.ToString() ?? "" }), [3, 4]);
        doc.P("Valori geotecnici effettivamente utilizzati per combinazione", true);
        doc.Table(["Caso", "Quote e attriti", "Terreno valle", "Portanza"], result.Cases.Select(c => new[] { c.Name,
            string.Join("\n", new[] { "Dv_m", "Hlib_m", "delta_muro_d_gradi", "delta_base_d_gradi", "mu_base", "delta_piano_equilibrio_gradi" }.Select(k => RetainingWall.AuditMeaning(k).Label + " = " + F(c.SoilAudit.D(k)) + " " + RetainingWall.AuditMeaning(k).Unit)),
            string.Join("\n", new[] { "peso_valle_kN_m", "momento_peso_valle_kNm_m", "q_ricoprimento_kPa", "passiva_disponibile_kN_m", "passiva_usata_kN_m", "passiva_frazione", "passiva_limite_equilibrio" }.Select(k => RetainingWall.AuditMeaning(k).Label + " = " + F(c.SoilAudit.D(k)) + " " + RetainingWall.AuditMeaning(k).Unit)),
            string.Join("\n", new[] { "Nq", "Ngamma", "iq", "igamma" }.Select(k => k + " = " + F(c.SoilAudit.D(k)))) + "\nBase ruvida: " + c.SoilAudit.B("base_ruvida") }), [1.4, 2.3, 2.7, 1.4]);
        doc.P("Coefficienti delle spinte sul fusto", true);
        doc.Table(["Caso / z₀–z₁ [m]", "φd [°]", "K / Ke", "Incremento [kPa]"], result.Cases.SelectMany(c => c.StemPressureDetails.Select(p => new[] { c.Name + " / " + F(p.Z0) + "–" + F(p.Z1), F(p.PhiDesign), F(p.K) + " / " + F(p.Ke), F(p.Dynamic) })), [3, 1, 2, 1]);
        doc.P("Pressioni sul fusto e resistenza di valle", true);
        doc.Table(["Caso / piano", "z₀–z₁ [m]", "p₀–p₁ [kPa]"], result.Cases.SelectMany(c => c.StemPressures.Select(p => new[] { c.Name + " / fusto", F(p.Z0) + "–" + F(p.Z1), F(p.P0) + "–" + F(p.P1) }).Concat(c.ValleyPressures.Select(p => new[] { c.Name + " / valle", F(p.Z0) + "–" + F(p.Z1), F(p.P0) + "–" + F(p.P1) }))), [3, 2, 2]);
        doc.P(d["water"].B("enabled") ? "Falda: profondità " + d["water"].S("depth") + " m; battente a valle " + d["water"].S("front_head") + " m. Sottospinta lineare integrale." : "Falda assente.");
        doc.P(d["seismic"].B("enabled") ? "Pseudostatica: kh=" + d["seismic"].D("kh").ToString("0.#####", CultureInfo.GetCultureInfo("it-IT")) + "; |kv|=" + d["seismic"].D("kv").ToString("0.#####", CultureInfo.GetCultureInfo("it-IT")) + ". Vedere i limiti di applicazione sopra riportati." : "Analisi sismica non richiesta nel presente calcolo.");
        if (d["seismic"].B("enabled") && d["seismic"].S("source") == RetainingWall.SeismicSite)
        {
            var s = d["seismic"]!; var parameters = new List<(string Key, string Label)> { ("method", "Modello delle spinte"), ("ag_g", "ag/g allo SLV"), ("ss_mode", "Definizione Ss"), ("st_mode", "Definizione St") };
            if (s.S("ss_mode") == RetainingWall.AmplificationCalculated) { parameters.Add(("soil_class", "Categoria sottosuolo")); if (s.S("soil_class") != "A") parameters.Add(("f0", "F₀ allo SLV")); } else parameters.Add(("ss", "Ss assegnato"));
            if (s.S("st_mode") == RetainingWall.AmplificationCalculated)
            {
                parameters.Add(("topography", "Forma del terreno"));
                if (s.S("topography") != RetainingWall.TopographyFlat) { parameters.Add(("slope", "Inclinazione media [°]")); if (s.D("slope") > 15) { parameters.Add(("relief_height", "Altezza rilievo [m]")); parameters.Add(("site_height", "Quota dalla base del rilievo [m]")); } }
            }
            else parameters.Add(("st", "St assegnato"));
            doc.P("Dati del sito per il calcolo dei coefficienti sismici", true);
            doc.Table(["Dato", "Valore"], parameters.Select(p => new[] { p.Label, s.S(p.Key) }), [4, 3]);
            doc.P(RetainingWall.DeriveSeismic(d)!.Description);
        }
        if (d.S("family") == "cantilever")
        {
            doc.P("Armature e verifiche strutturali", true);
            doc.P("Due facce simmetriche oppure indipendenti; quantità per metro. Resistenza N–M da GPCChecker.Concrete, taglio senza staffe secondo NTC 2018 senza beneficio della compressione. Sezioni del fusto ogni H/20, compresa la testa quando caricata, e radici delle mensole; l’inviluppo indica la sezione governante. SLE: sezione fessurata, viscosità assegnata, esposizione " + d["materials"].S("exposure") + ". Compressione positiva nei risultati del muro e convertita in N negativa per Checker.");
            doc.Table(["Elemento", "Barre per metro/faccia", "Ø [mm]"], new[] { ("stem", "Fusto inferiore / intero"), ("stem_upper", "Fusto superiore"), ("toe", "Valle"), ("heel", "Monte") }.Where(x => x.Item1 != "stem_upper" || d["reinforcement"].B("two_zones")).Select(x => new[] { x.Item2, d["reinforcement"]![x.Item1].S("count"), d["reinforcement"]![x.Item1].S("diameter") }), [3, 2, 1]);
            if (d["reinforcement"].B("two_zones")) doc.P("Armatura inferiore fino a h₁=" + d["reinforcement"].S("lower_height") + " m dal piede del fusto; armatura superiore fino alla sommità. Verifiche su entrambi i lati del cambio di armatura.");
            doc.P("Quantità indicative: " + F(result.Volume) + " m³/m di calcestruzzo e " + F(result.SteelKg) + " kg/m (con dettagli se attivati). La distinta aggiornata delle barre e i controlli di ancoraggio, sovrapposizione e armatura secondaria sono riportati nel capitolo dedicato.");
        }
        else if (d["gravity_design"].S("type") == "Resistenze assegnate") doc.P("Gravità: compressione e taglio confrontati con resistenze di progetto assegnate, da documentare per il materiale effettivo. Fusto verificato anche per assenza di trazione. Fondazione non armata verificata a flessione con fctd assegnata. Non è una verifica normativa completa di muratura o pietrame.");
        doc.P("Inviluppo verifiche geotecniche", true); Checks(Envelope(result.Checks));
        doc.P("Inviluppo verifiche strutturali", true); Checks(Envelope(result.Structural));
        void Checks(IEnumerable<RetainingWall.Check> checks) => doc.Table(["Controllo / caso", "Ed / Rd", "Unità", "Ed/Rd", "Esito"], checks.Select(c => new[] { c.Name + "\n" + c.Combination, F(c.Demand) + " / " + (c.Resistance is double rr ? F(rr) : "—"), c.Unit, c.Ratio is double q ? F(q) : "—", c.Status }), [3.3, 1.5, .8, .7, 2]);
        doc.P("Combinazioni e equilibrio", true);
        if (d.D("version") >= 2)
        {
            doc.Table(["Caso / stato", "γ muro / monte / valle / acqua", "γMφ / γRsc / γRrib / γRport", "kh / kv"], result.Cases.Select(c => new[] { c.Name + "\n" + c.State + " · " + c.Factors.S("approach"), $"{F(c.WallFactor)} / {F(c.SoilFactor)} / {F(c.Factors.D("valley_soil", c.SoilFactor))} / {F(c.WaterFactor)}", $"{c.Factors.S("mphi")} / {c.Factors.S("rslide")} / {c.Factors.S("rover")} / {c.Factors.S("rbearing")}", F(c.Kh) + " / " + F(c.Kv) }), [3, 1.6, 2, 1]);
            doc.Table(["Combinazione", "Fattori γ × ψ delle azioni"], result.Cases.Select(c => new[] { c.Name, string.Join("; ", c.Actions.Select(a => a.Name + ": " + F(a.Factor))) }), [2, 5]);
            doc.P("Spinte: valori e coefficienti", true);
            doc.P(d["seismic"].S("method") == "Wood semplificato" && d["seismic"].B("enabled") ? RetainingWall.SeismicHelp : "Piano virtuale con mensola: Ka=tan²(45°−φd/2). Senza mensola: Coulomb con δd; K e Ke tabulati sono le componenti orizzontali. Fusto: Coulomb/MO con δd; la tabella seguente riguarda il piano esterno. tanφd=tanφk/γMφ. Statica: p=Kaσ′v+KaΣ(fi·qi)+u. Sisma MO: θ=atan[kh/(1−kv)], Kae=cos²(φd−θ)/{cos²θ·[1+√(sinφd·sin(φd−θ)/cosθ)]²}; Δp=(Kae−Ka)(1−kv)γHt/2; pq=Kae(1−kv)Σ(fi·qi). Incremento uniforme; Ht=H+t.");
            doc.Table(["Caso / z₀–z₁ [m]", "φd / K / Kae", "p terra₀–₁", "pq / u₀–₁", "Δp / ptot₀–₁"], result.Cases.SelectMany(c => c.PressureDetails.Select(p => new[] { c.Name + "\n" + F(p.Z0) + "–" + F(p.Z1), F(p.PhiDesign) + " / " + F(p.K) + " / " + F(p.Ke), F(p.Soil0) + "–" + F(p.Soil1), F(p.Surcharge) + " / " + F(p.Water0) + "–" + F(p.Water1), F(p.Dynamic) + " / " + F(p.Total0) + "–" + F(p.Total1) })), [2.2, 1.6, 1.3, 1.5, 1.7]);
            doc.P("Pressioni in kPa; φd in gradi; z dalla sommità. σ′v include i fattori dei pesi e (1−kv). Le azioni laterali dirette sono integrate separatamente. Matrice completa e dettagli numerici disponibili anche nel JSON.");
        }
        doc.Table(["Caso", "γ muro/terra/acqua", "H [kN/m]", "V′ [kN/m]", "U [kN/m]"], result.Cases.Select(c => new[] { c.Name, $"{F(c.WallFactor)} / {F(c.SoilFactor)} / {F(c.Factors.D("valley_soil", c.SoilFactor))} / {F(c.WaterFactor)}", F(c.Horizontal), F(c.Vertical), F(c.Uplift) }), [2, 2, 1, 1, 1]);
        doc.Table(["Caso", "Mstab [kNm/m]", "Mrib [kNm/m]", "e [m]", "B′ [m]", "pmax [kPa]"], result.Cases.Select(c => new[] { c.Name, F(c.Stabilizing), F(c.Overturning), F(c.Eccentricity), F(c.EffectiveWidth), c.Contact.Valid ? F(c.Contact.Peak) : "Non disponibile" }), [2, 1.3, 1.3, .8, .8, 1.2]);
        doc.P("Convenzioni: x dal bordo a valle, e positivo verso valle, pressioni positive a compressione. N positivo a compressione. M del fusto positivo per trazione a monte; M delle mensole positivo per trazione inferiore. Sottospinta inclusa nel momento ribaltante. B′ è la larghezza efficace per la portanza, distinta dalla larghezza di contatto.");
        doc.P("Sollecitazioni alle radici", true);
        doc.Table(["Caso", "Elemento", "N [kN/m]", "M [kNm/m]", "V [kN/m]"], result.Cases.SelectMany(c => c.Sections.GroupBy(s => s.Name).Select(group => group.MaxBy(s => s.Position)!).Select(s => new[] { c.Name, s.Name, F(s.N), F(s.M), F(s.V) })), [2, 1, 1, 1, 1]);
        doc.P("Tutte le sezioni campionate sono disponibili nell’export JSON; il CSV contiene tutti i controlli. Un controllo non disponibile è mantenuto nell’inviluppo e non sostituito da un esito favorevole.");
        WriteAdvanced(doc, result);
        WriteGlobal(doc, result);
        doc.P("Riferimenti", true);
        doc.P("D.M. 17/01/2018, NTC 2018 §§4.1, 6.2.4, 6.5 e 7.11: https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg");
        doc.P("JRC 2013, Eurocode 7: Geotechnical Design – Worked examples, capitoli 3 e 4: https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/2013_06_WS_GEO.pdf");
        doc.P("Wood, 1973, Earthquake-induced soil pressures on structures: https://authors.library.caltech.edu/records/48499-83239; Yi, 2013, Seismic Design of Restrained Rigid Walls: https://www.cfms-sols.org/sites/default/files/Actes/3521-3524.pdf. Distribuzione uniforme dell’incremento esplicitamente assunta nel presente modello semplificato.");
        return doc.Bytes();
    }
    private sealed class WallDocument
    {
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private readonly XElement body = new(W + "body");
        private readonly List<byte[]> images = [];
        private static XElement Paragraph(string text, bool heading = false, bool keep = false) => new(W + "p", new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", heading ? 130 : 70)), heading || keep ? new XElement(W + "keepNext") : null),
            new XElement(W + "r", new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", "Calibri"), new XAttribute(W + "hAnsi", "Calibri")), new XElement(W + "sz", new XAttribute(W + "val", heading ? 25 : 19)), heading ? new XElement(W + "b") : null),
                text.Split('\n').SelectMany((line, i) => i == 0 ? new[] { new XElement(W + "t", line) } : new[] { new XElement(W + "br"), new XElement(W + "t", line) })));
        internal void P(string text, bool heading = false) => body.Add(Paragraph(text, heading));
        internal void PageBreak() => body.Add(new XElement(W + "p", new XElement(W + "r", new XElement(W + "br", new XAttribute(W + "type", "page")))));
        internal void Image(Figure figure)
        {
            images.Add(figure.Png); int id = images.Count; long cx = 5943600, cy = (long)(cx / figure.AspectRatio);
            XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing", a = "http://schemas.openxmlformats.org/drawingml/2006/main", pic = "http://schemas.openxmlformats.org/drawingml/2006/picture", r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            P(figure.Caption, true);
            var properties = new XElement(pic + "nvPicPr", new XElement(pic + "cNvPr", new XAttribute("id", id), new XAttribute("name", figure.Caption)), new XElement(pic + "cNvPicPr"));
            var fill = new XElement(pic + "blipFill", new XElement(a + "blip", new XAttribute(r + "embed", "image" + id)), new XElement(a + "stretch", new XElement(a + "fillRect")));
            var shape = new XElement(pic + "spPr", new XElement(a + "xfrm", new XElement(a + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(a + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))), new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst")));
            var graphic = new XElement(a + "graphic", new XElement(a + "graphicData", new XAttribute("uri", pic.NamespaceName), new XElement(pic + "pic", properties, fill, shape)));
            var inline = new XElement(wp + "inline", new XElement(wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)), new XElement(wp + "docPr", new XAttribute("id", id), new XAttribute("name", "Muro " + id)), graphic);
            body.Add(new XElement(W + "p", new XElement(W + "r", new XElement(W + "drawing", inline))));
        }
        internal void Table(string[] headers, IEnumerable<string[]> rows, double[] weights)
        {
            int[] widths = weights.Select(v => (int)(9360 * v / weights.Sum())).ToArray(); widths[^1] += 9360 - widths.Sum();
            var table = new XElement(W + "tbl", new XElement(W + "tblPr", new XElement(W + "tblW", new XAttribute(W + "w", 9360), new XAttribute(W + "type", "dxa")), new XElement(W + "tblLayout", new XAttribute(W + "type", "fixed")), new XElement(W + "tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(s => new XElement(W + s, new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 4), new XAttribute(W + "color", "CBD5E1"))))), new XElement(W + "tblGrid", widths.Select(w => new XElement(W + "gridCol", new XAttribute(W + "w", w)))));
            foreach (var (row, idx) in new[] { headers }.Concat(rows).Select((r, i) => (r, i)))
                table.Add(new XElement(W + "tr", new XElement(W + "trPr", new XElement(W + "cantSplit"), idx == 0 ? new XElement(W + "tblHeader") : null), row.Select((cell, i) => new XElement(W + "tc", new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", widths[i]), new XAttribute(W + "type", "dxa")), idx == 0 ? new XElement(W + "shd", new XAttribute(W + "fill", "E8EFF7")) : null), Paragraph(cell, keep: idx == 0)))));
            body.Add(table); P("");
        }
        internal byte[] Bytes()
        {
            body.Add(new XElement(W + "sectPr", new XElement(W + "pgSz", new XAttribute(W + "w", 11906), new XAttribute(W + "h", 16838)), new XElement(W + "pgMar", new XAttribute(W + "top", 1000), new XAttribute(W + "bottom", 1000), new XAttribute(W + "left", 1273), new XAttribute(W + "right", 1273))));
            using var memory = new MemoryStream(); using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            {
                void Add(string name, string text) { using var sw = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); sw.Write(text); }
                Add("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"png\" ContentType=\"image/png\"/><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
                Add("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"doc\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
                Add("word/document.xml", new XDocument(new XElement(W + "document", new XAttribute(XNamespace.Xmlns + "w", W), body)).ToString());
                XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
                Add("word/_rels/document.xml.rels", new XElement(rel + "Relationships", images.Select((_, i) => new XElement(rel + "Relationship", new XAttribute("Id", "image" + (i + 1)), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"), new XAttribute("Target", "media/image" + (i + 1) + ".png")))).ToString());
                for (int i = 0; i < images.Count; i++) { using var stream = zip.CreateEntry("word/media/image" + (i + 1) + ".png").Open(); stream.Write(images[i]); }
            }
            return memory.ToArray();
        }
    }
}
