namespace X.Core;

/// <summary>The H with an inclined web and the box girder in the report: hypotheses and real plates</summary>
public static partial class ReportBridge
{
    /// <summary>The paragraph of the type of section (empty for the H)</summary>
    /// <param name="g">The geometry</param>
    /// <returns>The paragraph</returns>
    public static string SectionTypeParagraph(BridgeGeometry g)
    {
        if (g.SectionType == BridgeSteelSectionType.H) return "";
        string angle = EngineeringFormat.Number(Math.Abs(g.WebAngle) * 180 / Math.PI);
        string kind = g.SectionType == BridgeSteelSectionType.Box
            ? $"Cassoncino in acciaio aperto superiormente, chiuso dalla soletta: due anime simmetriche inclinate di {angle}° dalla verticale, due piattabande superiori e un fondo. "
            : $"Sezione ad H con anima inclinata di {angle}° dalla verticale, piattabande centrate sulle estremità dell'anima. ";
        return kind + "Flessione retta attorno all'asse orizzontale: la sezione è vincolata lateralmente da soletta e controventi. Nell'analisi N–Mx le anime sono verticali " +
            "equivalenti di spessore complessivo n·tw/cos α e le piattabande superiori hanno la larghezza complessiva (valori della tabella precedente): area, baricentro e " +
            "inerzia rispetto all'asse orizzontale sono quelli esatti. Instabilità locale, taglio (V/(n cos α) nel piano di ciascuna lamiera), irrigidimenti, saldature e pioli " +
            "sono verificati sulle lamiere reali della tabella seguente." +
            (g.SectionType == BridgeSteelSectionType.Box ? " Il fondo è una lamiera interna tra le anime (kσ interno) con gli sbalzi esterni; torsione e distorsione della cella chiusa, diaframmi e irrigidimenti longitudinali del fondo non sono verificati." : "");
    }

    /// <summary>The real plates of the H with an inclined web and of the box girder</summary>
    /// <param name="g">The geometry</param>
    /// <returns>The rows: plate, width or length, thickness</returns>
    public static IEnumerable<string[]> RealPlates(BridgeGeometry g)
    {
        string F(double x) => EngineeringFormat.Number(x);
        bool box = g.SectionType == BridgeSteelSectionType.Box;
        yield return [box ? "Piattabande superiori (ciascuna, 2)" : "Piattabanda superiore", F(g.TopFlangeWidth), F(g.TopThickness)];
        yield return [box ? "Anime (ciascuna, 2) · lunghezza nel piano" : "Anima · lunghezza nel piano", F(g.PlateLength), F(g.PlateThickness)];
        yield return ["Anima · altezza verticale e larghezza orizzontale tw/cos α", F(g.WebHeight), F(g.WebHorizontalThickness)];
        if (box)
        {
            yield return ["Interasse anime · sommità / piede", F(g.WebSpacingTop) + " / " + F(g.WebSpacingBottom), "—"];
            yield return ["Fondo · parte interna tra le anime", F(g.BottomInternalWidth), F(g.Bottom1Thickness)];
            yield return ["Fondo · sbalzo oltre ciascuna anima", F(g.BottomOutstandWidth), F(g.Bottom1Thickness)];
        }
        yield return ["Piattabanda inferiore", F(g.Bottom1Width), F(g.Bottom1Thickness)];
    }
}
