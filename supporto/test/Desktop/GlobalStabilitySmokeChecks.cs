using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;
using Anthea.Application.Documents;

namespace X.Desktop;

/// <summary>
/// ui/smoke-global-stability. Since F5.13 g on the new shell, off screen (ShellSmokeBench of part e), with the steps and checks it had through MainWindow
/// until 67eafba5: the document opened in a tab of the shell in place of ShowSheet, the wall through WallSmoke on the XAML view of the tab, the captures of
/// the shell window, the report written by the code of the shell (HostedSheet.ExportReport, as MvvmSheetEditor), the sheet confirmed by the workspace before
/// the save.
/// </summary>
internal static class GlobalStabilitySmoke
{
    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        using var b = await ShellSmokeBench.OpenAsync(directory);
        var window = b.Window; window.WindowState = WindowState.Normal; window.Width = 1600; window.Height = 1000;
        var log = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); log.Add("OK " + message); }
        Task Settle() => b.Idle();
        try
        {
            b.Workspace.OpenDocument(Archivio.Documento(RetainingWall.Module), null, false); await Settle();
            var sheet = b.Sheet;
            // f5-suite-ui: the wall through WallSmoke, on the XAML view of the tab (docs/refactoring/f5-suite-ui.md).
            var w = new MvvmWallSmoke(sheet.Hosted, b.Services); await w.CalculateAsync();
            Check(!w.Data["global_stability"].B("enabled") && w.Calculation!.GlobalStability is null, "Compatibilità: globale disattivata sui nuovi documenti");
            w.SetCell("monte", 0, "thickness", "25"); w.PrepareGlobal();
            w.Tick("global", "enabled", true);
            await w.CalculateAsync();
            Check(w.Calculation!.GlobalError!.Contains("confermare") && w.Calculation.Cases.Count > 0, "Profilo da confermare: mantiene i risultati locali e segnala la globale");
            w.Tick("global", "profile_confirmed", true); w.Set("global_search_mode", "search_mode", "Assegnata"); w.Set("global_search", "depth_min", "0.1");
            await w.CalculateAsync();
            Check(w.GlobalResult is { Cases.Length: > 0 } && w.Calculation!.GlobalError is null, "Calcolo globale dalla UI");
            Check(w.Calculation!.Checks.Any(c => c.Name.Contains("Bishop")), "Verifica globale nel riepilogo completo");
            w.Page = 0; w.Expand("Stabilità globale"); w.BringFormIntoView("global"); await Settle();
            File.WriteAllBytes(Path.Combine(directory, "input-globale.png"), Ui.Snapshot(window));
            w.BringGlobalLayersIntoView(); await Settle(); File.WriteAllBytes(Path.Combine(directory, "strati-globali.png"), Ui.Snapshot(window));
            w.Page = 1; w.ViewMode = "Stabilità globale"; await Settle();
            Check(w.GlobalDrawing.IsVisible && w.GlobalCombinations == w.GlobalResult!.Cases.Length && w.CheckFilter == "Stabilità globale", "Vista dedicata e combinazioni indipendenti");
            File.WriteAllBytes(Path.Combine(directory, "superficie-critica.png"), Ui.Snapshot(window));
            File.WriteAllBytes(Path.Combine(directory, "sola-superficie.png"), Ui.Snapshot(w.GlobalDrawing));
            w.GlobalCombinationIndex = 0; await Settle();
            Check(w.GlobalDrawingCase == w.SelectedGlobalCombination, "Cambio combinazione aggiorna la superficie");
            w.GenerateGlobalMatrix(); w.SetCell("matrice-globale", 0, "r", "1.35"); await w.CalculateAsync();
            Check(w.Data["global_stability"].S("combination_mode") == "Personalizzate" && w.GlobalResult!.Cases[0].Factors.R == 1.35, "Matrice globale modificabile ed effettiva");
            sheet.Hosted.ExportReport(Path.Combine(directory, "muro-con-globale.docx"), "Muro con stabilità globale", []);
            var reportCaptions = w.ReportFigureCaptions(global: true);
            Check(WallAdvancedChecks.ReportHasFigures(File.ReadAllBytes(Path.Combine(directory, "muro-con-globale.docx")), reportCaptions), "Relazione completa include la figura globale: " + string.Join(" | ", reportCaptions));
            w.Set("foundation", "delta", "40"); await w.CalculateAsync(); Check(w.Calculation is null, "Input locale fuori campo invalida solo il calcolo ordinario");
            await w.CalculateGlobalAsync(); Check(w.GlobalResult is not null && w.Calculation is null, "Calcola solo globale indipendente dalle verifiche locali");
            File.WriteAllBytes(Path.Combine(directory, "globale-autonoma.docx"), Anthea.Application.Walls.WallExports.CreateGlobal(w.Data, w.GlobalResult!, new("Superficie critica", Ui.Snapshot(w.GlobalDrawing), w.GlobalDrawing.ActualWidth / w.GlobalDrawing.ActualHeight)));
            File.WriteAllText(Path.Combine(directory, "globale-conci.csv"), Anthea.Application.Walls.WallExports.GlobalCsv(w.GlobalResult!));
            w.Set("global_search", "depth_max", "100"); Check(w.GlobalResult is null, "Una modifica invalida il risultato globale autonomo");
            await w.CalculateGlobalAsync(); Check(w.GlobalResult is null, "Errore nella profondità non mantiene un risultato obsoleto");
            w.Set("global_search", "depth_max", "6.9"); w.Set("foundation", "delta", "26"); await w.CalculateAsync();
            window.Width = 1050; await Settle(); File.WriteAllBytes(Path.Combine(directory, "globale-1050.png"), Ui.Snapshot(window));
            Check(w.PageCount == 2, "Restano due sole schede");
            b.Workspace.CommitActive(); DocumentService.Save(Path.Combine(directory, "globale.anthea"), b.Workspace.CurrentDocument!.WorkingDocument);
            Check(DocumentService.Open(Path.Combine(directory, "globale.anthea"))["dati"]!["global_stability"].B("enabled"), "Salvataggio e rilettura degli input globali");
        }
        finally { b.Services.Confirmations.SaveChanges = () => Anthea.Presentation.Services.SaveChangesAnswer.Discard; File.WriteAllLines(Path.Combine(directory, "smoke.txt"), log); }
    }
}
