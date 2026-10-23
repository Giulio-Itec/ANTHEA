using Anthea.Application.Presentation;
using Anthea.Application.Modules;
using Anthea.Application.Scheduling;
using Anthea.Presentation.Modules.CompositeSection;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace : UserControl, IDisposable
{
    internal JsonObject Data { get; }
    internal BridgeResultView? Calculation { get; private set; }
    internal BridgeResultView? DisplayedCalculation { get; private set; }
    internal bool ResultsAreStale => Calculation is null && DisplayedCalculation is not null;
    internal JsonObject? Result => Calculation?.Json();
    internal bool Busy { get; private set; }
    internal event Action? Modified;
    internal readonly BridgeDrawing Drawing = new();
    internal readonly TabControl Pages = new() { Margin = new Thickness(12, 8, 12, 0), BorderThickness = new Thickness(0), Background = Appearance.Surface }, Results = new();
    internal readonly ComboBox StageChoice = new() { MinWidth = 200, MaxWidth = 360, Margin = new Thickness(4) };
    internal readonly ComboBox DisplayChoice = Ui.Choice(CompositeSectionFields.Displays, CompositeSectionFields.Displays[0]);
    private readonly TextBlock status = Ui.Text(BridgeInputPresentation.StatusPreparing, 12), overview = Ui.Text("", 12), materialInfo = Ui.Text("", 12);
    private readonly TextBlock warnings = Ui.Text("", 12, color: Ui.Brush("#8B5916"));
    private readonly ContentControl stressTable = new(), propertiesTable = new(), classTable = new(), phaseTable = new(), geometryTable = new();
    private readonly StackPanel phaseForms = new();
    private readonly TextBox details = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0), Padding = new Thickness(12) };
    private readonly RecalculationScheduler recalculation = new();
    private readonly IModuleService calculationService;
    private Task scheduledCalculation = Task.CompletedTask;
    private readonly List<InputForm> inputForms = [], phaseInputForms = [];
    private JsonObject[] displayedPhases = [];
    private JsonObject? selectedPhase;
    private bool followLatestStage = true;
    private bool building = true, disposed;
    private BridgeGeometry? previewGeometry;
    private JsonObject? previewInput;
    private string geometryError = "";
    internal BridgeWorkspace(JsonObject data, IModuleService? calculationService = null)
    {
        this.calculationService = calculationService ?? ModuleServices.Get(BridgeEditor.Module);
        Data = data; BridgeEditor.ValidateShape(data); BridgeEditor.EnsureAccessoryDefaults(data); Background = Appearance.Surface;
        RevisionInspection.Allow(StageChoice); RevisionInspection.Allow(DisplayChoice);
        SetValue(InputForm.CommitOnFocusLossProperty, true);
        SetValue(NumericPresentation.EnabledProperty, true);
        BuildInputs();
        Ui.Tab(Results, "Sollecitazioni", BuildActionsTable());
        Ui.Tab(Results, "Fasi e proprietà", Scroll(Ui.Stack(Block("Omogeneizzazione e proprietà per fase", propertiesTable),
            Block("Azioni ed equilibrio", phaseTable))));
        Ui.Tab(Results, "Sezione efficace", Scroll(Ui.Stack(Block("Pannelli di classe 4", classTable),
            Group("Proprietà geometriche · lorda ed efficace", geometryTable, true), Group("Convergenza ed equilibrio", details))));
        Ui.Tab(Results, "Tensioni", Ui.Dock(stressTable, Ui.Text("La trave accumula anche G1, portato dal solo acciaio. Le armature accumulano le fasi in cui partecipano; il confronto va fatto per contributo e quota, tenendo conto di Es/Ea.", 11, color: Ui.Muted)));
        Ui.Tab(Results, "Verifiche", Scroll(Ui.Stack(
            Ui.Text("Esiti locali della sezione. Leggere anche i controlli da completare: i controlli disattivati o privi di dati restano da verificare; fatica e reazione d’appoggio usano inviluppi dedicati.", 11, color: Ui.Muted),
            verificationTable, Group("Taglio · parametri e resistenze", shearResults, true), Group("Pioli · flusso per fase e resistenze", studResults, true),
            torsionGroup = Group("Cassoncino · torsione, distorsione e diaframmi", torsionResults, true))));
        BuildLayout();
        StageChoice.SelectionChanged += (_, _) =>
        {
            if (Busy) return;
            if (StageChoice.SelectedIndex is var i && i >= 0 && i < displayedPhases.Length) selectedPhase = displayedPhases[i];
            followLatestStage = StageChoice.SelectedIndex == displayedPhases.Length - 1;
            ShowResults(); ViewChanged();
        };
        DisplayChoice.SelectionChanged += (_, _) => { RefreshDrawing(); ViewChanged(); };
        Results.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, Results)) ViewChanged(); };
        building = false; RefreshPreview(); QueueCalculation();
    }
    private static ScrollViewer Scroll(UIElement body) => new ChainedScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private static UIElement Block(string title, UIElement body, string? subtitle = null)
    {
        var head = Ui.Text(title, 15, true); head.Margin = new Thickness(0, 12, 0, 8);
        var s = Ui.Stack(head); if (subtitle is not null) { var text = Ui.Text(subtitle, 11, color: Ui.Muted); text.Margin = new Thickness(0, 0, 0, 7); s.Children.Add(text); } s.Children.Add(body); return s;
    }
    private InputForm Form(JsonObject data, IEnumerable<Field> fields, Action<string>? handler = null)
    {
        var form = new InputForm(data, fields, key => { handler?.Invoke(key); Changed(); }, true, true);
        foreach (var (key, tip) in CompositeSectionFields.EditorToolTips) if (form.Editors.TryGetValue(key, out var editor)) editor.ToolTip = tip;
        inputForms.Add(form); return form;
    }
    /// <summary>Shows the fields of <paramref name="form"/> by the data (BridgeInputPresentation.FieldShown).</summary>
    private void ShowFields(InputForm? form, string name)
    {
        if (form is null) return;
        foreach (string key in form.Editors.Keys) form.ShowField(key, BridgeInputPresentation.FieldShown(name, key, Data));
    }
    private void BuildInputs()
    {
        var norm = Form(Data, CompositeSectionFields.Standard, key =>
            {
                if (key == "normativa")
                {
                    BridgeEditor.ApplyStandardDefaults(Data);
                    Dispatcher.BeginInvoke(BuildPhases);
                    Dispatcher.BeginInvoke(() => { foreach (var f in inputForms) foreach (string k in BridgeInputPresentation.StandardCoefficients) f.Set(k, Data.S(k), true); });
                }
            });
        var coefficients = Form(Data, CompositeSectionFields.Coefficients);
        InputForm? materials = null;
        materials = Form(Data, CompositeSectionFields.Materials, _ => ShowFields(materials, "materiali"));
        ShowFields(materials, "materiali");
        var materialBody = Ui.Stack(materials, Block("Proprietà adottate", materialInfo),
            Ui.Text("Se attivo, il valore assegnato sostituisce fy di catalogo per anima e tutte le piattabande.", 11, color: Ui.Muted));
        var slab = Form(Data, CompositeSectionFields.Slab);
        var predalle = BuildPredalleInputs();
        var steel = Form(Data, CompositeSectionFields.Steel, _ => RefreshSteelHeight());
        steelHeightInfo = Ui.Text("", 11, color: Ui.Muted);
        InputForm? plate = null, sectionType = null;
        TextBlock? plateNote = null;
        // the type of section: the offset of the webs for the inclined web and the box, the spacing of the webs and no second plate for the box
        void ShowSectionFields()
        {
            ShowFields(sectionType, "sezione");
            bool h = BridgeInputPresentation.IsH(Data);
            if (plate is not null) plate.Visibility = h ? Visibility.Visible : Visibility.Collapsed;
            if (plateNote is not null) plateNote.Visibility = h ? Visibility.Visible : Visibility.Collapsed;
            if (boxInputs is not null) boxInputs.Visibility = BridgeInputPresentation.IsBox(Data) ? Visibility.Visible : Visibility.Collapsed;
        }
        sectionType = Form(Data, CompositeSectionFields.SectionType, key => { ShowSectionFields(); RefreshSteelHeight(); if (key == "sezione") Dispatcher.BeginInvoke(BuildPhases); });
        plate = Form(Data, CompositeSectionFields.Plate, _ => { ShowFields(plate, "piastra"); RefreshSteelHeight(); });
        ShowFields(plate, "piastra");
        plateNote = Ui.Text(BridgeInputPresentation.PlateNote, 11, color: Ui.Muted);
        ShowSectionFields();
        var geometry = Ui.Stack(Block("Soletta", slab, "b_eff già comprensiva della larghezza collaborante adottata."), Block("Predalle · geometria", predalle), Block("Tipo di sezione", sectionType,
            "Anima inclinata: scostamento orizzontale del piede dell'anima rispetto alla sommità (positivo verso destra). Cassoncino: due anime simmetriche, larghezza superiore di " +
            "ciascuna piattabanda, larghezza inferiore dell'intero fondo; lo scostamento è il rientro di ciascuna anima al piede (interasse al piede = interasse − 2 × scostamento). " +
            "Altezza totale H misurata fuori tutto. Il programma ricava h anima = H − t sup − t inf; con la seconda piattabanda inferiore attiva sottrae anche il relativo spessore. Per l'anima inclinata h anima è la distanza verticale libera. Lo spessore anima è normale alla lamiera. Flessione retta: N–Mx con anima verticale equivalente tw/cos α, verifiche locali sulle lamiere reali."),
            Block("Carpenteria saldata", Ui.Stack(steel, steelHeightInfo)), plate, plateNote);
        var reinforcement = new StackPanel();
        foreach (string side in new[] { "top", "bottom" })
        {
            InputForm? bars = null;
            string name = side == "top" ? "armatura-sup" : "armatura-inf";
            bars = Form(Data, CompositeSectionFields.Bars(side), _ => ShowFields(bars, name));
            ShowFields(bars, name);
            reinforcement.Children.Add(Block(side == "top" ? "Armatura superiore" : "Armatura inferiore", bars));
        }
        reinforcement.Children.Add(Ui.Text("Le file possono essere entrambe assenti. Le barre sono distribuite e centrate secondo il passo; la distanza inserita è all’asse, non il copriferro netto.", 11, color: Ui.Muted));
        var accessories = BuildAccessories();
        ShowSectionFields();
        pageInputs.Add(Scroll(Ui.Stack(norm,
            Ui.Text("Dati comuni a tutte le situazioni. Coefficienti modificabili per l’Appendice Nazionale applicabile.", 11, color: Ui.Muted),
            Group("Coefficienti da normativa", coefficients), Group("Materiali", materialBody), Group("Geometria", geometry, true), Group("Armature", reinforcement), accessories)));
        pageInputs.Add(Scroll(Ui.Stack(
            BuildCalculationOptions(),
            Form(Data, CompositeSectionFields.Limits),
            Ui.Text("Con la classe 4 attiva, una parte con instabilità esclusa resta interamente efficace (ρ = 1): giustificarlo, ad esempio con la classe 1–3 o con il vincolo della soletta connessa.", 11, color: Ui.Muted),
            Ui.Text("y = 0 all’interfaccia; positivo verso l’alto. La quota comune si usa solo per le fasi che la selezionano; ogni fase può applicare N al proprio baricentro.", 11, color: Ui.Muted),
            Notice("N > 0 trazione; Mx > 0 comprime la parte superiore. Inserire i soli incrementi di carico già combinati: le fasi precedenti sono sommate automaticamente."),
            phaseForms,
            Ui.Bar(Ui.Button("+ Aggiungi fase", () => AddPhase(false)), Ui.Button("+ Ritiro", () => AddPhase(true))))));
        BuildPhases();
    }
    private void BuildPhases()
    {
        foreach (var form in phaseInputForms) inputForms.Remove(form);
        phaseInputForms.Clear(); int staticForms = inputForms.Count;
        homogenizationForms.Clear();
        phaseForms.Children.Clear();
        int index = 0;
        foreach (JsonObject p in Data.Array("fasi").OfType<JsonObject>().ToArray())
        {
            BridgeSheetPresentation.PreparePhase(p);
            int i = index++;
            var form = Form(p, CompositeSectionFields.Phase, key =>
                { if (key == "tipo") { BridgeSheetPresentation.PrepareKind(p); Dispatcher.BeginInvoke(BuildPhases); } });
            foreach (string key in form.Editors.Keys) form.ShowField(key, BridgeInputPresentation.PhaseFieldShown(key, Data, p));
            form.Editors["T"].ToolTip = CompositeSectionFields.TorqueToolTip;
            bool shrinkage = p.S("tipo") == BridgeEditor.ShrinkageKind;
            var commands = Ui.Bar(Ui.Button("↑", () => MovePhase(i, -1)), Ui.Button("↓", () => MovePhase(i, 1)), Ui.Button("Elimina", () => { if (Data.Array("fasi").Count <= 1) return; ActionsTable.Commit(); Data.Array("fasi").Remove(p); BuildPhases(); Changed(); }));
            var phaseBody = Ui.Stack(form);
            if (shrinkage) phaseBody.Children.Add(Ui.Text(BridgeInputPresentation.ShrinkageNote, 11, color: Ui.Muted));
            if (PhaseHomogenizationNeeded(p.S("tipo")))
            {
                var h = Form(p, CompositeSectionFields.Homogenization, key => BridgeSheetPresentation.HomogenizationEdited(p, key));
                var info = Ui.Text("", 11, color: Ui.Muted);
                homogenizationForms.Add((p, h, info));
                phaseBody.Children.Add(Block("Omogeneizzazione", Ui.Stack(h, info,
                    Ui.Text(BridgeInputPresentation.HomogenizationNote, 11, color: Ui.Muted))));
            }
            else phaseBody.Children.Add(Ui.Text(BridgeInputPresentation.NoHomogenizationNote(p), 11, color: Ui.Muted));
            phaseBody.Children.Add(commands);
            phaseForms.Children.Add(Group(BridgeInputPresentation.PhaseHeader(i, p), phaseBody, true));
        }
        phaseInputForms.AddRange(inputForms.Skip(staticForms));
        SynchronizeHomogenization();
        UpdateCommonReferenceField();
        RefreshActionsTable(); RefreshSectionProperties();
        RefreshCalculationOptions();
    }
    private void MovePhase(int i, int delta)
    {
        ActionsTable.Commit();
        var list = Data.Array("fasi"); int target = i + delta; if (target < 0 || target >= list.Count) return;
        var phase = list[i]!; list.RemoveAt(i); list.Insert(target, phase); BuildPhases(); Changed();
    }
    internal void Commit() { ActionsTable.Commit(); slabPropertyForm?.Commit(); foreach (var f in inputForms.ToArray()) f.Commit(); ResponseForm?.Commit(); SaveView(); }
    private void Changed()
    {
        if (building || disposed) return;
        SynchronizeHomogenization();
        UpdateCommonReferenceField();
        RefreshActionsTable();
        recalculation.Invalidate(); Calculation = null;
        InvalidateResponse();
        if (DisplayedCalculation is null) ClearResults();
        RefreshPreview(); UpdateResultNotice();
        status.Text = BridgeInputPresentation.StatusPending; QueueCalculation(); Modified?.Invoke();
    }
    private void RefreshPreview()
    {
        RefreshSteelHeight();
        RefreshSlabLayout();
        try
        {
            var preview = Anthea.Application.Presentation.BridgePreview.Read(Data);
            previewGeometry = preview.Geometry; previewInput = preview.Input;
            geometryError = preview.GeometryError; materialInfo.Text = preview.MaterialText;
        }
        catch (Exception ex) { geometryError = ex.Message; materialInfo.Text = ex.Message; }
        RefreshSectionProperties();
        RefreshDetailSketch();
        RefreshDrawing();
    }
    private void QueueCalculation() => scheduledCalculation = CalculateScheduledAsync(BridgeInputPresentation.RecalculationDelay, false);
    internal Task CalculateAsync(bool commit = true) => scheduledCalculation = CalculateScheduledAsync(TimeSpan.Zero, commit);
    private async Task CalculateScheduledAsync(TimeSpan delay, bool commit)
    {
        if (disposed) return;
        if (commit) Commit();
        var snapshot = (JsonObject)Data.DeepClone();
        var activePhases = Data.Array("fasi").OfType<JsonObject>().Where(p => p.B("attiva")).ToArray();
        var completed = await recalculation.ScheduleAsync(async token =>
        {
            Busy = true; progress.Visibility = Visibility.Visible;
            status.Text = BridgeInputPresentation.StatusRunning;
            try { return BridgeResultView.Read(await Task.Run(() => calculationService.Calculate(snapshot, token), token)); }
            finally { Busy = false; if (!disposed) progress.Visibility = Visibility.Collapsed; }
        }, delay);
        completed.TryPublish(current =>
        {
            // Suppress selection callbacks while restoring the selected construction phase.
            Busy = true;
            try
            {
                if (current.Error is not null) throw current.Error;
                var computed = current.Value!;
                int previous = selectedPhase is null ? StageChoice.SelectedIndex : Array.IndexOf(activePhases, selectedPhase);
                Calculation = computed; DisplayedCalculation = computed; displayedPhases = activePhases;
                StageChoice.ItemsSource = BridgeResultPresentation.Situations(computed);
                if (previous < 0 && selectedPhase is null) previous = (int)viewSettings.D("fase", -1);
                StageChoice.SelectedIndex = BridgeSheetPresentation.PublishedSituation(followLatestStage, previous, computed.Stages.Count);
                selectedPhase = activePhases[StageChoice.SelectedIndex];
                ShowResults(); UpdateResultNotice(); status.Text = BridgeInputPresentation.StatusUpdated(computed.Stages.Count);
            }
            catch (Exception ex) { Calculation = null; if (DisplayedCalculation is null) ClearResults(); UpdateResultNotice(); status.Text = BridgeInputPresentation.StatusInvalid(ex.Message); }
            finally { Busy = false; }
        });
    }
    private void ClearResults()
    {
        Drawing.Stage = null; Drawing.InvalidateVisual(); overview.Text = BridgeResultPresentation.OverviewToUpdate; warnings.Text = "";
        foreach (var host in new[] { stressTable, propertiesTable, classTable, phaseTable, geometryTable, verificationTable, shearResults, studResults, torsionResults }) host.Content = Render(BridgeResultPresentation.Empty);
        details.Text = "";
        summaryCards.Text = BridgeResultPresentation.NoResult;
    }
    private void ShowResults()
    {
        if (DisplayedCalculation is not { } result || StageChoice.SelectedIndex < 0 || StageChoice.SelectedIndex >= result.Stages.Count) return;
        RefreshDrawing();
        var shown = BridgeResultPresentation.Show(result, StageChoice.SelectedIndex);
        overview.Text = shown.Overview;
        summaryCards.Start();
        foreach (var summary in shown.Summaries) summaryCards.AddSummary(summary);
        if (shown.SummaryText is { } text) summaryCards.Text = text;
        shearResults.Content = Render(shown.Shear); studResults.Content = Render(shown.Studs);
        torsionGroup.Visibility = shown.TorsionShown ? Visibility.Visible : Visibility.Collapsed; torsionResults.Content = Render(shown.Torsion);
        warnings.Text = shown.Warnings;
        stressTable.Content = Render(shown.Stress);
        verificationTable.Content = Render(shown.Verification);
        propertiesTable.Content = Render(shown.Properties);
        classTable.Content = Render(shown.Class4);
        phaseTable.Content = Render(shown.Phases);
        geometryTable.Content = Render(shown.Geometry);
        details.Text = shown.Details;
    }
    /// <summary>A panel of results drawn with the controls of the view: one element, or a stack, in a scroll viewer when the panel asks for it.</summary>
    private static UIElement Render(BridgePanel panel)
    {
        var elements = panel.Blocks.Select(Render).ToArray();
        UIElement body = elements.Length == 1 && !panel.Scroll ? elements[0] : Ui.Stack(elements);
        return panel.Scroll ? Scroll(body) : body;
    }
    private static UIElement Render(BridgeBlock block) => block.Kind switch
    {
        BridgeBlockKind.Table => ResultTable([.. block.Columns!], block.Rows!),
        BridgeBlockKind.Group => Group(block.Text, Render(new BridgePanel(block.Children!))),
        _ => block.Style switch
        {
            BridgeTextStyle.Muted => Ui.Text(block.Text, 12, color: Ui.Muted),
            BridgeTextStyle.Note => Ui.Text(block.Text, 11, color: Ui.Muted),
            BridgeTextStyle.Plain => Ui.Text(block.Text),
            _ => Ui.Text(block.Text, 12)
        }
    };
    private void UpdateCommonReferenceField()
    {
        bool used = BridgeInputPresentation.CommonReferenceUsed(Data);
        foreach (var form in inputForms.Where(f => f.Editors.ContainsKey("y_ref"))) form.Enable("y_ref", used, dim: true);
    }
    private void RefreshDrawing()
    {
        bool geometryPage = currentPage == 0 && detachedWindow is null;
        var result = DisplayedCalculation;
        var stage = result is not null && StageChoice.SelectedIndex >= 0 && StageChoice.SelectedIndex < result.Stages.Count ? result.Stages[StageChoice.SelectedIndex] : null;
        Drawing.Geometry = geometryPage || stage is null ? previewGeometry : result!.Geometry;
        Drawing.Input = geometryPage || stage is null ? previewInput : result!.Input;
        Drawing.Stage = geometryPage ? null : stage;
        NeutralAxisToggle.Visibility = geometryPage ? Visibility.Collapsed : Visibility.Visible;
        NeutralAxisToggle.ToolTip = stage is null ? BridgeResultPresentation.NeutralAxisToolTip : NeutralAxisPresentation.Bridge(stage).Explanation;
        Drawing.Mode = geometryPage ? 2 : DisplayChoice.SelectedIndex;
        GeometryLabels.Visibility = RebarLabels.Visibility = Drawing.Mode == 2 ? Visibility.Visible : Visibility.Collapsed;
        Drawing.IsStale = !geometryPage && ResultsAreStale;
        Drawing.LoadY = null;
        Drawing.LoadPoints = !geometryPage && stage is not null ? BridgeResultPresentation.LoadPoints(stage) : [];
        RefreshStressScale();
        Drawing.InvalidateVisual();
        UpdateResultNotice();
    }
    private void UpdateResultNotice()
    {
        bool geometryPage = currentPage == 0 && detachedWindow is null;
        resultNotice.Text = BridgeInputPresentation.ResultNotice(geometryPage, geometryError, ResultsAreStale);
        resultNotice.Visibility = resultNotice.Text == "" ? Visibility.Collapsed : Visibility.Visible;
        Drawing.IsStale = !geometryPage && ResultsAreStale;
    }
    private static DataGrid ResultTable(string[] headers, IEnumerable<string[]> rows)
    {
        var table = Ui.Table(headers, rows);
        foreach (var column in table.Columns)
        {
            column.Width = DataGridLength.Auto; column.MinWidth = 72; column.MaxWidth = 300;
            if (column is DataGridTextColumn textColumn)
            {
                var style = new Style(typeof(TextBlock), textColumn.ElementStyle);
                style.Setters.Add(new Setter(MarginProperty, new Thickness(6, 3, 6, 3)));
                style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                style.Setters.Add(new Setter(ToolTipProperty, new System.Windows.Data.Binding("Text") { RelativeSource = System.Windows.Data.RelativeSource.Self }));
                textColumn.ElementStyle = style;
            }
        }
        return table;
    }
    internal static string F(double x) => Anthea.Application.Presentation.BridgePropertyPresentation.Number(x);
    private void ExportCsv()
    {
        if (Calculation is null || Busy) { status.Text = BridgeInputPresentation.WaitBeforeExport; return; }
        var dialog = new SaveFileDialog { Filter = "Tabella CSV|*.csv", FileName = BridgeResultPresentation.CsvFileName };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        Archivio.ScriviAtomico(dialog.FileName, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BridgeResultPresentation.Csv(Calculation))).ToArray());
    }
    public void Dispose() { disposed = true; recalculation.Dispose(); responseRecalculation.Dispose(); responseCancellation?.Cancel(); detachedWindow?.Close(); }
}
