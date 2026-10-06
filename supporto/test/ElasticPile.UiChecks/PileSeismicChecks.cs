using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anthea.Calculations;
using X.Core;

static partial class Program
{
    static int SeismicUiChecks(string directory)
    {
        var root=Input();var e=root["elastico"]!;var settings=e["dettagli"]!;
        var section=root["sezione"]!;section["longitudinal_bar_count"]=24;section["longitudinal_bar_diameter_mm"]=24;section["transverse_spacing_mm"]=140;
        settings["azioni_progetto"]=true;settings["taglio_confermato"]=true;
        e["tratti"]=new JsonArray(ElasticHorizontalPile.NewSegment("T1",6),ElasticHorizontalPile.NewSegment("T2",10.5),ElasticHorizontalPile.NewSegment("T3",null));
        foreach(var row in e.Array("tratti").Skip(1)){row!["collegato"]=false;foreach(var key in ElasticHorizontalPile.ReinforcementKeys)row[key]=section[key]!.DeepClone();row["longitudinal_bar_count"]=12;row["longitudinal_bar_diameter_mm"]=20;row["transverse_spacing_mm"]=150;}
        Check(!settings.B("sisma_testa"),"old archives migrate with explicit inactive seismic mode");
        var baseline=ElasticHorizontalPile.CalculateResponse(root);settings["sisma_testa"]=true;settings["sisma_lunghezza"]=10.13;
        var response=ElasticHorizontalPile.CalculateResponse(root);var cache=new ElasticPileVerificationCache();
        var result=ElasticHorizontalPile.CompleteReinforcement(root,response,cache);
        Check(response["risposta"]!.Array("Nodes").Any(n=>Math.Abs(n.D("Depth")-10.13)<1e-9),"exact seismic zone end inserted in FEM mesh");
        Check(response["input"]!["zona_sismica"].D("NominalLength")==10&&response["input"]!["zona_sismica"].D("AdoptedEnd")==10.13,"seismic graphic available from FEM before resistance calculations");
        Check(Math.Abs(response["risposta"].D("HeadDisplacement")-baseline["risposta"].D("HeadDisplacement"))<1e-5,"zone node changes discretization only, not assigned actions");
        var records=result["armature"]!.Array("tratti");JsonNode S(int index)=>records[index]!["sisma_testa"]!;
        JsonNode C(int index,string key)=>S(index).Array("Checks").Single(c=>c.S("Key")==key)!;
        Check(S(0).B("IntersectsHeadZone")&&S(1).B("IntersectsHeadZone")&&!S(2).B("IntersectsHeadZone"),"head zone crosses segment boundary and excludes third segment");
        Check(C(0,"SeismicSteelArea").B("Passed")&&!C(1,"SeismicSteelArea").B("Passed")&&C(2,"SeismicSteelArea").B("Passed"),"same lower cage fails1percent but passes0.3percent outside head");
        Check(!C(1,"SeismicLinkSpacing").B("Passed")&&C(2,"SeismicLinkSpacing").B("Passed"),"conditional6phi and8phi use local section");
        Check(C(0,"SeismicShear")["Passed"]==null&&C(0,"SeismicElasticMoment")["Passed"]==null,"unconfirmed loads remain pending in serialized result");
        Check(records[1].S("stato").Contains("sismici")&&records[1]!.Array("da_completare").Any(v=>v!.ToString().Contains("Armatura longitudinale")),"failed seismic detail is included in segment verdict and pending summary");
        settings["sisma_azioni"]=true;settings["sisma_elastico"]=true;settings["sisma_staffe"]="Staffe singole";
        Check(ElasticHorizontalPile.ResponseKey(root)==response.S("chiave_risposta"),"seismic action declarations do not invalidate physical response");
        result=ElasticHorizontalPile.CompleteReinforcement(root,response,cache);records=result["armature"]!.Array("tratti");
        Check(C(0,"SeismicShear")["Passed"]!=null&&C(0,"SeismicCompression")["Passed"]!=null&&C(0,"SeismicElasticMoment")["Passed"]!=null,"confirmed inputs enable numerical supplementary checks");
        Check(records.SelectMany(r=>r!.Array("dettagli_barre")).Any(),"seismic checks preserve cutting schedule");
        var proposal=ElasticHorizontalPile.DesignSegment(root,result,1,[12,24],[20,24],[10],[100,150]);
        Check(proposal.D("longitudinal_bar_count")==24&&proposal.D("longitudinal_bar_diameter_mm")==24&&proposal.D("transverse_spacing_mm")==100,"dimensioning rejects candidates below1percent or exceeding6phi");
        var doc=Archivio.Documento(PaloOrizzontale.Module);doc["dati"]=root;string path=Path.Combine(directory,"palo-sisma.programma");Archivio.Scrivi(path,doc);
        var reopened=Archivio.Leggi(path)["dati"]!.AsObject();Check(JsonNode.DeepEquals(root,reopened),"seismic mode and confirmations survive archive roundtrip");
        string keyBefore=ElasticHorizontalPile.ResponseKey(root);root["sezione"]!["longitudinal_bar_count"]=20;Check(ElasticHorizontalPile.ResponseKey(root)==keyBefore,"steel changes keep NVM independent with seismic mode");root["sezione"]!["longitudinal_bar_count"]=24;
        string csv=ElasticHorizontalPile.Csv(result);Check(csv.Contains("SeismicElasticMoment")&&csv.Contains("sisma_azioni"),"CSV contains seismic criteria and input provenance");
        var report=ReportElasticPile.Create("Controlli sismici di testa · prova",result);File.WriteAllBytes(Path.Combine(directory,"report-sisma.docx"),report);
        using(var zip=new System.IO.Compression.ZipArchive(new MemoryStream(report))){using var reader=new StreamReader(zip.GetEntry("word/document.xml")!.Open());string xml=reader.ReadToEnd();Check(xml.Contains("Controllo sismico")&&xml.Contains("Momento da analisi elastica")&&xml.Contains("pagina stampata 213"),"Word report exposes seismic checks and actual source");}
        var app=new TestApp();app.LoadStyles();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;capture=true;int changes=0;
        var type=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.PileReinforcementEditor")!;var editor=(FrameworkElement)Activator.CreateInstance(type,flags,null,[root,(Action)(()=>changes++),(Func<JsonObject?>)(()=>result),null],null)!;
        type.GetField("checkMode",flags)!.SetValue(editor,"Sisma · testa palo");type.GetMethod("UpdateSummary",flags)!.Invoke(editor,null);
        Snapshot(editor,Path.Combine(directory,"sisma-editor.png"),1600,1500);
        Check(Descendants<ComboBox>(editor).Any(c=>c.Items.Cast<object>().Any(v=>v.ToString()=="Sisma · testa palo")),"seismic checks selectable beside reinforcement segments");
        Check(Descendants<Expander>(editor).Any(x=>x.Header?.ToString()?.StartsWith("Sisma · testa")==true),"conditional seismic inputs grouped in dedicated panel");
        var enable=Descendants<CheckBox>(editor).First(c=>Descendants<TextBlock>(c).Any(t=>t.Text.StartsWith("Controlla zona dissipativa")));enable.IsChecked=false;enable.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check(changes>0&&!settings.B("sisma_testa"),"seismic mode changes invalidate using existing recalculation callback");enable.IsChecked=true;enable.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        var drawingType=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.PileReinforcementDrawing")!;var drawing=(FrameworkElement)Activator.CreateInstance(drawingType,true)!;drawingType.GetMethod("Set",flags)!.Invoke(drawing,[result,null]);var size=(Size)drawingType.GetProperty("SheetSize",flags)!.GetValue(drawing)!;
        Snapshot(drawing,Path.Combine(directory,"tavola-sisma.png"),(int)size.Width,(int)size.Height);
        var profileType=typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.ElasticPileDrawing")!;var profile=(FrameworkElement)Activator.CreateInstance(profileType,true)!;
        var view=new JsonObject();profileType.GetProperty("Options",flags)!.SetValue(profile,view);profileType.GetMethod("Set",flags)!.Invoke(profile,[result,false]);
        string unchanged=result.ToJsonString();Snapshot(profile,Path.Combine(directory,"profilo-zona-10D.png"),1900,760);
        Check(result.ToJsonString()==unchanged,"seismic shading and reference lines do not modify calculation results");
        view["sisma"]=false;profile.InvalidateVisual();Snapshot(profile,Path.Combine(directory,"profilo-zona-nascosta.png"),1900,760);
        Check(result.ToJsonString()==unchanged,"zone visibility toggle changes display only");
        File.WriteAllText(Path.Combine(directory,"input.json"),root.ToJsonString(J.Options));File.WriteAllText(Path.Combine(directory,"result.json"),result.ToJsonString(J.Options));File.WriteAllText(Path.Combine(directory,"risultati.csv"),csv);
        File.WriteAllText(Path.Combine(directory,"ui-checks.txt"),$"PASS {checks} seismic checks");Console.WriteLine($"TOTAL {checks} seismic UI checks");return 0;
    }
}
