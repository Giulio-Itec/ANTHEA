using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeGlobalStability(string directory)
    {
        testing = true; Directory.CreateDirectory(directory); WindowState = WindowState.Normal; Width = 1600; Height = 1000;
        var log = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); log.Add("OK " + message); }
        async Task Settle() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        try
        {
            document = Archivio.Documento(RetainingWall.Module); dirty = false; currentSheet = null; ShowSheet(document);
            var w = editor!.retainingWall!; await w.CalculateAsync();
            Check(!w.Data["global_stability"].B("enabled") && w.Calculation!.GlobalStability is null, "Compatibilità: globale disattivata sui nuovi documenti");
            w.LayerGrid.Rows[0]["thickness"] = "25"; w.PrepareGlobal();
            ((CheckBox)w.Forms["global"].Editors["enabled"]).IsChecked = true;
            await w.CalculateAsync();
            Check(w.Calculation!.GlobalError!.Contains("confermare") && w.Calculation.Cases.Count > 0, "Profilo da confermare: mantiene i risultati locali e segnala la globale");
            ((CheckBox)w.Forms["global"].Editors["profile_confirmed"]).IsChecked = true; w.Forms["global_search_mode"].Set("search_mode", "Assegnata"); w.Forms["global_search"].Set("depth_min", "0.1");
            await w.CalculateAsync();
            Check(w.GlobalResult is { Cases.Length: > 0 } && w.Calculation!.GlobalError is null, "Calcolo globale dalla UI");
            Check(w.Calculation!.Checks.Any(c => c.Name.Contains("Bishop")), "Verifica globale nel riepilogo completo");
            w.Pages.SelectedIndex = 0; w.Cards["Stabilità globale"].IsExpanded = true; w.Forms["global"].BringIntoView(); await Settle();
            File.WriteAllBytes(Path.Combine(directory, "input-globale.png"), Ui.Snapshot(this));
            w.GlobalLayers.BringIntoView(); await Settle(); File.WriteAllBytes(Path.Combine(directory, "strati-globali.png"), Ui.Snapshot(this));
            w.Pages.SelectedIndex = 1; w.ViewMode.SelectedItem = "Stabilità globale"; await Settle();
            Check(w.GlobalDrawing.IsVisible && w.GlobalCombination.Items.Count == w.GlobalResult!.Cases.Length && w.CheckFilter.SelectedItem as string == "Stabilità globale", "Vista dedicata e combinazioni indipendenti");
            File.WriteAllBytes(Path.Combine(directory, "superficie-critica.png"), Ui.Snapshot(this));
            File.WriteAllBytes(Path.Combine(directory, "sola-superficie.png"), Ui.Snapshot(w.GlobalDrawing));
            w.GlobalCombination.SelectedIndex = 0; await Settle();
            Check(w.GlobalDrawing.Case?.Factors.Name == (string)w.GlobalCombination.SelectedItem, "Cambio combinazione aggiorna la superficie");
            w.GenerateGlobalMatrix(); var first = w.GlobalMatrix.Rows[0]; first["r"] = "1.35"; await w.CalculateAsync();
            Check(w.Data["global_stability"].S("combination_mode") == "Personalizzate" && w.GlobalResult!.Cases[0].Factors.R == 1.35, "Matrice globale modificabile ed effettiva");
            editor.ExportReport(Path.Combine(directory, "muro-con-globale.docx"), "Muro con stabilità globale", []);
            var reportCaptions = WallAdvancedChecks.ReportFigureCaptions(w, global: true);
            Check(WallAdvancedChecks.ReportHasFigures(File.ReadAllBytes(Path.Combine(directory, "muro-con-globale.docx")), reportCaptions), "Relazione completa include la figura globale: " + string.Join(" | ", reportCaptions));
            w.Forms["foundation"].Set("delta", "40"); await w.CalculateAsync(); Check(w.Calculation is null, "Input locale fuori campo invalida solo il calcolo ordinario");
            await w.CalculateGlobalAsync(); Check(w.GlobalResult is not null && w.Calculation is null, "Calcola solo globale indipendente dalle verifiche locali");
            File.WriteAllBytes(Path.Combine(directory, "globale-autonoma.docx"), ReportRetainingWall.CreateGlobal(w.Data, w.GlobalResult!, new("Superficie critica", Ui.Snapshot(w.GlobalDrawing), w.GlobalDrawing.ActualWidth / w.GlobalDrawing.ActualHeight)));
            File.WriteAllText(Path.Combine(directory, "globale-conci.csv"), ReportRetainingWall.GlobalCsv(w.GlobalResult!));
            w.Forms["global_search"].Set("depth_max", "100"); Check(w.GlobalResult is null, "Una modifica invalida il risultato globale autonomo");
            await w.CalculateGlobalAsync(); Check(w.GlobalResult is null, "Errore nella profondità non mantiene un risultato obsoleto");
            w.Forms["global_search"].Set("depth_max", "6.9"); w.Forms["foundation"].Set("delta", "26"); await w.CalculateAsync();
            Width = 1050; await Settle(); File.WriteAllBytes(Path.Combine(directory, "globale-1050.png"), Ui.Snapshot(this));
            Check(w.Pages.Items.Count == 2, "Restano due sole schede");
            Commit(); Archivio.Scrivi(Path.Combine(directory, "globale.anthea"), document);
            Check(Archivio.Leggi(Path.Combine(directory, "globale.anthea"))["dati"]!["global_stability"].B("enabled"), "Salvataggio e rilettura degli input globali");
        }
        finally { File.WriteAllLines(Path.Combine(directory, "smoke.txt"), log); }
    }
}
