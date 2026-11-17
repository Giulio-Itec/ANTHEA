using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using Anthea.Application.Reporting;
using X.Core;

namespace X.Desktop;

/// <summary>Report-only geometry and stratigraphy; all characteristics come from the calculated input.</summary>
internal static class HorizontalPileReportFigures
{
    internal static IReadOnlyList<ReportFigure> Figures(JsonObject result, bool brief)
    {
        var figures = PileReportDiagrams.Figures(result, brief).ToList();
        if (brief || result.S("tipo_risultato") == "palo_elastico") return figures;
        var input = result["input"]!.AsObject();
        bool micro = input.S("tipo_sezione") == "CHS";
        string element = micro ? "Micropalo" : "Palo";
        figures.Insert(0, new(element + ": geometria, vincolo e caratteristiche adottate", new HorizontalPileGeometry(result).ReportPng(640, 440), "geometria_palo"));
        for (int survey = 0; survey < input.Array("stratigrafie").Count; survey++)
        {
            // Horizontal micropiles use the same geotechnical fields as piles, not the injection fields of vertical micropiles.
            var profile = new StratigraphyDrawing { Data = input, SelectedIndex = survey, ShowAll = false, ElementLabel = element };
            figures.Add(new($"Profilo stratigrafico {survey + 1}: terreni, falda e posizione del {element.ToLowerInvariant()}. Quote dal piano campagna [m].",
                profile.ReportPng(), "stratigrafia", survey + 1));
        }
        return figures;
    }
}

internal sealed class HorizontalPileGeometry(JsonObject result) : DrawingView
{
    const double Left = 22, Top = 85, Bottom = 345, Axis = 168, PileWidth = 34, GroundEnd = 295;
    const double InfoLeft = 330, InfoWidth = 292, TextSize = 14, RowPitch = 25, DimensionX = 250;
    const double Half = .5, DiameterLabelOffset = 47, DetailRadius = 41, DetailCenterX = 374, DetailCenterY = 335;
    const double MmPerMetre = 1000, ArrowSize = 7, SmallGap = 5, LabelGap = 20, Stroke = 1.3;
    static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    static double Number(JsonNode? node, string key) => J.Number(node?[key]) ?? 0;
    static string Value(JsonNode? node, string key, string format = "0.##") => J.Number(node?[key]) is double n ? n.ToString(format, Italian) : "—";

    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(size));
        var input = result["input"]!; var g = input["generali"]!;
        double length = Number(g, "lunghezza"), diameter = Number(g, "diametro"), head = Number(g, "profondita_testa"), eccentricity = Number(g, "eccentricita");
        if (length <= 0 || diameter <= 0) return;
        bool fixedHead = g.S("vincolo") == "Impedita", micro = input.S("tipo_sezione") == "CHS";
        string element = micro ? "Micropalo" : "Palo";
        Text(dc, element + " · geometria e caratteristiche", Left, 12, 17, Ui.Navy, size.Width - Left, true);
        // Only drawing coordinates: eccentricity is measured from the head, d from ground level.
        double aboveGround = Math.Max(0, eccentricity - head), scale = (Bottom - Top) / (aboveGround + head + length);
        double groundY = Top + aboveGround * scale, headY = groundY + head * scale, toeY = headY + length * scale, forceY = headY - eccentricity * scale;
        var outline = new Pen(Ui.Navy, Stroke);
        dc.DrawRectangle(Ui.Brush("#F2F2F2"), null, new Rect(Left, groundY, GroundEnd - Left, toeY - groundY));
        dc.DrawLine(new Pen(Ui.Brush("#697D58"), Stroke), new(Left, groundY), new(GroundEnd, groundY));
        Text(dc, "Piano campagna", Left, groundY + SmallGap, 12, Ui.Navy);
        dc.DrawRectangle(Ui.Brush("#C4C9CE"), outline, new Rect(Axis - PileWidth * Half, headY, PileWidth, toeY - headY));
        if (forceY < headY) dc.DrawLine(new Pen(Ui.Navy, Stroke) { DashStyle = DashStyles.Dash }, new(Axis, forceY), new(Axis, headY));
        if (fixedHead)
        {
            const double capHalfWidth = 29, hatchPitch = 8, hatchDepth = 9;
            dc.DrawLine(new Pen(Ui.Navy, 3), new(Axis - capHalfWidth, headY), new(Axis + capHalfWidth, headY));
            for (double x = Axis - capHalfWidth; x < Axis + capHalfWidth; x += hatchPitch)
                dc.DrawLine(outline, new(x, headY), new(x - SmallGap, headY - hatchDepth));
        }
        const double forceStart = 58, forceLabelY = 46;
        double force = Number(g, "azione_orizzontale");
        Arrow(dc, force >= 0 ? new(forceStart, forceY) : new(Axis, forceY), force >= 0 ? new(Axis, forceY) : new(forceStart, forceY), Ui.Brush("#125DA8"));
        Text(dc, "H = " + Value(g, "azione_orizzontale") + " kN", forceStart, forceLabelY, TextSize, Ui.Brush("#125DA8"));
        Dimension(dc, DimensionX, headY, toeY, "L = " + Value(g, "lunghezza") + " m");
        if (eccentricity > 0) Dimension(dc, Axis + PileWidth, forceY, headY, "e");
        if (head > 0) Dimension(dc, Left + LabelGap, groundY, headY, "d");
        const double diameterY = 365, diameterTextY = 375;
        dc.DrawLine(outline, new(Axis - PileWidth * Half, diameterY), new(Axis + PileWidth * Half, diameterY));
        foreach (double x in new[] { Axis - PileWidth * Half, Axis + PileWidth * Half }) dc.DrawLine(outline, new(x, diameterY - SmallGap), new(x, diameterY + SmallGap));
        Text(dc, "D = " + Value(g, "diametro") + " m", Axis - DiameterLabelOffset, diameterTextY, TextSize, Ui.Navy);

        Text(dc, "DATI ADOTTATI", InfoLeft, 49, 14, Ui.Navy, InfoWidth, true);
        string[] rows = [
            "Diametro D: " + Value(g, "diametro") + " m",
            "Lunghezza infissa L: " + Value(g, "lunghezza") + " m",
            "Testa sotto p.c. d: " + head.ToString("0.##", Italian) + " m",
            "Eccentricità dalla testa e: " + Value(g, "eccentricita") + " m",
            "Vincolo: " + (fixedHead ? "rotazione impedita" : "rotazione libera"),
            "Azione assiale N: " + Value(g, "azione_assiale") + " kN",
            "Momento resistente Mᵧ: " + Value(result, "momento_resistente_knm") + " kNm",
            "Origine Mᵧ: " + g.S("origine_momento")];
        for (int i = 0; i < rows.Length; i++) Text(dc, rows[i], InfoLeft, Top - SmallGap + i * RowPitch, TextSize, Ui.Navy, InfoWidth);
        Point center = new(DetailCenterX, DetailCenterY);
        dc.DrawEllipse(Ui.Brush("#E5E7EB"), outline, center, DetailRadius, DetailRadius);
        if (micro && result["sezione"] is JsonObject section)
        {
            double tubeRadius = DetailRadius * Number(section, "diametro_mm") / (diameter * MmPerMetre);
            double innerRadius = DetailRadius * Number(section, "diametro_interno_mm") / (diameter * MmPerMetre);
            dc.DrawEllipse(Ui.Brush("#7B838B"), outline, center, tubeRadius, tubeRadius);
            if (innerRadius > 0) dc.DrawEllipse(Brushes.White, outline, center, innerRadius, innerRadius);
            Text(dc, "CHS " + Value(section, "diametro_mm") + " × " + Value(section, "spessore_mm") + " mm", InfoLeft + 100, DetailCenterY - LabelGap, TextSize, Ui.Navy, InfoWidth - 100, true);
            Text(dc, "Sezione resistente in acciaio", InfoLeft + 100, DetailCenterY + SmallGap, 12, Ui.Navy, InfoWidth - 100);
        }
        else Text(dc, "Sezione geometrica\nDiametro D", InfoLeft + 100, DetailCenterY - LabelGap, TextSize, Ui.Navy, InfoWidth - 100);
        Text(dc, "Schema geometrico; diametro del palo nel prospetto rappresentato fuori scala.", Left, 410, 12, Ui.Muted, size.Width - Left);
    }

    static void Dimension(DrawingContext dc, double x, double from, double to, string label)
    {
        var pen = new Pen(Ui.Muted, 1);
        dc.DrawLine(pen, new(x, from), new(x, to));
        dc.DrawLine(pen, new(x - SmallGap, from), new(x + SmallGap, from));
        dc.DrawLine(pen, new(x - SmallGap, to), new(x + SmallGap, to));
        if (to - from >= LabelGap) Text(dc, label, x + SmallGap, (from + to) * Half - ArrowSize, 13, Ui.Navy, 55);
    }
    static void Arrow(DrawingContext dc, Point from, Point to, Brush brush)
    {
        var vector = to - from; if (vector.Length <= 0) return; vector.Normalize();
        var normal = new Vector(-vector.Y, vector.X); var pen = new Pen(brush, 2);
        dc.DrawLine(pen, from, to);
        dc.DrawLine(pen, to, to - vector * ArrowSize + normal * ArrowSize * Half);
        dc.DrawLine(pen, to, to - vector * ArrowSize - normal * ArrowSize * Half);
    }
}
