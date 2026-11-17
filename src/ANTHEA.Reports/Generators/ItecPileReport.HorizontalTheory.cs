using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using X.Core;

namespace Anthea.Reports;

internal sealed partial class ItecPileReport
{
    // Symbolic report equations only. Capacity and mechanism selection remain in GPC.
    void HorizontalTheory(JsonObject result)
    {
        var g = result["input"]!["generali"]!;
        bool fixedHead = g.S("vincolo") == "Impedita";
        bool stratified = result.S("metodo_calcolo") == PaloOrizzontale.StratifiedMethod;
        var surveys = result.Array("sondaggi");
        var soils = surveys.SelectMany(s => s.Array("diagramma_limite")).Select(s => s.S("tipologia")).ToHashSet();
        bool cohesive = soils.Contains("Coesivo"), granular = soils.Contains("Granulare");
        bool adapted = stratified || result.B("sperimentale") || Calcolo.ProfonditaTestaMostrata(result) is > 0 || (cohesive && granular);
        string head = fixedHead ? "testa incastrata (rotazione impedita)" : "testa libera (rotazione non impedita)";
        string soil = cohesive && granular ? "strati coesivi e non coesivi" : cohesive ? "terreno coesivo, in condizioni non drenate" : "terreno non coesivo, in condizioni drenate";
        Paragraph($"Il calcolo considera {head} e {soil}. " + (adapted
            ? HorizontalPileWording.Adaptation
            : "La capacità portante orizzontale è valutata con il metodo di Broms per terreno omogeneo."));
        Paragraph("D indica il diametro, L la lunghezza infissa, H la forza orizzontale e Mᵧ il momento resistente della sezione adottato nel calcolo. La coordinata z è positiva verso il basso dalla testa; d è la profondità della testa rispetto al piano campagna e zɡ = d + z. " +
            (fixedHead ? "Il vincolo e la forza sono applicati alla sezione di testa, con e = 0." : "L’eccentricità e è misurata dalla testa alla retta d’azione di H; il momento in testa vale M₀ = H e."));

        TheoryLabel("Reazione limite del terreno");
        if (cohesive)
        {
            Paragraph("Negli strati coesivi cᵤ è la resistenza al taglio non drenata. La zona priva di reazione si misura dal piano campagna e non riparte a ogni strato.");
            HEq("p_lim(z) = 0", "     per d + z < 1,5 D");
            HEq("p_lim(z) = 9 c_u D", "     per d + z ≥ 1,5 D");
        }
        if (granular)
        {
            Paragraph("Negli strati non coesivi φ′ è l’angolo di attrito efficace; σ′ᵥ comprende il peso di tutti gli strati sovrastanti e l’effetto della falda. La coesione efficace non contribuisce alla legge di reazione adottata.");
            HEq("K_p = ", HFrac("1 + sin φ′", "1 − sin φ′"), "     ;     p_lim(z) = 3 K_p D σ′_v(d + z)");
            HEq("σ′_v(d + z) = ", HIntegral("d + z", "γ′(s) ds"));
            Paragraph("Sopra falda si usa il peso unitario naturale; sotto falda il peso unitario immerso γ′ = γsat − γw. La reazione p_lim è espressa in kN/m.");
        }
        if (adapted) HorizontalIntegralMechanisms(result, fixedHead);
        else if (cohesive) HorizontalCohesiveMechanisms(fixedHead);
        else HorizontalGranularMechanisms(fixedHead);
        TheoryLabel("Scelta della capacità portante");
        Paragraph("Per ogni verticale si confrontano i soli meccanismi ammissibili: devono essere rispettati l’equilibrio, il momento resistente e la disponibilità della lunghezza necessaria al meccanismo. La capacità ultima è il minore dei candidati ammissibili.");
        HEq(fixedHead ? "H_u = min(H_corto; H_intermedio; H_lungo)" : "H_u = min(H_corto; H_lungo)");
        Paragraph("Riferimenti: B. B. Broms (1964), Lateral Resistance of Piles in Cohesive Soils, DOI 10.1061/JSFEAQ.0000611; Lateral Resistance of Piles in Cohesionless Soils, DOI 10.1061/JSFEAQ.0000614." +
            (adapted ? " L’adattamento al terreno stratificato segue l’approccio di G. Pacini implementato nel programma." : ""));
    }

    void HorizontalCohesiveMechanisms(bool fixedHead)
    {
        TheoryLabel("Formule di Broms — terreno coesivo omogeneo");
        Paragraph("La profondità di taglio nullo è zf; f è la lunghezza reagente sopra tale sezione. Per il meccanismo corto e quello intermedio g è il tratto inferiore fino al piede.");
        HEq("f = ", HFrac("H", "9 c_u D"), "     ;     z_f = 1,5 D + f     ;     g = L − 1,5 D − f");
        if (!fixedHead)
        {
            TheoryLabel("Testa libera — palo corto");
            HEq("H (e + 1,5 D + ", HFrac("f", "2"), ") = 2,25 c_u D g^2");
            Paragraph("Il momento massimo deve risultare non superiore a Mᵧ; non si forma una cerniera plastica.");
            TheoryLabel("Testa libera — palo lungo");
            HEq("M_y = H (e + 1,5 D + ", HFrac("f", "2"), ")");
        }
        else
        {
            TheoryLabel("Testa incastrata — palo corto");
            HEq("H = 9 c_u D (L − 1,5 D)");
            HEq("M_0 = −H (", HFrac("L", "2"), " + 0,75 D)     ;     |M_0| ≤ M_y");
            TheoryLabel("Testa incastrata — palo intermedio");
            HEq("H (1,5 D + ", HFrac("f", "2"), ") − M_y = 2,25 c_u D g^2");
            Paragraph("Si forma una cerniera in testa, con M₀ = −Mᵧ; il momento positivo lungo il palo non deve superare Mᵧ.");
            TheoryLabel("Testa incastrata — palo lungo");
            HEq("2 M_y = H (1,5 D + ", HFrac("f", "2"), ")");
        }
        Paragraph("Nel meccanismo lungo il tratto sotto zf deve consentire la chiusura delle reazioni; la profondità mobilitata t deve rientrare nella lunghezza infissa.");
        HEq("t = 1,5 D + f + ", HSqrt(HFrac("M_y", "2,25 c_u D")), " ≤ L");
    }

    void HorizontalGranularMechanisms(bool fixedHead)
    {
        TheoryLabel("Formule di Broms — terreno non coesivo omogeneo");
        Paragraph("Con testa al piano campagna e peso unitario efficace γ′ costante, la reazione limite cresce linearmente con la profondità. zf individua la sezione di taglio nullo.");
        HEq("p_lim(z) = 3 K_p γ′ D z     ;     z_f = ", HSqrt(HFrac("2 H", "3 K_p γ′ D")));
        if (!fixedHead)
        {
            TheoryLabel("Testa libera — palo corto");
            HEq("H = ", HFrac("K_p γ′ D L^3", "2 (L + e)"));
            HEq("M_max = H (e + ", HFrac("2", "3"), " z_f) ≤ M_y");
            TheoryLabel("Testa libera — palo lungo");
            HEq("M_y = H (e + ", HFrac("2", "3"), " z_f)");
        }
        else
        {
            TheoryLabel("Testa incastrata — palo corto");
            HEq("H = ", HFrac("3", "2"), " K_p γ′ D L^2     ;     M_0 = −", HFrac("2", "3"), " H L");
            Paragraph("Il meccanismo è ammissibile se |M₀| ≤ Mᵧ.");
            TheoryLabel("Testa incastrata — palo intermedio");
            HEq("H = ", HFrac(new object[] { "M_y + ", HFrac("K_p γ′ D L^3", "2") }, "L"));
            Paragraph("Si forma una cerniera in testa, con M₀ = −Mᵧ; il momento positivo lungo il palo non deve superare Mᵧ.");
            TheoryLabel("Testa incastrata — palo lungo");
            HEq("2 M_y = ", HFrac("2", "3"), " H z_f");
        }
        Paragraph("La formulazione di Broms per il terreno granulare rappresenta la reazione inferiore mediante una risultante concentrata. I rami con cerniera interna sono ammessi solo se la profondità mobilitata rientra nella lunghezza del palo.");
    }

    void HorizontalIntegralMechanisms(JsonObject result, bool fixedHead)
    {
        TheoryLabel("Formulazione per terreno stratificato");
        Paragraph("Le proprietà di ciascuno strato definiscono il diagramma p_lim. La risultante Q e i momenti S e A si ottengono integrando tale diagramma, senza mediare i parametri né sommare capacità di strati isolati.");
        HEq("Q(z) = ", HIntegral("z", "p_lim(s) ds"), "     ;     S(z) = ", HIntegral("z", "s p_lim(s) ds"));
        HEq("A(z) = z Q(z) − S(z)     ;     Q(z_f) = H");
        bool distributed = result.Array("sondaggi").Any(s => s.S("chiusura_equilibrio") == "Distribuita");
        bool concentrated = result.Array("sondaggi").Any(s => s.S("chiusura_equilibrio") != "Distribuita");
        if (distributed)
        {
            Paragraph("Per la chiusura distribuita, zr è la profondità di inversione delle reazioni e t il termine del tratto mobilitato. Le reazioni opposte sotto zf hanno risultante nulla e formano la coppia C.");
            HEq("Q(z_r) = ", HFrac("Q(z_f) + Q(t)", "2"));
            HEq("C(z_f, t) = S(t) + S(z_f) − 2 S(z_r)");
            Paragraph("Questa chiusura riproduce il caso coesivo omogeneo. Nei terreni non coesivi i meccanismi corto e intermedio differiscono dalla soluzione di Broms con forza concentrata al piede. La chiusura inferiore è un’ipotesi di equilibrio limite e non descrive l’interazione elastoplastica terreno–palo.");
        }
        if (concentrated) Paragraph("Per le verticali granulari calcolate con Broms, la chiusura inferiore usa una risultante concentrata al piede. Le relazioni seguenti utilizzano gli integrali del diagramma effettivo, comprensivo di stratigrafia e falda.");
        if (!fixedHead)
        {
            TheoryLabel("Testa libera — palo corto");
            if (distributed) HEq("H e + S(z_f) = C(z_f, L)");
            if (concentrated) HEq("H (L + e) = A(L)");
            Paragraph("Il momento massimo deve risultare non superiore a Mᵧ.");
            TheoryLabel("Testa libera — palo lungo");
            HEq("H e + S(z_f) = M_y");
        }
        else
        {
            TheoryLabel("Testa incastrata — palo corto");
            HEq("H = Q(L)     ;     M_0 = −S(L)     ;     S(L) ≤ M_y");
            TheoryLabel("Testa incastrata — palo intermedio");
            if (distributed) HEq("−M_y + S(z_f) = C(z_f, L)");
            if (concentrated) HEq("H L = M_y + A(L)");
            Paragraph("Il ramo intermedio si attiva dopo il raggiungimento di Mᵧ in testa; il momento positivo non deve superare Mᵧ.");
            TheoryLabel("Testa incastrata — palo lungo");
            HEq("S(z_f) = 2 M_y     ;     M_0 = −M_y");
        }
        if (distributed) HEq("C(z_f, t) = M_y     ;     z_f < t ≤ L");
        Paragraph("Il ramo lungo richiede una cerniera interna e una lunghezza sufficiente a chiudere l’equilibrio sotto tale sezione. Le condizioni sono controllate per ogni verticale.");
    }

    void HorizontalDiagramCriteria()
    {
        Paragraph("Le tensioni verticali efficaci si ottengono sottraendo la pressione idrostatica u alla tensione totale σᵥ. La pressione laterale equivalente q_lim [kPa] è distinta dalla reazione lineare p_lim [kN/m].");
        HEq("σ′_v = σ_v − u     ;     q_lim(z) = ", HFrac("p_lim(z)", "D"));
        Paragraph("Nei diagrammi p è la reazione con segno, positiva quando contrasta H. Con z misurata dalla testa verso il basso, taglio V e momento M derivano dall’equilibrio del tratto superiore:");
        HEq("V(z) = H − ", HIntegral("z", "p(s) ds"));
        HEq("M(z) = M_0 + H z − ", HIntegral("z", "(z − s) p(s) ds"));
        Paragraph("Le curve sono riferite alla capacità ultima Hᵤ. I coefficienti ξ e γR riducono la capacità globale, senza modificare le tensioni locali. La profondità zf di taglio nullo è distinta dalla profondità zr di inversione delle reazioni. Il tratto sotto la cerniera interna è un completamento idealizzato, rappresentato tratteggiato; le risultanti concentrate, quando presenti, sono indicate separatamente. Il modello di capacità portante non determina spostamenti e rotazioni.");
    }

    void TheoryLabel(string text)
    {
        var p = P(text);
        p.Element(W + "pPr")!.Add(new XElement(W + "keepNext"));
        p.Element(W + "r")!.AddFirst(new XElement(W + "rPr", new XElement(W + "b")));
        body.Add(p);
    }

    static readonly XNamespace HM = "http://schemas.openxmlformats.org/officeDocument/2006/math";
    static readonly Regex HorizontalMathScripts = new(@"([A-Za-zΑ-ω]+[′]?)(?:_([A-Za-z0-9]+)|\^([0-9]+))", RegexOptions.CultureInvariant);
    static IEnumerable<XElement> HNodes(object item)
    {
        if (item is XElement element) { yield return new XElement(element); yield break; }
        if (item is object[] items) { foreach (var child in items.SelectMany(HNodes)) yield return child; yield break; }
        string text = (string)item;
        int offset = 0;
        foreach (Match match in HorizontalMathScripts.Matches(text))
        {
            if (match.Index > offset) yield return HRun(text[offset..match.Index]);
            bool sub = match.Groups[2].Success;
            yield return new XElement(HM + (sub ? "sSub" : "sSup"), new XElement(HM + "e", HRun(match.Groups[1].Value)), new XElement(HM + (sub ? "sub" : "sup"), HRun(match.Groups[sub ? 2 : 3].Value)));
            offset = match.Index + match.Length;
        }
        if (offset < text.Length) yield return HRun(text[offset..]);
    }
    static XElement HRun(string text) => new(HM + "r", new XElement(HM + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text));
    static XElement HFrac(object numerator, object denominator) => new(HM + "f", new XElement(HM + "num", HNodes(numerator)), new XElement(HM + "den", HNodes(denominator)));
    static XElement HSqrt(object value) => new(HM + "rad", new XElement(HM + "radPr", new XElement(HM + "degHide", new XAttribute(HM + "val", 1))), new XElement(HM + "deg"), new XElement(HM + "e", HNodes(value)));
    static XElement HIntegral(string upper, string integrand) => new(HM + "nary", new XElement(HM + "naryPr", new XElement(HM + "chr", new XAttribute(HM + "val", "∫")), new XElement(HM + "limLoc", new XAttribute(HM + "val", "subSup"))), new XElement(HM + "sub", HRun("0")), new XElement(HM + "sup", HNodes(upper)), new XElement(HM + "e", HNodes(integrand)));
    void HEq(params object[] parts)
    {
        const int before = 160, after = 200;
        var p = P(""); p.Elements(W + "r").Remove();
        p.Element(W + "pPr")!.Add(new XElement(W + "spacing", new XAttribute(W + "before", before), new XAttribute(W + "after", after)));
        p.Add(new XElement(HM + "oMathPara", new XElement(HM + "oMathParaPr", new XElement(HM + "jc", new XAttribute(HM + "val", "left"))), new XElement(HM + "oMath", parts.SelectMany(HNodes))));
        body.Add(CenteredEquation(p));
    }
}
