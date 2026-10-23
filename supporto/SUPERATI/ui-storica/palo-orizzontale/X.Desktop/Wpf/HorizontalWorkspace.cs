using Anthea.Application.Concrete;
using Anthea.Application.Materials;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Application.Scheduling;
using Anthea.Presentation.Modules.HorizontalPile;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

/// <summary>Adaptive geotechnical workspace with automatic, revision-safe calculations.</summary>
internal sealed partial class HorizontalWorkspace : UserControl, IDisposable
{
    internal JsonObject Data { get; }
    private bool MicroHorizontal => Data.S("tipo_sezione") == "CHS";
    private readonly ChsDrawing chsDrawing = new() { Width = 400, Height = 400 };
    internal JsonObject? Result { get; private set; }
    private readonly ElasticPileWorkspace elastic;
    private readonly TabControl analyses = new();
    internal JsonObject? ActiveResult => analyses.SelectedIndex == 1 ? elastic.Result : Result;
    internal Task CalculateActiveAsync() => analyses.SelectedIndex == 1 ? elastic.CalculateAsync() : CalculateAsync();
    internal bool Busy { get; private set; }
    internal event Action? Modified;
    private bool disposed, building = true;
    private readonly RecalculationScheduler recalculation = new();
    private Task scheduledCalculation = Task.CompletedTask;
    private readonly List<JsonGrid> grids = [];
    private readonly List<HorizontalSoilEditor> soilEditors = [];
    private readonly InputForm ejForm;
    private readonly TabControl surveys = new();
    private readonly Grid sectionLayout = new();
    private readonly Viewbox sectionPreview = new() { Stretch = Stretch.Uniform, Margin = new Thickness(8, 0, 0, 0) };
    private readonly StratigraphyDrawing profile = new() { ShowAll = false, HorizontalForces = true };
    private readonly SectionDrawing sectionDrawing = new();
    private readonly TextBlock summary = Ui.Text(HorizontalPilePresentation.SummaryInitial, 14);
    private readonly TextBlock momentValue = Ui.Text(HorizontalPilePresentation.MomentInitial, 12, true);
    private readonly TextBlock status = Ui.Text(HorizontalPilePresentation.StatusToVerify, 12, color: Ui.Muted);
    private readonly TextBlock warnings = Ui.Text("", 12, color: Ui.Muted);
    private readonly Canvas layout = new();
    private readonly Button details, csv;
    private readonly InputForm general, model, factors, efficiency, moment, sectionFields;
    private readonly Expander advanced = new() { Header = "Opzioni avanzate", IsExpanded = false };
    private readonly TextBlock modelLabel = Ui.Text(HorizontalPilePresentation.ModelInitial, 13, true);
    private readonly IModuleService? capacityService;

    internal HorizontalWorkspace(JsonObject data) : this(data, null) { }

    /// <param name="capacityService">Calculation service of the capacity; null (production) uses the module service of the type of section.</param>
    /// <param name="elasticAnalysis">Creates the calculation of the elastic response; null (production) uses ElasticPileAnalysisSession.</param>
    internal HorizontalWorkspace(JsonObject data, IModuleService? capacityService, Func<IElasticPileAnalysis>? elasticAnalysis = null)
    {
        this.capacityService = capacityService;
        Anthea.Application.Presentation.HorizontalSheetPresentation.PrepareGeneral(data);
        Data = data; profile.Data = data; SetValue(InputForm.CommitOnFocusLossProperty, false);
        var g = data["generali"]!.AsObject();
        details = Ui.Button("Diagrammi e dettagli", ShowResults, inspection: true); details.IsEnabled = false;
        csv = Ui.Button("Esporta CSV", ExportCsv, inspection: true); csv.IsEnabled = false;
        general = new(g, HorizontalPileFields.General(MicroHorizontal), _ => Changed(), compact: true, symbolColumns: true);
        Group(general, HorizontalPileFields.GeneralGroups());
        model = new(g, HorizontalPileFields.Model(), _ => Changed(), compact: true, symbolColumns: true);
        ((Grid)model.Content).ColumnDefinitions[2].Width = new GridLength(150);
        advanced.Content = model; advanced.Expanded += (_, _) => LayoutCards(); advanced.Collapsed += (_, _) => LayoutCards();
        var coefficients = data["verifica"]!.AsObject();
        Anthea.Application.Presentation.HorizontalSheetPresentation.PrepareFactors(coefficients);
        factors = new(coefficients, HorizontalPileFields.Factors(), _ => Changed(), compact: true, symbolColumns: true);
        efficiency = new(coefficients, HorizontalPileFields.Efficiency(), _ => Changed(), compact: true, symbolColumns: true);
        AddCard("Dati generali", Ui.Dock(general, bottom: advanced));
        AddCard("Efficienza", Ui.Dock(new ChainedScrollViewer { Content = efficiency, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, bottom: Ui.Text(HorizontalPileFields.EfficiencyNote, 12, color: Ui.Muted)));
        AddCard("Coefficienti normativa", Ui.Dock(factors, bottom: Ui.Text(HorizontalPileFields.FactorsNote, 12, color: Ui.Muted)));
        modelLabel.Margin = new Thickness(0, 0, 0, 8);
        AddCard("Verifica", Ui.Dock(new ChainedScrollViewer { Content = Ui.Stack(modelLabel, verification, summary, CalculationDescription()), VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, bottom: Ui.Bar(details, csv)));
        RebuildSurveys(); surveys.SelectionChanged += (_, e) => { if (e.Source == surveys) { profile.SelectedIndex = surveys.SelectedIndex; profile.InvalidateVisual(); } };
        var surveyButtons = Ui.Bar(Ui.Button("+ Stratigrafia", () => { Commit(); Data.Array("stratigrafie").Add(new JsonArray(HorizontalPilePresentation.NewLayer())); RebuildSurveys(Data.Array("stratigrafie").Count - 1); Changed(); }),
            Ui.Button("− Stratigrafia", () => { if (surveys.SelectedIndex >= 0) DeleteSurvey(Data.Array("stratigrafie")[surveys.SelectedIndex]!.AsArray()); }),
            Ui.Button("Copia in…", () => ChooseSurveyCopy(true)), Ui.Button("Copia da…", () => ChooseSurveyCopy(false)));
        moment = new(g, HorizontalPileFields.Moment(MicroHorizontal), _ => Changed(), compact: true, symbolColumns: true);
        var sectionData = data["sezione"]!.AsObject();
        if (MicroHorizontal) sectionFields = CreateChsForm(sectionData);
        else {
        Anthea.Application.Presentation.HorizontalSheetPresentation.PrepareSection(data);
        sectionFields = new InputForm(sectionData, HorizontalPileFields.ConcreteSection(sectionData), SectionMaterialChanged, compact: true, symbolColumns: true);
        Group(sectionFields, HorizontalPileFields.ConcreteSectionGroups());
        foreach (string key in HorizontalPileFields.ConcreteSectionHidden()) sectionFields.ShowField(key, false);
        foreach (var (key, tip) in HorizontalPileFields.ConcreteSectionToolTips()) sectionFields.Editors[key].ToolTip = tip;
        ShowInnerRing(false);
        }
        moment.MaxWidth = sectionFields.MaxWidth = 650;
        moment.HorizontalAlignment = sectionFields.HorizontalAlignment = HorizontalAlignment.Left;
        sectionLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        sectionLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sectionDrawing.Width = 400; sectionDrawing.Height = 400;
        sectionPreview.Child = MicroHorizontal ? chsDrawing : sectionDrawing;
        Grid.SetColumn(sectionPreview, 1);
        var materialScroll=new ChainedScrollViewer{Content=sectionFields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var sectionAndResults=Ui.Dock(sectionPreview,bottom:momentValue);Grid.SetColumn(sectionAndResults,1);
        sectionLayout.Children.Add(materialScroll); sectionLayout.Children.Add(sectionAndResults);
        ejForm = new InputForm(sectionData, HorizontalPileFields.Ej(), _ => Changed(), compact:true);
        var sectionPane = Ui.Dock(sectionLayout, bottom:new Expander{Header="Opzioni avanzate · resistenza manuale ed EJ",Content=Ui.Stack(moment,ejForm)});
        AlignSurveyToolbar(surveyButtons);
        AddCard("Stratigrafia", surveys, true);
        AddCard("Profilo stratigrafico", profile, true);
        AddCard(HorizontalPileFields.SectionTitle(MicroHorizontal), sectionPane, true);
        scroll.Content = layout;
        var warningPane = new Expander { Header = "Ipotesi e limiti del calcolo", Content = new ChainedScrollViewer { Content = warnings, MaxHeight = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        var footer = Ui.Stack(Ui.Bar(status, Ui.Button("Modello e dati comuni…", () => CalculationHelpView.Show(this, true), inspection: true)), warningPane); footer.Margin = new Thickness(16, 3, 16, 8);
        elastic = new ElasticPileWorkspace(data, () => { analyses.SelectedIndex=0; expanded=4; LayoutCards(); }, RefreshAfterMigration, elasticAnalysis?.Invoke()); elastic.Modified += () => Modified?.Invoke();
        Ui.Tab(analyses, "Dati comuni e capacità · Broms / stratificato", Ui.Dock(scroll, bottom: footer));
        Ui.Tab(analyses, "Risposta elastica · trave su molle", elastic);
        analyses.SelectedIndex = data.S("vista_orizzontale") == "elastico" ? 1 : 0;
        analyses.SelectionChanged += (_, e) => { if(e.Source != analyses || building) return; Commit(); data["vista_orizzontale"] = analyses.SelectedIndex == 1 ? "elastico" : "capacita"; if(analyses.SelectedIndex == 1) recalculation.Invalidate(); else { LayoutCards(); if(Result is null) QueueCalculation(); } Modified?.Invoke(); };
        var engineReference = Ui.Text("Motore di calcolo: GPC Engine", 12, true, Ui.Muted);
        engineReference.Margin = new Thickness(16, 8, 16, 0); engineReference.HorizontalAlignment = HorizontalAlignment.Right;
        Content = Ui.Dock(analyses, top: engineReference);
        scroll.SizeChanged += (_, _) => LayoutCards();
        scroll.ScrollChanged += (_, e) => { if (e.Source == scroll && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) LayoutCards(); };
        building = false; Preview(); UpdateVerification(); LayoutCards(); QueueCalculation();
    }
    private void RebuildSurveys(int selected = 0)
    {
        surveys.Items.Clear(); grids.Clear(); soilEditors.Clear(); int index = 0;
        foreach (var node in Data.Array("stratigrafie"))
        {
            var rows = node!.AsArray();
            Field[] fields = HorizontalPileFields.Layers();
            var grid = new JsonGrid(fields); grids.Add(grid);
            JsonRow Bind(JsonObject data)
            {
                var display = (JsonObject)data.DeepClone();
                return new(display, key => { data[key] = display[key]?.DeepClone();Changed();grid.Rows.FirstOrDefault(r=>ReferenceEquals((r.Context as SoilRowContext)?.Layer,data))?.Refresh(); }){Context=new SoilRowContext(data,Changed)};
            }
            foreach (var row in rows.OfType<JsonObject>()) grid.Rows.Add(Bind(row));
            void AddLayer() { grid.Commit(); var row = HorizontalPilePresentation.NewLayer(); rows.Add(row); grid.Rows.Add(Bind(row)); Changed(); }
            void DeleteLayer(JsonRow row) { grid.Commit(); int i = grid.Rows.IndexOf(row); if (i < 0) return; rows.RemoveAt(i); grid.Rows.RemoveAt(i); Changed(); }
            var table = StratigraphyTable.Build(grid, fields, AddLayer, DeleteLayer, gammaFallback: false);
            grid.FrozenColumnCount=2;grid.EnableColumnVirtualization=false;
            grid.Columns.Insert(2,new DataGridTemplateColumn{Header=HorizontalSoilEditor.Header(),Width=HorizontalSoilEditor.Widths.Sum(),CellTemplate=new DataTemplate{VisualTree=new FrameworkElementFactory(typeof(HorizontalSoilEditor))}});
            var tab = Ui.Tab(surveys, $"{++index}", table);
            var remove = Ui.Button("[−]", () => DeleteSurvey(rows)); remove.FontSize = 11; remove.Padding = new Thickness(3, 0, 3, 0); remove.MinHeight = 20;
            remove.ToolTip = $"Elimina stratigrafia {index}";
            tab.Header = Ui.Bar(Ui.Text($"{index}", 12), remove);
        }
        surveys.SelectedIndex = Math.Clamp(selected, 0, surveys.Items.Count - 1); profile.SelectedIndex = surveys.SelectedIndex;
    }
    private void RefreshAfterMigration()
    {
        building=true;
        try{foreach(var form in new[]{general,sectionFields,ejForm}){var source=ReferenceEquals(form,general)?Data["generali"]!:Data["sezione"]!;foreach(string key in form.Editors.Keys.ToArray()){if(form.Editors[key] is CheckBox box)box.IsChecked=source.B(key);else form.Set(key,source.S(key),true);}}RebuildSurveys();}
        finally{building=false;}Changed();
    }
    private void Preview()
    {
        if (MicroHorizontal) UpdateChsPreview(); else UpdateSectionMaterialValues();
        var preview = HorizontalPreview.ReadModel(Data);
        modelLabel.Text = preview.Label; modelLabel.ToolTip = preview.Hint;
        // A legacy nonzero eccentricity stays editable so it can be corrected, never silently overwritten (FieldStates).
        foreach (var (key, enabled, dim) in HorizontalPilePresentation.FieldStates(Data))
            foreach (var form in new[] { general, moment, efficiency }) if (form.Editors.ContainsKey(key)) form.Enable(key, enabled, dim);
        sectionFields.IsEnabled = true; sectionFields.Opacity = 1;
        foreach (string key in HorizontalPileFields.EjValues()) ejForm?.ShowField(key, HorizontalPilePresentation.EjShown(Data["sezione"]!.AsObject()));
        foreach (var (key, value) in preview.Factors) factors.Set(key, value, true);
        efficiency.Set("__eta", preview.Efficiency, true);
        foreach (var grid in grids) for (int i = 0; i < grid.Rows.Count; i++)
        {
            grid.Rows[i].Output("__strato", HorizontalPilePresentation.LayerName(i));
            grid.Rows[i].Output("__color", StratigraphyDrawing.LayerColors[i % StratigraphyDrawing.LayerColors.Length]);
            var row = grid.Rows[i].Values;
            grid.Rows[i].Output("__kp", HorizontalPreview.PassivePressure(row));
        }
        profile.InvalidateVisual();
        if (!MicroHorizontal) try
        {
            var section = HorizontalPreview.ConcreteSection(Data);
            sectionDrawing.Outline = section.Geometry.Outline; sectionDrawing.Bars = section.Geometry.Bars;
            sectionDrawing.CircularLinkRadius = section.LinkRadius; sectionDrawing.LinkDiameter = section.LinkDiameter;
            sectionDrawing.ShowAxes = true; sectionDrawing.Caption = "Sezione principale · staffa verde";
        }
        catch (ArgumentException) { sectionDrawing.Outline = []; sectionDrawing.Bars = [];sectionDrawing.CircularLinkRadius=0; }
        sectionDrawing.InvalidateVisual();
    }
    private void Changed()
    {
        if (building || disposed) return;
        recalculation.Invalidate(); Result = null; summary.Text = HorizontalPilePresentation.SummaryChanged; momentValue.Text = HorizontalPilePresentation.MomentChanged;
        details.IsEnabled = csv.IsEnabled = false; warnings.Text = ""; elastic?.InvalidateShared(); Preview(); UpdateVerification(); QueueCalculation(); Modified?.Invoke();
    }
    internal void Commit() { general.Commit(); model.Commit(); factors.Commit(); efficiency.Commit(); moment.Commit(); sectionFields.Commit(); ejForm?.Commit(); foreach(var editor in soilEditors)editor.Commit(); foreach (var grid in grids) grid.Commit(); elastic?.Commit(); }
    private void QueueCalculation()
    {
        if (building || disposed || analyses.SelectedIndex == 1) return;
        status.Text = HorizontalPilePresentation.StatusPending;
        scheduledCalculation = CalculateScheduledAsync(TimeSpan.FromMilliseconds(450), false);
    }
    private void ShowMoment(HorizontalPileMoment shown)
    {
        momentValue.Text = shown.Text;
        if (shown.ToolTip is not null) momentValue.ToolTip = shown.ToolTip;
    }
    internal Task CalculateAsync(bool commitEdits = true) => scheduledCalculation = CalculateScheduledAsync(TimeSpan.Zero, commitEdits);
    private async Task CalculateScheduledAsync(TimeSpan delay, bool commitEdits)
    {
        if (disposed) return;
        if (commitEdits) Commit();
        var snapshot = (JsonObject)Data.DeepClone();
        var completed = await recalculation.ScheduleAsync(async token =>
        {
            Busy = true; status.Text = HorizontalPilePresentation.StatusRunning;
            try { return await Task.Run(() => HorizontalPileAnalysis.Calculate(snapshot, capacityService, token), token); }
            finally { Busy = false; }
        }, delay);
        completed.TryPublish(current =>
        {
            try
            {
                if (current.Error is not null) throw current.Error;
                var computed = current.Value!;
                var result = computed.Capacity;
                ShowMoment(HorizontalPilePresentation.CalculatedMoment(computed, snapshot, MicroHorizontal));
                Result = result.S("errore") == "" ? result : null;
                if (Result is null) { summary.Text = HorizontalPilePresentation.SummaryIncomplete(result.S("errore")); warnings.Text = result.S("errore"); status.Text = HorizontalPilePresentation.StatusIncomplete; details.IsEnabled = csv.IsEnabled = false; UpdateVerification(); return; }
                ShowMoment(HorizontalPilePresentation.ResultMoment(result, MicroHorizontal));
                summary.Text = HorizontalPilePresentation.Summary(result);
                UpdateVerification();
                warnings.Text = HorizontalPilePresentation.Warnings(result);
                status.Text = HorizontalPilePresentation.StatusCompleted; details.IsEnabled = csv.IsEnabled = true;
            }
            catch (Exception ex) { Result = null; summary.Text = HorizontalPilePresentation.SummaryFailed; momentValue.Text = HorizontalPilePresentation.MomentUnavailable; status.Text = ex.Message; details.IsEnabled = csv.IsEnabled = false; UpdateVerification(); }
        });
    }
    private TabControl ResultsTabs()
    {
        var tabs = new TabControl(); int index = 0;
        foreach (var survey in HorizontalPilePresentation.Results(Result!))
        {
            var panel = new TabControl();
            Ui.Tab(panel, "Terreno e tensioni", new ScrollViewer { Content = new HorizontalDiagrams(Result!, index, true) { MinWidth = 1000, MinHeight = 620 }, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Ui.Tab(panel, "Equilibrio del palo", new ScrollViewer { Content = new HorizontalDiagrams(Result!, index, false) { MinWidth = 1000, MinHeight = 620 }, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            foreach (var table in survey.Tables) Ui.Tab(panel, table.Title, CenteredTable(table.Columns.ToArray(), table.Rows));
            var info = Ui.Text(survey.Info, 12);
            info.Margin = new Thickness(12);
            Ui.Tab(tabs, $"Sondaggio {++index}", Ui.Dock(panel, info));
        }
        Ui.Tab(tabs, "Riferimenti", ReferencesPanel());
        Ui.Tab(tabs, "JSON", new TextBox { Text = Result!.ToJsonString(J.Options), IsReadOnly = true, AcceptsReturn = true, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return tabs;
    }
    private void ShowResults() { if (Result is not null) Ui.Dialog(this, "Palo orizzontale · risultati", ResultsTabs(), 1280, 900).ShowDialog(); }
    private void ExportCsv()
    {
        if (Result is null) return;
        var save = new SaveFileDialog { Filter = "Tabelle CSV|*.csv", FileName = "Palo_orizzontale.csv" };
        if (save.ShowDialog(Window.GetWindow(this)) != true) return;
        try { Archivio.ScriviAtomico(save.FileName, Encoding.UTF8.GetBytes(HorizontalPileEditor.Csv(Result))); }
        catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "Esportazione non riuscita"); }
    }
    public void Dispose() { disposed = true; recalculation.Dispose(); elastic.Dispose(); }
}
