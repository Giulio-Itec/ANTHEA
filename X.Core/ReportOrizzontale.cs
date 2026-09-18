using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace X.Core;

/// <summary>Self-contained DOCX with inputs, model, equilibrium diagnostics and numerical tables.</summary>
public static class ReportOrizzontale
{
    public static void Write(string path, string title, JsonObject result)
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
        void Table(string[] headers, IEnumerable<string[]> rows)
        {
            var table = new XElement(w + "tbl", new XElement(w + "tblPr", new XElement(w + "tblW", new XAttribute(w + "w", "9360"), new XAttribute(w + "type", "dxa")),
                new XElement(w + "tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(k => new XElement(w + k, new XAttribute(w + "val", "single"), new XAttribute(w + "sz", 4), new XAttribute(w + "color", "D9D9D9"))))));
            int width = 9360 / headers.Length;
            table.Add(new XElement(w + "tblGrid", headers.Select(_ => new XElement(w + "gridCol", new XAttribute(w + "w", width)))));
            foreach (var (cells, i) in new[] { headers }.Concat(rows).Select((r, i) => (r, i)))
                table.Add(new XElement(w + "tr", new XElement(w + "trPr", new XElement(w + "cantSplit"), i == 0 ? new XElement(w + "tblHeader") : null),
                    cells.Select(c => new XElement(w + "tc", new XElement(w + "tcPr", new XElement(w + "tcW", new XAttribute(w + "w", width), new XAttribute(w + "type", "dxa")),
                        i == 0 ? new XElement(w + "shd", new XAttribute(w + "fill", "E8EFF7")) : null), Paragraph(c)))));
            body.Add(table);
        }
        P("ANTHEA — " + title, true); P("Palo singolo: capacità portante orizzontale — " + result.S("versione_motore"));
        P(result.B("sperimentale") ? "MODALITÀ MULTISTRATO SPERIMENTALE" : "Metodo omogeneo di Broms", true);
        P("Modello e fonti", true); P(result.S("fonte")); P(result.S("percorso"));
        P("z positivo verso il basso; H positiva; p [kN/m] positiva se opposta a H. V(z)=H−∫p dz; M(z)=M0+Hz−∫(z−s)p(s) ds. Compressione N positiva.");
        P("Coesivo non drenato: p=0 per z<1,5D; p=9CuD al di sotto (Viggiani p.400). Granulare drenato: p=3KpDσ′v; Kp=(1+sinφ′)/(1−sinφ′), σ′v integrata dagli strati sovrastanti; γw=9,81 kN/m³. Nessun modello c–φ o sequenza mista.");
        P("Omogeneo: coesivo testa libera eq.13.23–13.29; impedita eq.13.30–13.36. Granulare libera eq.13.37–13.43; impedita eq.13.44–13.47. L'estensione a strati e falda interna è una scelta ANTHEA sperimentale: integrali esatti a tratti e ricerca delle radici, senza media dei parametri o somma di capacità.");
        P("Nel coesivo il tratto inferiore forma una coppia con risultante nulla. Nel granulare F è una risultante concentrata separata: il completamento sotto la cerniera è idealizzato, non univoco. I diagrammi non forniscono spostamenti o rotazioni.");
        P("Dati di ingresso", true);
        var input = result["input"]!; var g = input["generali"]!;
        void Parameters(string title, JsonNode values, (string Key, string Label)[] fields)
        {
            P(title, true); Table(["Parametro", "Valore"], fields.Select(f => new[] { f.Label, values.S(f.Key) }));
        }
        Parameters("Geometria e azioni", g, [("diametro", "Diametro D [m]"), ("lunghezza", "Lunghezza infissa L [m]"),
            ("eccentricita", "Quota forza sopra terreno e [m]"), ("vincolo", "Rotazione in testa"), ("modalita", "Modello terreno"),
            ("azione_orizzontale", "HEd [kN]"), ("azione_assiale", "N costante [kN], compressione positiva"),
            ("presenza_falda", "Presenza falda"), ("origine_momento", "Origine del momento resistente"),
            ("passo", "Passo dei diagrammi [m]"), ("tolleranza", "Tolleranza delle radici")]);
        if (g.B("presenza_falda")) P("Profondità falda: " + g.S("profondita_falda") + " m.");
        if (g.S("origine_momento") == "Manuale")
            P("My manuale: " + g.S("momento_resistente") + " kNm. Natura/provenienza: " + g.S("provenienza_momento"));
        else
        {
            var actualSection = SezioneCA.DefaultInput(); foreach (var (k, v) in input["sezione"]!.AsObject()) actualSection[k] = v?.DeepClone();
            Parameters("Sezione circolare e materiali", actualSection, [("cover_mm", "Copriferro esterno staffa [mm]"),
                ("transverse_bar_diameter_mm", "Diametro staffa [mm]"), ("longitudinal_bar_count", "Numero barre"),
                ("longitudinal_bar_diameter_mm", "Diametro barre [mm]"), ("fck_mpa", "fck [MPa]"), ("fyk_mpa", "fyk [MPa]"),
                ("alpha_cc", "αcc"), ("gamma_c", "γc"), ("gamma_s", "γs"), ("steel_modulus_mpa", "Es [MPa]")]);
            P("Diametro e N della sezione corrispondono ai dati generali del palo.");
        }
        if (input["verifica"].B("applica_fattori")) Parameters("Fattori manuali", input["verifica"]!,
            [("xi", "Divisore Hu → Rk"), ("gamma_r", "Divisore Rk → Rd"), ("riferimento", "Fonte e criterio adottato")]);
        int index = 0;
        foreach (var survey in result["input"].Array("stratigrafie"))
        {
            P($"Stratigrafia {++index}", true);
            Table(["Tipo", "S [m]", "γ", "γsat", "φ′ [°]", "Cu [kPa]"], survey!.AsArray().Select(r => new[] { r.S("tipologia"), r.S("spessore"), r.S("peso_specifico"), r.S("peso_specifico_saturo"), r.S("angolo_attrito"), r.S("coesione_non_drenata") }));
        }
        P("Risultati", true);
        P($"Hu = {result.D("capacita_kn"):0.###} kN; sondaggio governante {result.D("sondaggio_governante")}; meccanismo {result.S("meccanismo")}; My adottato = {result.D("momento_resistente_knm"):0.###} kNm.");
        if (result["sezione"] is JsonObject section)
        {
            P(section.S("modello")); P($"N={section.D("n_kn"):0.###} kN; x={section.D("asse_neutro_mm"):0.###} mm; fcd={section.D("fcd_mpa"):0.###} MPa; fyd={section.D("fyd_mpa"):0.###} MPa; As={section.D("area_acciaio_mm2"):0.###} mm²; residuo N={section.D("residuo_n_kn"):G4} kN; scarto mesh={section.D("scarto_mesh"):G4}.");
        }
        P("Verifica normativa: " + result.S("verifica_normativa"));
        if (result["resistenza_progetto_manuale_kn"] is not null)
            P($"Fattori manuali: Rk=Hu/ξ={result.D("resistenza_caratteristica_manuale_kn"):0.###} kN; Rd=Rk/γR={result.D("resistenza_progetto_manuale_kn"):0.###} kN. HEd={result.D("azione_kn"):0.###} kN. {result.S("esito_manuale")}. Non costituisce conformità normativa automatica.");
        else P("Rk e Rd non determinate. Nessun esito normativo attribuito al confronto HEd/Hu.");
        P("Avvisi e limiti", true); foreach (var warning in result.Array("avvisi")) P(warning!.ToString());
        index = 0;
        foreach (var survey in result.Array("sondaggi"))
        {
            P($"Sondaggio {++index}: {survey.S("meccanismo")}", true);
            P($"Hu={survey.D("capacita_kn"):0.###} kN; |M|max={survey.D("momento_massimo_knm"):0.###} kNm a z={survey.D("quota_momento_massimo_m"):0.###} m. Cerniere z [m]: {survey!["cerniere_m"]}.");
            P($"Residui di equilibrio: forza={survey.D("residuo_forza_kn"):G4} kN; momento={survey.D("residuo_momento_knm"):G4} kNm. Convergenza: {survey.S("convergenza")}.");
            P($"Risultante concentrata F={survey.D("risultante_concentrata_kn"):0.###} kN a z={survey.D("quota_risultante_m"):0.###} m, positiva nel verso di H. Non è una pressione distribuita.");
            Table(["Meccanismo", "Capacità candidata [kN]", "Stato"], survey.Array("candidati").Select(r => new[] { r.S("meccanismo"), r!["capacita_kn"] is null ? "Non attivabile" : r.D("capacita_kn").ToString("0.###"), r.S("stato") }));
            P("Diagrammi tabellari alla capacità ultima (prima/dopo: lati della quota)", true);
            Table(["z [m]", "Lato", "p [kN/m]", "V [kN]", "M [kNm]"], survey.Array("diagrammi").Select(r => new[] { r.D("z").ToString("0.####"), r.S("lato"), r.D("p_kn_m").ToString("0.####"), r.D("v_kn").ToString("0.####"), r.D("m_knm").ToString("0.####") }));
        }
        body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", 11906), new XAttribute(w + "h", 16838)),
            new XElement(w + "pgMar", new XAttribute(w + "top", 1134), new XAttribute(w + "bottom", 1134), new XAttribute(w + "left", 1273), new XAttribute(w + "right", 1273))));
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Entry(string name, string content) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(content); }
            Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
            Entry("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"doc\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
            Entry("word/document.xml", new XDocument(new XElement(w + "document", body)).ToString());
        }
        Archivio.ScriviAtomico(path, memory.ToArray());
    }
}
