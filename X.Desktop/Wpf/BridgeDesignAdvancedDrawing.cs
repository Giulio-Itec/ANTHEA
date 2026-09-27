using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class BridgeDesignDrawing
{
    private static void DrawUpperStructure(DrawingContext dc, BridgeConcept.Result r, BridgeConcept.AdvancedGeometry a, double start, double xs, double deckY, double ground)
    {
        if (a.Members.Length == 0) return;
        double maxZ = Math.Max(a.TowerHeight, a.Members.Max(m => Math.Max(m.Z1, m.Z2))), zs = 118 / Math.Max(1, maxZ);
        foreach (var m in a.Members)
        {
            bool cable = m.Kind is "Strallo" or "Pendino" or "Cavo principale" or "Cavo di riva";
            var pen = new Pen(cable ? Accent : Steel, m.Kind == "Cavo principale" ? 2.5 : cable ? .9 : 3.5);
            dc.DrawLine(pen, new(start + m.X1 * xs, deckY - m.Z1 * zs), new(start + m.X2 * xs, deckY - m.Z2 * zs));
        }
        if (a.TowerHeight > 0) foreach (var s in r.Supports.Where(s => s.Type.StartsWith("Antenna")))
        {
            double x = start + s.X * xs; Box(dc, x - 4, deckY - a.TowerHeight * zs, 8, a.TowerHeight * zs, Concrete);
            Text(dc, "h = " + F(a.TowerHeight) + " m", x + 9, deckY - a.TowerHeight * zs + 10, 11, Accent, true);
        }
        if (a.AnchorVolume > 0) foreach (double x in new[] { start, start + r.Length * xs })
        { Box(dc, x - 18, deckY + 18, 36, 22, Concrete); dc.DrawLine(new Pen(Accent, 2), new(x, deckY), new(x, deckY + 25)); }
        if (r.Family.Id is "tied_arch" or "truss" or "suspension") Text(dc, (r.Family.Id == "truss" ? "h reticolare = " : "f = ") + F(a.Rise) + " m", 435, 43, 12, Accent, true);
    }
    private static void DrawExtendedSection(DrawingContext dc, BridgeConcept.Result r, JsonNode i)
    {
        var a = r.Advanced!; double left = 100, width = 800, xs = width / r.Width, top = 150;
        double ys = Math.Min(115, 150 / r.Depth), d = r.Depth * ys;
        Dimension(dc, left, left + width, 106, "W = " + F(r.Width, "0.00") + " m");
        if (r.Family.Id == "filler_beam")
        {
            Box(dc, left, top, width, d, Concrete); double cover = i.D("embedded_cover") * ys;
            for (int k = 0; k < r.Girders; k++)
            {
                double x = left + (k + .5) * width / r.Girders, bf = i.D("flange_width") * xs, tf = Math.Max(2, i.D("flange_mm") / 1000 * ys), tw = Math.Max(1.5, i.D("web_mm") / 1000 * xs);
                Box(dc, x - bf / 2, top + cover, bf, tf, Steel); Box(dc, x - bf / 2, top + d - cover - tf, bf, tf, Steel);
                Box(dc, x - tw / 2, top + cover + tf, tw, d - 2 * cover - 2 * tf, Steel);
            }
            Text(dc, "Travi a I inglobate · cls netto del volume dei profili · c = " + F(i.D("embedded_cover") * 1000, "0") + " mm", 100, 330, 12, Line);
        }
        else if (a.Orthotropic)
        {
            double plate = Math.Max(3, i.D("deck_plate_mm") / 1000 * ys); Box(dc, left, top, width, plate, Steel);
            int ribs = (int)a.Dimensions.Single(t => t.Symbol == "n_r").Value;
            double rh = i.D("rib_height") * ys, rt = Math.Max(1.5, i.D("rib_mm") / 1000 * xs), ru = i.D("rib_top") * xs, rb = i.D("rib_bottom") * xs;
            for (int k = 0; k < ribs; k++)
            {
                double x = left + (k + .5) * width / ribs; var pen = new Pen(Steel, rt);
                dc.DrawLine(pen, new(x - ru / 2, top + plate), new(x - rb / 2, top + plate + rh));
                dc.DrawLine(pen, new(x - rb / 2, top + plate + rh), new(x + rb / 2, top + plate + rh));
                dc.DrawLine(pen, new(x + rb / 2, top + plate + rh), new(x + ru / 2, top + plate));
            }
            for (int k = 0; k < r.Girders; k++)
            {
                double x = left + (k + .5) * width / r.Girders, b = width * .55 / r.Girders, tf = Math.Max(3, i.D("flange_mm") / 1000 * ys), tw = Math.Max(2, i.D("web_mm") / 1000 * xs);
                Box(dc, x - b / 2, top + d - tf, b, tf, Steel); Box(dc, x - b / 2, top + plate, tw, d - plate - tf, Steel); Box(dc, x + b / 2 - tw, top + plate, tw, d - plate - tf, Steel);
            }
            Text(dc, $"Piastra {F(i.D("deck_plate_mm"), "0")} mm · {ribs} canalette · t nervature {F(i.D("rib_mm"), "0")} mm · cassoni {r.Girders}", 100, 330, 12, Line);
        }
        else
        {
            double slab = r.Slab * ys; Box(dc, left, top, width, slab, Concrete);
            for (int k = 0; k < r.Girders; k++)
            {
                double x = left + (k + .5) * width / r.Girders, b = i.D("flange_width") * xs, tf = Math.Max(3, i.D("flange_mm") / 1000 * ys);
                Box(dc, x - b / 2, top + slab, b, tf, Steel); Box(dc, x - b / 2, top + d - tf, b, tf, Steel); Box(dc, x - 2, top + slab + tf, 4, d - slab - 2 * tf, Steel);
            }
            Text(dc, $"Soletta {F(r.Slab * 1000, "0")} mm · {r.Girders} travi secondarie · bf {F(i.D("flange_width") * 1000, "0")} mm · tf {F(i.D("flange_mm"), "0")} mm", 100, 330, 12, Line);
        }
        if (a.Members.Length > 0)
        {
            foreach (double x in new[] { left + 8, left + width - 8 })
            {
                dc.DrawLine(new Pen(Accent, 2), new(x, top), new(x, 60));
                dc.DrawEllipse(Steel, new Pen(Ink, 1), new(x, 60), 6, 6);
            }
            Text(dc, "Due piani di archi / aste / cavi · schema trasversale", 290, 48, 12, Accent);
        }
        Text(dc, "d = " + F(r.Depth, "0.00") + " m", 790, 310, 11, Accent, true);
        Text(dc, r.Family.Name, 100, 367, 17, Ink, true);
        Text(dc, "Quote schematiche · aree di aste / cavi e dimensioni adottate nella scheda Sezioni e quote", 100, 393, 11, Line);
    }
}
