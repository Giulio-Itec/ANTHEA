using System.IO;
using System.Windows;
using System.Windows.Controls;
using X.Core;

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
        ((ComboBox)form.Editors["classe_cls"]).SelectedItem = "C40/50";
        ((ComboBox)form.Editors["classe_acciaio"]).SelectedItem = "B450A";
        Check(d["materials"].D("fck")==40 && d["materials"].S("classe_acciaio")=="B450A","Selettori aggiornano i materiali effettivi");
        Check(((TextBox)form.Editors["fyk"]).IsReadOnly && ((TextBox)form.Editors["steel_modulus_mpa"]).IsReadOnly,"Proprietà da catalogo protette");
        ((ComboBox)form.Editors["classe_acciaio"]).SelectedItem = "Personalizzato";
        Check(!((TextBox)form.Editors["steel_modulus_mpa"]).IsReadOnly,"Materiale personalizzato modificabile");
        ((TextBox)form.Editors["steel_modulus_mpa"]).Text = "190000"; form.Commit();
        await view.CalculateAsync(); Check(view.Calculation is not null,"Calcolo con acciaio personalizzato");
        Check(RetainingWall.SectionInput(view.Calculation!.Input,"Fusto",.4).D("steel_modulus_mpa")==190000,"Modulo editato arriva al motore");
        view.Width=1600;view.Height=1000;view.Cards["Terreno"].IsExpanded=false;
        view.Measure(new(1600,1000));view.Arrange(new(0,0,1600,1000));view.UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory,"materiali-muro.png"),Ui.Snapshot(view));
        File.WriteAllBytes(Path.Combine(directory,"report-materiali.docx"),view.BuildReport("Materiali del muro condivisi con il verificatore"));
        Check(view.Forms["material_cover"].Editors.ContainsKey("cover_ground"),"Durabilità e superficie di getto disponibili");
        var section=view.Calculation.Cases[0];
        var transferred=RetainingWall.ExportSection(view.Calculation,"Fusto",3,section.Name);
        Check(transferred["input"].D("steel_modulus_mpa")==190000,"Trasferimento al modulo c.a. conserva il materiale");
        File.WriteAllLines(Path.Combine(directory,"test.txt"),log.Append($"PASS {log.Count} controlli interfaccia"));
    }
}
