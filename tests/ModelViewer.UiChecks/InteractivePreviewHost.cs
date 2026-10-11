using System.Windows;
using System.Windows.Controls;
using ANTHEA.ModelWorkspace;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;

/// <summary>Controls for the disposable test window only. Never contacts MIDAS or changes source files.</summary>
internal static class InteractivePreviewHost
{
    public static FrameworkElement Create(ModelViewerControl viewer)
    {
        var state=(ModelViewerViewModel)viewer.DataContext;
        var host=new DockPanel();
        var bar=new WrapPanel { Margin=new Thickness(12,7,12,7), VerticalAlignment=VerticalAlignment.Center };
        bar.Children.Add(new TextBlock { Text="PROVA INTERATTIVA · copia offline", FontWeight=FontWeights.SemiBold,
            VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(0,0,18,0) });
        var settings=new Button { Content="Apri impostazioni vista", Margin=new Thickness(0,0,12,0), Padding=new Thickness(10,5,10,5) };
        settings.Click+=(_,_)=>state.ShowViewSettings=true;bar.Children.Add(settings);
        var kinds=new[]{ModelSourceFailureKind.SessionMissing,ModelSourceFailureKind.DocumentClosed,ModelSourceFailureKind.Timeout,ModelSourceFailureKind.AccessDenied,ModelSourceFailureKind.Unavailable};
        var failures=new ComboBox { ItemsSource=new[]{"Sessione MIDAS scaduta","Modello MIDAS chiuso","Tempo di risposta superato","Accesso rifiutato","MIDAS non raggiungibile"},SelectedIndex=0,
            Width=230,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0) };
        bar.Children.Add(failures);
        var simulate=new Button { Content="Simula avviso",Padding=new Thickness(10,5,10,5),ToolTip="Solo una prova del messaggio: nessuna richiesta a MIDAS." };
        simulate.Click+=(_,_)=>
        {
            var failure=new ModelSourceException(kinds[Math.Clamp(failures.SelectedIndex,0,kinds.Length-1)]);
            state.ImportFailure=new(failure.Message,failure.Recovery,"AVVISO SIMULATO · la copia del modello rimane disponibile. Nessuna connessione a MIDAS effettuata.");
        };
        bar.Children.Add(simulate);
        var strip=new Border { Child=bar,BorderThickness=new Thickness(0,0,0,1) };
        strip.SetResourceReference(Border.BackgroundProperty,"AppearanceSurface");strip.SetResourceReference(Border.BorderBrushProperty,"AppearanceBorder");
        DockPanel.SetDock(strip,Dock.Top);host.Children.Add(strip);host.Children.Add(viewer);
        return host;
    }
}
