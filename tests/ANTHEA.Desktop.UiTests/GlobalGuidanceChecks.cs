using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;
using Anthea.Application.Documents;

namespace X.Desktop;

/// <summary>
/// --check-global-guidance-offscreen: the guided path of the global stability of the walls. Since the Release switch of the walls (f5-pass-muri,
/// 23/10/2026) on the XAML view opened by the factory of the bench in a window outside the screen (<see cref="OffscreenWall"/>), driven through
/// its controls (<see cref="MvvmWallSmoke"/>); before, on the archived RetainingWallWorkspace (supporto/SUPERATI/ui-storica/muri).
/// </summary>
internal static class GlobalGuidanceChecks
{
    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory); var log = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); log.Add("OK " + message); }
        void Render(FrameworkElement element, string name, double width, double height)
        {
            element.Width = width; element.Height = height; element.InvalidateVisual(); element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), Ui.Snapshot(element));
        }
        async Task Shot(OffscreenWall host, string name, double width, double height) => File.WriteAllBytes(Path.Combine(directory, name + ".png"), await host.Png(width, height));
        using var host = await OffscreenWall.OpenAsync(RetainingWall.Example("cantilever"));
        var w = host.Wall; var sheet = host.Sheet;
        JsonNode G() => w.Data["global_stability"]!;
        try
        {
            Check(OffscreenWall.NothingOnScreen, "Nessuna finestra sullo schermo, nessun input del desktop (foglio fuori schermo)");
            var normal = new RetainingWallDrawing { Data = w.Data };
            Render(normal, "fondazione-prima-della-globale", 780, 480);
            Check(normal.GroundSource == "Terreno di fondazione", "Terreno sotto il muro già rappresentato prima della globale");
            w.SetCell("monte", 0, "thickness", "3.45");
            w.Command("ShowGlobalSetupCommand"); await host.Settle();
            Check(G().B("enabled") && !G().B("profile_confirmed"), "Primo accesso precompila e attiva senza confermare i dati del sito");
            Check(G().S("soil_mode") == "Due colonne" && G().S("search_mode") == "Automatica", "Due colonne e ricerca automatica proposte");
            Check(sheet.GlobalReadiness.Contains("fondazione"), "Messaggio identifica gli strati profondi mancanti");
            // The automatic calculation scheduled by the edit of the layer first (its publication completes the global combinations).
            await w.WaitIdle(); await host.Settle();
            string preserved = G().ToJsonString(); w.Command("ShowGlobalSetupCommand"); await host.Settle();
            if (G().ToJsonString() != preserved) { File.WriteAllText(Path.Combine(directory, "globale-prima.json"), preserved); File.WriteAllText(Path.Combine(directory, "globale-dopo.json"), G().ToJsonString()); }
            Check(G().ToJsonString() == preserved, "Riaprire il percorso conserva un modello già iniziato");
            w.SetCell("globale-monte", 0, "__thickness", "25"); w.SetCell("globale-valle", 0, "__thickness", "25");
            Check(Math.Abs(G().Array("layers")[0].D("bottom") + 21.55) < 1e-8, "Spessore 25 m da quota 3,45 produce fondo −21,55 m");
            Check(G().Array("valley_layers")[0].D("bottom") == -25, "Valle: spessore riferito alla propria superficie");
            Check(G().D("depth_max") == 6.9 && G().D("depth_min") == .1, "Ricerca limitata a 2(H+t) entro entrambe le indagini");
            w.Tick("global", "profile_confirmed", true);
            w.Set("global_soil_rear", "phi", "29");
            Check(!G().B("profile_confirmed") && w.IsChecked("global", "profile_confirmed") == false, "Modificare un terreno richiede un nuovo controllo esplicito");
            w.Set("global_soil_rear", "phi", "30");
            w.Set("global", "condition", "Non drenata");
            Check(sheet.Forms["global_soil_rear"]["cu"].IsVisible && !sheet.Forms["global_soil_rear"]["phi"].IsVisible, "In non drenata si presenta cu al posto di φ′ e c′");
            Check(sheet.GlobalReadiness.Contains("cu,k"), "cu assente segnalata prima del calcolo");
            w.Set("global", "condition", "Drenata");
            w.Command("AddGlobalLayerCommand", "monte");
            Check(G().Array("layers").Count == 2 && G().Array("layers")[1].S("gamma") == "", "Nuovo strato senza proprietà geotecniche inventate");
            w.SetCell("globale-monte", 1, "__thickness", "4");
            Check(Math.Abs(G().Array("layers")[1].D("bottom") + 25.55) < 1e-8, "Secondo strato aggiunto sotto il precedente");
            w.Command("RemoveGlobalLayerCommand", "monte");
            Check(G().Array("layers").Count == 1 && w.Rows("globale-monte") == 1, "Eliminazione sincronizza archivio ed editor");
            w.Set("global_search_mode", "search_mode", "Assegnata"); w.Set("global_search", "depth_max", "8");
            w.Set("global_soil_rear", "gamma", "19");
            Check(G().D("depth_max") == 8, "Ricerca assegnata conservata dopo modifiche al terreno");
            w.Set("global_soil_rear", "gamma", "18");
            w.Set("global_search_mode", "search_mode", "Automatica");
            Check(G().D("depth_max") == 6.9, "Ritorno alla proposta automatica");
            var preview = new GlobalStabilityDrawing { Data = w.Data, FocusCritical = false, ShowSearch = true, SelectedSoil = 0, SelectedColumn = "monte" };
            Render(preview, "profilo-ricerca-600", 600, 320);
            Check(preview.SoilLabelsDrawn >= 2 && preview.SearchDrawn, "Strati etichettati e dominio di ricerca visibili a 600 px");
            Render(preview, "profilo-ricerca-1000", 1000, 400);
            normal.Data = w.Data; Render(normal, "fondazione-con-strati-globali", 780, 480);
            Check(normal.GroundSource == "Strati globali", "Vista principale usa i terreni profondi del modello globale");
            await Shot(host, "workspace-input-1600", 1600, 1000);
            w.Command("ShowGlobalSetupCommand"); await Shot(host, "percorso-guidato-1600", 1600, 1000);
            w.BringGlobalLayersIntoView(); await Shot(host, "editor-strati-1600", 1600, 1000);
            w.Tick("global", "profile_confirmed", true);
            await w.CalculateGlobalAsync(); await host.Settle();
            Check(w.GlobalResult is { Cases.Length: > 0 }, "Calcolo globale completato dal nuovo percorso");
            Check(w.Page == 1 && host.View.ViewChoice.SelectedItem as string == "Stabilità globale", "Il calcolo apre direttamente il risultato pertinente");
            var result = w.GlobalResult!;
            Render(new GlobalStabilityDrawing { Data = w.Data, Result = result }, "superficie-critica", 1000, 460);
            File.WriteAllText(Path.Combine(directory, "risultati.json"), RetainingWall.Calculate(w.Data).Json().ToJsonString(J.Options));
            var doc = Archivio.Documento(RetainingWall.Module); doc["dati"] = w.Data.DeepClone(); DocumentService.Save(Path.Combine(directory, "esempio-guidato.anthea"), doc);
            Check(J.Equivalent(DocumentService.Open(Path.Combine(directory, "esempio-guidato.anthea"))["dati"], w.Data), "File ripercorribile mantiene quote, proprietà e limiti");
            await Shot(host, "workspace-verifiche-1600", 1600, 1000);
            w.Command("ShowGlobalSetupCommand"); await Shot(host, "workspace-input-1050", 1050, 1000);
            Check(w.PageCount == 2, "Restano le sole due schede Input e Verifiche");
            w.GenerateGlobalMatrix(); w.SetCell("matrice-globale", 0, "r", "3"); await w.CalculateGlobalAsync(); await host.Settle();
            Check(w.GlobalResult!.Cases[0].Critical!.Factor > w.GlobalResult.Cases[1].Critical!.Factor && host.View.GlobalCombinationChoice.SelectedIndex == 0,
                "Caso iniziale scelto dal tasso più alto, anche con γR differenti e F non minimo");
            w.SetCell("globale-monte", 0, "__thickness", "-");
            Check(w.GlobalResult is null && !G().B("profile_confirmed") && G().S("depth_max") == "", "Bozza non numerica invalida risultato, conferma e ricerca senza inventare quote");
            Render(new GlobalStabilityDrawing { Data = w.Data, ShowSearch = true }, "strato-incompleto", 600, 320);
            var legacy = (JsonObject)w.Data.DeepClone(); legacy["global_stability"]!.AsObject().Remove("search_mode"); legacy["global_stability"]!["depth_max"] = 7;
            using (var old = await OffscreenWall.OpenAsync(legacy))
                Check(old.Wall.Data["global_stability"].S("search_mode") == "Assegnata" && old.Wall.Data["global_stability"].D("depth_max") == 7, "Archivi precedenti: limiti conservati senza ricalcolo automatico");
            var layered = (JsonObject)w.Data.DeepClone(); var lg = layered["global_stability"]!;
            JsonObject Soil(string name, double bottom, double gamma, double phi) => J.Obj(("name", name), ("bottom", bottom), ("gamma", gamma), ("gamma_sat", gamma + 2), ("phi", phi), ("c", 0), ("cu", ""));
            lg["layers"] = new JsonArray(Soil("Riempimento", 0, 18, 30), Soil("Alluvioni", -2, 19, 28), Soil("Ghiaia", -10, 20, 36));
            lg["valley_layers"] = new JsonArray(Soil("Alluvioni", -2, 19, 28), Soil("Ghiaia", -10, 20, 36));
            lg["search_mode"] = "Automatica"; lg["combination_mode"] = "Automatiche"; lg["combinations"] = new JsonArray(); lg["profile_confirmed"] = true;
            using var illustrated = await OffscreenWall.OpenAsync(layered);
            var shown = illustrated.Wall; var data = shown.Data; var layers = data["global_stability"]!;
            shown.SelectRow("globale-monte", 1);
            Check(J.Number(shown.Row("globale-monte", 2)["__thickness"]) == 8 && J.Number(shown.Row("globale-valle", 0)["__thickness"]) == 2,
                "Riapertura multistrato ricava spessori dalle quote assolute di ciascuna colonna");
            Check(RetainingWallDrawing.GlobalLayerColor(data, layers.Array("layers")[1]) == RetainingWallDrawing.GlobalLayerColor(data, layers.Array("valley_layers")[0]), "Stesso nome del terreno: colore coerente nelle due colonne");
            Render(new GlobalStabilityDrawing { Data = data, FocusCritical = false, ShowSearch = true }, "esempio-stratificato-profilo", 640, 350);
            // The editor of the layers of the column of the mountain as the guide shows it: its group of the view, laid out in the sheet.
            shown.Command("ShowGlobalSetupCommand"); shown.BringGlobalLayersIntoView(); await illustrated.Settle();
            var soilPanel = (FrameworkElement)illustrated.View.GlobalRearGrid.Parent;
            File.WriteAllBytes(Path.Combine(directory, "esempio-stratificato-editor.png"), Ui.Snapshot(soilPanel));
            await shown.CalculateGlobalAsync(); Check(shown.GlobalResult is not null, "Esempio stratificato della guida ricalcolato dal percorso reale");
            doc = Archivio.Documento(RetainingWall.Module); doc["dati"] = data.DeepClone(); DocumentService.Save(Path.Combine(directory, "esempio-stratificato.anthea"), doc);
            File.WriteAllText(Path.Combine(directory, "esempio-stratificato-conci.csv"), Anthea.Application.Walls.WallExports.GlobalCsv(shown.GlobalResult!));
            Render(new GlobalStabilityDrawing { Data = data, Result = shown.GlobalResult }, "esempio-stratificato-esito", 640, 350);
            File.WriteAllText(Path.Combine(directory, "esempio-stratificato-esito.txt"), string.Join("\n", shown.GlobalResult!.Cases.Select(c => $"{c.Factors.Name}; F={c.Critical?.Factor:G10}; eta={c.Critical?.Ratio:G10}; {c.Status}")));
            Check(OffscreenWall.NothingOnScreen, "Controlli conclusi senza finestre sullo schermo");
            log.Add($"PASS {log.Count} controlli offscreen"); File.WriteAllText(Path.Combine(directory, "completato.txt"), log[^1]);
        }
        finally { File.WriteAllLines(Path.Combine(directory, "controlli.txt"), log); }
    }
}
