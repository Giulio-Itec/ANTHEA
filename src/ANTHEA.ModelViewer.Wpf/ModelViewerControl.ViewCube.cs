using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HelixToolkit.SharpDX;

namespace ANTHEA.ModelViewer.Wpf;

public partial class ModelViewerControl
{
    static TextureModel CreateViewCubeTexture()
    {
        // Helix atlas order: front, back, left, right, top, bottom. Labels and artwork belong to ANTHEA.
        string[] labels=["FR","RE","SX","DX","SU","GI"];
        Color[] colors=[Color.FromRgb(56,80,107),Color.FromRgb(56,80,107),Color.FromRgb(63,88,112),Color.FromRgb(63,88,112),Color.FromRgb(69,107,120),Color.FromRgb(69,107,120)];
        var visual=new DrawingVisual();
        using(var drawing=visual.RenderOpen())
            for(int i=0;i<labels.Length;i++)
            {
                drawing.DrawRectangle(new SolidColorBrush(colors[i]),null,new Rect(i*100,0,100,100));
                drawing.DrawRectangle(null,new Pen(new SolidColorBrush(Color.FromRgb(163,189,211)),2),new Rect(i*100+1,1,98,98));
                var text=new FormattedText(labels[i],CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),40,Brushes.White,1);
                drawing.DrawText(text,new Point(i*100+(100-text.Width)/2,(100-text.Height)/2));
            }
        var bitmap=new RenderTargetBitmap(600,100,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));var stream=new MemoryStream();encoder.Save(stream);stream.Position=0;
        return new TextureModel(stream,true);
    }
}
