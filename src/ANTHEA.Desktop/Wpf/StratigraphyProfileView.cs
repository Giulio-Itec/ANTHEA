using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Application.Stratigraphy;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.Stratigraphy;

namespace X.Desktop;

// The drawing of the profile (used by the XAML view of the stratigraphy, StratigraphyEditorView) and the icon of the module, moved unchanged
// from the end of StratigraphyWorkspace.cs when the historic view was archived (f5-pass-geo, supporto/SUPERATI/ui-storica/stratigrafia).

internal sealed class StratigraphyProfileView : DrawingView
{
    // Display dimensions only: pixels and proportions of the visible panel, not soil parameters.
    private const double ProfileWidthRatio = .35, InfiniteTailRatio = .25;
    private const double TopPixels = 30, LabelGapPixels = 8, TextInsetPixels = 3, EdgePixels = 4,
        LabelPaddingPixels = 12, WaterFooterPixels = 44, ExtentFooterPixels = 22;
    internal JsonObject? Data;
    internal int Selection;
    internal event Action<int>? Selected;
    private readonly List<(Rect Bounds, int Index)> hits = [];
    internal StratigraphyProfileView()
    {
        MinHeight = 320;
        MouseLeftButtonDown += (_, e) =>
        {
            var point = e.GetPosition(this);
            foreach (var hit in hits) if (hit.Bounds.Contains(point)) { Selected?.Invoke(hit.Index); break; }
        };
    }
    protected override void Render(DrawingContext dc, Size size)
    {
        hits.Clear(); if (Data is null || size.Width < 150 || size.Height < 150) return;
        var rows = StratigraphyDocument.Layers(Data).OfType<JsonObject>().ToArray();
        double finite = rows.Where(r => !StratigraphyDocument.Infinite(r)).Sum(r => StratigraphyDocument.Number(r["thickness"], "Spessore")!.Value);
        bool infinite = StratigraphyDocument.Infinite(rows[^1]);
        double shown = Math.Min(double.MaxValue, finite + (infinite ? Math.Max(2, finite * InfiniteTailRatio) : 0));
        double water = StratigraphyDocument.Number(Data["water_depth"], "Falda") ?? 0;
        if (infinite && Data["water_state"]!.ToString() == "Assegnata") shown = Math.Max(shown, water + 1);
        double height = size.Height - 100, top = 0, left = 58, width = Math.Max(60, size.Width * ProfileWidthRatio);
        var ink = Appearance.Foreground(Ui.Muted);
        Text(dc, "Piano campagna", left, 5, 12, ink);
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i]; bool endless = StratigraphyDocument.Infinite(row);
            double bottom = endless ? shown : top + StratigraphyDocument.Number(row["thickness"], "Spessore")!.Value;
            var bounds = new Rect(left, TopPixels + height * (top / shown), width, height * ((bottom - top) / shown));
            var fill = (SolidColorBrush)Ui.Brush(row["color"]!.ToString());
            dc.DrawRectangle(fill, null, bounds);
            dc.DrawRectangle(SoilHatchBrush.Create(StratigraphyDocument.SoilType(row), fill.Color), null, bounds);
            dc.DrawRectangle(null, new Pen(i == Selection ? Brushes.DodgerBlue : Brushes.DimGray, i == Selection ? 3 : 1), bounds);
            Text(dc, top.ToString("G4", CultureInfo.GetCultureInfo("it-IT")), 2, bounds.Top, 11, ink);
            Text(dc, row["name"]!.ToString() + "\n" + StratigraphyDocument.SoilType(row) + (endless ? "\n↓ indefinito" : ""), bounds.Right + LabelGapPixels, bounds.Top + TextInsetPixels, 12, ink, width: Math.Max(30, size.Width - bounds.Right - LabelPaddingPixels));
            hits.Add((bounds, i)); top = bottom;
        }
        Text(dc, infinite ? "↓" : top.ToString("G4", CultureInfo.GetCultureInfo("it-IT")), 2, TopPixels + height * (top / shown), 11, ink);
        if (Data["water_state"]!.ToString() == "Assegnata")
        {
            if (water <= shown)
            {
                double y = TopPixels + height * (water / shown);
                dc.DrawLine(new Pen(Brushes.DodgerBlue, 2) { DashStyle = DashStyles.Dash }, new(left - LabelGapPixels, y), new(left + width + LabelPaddingPixels, y));
                Text(dc, $"Falda: {water:G4} m", left, size.Height - WaterFooterPixels, 12, Brushes.DodgerBlue);
            }
            else Text(dc, $"Falda a {water:G4} m · sotto il profilo", left, size.Height - WaterFooterPixels, 12, Brushes.DodgerBlue);
        }
        if (infinite) Text(dc, $"Vista fino a {shown:G4} m; ultimo strato indefinito.", 2, size.Height - ExtentFooterPixels, 11, ink, width: size.Width - EdgePixels);
    }
}

internal sealed class SoilProfileIcon : DrawingView
{
    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Ui.Brush("#D5BE92"), null, new Rect(10, 10, 60, 20));
        dc.DrawRectangle(Ui.Brush("#B29C7C"), null, new Rect(10, 30, 60, 20));
        dc.DrawRectangle(Ui.Brush("#A7B7C7"), null, new Rect(10, 50, 60, 20));
        dc.DrawRectangle(null, new Pen(Appearance.Ink, 2), new Rect(10, 10, 60, 60));
        dc.DrawLine(new Pen(Brushes.DodgerBlue, 2) { DashStyle = DashStyles.Dash }, new Point(4, 40), new Point(76, 40));
    }
}
