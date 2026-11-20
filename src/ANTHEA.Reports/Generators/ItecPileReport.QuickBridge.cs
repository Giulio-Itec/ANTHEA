using X.Core;

namespace Anthea.Reports;

internal sealed partial class ItecPileReport
{
    internal static DocxDocument CreateQuickBridge(string title, BridgeQuickReportData data, IReadOnlySet<string> options, IReadOnlyList<ImmagineReport> images)
    {
        var writer = new ItecPileReport(); writer.BuildQuickBridge(title, data, options, images); return writer.document;
    }
    void BuildQuickBridge(string title, BridgeQuickReportData data, IReadOnlySet<string> options, IReadOnlyList<ImmagineReport> images)
    {
        bool brief = options.Contains(BridgeQuickReport.Brief), header = !brief && options.Contains("intestazione");
        if (!header) body.RemoveNodes();
        int cover = body.Elements().Count();
        if (brief) { QuickBridgeBrief(title, data, images); FinishFamily(); return; }
        foreach (var part in BridgeQuickReport.Outline().Where(c => options.Contains(c.Key)))
        {
            if (part.Key is "intestazione" or "indice") continue;
            Heading(part.Label);
            switch (part.Key)
            {
                case "oggetto":
                    Paragraph(title); Paragraph(data.Scope); Paragraph(data.Standard);
                    Paragraph("Si usa il motore della sezione composta da ponte, con le stesse fasi, la stessa omogeneizzazione e le stesse riduzioni efficaci. "
                        + "Le azioni sono già combinate e coefficientate: non si applicano ulteriori fattori ai carichi. N e V della tabella rapida sono applicati nella fase breve.");
                    Paragraph("Il ritiro eventualmente presente resta una fase autonoma del documento. N è positivo a trazione; le tensioni di compressione sono negative. "
                        + "Le quote y sono riferite all’intradosso della soletta. Dimensioni in mm, momenti in kNm, forze in kN e tensioni in MPa.");
                    Paragraph("Le tensioni sono confrontate con i limiti restituiti per lo stato limite assegnato; il taglio comprende l’instabilità dell’anima. "
                        + "I rapporti mostrati includono anche le situazioni intermedie. Il superamento di un limite locale e gli avvisi del motore rimangono espliciti.");
                    break;
                case "geometria":
                    QuickValues("Dimensioni", data.Geometry);
                    Table("Armature della soletta", ["Fila", "Presente", "Ø [mm]", "Passo [mm]", "Distanza [mm]", "As [mm²]"], data.Reinforcement.ToList(), preservePrecision: true);
                    Paragraph("Distanza dal bordo della soletta all’asse delle barre; area restituita dalla geometria del motore."); break;
                case "materiali": QuickValues("Materiali e coefficienti adottati", data.Materials); break;
                case "azioni":
                    Table("Combinazioni", ["Nome / SL", "M getto", "M lungo", "M breve", "V", "N"], data.Cases.Select(c => new[] { c.Name + " · " + c.State }.Concat(c.Actions).ToArray()).ToList(), preservePrecision: true);
                    Paragraph("M in kNm; N e V in kN. Lungo: permanenti portati. Breve: traffico.");
                    foreach (var item in data.Cases)
                        Table("Fasi conservate · " + item.Name, ["Fase", "Tipo", "M [kNm]", "N [kN]", "V [kN]", "φ", "ψL", "εcs [µε]"], item.Phases.ToList(), preservePrecision: true);
                    break;
                case "risultati":
                    Table("Riepilogo", ["Combinazione", "SL", "Rapporto", "Esito"], data.Cases.Select(c => new[] { c.Name, c.State, QNumber(c.Ratio), c.Status }).ToList(), preservePrecision: true);
                    foreach (var item in data.Cases)
                    {
                        Subheading(item.Name + " · " + item.State);
                        Table("Verifiche", ["Verifica", "Risultato", "η", "Esito"], item.Checks.Select(c => new[] { c.Kind, c.Value, QNumber(c.Ratio), QOutcome(c) }).ToList(), preservePrecision: true);
                        foreach (var check in item.Checks) Paragraph(check.Detail);
                        QuickValues("Valori del motore", item.Values);
                        Table("Tensioni per fase", ["Situazione", "Fibra", "σ [MPa]", "Limite [MPa]", "η"], item.Stresses.ToList(), preservePrecision: true);
                        Table("Taglio e tensione equivalente", ["Situazione", "Controllo", "Domanda", "Limite", "Unità", "η"], item.Shear.ToList(), preservePrecision: true);
                    }
                    break;
                case "avvisi": foreach (string warning in data.Warnings.Count == 0 ? ["Nessun avviso del motore."] : data.Warnings) Paragraph(warning); break;
                default: foreach (var image in images.Where(i => i.Categoria == part.Key)) QuickFigure(image, 3400000); break;
            }
        }
        FinishFamily(cover, header, options.Contains("indice"));
    }
    void QuickBridgeBrief(string title, BridgeQuickReportData data, IReadOnlyList<ImmagineReport> images)
    {
        BriefText("SEZIONE COMPOSTA · VERIFICA RAPIDA", 14, 100); BriefText(title, 11, 100); BriefText(data.Scope + " " + data.Standard, 10);
        BriefHeading("1  Geometria e materiali");
        SmallTable(["Dimensione", "mm", "Dimensione", "mm"], data.Geometry.Chunk(2).Select(pair =>
            new[] { pair[0].Label, pair[0].Value, pair.Length > 1 ? pair[1].Label : "", pair.Length > 1 ? pair[1].Value : "" }).ToList(), [3, 1, 3, 1]);
        BriefText(string.Join(" · ", data.Materials.Where(v => v.Label is "Calcestruzzo" or "Carpenteria" or "Armatura").Select(v => v.Label + " " + v.Value)), 10);
        SmallTable(["Fila", "Presente", "Ø", "Passo", "Distanza", "As [mm²]"], data.Reinforcement.ToList());
        var section = images.FirstOrDefault(i => i.Categoria == "sezione");
        if (section is not null) QuickFigure(section, 3000000, brief: true);
        BriefHeading("2  Casi governanti per stato limite", page: true);
        var governing = BridgeQuickReport.Governing(data);
        string CheckLabel(QuickReportCheck check) => check.Kind switch
        {
            "Taglio · instabilità dell’anima" => "Taglio / anima", "Anima · tensione equivalente" => "Tensione equivalente",
            "Tensioni · tutte le fasi" => "Tensioni", _ => check.Kind
        };
        SmallTable(["SL", "Caso", "Verifica", "Valore", "η", "Esito"], governing.Select(p => new[] { p.Case.State,
            p.Case.Name, CheckLabel(p.Check), p.Check.Value, QNumber(p.Check.Ratio), p.Check.Passed is true ? "OK" : p.Check.Passed is false ? "NO" : "n.d." }).ToList(), [1, 1.2, 2.5, 2.3, .7, .7]);
        BriefText("OK/NO: limite locale rispettato/superato. n.d.: rapporto non determinato; per il taglio agli SLE il motore non prevede il rapporto. Gli avvisi restano applicabili.", 10);
        BriefHeading("3  Azioni dei casi governanti");
        SmallTable(["Nome / SL", "M getto", "M lungo", "M breve", "V", "N"], governing.Select(p => p.Case).DistinctBy(c => c.Id)
            .Select(c => new[] { c.Name + "\n" + c.State }.Concat(c.Actions).ToArray()).ToList());
        BriefText("Momenti in kNm, forze in kN. N e V nella fase breve. Le situazioni intermedie concorrono all’esito. Dettagli e coefficienti nella relazione completa.", 10);
        foreach (var warning in data.Warnings) BriefText(warning, 10);
    }
}
