using Anthea.Application.Concrete;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace
{
    private JsonObject DesignOptions => settings["calcola_armature"]!.AsObject();
    private ConcreteResults.DesignResult? designResult;
    private ConcreteResults.DesignCandidate? designSelection;
    private CancellationTokenSource? designCancellation;
    private string? designSignature;
    private bool designRunning, designStale;
    private readonly ConcreteSectionViewport designPreview = new();
    private readonly TextBlock designStatus = Ui.Text(ConcreteDesignPresentation.Idle, 12);
    private readonly TextBlock designDescription = Ui.Text(ConcreteDesignPresentation.NoProposal, 12, true);
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
        ConcreteEditor.PrepareDesign(settings);
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
        var limits = new InputForm(DesignOptions, ConcreteEditor.DesignTargets.Select(t => new Field(t.Key, t.Name, "η max")), _ => { MarkDesignStale(); Modified?.Invoke(); }, true);
        var common = Ui.Stack(Ui.Text(ConcreteDesignPresentation.Notice, 11),
            Ui.Bar(Ui.Button("Sezione →", () => tabs.SelectedIndex = 0), Ui.Button("Dettagli →", () => tabs.SelectedIndex = 5)),
            Ui.Button("SLE / esposizione →", () => tabs.SelectedIndex = 3));
        var left = Panel("Ricerca dell’armatura", Scroller(Ui.Stack(common, designForm,
            Group("Limiti per ciascuna verifica", Ui.Stack(Ui.Text(ConcreteDesignPresentation.LimitsNotice, 11), limits), true))),
            "Elenchi discreti · numeri separati da punto e virgola");
        var frame = new ViewportFrame("Configurazione selezionata", designPreview, designPreview.ResetView);
        var selection = Panel("Risultato della ricerca", Ui.Dock(Scroller(Ui.Stack(designDescription, designMetrics, designScope)), bottom: designApply));
        var resultTabs = new TabControl();
        var top = new InputForm(DesignOptions, [new("mostra", "Mostra le prime", Choices: [.. ConcreteDesignPresentation.ShownChoices])], _ => { RefreshDesignRanking(); Modified?.Invoke(); }, true, true);
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
        foreach (var (key, shown) in ConcreteDesignPresentation.FieldsShown(Input, settings)) designForm.ShowField(key, shown);
    }
    private string DesignFingerprint() => ConcreteDesignPresentation.Fingerprint(Data);
    private void MarkDesignStale()
    {
        if (designResult is null && !designRunning) return;
        designStale = true; designCancellation?.Cancel(); designApply.IsEnabled = false;
        designStatus.Text = ConcreteDesignPresentation.Stale;
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
        designDescription.Text = ConcreteDesignPresentation.Searching; designScope.Text = ""; designPreview.Section = null; designPreview.InvalidateVisual();
        try
        {
            var snapshot = (JsonObject)Data.DeepClone();
            designSignature = DesignFingerprint(); designRunning = true; designRun.IsEnabled = false; designStop.IsEnabled = true;
            designCancellation?.Dispose(); designCancellation = new CancellationTokenSource();
            var token = designCancellation.Token;
            var updates = new Progress<ConcreteResults.DesignProgress>(p =>
            {
                if (disposed || designStale || !designRunning || token.IsCancellationRequested || designCancellation?.Token != token) return;
                designProgress.Value = ConcreteDesignPresentation.Fraction(p.Evaluated, p.Planned);
                designStatus.Text = ConcreteDesignPresentation.ProgressText(p);
                if (p.Best is not null && designSelection?.Id != p.Best.Id) SelectDesign(p.Best);
            });
            var calculated = await Task.Run(() => Anthea.Application.Modules.StructuralQueries.ConcreteReinforcement(snapshot, token, updates));
            if (disposed) return;
            designResult = calculated;
            designRunning = false; RefreshDesignFreshness();
            RefreshDesignRanking();
            var r = designResult;
            designProgress.Value = ConcreteDesignPresentation.Fraction(r.Evaluated, r.Planned);
            if (!designStale) designStatus.Text = ConcreteDesignPresentation.Status(r);
            if (r.Candidates.Count == 0) (designDescription.Text, designScope.Text) = ConcreteDesignPresentation.NoCandidates(r);
            designDiagnostics!.Text = ConcreteDesignPresentation.Diagnostics(r);
        }
        catch (OperationCanceledException) { if (!disposed) designStatus.Text = ConcreteDesignPresentation.Interrupted; }
        catch (Exception ex) { if (!disposed) { designStatus.Text = ConcreteDesignPresentation.NotStarted(ex.Message); designDescription.Text = ConcreteDesignPresentation.CompleteTheData; } }
        finally
        {
            designRunning = false;
            designCancellation?.Dispose(); designCancellation = null;
            if (!disposed) { designRun.IsEnabled = true; designStop.IsEnabled = false; designApply.IsEnabled = designSelection is not null && designResult is not null && !designStale; }
        }
    }
    private void RefreshDesignRanking()
    {
        if (designResult is null) return;
        int? selectedId = designSelection?.Id;
        designRanking.Rows.Clear();
        foreach (var row in ConcreteDesignPresentation.RankingRows(designResult, DesignOptions)) designRanking.Rows.Add(new JsonRow(row, _ => { }));
        if (designRanking.Rows.Count > 0) designRanking.SelectedItem = designRanking.Rows.FirstOrDefault(r => r.Values.S("id") == selectedId?.ToString()) ?? designRanking.Rows[0];
    }
    private void SelectDesign(ConcreteResults.DesignCandidate c)
    {
        designSelection = c; designPreview.Section = Anthea.Application.Concrete.ConcretePreview.PrepareGeometry(c.Input); designPreview.Stirrups = c.Shear; designPreview.InvalidateVisual();
        designDescription.Text = c.Description;
        designMetrics.Start();
        foreach (var value in ConcreteDesignPresentation.Metrics(c, designResult)) designMetrics.AddValue(value.Title, value.Value, value.Explanation);
        designScope.Text = ConcreteDesignPresentation.Scope(c);
        designChecks.Start();
        foreach (var check in ConcreteDesignPresentation.Checks(c)) designChecks.AddDetail(check.Title, check.Passed, check.Values, check.Explanation, check.Reference);
        designApply.IsEnabled = !designRunning && !designStale && designResult is not null;
    }
    private void ApplyDesign()
    {
        Commit(); RefreshDesignFreshness();
        if (designSelection is not { } candidate || designStale || designRunning || disposed) return;
        string shearNotice = ConcreteDesignPresentation.Apply(Input, settings, candidate, designResult!.Completed);
        foreach (var form in new[] { geometry, reinforcement }.Concat(additionalLayers.Values.Select(v => v.Form)))
            foreach (string k in form.Editors.Keys) if (Input.ContainsKey(k)) form.Set(k, Input.S(k), true);
        foreach (var form in detailingForms) foreach (string k in form.Editors.Keys) form.Set(k, DetailingOptions.S(k), true);
        foreach (string k in anchorageForm.Editors.Keys) anchorageForm.Set(k, settings["ancoraggi"].S(k), true);
        foreach (string k in shearForm.Editors.Keys) shearForm.Set(k, ShearOptions.S(k), true);
        foreach (string k in torsionForm.Editors.Keys) torsionForm.Set(k, ShearOptions.S(k), true);
        barInventory.Rows.Clear(); Invalidate();
        designApply.IsEnabled = false;
        designStatus.Text = ConcreteDesignPresentation.Applied(shearNotice);
    }
}
