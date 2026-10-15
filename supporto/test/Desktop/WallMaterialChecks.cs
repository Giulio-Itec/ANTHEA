using System.IO;
using System.Windows;
using System.Windows.Controls;
using X.Core;
using Anthea.Application.Documents;

namespace X.Desktop;
internal static class WallMaterialChecks
{
    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var log = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); log.Add("OK " + message); }
        var d = RetainingWall.Defaults(); using var view = new RetainingWallWorkspace(d);
        var form = view.Forms["materials"];
        Check(Application.Current.Windows.Count == 0,"Nessuna finestra nativa");
        // DET-3 (seguito del 16/10/2026): acciaio del muro salvato come B450A con εu = 30 ‰ (catalogo precedente). La scheda Materiali lo dichiara
        // con la nota del campo, come la sezione c.a. e il palo orizzontale, senza riscrivere il valore; con l'acciaio del catalogo nessuna nota.
        const string det3 = "Valori salvati diversi dal catalogo B450A: εu = 30 ‰ (catalogo 25 ‰). Il calcolo usa i valori salvati; riselezionare B450A per adottare quelli del catalogo.";
        IEnumerable<TextBlock> Notes(RetainingWallWorkspace w) => Ui.Descendants<TextBlock>(w.Forms["materials"]).Where(t => t.Text.StartsWith("Valori salvati diversi dal catalogo") && t.Parent is FrameworkElement { Visibility: Visibility.Visible });
        var saved = RetainingWall.Defaults(); saved["materials"]!["classe_acciaio"] = "B450A";
        RetainingWall.ApplyMaterialPreset(saved, Anthea.Application.Materials.MaterialInputCatalog.Steel(false, "NTC 2018").Single(x => x.S("nome") == "B450A"));
        saved["materials"]!["steel_eps_u"] = 30;
        using (var savedView = new RetainingWallWorkspace(saved))
        {
            savedView.Cards["Terreno"].IsExpanded = false; savedView.Width = 1600; savedView.Height = 1000; savedView.Measure(new(1600, 1000)); savedView.Arrange(new(0, 0, 1600, 1000)); savedView.UpdateLayout();
            Check(Notes(savedView).Select(t => t.Text).SequenceEqual([det3]), "Nota DET-3 dell'acciaio salvato nella scheda Materiali del muro");
            Check(saved["materials"].D("steel_eps_u") == 30, "Valore salvato non riscritto all'apertura");
            File.WriteAllBytes(Path.Combine(directory, "materiali-muro-acciaio-salvato.png"), Ui.Snapshot(savedView));
        }
        view.Width = 1600; view.Height = 1000; view.Measure(new(1600, 1000)); view.Arrange(new(0, 0, 1600, 1000)); view.UpdateLayout();
        Check(!Notes(view).Any(), "Nessuna nota DET-3 con l'acciaio del catalogo");
        ((ComboBox)form.Editors["classe_cls"]).SelectedItem = "C40/50";
        ((ComboBox)form.Editors["classe_acciaio"]).SelectedItem = "B450A";
        Check(d["materials"].D("fck")==40 && d["materials"].S("classe_acciaio")=="B450A","Selettori aggiornano i materiali effettivi");
        Check(!form.Editors["fyk"].IsEnabled && !form.Editors["steel_modulus_mpa"].IsEnabled,"Proprietà da catalogo protette");
        ((ComboBox)form.Editors["classe_acciaio"]).SelectedItem = "Personalizzato";
        Check(form.Editors["steel_modulus_mpa"].IsEnabled,"Materiale personalizzato modificabile");
        ((TextBox)form.Editors["steel_modulus_mpa"]).Text = "190000"; form.Commit();
        await view.CalculateAsync(); Check(view.Calculation is not null,"Calcolo con acciaio personalizzato");
        Check(RetainingWall.SectionInput(view.Calculation!.Input,"Fusto",.4).D("steel_modulus_mpa")==190000,"Modulo editato arriva al motore");
        view.Width=1600;view.Height=1000;view.Cards["Terreno"].IsExpanded=false;
        view.Measure(new(1600,1000));view.Arrange(new(0,0,1600,1000));view.UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory,"materiali-muro.png"),Ui.Snapshot(view));
        File.WriteAllBytes(Path.Combine(directory,"report-materiali.docx"),view.BuildReport("Materiali del muro condivisi con il verificatore"));
        Check(view.Forms["material_cover"].Editors.ContainsKey("cover_ground"),"Durabilità e superficie di getto disponibili");
        var section=view.Calculation.Cases[0];
        var transferred=Anthea.Application.Walls.WallModel.ExportSection(view.Calculation,"Fusto",3,section.Name);
        Check(transferred["input"].D("steel_modulus_mpa")==190000,"Trasferimento al modulo c.a. conserva il materiale");
        var drawing = view.CreateBarDrawing();
        Check(drawing.Schedule.Bars.Count == 12, "Tavola disponibile dal risultato aggiornato");
        var figures = RetainingWallWorkspace.BarFigures(drawing);
        File.WriteAllBytes(Path.Combine(directory,"distinta-unica.docx"),Anthea.Application.Walls.WallExports.CreateBarSchedule(drawing.Data,drawing.Schedule,figures));
        File.WriteAllBytes(Path.Combine(directory,"distinta-unica.pdf"),DrawingPdf.Create(figures.Select(f=>f.Png)));
        for(int i=0;i<figures.Count;i++) File.WriteAllBytes(Path.Combine(directory,$"distinta-unica-{i+1}.png"),figures[i].Png);
        var two=RetainingWall.Defaults();two["reinforcement"]!["two_zones"]=true;two["reinforcement"]!["lower_height"]=1.5;two["bar_schedule"]!["panel_length"]=4;
        using var twoView=new RetainingWallWorkspace(two);await twoView.CalculateAsync();
        var twoDrawing=twoView.CreateBarDrawing();var twoFigures=RetainingWallWorkspace.BarFigures(twoDrawing);
        Check(twoDrawing.Schedule.Bars.Count==17&&!twoDrawing.Schedule.CompleteQuantities,"Tavola a due zone segnala il peso parziale dei collegamenti");
        File.WriteAllBytes(Path.Combine(directory,"distinta-due-zone.docx"),Anthea.Application.Walls.WallExports.CreateBarSchedule(twoDrawing.Data,twoDrawing.Schedule,twoFigures));
        File.WriteAllBytes(Path.Combine(directory,"distinta-due-zone.pdf"),DrawingPdf.Create(twoFigures.Select(f=>f.Png)));
        for(int i=0;i<twoFigures.Count;i++) File.WriteAllBytes(Path.Combine(directory,$"distinta-due-zone-{i+1}.png"),twoFigures[i].Png);
        var archive=Archivio.Documento(RetainingWall.Module);archive["dati"]=two.DeepClone();DocumentService.Save(Path.Combine(directory,"distinta-due-zone.anthea"),archive);
        Check(Application.Current.Windows.Count==0,"Tavole e PDF prodotti senza usare mouse o finestre");
        File.WriteAllLines(Path.Combine(directory,"test.txt"),log.Append($"PASS {log.Count} controlli interfaccia"));
    }
}
