using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

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
        using var w = new RetainingWallWorkspace(RetainingWall.Example("cantilever"));
        try
        {
            Check(Application.Current.Windows.Count == 0, "Nessuna finestra nativa, nessun input del desktop");
            var normal = new RetainingWallDrawing { Data = w.Data };
            Render(normal, "fondazione-prima-della-globale", 780, 480);
            Check(normal.GroundSource == "Terreno di fondazione", "Terreno sotto il muro già rappresentato prima della globale");
            w.LayerGrid.Rows[0]["thickness"] = "3.45";
            w.ShowGlobalSetup(); var g = w.Data["global_stability"]!;
            Check(g.B("enabled") && !g.B("profile_confirmed"), "Primo accesso precompila e attiva senza confermare i dati del sito");
            Check(g.S("soil_mode") == "Due colonne" && g.S("search_mode") == "Automatica", "Due colonne e ricerca automatica proposte");
            Check(w.globalReadiness.Text.Contains("fondazione"), "Messaggio identifica gli strati profondi mancanti");
            string preserved = g.ToJsonString(); w.ShowGlobalSetup(); Check(g.ToJsonString() == preserved, "Riaprire il percorso conserva un modello già iniziato");
            w.GlobalLayers.Rows[0]["__thickness"] = "25"; w.GlobalValleyLayers.Rows[0]["__thickness"] = "25";
            Check(Math.Abs(g.Array("layers")[0].D("bottom") + 21.55) < 1e-8, "Spessore 25 m da quota 3,45 produce fondo −21,55 m");
            Check(g.Array("valley_layers")[0].D("bottom") == -25, "Valle: spessore riferito alla propria superficie");
            Check(g.D("depth_max") == 6.9 && g.D("depth_min") == .1, "Ricerca limitata a 2(H+t) entro entrambe le indagini");
            var confirm = (CheckBox)w.Forms["global"].Editors["profile_confirmed"]; confirm.IsChecked = true;
            w.Forms["global_soil_rear"].Set("phi", "29");
            Check(!g.B("profile_confirmed") && confirm.IsChecked == false, "Modificare un terreno richiede un nuovo controllo esplicito");
            w.Forms["global_soil_rear"].Set("phi", "30");
            w.Forms["global"].Set("condition", "Non drenata");
            Check(w.Forms["global_soil_rear"].Editors["cu"].Visibility == Visibility.Visible && w.Forms["global_soil_rear"].Editors["phi"].Visibility == Visibility.Collapsed, "In non drenata si presenta cu al posto di φ′ e c′");
            Check(w.globalReadiness.Text.Contains("cu,k"), "cu assente segnalata prima del calcolo");
            w.Forms["global"].Set("condition", "Drenata");
            var soilHost = (FrameworkElement)w.GlobalLayers.Parent;
            Ui.Descendants<Button>(soilHost).Single(b => b.Content as string == "+ Strato").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(g.Array("layers").Count == 2 && g.Array("layers")[1].S("gamma") == "", "Nuovo strato senza proprietà geotecniche inventate");
            w.GlobalLayers.Rows[1]["__thickness"] = "4";
            Check(Math.Abs(g.Array("layers")[1].D("bottom") + 25.55) < 1e-8, "Secondo strato aggiunto sotto il precedente");
            Ui.Descendants<Button>(soilHost).Single(b => b.Content as string == "− Strato").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(g.Array("layers").Count == 1 && w.GlobalLayers.Rows.Count == 1, "Eliminazione sincronizza archivio ed editor");
            w.Forms["global_search_mode"].Set("search_mode", "Assegnata"); w.Forms["global_search"].Set("depth_max", "8");
            w.Forms["global_soil_rear"].Set("gamma", "19");
            Check(g.D("depth_max") == 8, "Ricerca assegnata conservata dopo modifiche al terreno");
            w.Forms["global_soil_rear"].Set("gamma", "18");
            w.Forms["global_search_mode"].Set("search_mode", "Automatica");
            Check(g.D("depth_max") == 6.9, "Ritorno alla proposta automatica");
            var preview = new GlobalStabilityDrawing { Data = w.Data, FocusCritical = false, ShowSearch = true, SelectedSoil = 0, SelectedColumn = "monte" };
            Render(preview, "profilo-ricerca-600", 600, 320);
            Check(preview.SoilLabelsDrawn >= 2 && preview.SearchDrawn, "Strati etichettati e dominio di ricerca visibili a 600 px");
            Render(preview, "profilo-ricerca-1000", 1000, 400);
            normal.Data = w.Data; Render(normal, "fondazione-con-strati-globali", 780, 480);
            Check(normal.GroundSource == "Strati globali", "Vista principale usa i terreni profondi del modello globale");
            Render(w, "workspace-input-1600", 1600, 1000);
            w.ShowGlobalSetup(); await Dispatcher.Yield(DispatcherPriority.Background); w.UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory, "percorso-guidato-1600.png"), Ui.Snapshot(w));
            w.GlobalLayers.BringIntoView(); await Dispatcher.Yield(DispatcherPriority.Background); w.UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory, "editor-strati-1600.png"), Ui.Snapshot(w));
            confirm.IsChecked = true;
            await w.CalculateGlobalAsync();
            Check(w.GlobalResult is { Cases.Length: > 0 }, "Calcolo globale completato dal nuovo percorso");
            Check(w.Pages.SelectedIndex == 1 && w.ViewMode.SelectedItem as string == "Stabilità globale", "Il calcolo apre direttamente il risultato pertinente");
            var result = w.GlobalResult!;
            Render(new GlobalStabilityDrawing { Data = w.Data, Result = result }, "superficie-critica", 1000, 460);
            File.WriteAllText(Path.Combine(directory, "risultati.json"), RetainingWall.Calculate(w.Data).Json().ToJsonString(J.Options));
            var doc = Archivio.Documento(RetainingWall.Module); doc["dati"] = w.Data.DeepClone(); Archivio.Scrivi(Path.Combine(directory, "esempio-guidato.anthea"), doc);
            Check(J.Equivalent(Archivio.Leggi(Path.Combine(directory, "esempio-guidato.anthea"))["dati"], w.Data), "File ripercorribile mantiene quote, proprietà e limiti");
            Render(w, "workspace-verifiche-1600", 1600, 1000);
            w.ShowGlobalSetup(); Render(w, "workspace-input-1050", 1050, 1000);
            Check(w.Pages.Items.Count == 2, "Restano le sole due schede Input e Verifiche");
            w.GenerateGlobalMatrix(); w.GlobalMatrix.Rows[0]["r"] = "3"; await w.CalculateGlobalAsync();
            Check(w.GlobalResult!.Cases[0].Critical!.Factor > w.GlobalResult.Cases[1].Critical!.Factor && w.GlobalCombination.SelectedIndex == 0,
                "Caso iniziale scelto dal tasso più alto, anche con γR differenti e F non minimo");
            w.GlobalLayers.Rows[0]["__thickness"] = "-";
            Check(w.GlobalResult is null && !g.B("profile_confirmed") && g.S("depth_max") == "", "Bozza non numerica invalida risultato, conferma e ricerca senza inventare quote");
            Render(new GlobalStabilityDrawing { Data = w.Data, ShowSearch = true }, "strato-incompleto", 600, 320);
            var legacy = (JsonObject)w.Data.DeepClone(); legacy["global_stability"]!.AsObject().Remove("search_mode"); legacy["global_stability"]!["depth_max"] = 7;
            using var old = new RetainingWallWorkspace(legacy);
            Check(legacy["global_stability"].S("search_mode") == "Assegnata" && legacy["global_stability"].D("depth_max") == 7, "Archivi precedenti: limiti conservati senza ricalcolo automatico");
            var layered = (JsonObject)w.Data.DeepClone(); var lg = layered["global_stability"]!;
            JsonObject Soil(string name, double bottom, double gamma, double phi) => J.Obj(("name", name), ("bottom", bottom), ("gamma", gamma), ("gamma_sat", gamma + 2), ("phi", phi), ("c", 0), ("cu", ""));
            lg["layers"] = new JsonArray(Soil("Riempimento", 0, 18, 30), Soil("Alluvioni", -2, 19, 28), Soil("Ghiaia", -10, 20, 36));
            lg["valley_layers"] = new JsonArray(Soil("Alluvioni", -2, 19, 28), Soil("Ghiaia", -10, 20, 36));
            lg["search_mode"] = "Automatica"; lg["combination_mode"] = "Automatiche"; lg["combinations"] = new JsonArray(); lg["profile_confirmed"] = true;
            using var illustrated = new RetainingWallWorkspace(layered);
            illustrated.GlobalLayers.SelectedIndex = 1;
            Check(J.Number(illustrated.GlobalLayers.Rows[2].Values["__thickness"]) == 8 && J.Number(illustrated.GlobalValleyLayers.Rows[0].Values["__thickness"]) == 2,
                "Riapertura multistrato ricava spessori dalle quote assolute di ciascuna colonna");
            Check(RetainingWallDrawing.GlobalLayerColor(layered, lg.Array("layers")[1]) == RetainingWallDrawing.GlobalLayerColor(layered, lg.Array("valley_layers")[0]), "Stesso nome del terreno: colore coerente nelle due colonne");
            Render(new GlobalStabilityDrawing { Data = layered, FocusCritical = false, ShowSearch = true }, "esempio-stratificato-profilo", 640, 350);
            var soilPanel = (FrameworkElement)illustrated.GlobalLayers.Parent; ((ContentControl)soilPanel.Parent).Content = null;
            // Detached grids have no presentation source to resolve star column widths.
            for (int i = 0; i < 3; i++) illustrated.GlobalLayers.Columns[i].Width = new DataGridLength(i == 0 ? 310 : 165);
            var soilPaper = Ui.Paper(soilPanel, 10); soilPaper.Measure(new Size(680, double.PositiveInfinity)); Render(soilPaper, "esempio-stratificato-editor", 680, soilPaper.DesiredSize.Height);
            await illustrated.CalculateGlobalAsync(); Check(illustrated.GlobalResult is not null, "Esempio stratificato della guida ricalcolato dal percorso reale");
            doc = Archivio.Documento(RetainingWall.Module); doc["dati"] = layered.DeepClone(); Archivio.Scrivi(Path.Combine(directory, "esempio-stratificato.anthea"), doc);
            File.WriteAllText(Path.Combine(directory, "esempio-stratificato-conci.csv"), ReportRetainingWall.GlobalCsv(illustrated.GlobalResult!));
            Render(new GlobalStabilityDrawing { Data = layered, Result = illustrated.GlobalResult }, "esempio-stratificato-esito", 640, 350);
            File.WriteAllText(Path.Combine(directory, "esempio-stratificato-esito.txt"), string.Join("\n", illustrated.GlobalResult!.Cases.Select(c => $"{c.Factors.Name}; F={c.Critical?.Factor:G10}; eta={c.Critical?.Ratio:G10}; {c.Status}")));
            Check(Application.Current.Windows.Count == 0, "Controlli conclusi senza aprire finestre");
            log.Add($"PASS {log.Count} controlli offscreen"); File.WriteAllText(Path.Combine(directory, "completato.txt"), log[^1]);
        }
        finally { File.WriteAllLines(Path.Combine(directory, "controlli.txt"), log); }
    }
}
