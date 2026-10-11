using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ANTHEA.ModelReports;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;

namespace ANTHEA.ModelViewer.Wpf;

internal static class MemberDiagramReportRenderer
{
    internal static ReportFigure Capture(ModelSnapshot model,ModelReportBlock block,string path,int width,bool white)
    {
        var options=block.MemberDiagram!;var view=block.View!;
        var all=MemberDiagramSeries.Read(model,options.Element,view.Case,view.ForceUnit,view.LengthUnit);
        var series=options.AllComponents?all:all.Where(s=>s.Component==view.Component).ToArray();
        if(series.Length==0)throw new InvalidDataException($"«{block.Title}»: campioni dell’asta non disponibili per {view.Case}.");
        Brush paper=white?Brushes.White:(Brush)new BrushConverter().ConvertFromString(view.Background)!;
        // Print colors are explicit; app theme changes never alter a saved report.
        var color=(paper as SolidColorBrush)?.Color??Colors.White;
        Brush ink=white || (color.R*.299+color.G*.587+color.B*.114)>150?Brushes.Black:Brushes.White;
        const double layoutWidth=1000;
        var panel=new StackPanel {Width=layoutWidth,Background=paper};
        panel.Children.Add(new TextBlock {Text=$"ANTHEA · asta {options.Element}",FontSize=24,FontWeight=FontWeights.SemiBold,Foreground=ink,Margin=new Thickness(16,10,16,5)});
        panel.Children.Add(new TextBlock {Text=$"{view.Case} · assi locali · x/L = {options.Station:0.####}",FontSize=17,Foreground=ink,Margin=new Thickness(16,0,16,14)});
        var grid=new Grid();int columns=series.Length>1?2:1;
        for(int column=0;column<columns;column++)grid.ColumnDefinitions.Add(new());
        for(int row=0;row<(series.Length+columns-1)/columns;row++)grid.RowDefinitions.Add(new(){Height=new GridLength(260)});
        for(int index=0;index<series.Length;index++)
        {
            var plot=new MemberDiagramPlot {Series=series[index],Station=options.Station,Invert=options.Invert,FillDiagram=options.Fill,Background=paper,Foreground=ink,FontSize=18,Margin=new Thickness(8)};
            Grid.SetColumn(plot,index%columns);Grid.SetRow(plot,index/columns);grid.Children.Add(plot);
        }
        panel.Children.Add(grid);
        panel.Measure(new Size(layoutWidth,double.PositiveInfinity));panel.Arrange(new Rect(new Point(),panel.DesiredSize));panel.UpdateLayout();
        double scale=width/layoutWidth;var drawing=new DrawingVisual();
        using(var context=drawing.RenderOpen()){context.PushTransform(new ScaleTransform(scale,scale));context.DrawRectangle(new VisualBrush(panel),null,new Rect(new Point(),panel.DesiredSize));}
        var bitmap=new RenderTargetBitmap(width,(int)Math.Ceiling(panel.ActualHeight*scale),96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))png.Save(file);
        var result=model.Results.First(r=>r.Name==view.Case);
        string description=$"Asta {options.Element} · {view.Case} · assi locali. Punti: campioni importati; segmenti: interpolazione grafica. Nessuna estrapolazione.\n";
        description+=string.Join("; ",series.Select(s=>{var at=s.At(options.Station);return $"{s.Component}: {(at.Value is double v?v.ToString("G7"):"—")} {s.Unit} ({(at.Value==null?"non disponibile":at.Imported?"importato":"interpolato graficamente")})";}));
        if(result.IsEnvelope)description+="\nInviluppo: estremi per componente non simultanei.";
        if(series.Any(s=>s.HasAmbiguousStations))description+="\nStazioni con valori multipli: mostrati soltanto i punti, senza interpolazione.";
        return new(path,description);
    }
}
