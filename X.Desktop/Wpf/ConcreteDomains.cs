using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace
{
    private readonly Dictionary<string, (string Signature, CheckerDomain3D? Three, CheckerDomain2D? Two)> domainCache = new();
    private (string Signature, CheckerSectionModel Model)? preparedSection;
    private readonly Dictionary<string, string> calculationErrors = new();
    private sealed class DomainPanel
    {
        internal bool ThreeD;
        internal JsonObject Options = null!;
        internal InputForm Form = null!;
        internal ComboBox Mode = null!;
        internal JsonGrid Grid = null!;
        internal DomainViewport3D? View3D;
        internal Button ForceToggle = null!;
        internal Slider? Transparency;
        internal bool NeedsVisualRefresh = true, AttachingRows;
        internal string? OutputKey;
        internal Dictionary<string, DomainCheck>? OutputChecks;
        internal readonly Plot Plot = new() { InvertY = false, CenteredAxes = true, Title = "Sezione del dominio", EmptyMessage = "Dominio in attesa di aggiornamento automatico" };
        internal readonly TextBlock Detail = Ui.Text("Selezionare una combinazione", 12);
        internal readonly VerificationCards Summary = new();
        internal readonly SectionInspection Inspection = new();
        internal string Key => Options.S("stato", "SLU");
        internal string Prefix => ThreeD ? "3D:" : "2D:";
    }
    private UIElement BuildDomainPanel(bool threeD)
    {
        var panel = new DomainPanel { ThreeD = threeD, Options = settings[threeD ? "dominio3d" : "dominio2d"]!.AsObject() }; domainPanels.Add(panel);
        var fields = new List<Field> { new("stato", "Tipo di dominio", Choices: ["SLU", "SLV"]) };
        if (threeD) fields.AddRange([new("criterio", "Ricerca resistenza", Choices: CheckerSection.Criteria), new("strategia", "Metodo", Choices: ["Iterativo", "Intersezione"]), new("interpolazione", "Interpolazione mesh", Choices: ["Quadratica", "Lineare"]), new("suddivisioni_n", "Suddivisioni N (mesh)")]);
        else fields.AddRange([new("tipo", "Sezione dominio", Choices: ["N–M", "Mx–My"]), new("theta", "Direzione θ", "°"), new("N", "N fissato", "kN")]);
        fields.AddRange([new("angoli", "Direzioni angolari"), new("trazione_cls", "CLS resistente a trazione", Choices: ["No", "Sì"]), new("assi", "Assi delle azioni", Choices: ["Locali", "Principali", "Personalizzati"]), new("origine_x", "Origine x", "mm"), new("origine_y", "Origine y", "mm"), new("rotazione", "Rotazione assi", "°")]);
        if (!threeD) fields.Add(new("proietta", "Proietta azioni sul piano", Choices: ["No", "Sì"]));
        fields.Add(new("filtro", "Filtro azioni", Choices: ["Tutte", "Entro il dominio", "Fuori dominio", "Da controllare"]));
        panel.Form = new(panel.Options, fields, key =>
        {
            if (initializing) return;
            if (key is "filtro" or "stato") { AttachDomainRows(panel); RefreshDomainPanel(panel); Modified?.Invoke(); }
            else if (key is "interpolazione" or "suddivisioni_n") RefreshDisplayMesh(panel);
            else if (key is "criterio" or "strategia" or "proietta") InvalidateDomainForces(panel);
            else { panel.NeedsVisualRefresh = true; if (panel.ThreeD) panel.View3D?.SetMesh(null); else { panel.Plot.Segments = []; panel.Plot.InvalidateVisual(); } InvalidateDomainForces(panel); }
            panel.Form.Enable("theta", panel.Options.S("tipo") == "N–M"); panel.Form.Enable("N", panel.Options.S("tipo") == "Mx–My");
            foreach (var field in new[] { "origine_x", "origine_y", "rotazione" }) panel.Form.Enable(field, panel.Options.S("assi") == "Personalizzati");
        }, true, true);
        panel.Mode = (ComboBox)panel.Form.Editors["stato"];
        RevisionInspection.Allow(panel.Mode); RevisionInspection.Allow(panel.Form.Editors["filtro"]);
        var modeText = new FrameworkElementFactory(typeof(TextBlock)); modeText.SetBinding(TextBlock.TextProperty, new Binding { Converter = new DomainLabelConverter() });
        panel.Mode.ItemTemplate = new DataTemplate { VisualTree = modeText };
        panel.Form.GroupFields("Discretizzazione e interpolazione", ["angoli", "interpolazione", "suddivisioni_n"]);
        panel.Form.GroupFields("Modello e assi delle azioni", ["trazione_cls", "assi", "origine_x", "origine_y", "rotazione"]);
        var options = Ui.Stack(panel.Form);
        if (!threeD) options.Children.Add(Ui.Button("Sezione sull’azione selezionata", () =>
        {
            if (panel.Grid.SelectedItem is not JsonRow row) return;
            try
            {
                var a = ReadAction(row);
                if (panel.Options.S("tipo") == "Mx–My") panel.Form.Set("N", a.N.ToString("G12", System.Globalization.CultureInfo.InvariantCulture));
                else panel.Form.Set("theta", (Math.Atan2(a.My, a.Mx) * 180 / Math.PI).ToString("G12", System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (ArgumentException ex) { status.Text = ex.Message; }
        }));
        options.Children.Add(Notice(threeD ? "Domini plastico/elastico e punti resistenti calcolati da Checker. L’interpolazione modifica la mesh visualizzata. Gli assi scelti si riferiscono alle azioni; il grafico è negli assi locali della sezione." : "Stessa logica CheckerUI: N–M diretto nelle direzioni locali principali, altrimenti sezione del dominio tramite Checker. Con proiezione attiva si verifica l’azione proiettata, non l’intera azione 3D."));
        options.Children.Add(Ui.Text(UtilizationPalette.Legend + "\nGrigio: esito incompleto o non disponibile", 11, color: Ui.Muted));
        var left = Panel("Opzioni di calcolo", Scroller(options), "N negativo a compressione · grafici negli assi locali Checker");
        UIElement viewport; ViewportFrame frame;
        if (threeD)
        {
            panel.View3D = new(); frame = new ViewportFrame("Dominio N–Mx–My", panel.View3D, panel.View3D.ResetView);
            frame.Toolbar.Children.Insert(0, Ui.Button("Mesh", panel.View3D.ToggleWireframe, inspection: true));
            var view = Ui.Choice(["Isometrica", "N–Mx", "N–My", "Mx–My"], "Isometrica"); view.Width = 100; view.SelectionChanged += (_, _) => panel.View3D.StandardView(view.Text); frame.Toolbar.Children.Insert(0, view);
            panel.View3D.ActionSelected += id => { panel.Grid.SelectedItem = actions[panel.Key].FirstOrDefault(r => r.Values.S("id") == id); if (panel.Grid.SelectedItem is not null) panel.Grid.ScrollIntoView(panel.Grid.SelectedItem); };
            panel.Transparency = new Slider { Minimum = 0, Maximum = 100, Value = Math.Clamp(panel.Options.D("trasparenza", 35), 0, 100), Width = 95, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Trasparenza del dominio: 0% opaco, 100% trasparente", SmallChange = 5, LargeChange = 10 };
            var percent = Ui.Text($"{panel.Transparency.Value:0}%", 11); percent.MinWidth = 32;
            panel.View3D.SurfaceOpacity = 1 - panel.Transparency.Value / 100;
            panel.Transparency.ValueChanged += (_, _) => { panel.Options["trasparenza"] = panel.Transparency.Value; percent.Text = $"{panel.Transparency.Value:0}%"; panel.View3D.SurfaceOpacity = 1 - panel.Transparency.Value / 100; Modified?.Invoke(); };
            frame.Toolbar.Children.Add(Ui.Bar(Ui.Text("Trasparenza", 11), panel.Transparency, percent));
        }
        else frame = new ViewportFrame("Sezione del dominio", panel.Plot, panel.Plot.ResetView);
        AddDomainScaleControls(panel, frame);
        RevisionInspection.Allow(frame.Toolbar);
        foreach (var (key, label) in new[] { ("dimensione_ed", "Punti Ed"), ("dimensione_rd", "Punti Rd") })
            {
                var slider = new Slider { Minimum = 2, Maximum = 14, Value = Math.Clamp(panel.Options.D(key, 5), 2, 14), Width = 70, ToolTip = label + " · raggio grafico" };
                slider.ValueChanged += (_, _) => { panel.Options[key] = slider.Value; UpdateSelection(panel); Modified?.Invoke(); };
                options.Children.Add(Ui.Bar(Ui.Text(label + " · dimensione", 12), slider));
            }
        string ForceLabel() => panel.Options.B("solo_selezionata") ? "Forze: selezionata" : "Forze: tutte";
        panel.ForceToggle = Ui.Button(ForceLabel(), () => { panel.Options["solo_selezionata"] = !panel.Options.B("solo_selezionata"); panel.ForceToggle.Content = ForceLabel(); UpdateSelection(panel); Modified?.Invoke(); });
        panel.ForceToggle.ToolTip = "Alterna tutte le azioni visibili e la sola combinazione selezionata; i filtri della tabella restano attivi.";
        frame.Toolbar.Children.Insert(0, panel.ForceToggle); viewport = frame;
        var displayChecks = new Dictionary<string, CheckBox>();
        foreach (var (key, label) in new[] { ("mostra_ed", "Sollecitazioni"), ("mostra_rd", "Resistenze"), ("tutte_rd", "Tutti i punti resistenti"), ("mostra_linee", "Linee di verifica"), ("colora_eta", "Colori η") })
        {
            var check = new CheckBox { Content = label, IsChecked = panel.Options.B(key, key != "colora_eta" && key != "tutte_rd"), Margin = new Thickness(5, 3, 5, 3), VerticalAlignment = VerticalAlignment.Center };
            displayChecks[key] = check;
            check.Click += (_, _) => { panel.Options[key] = check.IsChecked == true;
                if (key == "tutte_rd" && check.IsChecked == true) { panel.Options["mostra_linee"] = true; panel.Options["mostra_rd"] = true; displayChecks["mostra_linee"].IsChecked = true; displayChecks["mostra_rd"].IsChecked = true; }
                UpdateSelection(panel); Modified?.Invoke(); };
            frame.Toolbar.Children.Add(check);
        }
        var legend = new StackPanel();
        foreach (var (value, label) in new[] { (.25, "0 ≤ η ≤ 0,50"), (.6, "0,50 < η ≤ 0,70"), (.8, "0,70 < η ≤ 0,90"), (.95, "0,90 < η ≤ 1,00"), (1.1, "η > 1,00") })
            legend.Children.Add(Ui.Text("■  " + label, 11, color: UtilizationPalette.Brush(value)));
        frame.Toolbar.Children.Add(new Expander { Header = "Legenda η", Content = legend, Margin = new Thickness(5) });
        string ratio = threeD ? "eta3d" : "eta2d", outcome = threeD ? "esito3d" : "esito2d";
        panel.Grid = new JsonGrid([new("visible", "Mostra", Bool: true), new("nome", "Combinazione"), new("N", "N [kN]"), new("Mx", "Mx [kNm]"), new("My", "My [kNm]"), new(ratio, "η [-]", ReadOnly: true), new(outcome, "Esito", ReadOnly: true)], true, actions[panel.Key]);
        panel.Grid.Columns[^1].Width = new DataGridLength(2, DataGridLengthUnitType.Star);
        var table = Panel("Combinazioni N–Mx–My", ActionTable(panel.Key, panel.Grid, () => UpdateSelection(panel)), "Le azioni Plastico / Elastico sono condivise fra le schede 3D e 2D");
        var detailTabs = new TabControl { SelectedIndex = 1 }; Ui.Tab(detailTabs, "Dettagli combinazione", Scroller(panel.Detail)); Ui.Tab(detailTabs, "Riepilogo verifiche", Scroller(panel.Summary));
        var body = AnalysisLayout(left, BuildSectionInspection(panel, viewport), detailTabs, table);
        panel.Grid.IsVisibleChanged += (_, _) => { if (panel.Grid.IsVisible && panel.NeedsVisualRefresh) RefreshDomainPanel(panel); };
        AttachDomainRows(panel); panel.Form.Enable("theta", panel.Options.S("tipo") == "N–M"); panel.Form.Enable("N", panel.Options.S("tipo") == "Mx–My");
        foreach (var field in new[] { "origine_x", "origine_y", "rotazione" }) panel.Form.Enable(field, panel.Options.S("assi") == "Personalizzati");
        return body;
    }
    private void AttachDomainRows(DomainPanel panel)
    {
        if (panel.Grid is null) return;
        if (panel.Grid.ItemsSource is ListCollectionView editing && (editing.IsEditingItem || editing.IsAddingNew)) return;
        var previous = (panel.Grid.SelectedItem as JsonRow)?.Values.S("id"); panel.Grid.Tag = panel.Key;
        var view = new ListCollectionView(actions[panel.Key]);
        view.Filter = item =>
        {
            if (item is not JsonRow row) return false;
            domainResults.TryGetValue(panel.Prefix + panel.Key, out var results); results?.TryGetValue(row.Values.S("id"), out _);
            DomainCheck? check = results?.GetValueOrDefault(row.Values.S("id"));
            return panel.Options.S("filtro") switch { "Entro il dominio" => check?.Utilization is <= 1, "Fuori dominio" => check?.Utilization is > 1, "Da controllare" => check?.Utilization is null, _ => true };
        };
        panel.NeedsVisualRefresh = true; panel.AttachingRows = true;
        try
        {
            panel.Grid.ItemsSource = view;
            ApplyTableFilter(panel.Grid);
            panel.Grid.SelectedItem = view.Cast<JsonRow>().FirstOrDefault(r => r.Values.S("id") == previous) ?? view.Cast<JsonRow>().FirstOrDefault();
        }
        finally { panel.AttachingRows = false; }
    }
    private static ActionPoint ReadAction(JsonRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Values.S("nome"))) throw new ArgumentException("Assegnare un nome alla combinazione.");
        return new(SectionWorkspace.Number(row.Values.S("N"), "N"), SectionWorkspace.Number(row.Values.S("Mx"), "Mx"), SectionWorkspace.Number(row.Values.S("My"), "My"));
    }
    private async Task CalculateDomain(DomainPanel panel, CancellationToken token, string? requestedKey = null, CheckerSectionModel? prepared = null, JsonObject? preparedInput = null, JsonObject? preparedWorkspace = null)
    {
        string key = requestedKey ?? panel.Key;
        status.Text = $"Checker · {SectionWorkspace.Label(key)} · dominio {(panel.ThreeD ? "3D" : "2D")}…";
        var input = preparedInput ?? (JsonObject)Input.DeepClone(); var workspace = preparedWorkspace ?? (JsonObject)settings.DeepClone(); var options = (JsonObject)panel.Options.DeepClone();
        var snapshots = actions[key].Select(row => (JsonObject)row.Values.DeepClone()).ToArray();
        var computationOptions = (JsonObject)options.DeepClone();
        foreach (string visual in new[] { "stato", "filtro", "solo_selezionata", "trasparenza", "mostra_ed", "mostra_rd", "tutte_rd", "mostra_linee", "colora_eta", "criterio", "strategia", "proietta", "scala_x", "scala_y", "scala_n", "scala_mx", "scala_my", "fit_azioni", "dimensione_ed", "dimensione_rd", "interpolazione", "suddivisioni_n" }) computationOptions.Remove(visual);
        string signature = input.ToJsonString() + workspace.S("normativa") + workspace["coefficienti"]?.ToJsonString() + workspace["trefoli"]?.ToJsonString() + computationOptions.ToJsonString();
        var cached = domainCache.GetValueOrDefault(panel.Prefix + key);
        var calculated = await Task.Run(() =>
        {
            CheckerDomain3D? three = cached.Signature == signature ? cached.Three : null;
            CheckerDomain2D? two = cached.Signature == signature ? cached.Two : null;
            if (three is null && two is null)
            {
                var engine = prepared is null ? new CheckerSection(input, workspace, options, key) : new CheckerSection(prepared, input, workspace, options, key);
                three = panel.ThreeD ? engine.Domain3D(token) : null;
                two = panel.ThreeD ? null : engine.Domain2D(token);
            }
            three?.ConfigureVerification(options);
            if (two is not null) two.Section.Options["proietta"] = options["proietta"]?.DeepClone();
            var results = new Dictionary<string, DomainCheck>();
            var valid = new List<(string Id, ActionPoint Force)>();
            foreach (var snapshot in snapshots)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var force = ReadAction(new JsonRow(snapshot));
                    if (three is not null) valid.Add((snapshot.S("id"), force));
                    else results[snapshot.S("id")] = two!.Check(force);
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { results[snapshot.S("id")] = new(null, null, "Checker: " + ex.Message); }
            }
            if (three is not null)
            {
                var checks = three.CheckMany(valid.Select(row => row.Force).ToArray(), token);
                for (int i = 0; i < valid.Count; i++) results[valid[i].Id] = checks[i];
            }
            return (three, two, results);
        }, token);
        token.ThrowIfCancellationRequested();
        domainCache[panel.Prefix + key] = (signature, calculated.three, calculated.two);
        if (calculated.three is not null) checker3D[key] = calculated.three;
        if (calculated.two is not null) checker2D[key] = calculated.two;
        domainResults[panel.Prefix + key] = calculated.results;
        if (key == panel.Key) { AttachDomainRows(panel); RefreshDomainPanel(panel); }
    }
    private void RefreshDomainViews() { foreach (var panel in domainPanels) RefreshDomainPanel(panel); }
    private void RefreshDomainPanel(DomainPanel panel, bool renderHidden = false)
    {
        if (panel.Grid is null) return;
        domainResults.TryGetValue(panel.Prefix + panel.Key, out var checks);
        if (panel.OutputKey != panel.Key || !ReferenceEquals(panel.OutputChecks, checks))
        {
            using var notifications = JsonRow.DeferNotifications(actions[panel.Key]);
            string eta = panel.ThreeD ? "eta3d" : "eta2d", outcome = panel.ThreeD ? "esito3d" : "esito2d";
            foreach (var row in actions[panel.Key])
            {
                var check = checks?.GetValueOrDefault(row.Values.S("id")); row.Output(eta, EngineeringFormat.Number(check?.Utilization)); row.Output(outcome, check?.Status ?? "Da calcolare");
            }
            panel.OutputKey = panel.Key; panel.OutputChecks = checks; panel.NeedsVisualRefresh = true;
        }
        if (synchronizing || !panel.NeedsVisualRefresh || !renderHidden && !panel.Grid.IsVisible) return;
        if (panel.ThreeD)
        {
            var mesh = checker3D.GetValueOrDefault(panel.Key)?.DisplayMesh(panel.Options);
            if (mesh is not null) meshes[panel.Key] = mesh;
            panel.View3D!.SetMesh(mesh);
        }
        else
        {
            panel.Plot.Series = []; panel.Plot.Markers = [];
            if (checker2D.TryGetValue(panel.Key, out var section2d))
            {
                try
                {
                    bool nm = panel.Options.S("tipo") == "N–M";
                    double value = SectionWorkspace.Number(panel.Options.S(nm ? "theta" : "N"), nm ? "θ" : "N") * (nm ? Math.PI / 180 : 1);
                    var segments = section2d.Segments.Select(s => (section2d.Project(s.A), section2d.Project(s.B))).ToArray();
                    panel.Plot.Segments = segments;
                    panel.Plot.Title = $"{SectionWorkspace.Label(panel.Key)} · " + (nm ? $"N–M, θ = {value * 180 / Math.PI:0.00}°" : $"Mx–My, N = {value:0.00} kN");
                    panel.Plot.XLabel = nm ? "Mθ [kNm]" : "Mx [kNm]"; panel.Plot.YLabel = nm ? "N [kN]" : "My [kNm]";
                    panel.Plot.Note = "Sezione calcolata da Checker · azioni fuori piano escluse salvo proiezione esplicita";
                    panel.Plot.EmptyMessage = "Nessuna intersezione del dominio con il piano selezionato";
                }
                catch (ArgumentException ex) { panel.Plot.Segments = []; panel.Plot.EmptyMessage = ex.Message; }
            }
            else { panel.Plot.Segments = []; panel.Plot.EmptyMessage = calculationErrors.GetValueOrDefault(panel.Prefix + panel.Key, "Dominio in attesa di aggiornamento automatico"); }
            panel.Plot.InvalidateVisual();
        }
        UpdateSelection(panel, renderHidden);
    }
    private static double[] Project(ActionPoint p, bool nm, double value) => nm ? [p.Mx * Math.Cos(value) + p.My * Math.Sin(value), p.N] : [p.Mx, p.My];
    private void UpdateSelection(DomainPanel panel, bool renderHidden = false)
    {
        panel.NeedsVisualRefresh = true;
        if (synchronizing || panel.AttachingRows || panel.Grid is null || !renderHidden && !panel.Grid.IsVisible) return;
        panel.NeedsVisualRefresh = false;
        UpdateSectionInspection(panel);
        var row = panel.Grid.SelectedItem as JsonRow; string? id = row?.Values.S("id"); domainResults.TryGetValue(panel.Prefix + panel.Key, out var checks); DomainCheck? selectedCheck = id is null ? null : checks?.GetValueOrDefault(id);
        var visible = new List<(string Id, ActionPoint Force, bool Pass)>();
        foreach (var item in panel.Grid.Items.OfType<JsonRow>())
        {
            if (!item.Values.B("visible", true) || panel.Options.B("solo_selezionata") && !panel.Options.B("tutte_rd") && item.Values.S("id") != id) continue;
            try {
                var force = ReadAction(item);
                if (panel.ThreeD && checker3D.TryGetValue(panel.Key, out var d3)) force = CheckerSection.Point(d3.Section.Force(force));
                if (!panel.ThreeD && checker2D.TryGetValue(panel.Key, out var d2)) force = d2.LocalAction(force);
                visible.Add((item.Values.S("id"), force, checks?.GetValueOrDefault(item.Values.S("id"))?.Utilization is <= 1));
            } catch (ArgumentException) { }
        }
        if (panel.ThreeD)
        {
            panel.View3D!.ActionPointSize = panel.Options.D("dimensione_ed", 5); panel.View3D.ResistancePointSize = panel.Options.D("dimensione_rd", 5);
            panel.View3D.OnlySelectedActions = panel.Options.B("solo_selezionata"); panel.View3D.ShowActions = panel.Options.B("mostra_ed", true); panel.View3D.ShowResistance = panel.Options.B("mostra_rd", true); panel.View3D.ShowVerificationLines = panel.Options.B("mostra_linee", true); panel.View3D.ColorByRatio = panel.Options.B("colora_eta");
            panel.View3D.Ratios = checks?.ToDictionary(kv => kv.Key, kv => kv.Value.Utilization);
            panel.View3D.Resistances = panel.Options.B("tutte_rd") ? panel.Grid.Items.OfType<JsonRow>().Where(r => r.Values.B("visible", true))
                .Select(r => (Id: r.Values.S("id"), Check: checks?.GetValueOrDefault(r.Values.S("id")))).Where(r => r.Check?.Resistance is not null)
                .ToDictionary(r => r.Id, r => r.Check!.Resistance!.Value) : null;
            panel.View3D.ToolTip = UtilizationPalette.Legend;
            panel.View3D.VerificationCriterion = panel.Options.S("criterio", "N costante");
            panel.View3D.SetActions(visible, id, selectedCheck?.Resistance);
        }
        else
        {
            panel.Plot.Markers = []; panel.Plot.VerificationSegments = [];
            try
            {
                bool nm = panel.Options.S("tipo") == "N–M"; double value = SectionWorkspace.Number(panel.Options.S(nm ? "theta" : "N"), "Piano") * (nm ? Math.PI / 180 : 1);
                foreach (var action in visible)
                {
                    var p = checker2D.TryGetValue(panel.Key, out var d2) ? d2.Project(action.Force) : Project(action.Force, nm, value);
                    if (panel.Options.B("mostra_ed", true) && (!panel.Options.B("solo_selezionata") || action.Id == id)) panel.Plot.Markers.Add(new(p[0], p[1], action.Id == id ? "Ed" : "", panel.Options.B("colora_eta") ? RatioColor(checks?.GetValueOrDefault(action.Id)?.Utilization) : action.Id == id ? Ui.Brush("#E09620") : Ui.Blue, Radius: panel.Options.D("dimensione_ed", 5)));
                    if ((action.Id == id || panel.Options.B("tutte_rd")) && panel.Options.B("mostra_linee", true))
                    {
                        panel.Plot.VerificationSegments.Add((new double[] { 0, 0 }, p));
                        if (checks?.GetValueOrDefault(action.Id)?.Resistance is ActionPoint resistance)
                            panel.Plot.VerificationSegments.Add((p, d2 is not null ? d2.Project(resistance) : Project(resistance, nm, value)));
                    }
                }
                if (panel.Options.B("mostra_rd", true))
                    foreach (var item in panel.Grid.Items.OfType<JsonRow>().Where(r => r.Values.B("visible", true) && (panel.Options.B("tutte_rd") || r.Values.S("id") == id)))
                        if (checks?.GetValueOrDefault(item.Values.S("id")) is { Resistance: ActionPoint r } check)
                        { var p = checker2D.TryGetValue(panel.Key, out var d2) ? d2.Project(r) : Project(r, nm, value); panel.Plot.Markers.Add(new(p[0], p[1], item.Values.S("id") == id ? "Rd" : "", RatioColor(check.Utilization), Radius: panel.Options.D("dimensione_rd", 5))); }
                panel.Plot.ToolTip = UtilizationPalette.Legend;
            }
            catch (ArgumentException) { }
            panel.Plot.InvalidateVisual();
        }
        if (row is null) { panel.Detail.Text = "Nessuna combinazione selezionata"; return; }
        try
        {
            var a = ReadAction(row); panel.Detail.Text = $"{row.Values.S("nome")}\n\nNEd = {a.N:0.00} kN\nMx,Ed = {a.Mx:0.00} kNm\nMy,Ed = {a.My:0.00} kNm\n\n" + (selectedCheck is null ? "Da calcolare" : $"η = {selectedCheck.Utilization?.ToString("0.00") ?? "—"}\n{selectedCheck.Status}");
            panel.Detail.Text = SectionWorkspace.Label(panel.Key) + " · " + (panel.ThreeD ? panel.Options.S("criterio") + " / " + panel.Options.S("strategia") : panel.Options.S("tipo") + (panel.Options.S("proietta") == "Sì" ? " · proiezione attiva" : " · senza proiezione")) + "\n\nAZIONI · " + panel.Options.S("assi", "Locali") + "\n" + panel.Detail.Text;
            if (selectedCheck?.Resistance is ActionPoint r) panel.Detail.Text += $"\n\nRESISTENZA · assi locali\nNRd = {r.N:0.00} kN\nMx,Rd = {r.Mx:0.00} kNm\nMy,Rd = {r.My:0.00} kNm\n" + ResponseSummary(selectedCheck.Response, "STATO AL PUNTO RESISTENTE");
            var vertices = panel.ThreeD ? meshes.GetValueOrDefault(panel.Key)?.Vertices.AsEnumerable() : checker2D.GetValueOrDefault(panel.Key)?.Segments.SelectMany(s => new[] { s.A, s.B });
            if (vertices?.ToArray() is { Length: > 0 } extrema)
                panel.Detail.Text += $"\n\nESTREMI {(panel.ThreeD ? "MESH" : "CURVA")}\nN: {extrema.Min(p => p.N):0.00} … {extrema.Max(p => p.N):0.00} kN\nMx: {extrema.Min(p => p.Mx):0.00} … {extrema.Max(p => p.Mx):0.00} kNm\nMy: {extrema.Min(p => p.My):0.00} … {extrema.Max(p => p.My):0.00} kNm\nEstremi campionati; non sono resistenze al N della riga.";
            if (calculationErrors.TryGetValue(panel.Prefix + panel.Key, out var error)) panel.Detail.Text += "\n\nDa correggere: " + error;
        }
        catch (ArgumentException ex) { panel.Detail.Text = ex.Message; }
    }
    private static Brush RatioColor(double? ratio) => UtilizationPalette.Brush(ratio);
    private static string ResponseSummary(SectionResponse? r, string title)
    {
        if (r is null) return title + "\nRiepilogo nativo non disponibile";
        static string V(double? v) => EngineeringFormat.Number(v);
        string s = $"\n{title}\nσc min / max: {V(r.CMin)} / {V(r.CMax)} MPa\nσs min / max: {V(r.SMin)} / {V(r.SMax)} MPa\nεc min / max: {V(r.EcMin)} / {V(r.EcMax)} ‰\nεs min / max: {V(r.EsMin)} / {V(r.EsMax)} ‰";
        if (r.PMin is not null) s += $"\nσp min / max: {V(r.PMin)} / {V(r.PMax)} MPa\nεp min / max: {V(r.EpMin)} / {V(r.EpMax)} ‰";
        return s + $"\n\nd utile: {V(r.UsefulDepth)} mm\nDistanza asse neutro dal lembo (Checker): {V(r.NeutralDistance)} mm\nInclinazione asse neutro: {V(r.NeutralAngle)}°\n— indica dato non disponibile (es. deformazione uniforme).";
    }
    private async Task RunAnalysis(Func<CancellationToken, Task> work)
    {
        if (Busy || disposed) return; calculationQueued = false; int requested = revision; Busy = true; cancellation = new CancellationTokenSource(); var token = cancellation.Token;
        progress.Visibility = Visibility.Visible;
        try
        {
            await work(token); token.ThrowIfCancellationRequested();
            if (disposed || requested != revision) return;
            status.Text = "Aggiornamento tabelle e riepiloghi…";
            RefreshDomainViews(); RefreshSummary();
            exportResult = null;
            HasResults = domainResults.Count != 0 || stressResults.Count != 0 || shearResults.Count != 0;
            status.Text = calculationErrors.Count == 0 ? "Aggiornamento automatico completato · risultati riferiti ai dati correnti · controllare esiti e limiti di applicabilità" : "Aggiornamento parziale · " + string.Join(" · ", calculationErrors.Select(e => e.Key + ": " + e.Value));
        }
        catch (OperationCanceledException) { status.Text = "Nuove modifiche · aggiornamento in attesa…"; }
        catch (Exception ex) { status.Text = "Dati non validi · calcolo bloccato: " + ex.Message; calculationErrors["Dati"] = ex.Message; Result = null; RefreshSummary(); }
        finally
        {
            Busy = false; progress.Visibility = Visibility.Collapsed; cancellation.Dispose(); cancellation = null;
            if (requested != revision) QueueCalculation();
        }
    }
    internal async Task CalculateAllAsync()
    {
        while (Busy && !disposed) await Task.Delay(30);
        if (disposed) return;
        var requestedCalculations = pendingCalculations.ToHashSet();
        if (requestedCalculations.Count == 0) return;
        await RunAnalysis(async token =>
        {
            calculationErrors.Remove("Dati");
            var activeSteps = new HashSet<string>();
            async Task Step(string name, Func<Task> action)
            {
                token.ThrowIfCancellationRequested();
                calculationErrors.Remove(name);
                activeSteps.Add(name);
                void Progress() => status.Text = "Checker · in corso: " + string.Join(" · ", activeSteps);
                try { var task = action(); Progress(); await task; }
                catch (Exception ex) when (ex is not OperationCanceledException) { calculationErrors[name] = ex.Message; }
                finally
                {
                    // A modification during an await cancels the snapshot. Keep its
                    // unfinished work queued, together with the newly affected checks.
                    if (!token.IsCancellationRequested) pendingCalculations.Remove(name);
                    activeSteps.Remove(name); if (activeSteps.Count > 0) Progress();
                }
            }
            if (requestedCalculations.SetEquals(["Taglio"]))
            {
                await Step("Taglio", () => { CalculateShear(); return Task.CompletedTask; }); return;
            }
            var input = (JsonObject)Input.DeepClone(); var workspace = (JsonObject)settings.DeepClone();
            string sectionSignature = input.ToJsonString() + workspace["trefoli"]?.ToJsonString();
            CheckerSectionModel prepared;
            if (preparedSection is { } cached && cached.Signature == sectionSignature) prepared = cached.Model;
            else
            {
                status.Text = "Checker · preparazione unica della sezione…";
                prepared = await Task.Run(() => CheckerSection.PrepareModel(input, workspace), token);
                token.ThrowIfCancellationRequested(); preparedSection = (sectionSignature, prepared);
            }
            token.ThrowIfCancellationRequested();
            var domainTasks = from key in new[] { "SLU", "SLV" } from panel in domainPanels
                              where requestedCalculations.Contains(panel.Prefix + key)
                              select Step(panel.Prefix + key, () => CalculateDomain(panel, token, key, prepared, input, workspace));
            await Task.WhenAll(domainTasks);
            var stressTasks = SectionWorkspace.Sets.Skip(2).Where(requestedCalculations.Contains).Select(key => Step(key, () => CalculateStress(key, token, prepared, input, workspace)));
            await Task.WhenAll(stressTasks);
            token.ThrowIfCancellationRequested();
            if (requestedCalculations.Contains("Taglio")) await Step("Taglio", () => { status.Text = "Verifiche a taglio…"; CalculateShear(); return Task.CompletedTask; });
        });
    }
    private void RebuildExport()
    {
        if (domainResults.Count == 0 && stressResults.Count == 0 && shearResults.Count == 0) { Result = null; return; }
        var domains = new JsonObject(); foreach (var (key, rows) in domainResults) domains[key] = J.Node(rows);
        var stresses = new JsonObject();
        foreach (var (key, rows) in stressResults)
        {
            var exportedRows = new JsonObject();
            foreach (var (id, outcome) in rows)
            {
                var s = outcome.State;
                // Keep engineering results and report details, never native objects,
                // integration fibres, raster pixels or other reconstructible graphics.
                exportedRows[id] = J.Node(new
                {
                    State = s is null ? null : new { s.sigma_cls, s.sigma_acciaio, s.tensioni_barre,
                        s.Response, s.ConcreteStressLimit, s.SteelStressLimit, s.ConcreteVertices, s.BarStrains },
                    outcome.Ratio, outcome.Status, outcome.Cracking, outcome.CrackResult
                });
            }
            stresses[key] = exportedRows;
        }
        // Attach freshly created nodes directly: J.Obj would deep-clone them again.
        Result = new JsonObject
        {
            ["errore"] = "", ["motore"] = "GPCChecker.Concrete.dll",
            ["normativa_riferimento"] = settings.S("normativa"), ["verifica_normativa_completa"] = false,
            ["riepilogo_verifiche"] = J.Node(CollectVerificationSummaries()),
            ["domini"] = domains, ["tensioni"] = stresses, ["taglio"] = J.Node(shearResults),
            ["torsione"] = J.Node(torsionResults), ["dettagli_costruttivi"] = J.Node(detailingResults), ["dettagli_esito"] = detailingText.Text + string.Join("\n",detailingTopics.Values.Select(cards=>cards.Text)),
            ["ancoraggio"] = J.Node(anchorageResult), ["ancoraggio_esito"] = anchorageText.Text, ["momento_curvatura"] = J.Node(curvatureResult),
            ["dati"] = Data.DeepClone(), ["errori_calcolo"] = J.Node(calculationErrors),
            ["avvisi"] = J.Node(new[] { "Compressione negativa; azioni in kN e kNm", "Taglio e fessurazione: vedere esiti specifici e limiti di applicabilità" })
        };
    }
    private void RefreshSummary() => RefreshVerificationSummaries();
}
