using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;
namespace X.Desktop;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode=ShutdownMode.OnExplicitShutdown;
        string path=e.Args[0], output=e.Args[1];Directory.CreateDirectory(output);
        DispatcherUnhandledException+=(_,err)=>{File.AppendAllText(Path.Combine(output,"fatal.txt"),err.Exception.ToString());err.Handled=true;Shutdown(1);};
        EventManager.RegisterClassHandler(typeof(FrameworkElement),ToolTipService.ToolTipOpeningEvent,new ToolTipEventHandler((_,a)=>a.Handled=true));
        Dispatcher.InvokeAsync(async ()=> {
            var manifest=new JsonArray();
            foreach(var c in JsonNode.Parse(File.ReadAllText(path))!.AsArray().OfType<JsonObject>())
            {
                string id=c.S("id"); Window? win=null; IDisposable? content=null;
                try
                {
                    File.AppendAllText(Path.Combine(output,"progress.log"),id+" start\n");
                    if(File.Exists(Path.Combine(output,id+".png")))continue;
                    var data=(JsonObject)c["data"]!.DeepClone();
                    FrameworkElement view;
                    if(c.S("module")=="bridge")
                    {
                        var full=BridgeSection.Defaults();foreach(var (key,value) in data)full[key]=value?.DeepClone();data=full;
                        data["metodo_analisi"]=BridgeSection.CalculationMethods[(int)data.D("methodIndex")];
                        if(data["curve_sezione"] is JsonObject q){q["origine"]=BridgeSection.ResponseOrigins[(int)q.D("originIndex")];q["tipo"]=BridgeSection.ResponseModes[(int)q.D("typeIndex")];}
                        view=new BridgeWorkspace(data);
                    }
                    else {var caView=new ConcreteWorkspace(data);caView.PrepareValidationCapture(c);view=caView;}
                    content=(IDisposable)view;
                    win=new Window{Title="ANTHEA · Validazione · "+id,Width=1500,Height=950,Content=view,Background=Ui.Bg,ShowActivated=false};
                    win.Show();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    if(view is ConcreteWorkspace ca)await ca.ValidationCapture(c,output,id);
                    else if(view is BridgeWorkspace b)
                    {
                        await b.CalculateAsync();
                        if(c.B("curve")){b.Pages.SelectedIndex=2;await b.CalculateResponseAsync();b.ResponsePoint.Value=b.ResponseCalculation?.Points.Count-1??0;}
                        else{b.Pages.SelectedIndex=1;b.StageChoice.SelectedIndex=b.StageChoice.Items.Count-1;b.DisplayChoice.SelectedIndex=0;b.Results.SelectedIndex=(int)c.D("resultTab",3);}
                        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);win.UpdateLayout();
                        File.WriteAllBytes(Path.Combine(output,id+".png"),Ui.Snapshot(win));
                        File.WriteAllText(Path.Combine(output,id+"-actual.json"),(b.Result??new JsonObject()).ToJsonString());
                    }
                    File.WriteAllText(Path.Combine(output,id+"-input.json"),data.ToJsonString());
                    manifest.Add(J.Obj(("id",id),("ok",true),("note",c.S("note"))));
                    File.AppendAllText(Path.Combine(output,"progress.log"),id+" OK\n");
                }
                catch(Exception ex){File.WriteAllText(Path.Combine(output,id+"-error.txt"),ex.ToString());manifest.Add(J.Obj(("id",id),("ok",false)));}
                finally{content?.Dispose();win?.Close();}
                File.WriteAllText(Path.Combine(output,"capture-manifest.json"),manifest.ToJsonString());
            }
            Shutdown();
        });
    }
}
internal sealed partial class ConcreteWorkspace
{
    internal void PrepareValidationCapture(JsonObject c)
    {
        pendingCalculations.Clear();
        if(c.S("mode")=="domain")pendingCalculations.Add(((int)c.D("tab")==1?"3D:":"2D:")+c.S("state","SLU"));
        else if(c.S("mode")=="stress")pendingCalculations.Add(c.S("set","SLE_QP"));
        else if(c.S("mode")=="shear")pendingCalculations.Add("Taglio");
    }
    internal async Task ValidationCapture(JsonObject c,string output,string id)
    {
        string mode=c.S("mode");
        // Use the production controls and production calculation entry points.
        // No numerical result is injected into a screen.
        while(Busy||calculationQueued)await Task.Delay(100);
        if(mode=="material")
        {
            var dialog=CreateMaterialDialog(c.B("steel")?"Acciaio":"Calcestruzzo",_=>{});
            dialog.Show();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var form=Ui.Descendants<InputForm>(dialog).First();
            foreach(var key in new[]{"fck_mpa","cls_diagramma","steel_modulus_mpa","fyk_mpa","steel_fu_mpa","steel_eps_u","steel_diagramma"})
                if(form.Editors.ContainsKey(key))form.Set(key,Input.S(key));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);dialog.UpdateLayout();
            File.WriteAllBytes(Path.Combine(output,id+".png"),Ui.Snapshot(dialog));dialog.Close();return;
        }
        int tab=(int)c.D("tab");tabs.SelectedIndex=tab;
        if(mode=="domain")
        {
            var panel=domainPanels[tab-1];panel.Mode.SelectedItem=c.S("state","SLU");
            panel.Grid.SelectedItem=actions[c.S("state","SLU")].FirstOrDefault();UpdateSelection(panel);
        }
        else if(mode=="stress")
        {
            string set=c.S("set","SLE_QP");sleTabs.SelectedIndex=Array.IndexOf(SectionWorkspace.Sets,set)-2;
            stressPanels[set].Grid.SelectedItem=actions[set].FirstOrDefault();UpdateStressSelection(set);
        }
        else if(mode=="shear"){CalculateShear();shearGrid!.SelectedIndex=0;UpdateShearSelection();}
        else if(mode=="detail"||mode=="anchor"||mode=="cover")
        {RefreshDetailing();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);if(mode=="anchor"&&((TabItem)tabs.Items[5]).Content is ScrollViewer scroller){scroller.ScrollToBottom();workspaceScroll.ScrollToTop();}else if(mode=="cover")detailingTopics["cover"].BringIntoView();}
        else if(mode=="quick")await RefreshQuickResistance();
        else if(mode=="curve"){CalculateCurvature();while(curvatureRunning)await Task.Delay(100);}
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);UpdateLayout();
        File.WriteAllBytes(Path.Combine(output,id+".png"),Ui.Snapshot(Window.GetWindow(this)));
        File.WriteAllText(Path.Combine(output,id+"-actual.json"),(Result??new JsonObject()).ToJsonString());
    }
}
