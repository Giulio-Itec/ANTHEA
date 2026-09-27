using System.Windows;
using System.Windows.Media;

namespace X.Desktop;

/// <summary>The real steel plates of the H with an inclined web and of the box girder (the calculation of N–Mx uses the equivalent H)</summary>
internal sealed partial class BridgeDrawing
{
    /// <summary>The horizontal offset of the web at the bottom flange (inclined web H)</summary>
    private static double WebOffset(BridgeGeometry g) => Math.Tan(g.WebAngle) * g.WebHeight;

    /// <summary>The horizontal extent of the real steel section</summary>
    internal static double SteelExtent(BridgeGeometry g) => g.SectionType switch
    {
        BridgeSteelSectionType.Box => Math.Max(g.WebSpacingTop + g.TopFlangeWidth, g.Bottom1Width),
        BridgeSteelSectionType.InclinedWebH => 2 * Math.Max(g.TopFlangeWidth / 2, Math.Abs(WebOffset(g)) + g.Bottom1Width / 2),
        _ => Math.Max(g.TopWidth, g.Bottom1Width)
    };

    /// <summary>The x of the axes of the webs at the top and at the bottom (the slab goes from 0 to Width, centred on the steel)</summary>
    internal static (double Top, double Bottom)[] WebAxes(BridgeGeometry g)
    {
        double c = g.Width / 2;
        if (g.SectionType == BridgeSteelSectionType.Box)
            return [(c - g.WebSpacingTop / 2, c - g.WebSpacingBottom / 2), (c + g.WebSpacingTop / 2, c + g.WebSpacingBottom / 2)];
        return [(c, c + WebOffset(g))];
    }

    /// <summary>
    /// Draws the top flanges, the inclined webs (parallelograms, with the effective strips and the excluded part) and the bottom flange, then the
    /// excluded parts of the flanges (box: in the middle of the bottom flange between the webs and at the ends of its outstands)
    /// </summary>
    private void DrawRealSteel(DrawingContext dc, BridgeGeometry g, Func<double, double, Point> point, Pen outline, Action<Rect> hatch,
        Func<double, double, double, double, Rect> rectangle, Action<string, double, double> text)
    {
        double tt = g.TopThickness, hw = g.WebHeight, top = -tt, bottom = -tt - hw, twh = g.WebHorizontalThickness, tb = g.Bottom1Thickness;
        var axes = WebAxes(g);
        void Polygon(Brush fill, params (double X, double Y)[] points)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(point(points[0].X, points[0].Y), true, true);
                context.PolyLineTo(points.Skip(1).Select(p => point(p.X, p.Y)).ToList(), true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(fill, outline, geometry);
        }
        foreach (var a in axes)
            dc.DrawRectangle(SectionFill("Acciaio", top, tt, Steel), outline, rectangle(a.Top - g.TopFlangeWidth / 2, top, g.TopFlangeWidth, tt));
        double bottomCentre = g.SectionType == BridgeSteelSectionType.Box ? g.Width / 2 : axes[0].Bottom;
        dc.DrawRectangle(SectionFill("Acciaio", -g.Height, tb, Steel), outline, rectangle(bottomCentre - g.Bottom1Width / 2, -g.Height, g.Bottom1Width, tb));
        double webTop = Stage?.Effective.WebTop ?? hw / 2, webBottom = Stage?.Effective.WebBottom ?? hw / 2, missing = hw - webTop - webBottom;
        foreach (var a in axes)
        {
            double X(double y) => a.Top + (a.Bottom - a.Top) * (top - y) / hw;
            void Strip(double upper, double lower, double opacity)
            {
                if (upper - lower < 1e-6) return;
                if (opacity < 1) dc.PushOpacity(opacity);
                Polygon(SectionFill("Acciaio", lower, upper - lower, Steel), (X(upper) - twh / 2, upper), (X(upper) + twh / 2, upper), (X(lower) + twh / 2, lower), (X(lower) - twh / 2, lower));
                if (opacity < 1) dc.Pop();
            }
            if (Stage is not null && missing > 1e-3)
            {
                Strip(top, top - webTop, 1);
                Strip(bottom + webBottom, bottom, 1);
                Strip(top - webTop, bottom + webBottom, .28);
            }
            else Strip(top, bottom, 1);
        }
        if (Stage is not { } s) return;
        if (missing > 1e-3)
        {
            var a = axes[^1];
            double y = bottom + webBottom + missing / 2, x = a.Top + (a.Bottom - a.Top) * (top - y) / hw + twh / 2;
            text($"Inefficace {BridgeWorkspace.F(missing)} mm (in altezza)", point(x, y).X + 14, point(x, y).Y - 8);
        }
        void Gap(double x, double y, double width, double thickness) { if (width > 1e-3) hatch(rectangle(x, y, width, thickness)); }
        double outstand = (g.TopFlangeWidth - twh) / 2, excludedTop = outstand - s.Effective.Top.EffectiveAtStart;
        foreach (var a in axes)
        {
            Gap(a.Top - g.TopFlangeWidth / 2, top, excludedTop, tt);
            Gap(a.Top + g.TopFlangeWidth / 2 - excludedTop, top, excludedTop, tt);
        }
        BridgePlate? ends = g.SectionType == BridgeSteelSectionType.Box ? s.Effective.BottomOutstand : s.Effective.Bottom;
        if (g.SectionType == BridgeSteelSectionType.Box)
        {
            var inner = s.Effective.Bottom;
            Gap(axes[0].Bottom + twh / 2 + inner.EffectiveAtStart, -g.Height, g.BottomInternalWidth - inner.EffectiveAtStart - inner.EffectiveAtEnd, tb);
        }
        if (ends is not null)
        {
            double excluded = ends.Width - ends.EffectiveAtStart;
            Gap(bottomCentre - g.Bottom1Width / 2, -g.Height, excluded, tb);
            Gap(bottomCentre + g.Bottom1Width / 2 - excluded, -g.Height, excluded, tb);
        }
    }
}
