using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using X.Core;

namespace X.Desktop;

/// <summary>Physical-page layout for report figures; values and idealised branches come from the existing diagram model.</summary>
internal sealed class PileReportDiagrams(string caption, double length, IReadOnlyList<HorizontalDiagramPanel> panels, double? hingeDepth = null) : DrawingView
{
    const double OuterMargin = 12, CaptionInset = 24, BottomReserve = 77, SlotReserve = 22, AxisReserve = 67, TickInset = 34, TickCenter = 17, LegendStart = 25, LegendInset = 18, TitleInset = 20, MessageInset = 4, MessageTop = 10;
    const int TickIntervals = 4;
    const double PanelLeft = 46, LegendPitch = 15, TextHalfHeight = 8;
    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(size));
        Text(dc, caption, 12, 6, 16, Ui.Navy, size.Width - CaptionInset, true);
        const double top = 84;
        double bottom = size.Height - BottomReserve, slot = (size.Width - SlotReserve) / panels.Count;
        double Y(double z) => top + z / length * (bottom - top);
        for (int i = 0; i < panels.Count; i++)
        {
            var panel = panels[i]; double left = PanelLeft + slot * i, width = slot - AxisReserve;
            double minimum = panel.Minimum, maximum = panel.Maximum;
            if (maximum <= minimum) { minimum -= 1; maximum += 1; }
            double X(double x) => left + (x - minimum) / (maximum - minimum) * width;
            Text(dc, panel.Title, slot * i + OuterMargin, 38, 14, Ui.Navy, slot - TitleInset, true);
            if (hingeDepth is double internalHinge && internalHinge > 0 && internalHinge < length)
                dc.DrawRectangle(Ui.Brush("#F1F3F5"), null, new Rect(left, Y(internalHinge), width, bottom - Y(internalHinge)));
            for (int tick = 0; tick <= TickIntervals; tick++)
            {
                double z = length * tick / TickIntervals, y = Y(z);
                dc.DrawLine(new Pen(Ui.Brush("#E2E5E9"), 1), new(left, y), new(left + width, y));
                Text(dc, z.ToString("0.##", CultureInfo.GetCultureInfo("it-IT")), left - TickInset, y - TextHalfHeight, 12, width: 33);
            }
            dc.DrawRectangle(null, new Pen(Ui.Brush("#64748B"), 1), new Rect(left, top, width, bottom - top));
            if (minimum < 0 && maximum > 0) dc.DrawLine(new Pen(Ui.Brush("#94A3B8"), 1), new(X(0), top), new(X(0), bottom));
            for (int tick = 0; tick <= 2; tick++)
            {
                double value = minimum + (maximum - minimum) * tick / 2;
                Text(dc, value.ToString("G3", CultureInfo.GetCultureInfo("it-IT")), left - TickCenter + width * tick / 2, bottom + MessageInset, 12, width: width / 2 + TitleInset);
            }
            dc.PushClip(new RectangleGeometry(new Rect(left, top, width, bottom - top)));
            foreach (var curve in panel.Curves)
            {
                var brush = Ui.Brush(curve.Color);
                foreach (var segment in curve.Segments)
                {
                    var pen = new Pen(brush, curve.Signed ? 2.2 : 1.5) { DashStyle = segment.Dashed || curve.Dashed ? DashStyles.Dash : DashStyles.Solid };
                    dc.DrawLine(pen, new(X(segment.FromValue), Y(segment.FromDepth)), new(X(segment.ToValue), Y(segment.ToDepth)));
                }
            }
            foreach (var hinge in panel.Hinges) dc.DrawEllipse(Ui.Navy, null, new(X(hinge.Value), Y(hinge.Depth)), 3.5, 3.5);
            if (panel.MomentLimit is double limit) foreach (double v in new[] { -limit, limit })
                dc.DrawLine(new Pen(Ui.Brush("#94A3B8"), 1) { DashStyle = DashStyles.Dot }, new(X(v), top), new(X(v), bottom));
            dc.Pop();
            int row = 0;
            foreach (var curve in panel.Curves.Where(c => c.Label != "").Take(3))
                Text(dc, curve.Label, slot * i + OuterMargin, bottom + LegendStart + LegendPitch * row++, 12, Ui.Brush(curve.Color), slot - LegendInset);
            if (panel.Message is { } message) Text(dc, message, left + MessageInset, top + MessageTop, 13, Ui.Navy, width - TextHalfHeight);
        }
    }

    internal static IReadOnlyList<ReportFigure> Figures(JsonObject result, bool brief)
    {
        var images = new List<ReportFigure>();
        if (result.S("tipo_risultato") == "palo_elastico")
        {
            var points = result["risposta"].Array("Points");
            var panels = new List<HorizontalDiagramPanel>();
            foreach (var (key, label) in new[] { ("Displacement", "Spostamento y [m]"), ("Shear", "Taglio V [kN]"), ("Moment", "Momento M [kNm]") })
            {
                var values = points.Select(p => new { X = J.Number(p?[key]) ?? throw new InvalidDataException("Ordinata elastica assente."), Z = J.Number(p?["Depth"]) ?? throw new InvalidDataException("Profondità elastica assente.") }).ToArray();
                var segments = values.Zip(values.Skip(1), (a, b) => new HorizontalDiagramSegment(a.X, a.Z, b.X, b.Z, false)).ToArray();
                panels.Add(new(label, [new("Carico assegnato", "#125DA8", true, false, segments)], Math.Min(0, values.Min(p => p.X)), Math.Max(0, values.Max(p => p.X)), [], null, null, [], null));
            }
            double length = J.Number(result["input"]?["lunghezza"]) ?? throw new InvalidDataException("Lunghezza assente.");
            images.Add(new("Risposta elastica · profondità x dalla testa [m]", new PileReportDiagrams("Risposta al carico assegnato", length, panels).ReportPng(640, 395), "grafici", 1));
            return images;
        }
        int count = result.Array("sondaggi").Count;
        int governing = (int)(J.Number(result["sondaggio_governante"]) ?? throw new InvalidDataException("Verticale governante assente.")) - 1;
        foreach (int survey in brief ? new[] { governing } : Enumerable.Range(0, count))
        {
            var pile = HorizontalPileDiagrams.Build(result, survey, false);
            var main = pile.Panels.Where(p => p.Title.StartsWith("Taglio") || p.Title.StartsWith("Momento")).ToArray();
            images.Add(new($"Verticale {survey + 1}: V e M alla Hu · profondità dalla testa [m]", new PileReportDiagrams($"Verticale {survey + 1} · sollecitazioni alla Hu", pile.Length, main, pile.Hinge).ReportPng(640, 395), "grafici", survey + 1));
            if (brief) continue;
            var soil = HorizontalPileDiagrams.Build(result, survey, true);
            images.Add(new($"Verticale {survey + 1}: tensioni e reazioni. {soil.Note} {soil.HingeNote}", new PileReportDiagrams($"Verticale {survey + 1} · terreno", soil.Length, soil.Panels, soil.Hinge).ReportPng(640, 440), "grafici", survey + 1));
            var other = pile.Panels.Except(main).ToArray();
            images.Add(new($"Verticale {survey + 1}: equilibrio. {pile.Note} {pile.HingeNote}", new PileReportDiagrams($"Verticale {survey + 1} · equilibrio alla Hu", pile.Length, other, pile.Hinge).ReportPng(640, 440), "grafici", survey + 1));
        }
        return images;
    }
}
