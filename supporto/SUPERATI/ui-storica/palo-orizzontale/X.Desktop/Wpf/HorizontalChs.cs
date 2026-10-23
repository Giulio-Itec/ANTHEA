using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.HorizontalPile;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class HorizontalWorkspace
{
    private InputForm CreateChsForm(JsonObject input)
    {
        var form = new InputForm(input, HorizontalPileFields.Chs(), _ => Changed(), compact: true, symbolColumns: true);
        Group(form, HorizontalPileFields.ChsGroups());
        return form;
    }
    private void UpdateChsPreview()
    {
        foreach (var (key, enabled) in HorizontalPilePresentation.ChsFieldStates(Data["sezione"]!.AsObject())) sectionFields.Enable(key, enabled, true);
        var preview = HorizontalPreview.ChsSection(Data);
        foreach (var (key, value) in preview.Fields) sectionFields.Set(key, value, true);
        chsDrawing.Properties = preview.Geometry;
        chsDrawing.InvalidateVisual();
    }
}

internal sealed class ChsDrawing : DrawingView
{
    internal JsonObject? Properties { get; set; }
    protected override void Render(DrawingContext dc, Size size)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(size));
        if (Properties is not { } p) { Text(dc, "Completare i dati CHS", 20, 30); return; }
        double r = Math.Max(1, Math.Min(size.Width - 60, size.Height - 100) / 2);
        Point center = new(size.Width / 2, (size.Height - 65) / 2);
        dc.DrawEllipse(Ui.Brush("#96999D"), new Pen(Ui.Navy, 1.5), center, r, r);
        double ri = r * p.D("diametro_interno_mm") / p.D("diametro_mm");
        dc.DrawEllipse(Brushes.White, new Pen(Ui.Navy, 1), center, ri, ri);
        Text(dc, $"CHS {p.D("diametro_mm"):0.0} × {p.D("spessore_mm"):0.0} mm\nSolo acciaio · riempimento non resistente", 15, size.Height - 55, 12, Ui.Navy, size.Width - 30);
    }
}
