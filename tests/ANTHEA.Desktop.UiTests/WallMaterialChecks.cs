using System.IO;
using System.Windows;
using System.Windows.Controls;
using X.Core;
using Anthea.Application.Documents;

namespace X.Desktop;

/// <summary>
/// --check-wall-materials-offscreen: the materials of the walls shared with the checker, DET-3 and the bar schedule. Since the Release switch of
/// the walls (f5-pass-muri, 23/10/2026) on the XAML view opened by the factory of the bench in a window outside the screen
/// (<see cref="OffscreenWall"/>), driven through its controls (<see cref="MvvmWallSmoke"/>); the bar schedule by the recipe of the dialog of the
/// view (RetainingWallBarDrawing on the schedule of the sheet, ModuleFigureRenderer.BarFigures). Before, on the archived RetainingWallWorkspace.
/// </summary>
internal static class WallMaterialChecks
{
    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var log = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); log.Add("OK " + message); }
        using var host = await OffscreenWall.OpenAsync(RetainingWall.Defaults());
        var view = host.Wall; var d = view.Data;
        Check(OffscreenWall.NothingOnScreen, "Nessuna finestra sullo schermo (foglio fuori schermo)");
        // DET-3 (seguito del 16/10/2026): acciaio del muro salvato come B450A con εu = 30 ‰ (catalogo precedente). La scheda Materiali lo dichiara
        // con la nota del campo, come la sezione c.a. e il palo orizzontale, senza riscrivere il valore; con l'acciaio del catalogo nessuna nota.
        const string det3 = "Valori salvati diversi dal catalogo B450A: εu = 30 ‰ (catalogo 25 ‰). Il calcolo usa i valori salvati; riselezionare B450A per adottare quelli del catalogo.";
        static IEnumerable<TextBlock> Notes(MvvmWallSmoke w) => RetainingWallViewChecks.Walk(w.FormView("materials"), popups: false).OfType<TextBlock>()
            .Where(t => t.Text.StartsWith("Valori salvati diversi dal catalogo") && t.Parent is FrameworkElement { Visibility: Visibility.Visible });
        var saved = RetainingWall.Defaults(); saved["materials"]!["classe_acciaio"] = "B450A";
        RetainingWall.ApplyMaterialPreset(saved, Anthea.Application.Materials.MaterialInputCatalog.Steel(false, "NTC 2018").Single(x => x.S("nome") == "B450A"));
        saved["materials"]!["steel_eps_u"] = 30;
        using (var savedView = await OffscreenWall.OpenAsync(saved))
        {
            savedView.Wall.BringFormIntoView("materials");
            Check(Notes(savedView.Wall).Select(t => t.Text).SequenceEqual([det3]), "Nota DET-3 dell'acciaio salvato nella scheda Materiali del muro");
            Check(savedView.Wall.Data["materials"].D("steel_eps_u") == 30, "Valore salvato non riscritto all'apertura");
            File.WriteAllBytes(Path.Combine(directory, "materiali-muro-acciaio-salvato.png"), await savedView.Png(1600, 1000));
        }
        Check(!Notes(view).Any(), "Nessuna nota DET-3 con l'acciaio del catalogo");
        view.Set("materials", "classe_cls", "C40/50");
        view.Set("materials", "classe_acciaio", "B450A");
        Check(d["materials"].D("fck") == 40 && d["materials"].S("classe_acciaio") == "B450A", "Selettori aggiornano i materiali effettivi");
        Check(!view.IsEditable("materials", "fyk") && !view.IsEditable("materials", "steel_modulus_mpa"), "Proprietà da catalogo protette");
        view.Set("materials", "classe_acciaio", "Personalizzato");
        Check(view.IsEditable("materials", "steel_modulus_mpa"), "Materiale personalizzato modificabile");
        view.Set("materials", "steel_modulus_mpa", "190000");
        await view.CalculateAsync(); Check(view.Calculation is not null, "Calcolo con acciaio personalizzato");
        Check(RetainingWall.SectionInput(view.Calculation!.Input, "Fusto", .4).D("steel_modulus_mpa") == 190000, "Modulo editato arriva al motore");
        view.BringFormIntoView("materials");
        File.WriteAllBytes(Path.Combine(directory, "materiali-muro.png"), await host.Png(1600, 1000));
        File.WriteAllBytes(Path.Combine(directory, "report-materiali.docx"), host.Editor.BuildReport("Materiali del muro condivisi con il verificatore", []));
        Check(host.Sheet.Forms["material_cover"].Contains("cover_ground"), "Durabilità e superficie di getto disponibili");
        var section = view.Calculation.Cases[0];
        var transferred = Anthea.Application.Walls.WallModel.ExportSection(view.Calculation, "Fusto", 3, section.Name);
        Check(transferred["input"].D("steel_modulus_mpa") == 190000, "Trasferimento al modulo c.a. conserva il materiale");
        var drawing = new RetainingWallBarDrawing(view.Calculation.Input, host.Sheet.CreateBarSchedule());
        // 1.16.0: fusto monte e valle, fondazione inferiore e superiore, forcella e quattro file di longitudinali.
        Check(drawing.Schedule.Bars.Count == 9 && drawing.Schedule.CompleteQuantities, "Tavola disponibile dal risultato aggiornato");
        var figures = ModuleFigureRenderer.BarFigures(drawing);
        File.WriteAllBytes(Path.Combine(directory, "distinta-unica.docx"), Anthea.Application.Walls.WallExports.CreateBarSchedule(drawing.Data, drawing.Schedule, figures));
        File.WriteAllBytes(Path.Combine(directory, "distinta-unica.pdf"), DrawingPdf.Create(figures.Select(f => f.Png)));
        for (int i = 0; i < figures.Count; i++) File.WriteAllBytes(Path.Combine(directory, $"distinta-unica-{i + 1}.png"), figures[i].Png);
        var two = RetainingWall.Defaults(); two["reinforcement"]!["two_zones"] = true; two["reinforcement"]!["lower_height"] = 1.5; two["bar_schedule"]!["panel_length"] = 4;
        using var twoView = await OffscreenWall.OpenAsync(two); await twoView.Wall.CalculateAsync();
        var twoDrawing = new RetainingWallBarDrawing(twoView.Wall.Calculation!.Input, twoView.Sheet.CreateBarSchedule()); var twoFigures = ModuleFigureRenderer.BarFigures(twoDrawing);
        // Due zone: quattro principali del fusto, fondazione, forcella, spilli con sagoma calcolata, sei file di longitudinali.
        Check(twoDrawing.Schedule.Bars.Count == 14 && twoDrawing.Schedule.CompleteQuantities, "Tavola a due zone con gli spilli calcolati");
        File.WriteAllBytes(Path.Combine(directory, "distinta-due-zone.docx"), Anthea.Application.Walls.WallExports.CreateBarSchedule(twoDrawing.Data, twoDrawing.Schedule, twoFigures));
        File.WriteAllBytes(Path.Combine(directory, "distinta-due-zone.pdf"), DrawingPdf.Create(twoFigures.Select(f => f.Png)));
        for (int i = 0; i < twoFigures.Count; i++) File.WriteAllBytes(Path.Combine(directory, $"distinta-due-zone-{i + 1}.png"), twoFigures[i].Png);
        var archive = Archivio.Documento(RetainingWall.Module); archive["dati"] = twoView.Wall.Data.DeepClone(); DocumentService.Save(Path.Combine(directory, "distinta-due-zone.anthea"), archive);
        Check(OffscreenWall.NothingOnScreen, "Tavole e PDF prodotti senza usare mouse o finestre sullo schermo");
        File.WriteAllLines(Path.Combine(directory, "test.txt"), log.Append($"PASS {log.Count} controlli interfaccia"));
    }
}
