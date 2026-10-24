using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anthea.Calculations;
using X.Core;

static partial class Program
{
    static int ClarityChecks(string dir)
    {
        var root=Input();var settings=root["elastico"]!["dettagli"]!;
        var rows=new JsonArray(ElasticHorizontalPile.NewSegment("T1",6),ElasticHorizontalPile.NewSegment("T2",null));root["elastico"]!["tratti"]=rows;
        rows[1]!["collegato"]=false;rows[1]!["tipo_trasversale"]="Spirale";
        root["sezione"]!["longitudinal_bar_count"]=24;root["sezione"]!["longitudinal_bar_diameter_mm"]=24;root["sezione"]!["transverse_spacing_mm"]=140;
        var response=ElasticHorizontalPile.CalculateResponse(root);string physical=response.S("chiave_risposta"),section=root["sezione"]!.ToJsonString(),partition=rows.ToJsonString();
        ElasticHorizontalPile.ApproveReinforcementAssumptions(root,false);
        Check(ElasticHorizontalPile.OrdinaryApprovalKeys.All(k=>settings.B(k)),"approve all ordinary assumptions");
        Check(!settings.B("sisma_azioni")&&!settings.B("sisma_elastico")&&!settings.B("sisma_testa"),"ordinary approval does not silently activate seismic analysis");
        ElasticHorizontalPile.ApproveReinforcementAssumptions(root,true);
        Check(settings.B("sisma_azioni")&&settings.B("sisma_elastico"),"seismic approval confirms action provenance");
        Check(root["sezione"]!.ToJsonString()==section&&rows.ToJsonString()==partition&&settings.D("z_d")==.9,"approval preserves section, numerical constraints and custom spiral");
        Check(ElasticHorizontalPile.ResponseKey(root)==physical,"approvals and reinforcement type preserve FEM cache");
        var cache=new ElasticPileVerificationCache();JsonObject? result=ElasticHorizontalPile.CompleteReinforcement(root,response,cache);
        var records=result["armature"]!.Array("tratti");
        Check(records[0]!["distinta_staffe"].S("tipo")=="Staffe singole"&&records[1]!["distinta_staffe"].S("tipo")=="Spirale","per-segment type used in schedule");
        Check(records[1]!["armatura_trasversale"].Array("Projection").Count>20&&records[1]!["distinta_staffe"].D("spire")>0,"spiral geometry comes from Checker");
        Check(records[1]!.Array("verifiche").All(c=>c?["VRd"]==null)&&records[1]!["riepilogo"].Array("da_completare").Any(x=>x!.ToString().Contains("spirale")),"approve all does not certify unsupported spiral shear");
        Check(records[1]!.Array("verifiche").All(c=>c.S("Message").Contains("Taglio con spirale non verificato")&&!c.S("Message").Contains("Confermare il modello di taglio")),"section messages identify unsupported spiral instead of requesting an already approved assumption");
        Check(records.All(r=>r?["riepilogo"].Array("non_soddisfatti").Count>0),"insufficient bar development remains clearly nonverified after approval");
        var successful=ElasticHorizontalPile.ReinforcementSummary(J.Obj(("verifiche",new JsonArray(J.Obj(("Status","Soddisfatto")))),("da_completare",new JsonArray("Non eseguiti: SLE")),("da_completare_tipizzati",new JsonArray(J.Obj(("Kind",2),("Text","Non eseguiti: SLE"),("Summary","SLE"))))));
        Check(successful.B("eseguiti_soddisfatti")&&successful.Array("esclusi").Count==1&&successful.Array("non_soddisfatti").Count==0,"green success explicitly restricted to executed checks");
        var archived=JsonNode.Parse(root.ToJsonString())!.AsObject();ElasticHorizontalPile.PrepareReinforcement(archived);
        Check(ElasticHorizontalPile.TransverseKind(archived,archived["elastico"]!.Array("tratti")[1])=="Spirale","custom spiral persists in archive");
        settings["sisma_testa"]=true;response=ElasticHorizontalPile.CalculateResponse(root);result=ElasticHorizontalPile.CompleteReinforcement(root,response,cache);
        var spiralSeismic=result["armature"]!.Array("tratti")[1]!["sisma_testa"]!;
        Check(spiralSeismic.Array("Checks").Any(c=>c.S("Key")=="SeismicSingleHoops"&&!c.B("Passed")),"spiral fails single-hoop requirement even after seismic approval");
        var app=new TestApp();app.LoadStyles();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;capture=Environment.GetCommandLineArgs().Contains("--images");int changed=0;
        System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
        // f5-pass-orizzontale: the XAML view of the reinforcement in the elastic tab of a sheet on the same data; an invalidation is a Modified of the sheet
        // with the result of the elastic response cleared (the historic check counted the callback of the editor and cleared its result).
        var input=(JsonObject)root.DeepClone();input["vista_orizzontale"]="elastico";
        using(var host=new PileHost(input,width:1600,height:1550))
        {
            Wait(host.Elastic.CalculateAsync());host.Settle();var editor=host.ReinforcementView;var hostRows=host.Data["elastico"]!.Array("tratti");
            host.Sheet.Modified+=(_,_)=>changed++;
            Picture(editor,Path.Combine(dir,"riepilogo.png"));
            var button=Descendants<Button>(editor).Single(b=>b.Content?.ToString()=="Approva tutto · ipotesi");PileHost.Press(button);host.Settle();
            Check(changed==1&&host.Elastic.Result==null,"approval triggers immediate result invalidation exactly once");
            Picture(editor,Path.Combine(dir,"invalidazione.png"));
            var sb=Descendants<Button>(editor).Single(b=>b.Content?.ToString()=="Approva tutto · sisma");PileHost.Press(sb);host.Settle();Check(changed==2,"seismic approve button wired to recalculation");
            Picture(editor,Path.Combine(dir,"controlli.png"));
            var global=Descendants<ComboBox>(editor).First(c=>c.Items.Contains("Spirale"));global.SelectedItem="Spirale";host.Settle();
            Check(ElasticHorizontalPile.TransverseKind(host.Data,hostRows[0])=="Spirale"&&ElasticHorizontalPile.TransverseKind(host.Data,hostRows[1])=="Spirale","global dropdown updates inherited transverse type");
            Picture(editor,Path.Combine(dir,"controlli-spirale.png"));
            Descendants<ComboBox>(editor).First(c=>c.Items.Contains("Spirale")).SelectedItem="Staffe singole";host.Settle();
            Check(ElasticHorizontalPile.TransverseKind(host.Data,hostRows[0])=="Staffe singole"&&ElasticHorizontalPile.TransverseKind(host.Data,hostRows[1])=="Spirale","main type change preserves personalized spiral");
            // «NTC §7.2.5»: the dialog of the view (ElasticNtcView, CreateNtcWindow of the historic editor), shown off screen without waiting.
            Window? window=null;var showWindow=editor.GetType().GetProperty("ShowWindow",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
            showWindow.SetValue(editor,(Func<Window,bool?>)(w=>{w.WindowStartupLocation=WindowStartupLocation.Manual;w.Left=-32000;w.Top=-32000;w.ShowActivated=false;w.ShowInTaskbar=false;w.Show();window=w;return null;}));
            PileHost.Press(Descendants<Button>(editor).First(b=>b.Content?.ToString()=="NTC §7.2.5"));host.Settle();
            var tabs=Descendants<TabControl>(window??throw new Exception("NTC window not shown")).First();Picture(tabs,Path.Combine(dir,"ntc-info.png"));
            tabs.SelectedIndex=1;window.UpdateLayout();host.Settle();Picture(tabs,Path.Combine(dir,"ntc-pagina.png"));
            Check(Descendants<Image>(tabs).Any(i=>i.Source!=null),"official NTC page embedded in internal window with no network");window.Close();
        }
        result=ElasticHorizontalPile.CompleteReinforcement(root,response,cache);
        var drawingType=typeof(X.Desktop.App).Assembly.GetType("X.Desktop.PileReinforcementDrawing")!;var drawing=(FrameworkElement)Activator.CreateInstance(drawingType,true)!;
        drawingType.GetMethod("Set",flags)!.Invoke(drawing,[result,null]);var size=(Size)drawingType.GetProperty("SheetSize",flags)!.GetValue(drawing)!;
        Snapshot(drawing,Path.Combine(dir,"staffe-spirale.png"),(int)size.Width,(int)size.Height);
        var report=ReportElasticPile.Create("Palo con staffe e spirale",result);File.WriteAllBytes(Path.Combine(dir,"report.docx"),report);
        using(var zip=new System.IO.Compression.ZipArchive(new MemoryStream(report))){using var reader=new StreamReader(zip.GetEntry("word/document.xml")!.Open());var xml=reader.ReadToEnd();Check(xml.Contains("Spirale")&&xml.Contains("Fuori dal perimetro")&&xml.Contains("lunghezza geometrica"),"report preserves type, geometric quantities and verification scope");}
        File.WriteAllText(Path.Combine(dir,"result.json"),result.ToJsonString(J.Options));File.WriteAllText(Path.Combine(dir,"input.json"),root.ToJsonString(J.Options));
        File.WriteAllText(Path.Combine(dir,"ui-checks.txt"),$"PASS {checks} clarity checks");Console.WriteLine($"TOTAL {checks} clarity checks");return 0;
    }
}
