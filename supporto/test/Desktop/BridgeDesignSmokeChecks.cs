using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;
using Anthea.Application.Documents;
using Anthea.Presentation.Modules.BridgeDesign;
using X.Desktop.Views.BridgeDesign;

namespace X.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// ui/smoke-bridge-design on the XAML view of Bridge Design, as the Release opens it since the switch of 23/10/2026 (the historic view
    /// BridgeDesignWorkspace is archived in supporto/SUPERATI/ui-storica/bridge-design): the same scenario and the same checks as on the historic view.
    /// The members of the historic view are those of the view (pages, tabs, buttons, scrollers, drawings and charts, «Mostra le prime N») and of its
    /// ViewModel (calculation, parameters, undo, families, optimization), with the same names where they exist (<see cref="BridgeDesignSmoke"/>).
    /// </summary>
    internal async Task SmokeBridgeDesign(string directory)
    {
        Directory.CreateDirectory(directory); WindowState = WindowState.Normal; Width = 1600; Height = 990;
        document = Archivio.Documento(BridgeConcept.Module); dirty = false; currentSheet = null; ShowSheet(document);
        var workspace = BridgeDesignSmoke.Of(editor ?? throw new Exception("Bridge Design non aperto."), this);
        var sheet = workspace.Sheet; var view = workspace.View;
        async Task Settle() { await sheet.WaitForCalculationAsync(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        void Check(bool ok, string error) { if (!ok) throw new Exception(error); }
        await Settle();
        Check(sheet.Calculation is not null, "Risultato iniziale assente.");
        File.WriteAllBytes(Path.Combine(directory, "bridge_design_1600.png"), Ui.Snapshot(this));
        double cost = sheet.Calculation!.TotalCost, co2 = sheet.Calculation.Carbon;
        sheet.Pin(); view.OutputTabs.SelectedIndex = 3; await Settle();
        workspace.Type("rates/concrete_deck", "480"); await Settle();
        Check(sheet.Calculation!.TotalCost > cost && sheet.Calculation.Carbon == co2, "Il prezzo non si propaga correttamente.");
        sheet.Undo(); await Settle();
        Check(workspace.Text("rates/concrete_deck") == "260", "Annulla non ripristina il controllo prezzi.");
        Check(Math.Abs(sheet.Calculation!.TotalCost - cost) < 1e-6, "Annulla non ripristina il calcolo.");
        workspace.Type("rates/rebar", "1234"); await Settle();
        workspace.Press("Applica valori orientativi 2026"); await Settle();
        Check(workspace.Text("rates/rebar") == "1660" && sheet.Data["rates"].D("rebar") == 1660, "Listino 2026 non aggiorna editor e dati");
        sheet.Undo(); await Settle(); Check(workspace.Text("rates/rebar") == "1234", "Undo listino perde prezzo personale");
        sheet.Undo(); await Settle();
        workspace.Type("input/length", ""); Check(sheet.Calculation is null && editor!.Result is null, "Risultati obsoleti con input vuoto.");
        sheet.Undo(); await Settle(); Check(sheet.Calculation is not null, "Ripristino da input invalido fallito.");
        for (int tab = 0; tab < 4; tab++)
        {
            view.InputTabs.SelectedIndex = tab; view.OutputTabs.SelectedIndex = 0; await Settle();
            File.WriteAllBytes(Path.Combine(directory, $"input_{tab}.png"), Ui.Snapshot(this));
        }
        for (int tab = 0; tab < 6; tab++)
        {
            view.OutputTabs.SelectedIndex = tab; view.OutputTabs.BringIntoView(); await Settle();
            File.WriteAllBytes(Path.Combine(directory, $"risultati_{tab}.png"), Ui.Snapshot(this));
        }
        view.InputTabs.SelectedIndex = 2; await Settle();
        foreach (var family in BridgeConcept.Families)
        {
            var button = Ui.Descendants<Button>(view).Single(b => (string)b.GetValue(System.Windows.Automation.AutomationProperties.NameProperty) == family.Name);
            BridgeSmoke.Press(button);
            workspace.Type("input/length", (BridgeConcept.HasTowers(family.Id) ? 2 * Math.Min(450, (family.MinSpan + family.MaxSpan) / 2) : (family.MinSpan + family.MaxSpan) / 2 * 4.6).ToString(System.Globalization.CultureInfo.InvariantCulture));
            workspace.Type("input/height", "40");
            await Settle();
            if (sheet.Calculation is null)
            {
                File.WriteAllText(Path.Combine(directory, "failed-family-input.json"), sheet.Data.ToJsonString());
                _ = Anthea.Calculations.CalculationService.Calculate(BridgeConcept.Module, sheet.Data);
            }
            Check(sheet.Calculation?.Family.Id == family.Id, "Cambio famiglia fallito: " + family.Id);
            File.WriteAllBytes(Path.Combine(directory, family.Id + "_prospetto.png"), view.Drawing.Png(false));
            File.WriteAllBytes(Path.Combine(directory, family.Id + "_sezione.png"), view.Drawing.Png(true));
        }
        sheet.AutoSize(); await Settle(); Check(sheet.Calculation is not null, "Dimensioni automatiche fallite.");
        for (int seed = 0; seed < 100; seed++)
        {
            sheet.Randomize(new Random(seed)); await Settle();
            Check(sheet.Calculation is not null, "Ponte casuale non calcolabile, seed " + seed); sheet.Undo();
        }
        workspace.Type("input/pile_count", "1"); await Settle(); _ = view.Drawing.Png(); sheet.Undo();
        // The historic smoke wrote the inputs into the document of the view and then pressed «Dimensioni automatiche»: here the document of the
        // sheet, then AutoSize, which recalculates from it.
        sheet.Data["input"] = BridgeConcept.Defaults()["input"]!.DeepClone();
        sheet.Data["input"]!["length"] = 120; sheet.Data["input"]!["spans"] = 3;
        sheet.Data["input"]!["height"] = 12; sheet.Data["input"]!["soil"] = "Roccia"; sheet.Data["input"]!["obstacle"] = "Nessuno";
        sheet.AutoSize(); await Settle();
        workspace.Press("Ottimizza"); await Settle();
        Check(view.Pages.SelectedIndex == 1 && view.OutputTabs.Items.Count == 6, "Tasto Ottimizza non apre la nuova scheda autonoma.");
        await sheet.RunOptimizationAsync(); await Settle();
        Check(sheet.Optimization?.Candidates.Length > 0, "Ottimizzazione UI senza risultati.");
        File.WriteAllBytes(Path.Combine(directory, "ottimizzazione_risultati.png"), Ui.Snapshot(this));
        var complete = sheet.Optimization!;
        string unchanged = sheet.Data.ToJsonString();
        Check(view.CloudPlot.PointCount == complete.Solutions.Length, "Nuvola non include tutte le geometrie ammesse.");
        Check(view.TracePlot.PointCount == complete.Trials.Count(t => t.Depth.HasValue), "Traccia incompleta.");
        view.TopBox.Text = "25";
        workspace.Press("Aggiorna elenco"); await Settle();
        Check(ReferenceEquals(complete, sheet.Optimization) && workspace.DisplayedOptimizationCount == Math.Min(25, complete.Solutions.Length), "N ripete o limita la ricerca.");
        view.OptimizationScroll.ScrollToBottom(); await Settle();
        File.WriteAllBytes(Path.Combine(directory, "ottimizzazione_graduatoria.png"), Ui.Snapshot(this));
        sheet.SelectSolution(complete.Solutions.Length - 1); await Settle();
        Check(sheet.SelectedRank == complete.Solutions.Length && sheet.Data.ToJsonString() == unchanged, "Selezione fuori dalla top N modifica il progetto o perde il rango.");
        Check(view.Preview.Result!.TotalCost == complete.Solutions.Last().Trial.Cost, "Preview non corrisponde al rango scelto.");
        var point = view.CloudPlot.Points.First(p => p.Rank == 1);
        view.CloudPlot.SelectAt(point.Point); await Settle();
        Check(sheet.SelectedRank == 1, "Click sul grafico non seleziona l’ottimo.");
        view.CloudPlot.BringIntoView(); await Settle();
        File.WriteAllBytes(Path.Combine(directory, "ottimizzazione_grafici.png"), Ui.Snapshot(this));
        for (int v = 0; v < 9; v++)
        {
            sheet.Variable = v; view.TracePlot.InvalidateVisual(); await Settle();
            Check(view.TracePlot.Variable == v, "La traccia non segue la variabile scelta: " + v);
            // Render a detached copy: an element inside a scroller inherits clipping and offsets in RenderTargetBitmap.
            var plot = new BridgeOptimizationPlot { Width = 900, Height = 310, Variable = v };
            plot.SetData(complete.Trials, complete.Solutions, complete.Baseline);
            plot.Measure(new Size(900, 310)); plot.Arrange(new Rect(0, 0, 900, 310));
            File.WriteAllBytes(Path.Combine(directory, $"traccia_{v}.png"), Ui.Snapshot(plot));
        }
        sheet.ParetoOnly = true; view.CloudPlot.InvalidateVisual(); await Settle();
        Check(view.CloudPlot.PointCount == complete.Solutions.Count(s => s.Pareto), "Filtro Pareto errato.");
        sheet.ParetoOnly = false; sheet.Variable = 2;
        foreach (double width in new[] { 1366d, 960d, 780d })
        {
            Width = width; Height = width < 1000 ? 720 : 900; await Settle();
            view.OptimizationScroll.ScrollToTop(); await Settle();
            Check(view.OptimizationScroll.ScrollableWidth < 1, "Scheda ottimizzazione deborda orizzontalmente.");
            File.WriteAllBytes(Path.Combine(directory, $"ottimizzazione_{width:0}.png"), Ui.Snapshot(this));
            view.CloudPlot.BringIntoView(); await Settle();
            File.WriteAllBytes(Path.Combine(directory, $"nuvola_{width:0}.png"), Ui.Snapshot(this));
        }
        Width = 1600; Height = 990; await Settle();
        workspace.Type("rates/concrete_deck", "333");
        Check(sheet.Optimization is null, "Listino modificato non invalida le alternative."); sheet.Undo();
        Check(sheet.SolutionResult is null && view.Preview.Result is null, "Invalidazione lascia una preview obsoleta.");
        await sheet.RunOptimizationAsync(new Anthea.Application.BridgeDesign.BridgeDesignModel.OptimizationOptions { MaxDepth = .1 }); await Settle();
        Check(sheet.Optimization?.Solutions.Length == 0 && sheet.SolutionResult is null, "Ricerca vuota mostra un vecchio candidato.");
        await sheet.RunOptimizationAsync(new Anthea.Application.BridgeDesign.BridgeDesignModel.OptimizationOptions { MinDepth = 3, MaxDepth = 2 }); await Settle();
        Check(sheet.Optimization is null && sheet.SolutionResult is null, "Range invalido conserva risultati applicabili.");
        var running = sheet.RunOptimizationAsync();
        workspace.Press("Interrompi");
        await running; await Settle();
        Check(sheet.Optimization is null && sheet.SolutionResult is null, "Cancellazione lascia risultati provvisori applicabili.");
        running = sheet.RunOptimizationAsync(); workspace.Type("rates/concrete_deck", "334");
        await running; await Settle(); Check(sheet.Optimization is null, "Ricerca completata su listino obsoleto."); sheet.Undo();
        await sheet.RunOptimizationAsync(); await Settle();
        string original = sheet.Data["input"]!.ToJsonString();
        int chosen = sheet.Optimization!.Solutions.Length - 1;
        double predictedCost = sheet.Optimization.Solutions[chosen].Trial.Cost!.Value;
        sheet.SelectSolution(chosen); sheet.ApplySolution(chosen); await Settle();
        Check(Math.Abs(sheet.Calculation!.TotalCost - predictedCost) < 1e-6, "Applica soluzione non riproduce il costo previsto.");
        Check(view.Pages.SelectedIndex == 0 && view.OutputTabs.SelectedIndex == 5, "Applicazione non mostra il progetto e le quote tecniche.");
        File.WriteAllBytes(Path.Combine(directory, "soluzione_ottimizzata_quote.png"), Ui.Snapshot(this));
        sheet.Undo(); await Settle();
        Check(sheet.Data["input"]!.ToJsonString() == original, "Annulla non ripristina la configurazione pre-ottimizzazione.");
        // Save through the shell to exercise normal archive integration, then reopen an independent editor.
        Commit(); string archive = Path.Combine(directory, "BridgeDesign.anthea"); DocumentService.Save(archive, document);
        var reopened = DocumentService.Open(archive); using var restored = sheetEditors.Open(BridgeConcept.Module, reopened["dati"]!.AsObject());
        await restored.CalculateAsync();
        Check(restored.HasResults && restored.Data["alternative_a"] is JsonObject, "Archivio incompleto al riavvio.");
        File.WriteAllBytes(Path.Combine(directory, "BridgeDesign.docx"), editor!.BuildReport("Predimensionamento del ponte", []));
        var section = J.Obj(("nome", "Opera di prova"), ("fogli", new JsonArray(J.Obj(("nome", "Bridge Design"), ("modulo_id", BridgeConcept.Module), ("dati", sheet.Data.DeepClone())))), ("strutture", new JsonArray()));
        int unavailable = await GenerateSectionReport(section, Path.Combine(directory, "Progetto.docx"), _ => { }, CancellationToken.None);
        Check(unavailable == 0, "Bridge Design non incluso nel report di progetto.");
        // Home keeps the same live editor, just like the other ANTHEA modules.
        ShowHome(); OpenModule(BridgeConcept.Module); Check(editor is MvvmSheetEditor { Sheet: var again } && ReferenceEquals(again, sheet), "Navigazione Home perde la configurazione.");
        view.InputTabs.SelectedIndex = 0; view.OutputTabs.SelectedIndex = 0;
        foreach (double width in new[] { 1366d, 960d, 780d })
        {
            Width = width; Height = width < 1000 ? 720 : 900; await Settle();
            var scroller = view.DesignScroll; scroller.ScrollToTop(); await Settle();
            Check(scroller.ScrollableWidth < 1, "Scorrimento orizzontale esterno indesiderato.");
            File.WriteAllBytes(Path.Combine(directory, $"bridge_design_{width:0}.png"), Ui.Snapshot(this));
        }
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "PASS: catalogo, quattro schede input, quattordici famiglie, sei schede risultati, listino, invalidazione, undo, A/B, auto, random, PNG, archivio, report Word singolo e di progetto, Home. Ottimizzazione: scheda autonoma, ricerca asincrona, nove variabili graficabili, nuvola completa, selezione da grafico, filtro Pareto, top N senza ricalcolo, preview senza mutazioni, selezione/applicazione fuori dalla top N, invalidazione listino, risultati vuoti, range invalidi, interruzione, cambio prezzo durante il calcolo, annullamento. Layout progetto, ottimizzazione e grafici 1600/1366/960/780. Vista XAML della Release (MvvmSheetEditor).");
    }
}

/// <summary>
/// F5.12: Bridge Design as ui/smoke-bridge-design and ui/smoke-bridge-traffic drive it, on the XAML view hosted by <see cref="MvvmSheetEditor"/> (the
/// Release since the switch of 23/10/2026): the sheet, its view, the parameters typed in their text boxes as the user types them (the text box of the
/// view when its tab is shown, otherwise the text of the parameter of the sheet, which is what the binding of the box writes), the buttons pressed
/// as the user presses them, the rows of the ranking shown.
/// </summary>
internal sealed class BridgeDesignSmoke(BridgeDesignSheetViewModel sheet, BridgeDesignSheetView view)
{
    internal BridgeDesignSheetViewModel Sheet => sheet;
    internal BridgeDesignSheetView View => view;

    /// <summary>The sheet open in <paramref name="window"/>: the MVVM editor of Bridge Design and its view.</summary>
    internal static BridgeDesignSmoke Of(ISheetEditor editor, Window window)
    {
        if (editor is not MvvmSheetEditor { Sheet: BridgeDesignSheetViewModel sheet }) throw new InvalidOperationException("Bridge Design non aperto con la vista MVVM: " + editor.Module);
        window.UpdateLayout();
        return new(sheet, Ui.Descendants<BridgeDesignSheetView>(window).Single());
    }

    /// <summary>The text box of the parameter <paramref name="key"/> («gruppo/chiave») in the view, when its tab is shown.</summary>
    internal TextBox? Box(string key) => Ui.Descendants<TextBox>(view).FirstOrDefault(t => t.DataContext is BridgeDesignParameter p && p.Group + "/" + p.Key == key && t.IsVisible);

    /// <summary>A value typed in the parameter (as the historic smoke wrote Editors[key].Text).</summary>
    internal void Type(string key, string text)
    {
        if (Box(key) is { } box) box.Text = text;
        else sheet.Parameters[key].Text = text;
    }

    /// <summary>The text of the parameter (as the historic smoke read Editors[key].Text).</summary>
    internal string Text(string key) => Box(key)?.Text ?? sheet.Parameters[key].Text;

    /// <summary>The visible button with this caption pressed as the user presses it (the Click event, then the command).</summary>
    internal void Press(string caption)
    {
        view.UpdateLayout();
        BridgeSmoke.Press(Ui.Descendants<Button>(view).Single(b => b.Content is string label && label == caption && b.IsVisible));
    }

    /// <summary>The rows of the ranking shown («Mostra le prime N»), as DisplayedOptimizationCount of the historic view.</summary>
    internal int DisplayedOptimizationCount => sheet.SearchResults?.OfType<BridgeDesignTable>().FirstOrDefault()?.Rows.Length ?? 0;
}
