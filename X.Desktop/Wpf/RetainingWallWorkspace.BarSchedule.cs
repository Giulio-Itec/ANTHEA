using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal RetainingWallBarDrawing CreateBarDrawing()
    {
        if (Calculation is null || Busy) throw new ArgumentException("Attendere il calcolo aggiornato prima di aprire la distinta.");
        return new(Calculation.Input, RetainingWall.CalculateBarSchedule(Calculation));
    }
    internal static IReadOnlyList<ReportRetainingWall.Figure> BarFigures(RetainingWallBarDrawing drawing)
    {
        int oldPage = drawing.Page; var figures = new List<ReportRetainingWall.Figure>();
        try
        {
            for (int i = 0; i < drawing.PageCount; i++)
            {
                drawing.Page = i;
                figures.Add(new("Tavola armature e distinta ferri · " + (i + 1), drawing.Png(3360, 2370), 1120d / 790));
            }
        }
        finally { drawing.Page = oldPage; }
        return figures;
    }
    private void ShowBarSchedule()
    {
        Commit();
        try
        {
            var sheet = CreateBarDrawing(); var pages = Ui.Choice(Enumerable.Range(1, sheet.PageCount).Select(n => "Tavola " + n).ToArray(), "Tavola 1");
            pages.SelectionChanged += (_, _) => { sheet.Page = pages.SelectedIndex; sheet.InvalidateVisual(); };
            var zoom = new Slider { Minimum = 35, Maximum = 175, Value = 90, Width = 150, Margin = new Thickness(8) };
            sheet.LayoutTransform = new ScaleTransform(.9, .9);
            zoom.ValueChanged += (_, _) => sheet.LayoutTransform = new ScaleTransform(zoom.Value / 100, zoom.Value / 100);
            void Save(string ext, Func<byte[]> contents)
            {
                var save = new SaveFileDialog { Filter = ext.ToUpperInvariant() + "|*." + ext, FileName = "Muro_distinta_ferri." + ext };
                if (save.ShowDialog(Window.GetWindow(sheet)) != true) return;
                try { Archivio.ScriviAtomico(save.FileName, contents()); }
                catch (Exception ex) { MessageBox.Show(Window.GetWindow(sheet), "Esportazione non riuscita: " + ex.Message); }
            }
            var bar = Ui.Bar(Ui.Text("Pagina", 12), pages, Ui.Text("Zoom", 12), zoom,
                Ui.Button("PDF completo", () => Save("pdf", () => DrawingPdf.Create(BarFigures(sheet).Select(f => f.Png))), inspection: true),
                Ui.Button("Word completo", () => Save("docx", () => ReportRetainingWall.CreateBarSchedule(sheet.Data, sheet.Schedule, BarFigures(sheet))), inspection: true),
                Ui.Button("CSV distinta", () => Save("csv", () => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ReportRetainingWall.BarScheduleCsv(sheet.Schedule))).ToArray()), inspection: true),
                Ui.Button("PNG pagina", () => Save("png", () => sheet.Png(3360, 2370)), inspection: true));
            var view = new ScrollViewer { Content = sheet, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Ui.Dialog(this, "Armature del muro · sezione e distinta ferri", Ui.Dock(view, top: bar), 1270, 920).ShowDialog();
        }
        catch (ArgumentException ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "Distinta ferri"); }
    }
}
