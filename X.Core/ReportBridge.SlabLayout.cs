namespace X.Core;

public static partial class ReportBridge
{
    private static void SlabLayoutReport(Document doc, BridgeResult result)
    {
        var d = result.Input; var slab = BridgeSection.SlabLayout(d);
        string F(double v) => EngineeringFormat.Number(v);
        if (slab.HasPredalle)
        {
            doc.P(BridgeSection.PredalleScope);
            doc.P($"Spessore totale soletta: {F(slab.Height)} mm; predalle: {F(slab.PredalleThickness)} mm; getto sovrastante: {F(slab.Height - slab.PredalleThickness)} mm.");
        }
        if (d.B("rebars_bottom")) doc.P($"Ferri inferiori: riferimento = {slab.BottomReference}; distanza riferimento–asse = {F(d.D("cover_bottom"))} mm; quota effettiva y = {F(slab.BottomAxisY)} mm dall'intradosso soletta.");
        if (d.B("rebars_top")) doc.P($"Ferri superiori: estradosso soletta–asse = {F(d.D("cover_top"))} mm; quota effettiva y = {F(slab.TopAxisY)} mm.");
    }
}
