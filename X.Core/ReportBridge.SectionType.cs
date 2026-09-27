namespace X.Core;

/// <summary>The H with an inclined web and the box girder in the report: hypotheses and real plates</summary>
public static partial class ReportBridge
{
    /// <summary>The paragraph of the type of section (empty for the H)</summary>
    /// <param name="g">The geometry</param>
    /// <param name="torsion">True: the box with the torsion checks</param>
    /// <returns>The paragraph</returns>
    public static string SectionTypeParagraph(BridgeGeometry g, bool torsion = false)
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
            (g.SectionType != BridgeSteelSectionType.Box ? "" : torsion
                ? " Il fondo è una lamiera interna tra le anime (kσ interno) con gli sbalzi esterni; torsione della cella chiusa, distorsione e diaframmi sono verificati nel capitolo del taglio; irrigidimenti longitudinali del fondo non considerati."
                : " Il fondo è una lamiera interna tra le anime (kσ interno) con gli sbalzi esterni; torsione e distorsione della cella chiusa, diaframmi e irrigidimenti longitudinali del fondo non sono verificati.");
    }

    /// <summary>The torsion of the box in one situation: flows of the phases, distortion, values and checks</summary>
    private static void TorsionSection(Document doc, BridgeTorsionResult t, Func<double?, string> f)
    {
        doc.Sub("Cassoncino · torsione, distorsione e diaframmi");
        doc.P($"q anime e fondo = {f(t.WebFlow)} kN/m; q soletta e connessione = {f(t.SlabFlow)} kN/m; q controvento superiore = {f(t.BracingFlow)} kN/m.");
        doc.Table(["Fase", "Sezione", "ΔT [kNm]", "A0 [m²]", "J [m⁴]", "q [kN/m]", "Cella"], t.Flows.Select(x => new[] {
            x.Phase, x.Kind, f(x.TorqueKNm), f(x.CellArea / 1e6), f(x.TorsionConstant / 1e12), f(x.Flow), x.Closed ? "Chiusa" : "Aperta" }), [1.8, 1.3, .8, .8, .9, .8, .8]);
        if (t.Distortion is { } d)
            doc.P($"Distorsione: I_Dw = {f(d.WarpingInertia / 1e18)} m⁶; K = {f(d.FrameStiffness / 1000)} kN·m/m; K_D = {f(d.DiaphragmStiffness / 1e9)} MN·m; " +
                $"{d.Diaphragms} diaframmi intermedi. σdw al fondo = {f(d.WarpingStressBottom)} MPa, pari al {f(d.BendingRatio * 100)}% della σ di flessione: " +
                (d.Included ? "oltre il 10%, sommata nelle verifiche del fondo (EN 1993-2 §6.2.7(3))." : "entro il 10%, trascurata nelle verifiche del fondo (EN 1993-2 §6.2.7(3)); i nodi la includono."));
        doc.Table(["Parametro", "Valore", "Unità"], t.Details.Select(v => new[] { v.Name, f(v.Value), v.Unit }), [3.2, 1.5, .7]);
        doc.Table(["Controllo", "Domanda", "Limite", "η", "Esito"], t.Checks.Select(c => new[] { c.Name, f(c.Demand) + " " + c.Unit, f(c.Resistance) + " " + c.Unit, f(c.Ratio), c.Status }), [2.5, 1.2, 1.2, .6, 1.1]);
        foreach (var c in t.Checks.Where(c => c.Note.Length > 0)) doc.P(c.Name + ": " + c.Note + ".");
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
