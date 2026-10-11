using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using ANTHEA.ModelWorkspace;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void ExportLayoutRenderingChecks(string output,bool captureBefore)
    {
        SetAppearance("VeryDark");
        var store=new ModelViewPresetStore(Path.Combine(output,"preferenze-prova"));
        using var control=new ModelViewerControl(Sample.Model(),[],false,_=>{},(_,_,_,_)=>{},store) {Margin=new Thickness(17,21,13,19)};
        var grid=new Grid();grid.RowDefinitions.Add(new(){Height=new GridLength(80)});grid.RowDefinitions.Add(new());
        grid.Children.Add(new Border {Background=Brushes.Magenta});Grid.SetRow(control,1);grid.Children.Add(control);
        var host=new Window {Content=grid,Width=1500,Height=950,ShowInTaskbar=false,ShowActivated=false};WatchAppearance(host);host.Show();Wait(400);
        try
        {
            var state=(ModelViewerViewModel)control.DataContext;state.ShowInspector=false;state.SelectedPanel=1;Wait(100);
            var viewport=Children<Viewport3DX>(control).Single();
            foreach(var size in new[]{(Width:1500.0,Height:950.0),(Width:1120.0,Height:720.0)})
            {
                host.Width=size.Width;host.Height=size.Height;Wait(150);
                string name="incorporata-"+size.Width;
                ViewerImageExporter.Save(control,viewport,Path.Combine(output,name+".png"));
                Capture(control,viewport,Path.Combine(output,name+"-runner.png"),100);
                var offset=VisualTreeHelper.GetOffset(control);
                File.WriteAllText(Path.Combine(output,name+"-layout.json"),System.Text.Json.JsonSerializer.Serialize(new{control.ActualWidth,control.ActualHeight,Offset=offset.ToString(),Bounds=VisualTreeHelper.GetDescendantBounds(control).ToString()}));
                foreach(bool withOffset in new[]{false,true})
                {
                    var visual=new DrawingVisual();
                    using(var dc=visual.RenderOpen())dc.DrawRectangle(new VisualBrush(control){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(withOffset?new Point(offset.X,offset.Y):new Point(),control.RenderSize)},null,new Rect(control.RenderSize));
                    var bitmap=new RenderTargetBitmap((int)control.ActualWidth,(int)control.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,name+(withOffset?"-offset.png":"-zero.png")));encoder.Save(file);
                }
                if(captureBefore)continue;
                using var stream=File.OpenRead(Path.Combine(output,name+".png"));var pixels=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
                var rgba=new byte[4];pixels.CopyPixels(new Int32Rect(3,3,1,1),rgba,4,0);
                var paper=((SolidColorBrush)Application.Current.Resources["AppearancePaper"]).Color;
                Check(rgba[0]==paper.B&&rgba[1]==paper.G&&rgba[2]==paper.R&&rgba[3]==255,"embedded export starts at the viewer background without the host margin at width "+size.Width);
                pixels.CopyPixels(new Int32Rect(pixels.PixelWidth-4,3,1,1),rgba,4,0);
                Check(rgba[0]==paper.B&&rgba[1]==paper.G&&rgba[2]==paper.R&&rgba[3]==255,"resized export fills its actual width instead of scaling retained Direct3D bounds at width "+size.Width);
            }
            var scrollbar=Children<ScrollBar>(control).First(s=>s.IsVisible && s.Orientation==Orientation.Vertical);
            Check(scrollbar.Template==Application.Current.FindResource("AppearanceVerticalScrollBarTemplate"),"a new dark window receives themed scrollbars at its first opening");
            state.ViewName="Preferenza di prova";state.SaveViewCommand.Execute(null);
            Check(store.Read(state.Snapshot!.Fingerprint()).Any(v=>v.Name=="Preferenza di prova"),"preview view preferences are saved through the injected store");
            using var reopened=new ModelViewerControl(Sample.Model(),[],false,_=>{},(_,_,_,_)=>{},store);
            Check(((ModelViewerViewModel)reopened.DataContext).SavedViews.Any(v=>v.Name=="Preferenza di prova"),"a new viewer restores the dedicated preview preferences without using the app store");
            host.Width=1500;host.Height=950;Wait(150);
            state.MemberDiagramElementId=12;state.MemberDiagramAllComponents=true;state.OpenMemberDiagramCommand.Execute(null);
            // Deliberately do not pump a layout/render pass between opening the drawer and exporting.
            string immediate=Path.Combine(output,"diagrammi-immediati.png");
            ViewerImageExporter.Save(control,viewport,immediate);
            Capture(control,viewport,Path.Combine(output,"diagrammi-immediati-runner.png"),100);
            using var frame=File.OpenRead(Path.Combine(output,"diagrammi-immediati-runner.viewport.png"));
            var native=BitmapDecoder.Create(frame,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
            var dpi=VisualTreeHelper.GetDpi(viewport);
            Check(Math.Abs(native.PixelWidth-viewport.ActualWidth*dpi.DpiScaleX)<3 && Math.Abs(native.PixelHeight-viewport.ActualHeight*dpi.DpiScaleY)<3,"immediate drawer export renders at the new viewport size instead of compressing the previous frame");
        }
        finally {host.Close();}
    }
}
