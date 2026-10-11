using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ANTHEA.ModelWorkspace;

static partial class Program
{
    static double previewReadMilliseconds;
    static ModelSnapshot ReadPreviewSnapshot(string path,bool interactive)
    {
        if(!Path.GetExtension(path).Equals(".antheamodel",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("L’apertura diretta richiede un pacchetto .antheamodel.");
        using var cancellation=new CancellationTokenSource();
        var stack=new StackPanel {Margin=new Thickness(26)};
        var title=new TextBlock {Text="ANTHEA · Modelli",FontSize=22,FontWeight=FontWeights.SemiBold};title.SetResourceReference(TextBlock.ForegroundProperty,"AppearanceInk");stack.Children.Add(title);
        var text=new TextBlock {Text="Lettura e controllo della copia salvata…",Margin=new Thickness(0,14,0,12)};text.SetResourceReference(TextBlock.ForegroundProperty,"AppearanceInk");stack.Children.Add(text);
        stack.Children.Add(new ProgressBar {Height=5,IsIndeterminate=true});
        var detail=new TextBlock {Text=Path.GetFileName(path),Margin=new Thickness(0,12,0,0),TextTrimming=TextTrimming.CharacterEllipsis};detail.SetResourceReference(TextBlock.ForegroundProperty,"AppearanceInk");stack.Children.Add(detail);
        var window=new Window {Title="ANTHEA · caricamento modello",Content=stack,Width=470,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterScreen,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=interactive,ShowActivated=interactive};
        window.SetResourceReference(Window.BackgroundProperty,"AppearancePaper");window.Closed+=(_,_)=>cancellation.Cancel();window.Show();
        var watch=Stopwatch.StartNew();
        try
        {
            var read=Task.Run(()=>ModelSnapshotFiles.Read(path,cancellation.Token),cancellation.Token);
            while(!read.IsCompleted)Wait(25);
            var snapshot=read.GetAwaiter().GetResult();previewReadMilliseconds=watch.Elapsed.TotalMilliseconds;return snapshot;
        }
        finally{window.Close();}
    }
}
