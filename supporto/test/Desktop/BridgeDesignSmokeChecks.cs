using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeBridgeDesign(string directory)
    {
        testing = true; Directory.CreateDirectory(directory); WindowState = WindowState.Normal; Width = 1600; Height = 990;
        document = Archivio.Documento(BridgeConcept.Module); dirty = false; currentSheet = null; ShowSheet(document);
        var workspace = editor!.bridgeDesign ?? throw new Exception("Bridge Design non aperto.");
        async Task Settle() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        void Check(bool ok, string error) { if (!ok) throw new Exception(error); }
        await Settle();
        Check(workspace.Calculation is not null, "Risultato iniziale assente.");
        File.WriteAllBytes(Path.Combine(directory, "bridge_design_1600.png"), Ui.Snapshot(this));
        double cost = workspace.Calculation!.TotalCost, co2 = workspace.Calculation.Carbon;
        workspace.Pin(); workspace.Outputs.SelectedIndex = 3; await Settle();
        var price = workspace.Editors["rates/concrete_deck"]; price.Text = "480"; await Settle();
        Check(workspace.Calculation!.TotalCost > cost && workspace.Calculation.Carbon == co2, "Il prezzo non si propaga correttamente.");
        workspace.Undo(); await Settle();
        Check(workspace.Editors["rates/concrete_deck"].Text == "260", "Annulla non ripristina il controllo prezzi.");
        Check(Math.Abs(workspace.Calculation!.TotalCost - cost) < 1e-6, "Annulla non ripristina il calcolo.");
        workspace.Editors["rates/rebar"].Text = "1234"; await Settle();
        var pricePreset = Ui.Descendants<Button>(workspace).Single(b => b.Content is string label && label == "Applica valori orientativi 2026");
        pricePreset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Settle();
        Check(workspace.Editors["rates/rebar"].Text == "1660" && workspace.Data["rates"].D("rebar") == 1660, "Listino 2026 non aggiorna editor e dati");
        workspace.Undo(); await Settle(); Check(workspace.Editors["rates/rebar"].Text == "1234", "Undo listino perde prezzo personale");
        workspace.Undo(); await Settle();
        workspace.Editors["input/length"].Text = ""; Check(workspace.Calculation is null && editor.Result is null, "Risultati obsoleti con input vuoto.");
        workspace.Undo(); Check(workspace.Calculation is not null, "Ripristino da input invalido fallito.");
        for (int tab = 0; tab < 4; tab++)
        {
            workspace.Inputs.SelectedIndex = tab; workspace.Outputs.SelectedIndex = 0; await Settle();
            File.WriteAllBytes(Path.Combine(directory, $"input_{tab}.png"), Ui.Snapshot(this));
        }
        for (int tab = 0; tab < 6; tab++)
        {
            workspace.Outputs.SelectedIndex = tab; workspace.Outputs.BringIntoView(); await Settle();
            File.WriteAllBytes(Path.Combine(directory, $"risultati_{tab}.png"), Ui.Snapshot(this));
        }
        workspace.Inputs.SelectedIndex = 2; await Settle();
        foreach (var family in BridgeConcept.Families)
        {
            var button = Ui.Descendants<Button>(workspace).Single(b => (string)b.GetValue(System.Windows.Automation.AutomationProperties.NameProperty) == family.Name);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            workspace.Editors["input/length"].Text = (BridgeConcept.HasTowers(family.Id) ? 2 * Math.Min(450, (family.MinSpan + family.MaxSpan) / 2) : (family.MinSpan + family.MaxSpan) / 2 * 4.6).ToString(System.Globalization.CultureInfo.InvariantCulture);
            workspace.Editors["input/height"].Text = "40";
            await Settle(); Check(workspace.Calculation?.Family.Id == family.Id, "Cambio famiglia fallito: " + family.Id);
            File.WriteAllBytes(Path.Combine(directory, family.Id + "_prospetto.png"), workspace.Drawing.Png(false));
            File.WriteAllBytes(Path.Combine(directory, family.Id + "_sezione.png"), workspace.Drawing.Png(true));
        }
        workspace.AutoSize(); await Settle(); Check(workspace.Calculation is not null, "Dimensioni automatiche fallite.");
        for (int seed = 0; seed < 100; seed++)
        {
            workspace.Randomize(new Random(seed)); await Settle();
            Check(workspace.Calculation is not null, "Ponte casuale non calcolabile, seed " + seed); workspace.Undo();
        }
        workspace.Editors["input/pile_count"].Text = "1"; await Settle(); _ = workspace.Drawing.Png(); workspace.Undo();
        workspace.Data["input"] = BridgeConcept.Defaults()["input"]!.DeepClone();
        workspace.Data["input"]!["length"] = 120; workspace.Data["input"]!["spans"] = 3;
        workspace.Data["input"]!["height"] = 12; workspace.Data["input"]!["soil"] = "Roccia"; workspace.Data["input"]!["obstacle"] = "Nessuno";
        workspace.AutoSize(); await Settle();
        var optimizeButton = Ui.Descendants<Button>(workspace).Single(b => b.Content is string label && label == "Ottimizza");
        optimizeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Settle();
        Check(workspace.WorkspaceTabs.SelectedIndex == 1 && workspace.Outputs.Items.Count == 6, "Tasto Ottimizza non apre la nuova scheda autonoma.");
        await workspace.RunOptimization(); await Settle();
        Check(workspace.Optimization?.Candidates.Length > 0, "Ottimizzazione UI senza risultati.");
        File.WriteAllBytes(Path.Combine(directory, "ottimizzazione_risultati.png"), Ui.Snapshot(this));
        var complete = workspace.Optimization!;
        string unchanged = workspace.Data.ToJsonString();
        Check(workspace.OptimizationCloud.PointCount == complete.Solutions.Length, "Nuvola non include tutte le geometrie ammesse.");
        Check(workspace.OptimizationTrace.PointCount == complete.Trials.Count(t => t.Depth.HasValue), "Traccia incompleta.");
        workspace.OptimizationTop.Text = "25";
        Ui.Descendants<Button>(workspace).Single(b => b.Content is string s && s == "Aggiorna elenco").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Settle();
        Check(ReferenceEquals(complete, workspace.Optimization) && workspace.DisplayedOptimizationCount == Math.Min(25, complete.Solutions.Length), "N ripete o limita la ricerca.");
        workspace.OptimizationScroll.ScrollToBottom(); await Settle();
        File.WriteAllBytes(Path.Combine(directory, "ottimizzazione_graduatoria.png"), Ui.Snapshot(this));
        workspace.SelectOptimization(complete.Solutions.Length - 1); await Settle();
        Check(workspace.SelectedOptimizationRank == complete.Solutions.Length && workspace.Data.ToJsonString() == unchanged, "Selezione fuori dalla top N modifica il progetto o perde il rango.");
        Check(workspace.OptimizationPreview.Result!.TotalCost == complete.Solutions.Last().Trial.Cost, "Preview non corrisponde al rango scelto.");
        var point = workspace.OptimizationCloud.Points.First(p => p.Rank == 1);
        workspace.OptimizationCloud.SelectAt(point.Point); await Settle();
        Check(workspace.SelectedOptimizationRank == 1, "Click sul grafico non seleziona l’ottimo.");
        workspace.OptimizationCloud.BringIntoView(); await Settle();
        File.WriteAllBytes(Path.Combine(directory, "ottimizzazione_grafici.png"), Ui.Snapshot(this));
        for (int v = 0; v < 9; v++)
        {
            workspace.OptimizationTrace.Variable = v; workspace.OptimizationTrace.InvalidateVisual(); await Settle();
            // Render a detached copy: an element inside a scroller inherits clipping and offsets in RenderTargetBitmap.
            var plot = new BridgeOptimizationPlot { Width = 900, Height = 310, Variable = v };
            plot.SetData(complete.Trials, complete.Solutions, complete.Baseline);
            plot.Measure(new Size(900, 310)); plot.Arrange(new Rect(0, 0, 900, 310));
            File.WriteAllBytes(Path.Combine(directory, $"traccia_{v}.png"), Ui.Snapshot(plot));
        }
        workspace.OptimizationCloud.ParetoOnly = true; workspace.OptimizationCloud.InvalidateVisual(); await Settle();
        Check(workspace.OptimizationCloud.PointCount == complete.Solutions.Count(s => s.Pareto), "Filtro Pareto errato.");
        workspace.OptimizationCloud.ParetoOnly = false; workspace.OptimizationTrace.Variable = 2;
        foreach (double width in new[] { 1366d, 960d, 780d })
        {
            Width = width; Height = width < 1000 ? 720 : 900; await Settle();
            workspace.OptimizationScroll.ScrollToTop(); await Settle();
            Check(workspace.OptimizationScroll.ScrollableWidth < 1, "Scheda ottimizzazione deborda orizzontalmente.");
            File.WriteAllBytes(Path.Combine(directory, $"ottimizzazione_{width:0}.png"), Ui.Snapshot(this));
            workspace.OptimizationCloud.BringIntoView(); await Settle();
            File.WriteAllBytes(Path.Combine(directory, $"nuvola_{width:0}.png"), Ui.Snapshot(this));
        }
        Width = 1600; Height = 990; await Settle();
        workspace.Editors["rates/concrete_deck"].Text = "333";
        Check(workspace.Optimization is null, "Listino modificato non invalida le alternative."); workspace.Undo();
        Check(workspace.OptimizationPreview.Result is null, "Invalidazione lascia una preview obsoleta.");
        await workspace.RunOptimization(new BridgeConcept.OptimizationOptions { MaxDepth = .1 }); await Settle();
        Check(workspace.Optimization?.Solutions.Length == 0 && workspace.OptimizationPreview.Result is null, "Ricerca vuota mostra un vecchio candidato.");
        await workspace.RunOptimization(new BridgeConcept.OptimizationOptions { MinDepth = 3, MaxDepth = 2 }); await Settle();
        Check(workspace.Optimization is null && workspace.OptimizationPreview.Result is null, "Range invalido conserva risultati applicabili.");
        var running = workspace.RunOptimization();
        Ui.Descendants<Button>(workspace).Single(b => b.Content is string s && s == "Interrompi").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await running; await Settle();
        Check(workspace.Optimization is null && workspace.OptimizationPreview.Result is null, "Cancellazione lascia risultati provvisori applicabili.");
        running = workspace.RunOptimization(); workspace.Editors["rates/concrete_deck"].Text = "334";
        await running; await Settle(); Check(workspace.Optimization is null, "Ricerca completata su listino obsoleto."); workspace.Undo();
        await workspace.RunOptimization(); await Settle();
        string original = workspace.Data["input"]!.ToJsonString();
        int chosen = workspace.Optimization!.Solutions.Length - 1;
        double predictedCost = workspace.Optimization.Solutions[chosen].Trial.Cost!.Value;
        workspace.SelectOptimization(chosen); workspace.ApplyOptimization(chosen); await Settle();
        Check(Math.Abs(workspace.Calculation!.TotalCost - predictedCost) < 1e-6, "Applica soluzione non riproduce il costo previsto.");
        Check(workspace.WorkspaceTabs.SelectedIndex == 0 && workspace.Outputs.SelectedIndex == 5, "Applicazione non mostra il progetto e le quote tecniche.");
        File.WriteAllBytes(Path.Combine(directory, "soluzione_ottimizzata_quote.png"), Ui.Snapshot(this));
        workspace.Undo(); await Settle();
        Check(workspace.Data["input"]!.ToJsonString() == original, "Annulla non ripristina la configurazione pre-ottimizzazione.");
        // Save through the shell to exercise normal archive integration, then reopen an independent editor.
        Commit(); string archive = Path.Combine(directory, "BridgeDesign.anthea"); Archivio.Scrivi(archive, document);
        var reopened = Archivio.Leggi(archive); using var restored = new SheetEditor(BridgeConcept.Module, reopened["dati"]!.AsObject());
        Check(restored.HasResults && restored.Data["alternative_a"] is JsonObject, "Archivio incompleto al riavvio.");
        File.WriteAllBytes(Path.Combine(directory, "BridgeDesign.docx"), workspace.BuildReport("Predimensionamento del ponte"));
        var section = J.Obj(("nome", "Opera di prova"), ("fogli", new JsonArray(J.Obj(("nome", "Bridge Design"), ("modulo_id", BridgeConcept.Module), ("dati", workspace.Data)))), ("strutture", new JsonArray()));
        int unavailable = await GenerateSectionReport(section, Path.Combine(directory, "Progetto.docx"), _ => { }, CancellationToken.None);
        Check(unavailable == 0, "Bridge Design non incluso nel report di progetto.");
        // Home keeps the same live editor, just like the other ANTHEA modules.
        ShowHome(); OpenModule(BridgeConcept.Module); Check(ReferenceEquals(editor!.bridgeDesign, workspace), "Navigazione Home perde la configurazione.");
        workspace.Inputs.SelectedIndex = 0; workspace.Outputs.SelectedIndex = 0;
        foreach (double width in new[] { 1366d, 960d, 780d })
        {
            Width = width; Height = width < 1000 ? 720 : 900; await Settle();
            var scroller = workspace.DesignScroll; scroller.ScrollToTop(); await Settle();
            Check(scroller.ScrollableWidth < 1, "Scorrimento orizzontale esterno indesiderato.");
            File.WriteAllBytes(Path.Combine(directory, $"bridge_design_{width:0}.png"), Ui.Snapshot(this));
        }
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "PASS: catalogo, quattro schede input, quattordici famiglie, sei schede risultati, listino, invalidazione, undo, A/B, auto, random, PNG, archivio, report Word singolo e di progetto, Home. Ottimizzazione: scheda autonoma, ricerca asincrona, nove variabili graficabili, nuvola completa, selezione da grafico, filtro Pareto, top N senza ricalcolo, preview senza mutazioni, selezione/applicazione fuori dalla top N, invalidazione listino, risultati vuoti, range invalidi, interruzione, cambio prezzo durante il calcolo, annullamento. Layout progetto, ottimizzazione e grafici 1600/1366/960/780.");
    }
}
