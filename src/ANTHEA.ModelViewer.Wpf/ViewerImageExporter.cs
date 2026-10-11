using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HelixToolkit.Wpf.SharpDX;

namespace ANTHEA.ModelViewer.Wpf;

/// <summary>Composes the control's own WPF content with its Direct3D scene, including labels and legend.</summary>
internal static class ViewerImageExporter
{
    internal static void SynchronizeLayout(FrameworkElement root,Viewport3DX viewport)
    {
        root.UpdateLayout();viewport.UpdateLayout();
        if(viewport.RenderHost is {} host)
        {
            host.Resize(Math.Max(1,(int)Math.Round(viewport.ActualWidth)),Math.Max(1,(int)Math.Round(viewport.ActualHeight)));
            host.UpdateAndRender();
        }
    }
    private static BitmapSource RenderScene(Viewport3DX view,int width,int height)
    {
        var host=view.RenderHost??throw new InvalidOperationException("Vista grafica non pronta.");
        var dpi=VisualTreeHelper.GetDpi(view);
        // Resize consumes WPF DIPs, while ActualWidth/Height on the render host are physical pixels.
        // Restoring host dimensions through the framework overload multiplies the DPI scale twice.
        try
        {
            host.Resize(Math.Max(1,(int)Math.Round(width/dpi.DpiScaleX)),Math.Max(1,(int)Math.Round(height/dpi.DpiScaleY)));
            return view.RenderBitmap()??throw new InvalidOperationException("Immagine della scena non disponibile.");
        }
        finally
        {
            host.Resize(Math.Max(1,(int)Math.Round(view.ActualWidth)),Math.Max(1,(int)Math.Round(view.ActualHeight)));
            host.UpdateAndRender();
        }
    }
    public static void SaveScene(Viewport3DX viewport,Viewport3DX? comparison,ANTHEA.ModelViewer.Presentation.ModelViewerViewModel state,string path,bool printLayout=false)
    {
        int width=state.ExportWidths.Contains(state.ExportWidth)?state.ExportWidth:2400;
        double scale=printLayout?width/1000.0:1; int layoutWidth=printLayout?1000:width;
        int count=comparison==null?1:2,panelWidth=layoutWidth/count,legendWidth=state.IsResultsView?150:0,sceneWidth=panelWidth-legendWidth;
        int sceneHeight=(int)Math.Clamp(sceneWidth*Math.Max(1,viewport.ActualHeight)/Math.Max(1,viewport.ActualWidth),350,printLayout?1100:2400),height=sceneHeight+128;
        var background=state.ExportWhiteBackground?Colors.White:(Color)ColorConverter.ConvertFromString(state.SceneBackground);
        var ink=state.ExportWhiteBackground?Brushes.Black:(Brush)new BrushConverter().ConvertFromString(state.SceneInk)!;
        var visual=new DrawingVisual();
        using(var drawing=visual.RenderOpen())
        {
            drawing.PushTransform(new ScaleTransform(scale,scale));
            drawing.DrawRectangle(new SolidColorBrush(background),null,new Rect(0,0,layoutWidth,height));
            void Text(string value,double x,double y,double size,double maxWidth)
            {
                var text=new FormattedText(value,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,ink,1) { MaxTextWidth=maxWidth,Trimming=TextTrimming.CharacterEllipsis,MaxLineCount=2 };
                drawing.DrawText(text,new Point(x,y));
            }
            Text("ANTHEA · "+state.Title,24,12,23,layoutWidth-48);
            for(int panel=0;panel<count;panel++)
            {
                var view=panel==0?viewport:comparison!;var model=panel==0?state:state.Comparison!;var previous=view.BackgroundColor;
                try
                {
                    view.SetCurrentValue(Viewport3DX.BackgroundColorProperty,background);
                    var pixels=RenderScene(view,(int)Math.Round(sceneWidth*scale),(int)Math.Round(sceneHeight*scale));
                    drawing.DrawImage(pixels,new Rect(panel*panelWidth,80,sceneWidth,sceneHeight));
                }
                finally {view.SetCurrentValue(Viewport3DX.BackgroundColorProperty,previous);}
                Text(model.ResultHeading,panel*panelWidth+24,45,14,panelWidth-40);
                if(!model.IsResultsView)continue;
                double legendX=(panel+1)*panelWidth-legendWidth+20,legendY=100,legendHeight=sceneHeight-60;
                for(int i=0;i<256;i++)
                {
                    var c=Contours.DisplayColor(1-i/255.0,model.SelectedContourPalette,model.ReversePalette,model.ContourBands);
                    drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb((byte)(c.Red*255),(byte)(c.Green*255),(byte)(c.Blue*255))),null,new Rect(legendX,legendY+i*legendHeight/256,20,legendHeight/256+1));
                }
                for(int i=0;i<5;i++)Text(model.LegendTicks[i],legendX+28,legendY+i*legendHeight/4-7,printLayout?16:12,100);
                Text(model.ResultUnit,panel*panelWidth+24,height-38,printLayout?16:13,panelWidth-30);
            }
        }
        var target=new RenderTargetBitmap(width,(int)Math.Round(height*scale),96,96,PixelFormats.Pbgra32);target.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(target));using var output=File.Create(path);encoder.Save(output);
    }
    public static void Save(FrameworkElement root, Viewport3DX viewport, string path)
    {
        string temporary = Path.Combine(Path.GetTempPath(), "anthea-view-" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            SynchronizeLayout(root,viewport);
            viewport.SaveScreen(temporary);
            BitmapSource scene;
            using(var input=File.OpenRead(temporary))scene=BitmapDecoder.Create(input,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
            scene.Freeze();
            root.UpdateLayout(); var dpi = VisualTreeHelper.GetDpi(root);
            int width = (int)Math.Ceiling(root.ActualWidth * dpi.DpiScaleX), height = (int)Math.Ceiling(root.ActualHeight * dpi.DpiScaleY);
            var surface = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32); var content=new DrawingVisual();
            // A hosted visual includes its parent-relative offset; retained Direct3D bounds can also exceed its new size.
            // Select the actual arranged rectangle explicitly, rather than auto-scaling the stale descendant bounds.
            var offset=VisualTreeHelper.GetOffset(root);
            using(var context=content.RenderOpen())context.DrawRectangle(new VisualBrush(root) { Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(new Point(offset.X,offset.Y),root.RenderSize) },null,new Rect(root.RenderSize));
            surface.Render(content);
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawImage(surface, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
                var origin = viewport.TransformToAncestor(root).Transform(new Point());
                drawing.DrawImage(scene, new Rect(origin.X, origin.Y, viewport.ActualWidth, viewport.ActualHeight));
            }
            var target = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32); target.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target)); using var output = File.Create(path); encoder.Save(output);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
