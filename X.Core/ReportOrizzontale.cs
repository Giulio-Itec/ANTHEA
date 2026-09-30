using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace X.Core;

/// <summary>Self-contained DOCX with inputs, model, equilibrium diagnostics and numerical tables.</summary>
public static class ReportOrizzontale
{
    public sealed record Figure(int Survey, string Caption, byte[] Png, double AspectRatio);
    public static void Write(string path, string title, JsonObject result, bool includeInputs = true)
        => Archivio.ScriviAtomico(path, Create(title, result, includeInputs));
    public static byte[] Create(string title, JsonObject result, bool includeInputs = true, IReadOnlyList<Figure>? figures = null)
    {
        if (result.S("errore") != "" || result["input"] is not JsonObject) throw new ArgumentException("Risultato orizzontale non disponibile.");
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body = new XElement(w + "body");
        XElement Paragraph(string text, bool heading = false) => new(w + "p",
            new XElement(w + "pPr", new XElement(w + "spacing", new XAttribute(w + "after", heading ? 160 : 70)), heading ? new XElement(w + "keepNext") : null),
            new XElement(w + "r", new XElement(w + "rPr", new XElement(w + "rFonts", new XAttribute(w + "ascii", "Calibri"), new XAttribute(w + "hAnsi", "Calibri")),
                new XElement(w + "sz", new XAttribute(w + "val", heading ? 25 : 20)), heading ? new XElement(w + "b") : null),
                new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
        void P(string text, bool heading = false) => body.Add(Paragraph(text, heading));
        var images = new List<byte[]>();
        void Image(Figure figure)
        {
            images.Add(figure.Png); int id = images.Count; long cx = 9251950, cy = (long)(cx / figure.AspectRatio);
            XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing", a = "http://schemas.openxmlformats.org/drawingml/2006/main", pic = "http://schemas.openxmlformats.org/drawingml/2006/picture", r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            P($"Sondaggio {figure.Survey} · {figure.Caption}", true);
            var properties = new XElement(pic + "nvPicPr", new XElement(pic + "cNvPr", new XAttribute("id", id), new XAttribute("name", figure.Caption)), new XElement(pic + "cNvPicPr"));
            var fill = new XElement(pic + "blipFill", new XElement(a + "blip", new XAttribute(r + "embed", "image" + id)), new XElement(a + "stretch", new XElement(a + "fillRect")));
            var shape = new XElement(pic + "spPr", new XElement(a + "xfrm", new XElement(a + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(a + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))), new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst")));
            var graphic = new XElement(a + "graphic", new XElement(a + "graphicData", new XAttribute("uri", pic.NamespaceName), new XElement(pic + "pic", properties, fill, shape)));
            var inline = new XElement(wp + "inline", new XElement(wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)), new XElement(wp + "docPr", new XAttribute("id", id), new XAttribute("name", figure.Caption), new XAttribute("descr", "Diagrammi con profondità comune, strati e quote caratteristiche; completamento idealizzato tratteggiato.")), graphic);
            body.Add(new XElement(w + "p", new XElement(w + "r", new XElement(w + "drawing", inline))));
        }
        void Table(string[] headers, IEnumerable<string[]> rows)
        {
            var table = new XElement(w + "tbl", new XElement(w + "tblPr", new XElement(w + "tblW", new XAttribute(w + "w", "9360"), new XAttribute(w + "type", "dxa")),
                new XElement(w + "tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(k => new XElement(w + k, new XAttribute(w + "val", "single"), new XAttribute(w + "sz", 4), new XAttribute(w + "color", "D9D9D9"))))));
            int width = 9360 / headers.Length;
            table.Add(new XElement(w + "tblGrid", headers.Select(_ => new XElement(w + "gridCol", new XAttribute(w + "w", width)))));
            foreach (var (cells, i) in new[] { headers }.Concat(rows).Select((r, i) => (r, i)))
                table.Add(new XElement(w + "tr", new XElement(w + "trPr", new XElement(w + "cantSplit"), i == 0 ? new XElement(w + "tblHeader") : null),
                    cells.Select(c => new XElement(w + "tc", new XElement(w + "tcPr", new XElement(w + "tcW", new XAttribute(w + "w", width), new XAttribute(w + "type", "dxa")),
                        i == 0 ? new XElement(w + "shd", new XAttribute(w + "fill", "E8EFF7")) : null), i == 0 ? new XElement(w + "p", new XElement(w + "pPr", new XElement(w + "keepNext")), new XElement(w + "r", new XElement(w + "rPr", new XElement(w + "b"), new XElement(w + "sz", new XAttribute(w + "val", 20))), new XElement(w + "t", c))) : Paragraph(c)))));
            body.Add(table);
        }
        P("ANTHEA — " + title, true); P("Palo singolo: capacità portante orizzontale — " + result.S("versione_motore"));
        bool distributed = result.S("metodo_calcolo") == PaloOrizzontale.StratifiedMethod;
        P(distributed ? "STRATIFICATO — MODELLO SPERIMENTALE" : result.B("sperimentale") ? "MODALITÀ MULTISTRATO SPERIMENTALE" : "Metodo omogeneo di Broms", true);
        if (result.S("selezione_modello") == "Automatica") P("Modello selezionato automaticamente in base alle proprietà degli strati attraversati e alla falda: " + result.S("modello_adottato") + ".");
        P("Modello e fonti", true); P(result.S("fonte")); P(result.S("percorso"));
        P("z positivo verso il basso; H positiva; p [kN/m] positiva se opposta a H. V(z)=H−∫p dz; M(z)=M0+Hz−∫(z−s)p(s) ds. Compressione N positiva.");
        P("Coesivo non drenato: p=0 per z<1,5D; p=9CuD al di sotto (Viggiani p.400). Granulare drenato: p=3KpDσ′v; Kp=(1+sinφ′)/(1−sinφ′), σ′v integrata dagli strati sovrastanti; γw=9,81 kN/m³. Nessun modello c–φ. " +
            (distributed ? "Ammesse sequenze miste: il peso efficace degli strati coesivi contribuisce al confinamento dei granulari sottostanti. Il taglio a 1,5D è riferito al piano campagna e si applica solo ai tratti coesivi." : "Sequenze miste escluse dal metodo Broms."));
        if (distributed)
        {
            P("Sviluppo dell'approccio stratificato: Q(z)=∫p_lim dz; S(z)=∫z p_lim dz; Q(z_f)=H. La coppia inferiore vale C(z_f,t)=S(t)+S(z_f)−2S(b), con Q(b)=[Q(z_f)+Q(t)]/2. Si confrontano i candidati corto, intermedio e lungo con un unico My e si controlla l'equilibrio globale.");
            P("Reazioni distribuite anche nei granulari e nei profili misti; nessuna risultante concentrata al piede. Recupera il coesivo omogeneo e il ramo lungo granulare quando la lunghezza è sufficiente alla chiusura distribuita. Per il granulare corto/intermedio differisce dalle formule di Broms con forza concentrata. La chiusura inferiore è un'ipotesi del modello, non una soluzione dell'interazione elastoplastica o una validazione sperimentale.");
        }
        else
        {
            P("Omogeneo: coesivo testa libera eq.13.23–13.29; impedita eq.13.30–13.36. Granulare libera eq.13.37–13.43; impedita eq.13.44–13.47. L'estensione a strati e falda interna è una scelta ANTHEA sperimentale: integrali esatti a tratti e ricerca delle radici, senza media dei parametri o somma di capacità.");
            P("Nel coesivo il tratto inferiore forma una coppia con risultante nulla. Nel granulare F è una risultante concentrata separata: il completamento sotto la cerniera è idealizzato, non univoco.");
        }
        P("I diagrammi non forniscono spostamenti o rotazioni.");
        P("Lettura dei diagrammi", true);
        P("σv è la tensione verticale totale da peso proprio; u è la pressione idrostatica; σ′v=σv−u. La pressione laterale equivalente q_lim=p_lim/D [kPa] è distinta dalla reazione lineare p_lim [kN/m]. Le curve p e q rappresentano la distribuzione adottata alla capacità Hu, non il carico HEd. ξ e γR riducono la capacità globale e non le tensioni locali.");
        P("Q(z)=∫p_lim dz individua z_f con Q(z_f)=Hu. z_f (taglio nullo) è distinto da b (inversione delle reazioni). Il tratto sotto la cerniera interna è un completamento idealizzato, mostrato tratteggiato su fondo grigio. Le forze concentrate sono dichiarate separatamente.");
        var input = result["input"]!; var g = input["generali"]!;
        void Parameters(string title, JsonNode values, (string Key, string Label)[] fields)
        {
            P(title, true); Table(["Parametro", "Valore"], fields.Select(f => new[] { f.Label, values.S(f.Key) }));
        }
        if (includeInputs)
        {
        P("Dati di ingresso", true);
        Parameters("Geometria e azioni", g, [("diametro", "Diametro D [m]"), ("lunghezza", "Lunghezza infissa L [m]"),
            ("eccentricita", "Quota forza sopra terreno e [m]"), ("vincolo", "Rotazione in testa"),
            ("azione_orizzontale", "HEd [kN]"), ("azione_assiale", "N costante [kN], compressione positiva"),
            ("presenza_falda", "Presenza falda"), ("origine_momento", "Origine del momento resistente"),
            ("passo", "Passo dei diagrammi [m]"), ("tolleranza", "Tolleranza delle radici")]);
        if (g.B("presenza_falda")) P("Profondità falda: " + g.S("profondita_falda") + " m.");
        if (g.S("origine_momento") == "Manuale")
            P("My manuale: " + g.S("momento_resistente") + " kNm. Natura/provenienza: " + g.S("provenienza_momento"));
        else if (input.S("tipo_sezione") == "CHS")
        {
            Parameters("Sezione resistente CHS · solo acciaio", input["sezione"]!, [("modo_chs", "Inserimento"), ("profilo_chs", "Catalogo (solo modalità catalogo)"),
                ("diametro_chs_mm", "Diametro manuale [mm] (solo modalità manuale)"), ("spessore_chs_mm", "Spessore manuale [mm] (solo modalità manuale)"), ("fy_chs_mpa", "fy [MPa]"), ("gamma_m0", "γM0")]);
            if (result["sezione"] is JsonObject chs) Parameters("Proprietà CHS adottate", chs,
                [("diametro_mm", "De [mm]"), ("spessore_mm", "t [mm]"), ("area_mm2", "A [mm²]"), ("inerzia_mm4", "I [mm⁴]"), ("wel_mm3", "Wel [mm³]"), ("wpl_mm3", "Wpl [mm³]"), ("classe", "Classe"), ("momento_knm", "My(N) [kNm]")]);
        }
        else
        {
            var actualSection = SezioneCA.DefaultInput(); foreach (var (k, v) in input["sezione"]!.AsObject()) actualSection[k] = v?.DeepClone();
            Parameters("Sezione circolare e materiali", actualSection, [("cover_mm", "Copriferro esterno staffa [mm]"),
                ("transverse_bar_diameter_mm", "Diametro staffa [mm]"), ("longitudinal_bar_count", "Numero barre"),
                ("longitudinal_bar_diameter_mm", "Diametro barre [mm]"), ("fck_mpa", "fck [MPa]"), ("fyk_mpa", "fyk [MPa]"),
                ("alpha_cc", "αcc"), ("gamma_c", "γc"), ("gamma_s", "γs"), ("steel_modulus_mpa", "Es [MPa]")]);
            P("Diametro e N della sezione corrispondono ai dati generali del palo.");
        }
        }
        P($"Coefficienti: verticali indagate {result.S("verticali_indagate")}; ξ3 = {result.D("xi3"):0.00}; ξ4 = {result.D("xi4"):0.00}; γR = {result.D("gamma_r"):0.00}.");
        if (result["efficienza"] is JsonObject efficiency)
        {
            P($"Efficienza: {efficiency.S("metodo")}; η = {efficiency.D("eta"):0.000}. Rd = η · Rk / γR.");
            if (includeInputs && efficiency.S("metodo") != "Manuale")
            {
                Parameters("Interassi rispetto alla direzione di H", input["verifica"]!,
                    [("interasse_anteriore", "Anteriore [m]"), ("interasse_posteriore", "Posteriore [m]"), ("interasse_sinistro", "Sinistro [m]"), ("interasse_destro", "Destro [m]")]);
                foreach (var coefficient in efficiency.Where(p => p.Key is not ("metodo" or "eta")))
                    P($"η {coefficient.Key}: {J.Number(coefficient.Value):0.000}");
            }
        }
        int index = 0;
        foreach (var survey in includeInputs ? result["input"].Array("stratigrafie") : new JsonArray())
        {
            P($"Stratigrafia {++index}", true);
            Table(["Tipo", "S [m]", "γ", "γsat", "φ′ [°]", "Cu [kPa]"], survey!.AsArray().Select(r => new[] { r.S("tipologia"), r.S("spessore"), r.S("peso_specifico"), r.S("peso_specifico_saturo"), r.S("angolo_attrito"), r.S("coesione_non_drenata") }));
        }
        P("Risultati", true);
        P($"Hu = {result.D("capacita_kn"):0.###} kN; sondaggio governante {result.D("sondaggio_governante")}; meccanismo {result.S("meccanismo")}; My adottato = {result.D("momento_resistente_knm"):0.###} kNm.");
        if (result["sezione"] is JsonObject section)
        {
            P(section.S("modello"));
            if (section.S("tipo") == "CHS") P($"N={section.D("n_kn"):0.0} kN; fyd={section.D("fyd_mpa"):0.0} MPa; My(N)={section.D("momento_knm"):0.0} kNm.");
            else P($"N={section.D("n_kn"):0.###} kN (compressione positiva); fcd={section.D("fcd_mpa"):0.###} MPa; fyd={section.D("fyd_mpa"):0.###} MPa; As={section.D("area_acciaio_mm2"):0.###} mm²; residuo N={section.D("residuo_n_kn"):G4} kN; motore={section.S("motore", "storico")}; contorno={section.D("lati_contorno"):0} lati.");
        }
        P("Verifica normativa: " + result.S("verifica_normativa"));
        if (result["resistenza_progetto_manuale_kn"] is not null)
            P($"Rk = min(Hu,media/ξ3; Hu,min/ξ4) = min({result.D("ramo_media_kn"):0.0}; {result.D("ramo_minimo_kn"):0.0}) = {result.D("resistenza_caratteristica_manuale_kn"):0.0} kN; criterio governante: {result.S("criterio_governante")}. Rd = η · Rk/γR = {result.D("resistenza_progetto_manuale_kn"):0.0} kN. HEd = {result.D("azione_kn"):0.0} kN. {result.S("esito_manuale")}. Non costituisce conformità normativa complessiva automatica.");
        else P("Rk e Rd non determinate. Nessun esito normativo attribuito al confronto HEd/Hu.");
        P("Avvisi e limiti", true); foreach (var warning in result.Array("avvisi")) P(warning!.ToString());
        index = 0;
        foreach (var survey in result.Array("sondaggi"))
        {
            P($"Sondaggio {++index}: {survey.S("meccanismo")}", true);
            P($"Hu={survey.D("capacita_kn"):0.###} kN; |M|max={survey.D("momento_massimo_knm"):0.###} kNm a z={survey.D("quota_momento_massimo_m"):0.###} m. Cerniere z [m]: {survey!["cerniere_m"]}.");
            P($"Residui di equilibrio: forza={survey.D("residuo_forza_kn"):G4} kN; momento={survey.D("residuo_momento_knm"):G4} kNm. Convergenza: {survey.S("convergenza")}.");
            if (survey.S("chiusura_equilibrio") == "Distribuita")
                P($"Chiusura distribuita: inversione a {survey.D("inversione_reazioni_m"):0.####} m; fine reazioni a {survey.D("fine_reazioni_m"):0.####} m. Risultante concentrata nulla.");
            else P($"Risultante concentrata F={survey.D("risultante_concentrata_kn"):0.###} kN a z={survey.D("quota_risultante_m"):0.###} m, positiva nel verso di H. Non è una pressione distribuita.");
            Table(["Meccanismo", "Capacità candidata [kN]", "Stato"], survey.Array("candidati").Select(r => new[] { r.S("meccanismo"), r!["capacita_kn"] is null ? "Non attivabile" : r.D("capacita_kn").ToString("0.###"), r.S("stato") }));
            P(survey.S("nota_tensioni"));
            P("Legge locale per tratti", true);
            Table(["Strato / tipo", "da / a [m]", "σ′v iniz. [kPa]", "p iniz. [kN/m]", "dp/dz [kN/m²]"], survey.Array("diagramma_limite").Select(r => new[] {
                r.S("strato") + " / " + r.S("tipologia"), $"{r.D("da_m"):0.###} / {r.D("a_m"):0.###}", r!["sigma_eff_iniziale_kpa"] is null ? "Non disponibile" : r.D("sigma_eff_iniziale_kpa").ToString("0.###"), r.D("p_iniziale_kn_m").ToString("0.###"), r.D("pendenza_kn_m2").ToString("0.###") }));
            P("Diagrammi tabellari alla capacità ultima (prima/dopo: lati della quota)", true);
            Table(["z [m]", "Lato", "p [kN/m]", "V [kN]", "M [kNm]"], survey.Array("diagrammi").Select(r => new[] { r.D("z").ToString("0.####"), r.S("lato"), r.D("p_kn_m").ToString("0.####"), r.D("v_kn").ToString("0.####"), r.D("m_knm").ToString("0.####") }));
        }
        P("Riferimenti bibliografici e attribuzione delle ipotesi", true);
        foreach (var reference in result.Array("riferimenti"))
        {
            P(reference.S("titolo"), true); P(reference.S("dettaglio")); P(reference.S("uso"));
            if (reference.S("url") != "") P(reference.S("url"));
        }
        XElement Section(bool landscape) => new(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", landscape ? 16838 : 11906), new XAttribute(w + "h", landscape ? 11906 : 16838), landscape ? new XAttribute(w + "orient", "landscape") : null),
            new XElement(w + "pgMar", new XAttribute(w + "top", landscape ? 567 : 1134), new XAttribute(w + "bottom", landscape ? 567 : 1134), new XAttribute(w + "left", landscape ? 1134 : 1273), new XAttribute(w + "right", landscape ? 1134 : 1273)));
        if (figures?.Count > 0)
        {
            body.Add(new XElement(w + "p", new XElement(w + "pPr", Section(false))));
            foreach (var figure in figures)
            {
                if (images.Count > 0) body.Add(new XElement(w + "p", new XElement(w + "r", new XElement(w + "br", new XAttribute(w + "type", "page")))));
                Image(figure);
            }
        }
        body.Add(Section(images.Count > 0));
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string content) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(content); }
            Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"png\" ContentType=\"image/png\"/><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"doc\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Entry("word/document.xml", new XDocument(new XElement(w + "document", body)).ToString());
            XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
            Entry("word/_rels/document.xml.rels", new XElement(rel + "Relationships", images.Select((_, i) => new XElement(rel + "Relationship", new XAttribute("Id", "image" + (i + 1)), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"), new XAttribute("Target", "media/image" + (i + 1) + ".png")))).ToString());
            for (int i = 0; i < images.Count; i++) { using var stream = zip.CreateEntry("word/media/image" + (i + 1) + ".png").Open(); stream.Write(images[i]); }
        }
        return memory.ToArray();
    }
}
