using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using X.Core;
using X.Desktop;

internal static class Program
{
    [STAThread] static void Main(string[] args)
    {
        Directory.CreateDirectory(args[0]);
        var app=new App();app.InitializeComponent();
        var window=new MainWindow();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        object? Invoke(string name,params object[] values)=>typeof(MainWindow).GetMethod(name,flags)!.Invoke(window,values);
        try
        {
            var toolbar=(WrapPanel)Invoke("FileCommands",true,true)!;
            var shortButton=toolbar.Children.OfType<Button>().Single(b=>b.Content?.ToString()=="Report short");
            if(shortButton.Visibility!=Visibility.Collapsed)throw new Exception("CLS button visible outside section module");
            Invoke("ShowSheet",Archivio.Documento("str_palo"));
            if(shortButton.Visibility!=Visibility.Visible)throw new Exception("CLS button not visible");
            int index=toolbar.Children.IndexOf(shortButton);
            if(index<1 || ((Button)toolbar.Children[index-1]).ToolTip?.ToString()!="Report Word")throw new Exception("Button not next to Word report");
            var panel=new Border{Background=new SolidColorBrush(Color.FromRgb(27,44,63)),Padding=new Thickness(12),Child=toolbar};
            panel.Measure(new Size(620,85));panel.Arrange(new Rect(0,0,620,85));panel.UpdateLayout();
            var bitmap=new RenderTargetBitmap(620,85,96,96,PixelFormats.Pbgra32);bitmap.Render(panel);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(args[0],"pulsante-report-short.png")))png.Save(stream);
            Invoke("ShowSheet",Archivio.Documento("geo_palo_verticale"));
            if(shortButton.Visibility!=Visibility.Collapsed)throw new Exception("CLS button remains in another module");
            File.WriteAllText(Path.Combine(args[0],"ui-test.txt"),"PASS: adjacent to Report Word, visible for CLS, hidden for other modules.");
            Console.WriteLine("PASS: toolbar placement and module visibility");
        }
        finally{Invoke("FinishSmoke");window.Close();app.Shutdown();}
    }
}
