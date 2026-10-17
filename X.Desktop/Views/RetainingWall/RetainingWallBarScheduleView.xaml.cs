using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Application.Walls;
using Anthea.Presentation.Modules.RetainingWall;

namespace X.Desktop.Views.RetainingWall;

/// <summary>
/// The dialog of the bar schedule (MU10, ShowBarSchedule of the historic view): pages and zoom are state of the view, the drawing is the
/// historic <see cref="RetainingWallBarDrawing"/> (Q3), the exports build the bytes as the historic dialog (the figures of
/// ModuleFigureRenderer.BarFigures, DrawingPdf, WallExports) and the sheet writes them in the file chosen through its file dialog service.
/// </summary>
public partial class RetainingWallBarScheduleView : UserControl
{
    /// <summary>Caption of the window, as the historic dialog.</summary>
    internal const string Title = "Armature del muro · sezione e distinta ferri";

    private readonly RetainingWallSheetViewModel sheet;

    internal RetainingWallBarScheduleView(RetainingWallSheetViewModel sheet, RetainingWallBarDrawing drawing)
    {
        this.sheet = sheet;
        Drawing = drawing;
        InitializeComponent();
        PageChoice.ItemsSource = Enumerable.Range(1, drawing.PageCount).Select(n => "Tavola " + n).ToArray();
        PageChoice.SelectedItem = "Tavola 1";
        drawing.LayoutTransform = new ScaleTransform(.9, .9);
        SheetScroll.Content = drawing;
    }

    /// <summary>The drawing of the sheet of the bar schedule.</summary>
    internal RetainingWallBarDrawing Drawing { get; }

    private void Page_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Drawing is null || PageChoice is null || PageChoice.SelectedIndex < 0) return;
        Drawing.Page = PageChoice.SelectedIndex; Drawing.InvalidateVisual();
    }

    private void Zoom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Drawing is not null && Zoom is not null) Drawing.LayoutTransform = new ScaleTransform(Zoom.Value / 100, Zoom.Value / 100);
    }

    private void Pdf_Click(object sender, RoutedEventArgs e) =>
        sheet.SaveBarSchedule("pdf", () => DrawingPdf.Create(ModuleFigureRenderer.BarFigures(Drawing).Select(f => f.Png)));

    private void Word_Click(object sender, RoutedEventArgs e) =>
        sheet.SaveBarSchedule("docx", () => WallExports.CreateBarSchedule(Drawing.Data, Drawing.Schedule, ModuleFigureRenderer.BarFigures(Drawing)));

    private void Csv_Click(object sender, RoutedEventArgs e) =>
        sheet.SaveBarSchedule("csv", () => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(WallExports.BarScheduleCsv(Drawing.Schedule))).ToArray());

    private void Png_Click(object sender, RoutedEventArgs e) => sheet.SaveBarSchedule("png", () => Drawing.Png(3360, 2370));
}
