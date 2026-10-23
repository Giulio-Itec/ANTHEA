using Anthea.Application.Presentation;
using Anthea.Application.Modules;
using Anthea.Presentation.Modules.VerticalPile;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class SheetEditor
{
    private void BuildGeo()
    {
        ModuleSheetPreparation.PrepareVertical(Data, Module);
        var defaults = Archivio.NuovoFoglio(Module); var g = Data["generali"]!.AsObject();
        // F5.6: the fields, the texts and the rules of the sheet are shared with VerticalPileSheetViewModel (VerticalPileFields in
        // ANTHEA.Presentation, VerticalPilePresentation in ANTHEA.Application), moved without changes.
        generalForm = new(g, VerticalPileFields.General(Micro), key =>
        {
            if (Micro && key == "tipo_iniezione") ResetAlphas();
            // D7-d: a new technology brings its γb of NTC Tab. 6.4.II; a value typed afterwards is kept (and declared by the calculation).
            if (key is "tipo_palo" or "sottotipo_palo_battuto") normativeForm.Set("sicurezza_base", NormativeBase(g));
            Changed();
        }, symbolColumns: true);
        generalForm.ShowField(VerticalPileFields.HiddenMethod(Micro), false); AddCard("Dati generali", generalForm);
        efficiencyForm = new(Data["efficienza"]!.AsObject(), VerticalPileFields.Efficiency(), _ => Changed(), true, symbolColumns: true);
        AddCard("Efficienza", Ui.Dock(efficiencyForm, bottom: effLabel));
        Field[] norm = VerticalPileFields.Normative();
        normativeForm = new(g, norm, _ => Changed(), true, symbolColumns: true);
        AddCard("Coefficienti normativa", normativeForm, action: Ui.Button("Reset", () => { foreach (var f in norm.Where(f => !f.ReadOnly)) normativeForm.Set(f.Key, VerticalPilePresentation.NormativeResetValue(f.Key, g, defaults, Micro)); }));
        AddCard("Verifica", Ui.Dock(verification, bottom: Ui.Text(VerticalPilePresentation.VerificationNote, 13, color: Ui.Muted)));
        if (Data["stratigrafie"] is not JsonArray) Data["stratigrafie"] = new JsonArray(); RebuildSondages();
        sondages.SelectionChanged += (_, e) => { if (e.Source == sondages) { UpdateProfile(); BuildReferencePlot(); } };
        var surveyActions = Ui.Bar(Ui.Button("+", () => { Commit(); Data.Array("stratigrafie").Add(new JsonArray(NewLayer())); RebuildSondages(Data.Array("stratigrafie").Count - 1); Changed(); }), Ui.Button("−", () =>
        {
            if (sondages.SelectedIndex < 0) return;
            DeleteSurvey(Data.Array("stratigrafie")[sondages.SelectedIndex]!.AsArray());
        }));
        if (Geo)
        {
            surveyActions.Children.Add(Ui.Button("Copia in…", () => ChooseSurveyCopy(copyInto: true)));
            surveyActions.Children.Add(Ui.Button("Copia da…", () => ChooseSurveyCopy(copyInto: false)));
        }
        AddCard("Stratigrafia", sondages, true, surveyActions); stratigraphy.Data = Data; stratigraphy.Micro = Micro; AddCard("Profilo stratigrafico", stratigraphy, true);
        capacityView.ItemsSource = VerticalPileFields.CapacityViews(Micro);
        capacityView.SelectedIndex = 0; capacityView.SelectionChanged += (_, _) => { UpdateVisible(); RebuildCurveChoices(); };
        var actions = Ui.Bar(endValues, Ui.Button("Tutte", () => SetVisible(true), inspection: true), Ui.Button("Nessuna", () => SetVisible(false), inspection: true));
        var footer = Ui.Stack(actions, new ScrollViewer { Content = curveChoices, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        outputs.Items.Insert(0, new TabItem { Header = "Grafico", Content = Ui.Dock(plot, bottom: footer) });
        outputs.Items.Insert(2, new TabItem { Header = Micro ? "Abachi" : "Nq", Content = reference }); outputs.SelectedIndex = 0;
        var context = new ContextMenu(); var fit = new MenuItem { Header = "Adatta" }; fit.Click += (_, _) => plot.ResetView(); var save = new MenuItem { Header = "Salva PNG…" }; save.Click += (_, _) => SavePlot(); context.Items.Add(fit); context.Items.Add(save); plot.ContextMenu = context;
        AddCard("Grafici capacità portante", Ui.Dock(outputs, capacityView), true); UpdateVerification();
    }
    /// <summary>γb of NTC Tab. 6.4.II for the technology of the sheet, in the invariant format of the stored coefficients.</summary>
    private string NormativeBase(JsonNode g) => VerticalPilePreview.NormativeBase(g, Micro);
    private void ResetAlphas()
    {
        VerticalPilePreview.ResetAlphas(Data);
        RebuildSondages();
    }
    private void RebuildSondages(int? selection = null)
    {
        int selected = selection ?? sondages.SelectedIndex; sondages.Items.Clear(); layerGrids.Clear(); int index = 0;
        foreach (var rowsNode in Data.Array("stratigrafie"))
        {
            var rows = rowsNode!.AsArray();
            // Legacy values are included in the selectors without silently replacing user data (VerticalPileFields.Layers).
            var fields = VerticalPileFields.Layers(Micro, rows);
            var grid = new JsonGrid(fields); layerGrids.Add(grid);
            JsonRow Bind(JsonObject data)
            {
                var display = (JsonObject)data.DeepClone(); if (!display.ContainsKey("laterale_attiva")) display["laterale_attiva"] = true;
                if (!Micro && !display.ContainsKey("nc")) display["nc"] = "9";
                JsonRow? bound = null;
                bound = new JsonRow(display, key =>
                {
                    if (Micro && key == "terreno" && VerticalPilePreview.InitialAlpha(display.S("terreno"), Data["generali"].S("tipo_iniezione")) is { } alpha)
                        bound!.Output("alpha", alpha);
                    foreach (var f in fields.Where(f => !f.ReadOnly)) data[f.Key] = display[f.Key]?.DeepClone();
                    Changed();
                });
                return bound;
            }
            foreach (var r in rows.OfType<JsonObject>()) grid.Rows.Add(Bind(r));
            if (Geo)
            {
                void AddLayer() { var row = NewLayer(); rows.Add(row); grid.Rows.Add(Bind(row)); Changed(); }
                void DeleteLayer(JsonRow row)
                {
                    int position = grid.Rows.IndexOf(row); if (position < 0) return;
                    rows.RemoveAt(position); grid.Rows.RemoveAt(position); Changed();
                }
                var tab = Ui.Tab(sondages, $"{++index}", StratigraphyTable.Build(grid, fields, AddLayer, DeleteLayer, Micro));
                var removeSurvey = Ui.Button("[−]", () => DeleteSurvey(rows));
                removeSurvey.FontSize = 11; removeSurvey.MinHeight = 20; removeSurvey.Padding = new Thickness(3, 0, 3, 0);
                removeSurvey.ToolTip = $"Elimina stratigrafia {index}";
                tab.Header = Ui.Bar(Ui.Text($"{index}", 12), removeSurvey);
                continue;
            }
            var buttons = Ui.Bar(Ui.Button(Micro ? "+ Strato" : "Aggiungi strato", () =>
            {
                grid.Commit(); var row = Micro ? J.Obj(("spessore", ""), ("terreno", ""), ("alpha", ""), ("laterale_attiva", true)) : J.Obj(("spessore", ""), ("tipologia", "Granulare"), ("addensamento", "Sciolto"), ("peso_specifico", ""), ("peso_specifico_saturo", ""), ("angolo_attrito", ""), ("coesione_efficace", ""), ("coesione_non_drenata", ""), ("nc", "9"), ("laterale_attiva", true));
                rows.Add(row); grid.Rows.Add(Bind(row)); Changed();
            }), Ui.Button(Micro ? "− Strato" : "Elimina ultimo strato", () =>
            { grid.Commit(); int i = Micro ? grid.SelectedIndex : grid.Rows.Count - 1; if (i < 0) return; rows.RemoveAt(i); grid.Rows.RemoveAt(i); Changed(); }));
            Ui.Tab(sondages, Micro ? $"Sondaggio {++index}" : $"{++index}", Ui.Dock(grid, bottom: buttons));
        }
        if (sondages.Items.Count > 0) sondages.SelectedIndex = Math.Clamp(selected, 0, sondages.Items.Count - 1);
    }
    private JsonObject NewLayer() => Micro ? Archivio.NuovoStratoMicropalo() : Archivio.NuovoStratoPalo();
    private void DeleteSurvey(JsonArray survey, bool confirm = true)
    {
        var surveys = Data.Array("stratigrafie"); int index = surveys.IndexOf(survey);
        if (index < 0) return;
        if (confirm && MessageBox.Show(Window.GetWindow(this), $"Eliminare la stratigrafia {index + 1} e tutti i suoi strati?", "Stratigrafia", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Commit(); int selected = sondages.SelectedIndex;
        surveys.RemoveAt(index);
        RebuildSondages(index < selected ? selected - 1 : selected); Changed();
    }
    private void ChooseSurveyCopy(bool copyInto)
    {
        int current = sondages.SelectedIndex; var surveys = Data.Array("stratigrafie");
        if (current < 0) { MessageBox.Show(Window.GetWindow(this), "Selezionare una stratigrafia."); return; }
        var choices = Enumerable.Range(0, surveys.Count).Where(i => i != current)
            .Select(i => new KeyValuePair<int, string>(i, $"Stratigrafia {i + 1}")).ToList();
        if (copyInto) choices.Add(new(-1, "Nuova stratigrafia"));
        if (choices.Count == 0) { MessageBox.Show(Window.GetWindow(this), "Non ci sono altre stratigrafie da cui copiare."); return; }
        var selection = new ComboBox { ItemsSource = choices, DisplayMemberPath = "Value", SelectedValuePath = "Key", SelectedIndex = 0, Margin = new Thickness(8) };
        var dialog = Ui.Dialog(this, copyInto ? "Copia stratigrafia in…" : "Copia stratigrafia da…", new Grid(), 430, 210);
        var ok = Ui.Button("Copia", () => dialog.DialogResult = true, true); ok.IsDefault = true;
        var cancel = Ui.Button("Annulla", () => dialog.DialogResult = false); cancel.IsCancel = true;
        dialog.Content = Ui.Paper(Ui.Dock(selection, Ui.Text(copyInto ? $"Copia gli strati della stratigrafia {current + 1} in:" : $"Sostituisci gli strati della stratigrafia {current + 1} copiando da:"), Ui.Bar(ok, cancel)));
        if (dialog.ShowDialog() == true && selection.SelectedValue is int other)
            CopySurvey(copyInto ? current : other, copyInto ? other : current);
    }
    private bool CopySurvey(int source, int destination, bool confirm = true)
    {
        var surveys = Data.Array("stratigrafie");
        if (source < 0 || source >= surveys.Count || destination < -1 || destination >= surveys.Count || source == destination) return false;
        if (destination >= 0 && confirm && MessageBox.Show(Window.GetWindow(this),
            $"Sostituire tutti gli strati della stratigrafia {destination + 1} con una copia della stratigrafia {source + 1}?", "Copia stratigrafia", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;
        Commit(); var copy = surveys[source]!.DeepClone();
        if (destination == -1) { surveys.Add(copy); destination = surveys.Count - 1; }
        else surveys[destination] = copy;
        RebuildSondages(destination); Changed(); return true;
    }
    private void Preview()
    {
        if (Section) return;
        var values = VerticalPilePreview.Read(Data, Micro);
        if (Micro)
        {
            generalForm.Set("__peso_lineare", values.Weight, display: true);
            generalForm.Editors["__peso_lineare"].ToolTip = "Peso calcolato da diametro, profilo CHS e peso specifico del riempimento. La visualizzazione è arrotondata; il calcolo usa la precisione completa.";
        }
        var g = Data["generali"]!; effLabel.Text = values.Efficiency;
        foreach (var (key, value) in values.Correlation) normativeForm.Set(key, value, true);
        foreach (var (key, enabled) in VerticalPilePresentation.GeneralFieldStates(g, Micro)) generalForm.Enable(key, enabled, dim: true);
        foreach (var (key, enabled) in VerticalPilePresentation.NormativeFieldStates(g, Micro)) normativeForm.Enable(key, enabled, dim: true);
        string method = Data["efficienza"].S("metodo"); foreach (string key in efficiencyForm.Editors.Keys.Where(k => k != "metodo")) efficiencyForm.ShowField(key, VerticalPilePresentation.EfficiencyFieldVisible(key, method));
        plot.EmptyMessage = VerticalPilePresentation.CapacityEmptyMessage(Data);
        UpdateProfile(); UpdatePileCoefficients(); BuildReferencePlot();
    }
    private void UpdateProfile()
    { if (Section) return; stratigraphy.SelectedIndex = sondages.SelectedIndex; stratigraphy.ShowAll = expanded == 5; stratigraphy.InvalidateVisual(); }
    private void UpdatePileCoefficients()
    {
        if (!Geo) return;
        foreach (var grid in layerGrids)
        {
            var fields = VerticalPilePreview.LayerFields(Data["generali"]!, grid.Rows.Select(row => row.Values), Micro);
            for (int index = 0; index < grid.Rows.Count; index++)
            {
                var row = grid.Rows[index];
                row.Output("__color", StratigraphyDrawing.LayerColors[index % StratigraphyDrawing.LayerColors.Length]);
                foreach (var (key, value) in fields[index]) row.Output(key, value);
            }
        }
    }
    private void ShowGeoResults()
    {
        allSeries.Clear(); visibility.Clear();
        foreach (var curve in Anthea.Application.Presentation.VerticalPileCharts.Capacity(Data, Result!, Micro))
        { allSeries.Add((curve.Key, PlotPresentation.Series(curve.Series))); visibility[curve.Key] = curve.Visible; }
        plot.Title = VerticalPilePresentation.CapacityTitle(Micro); plot.YLabel = VerticalPilePresentation.CapacityAxis(Micro); plot.CapacityDepth = VerticalPilePresentation.AvailableDepth(Result!); plot.ResetView(); UpdateVisible(); RebuildCurveChoices();
        tables = VerticalPilePresentation.Tables(Result!, Micro);
        PopulateTables(); UpdateVerification(); BuildReferencePlot();
        if (!Result!.B("copertura_completa")) SetWarnings(VerticalPilePresentation.Warnings(warnings.Text, Result));
    }
    private void UpdateVerification()
    {
        if (Geo) { UpdatePileVerification(); return; }
        if (Section) return; var rows = new List<string[]>();
        if (Result?.B("copertura_completa") == true)
        {
            foreach (var (key, curve) in Result["curve"]!.AsObject())
            {
                double rd = curve!.Array("progetto")[^1]![1]!.GetValue<double>(); var action = Anthea.Application.Presentation.VerticalPileCharts.Actions(Result, key)?.LastOrDefault(); double? ed = J.Number(action?[1]);
                rows.Add([key.Replace("non_drenante", "N.dr.").Replace("drenante", "Dr.").Replace("compressione", "C").Replace("trazione", "T").Replace('_', ' '), ed?.ToString("N1") ?? "—", rd.ToString("N1"), ed.HasValue && rd > 0 ? (100 * ed.Value / rd).ToString("N1") + "%" : "—"]);
            }
        }
        else foreach (string name in Micro ? new[] { "C", "T" } : ["Dr. C", "Dr. T", "N.dr. C", "N.dr. T"]) rows.Add([name, "—", "—", "—"]);
        var table = Ui.Table(["Cond.", "NEd [kN]", "Rd [kN]", "Util."], rows); foreach (var col in table.Columns) col.Width = new DataGridLength(1, DataGridLengthUnitType.Star); verification.Content = table;
        if (Pile) { table.FontSize = 15; table.RowHeight = 35; }
    }
    private void UpdatePileVerification()
    {
        var table = Ui.Table(VerticalPilePresentation.VerificationColumns(), VerticalPilePresentation.Verification(Result, Micro));
        table.FontSize = 15; table.RowHeight = double.NaN; table.MinRowHeight = 52;
        double[] weights = [1.4, .9, .9, .8, 1.8], minimums = [85, 52, 52, 48, 100];
        if (Micro) { weights = [1.8, .85, .85, .75, 1.9]; minimums = [100, 48, 48, 44, 96]; }
        for (int i = 0; i < table.Columns.Count; i++)
        {
            var column = (DataGridTextColumn)table.Columns[i];
            column.Width = new DataGridLength(weights[i], DataGridLengthUnitType.Star); column.MinWidth = minimums[i];
            var header = Ui.Text((string)column.Header, 12, true); header.TextAlignment = TextAlignment.Center; column.Header = header;
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, i is >= 1 and <= 3 || Micro && i == 0 ? TextWrapping.NoWrap : TextWrapping.Wrap));
            if (Micro && i == 0) style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 13.0));
            if (i is >= 1 and <= 3) style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 14.0));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(3, 5, 3, 5)));
            if (i == 4)
            {
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Appearance.Foreground(Ui.Muted)));
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                foreach (var (value, color) in new[] { ("Verifica soddisfatta", "#16703B"), ("Verifica non soddisfatta", "#B42318") })
                {
                    var trigger = new DataTrigger { Binding = new Binding("[4]"), Value = value };
                    trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, Appearance.Foreground(Ui.Brush(color)))); style.Triggers.Add(trigger);
                }
            }
            column.ElementStyle = style;
        }
        verification.Content = table;
    }
    private bool CapacityIncludes(string key)
        => Anthea.Application.Presentation.VerticalPileCharts.CapacityIncludes(key, Micro, capacityView.SelectedIndex, allSeries.Select(s => s.Key));
    private void UpdateVisible()
    { plot.Series = allSeries.Where(p => visibility.GetValueOrDefault(p.Key) && CapacityIncludes(p.Key)).Select(p => p.Series).ToList(); if (Result is not null) plot.EmptyMessage = VerticalPilePresentation.NoCurveSelected; plot.InvalidateVisual(); }
    private void SetVisible(bool show)
    { foreach (var p in allSeries) visibility[p.Key] = show; StoreVisibility(); UpdateVisible(); RebuildCurveChoices(); }
    private void StoreVisibility()
    {
        if (Result is null) return; if (Data["visibilita_grafici"] is not JsonObject) Data["visibilita_grafici"] = new JsonObject(); bool changed = false;
        foreach (var (key, value) in visibility) { if (Data["visibilita_grafici"]![key]?.ToString() != value.ToString().ToLowerInvariant()) changed = true; Data["visibilita_grafici"]![key] = value; }
        if (changed) Modified?.Invoke();
    }
    private void RebuildCurveChoices()
    {
        curveChoices.Children.Clear(); endValues.Text = VerticalPilePresentation.EndValues(Data, Result);
        foreach (var (key, series) in allSeries.Where(p => CapacityIncludes(p.Key)))
        {
            var check = new CheckBox { Content = VerticalPilePresentation.CurveChoice(series.Name, series.Points, Result), Foreground = Appearance.Series(series.Color), IsChecked = visibility[key], Margin = new Thickness(2, 4, 8, 4), FontSize = 11 };
            void Change() { visibility[key] = check.IsChecked == true; StoreVisibility(); UpdateVisible(); }
            check.Checked += (_, _) => Change(); check.Unchecked += (_, _) => Change(); curveChoices.Children.Add(check);
        }
    }
    private void BuildReferencePlot(int? selectedSurvey = null)
    {
        if (Section) return;
        PlotPresentation.Apply(reference, Anthea.Application.Presentation.VerticalPileCharts.Reference(Data, Result, Micro, selectedSurvey ?? sondages.SelectedIndex));
    }
}
