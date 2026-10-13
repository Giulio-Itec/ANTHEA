using Anthea.Application.Walls;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Modules;
using Anthea.Application.Scheduling;
using Anthea.Reports;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal sealed partial class SheetEditor : UserControl, IDisposable
{
    internal string Module { get; }
    internal JsonObject Data { get; }
    private JsonObject? result;
    private bool busy;
    internal readonly Materiali.MaterialView? materials;
    internal readonly RebarMaterialView? rebarMaterial;
    private readonly ConcreteWorkspace? concrete;
    private readonly HorizontalWorkspace? horizontal;
    private readonly HorizontalPileGroupWorkspace? pileGroup;
    private readonly FoundationWorkspace? foundation;
    internal readonly BridgeWorkspace? bridge;
    internal readonly BridgeDesignWorkspace? bridgeDesign;
    internal readonly RetainingWallWorkspace? retainingWall;
    internal JsonObject? Result { get => foundation is not null ? foundation.Result : pileGroup is not null ? pileGroup.Result : retainingWall is not null ? retainingWall.Result : bridgeDesign is not null ? bridgeDesign.Result : bridge is not null ? bridge.Result : horizontal is not null ? horizontal.ActiveResult : concrete is null ? result : concrete.Result; private set => result = value; }
    internal bool HasResults => concrete is not null ? concrete.HasResults : Result is not null;
    internal bool Busy { get => retainingWall?.Busy ?? bridge?.Busy ?? horizontal?.Busy ?? concrete?.Busy ?? busy; private set => busy = value; }
    internal event Action? Modified;
    internal event Action<JsonObject, string>? ConcreteSectionRequested;
    internal event Action? ReportRequested;
    internal byte[] BuildShortReport(string title) => concrete?.BuildShortReport(title)
        ?? throw new InvalidOperationException("Il report short è disponibile per le sezioni in calcestruzzo armato.");
    internal event Action<string, JsonObject, string>? ModuleCopyRequested;
    internal event Action<JsonObject>? SoilReplacementRequested;
    private bool building = true, disposed;
    private int expanded = -1;
    private bool Micro => Module == "geo_micropalo_verticale";
    private bool Pile => Module == "geo_palo_verticale";
    private bool Geo => Pile || Micro;
    private bool Section => Module == "str_palo";
    private readonly Canvas canvas = new();
    private readonly ScrollViewer scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly List<Border> cards = [];
    private readonly Dictionary<int, Button> expandButtons = new();
    private readonly TextBlock status = Ui.Text("Dati da verificare · premere Calcola", color: Ui.Muted);
    private readonly TextBox warnings = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Appearance.Background("#FFFAEB"), MaxHeight = 62, Visibility = Visibility.Collapsed };
    private readonly Button calculate;
    private readonly RecalculationScheduler recalculation = new();
    private readonly ModuleCalculationSession calculationSession;
    private Task scheduledCalculation = Task.CompletedTask;
    private readonly Plot plot = new() { Capacity = true }, reference = new() { InvertY = false };
    private readonly StratigraphyDrawing stratigraphy = new();
    private readonly TabControl outputs = new(), sondages = new();
    private readonly ComboBox tableSelect = new();
    private readonly ContentControl tableHost = new();
    private readonly TextBox raw = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas") };
    private readonly ContentControl verification = new();
    private readonly ComboBox capacityView = new();
    private readonly WrapPanel curveChoices = new();
    private readonly TextBlock endValues = Ui.Text("Valori a L non disponibili", 11, true);
    private readonly TextBlock effLabel = Ui.Text("", 12, color: Ui.Muted);
    private readonly List<JsonGrid> layerGrids = [];
    private readonly List<(string Key, Serie Series)> allSeries = [];
    private readonly Dictionary<string, bool> visibility = new();
    private List<Tabella> tables = [];
    private InputForm generalForm = null!, normativeForm = null!, efficiencyForm = null!;

    internal SheetEditor(string module, JsonObject data, IModuleService? calculationService = null)
    {
        _ = ModuleCatalog.Get(module);
        calculationSession = new(calculationService ?? ModuleServices.Get(module));
        Module = module; Data = (JsonObject)data.DeepClone(); Background = Appearance.Surface;
        SetValue(WikiContextHelp.ModuleProperty, module);
        KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F1 && WikiCatalog.ForModule(Module) is { } guide)
            { e.Handled = true; WikiContextHelp.Open(this, guide.Id); }
        };
        calculate = Ui.Button("Calcola", async () => await CalculateAsync(), true, inspection: true); calculate.Width = 120; calculate.Visibility = Geo ? Visibility.Collapsed : Visibility.Visible;
        RevisionInspection.Allow(tableSelect); RevisionInspection.Allow(capacityView); RevisionInspection.Allow(curveChoices);
        if (module == Anthea.FoundationWorkspace.FoundationDocument.Module)
        {
            foundation = new FoundationWorkspace(Data); foundation.Modified += () => Modified?.Invoke();
            foundation.ReportRequested += () => ReportRequested?.Invoke(); foundation.SoilTransferRequested += OpenSoilTransfer; Content = foundation; building = false; return;
        }
        if (module == HorizontalPileGroup.Module) { pileGroup = new HorizontalPileGroupWorkspace(Data); pileGroup.Modified += () => Modified?.Invoke(); Content = pileGroup; building = false; return; }
        if (module == WallModel.Module)
        {
            retainingWall = new RetainingWallWorkspace(Data); retainingWall.Modified += () => Modified?.Invoke();
            retainingWall.SectionRequested += (section, name) => ConcreteSectionRequested?.Invoke(section, name);
            retainingWall.ReportRequested += () => ReportRequested?.Invoke();
            retainingWall.SoilTransferRequested += OpenSoilTransfer;
            Content = retainingWall; building = false; return;
        }
        if (module == BridgeDesignModel.Module)
        {
            bridgeDesign = new BridgeDesignWorkspace(Data); bridgeDesign.Modified += () => Modified?.Invoke(); Content = bridgeDesign; building = false; return;
        }
        if (module == BridgeEditor.Module)
        {
            bridge = new BridgeWorkspace(Data); bridge.Modified += () => Modified?.Invoke(); Content = bridge; building = false; return;
        }
        if (module is PaloOrizzontale.Module or MicropaloOrizzontale.Module)
        {
            horizontal = new HorizontalWorkspace(Data); horizontal.Modified += () => Modified?.Invoke(); Content = Ui.Dock(horizontal, Ui.Bar(Ui.Button("Invia / carica terreno…", OpenSoilTransfer))); building = false; return;
        }
        if (module == "mat_calcestruzzo")
        {
            materials = new Materiali.MaterialView((label, key) => WikiContextHelp.Label(label, key, "mat_calcestruzzo")); materials.RestoreState(Materiali.MaterialSheetInput.NormalizeState(Data));
            materials.Modified += () => Modified?.Invoke(); Content = materials; building = false; return;
        }
        if (module == RebarMaterial.Module)
        {
            rebarMaterial = new RebarMaterialView(Data); rebarMaterial.Modified += () => Modified?.Invoke(); Content = rebarMaterial; building = false; return;
        }
        if (Section)
        {
            concrete = new ConcreteWorkspace(Data); concrete.Modified += () => Modified?.Invoke(); Content = concrete; building = false; return;
        }
        var footer = Ui.Dock(status, bottom: null); DockPanel.SetDock(calculate, System.Windows.Controls.Dock.Right); footer.Children.Insert(0, calculate); footer.Margin = new Thickness(24, 4, 24, 4);
        var theory = Ui.Button("Modello e dati comuni…", () => CalculationHelpView.Show(this, true), inspection: true);
        DockPanel.SetDock(theory, System.Windows.Controls.Dock.Right); footer.Children.Insert(0, theory);
        if (Pile) { var soil = Ui.Button("Invia / carica terreno…", OpenSoilTransfer); DockPanel.SetDock(soil, System.Windows.Controls.Dock.Right); footer.Children.Insert(0, soil); }
        scroll.Content = canvas; Content = Ui.Dock(scroll, bottom: Ui.Stack(footer, warnings));
        tableSelect.SelectionChanged += (_, _) => ShowTable();
        Ui.Tab(outputs, "Tabelle e dettagli", Ui.Dock(tableHost, tableSelect)); Ui.Tab(outputs, "Risultati JSON", raw);
        BuildGeo();
        scroll.SizeChanged += (_, _) => LayoutCards();
        scroll.ScrollChanged += (_, e) => { if (e.Source == scroll && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) LayoutCards(); };
        building = false; Preview(); LayoutCards(); if (Geo) QueueCalculation();
    }
    internal void OpenSoilTransfer()
    {
        Commit(); var dialog = new SoilTransferDialog(Window.GetWindow(this), Module, Data);
        if (dialog.ShowDialog() != true || dialog.Output is null) return;
        if (dialog.Replace) SoilReplacementRequested?.Invoke(dialog.Output);
        else if (dialog.DestinationModule is string target) ModuleCopyRequested?.Invoke(target, dialog.Output, "Terreno da " + ModuleCatalog.Get(Module).Element);
    }
    private Action? releaseRevisionInspection;
    internal void InspectRevision() => releaseRevisionInspection = RevisionInspection.Protect(this);
    public void Dispose() { disposed = true; releaseRevisionInspection?.Invoke(); recalculation.Dispose(); calculationSession.Clear(); concrete?.Dispose(); horizontal?.Dispose(); pileGroup?.Dispose(); bridge?.Dispose(); retainingWall?.Dispose(); bridgeDesign?.Dispose(); }
    internal void Commit() { if (foundation is not null) foundation.Commit(); else if (pileGroup is not null) pileGroup.Commit(); else if (retainingWall is not null) retainingWall.Commit(); else if (materials is not null) { var state = materials.CaptureState(); Data.Clear(); foreach (var pair in state) Data[pair.Key] = pair.Value?.DeepClone(); } else if (bridge is not null) bridge.Commit(); else if (rebarMaterial is not null) rebarMaterial.Commit(); else if (horizontal is not null) horizontal.Commit(); else if (concrete is not null) concrete.Commit(); else foreach (var grid in layerGrids) grid.Commit(); }
    private void AddCard(string title, UIElement content, bool expandable = false, UIElement? action = null)
    {
        int index = cards.Count; var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8), MinHeight = 28 };
        if (expandable)
        {
            var b = Ui.Button("Estendi", () => { expanded = expanded == index ? -1 : index; LayoutCards(); }, inspection: true); b.FontSize = 11; b.Padding = new Thickness(5, 2, 5, 2); DockPanel.SetDock(b, System.Windows.Controls.Dock.Right); header.Children.Add(b); expandButtons[index] = b;
        }
        if (action is not null) { DockPanel.SetDock(action, System.Windows.Controls.Dock.Right); header.Children.Add(action); }
        header.Children.Add(Ui.Text(title, Geo && index == 3 ? 18 : 16, true)); var card = Ui.Paper(Ui.Dock(content, header), Section ? 8 : 14); card.ClipToBounds = true; cards.Add(card); canvas.Children.Add(card);
    }
    private void LayoutCards()
    {
        double availableWidth = double.IsFinite(scroll.ViewportWidth) && scroll.ViewportWidth > 0 ? scroll.ViewportWidth : ActualWidth;
        double availableHeight = double.IsFinite(scroll.ViewportHeight) && scroll.ViewportHeight > 0 ? scroll.ViewportHeight : ActualHeight;
        double w = Math.Max(320, availableWidth - 4), h = Math.Max(240, availableHeight - 4);
        double margin = w < 1000 ? 12 : 24, gap = Geo ? 12 : 20;
        var generalSize = Geo ? generalForm.UnwrappedSize() : default;
        double generalWidth = Math.Ceiling(generalSize.Width) + 32;
        double efficiencyWidth = Geo ? Math.Ceiling(efficiencyForm.UnwrappedSize().Width) + 32 : 0;
        double normativeWidth = Geo ? Math.Ceiling(normativeForm.UnwrappedSize().Width) + 32 : 0;
        bool compactLayout = w < Math.Max(1450, Geo ? generalWidth + efficiencyWidth + normativeWidth + 378 + 2 * margin + 3 * gap : 0);
        double pileTop = Geo ? Math.Ceiling(Math.Max(generalSize.Height,
            Math.Max(normativeForm.UnwrappedSize().Height, efficiencyForm.UnwrappedSize().Height + 40))) + 68 : 460;
        foreach (var (index, b) in expandButtons) b.Content = expanded == index ? "Riduci" : "Estendi";
        void Place(int i, double x, double y, double width, double height)
        { var c = cards[i]; Canvas.SetLeft(c, x); Canvas.SetTop(c, y); c.Width = Math.Max(100, width); c.Height = Math.Max(100, height); }
        if (!Section && (expanded == 6 || Geo && expanded == 4 || compactLayout && expanded >= 0))
        {
            for (int i = 0; i < cards.Count; i++) cards[i].Visibility = i == expanded ? Visibility.Visible : Visibility.Collapsed;
            Place(expanded, 16, 12, w - 32, h - 24); canvas.Width = w; canvas.Height = h; UpdateProfile(); return;
        }
        foreach (var card in cards) card.Visibility = Visibility.Visible;
        if (!Section && compactLayout)
        {
            int columns = w < 1000 ? 1 : 2;
            double cardWidth = (w - 2 * margin - (columns - 1) * gap) / columns, y = 14;
            for (int i = 0; i < 4; i += columns)
            {
                double rowHeight = pileTop;
                for (int col = 0; col < columns; col++) Place(i + col, margin + col * (cardWidth + gap), y, cardWidth, rowHeight);
                y += rowHeight + gap;
            }
            // Tables and plots need the full available width in the compact layout.
            for (int i = 4; i < cards.Count; i++)
            {
                double rowHeight = Math.Clamp(h - 28, 340, 500);
                Place(i, margin, y, w - 2 * margin, rowHeight);
                y += rowHeight + gap;
            }
            canvas.Width = w; canvas.Height = y; UpdateProfile(); return;
        }
        if (Section)
        {
            double left = (w - 2 * margin - gap) * .60, right = w - 2 * margin - gap - left, top = 430, third = (left - 2 * gap) / 3;
            for (int i = 0; i < 3; i++) Place(i, margin + i * (third + gap), margin, third, top);
            double half = (left - gap) / 2; Place(3, margin, margin + top + gap, half, h - top - gap - 2 * margin); Place(4, margin + half + gap, margin + top + gap, left - half - gap, h - top - gap - 2 * margin);
            double rh = (h - 2 * margin - 2 * gap) / 3; for (int i = 0; i < 3; i++) Place(5 + i, margin + left + gap, margin + i * (rh + gap), right, rh);
        }
        else
        {
            double top = pileTop, x = margin, avail = w - 2 * margin - 3 * gap;
            double efficiency = Geo ? efficiencyWidth : 0, normative = Geo ? normativeWidth : 0;
            double[] widths = Geo
                ? [generalWidth, efficiency, normative, avail - generalWidth - efficiency - normative]
                : [avail * .39, avail * .17, avail * .24, avail * .20];
            for (int i = 0; i < 4; i++) { double cw = widths[i]; Place(i, x, 14, cw, top); x += cw + gap; }
            double y = 14 + top + gap; h = Math.Max(h, y + 440); avail = w - 2 * margin - 2 * gap; x = margin;
            double[] lower = expanded == 5 ? [.16, .65, .19] : expanded == 4 ? [.66, .15, .19] : [.47, .20, .33];
            for (int i = 0; i < 3; i++) { double cw = avail * lower[i]; Place(4 + i, x, y, cw, h - y - 24); x += cw + gap; }
        }
        canvas.Width = w; canvas.Height = h; UpdateProfile();
    }
    private void Changed()
    {
        if (building || disposed) return;
        recalculation.Invalidate(); Result = null; tables = []; tableSelect.Items.Clear(); tableHost.Content = null; warnings.Text = ""; warnings.Visibility = Visibility.Collapsed;
        status.Text = "Dati modificati · premere Calcola"; allSeries.Clear(); visibility.Clear(); curveChoices.Children.Clear(); plot.Series = []; plot.InvalidateVisual();
        raw.Text = "Dati modificati · risultati da ricalcolare";
        Preview(); LayoutCards(); UpdateVerification(); QueueCalculation(); Modified?.Invoke();
    }
    private void QueueCalculation()
    {
        if (!Geo || building || disposed) return;
        status.Text = "Aggiornamento automatico in attesa…";
        scheduledCalculation = CalculateScheduledAsync(TimeSpan.FromMilliseconds(450), commitEdits: false);
    }
    internal async Task CalculateAsync(bool commitEdits = true)
    {
        if (foundation is not null) { foundation.TryCalculate(); return; }
        if (pileGroup is not null) { pileGroup.TryCalculate(); return; }
        if (retainingWall is not null) { await retainingWall.CalculateAsync(); return; }
        if (bridgeDesign is not null) { await bridgeDesign.CalculateAsync(); return; }
        if (bridge is not null) { await bridge.CalculateAsync(commitEdits); return; }
        if (materials is not null || rebarMaterial is not null) { Commit(); return; }
        if (horizontal is not null) { await horizontal.CalculateActiveAsync(); return; }
        if (concrete is not null) { await concrete.CalculateAllAsync(); return; }
        scheduledCalculation = CalculateScheduledAsync(TimeSpan.Zero, commitEdits);
        await scheduledCalculation;
    }
    private async Task CalculateScheduledAsync(TimeSpan delay, bool commitEdits)
    {
        if (disposed) return;
        if (commitEdits) Commit();
        var snapshot = (JsonObject)Data.DeepClone();
        var completed = await recalculation.ScheduleAsync(async token =>
        {
            Busy = true; calculate.IsEnabled = false; if (!Geo) canvas.IsEnabled = false;
            status.Text = "Calcolo in corso…";
            try { return (await Task.Run(() => calculationSession.Calculate(snapshot, token), token)).Values; }
            finally { Busy = false; if (!disposed) { calculate.IsEnabled = true; canvas.IsEnabled = true; } }
        }, delay);
        completed.TryPublish(current =>
        {
            try
            {
                if (current.Error is not null) throw current.Error;
                var calculated = current.Value!;
                if (calculated.S("errore") != "") { Result = null; status.Text = "Dati da completare: " + calculated.S("errore"); SetWarnings(Geo ? "" : calculated.S("errore")); return; }
                Result = calculated; SetWarnings(string.Join(Environment.NewLine, calculated.Array("avvisi").Select(v => v!.ToString())));
                ShowGeoResults(); raw.Text = Result.ToJsonString(J.Options);
                status.Text = "Calcolo completato · risultati riferiti ai dati correnti";
            }
            catch (Exception ex) { Result = null; status.Text = "Errore: " + ex.Message; SetWarnings(ex.Message); }
        });
    }
    private void SetWarnings(string text) { warnings.Text = text; warnings.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible; }
    private void PopulateTables()
    { tableSelect.Items.Clear(); if (Geo) tableSelect.Items.Add("Capacità portante — riepilogo"); foreach (var t in tables) tableSelect.Items.Add(t.Titolo); if (tableSelect.Items.Count > 0) tableSelect.SelectedIndex = 0; }
    private void ShowTable()
    {
        int i = tableSelect.SelectedIndex;
        if (Geo && i == 0 && Result is not null)
        {
            var panel = new StackPanel { MaxWidth = 620 };
            panel.Children.Add(Ui.Text($"Compressione e trazione · quota disponibile {(Micro ? "s" : "z")} = {Result.D("profondita_massima").ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("it-IT"))} m" + (Result.B("copertura_completa") ? "" : " · copertura incompleta"), 11, color: Ui.Muted));
            foreach (var t in Tabelle.CapacitaPalo(Result, Micro))
            {
                var title = Ui.Text(t.Titolo, 15, true); title.HorizontalAlignment = HorizontalAlignment.Center; title.Margin = new Thickness(0, 12, 0, 5); panel.Children.Add(title);
                var grid = DetailTable(t);
                grid.MinHeight = 80; panel.Children.Add(grid);
            }
            panel.Children.Add(Ui.Text("Ramo governante (media o minimo) scelto separatamente per compressione e trazione.", 10, color: Ui.Muted));
            tableHost.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            return;
        }
        if (Geo) i--;
        if (i >= 0 && i < tables.Count) { var t = tables[i]; tableHost.Content = Geo ? DetailTable(t) : Ui.Table(t.Colonne, t.Righe); }
    }
    private static DataGrid DetailTable(Tabella table)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        string Format(string value) => double.TryParse(value, System.Globalization.NumberStyles.Float, culture, out double number) && double.IsFinite(number)
            ? number.ToString("0.0", culture) : value;
        var grid = Ui.Table(table.Colonne, table.Righe.Select(row => row.Select(Format).ToArray()));
        grid.FontSize = 12; grid.MaxWidth = Math.Min(1100, table.Colonne.Length * 155);
        foreach (var column in grid.Columns.Cast<DataGridTextColumn>())
        {
            column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            column.ElementStyle = style;
            var header = Ui.Text((string)column.Header, 12, true); header.TextAlignment = TextAlignment.Center; column.Header = header;
        }
        return grid;
    }
    private void ShowDetails()
    {
        var dialog = Ui.Dialog(this, "Tabelle e dettagli", outputs, 1050, 650); dialog.Closed += (_, _) => dialog.Content = null; dialog.ShowDialog();
    }
    internal void ExportResult(string filename)
    {
        if (!HasResults || Busy) throw new InvalidOperationException(concrete is not null ? "Attendere l’aggiornamento automatico e correggere gli eventuali dati incompleti prima di esportare." : "Premere Calcola prima di esportare.");
        var exported = Result!;
        if (bridgeDesign is not null) exported["dati"] = Data.DeepClone();
        if (concrete is not null) exported["dati"] = Data.DeepClone();
        Archivio.ScriviAtomico(filename, Encoding.UTF8.GetBytes(exported.ToJsonString(J.Options)));
    }
    internal void ExportReport(string filename, string title, HashSet<string> options, bool projectReport = false)
    {
        if (foundation is not null)
        {
            var current = Result ?? throw new InvalidOperationException("Calcolare prima di esportare.");
            var word = Anthea.FoundationWorkspace.FoundationReport.Word(title, current);
            var pdf = Anthea.FoundationWorkspace.FoundationReport.Pdf(title, current);
            Archivio.ScriviAtomico(filename, word);
            Archivio.ScriviAtomico(Path.ChangeExtension(filename, ".pdf"), pdf); return;
        }
        if (materials is not null || rebarMaterial is not null)
        {
            Commit();
            var sheet = J.Obj(("nome", title), ("modulo_id", Module), ("dati", Data.DeepClone()));
            var section = J.Obj(("nome", title), ("fogli", new JsonArray()), ("strutture", new JsonArray()));
            section.Array("fogli").Add(sheet);
            ReportProject.Write(filename, new ProjectReportPlan(section), new Dictionary<JsonObject, ReportProject.SheetContent> { [sheet] = MaterialReportContent() }, [], materialSheet: true);
            return;
        }
        Archivio.ScriviAtomico(filename, BuildReport(title, options, projectReport));
    }
    internal ReportProject.SheetContent MaterialReportContent()
    {
        Commit(); return Materiali.MaterialReportChapter.Create(Module, Data);
    }
    internal byte[] BuildReport(string title, HashSet<string> options, bool projectReport = false) => DocxWriter.Write(BuildReportDocument(title, options, projectReport));
    internal DocxDocument BuildReportDocument(string title, HashSet<string> options, bool projectReport = false)
    {
        if (foundation is not null) return Anthea.FoundationWorkspace.FoundationReport.WordDocument(title, Result ?? throw new InvalidOperationException("Calcolare prima di esportare."));
        if (pileGroup is not null) return ReportPileGroup.CreateDocument(title, Result ?? throw new InvalidOperationException("Calcolare prima di esportare."));
        if (retainingWall is not null) return retainingWall.BuildReportDocument(title);
        if (bridgeDesign is not null) return bridgeDesign.BuildReportDocument(title);
        if (bridge is not null) return bridge.BuildReportDocument(title, options, projectReport);
        if (concrete is not null) return concrete.BuildReportDocument(title, options, projectReport);
        if (Result is null) throw new InvalidOperationException(concrete is not null ? "Attendere l’aggiornamento automatico e correggere gli eventuali dati incompleti prima di esportare." : "Premere Calcola prima di esportare.");
        if (horizontal is not null && Result.S("tipo_risultato") == "palo_elastico") return ReportElasticPile.CreateDocument(title, Result);
        if (horizontal is not null) return ReportOrizzontale.CreateDocument(title, Result, !projectReport, horizontal.ReportFigures());
        var images = ModuleFigureRenderer.VerticalFigures(Data, Result, Micro, plot, reference);
        return ReportWord.CreateDocument(title, Module, Data, Result, options, images.Select(f => f.Image()).ToList(), !projectReport);
    }
    private void SavePlot()
    { var save = new SaveFileDialog { Filter = "Immagine PNG|*.png", FileName = "capacita.png" }; if (save.ShowDialog(Window.GetWindow(this)) == true) Archivio.ScriviAtomico(save.FileName, plot.Png()); }
}
