using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

// --capture-validation <ingressi.json> <cartella> (AppHarness): screenshots of the illustrated validation of 26/09/2026
// (supporto/scripts/validazione_illustrata_2026_09_26). Moved unchanged from supporto/test/ValidationIllustrations/App.cs,
// which recompiled the sources of X.Desktop and no longer built (refactoring F1.6).
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
