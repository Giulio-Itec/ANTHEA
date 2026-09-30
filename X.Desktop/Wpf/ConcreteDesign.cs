using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace
{
    private JsonObject DesignOptions => settings["calcola_armature"]!.AsObject();
    private ConcreteDesignResult? designResult;
    private ConcreteDesignCandidate? designSelection;
    private CancellationTokenSource? designCancellation;
    private string? designSignature;
    private bool designRunning, designStale;
    private readonly ConcreteSectionViewport designPreview = new();
    private readonly TextBlock designStatus = Ui.Text("Impostare gli elenchi e avviare la ricerca sulle combinazioni del foglio.", 12);
    private readonly TextBlock designDescription = Ui.Text("La configurazione proposta comparirà qui.", 12, true);
    private readonly TextBlock designScope = Ui.Text("", 11, color: Ui.Muted);
    private readonly VerificationCards designMetrics = new(2), designChecks = new(2);
    private readonly ProgressBar designProgress = new() { Height = 4, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 5, 0, 5) };
    private readonly JsonGrid designRanking = new([new("rank", "#", ReadOnly: true), new("config", "Configurazione", ReadOnly: true),
        new("peso", "kg/m", ReadOnly: true), new("barre", "Barre long.", ReadOnly: true), new("pezzi", "Pezzi/m", ReadOnly: true),
        new("diametri", "Ø diversi", ReadOnly: true), new("pareto", "Pareto", ReadOnly: true), new("esito", "Dettagli esecutivi", ReadOnly: true)], true);
    private Button designRun = null!, designStop = null!, designApply = null!;
    private InputForm? designForm;

    private UIElement BuildDesignPanel()
    {
        ConcreteReinforcementDesign.Prepare(settings);
        designRun = Ui.Button("Calcola armature", () => _ = RunDesignAsync());
        designStop = Ui.Button("Ferma ricerca", () => designCancellation?.Cancel()); designStop.IsEnabled = false;
        designApply = Ui.Button("Applica configurazione", ApplyDesign); designApply.IsEnabled = false;
        designForm = new InputForm(DesignOptions,
        [new("obiettivo", "Ottimizza", Choices: ["Peso", "Numero barre", "Numero diametri", "Compromesso"]),
         new("simmetrica", "Armatura superiore e inferiore uguale", Bool: true),
         new("diametri", "Ø longitudinali ammessi · separati da ;", "mm", Wide: true),
         new("n_superiori", "Numeri barre superiori", Wide: true), new("n_inferiori", "Numeri barre inferiori", Wide: true),
         new("n_laterali", "Numeri barre laterali per lato", Wide: true), new("n_circolari", "Numeri barre circolari", Wide: true),
         new("staffe", "Staffe", Choices: ["Ottimizza", "Mantieni", "Senza staffe", "Con e senza staffe"]),
         new("diametri_staffe", "Ø staffe ammessi", "mm", Wide: true), new("passi_staffe", "Passi staffe ammessi", "mm", Wide: true),
         new("diametri_ortogonali", "Ø ortogonali · entrambe le facce", "mm", Wide: true), new("passi_ortogonali", "Passi ortogonali ammessi", "mm", Wide: true),
         new("max_diametri", "Massimo Ø distinti"), new("ricerca_completa", "Esamina tutte le configurazioni ammesse", Bool: true), new("max_tentativi", "Massimo tentativi (campione)"),
         new("parallelismo", "Calcoli contemporanei", Choices: ["Automatico", "1", "2", "4", "8"]),
         new("ganci_phi", "Extra sviluppo staffa / legatura", "Ø"), new("riserva_torsione", "Area riservata a torsione per barra", "%"),
         new("peso_peso", "Peso obiettivo · kg/m"), new("peso_barre", "Peso obiettivo · pezzi/m"), new("peso_diametri", "Peso obiettivo · Ø distinti"),
         new("ancoraggio_confermato", "Confermo Asl ancorata per proposte senza staffe", Bool: true),
         new("torsione_confermata", "Confermo schema periferico chiuso per torsione", Bool: true)], _ =>
         { MarkDesignStale(); RefreshDesignInputs(); Modified?.Invoke(); }, true, true);
        var limits = new InputForm(DesignOptions, ConcreteReinforcementDesign.Targets.Select(t => new Field(t.Key, t.Name, "η max")), _ => { MarkDesignStale(); Modified?.Invoke(); }, true);
        var common = Ui.Stack(Ui.Text("Geometria, materiali, copriferro e azioni sono quelli del foglio. Il tipo di elemento e l’esposizione vanno compilati nelle schede comuni. Resistenza 3D iterativa a eccentricità costante.", 11),
            Ui.Bar(Ui.Button("Sezione →", () => tabs.SelectedIndex = 0), Ui.Button("Dettagli →", () => tabs.SelectedIndex = 5)),
            Ui.Button("SLE / esposizione →", () => tabs.SelectedIndex = 3));
        var left = Panel("Ricerca dell’armatura", Scroller(Ui.Stack(common, designForm,
            Group("Limiti per ciascuna verifica", Ui.Stack(Ui.Text("0 < η ≤ 1. La ricerca considera tutte le combinazioni compilate, anche quelle nascoste nei grafici. I dettagli numerici devono rispettare i limiti normativi.", 11), limits), true))),
            "Elenchi discreti · numeri separati da punto e virgola");
        var frame = new ViewportFrame("Configurazione selezionata", designPreview, designPreview.ResetView);
        var selection = Panel("Risultato della ricerca", Ui.Dock(Scroller(Ui.Stack(designDescription, designMetrics, designScope)), bottom: designApply));
        var resultTabs = new TabControl();
        var top = new InputForm(DesignOptions, [new("mostra", "Mostra le prime", Choices: ["1", "5", "10", "25", "50", "Tutte"])], _ => { RefreshDesignRanking(); Modified?.Invoke(); }, true, true);
        Ui.Tab(resultTabs, "Alternative", Ui.Dock(designRanking, top: top));
        Ui.Tab(resultTabs, "Verifiche della proposta", Scroller(designChecks));
        var diagnostics = Ui.Text("", 11); designDiagnostics = diagnostics;
        Ui.Tab(resultTabs, "Ricerca e scarti", Scroller(diagnostics));
        designRanking.SelectionChanged += (_, _) =>
        {
            if (designRanking.SelectedItem is not JsonRow r || designResult is null) return;
            var candidate = designResult.Candidates.FirstOrDefault(c => c.Id.ToString() == r.Values.S("id"));
            if (candidate is not null) SelectDesign(candidate);
        };
        var right = Ui.Dock(Rows(ResultColumns(frame, selection), resultTabs),
            top: Ui.Stack(Ui.Bar(designRun, designStop), designProgress, designStatus));
        Modified += RefreshDesignFreshness;
        RefreshDesignInputs(); return WorkspaceLayout(left, right);
    }
    private TextBlock? designDiagnostics;
    private void RefreshDesignInputs()
    {
        if (designForm is null) return;
        bool circular = Input.S("shape") == "Circolare", plate = DetailingOptions.S("elemento") is "Soletta piena" or "Parete";
        foreach (string k in new[] { "n_superiori", "n_inferiori", "n_laterali", "simmetrica" }) designForm.ShowField(k, !circular);
        designForm.ShowField("n_circolari", circular);
        foreach (string k in new[] { "diametri_ortogonali", "passi_ortogonali" }) designForm.ShowField(k, plate);
        foreach (string k in new[] { "peso_peso", "peso_barre", "peso_diametri" }) designForm.ShowField(k, DesignOptions.S("obiettivo") == "Compromesso");
        bool varying = DesignOptions.S("staffe") is "Ottimizza" or "Con e senza staffe";
        designForm.ShowField("diametri_staffe", varying); designForm.ShowField("passi_staffe", varying);
        designForm.ShowField("max_tentativi", !DesignOptions.B("ricerca_completa", true));
    }
    private string DesignFingerprint()
    {
        var ws = (JsonObject)settings.DeepClone();
        foreach (string key in new[] { "tab", "layout", "resistenze_rapide", "proposta_armature_applicata" }) ws.Remove(key);
        ws["calcola_armature"]!.AsObject().Remove("mostra");
        return Input.ToJsonString() + Data["combinazioni"]!.ToJsonString() + ws.ToJsonString();
    }
    private void MarkDesignStale()
    {
        if (designResult is null && !designRunning) return;
        designStale = true; designCancellation?.Cancel(); designApply.IsEnabled = false;
        designStatus.Text = "Dati modificati: ricalcolare la ricerca prima di applicare una proposta.";
    }
    private void RefreshDesignFreshness()
    {
        RefreshDesignInputs();
        if (designSignature is not null && designSignature != DesignFingerprint()) MarkDesignStale();
    }
    private async Task RunDesignAsync()
    {
        if (designRunning || disposed) return;
        Commit();
        designResult = null; designSelection = null; designRanking.Rows.Clear(); designChecks.Start(); designMetrics.Start();
        designApply.IsEnabled = false; designStale = false; designProgress.Value = 0;
        designDiagnostics!.Text = "";
        designDescription.Text = "Ricerca in corso…"; designScope.Text = ""; designPreview.Section = null; designPreview.InvalidateVisual();
        try
        {
            var options = ConcreteDesignOptions.Read(DesignOptions);
            var snapshot = (JsonObject)Data.DeepClone();
            designSignature = DesignFingerprint(); designRunning = true; designRun.IsEnabled = false; designStop.IsEnabled = true;
            designCancellation?.Dispose(); designCancellation = new CancellationTokenSource();
            var token = designCancellation.Token;
            var updates = new Progress<ConcreteDesignProgress>(p =>
            {
                if (disposed || designStale || !designRunning) return;
                designProgress.Value = p.Planned == 0 ? 0 : (double)p.Evaluated / p.Planned;
                designStatus.Text = $"{p.Evaluated:N0} / {p.Planned:N0} configurazioni · {p.Accepted:N0} ammissibili · {p.Seconds:0.#} s\n{p.Phase}" +
                    (p.Phase.StartsWith("Controlli preliminari") ? $" ({p.PreliminaryChecked:N0} esaminati)" : "") + $" · fino a {p.Workers} calcoli contemporanei";
                if (p.Best is not null && designSelection?.Id != p.Best.Id) SelectDesign(p.Best);
            });
            designResult = await Task.Run(() => ConcreteReinforcementDesign.Optimize(snapshot, options, token, updates));
            if (disposed) return;
            designRunning = false; RefreshDesignFreshness();
            RefreshDesignRanking();
            var r = designResult;
            designProgress.Value = r.Planned == 0 ? 0 : (double)r.Evaluated / r.Planned;
            if (!designStale) designStatus.Text = (r.Completed ? "Ricerca completata" : "Ricerca parziale · interruzione o limite tentativi") +
                $" · {r.Evaluated:N0}/{r.Planned:N0} configurazioni · {r.Candidates.Count:N0} ammissibili" +
                (r.Performance is { } perf ? $" · {perf.Seconds:0.##} s" : "");
            string scarti = string.Join("\n\n", r.Exclusions.OrderByDescending(x => x.Value).Select(x =>
            {
                string text = $"{x.Value:N0} configurazioni · {x.Key}";
                if (r.ExclusionExamples.TryGetValue(x.Key, out var example)) text += "\nEsempio: " + example.Combination +
                    (example.Ratio is double eta ? $" · η = {eta:0.###}, obiettivo ≤ {example.Target:0.###}" : "") + "\n" + example.Detail;
                return text;
            }));
            string coverage = r.Completed ? $"Esaminate tutte le {r.Planned:N0} configurazioni ammesse." :
                $"Ricerca parziale: {r.Planned - r.Evaluated:N0} configurazioni non calcolate. Attivare la ricerca completa o ripeterla senza interromperla.";
            if (r.Candidates.Count == 0)
            {
                designDescription.Text = r.Completed ? "Nessuna configurazione rispetta tutti i vincoli negli elenchi assegnati." : "Nessuna soluzione nel campione esaminato · ricerca incompleta.";
                designScope.Text = coverage + "\n\nPrimi motivi di scarto:\n" + string.Join("\n", r.Exclusions.OrderByDescending(x => x.Value).Take(4).Select(x => $"{x.Value:N0} · {x.Key}")) +
                    "\n\nValori, limiti e combinazioni nella scheda Ricerca e scarti.";
            }
            string timing = r.Performance is { } p ? $"\n\nTempi di ricerca: {p.Seconds:0.##} s · fino a {p.Workers} calcoli contemporanei." +
                (p.FirstSolutionSeconds is double first ? $" Prima configurazione ammissibile dopo {first:0.##} s." : "") +
                $"\n{p.LongitudinalCalculations:N0} sezioni calcolate per resistenza/SLE, {p.LongitudinalReuses:N0} risultati riutilizzati per altri passi staffa; {p.ProbeSections} sezioni nelle prove iniziali." +
                "\nTempi per fase (sommati fra i calcoli contemporanei; non sono la durata complessiva):\n" +
                string.Join("\n", p.Stages.Select(s => $"{s.Name}: {s.Seconds:0.##} s · {s.Calls:N0} esecuzioni")) : "";
            designDiagnostics!.Text = coverage + timing + "\n\nScarti (primo motivo per configurazione; gli esempi non sono soluzioni accettate):\n\n" +
                (scarti.Length > 0 ? scarti : "Nessuno scarto.") + "\n\n" + string.Join("\n\n", r.Notes);
        }
        catch (OperationCanceledException) { if (!disposed) designStatus.Text = "Ricerca interrotta."; }
        catch (Exception ex) { if (!disposed) { designStatus.Text = "Ricerca non avviata: " + ex.Message; designDescription.Text = "Completare i dati indicati e ripetere la ricerca."; } }
        finally
        {
            designRunning = false;
            if (!disposed) { designRun.IsEnabled = true; designStop.IsEnabled = false; designApply.IsEnabled = designSelection is not null && designResult is not null && !designStale; }
        }
    }
    private void RefreshDesignRanking()
    {
        if (designResult is null) return;
        int? selectedId = designSelection?.Id;
        designRanking.Rows.Clear();
        int top = DesignOptions.S("mostra") == "Tutte" ? int.MaxValue : (int)DesignOptions.D("mostra", 10);
        int rank = 0;
        foreach (var c in designResult.Candidates.Take(Math.Max(1, top)))
            designRanking.Rows.Add(new JsonRow(J.Obj(("id", c.Id.ToString()), ("rank", ++rank), ("config", c.Description),
                ("peso", EngineeringFormat.Number(c.Metrics.KgPerM)), ("barre", c.Metrics.Bars), ("pezzi", EngineeringFormat.Number(c.Metrics.PiecesPerM)),
                ("diametri", c.Metrics.Diameters), ("pareto", c.Pareto ? "Sì" : ""), ("esito", c.NeedsReview ? "Da completare" : "Completati")), _ => { }));
        if (designRanking.Rows.Count > 0) designRanking.SelectedItem = designRanking.Rows.FirstOrDefault(r => r.Values.S("id") == selectedId?.ToString()) ?? designRanking.Rows[0];
    }
    private void SelectDesign(ConcreteDesignCandidate c)
    {
        designSelection = c; designPreview.Section = new SezioneCA(c.Input); designPreview.Stirrups = c.Shear; designPreview.InvalidateVisual();
        designDescription.Text = c.Description;
        designMetrics.Start();
        designMetrics.AddValue("Peso stimato", $"{c.Metrics.KgPerM:0.###} kg/m", $"Longitudinali {c.Metrics.LongitudinalKgPerM:0.###}; staffe e ortogonali {c.Metrics.TransverseKgPerM:0.###} kg/m. Esclusi sfridi e sovrapposizioni.");
        designMetrics.AddValue("Quantità", $"{c.Metrics.Bars} barre · {c.Metrics.Diameters} diametri", $"As = {c.Metrics.SteelArea:0.#} mm² · {c.Metrics.PiecesPerM:0.##} pezzi/m convenzionali. " + (c.Pareto ? "Soluzione Pareto." : ""));
        var governing = c.Checks.Where(ch => ch.Ratio is not null).MaxBy(ch => ch.Ratio / ch.Target);
        if (governing is not null) designMetrics.AddValue("Verifica più vicina all’obiettivo", $"η {governing.Ratio:0.###} / {governing.Target:0.###}", governing.Name + " · " + governing.Combination);
        if (designResult?.Baseline is { } baseline) designMetrics.AddValue("Rispetto al foglio iniziale", $"{c.Metrics.KgPerM - baseline.KgPerM:+0.###;-0.###;0} kg/m", $"Peso iniziale stimato {baseline.KgPerM:0.###} kg/m. Il confronto non attesta la verifica dell’armatura iniziale.");
        designScope.Text = (c.NeedsReview ? "Controlli numerici soddisfatti; dettagli esecutivi da completare. " : "Controlli richiesti soddisfatti. ") +
            "La proposta sostituisce barre manuali, secondi strati e staffe del foglio solo con Applica. " +
            (c.Detailing.S("elemento") is "Soletta piena" or "Parete" ? $"Rete ortogonale: As totale {c.Detailing.D("as_secondaria"):0.#} mm²/m, metà per faccia; la vista mostra la sezione longitudinale. " : "") +
            "Le schede dei dettagli conservano le verifiche da completare.";
        designChecks.Start();
        foreach (var ch in c.Checks) designChecks.AddDetail(ch.Name, ch.Passed,
            ch.Combination + (ch.Ratio is double eta ? $" · η = {eta:0.####} ≤ {ch.Target:0.###}" : ""), ch.Detail, "Calcolo della configurazione selezionata");
        designApply.IsEnabled = !designRunning && !designStale && designResult is not null;
    }
    private void ApplyDesign()
    {
        Commit(); RefreshDesignFreshness();
        if (designSelection is not { } candidate || designStale || designRunning || disposed) return;
        // Keep bound JsonObject instances alive; replace their values, not the form data sources.
        Input.Clear(); foreach (var (k, v) in candidate.Input) Input[k] = v?.DeepClone();
        foreach (var (k, v) in candidate.Shear) if (k != "azioni") ShearOptions[k] = v?.DeepClone();
        foreach (string k in new[] { "as_secondaria", "passo_secondaria", "diametro_secondaria" })
            if (candidate.Detailing.ContainsKey(k)) DetailingOptions[k] = candidate.Detailing[k]?.DeepClone();
        DetailingOptions["barre_trattenute"] = "Da confermare"; DetailingOptions["ancoraggio_appoggi"] = "Da confermare";
        settings["ancoraggi"]!["diametro"] = ""; settings["ancoraggi"]!["sigma"] = "";
        foreach (string k in new[] { "confinamento", "posizione", "cautele" }) settings["ancoraggi"]![k] = "Da verificare";
        settings["proposta_armature_applicata"] = J.Obj(("descrizione", candidate.Description), ("metriche", J.Node(candidate.Metrics)), ("controlli", J.Node(candidate.Checks)),
            ("ricerca_completa", designResult!.Completed), ("riserva_torsione_percento", DesignOptions["riserva_torsione"]));
        foreach (var form in new[] { geometry, reinforcement }.Concat(additionalLayers.Values.Select(v => v.Form)))
            foreach (string k in form.Editors.Keys) if (Input.ContainsKey(k)) form.Set(k, Input.S(k), true);
        foreach (var form in detailingForms) foreach (string k in form.Editors.Keys) form.Set(k, DetailingOptions.S(k), true);
        foreach (string k in anchorageForm.Editors.Keys) anchorageForm.Set(k, settings["ancoraggi"].S(k), true);
        foreach (string k in shearForm.Editors.Keys) shearForm.Set(k, ShearOptions.S(k), true);
        foreach (string k in torsionForm.Editors.Keys) torsionForm.Set(k, ShearOptions.S(k), true);
        barInventory.Rows.Clear(); Invalidate();
        designApply.IsEnabled = false;
        designStatus.Text = "Configurazione applicata. Verifiche del foglio in aggiornamento; riscontri esecutivi da confermare sulla nuova disposizione.";
    }
}
