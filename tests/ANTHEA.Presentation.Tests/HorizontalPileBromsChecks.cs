using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Reports;
using X.Core;

namespace Anthea.Presentation.Tests;

/// <summary>
/// Stratificato of the horizontal pile, option C of the ANTHEA decision sheet of 16/11/2026 (decided by the user on 17/11/2026, «A + C, con la Hu
/// di Broms accanto»): where the stratified model governs a short or intermediate pile in granular soil the sheet and the report also show Hu of
/// Broms for the same pile and vertical and the ratio Stratificato/Broms (GPCChecker.Geotechnics 0.30, LateralSurveyResult.BromsCapacity). The
/// capacity and the verification stay the stratified ones; nothing is added elsewhere.
/// Expected values by closed form, figure 2 of the article on the stratified pile (supporto/docs/articolo-palo-orizzontale-stratificato.md) and
/// tools/attesi/palo_stratificato_broms.py of Checker: dry sand φ′ 30°, γ 18 kN/m³, D 0.6 m, L 4 m, free head, e = 1 m, My large enough for the
/// short mechanism: stratified 153.234046 kN, Broms 0.5 γ D L³ Kp/(L + e) = 207.36 kN, ratio 0.738976; e = 0: 202.114608 / 259.2 kN = 0.779763.
/// </summary>
internal static class HorizontalPileBromsChecks
{
    static JsonObject Sand(string method, double length, double eccentricity, string moment)
    {
        var data = PaloOrizzontale.Defaults(); var g = data["generali"]!.AsObject();
        g["diametro"] = "0.6"; g["lunghezza"] = length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        g["eccentricita"] = eccentricity.ToString(System.Globalization.CultureInfo.InvariantCulture); g["vincolo"] = "Libera";
        g["metodo_calcolo"] = method; g["azione_orizzontale"] = "100"; g["presenza_falda"] = false; g["tolleranza"] = "1e-10";
        g["origine_momento"] = "Manuale"; g["momento_resistente"] = moment; g["provenienza_momento"] = "Valore di collaudo";
        var layer = PaloOrizzontale.Layer(); layer["spessore"] = "30"; layer["peso_specifico"] = "18"; layer["peso_specifico_saturo"] = "20";
        layer["angolo_attrito"] = "30"; layer["coesione_efficace"] = "0";
        data["stratigrafie"] = new JsonArray(new JsonArray(layer));
        return data;
    }

    static bool Close(double actual, double expected, double relative) => Math.Abs(actual - expected) <= relative * Math.Abs(expected);

    internal static int Run(string output)
    {
        var e = new Expect();
        var free = PaloOrizzontale.Calculate(Sand(PaloOrizzontale.StratifiedMethod, 4, 1, "1000000"));
        e.That(free.S("errore") == "" && free.S("meccanismo") == "Corto", "Broms accanto: calcolo stratificato del palo corto: " + free.S("errore"));
        e.That(Close(free.D("capacita_kn"), 153.234045952, 1e-6) && Close(free.D("capacita_broms_kn"), 207.36, 1e-9)
            && Close(free.D("rapporto_broms"), .738975916048, 1e-6) && free.S("meccanismo_broms") == "Corto",
            $"Broms accanto: Hu {free.D("capacita_kn")}, Broms {free.D("capacita_broms_kn")}, rapporto {free.D("rapporto_broms")}, {free.S("meccanismo_broms")}.");
        var survey = free.Array("sondaggi")[0]!;
        e.That(survey.D("capacita_broms_kn") == free.D("capacita_broms_kn") && survey.D("rapporto_broms") == free.D("rapporto_broms")
            && survey.S("meccanismo_broms") == "Corto", "Broms accanto: valori della verticale diversi da quelli del risultato.");
        e.That(HorizontalPilePresentation.Verification(free).Rows[1][1] == "153,2" && free.D("resistenza_caratteristica_manuale_kn") <= free.D("capacita_kn"),
            "Broms accanto: la capacità della verifica non è più quella stratificata.");
        var centred = PaloOrizzontale.Calculate(Sand(PaloOrizzontale.StratifiedMethod, 4, 0, "1000000"));
        e.That(Close(centred.D("capacita_kn"), 202.114608398, 1e-6) && Close(centred.D("capacita_broms_kn"), 259.2, 1e-9)
            && Close(centred.D("rapporto_broms"), .779763149685, 1e-6), "Broms accanto: caso e = 0.");

        // No comparison with the Broms method, for a long pile or a cohesive vertical: the results keep the keys of before.
        var broms = PaloOrizzontale.Calculate(Sand(PaloOrizzontale.BromsMethod, 4, 1, "1000000"));
        var longPile = PaloOrizzontale.Calculate(Sand(PaloOrizzontale.StratifiedMethod, 20, 0, "300"));
        var clay = Sand(PaloOrizzontale.StratifiedMethod, 6, 0, "1000000"); clay["stratigrafie"]![0]![0]!["tipologia"] = "Coesivo";
        var cohesive = PaloOrizzontale.Calculate(clay);
        foreach (var (name, r) in new[] { ("Broms", broms), ("palo lungo", longPile), ("coesivo", cohesive) })
        {
            e.That(r.S("errore") == "", "Broms accanto, " + name + ": " + r.S("errore"));
            e.That(r["capacita_broms_kn"] is null && r["rapporto_broms"] is null && r["meccanismo_broms"] is null
                && r.Array("sondaggi").All(s => s!["capacita_broms_kn"] is null && s["rapporto_broms"] is null && s["meccanismo_broms"] is null)
                && !HorizontalPilePresentation.Summary(r).Contains("Broms =", StringComparison.Ordinal),
                "Broms accanto: confronto scritto anche per " + name + ".");
        }
        e.That(Close(broms.D("capacita_kn"), 207.36, 1e-9) && longPile.S("meccanismo") == "Lungo", "Broms accanto: premesse dei casi senza confronto.");

        // Sheet: the summary and the details of the vertical; report: the results and the vertical.
        string line = "\nHu di Broms = 207,4 kN (corto) · Stratificato / Broms = 0,739";
        e.That(HorizontalPilePresentation.Summary(free).EndsWith("Modello di calcolo per terreno stratificato" + line, StringComparison.Ordinal),
            "Broms accanto: sintesi del foglio: " + HorizontalPilePresentation.Summary(free));
        e.That(HorizontalPilePresentation.Results(free)[0].Info.EndsWith(line, StringComparison.Ordinal), "Broms accanto: dettagli della verticale nel foglio.");
        var xml = DocxWriter.Read(ReportOrizzontale.Create("Palo corto in sabbia", free)).GetXml("word/document.xml").ToString();
        e.That(xml.Contains("Confronto con Broms (verticale governante): Hu di Broms = 207,36 kN, meccanismo corto; rapporto Stratificato / Broms = 0,739.", StringComparison.Ordinal)
            && xml.Contains("Confronto con Broms (stessa verticale): Hu di Broms = 207,36 kN", StringComparison.Ordinal),
            "Broms accanto: confronto assente dalla relazione.");
        var plain = DocxWriter.Read(ReportOrizzontale.Create("Palo lungo", longPile)).GetXml("word/document.xml").ToString();
        e.That(!plain.Contains("Confronto con Broms", StringComparison.Ordinal), "Broms accanto: confronto nella relazione del palo lungo.");
        string dir = Path.Combine(output, "palo-orizzontale-broms"); Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "palo-corto-sabbia.docx"), ReportOrizzontale.Create("Palo corto in sabbia", free));
        return e.Count;
    }
}
