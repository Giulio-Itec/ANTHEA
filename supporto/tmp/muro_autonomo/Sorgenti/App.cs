using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Anthea.Muro;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if(args.Length==2 && args[0]=="--example")
        {
            Directory.CreateDirectory(args[1]);var input=Input.Example();var r=Engine.Calculate(input);
            Engine.Save(Path.Combine(args[1],"Esempio H3.muro.json"),input);
            File.WriteAllText(Path.Combine(args[1],"Relazione esempio.html"),Report.Html(r));
            File.WriteAllText(Path.Combine(args[1],"Risultati esempio.json"),System.Text.Json.JsonSerializer.Serialize(r,Engine.JsonOptions));return 0;
        }
        if(args.Length==2 && args[0]=="--test")
        {
            try{File.WriteAllText(args[1],Tests.Run());return 0;}catch(Exception ex){File.WriteAllText(args[1],ex.ToString());return 1;}
        }
        var app=new Application();var window=new MainWindow();
        if(args.Length==2 && args[0]=="--smoke")
        {
            window.Testing=true;
            Directory.CreateDirectory(args[1]);
            window.Loaded+=async (_,_)=>
            {
                try
                {
                    window.LoadInput(Input.Example());await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                    window.Snapshot(Path.Combine(args[1],"ampia.png"));
                    window.ScrollCalculationToBottom();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                    window.Snapshot(Path.Combine(args[1],"risultati.png"));
                    window.Width=850;window.Height=720;await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                    window.Snapshot(Path.Combine(args[1],"compatta.png"));
                    window.SelectDetails();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                    window.Snapshot(Path.Combine(args[1],"dettagli.png"));
                    window.SelectMethod();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
                    window.Snapshot(Path.Combine(args[1],"metodo.png"));
                    if(!window.HasResult)throw new Exception("Esempio non calcolato.");
                    window.SetInput("H","");if(window.HasResult)throw new Exception("Risultati obsoleti dopo modifica.");
                    window.Recalculate();if(window.HasResult)throw new Exception("Campo vuoto accettato.");
                    File.WriteAllText(Path.Combine(args[1],"smoke.txt"),"OK: rendering ampio e compatto, dettagli, esempio, invalidazione immediata e campo vuoto.");
                    app.Shutdown(0);
                }
                catch(Exception ex){File.WriteAllText(Path.Combine(args[1],"errore.txt"),ex.ToString());app.Shutdown(1);}
            };
        }
        return app.Run(window);
    }
}
