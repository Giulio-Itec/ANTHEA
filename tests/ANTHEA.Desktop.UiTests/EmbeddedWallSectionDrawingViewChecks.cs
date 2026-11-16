using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Modules.EmbeddedWalls;
using X.Desktop.Views.EmbeddedWalls;

namespace X.Desktop;

/// <summary>
/// Organizzazione del modulo paratie (progetto del 16/11), step 5, off screen, on the sheet with a tie and the water: the legend of the soils in the
/// drawing opens its soil; the soils keep the identification tints in Chiara, Scuro and Molto scuro (technical drawing, identity role); the
/// wheel and the page Vista magnify the drawing and «Adatta» brings it back; the legend can be hidden; the element chosen is the row of the
/// editor; the tie is drawn to scale with the lengths of «Sfilamento dei tiranti». Captures sezione-*; data restored afterwards.
/// </summary>
internal static partial class EmbeddedWallViewChecks
{
    private static async Task CheckSectionDrawing(FrameworkElement view, EmbeddedWallSheetViewModel sheet,
        Func<Task> settle, Action<bool, string> check, Action<string> capture)
    {
        var before = sheet.Data.DeepClone().AsObject();
        var sketch = Visual(view).OfType<EmbeddedWallModelSketch>().Single();
        await settle();
        var legend = sketch.LegendBounds;
        check(!legend.IsEmpty && legend.Right <= sketch.ActualWidth && legend.Top < sketch.ActualHeight / 2 && sheet.Sketch!.Soils.Count == 2,
            "Passo 5: legenda dei terreni assente o fuori dal disegno.");
        var fromLegend = Enumerable.Range(0, (int)legend.Height).Select(dy => sketch.PickAt(new Point(legend.X + legend.Width / 2, legend.Y + dy)))
            .FirstOrDefault(p => p is not null);
        check(fromLegend?.Table == "left_soils" && fromLegend.Detail.Contains("kN/m³"), "Passo 5: la riga della legenda non apre il terreno.");
        capture("sezione-legenda");
        // The lower soil of the left side (Ghiaia, second soil by name) keeps the tint Swatch.Soil2 in every appearance.
        var gravel = sheet.Sketch!.Bands.First(b => b.Table == "left_soils" && b.Name == "Ghiaia");
        double x = sketch.ActualWidth * .35;
        var rows = Enumerable.Range(0, (int)sketch.ActualHeight).Where(y => ReferenceEquals(sketch.PickAt(new Point(x, y))?.Row, gravel.Row)).ToArray();
        check(rows.Length > 20 && gravel.Tint == 1, "Passo 5: lo strato inferiore non è disegnato.");
        var probe = sketch.TranslatePoint(new Point(x, rows[rows.Length * 3 / 4]), view);
        var soil2 = X.Desktop.Themes.ThemeTokens.Get("Swatch.Soil2").Color;
        foreach (var appearance in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark })
        {
            Appearance.Set(appearance, false); await settle();
            check(Share(view, probe, soil2) > .6, $"Passo 5: tinta dello strato diversa da Swatch.Soil2 nell'aspetto {appearance}.");
            if (appearance == AppAppearance.Dark) capture("sezione-scura");
            if (appearance == AppAppearance.VeryDark) capture("sezione-molto-scura");
        }
        Appearance.Set(AppAppearance.Light, false); await settle();
        // Magnification: the page Vista and the wheel, both on the state of the view of the sheet; «Adatta» back to the whole section.
        sheet.ZoomInSketchCommand.Execute(null); sheet.ZoomInSketchCommand.Execute(null); await settle();
        check(sketch.Zoom == sheet.SketchZoom && sheet.SketchZoom > 1.5, "Passo 5: «Ingrandisci» non arriva al disegno.");
        double zoomed = sheet.SketchZoom;
        sketch.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, Mouse.MouseWheelDeltaForOneLine) { RoutedEvent = UIElement.MouseWheelEvent });
        await settle();
        check(sheet.SketchZoom > zoomed && sketch.Zoom == sheet.SketchZoom, "Passo 5: la rotellina non ingrandisce il disegno o non arriva al foglio.");
        capture("sezione-ingrandita");
        sheet.FitSketchCommand.Execute(null); await settle();
        check(sketch.Zoom == EmbeddedWallSketchZoom.Minimum && !sheet.FitSketchCommand.CanExecute(null), "Passo 5: «Adatta» non torna alla sezione intera.");
        sheet.ToggleSoilLegendCommand.Execute(null); await settle();
        check(sketch.LegendBounds.IsEmpty && !sketch.ShowLegend, "Passo 5: la legenda non si nasconde.");
        sheet.ToggleSoilLegendCommand.Execute(null); await settle();
        // The element chosen is the row of the editor, highlighted.
        var tie = sheet.Sketch!.Supports.Single();
        sketch.PickCommand!.Execute(new WallSketchPick("supports", tie.Row, tie.Detail)); await settle();
        check(ReferenceEquals(sketch.Selected, tie.Row) && sheet.InspectorTable?.Key == "supports", "Passo 5: il sostegno scelto non è evidenziato.");
        capture("sezione-selezione");
        // Cifre significative (16/11): «Tipo» in Italian, the names of the layers off the water lines.
        await CheckSectionTerms(view, sheet, settle, check, capture);
        // R08: the tie to scale only with free and bonded lengths of «Sfilamento dei tiranti».
        check(tie.FreeLength is null && tie.Detail.Contains("Simbolo schematico"), "Passo 5: tirante in scala senza le lunghezze.");
        var scaled = before.DeepClone().AsObject();
        var lengths = EmbeddedWallDocument.NewRow("anchor_checks"); lengths["support"] = tie.Id; lengths["free_length"] = 6; lengths["bond_length"] = 5;
        scaled["anchor_checks"]!.AsArray().Add(lengths);
        sheet.Reload(scaled); await settle();
        check(sketch.Model?.Supports.Single() is { FreeLength: 6d, BondLength: 5d, AnchorCheck: not null }, "Passo 5: il tirante non usa le lunghezze di «Sfilamento dei tiranti».");
        capture("sezione-tirante-in-scala");
        OwnerWindow(view).Width = 1100; OwnerWindow(view).Height = 760; await settle();
        check(sketch.ActualWidth > 310 && sketch.ActualHeight > 200 && !sketch.LegendBounds.IsEmpty && sketch.LegendBounds.Width <= sketch.ActualWidth / 2 + 1,
            $"Passo 5: alla misura minima disegno troppo piccolo o legenda assente o più larga di metà disegno (disegno {sketch.ActualWidth:0}×{sketch.ActualHeight:0}, legenda {sketch.LegendBounds}).");
        capture("sezione-compatta");
        OwnerWindow(view).Width = 1450; OwnerWindow(view).Height = 950;
        sheet.Reload(before); await settle();
        check(JsonNode.DeepEquals(sheet.Data, before) && sheet.SketchZoom == EmbeddedWallSketchZoom.Minimum && sheet.ShowSoilLegend,
            "Passo 5: il disegno lascia dati o stato della vista modificati.");
    }

    private static Window OwnerWindow(FrameworkElement view) => Window.GetWindow(view)!;

    /// <summary>Share of the pixels of a 9×9 block around <paramref name="point"/> (coordinates of <paramref name="view"/>) equal to <paramref name="colour"/>.</summary>
    private static double Share(FrameworkElement view, Point point, Color colour)
    {
        var bitmap = new RenderTargetBitmap((int)view.ActualWidth, (int)view.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        const int half = 4, side = 2 * half + 1;
        var pixels = new byte[side * side * 4];
        bitmap.CopyPixels(new Int32Rect((int)point.X - half, (int)point.Y - half, side, side), pixels, side * 4, 0);
        int equal = 0;
        for (int i = 0; i < pixels.Length; i += 4) if (pixels[i] == colour.B && pixels[i + 1] == colour.G && pixels[i + 2] == colour.R) equal++;
        return equal / (double)(side * side);
    }
}
