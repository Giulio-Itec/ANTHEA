using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

/// <summary>Qualitative, resolution-independent explanations; never used to calculate a result.</summary>
internal sealed class BridgeMethodSketch(int kind) : FrameworkElement
{
    internal int Kind => kind;
    protected override Size MeasureOverride(Size availableSize) => new(double.IsFinite(availableSize.Width) ? availableSize.Width : 900, 250);
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Ui.Brush("#F5F8FC"), null, new Rect(RenderSize));
        dc.PushTransform(new ScaleTransform(ActualWidth / 900, ActualHeight / 250));
        var blue = Ui.Blue; var muted = Ui.Muted; var orange = Ui.Brush("#C88624"); var teal = Ui.Brush("#127A83");
        void Text(string s, double x, double y, Brush? color = null, double size = 14) => dc.DrawText(new FormattedText(s, CultureInfo.GetCultureInfo("it-IT"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, color ?? Ui.Navy, 1), new Point(x, y));
        void Line(double x1, double y1, double x2, double y2, Brush? color = null, bool dash = false) => dc.DrawLine(new Pen(color ?? muted, 1.3) { DashStyle = dash ? DashStyles.Dash : DashStyles.Solid }, new(x1, y1), new(x2, y2));
        void Arrow(double x1, double y1, double x2, double y2)
        {
            Line(x1, y1, x2, y2); var direction = new Vector(x2 - x1, y2 - y1); direction.Normalize(); var normal = new Vector(-direction.Y, direction.X);
            dc.DrawLine(new Pen(muted, 1.3), new(x2, y2), new Point(x2, y2) - direction * 7 + normal * 4);
            dc.DrawLine(new Pen(muted, 1.3), new(x2, y2), new Point(x2, y2) - direction * 7 - normal * 4);
        }
        void Profile(double x, bool slab = true, bool effective = false)
        {
            dc.PushOpacity(slab ? 1 : .2); dc.DrawRectangle(Ui.Brush("#CBD6E2"), null, new(x - 75, 50, 150, 23)); dc.Pop();
            if (slab) foreach (double dx in new[] { -52d, -26, 0, 26, 52 }) dc.DrawEllipse(orange, null, new(x + dx, 59), 2.5, 2.5);
            dc.DrawRectangle(blue, null, new(x - 38, 74, 76, 7)); dc.DrawRectangle(blue, null, new(x - 48, 188, 96, 8));
            dc.DrawRectangle(blue, null, new(x - 4, 81, 8, 28)); dc.DrawRectangle(blue, null, new(x - 4, 151, 8, 37));
            dc.PushOpacity(effective ? .28 : 1); dc.DrawRectangle(blue, null, new(x - 4, 109, 8, 42)); dc.Pop();
            if (effective) dc.DrawRectangle(null, new Pen(orange, 1.5) { DashStyle = DashStyles.Dash }, new(x - 6, 109, 12, 42));
        }
        void Target(double x, double y, string label)
        {
            dc.DrawEllipse(Brushes.White, new Pen(teal, 2), new(x, y), 6, 6); Line(x - 11, y, x + 11, y, teal); Line(x, y - 11, x, y + 11, teal); Text(label, x + 17, y - 11, teal);
        }
        if (kind == 0)
        {
            Profile(140, false); Profile(425); Profile(710);
            Text("1 · Solo acciaio", 75, 12); Text("2 · Composta, φ₂", 355, 12); Text("3 · Composta, φ₃", 640, 12);
            Text("Δσ₁", 120, 207, blue, 18); Text("+", 265, 115, muted, 24); Text("Δσ₂", 405, 207, orange, 18); Text("+", 555, 115, muted, 24); Text("Δσ₃", 690, 207, teal, 18);
            Text("Situazione 3: σ = Δσ₁ + Δσ₂ + Δσ₃  ·  esempio con tre fasi attive", 240, 230, muted, 12);
        }
        else if (kind == 1)
        {
            Arrow(75, 205, 410, 205); Arrow(75, 205, 75, 20); Text("φ", 420, 195); Text("n / n₀", 20, 13);
            for (int i = 0; i <= 4; i++) { Text(i.ToString(), 70 + i * 75, 211, muted, 12); if (i > 0) Line(75 + i * 75, 205, 75 + i * 75, 45, Ui.Brush("#E2E8EF")); }
            Text("1", 52, 167, muted, 12); Text("3", 52, 108, muted, 12); Text("5", 52, 48, muted, 12);
            Line(75, 175, 375, 43, blue); dc.DrawEllipse(blue, null, new(75, 175), 4, 4);
            Text("n₀ = Ea / Ecm", 485, 37, null, 20); Text("n = n₀ (1 + ψL · φ)", 485, 82, blue, 20);
            Text("φ = (n / n₀ − 1) / ψL", 485, 127, null, 20); Text("Esempio: ψL = 1,1 · a φ = 0 si ha n = n₀", 485, 186, muted, 14);
        }
        else if (kind == 2)
        {
            Profile(160, true, true); Profile(570, true, true);
            Text("Lordo · punto N fisso", 70, 12); Text("Efficace · punto N aggiornato", 465, 12);
            Line(90, 116, 240, 116, muted, true); Line(90, 151, 240, 151, orange, true);
            Target(160, 116, "N a G₀"); Text("G efficace", 207, 147, orange, 13); Arrow(104, 118, 104, 149); Text("e", 84, 127, muted);
            Line(500, 116, 660, 116, muted, true); Line(500, 151, 660, 151, orange, true); Text("G₀", 671, 108, muted, 13); Target(570, 151, "N a G efficace");
            Text("Mx,G = Mx + N · (yG − yN)", 80, 218, null, 16); Text("yN = yG corrente → Mx,G = Mx", 480, 218, null, 16);
        }
        else
        {
            Profile(95); Profile(280, true, true); Arrow(175, 130, 218, 130);
            Text("Lorda", 70, 13); Text("Efficace", 246, 13); Text("Tratto escluso", 225, 213, orange, 13);
            var steps = new[] { "Calcolo dei contributi", "Somma delle tensioni", "Larghezze efficaci", "Confronto e rilassamento" };
            for (int i = 0; i < 4; i++)
            {
                double y = 15 + i * 51; dc.DrawRoundedRectangle(Brushes.White, new Pen(Ui.Brush("#CFDAE5"), 1), new(455, y, 245, 35), 4, 4); Text(steps[i], 467, y + 7);
                if (i < 3) Arrow(577, y + 36, 577, y + 49);
            }
            Line(701, 185, 764, 185); Line(764, 185, 764, 32); Arrow(764, 32, 702, 32); Text("ripeti", 779, 98, muted, 13);
            Text("Convergenza → tensioni e proprietà finali", 455, 225, blue, 14);
        }
        dc.Pop();
    }
}
