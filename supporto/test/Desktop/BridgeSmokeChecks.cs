using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeBridge(string directory)
    {
        testing = true; Directory.CreateDirectory(directory); WindowState = WindowState.Normal; Width = 1600; Height = 990;
        foreach (int oldPage in new[] { 0, 1, 2 })
        {
            var legacyData = BridgeSection.Defaults();
            legacyData["ui_mista"] = J.Obj(("layout_version", 2), ("tab", oldPage), ("vista_0", 2), ("vista_1", 1), ("vista_2", 0),
                ("risultato_0", 2), ("risultato_1", 1), ("risultato_2", 0), ("fase", 1), ("split_ingressi", .34));
            using var legacy = new BridgeWorkspace(legacyData);
            if (legacy.Pages.SelectedIndex != Math.Min(oldPage, 1) || legacy.DisplayChoice.SelectedIndex != new[] { 2, 1, 0 }[oldPage] ||
                legacy.Results.SelectedIndex != new[] { 2, 1, 3 }[oldPage] || legacyData["ui_mista"].D("fase") != 1 || legacyData["ui_mista"].D("split_ingressi") != .34)
                throw new Exception("Migrazione dalle tre schede perde la vista attiva o le preferenze.");
        }
        document = Archivio.Documento(BridgeSection.Module); document["dati"]!["plate2"] = true;
        document["dati"]!["ui_mista"] = J.Obj(("tab", 2), ("vista_2", 1), ("risultato_2", 1));
        dirty = false; currentSheet = null; ShowSheet(document);
        var bridge = editor!.bridge!;
        async Task Wait()
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (bridge.Calculation is null || bridge.Busy)
            { if (DateTime.UtcNow > deadline) throw new Exception("Sezione composta: risultato automatico non disponibile."); await Task.Delay(80); }
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        }
        await Wait();
        await CheckBridgeGeometryView(bridge, Wait, directory);
        await CheckBridgeStressView(bridge, Wait, directory);
        await CheckBridgeShrinkageUi(directory);
        await CheckBridgeShearUi(directory);
        await CheckBridgeDetailsUi(directory);
        if (bridge.Pages.Items.Count != 2 || bridge.Results.Items.Count != 5 || bridge.Pages.SelectedIndex != 1 || bridge.Results.SelectedIndex != 1 || bridge.DisplayChoice.SelectedIndex != 1)
            throw new Exception("Disposizione compatta o migrazione della precedente scheda Omogeneizzazione non riuscita.");
        var initialCalculation = bridge.Calculation;
        for (int input = 0; input < bridge.Pages.Items.Count; input++)
        {
            bridge.Pages.SelectedIndex = input; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
            if (!ReferenceEquals(bridge.Calculation, initialCalculation)) throw new Exception("Navigazione nelle schede invalida il calcolo.");
            File.WriteAllBytes(Path.Combine(directory, $"mista_input_{input}.png"), Ui.Snapshot(this));
        }
        if (bridge.InputForms.Count(f => f.Editors.ContainsKey("stato")) != 1 || bridge.InputForms.Count(f => f.Editors.ContainsKey("classe4")) != 1 ||
            Ui.Descendants<Expander>(bridge).Any(e => (e.Header as TextBlock)?.Text is "Criteri di calcolo" or "Campo del modello"))
            throw new Exception("Opzioni duplicate o informazioni statiche ancora presenti nei pannelli operativi.");
        var stateChoice = (ComboBox)bridge.InputForms.Single(f => f.Editors.ContainsKey("stato")).Editors["stato"];
        if (!stateChoice.IsVisible) throw new Exception("Limiti tensionali assenti dalla scheda Fasi e tensioni.");
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var info = Application.Current.Windows.OfType<Window>().Single(w => w.Title == "Sezione composta · informazioni sul modello");
            info.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "mista_info.png"), Ui.Snapshot(info));
            var sketches = Ui.Descendants<BridgeMethodSketch>(info).ToArray();
            if (sketches.Length != 4) throw new Exception("Schemi esplicativi del modello incompleti.");
            foreach (var sketch in sketches)
            {
                sketch.BringIntoView(); info.UpdateLayout();
                File.WriteAllBytes(Path.Combine(directory, $"modello_schema_{sketch.Kind}.png"), Ui.Snapshot(sketch));
                File.WriteAllBytes(Path.Combine(directory, $"modello_pagina_{sketch.Kind}.png"), Ui.Snapshot(info));
            }
            info.Close();
        }));
        Ui.Descendants<Button>(bridge).Single(b => b.Content?.ToString() == "Info modello…").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!ReferenceEquals(initialCalculation, bridge.Calculation)) throw new Exception("Le informazioni sul modello modificano il risultato.");
        var lastMoment = (TextBox)bridge.InputForms.Last(f => f.Editors.ContainsKey("Mx")).Editors["Mx"];
        var oldMoment = bridge.Data.Array("fasi").Last().D("Mx");
        lastMoment.BringIntoView(); lastMoment.Focus(); lastMoment.Text = (oldMoment + 500).ToString(System.Globalization.CultureInfo.InvariantCulture);
        bridge.Drawing.Focus();
        if (bridge.Calculation is not null) throw new Exception("Modifica delle azioni conserva risultati obsoleti.");
        await Wait();
        if (!initialCalculation!.Stages[0].Points.Select(p => p.Stress).SequenceEqual(bridge.Calculation!.Stages[0].Points.Select(p => p.Stress)) ||
            initialCalculation.Stages.Last().Points.Select(p => p.Stress).SequenceEqual(bridge.Calculation.Stages.Last().Points.Select(p => p.Stress)))
            throw new Exception("Una modifica all'ultima fase non aggiorna correttamente i risultati cumulati.");
        var beforeLimits = bridge.Calculation.Stages.Last();
        stateChoice.SelectedItem = "SLE rara"; await Wait();
        var afterLimits = bridge.Calculation!.Stages.Last();
        if (beforeLimits.Points.Where(p => p.Active).Select(p => p.Limit).SequenceEqual(afterLimits.Points.Where(p => p.Active).Select(p => p.Limit)) ||
            beforeLimits.Points.Zip(afterLimits.Points).Any(pair => Math.Abs(pair.First.Stress - pair.Second.Stress) > 1e-7))
            throw new Exception("La scelta dei limiti deve aggiornare le verifiche mantenendo le tensioni elastiche.");
        stateChoice.SelectedItem = "SLU"; await Wait();
        lastMoment.Text = oldMoment.ToString(System.Globalization.CultureInfo.InvariantCulture); await Wait();
        await CheckBridgeOptions(bridge, Wait, directory);
        bridge.Pages.SelectedIndex = 0;
        bridge.StageChoice.SelectedIndex = 0;
        if (bridge.Drawing.Stage is not null || bridge.Drawing.Mode != 2 || bridge.StageChoice.IsVisible || bridge.DisplayChoice.IsVisible) throw new Exception("Il pannello di controllo contiene ancora la selezione o il grafico delle tensioni.");
        bridge.StageChoice.SelectedIndex = bridge.Calculation.Stages.Count - 1;
        bridge.Pages.SelectedIndex = 1;
        bridge.Results.SelectedIndex = 3;
        bridge.DisplayChoice.SelectedIndex = 0;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "mista_fasi_tensioni.png"), Ui.Snapshot(this));
        // Exercise the same expand/reparent command used by the concrete viewport.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var expanded = Application.Current.Windows.OfType<Window>().Single(w => w != this && w.Title == "Sezione composta · geometria e tensioni");
            expanded.UpdateLayout(); File.WriteAllBytes(Path.Combine(directory, "mista_espansa.png"), Ui.Snapshot(expanded)); expanded.Close();
        }));
        bridge.Viewport.Toolbar.Children.OfType<Button>().Single(b => b.Content?.ToString() == "Espandi").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!ReferenceEquals(bridge.Viewport.Host.Content, bridge.Drawing)) throw new Exception("Chiusura vista espansa perde la viewport.");
        File.WriteAllBytes(Path.Combine(directory, "mista_viewport.png"), Ui.Snapshot(bridge.Drawing));
        bridge.Pages.SelectedIndex = 1;
        for (int result = 0; result < bridge.Results.Items.Count; result++)
        {
            bridge.Results.SelectedIndex = result; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
            if (bridge.Drawing.ActualWidth > bridge.ActualWidth - 380) throw new Exception("La scheda risultati espande la viewport oltre il pannello.");
            File.WriteAllBytes(Path.Combine(directory, $"mista_risultati_{result}.png"), Ui.Snapshot(this));
        }
        bridge.Results.SelectedIndex = 3; bridge.DisplayChoice.SelectedIndex = 1;
        for (int stage = 0; stage < bridge.Calculation!.Stages.Count; stage++)
        {
            bridge.StageChoice.SelectedIndex = stage;
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory, $"mista_fase_{stage}.png"), Ui.Snapshot(this));
        }
        bridge.DisplayChoice.SelectedIndex = 0; bridge.Drawing.ResetView();
        foreach (var size in new[] { (1366, 768), (960, 640) })
        {
            Width = size.Item1; Height = size.Item2; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory, $"mista_{size.Item1}.png"), Ui.Snapshot(this));
        }
        Width = 1600; Height = 990; bridge.Pages.SelectedIndex = 0; await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var web = bridge.InputForms.First(f => f.Editors.ContainsKey("t_web"));
        var webEditor = (TextBox)web.Editors["t_web"]; webEditor.BringIntoView(); webEditor.Focus();
        if (!webEditor.IsKeyboardFocused) throw new Exception("Campo anima non raggiungibile con focus.");
        var beforeTyping = bridge.Calculation;
        webEditor.Text = "10.123456789";
        if (bridge.Data.D("t_web") != 14 || !ReferenceEquals(beforeTyping, bridge.Calculation)) throw new Exception("Input numerico acquisito prima dell'uscita dal campo.");
        bridge.Drawing.Focus();
        if (bridge.Calculation is not null || bridge.Data.D("t_web") != 10.123456789) throw new Exception("Uscita dal campo non acquisisce il valore completo.");
        await Wait();
        if (bridge.Calculation!.Geometry.WebThickness != 10.123456789 || web.Get("t_web") != "10.123456789") throw new Exception("Presentazione numerica perde precisione.");
        ((TextBox)web.Editors["t_web"]).Text = "10";
        if (bridge.Calculation is not null) throw new Exception("Risultato obsoleto conservato dopo modifica.");
        await Wait();
        if (bridge.Calculation!.Geometry.WebThickness != 10) throw new Exception("Modifica geometrica non acquisita.");
        bridge.Pages.SelectedIndex = 1; bridge.Results.SelectedIndex = 2;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "mista_classe4.png"), Ui.Snapshot(this));
        ((TextBox)web.Editors["t_web"]).Text = "invalido";
        await Task.Delay(650);
        if (bridge.Calculation is not null || bridge.Drawing.Stage is null || bridge.Drawing.Geometry is null || !bridge.Drawing.IsStale) throw new Exception("Dati invalidi perdono l'ultimo risultato o lo presentano come corrente.");
        ((TextBox)web.Editors["t_web"]).Text = "10"; await Wait();
        var split = bridge.Splits["ingressi"];
        split.ColumnDefinitions[0].Width = new GridLength(.34, GridUnitType.Star); split.ColumnDefinitions[2].Width = new GridLength(.66, GridUnitType.Star); UpdateLayout();
        double splitRatio = split.ColumnDefinitions[0].ActualWidth / (split.ColumnDefinitions[0].ActualWidth + split.ColumnDefinitions[2].ActualWidth);
        split.Children.OfType<GridSplitter>().Single().RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
        if (Math.Abs(splitRatio - .34) > .002 || Math.Abs(bridge.Data["ui_mista"].D("split_ingressi") - splitRatio) > 1e-9) throw new Exception("Posizione dei pannelli non memorizzata.");
        Commit(); var retained = editor; var computed = bridge.Calculation;
        ShowHome(); ResumeCalculation();
        if (!ReferenceEquals(editor, retained) || !ReferenceEquals(bridge.Calculation, computed)) throw new Exception("Home perde la sezione composta.");
        Archivio.Scrivi(Path.Combine(directory, "ponte.anthea"), document); editor.ExportResult(Path.Combine(directory, "ponte-risultati.json"));
        var saved = editor.Data.DeepClone(); dirty = false; LoadFile(Path.Combine(directory, "ponte.anthea"));
        if (!JsonNode.DeepEquals(saved, editor!.Data)) throw new Exception("Riapertura perde dati delle fasi.");
        await editor.CalculateAsync();
        if (editor.Result is null) throw new Exception("Ricalcolo dopo riapertura fallito.");
        if (editor.bridge!.Pages.SelectedIndex != 1 || editor.bridge.Results.SelectedIndex != 2 || Math.Abs(editor.bridge.Splits["ingressi"].ColumnDefinitions[0].Width.Value - splitRatio) > 1e-9) throw new Exception("Riapertura perde scheda o disposizione pannelli.");
        var reportCalculation = editor.bridge.Calculation; int reportPage = editor.bridge.Pages.SelectedIndex, reportStage = editor.bridge.StageChoice.SelectedIndex;
        var report = editor.BuildReport("Esempio di sezione composta da ponte", ReportBridge.DefaultSections());
        File.WriteAllBytes(Path.Combine(directory, "Relazione_sezione_composta.docx"), report);
        using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(report)))
        {
            using var stream = zip.GetEntry("word/document.xml")!.Open(); var xml = System.Xml.Linq.XDocument.Load(stream);
            if (zip.Entries.Count(e => e.FullName.StartsWith("word/media/")) != 4 || !xml.ToString().Contains("Situazione 3 dopo")) throw new Exception("Report incompleto per situazioni o grafici.");
        }
        if (!ReferenceEquals(reportCalculation, editor.bridge.Calculation) || reportPage != editor.bridge.Pages.SelectedIndex || reportStage != editor.bridge.StageChoice.SelectedIndex) throw new Exception("Report modifica calcolo o selezione corrente.");
        using (var selectedZip = new System.IO.Compression.ZipArchive(new MemoryStream(editor.BuildReport("Solo geometria", ["geometria"]))))
        {
            using var stream = selectedZip.GetEntry("word/document.xml")!.Open(); var xml = System.Xml.Linq.XDocument.Load(stream).ToString();
            if (selectedZip.Entries.Any(e => e.FullName.StartsWith("word/media/")) || xml.Contains("Situazione 1 dopo") || !xml.Contains("Geometria e armature")) throw new Exception("Opzioni del report non rispettate.");
        }
        var projectSheet = J.Obj(("id", "bridge-report"), ("nome", "Sezione composta nel progetto"), ("modulo_id", BridgeSection.Module), ("dati", editor.Data.DeepClone()));
        var projectSection = J.Obj(("id", "bridge-section"), ("nome", "Ponte · report di progetto"), ("fogli", new JsonArray(projectSheet)), ("strutture", new JsonArray()));
        string projectReport = Path.Combine(directory, "Relazione_progetto_ponte.docx");
        if (await GenerateSectionReport(projectSection, projectReport, _ => { }, CancellationToken.None) != 0) throw new Exception("Sezione composta mancante nel report di progetto.");
        using (var projectZip = new System.IO.Compression.ZipArchive(File.OpenRead(projectReport)))
        {
            using var stream = projectZip.GetEntry("word/document.xml")!.Open(); var xml = System.Xml.Linq.XDocument.Load(stream).ToString();
            if (projectZip.Entries.Count(e => e.FullName.StartsWith("word/media/")) != 4 || !xml.Contains("Situazione 3 dopo") || !xml.Contains("Geometria e armature")) throw new Exception("Report di progetto perde dati o grafici della sezione composta.");
        }
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK: due schede numerate, cinque gruppi di risultati, migrazione delle disposizioni precedenti, limiti e fasi nella stessa scheda, finestra informativa dedicata, aggiornamento dei risultati dopo modifica delle azioni, tre situazioni, navigazione senza invalidazione, geometria a due piastre, viewport condivisa con CA, espansione/rientro e PNG, 1600/1366/960, input a fine modifica con precisione conservata, calcolo automatico, invalidazione, dati non validi, Home/Riprendi, archivio e riapertura, persistenza scheda e separatori, export JSON, report Word completo con quattro immagini, selezione contenuti, nessuna modifica al risultato, integrazione nella relazione di progetto.");
        dirty = false;
    }
}
