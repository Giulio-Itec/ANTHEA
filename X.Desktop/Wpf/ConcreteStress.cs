using System.Text.Json.Nodes;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace
{
    private readonly ConcurrentDictionary<string, (string Signature, CheckerSection Engine, CheckerStressState State)> stressCache = new();
    private static readonly string[] CrackFields = ["esposizione", "sensibilita", "durata", "aderenza", "copriferro_fessure", "spaziatura_fessure"];
    private readonly TabControl sleTabs = new() { BorderThickness = new Thickness(0), Background = Ui.Bg };
    internal sealed record StressOutcome(CheckerStressState? State, double? Ratio, string Status, string Cracking = "Da calcolare", Ntc2018Checks.CrackResult? CrackResult = null);
    private sealed class StressPanel
    {
        internal readonly ConcreteSectionViewport View = new();
        internal readonly TextBlock Detail = Ui.Text("Selezionare una combinazione", 12);
        internal readonly TextBox CrackDetail = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0), FontSize = 12, Padding = new Thickness(8) };
        internal readonly TextBlock Summary = Ui.Text("Verifiche da calcolare", 12);
        internal readonly JsonGrid Bars = new([new("id", "Barra", ReadOnly: true), new("stress", "σs [MPa]", ReadOnly: true), new("strain", "ε [‰]", ReadOnly: true), new("type", "Stato", ReadOnly: true)], true);
        internal readonly JsonGrid Concrete = new([new("id", "Vertice", ReadOnly: true), new("x", "x [mm]", ReadOnly: true), new("y", "y [mm]", ReadOnly: true), new("stress", "σc [MPa]", ReadOnly: true), new("strain", "εc [‰]", ReadOnly: true)], true);
        internal InputForm Options = null!;
        internal JsonGrid Grid = null!;
    }
    private UIElement BuildStressTabs()
    {
        foreach (string key in SectionWorkspace.Sets.Skip(2))
        {
            var panel = new StressPanel(); stressPanels[key] = panel; var options = settings["sle"]![key]!.AsObject();
            void EnableOptions()
            {
                foreach (var field in new[] { "phi", "phi_trefoli", "n_armature", "n_trefoli" }) panel.Options.Enable(field, options.S("modello") == "Lineare");
                foreach (var field in new[] { "origine_x", "origine_y", "rotazione" }) panel.Options.Enable(field, options.S("assi") == "Personalizzati");
            }
            panel.Options = new InputForm(options, [new("modello", "Analisi", Choices: ["Lineare", "Non lineare"]), new("n_armature", "n armature"), new("phi", "Viscosità φ armature"), new("n_trefoli", "n trefoli (Ep riferimento)"), new("phi_trefoli", "Viscosità φ trefoli"), new("__ep_ref", "Ep di riferimento", "MPa", ReadOnly: true), new("trazione_cls", "CLS resistente a trazione", Choices: ["No", "Sì"]), new("assi", "Assi delle azioni", Choices: ["Locali", "Principali", "Personalizzati"]), new("origine_x", "Origine x", "mm"), new("origine_y", "Origine y", "mm"), new("rotazione", "Rotazione assi", "°"), new("esposizione", "Esposizione", Choices: Ntc2018Checks.Exposures), new("sensibilita", "Armatura", Choices: ["Poco sensibile", "Sensibile"]), new("durata", "Durata del carico", Choices: ["Lunga", "Breve"]), new("aderenza", "Barre", Choices: ["Migliorata", "Liscia"]), new("copriferro_fessure", "c barra (vuoto: auto)", "mm"), new("spaziatura_fessure", "Interasse massimo (vuoto: auto)", "mm")], field =>
            {
                if (initializing) return;
                using var notifications = JsonRow.DeferNotifications(actions.Values.SelectMany(r => r));
                SynchronizeHomogenization(key, field); SynchronizeSharedSle(key); EnableOptions();
                if (CrackFields.Contains(field)) { InvalidateChecks(SectionWorkspace.Sets.Skip(2).ToArray()); return; }
                stressResults.Remove(key); foreach (var row in actions[key]) { row.Output("sigma_c", "—"); row.Output("sigma_s", "—"); row.Output("stress_status", "Da calcolare"); row.Output("eta_sigma", "—"); row.Output("wk", "Da calcolare"); }
                UpdateStressSelection(key); RefreshSummary(); InvalidateChecks(SectionWorkspace.Sets.Skip(2).ToArray());
            }, true, true);
            panel.Options.GroupFields("Analisi e omogeneizzazione", ["modello", "n_armature", "phi", "n_trefoli", "phi_trefoli", "__ep_ref"], true);
            panel.Options.GroupFields("Verifiche SLE · ambiente e armatura", ["esposizione", "sensibilita", "durata", "aderenza"], true);
            panel.Options.GroupFields("Fessurazione · disposizione delle barre", ["copriferro_fessure", "spaziatura_fessure"]);
            panel.Options.GroupFields("Avanzate · CLS teso e assi", ["trazione_cls", "assi", "origine_x", "origine_y", "rotazione"]);
            SynchronizeHomogenization(key); EnableOptions();
            var instructions = Notice("Analisi Checker lineare/non lineare. Rara: limiti CLS e acciaio; quasi permanente: limite CLS. Frequente: tensioni calcolate, nessun limite tensionale automatico. φ è il coefficiente di viscosità.");
            var optionsPanel = Panel("Opzioni SLE comuni", Scroller(Ui.Stack(panel.Options, instructions)), "Modifiche valide per Rara, Frequente e Quasi permanente. Azioni separate, già combinate.");
            var viewport = new ViewportFrame("Mappa tensionale della sezione", panel.View, panel.View.ResetView);
            var contour = Ui.Choice(ConcreteSectionViewport.Contours, options.S("contour", ConcreteSectionViewport.Contours[0])); contour.Width = 205;
            panel.View.Contour = contour.SelectedItem as string ?? ConcreteSectionViewport.Contours[0];
            options["contour"] = panel.View.Contour;
            contour.SelectionChanged += (_, _) => { if (contour.SelectedItem is not string selected) return; options["contour"] = selected; panel.View.Contour = selected; panel.View.InvalidateVisual(); Modified?.Invoke(); };
            viewport.Toolbar.Children.Insert(0, contour);
            foreach (var (field, label) in new[] { ("testi_barre", "σ/ε barre"), ("testi_trefoli", "σ/ε trefoli"), ("testi_cls", "σ/ε vertici CLS") })
            {
                var toggle = new CheckBox { Content = label, IsChecked = options.B(field), Margin = new Thickness(5, 3, 5, 3) };
                if (field == "testi_trefoli") tendonOnlyControls.Add(toggle);
                void ShowValues() { panel.View.BarValues = options.B("testi_barre"); panel.View.TendonValues = options.B("testi_trefoli"); panel.View.ConcreteValues = options.B("testi_cls"); panel.View.InvalidateVisual(); }
                toggle.Click += (_, _) => { options[field] = toggle.IsChecked == true; ShowValues(); Modified?.Invoke(); }; ShowValues(); viewport.Toolbar.Children.Add(toggle);
            }
            var details = new TabControl { BorderThickness = new Thickness(0), Background = Brushes.White };
            Ui.Tab(details, "Riepilogo", Scroller(panel.Detail));
            Ui.Tab(details, "Barre e trefoli", WithFilters(panel.Bars));
            Ui.Tab(details, "Calcestruzzo", WithFilters(panel.Concrete));
            Ui.Tab(details, "Fessurazione · passaggi", panel.CrackDetail);
            panel.View.BarSelected += index => { if (index < panel.Bars.Rows.Count) { details.SelectedIndex = 1; panel.Bars.SelectedIndex = index; panel.Bars.ScrollIntoView(panel.Bars.SelectedItem); } };
            panel.Bars.SelectionChanged += (_, _) => { panel.View.SelectedBar = (panel.Bars.SelectedItem as JsonRow)?.Values.S("id") ?? ""; panel.View.InvalidateVisual(); };
            var verificationTabs = new TabControl(); Ui.Tab(verificationTabs, "Dettagli combinazione", details); Ui.Tab(verificationTabs, "Riepilogo verifiche", Scroller(panel.Summary));
            var upper = Columns((viewport, 6, 310), (verificationTabs, 4, 240));
            panel.Grid = new JsonGrid([new("nome", "Combinazione"), new("N", "N [kN]"), new("Mx", "Mx [kNm]"), new("My", "My [kNm]"), new("sigma_c", "σc [MPa]", ReadOnly: true), new("sigma_s", "|σs| [MPa]", ReadOnly: true), new("eta_sigma", "ησ [-]", ReadOnly: true), new("stress_status", "Tensioni", ReadOnly: true), new("wk", "Fessurazione", ReadOnly: true)], true, actions[key]);
            panel.Grid.Columns[^2].Width = new DataGridLength(1.8, DataGridLengthUnitType.Star); panel.Grid.Columns[^1].Width = new DataGridLength(1.4, DataGridLengthUnitType.Star);
            panel.Grid.Columns[0].MinWidth = 130; panel.Grid.Columns[^2].MinWidth = 145; panel.Grid.Columns[^1].MinWidth = 125;
            var resultStyle = new Style(typeof(TextBlock)); resultStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            panel.Grid.RowHeight = double.NaN; panel.Grid.MinRowHeight = 30;
            foreach (var column in panel.Grid.Columns.TakeLast(2).Cast<DataGridTextColumn>()) column.ElementStyle = resultStyle;
            var table = Panel("Combinazioni · " + SectionWorkspace.Label(key), ActionTable(key, panel.Grid, () => UpdateStressSelection(key)));
            var body = Columns((optionsPanel, 2.6, 260), (Rows(upper, table, 3.5, 2), 7.4, 680)); body.Margin = new Thickness(0, 8, 0, 0);
            Ui.Tab(sleTabs, SectionWorkspace.Label(key), body);
            panel.View.IsVisibleChanged += (_, _) => { if (panel.View.IsVisible) UpdateStressSelection(key); };
            if (actions[key].Count > 0) panel.Grid.SelectedIndex = 0;
        }
        var header = Ui.Text("SLE · tensioni e fessurazione", 17, true); header.Margin = new Thickness(4, 12, 4, 10);
        return Ui.Dock(sleTabs, header);
    }
    private async Task CalculateStress(string key, CancellationToken token, CheckerSectionModel? prepared = null, JsonObject? preparedInput = null, JsonObject? preparedWorkspace = null)
    {
        status.Text = "Analisi tensionale · " + SectionWorkspace.Label(key) + "…"; var settingsSle = settings["sle"]![key]!;
        var input = preparedInput ?? (JsonObject)Input.DeepClone(); var workspace = preparedWorkspace ?? (JsonObject)settings.DeepClone(); var options = (JsonObject)settingsSle.DeepClone();
        var requests = actions[key].Select(row => (Id: row.Values.S("id"), Values: (JsonObject)row.Values.DeepClone())).ToArray();
        var activeIds = requests.Select(r => key + ":" + r.Id).ToHashSet();
        foreach (var id in stressCache.Keys.Where(id => id.StartsWith(key + ":") && !activeIds.Contains(id))) stressCache.TryRemove(id, out _);
        var analysisOptions = (JsonObject)options.DeepClone();
        foreach (var field in CrackFields.Concat(new[] { "contour", "testi_barre", "testi_trefoli", "testi_cls" })) analysisOptions.Remove(field);
        string analysisSignature = input.ToJsonString() + workspace.S("normativa") + workspace["coefficienti"]?.ToJsonString() + workspace["trefoli"]?.ToJsonString() + analysisOptions.ToJsonString();
        var outcomes = await Task.Run(() =>
        {
            var results = new ConcurrentDictionary<string, StressOutcome>(); if (requests.Length == 0) return new Dictionary<string, StressOutcome>();
            Parallel.ForEach(requests, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = Math.Max(1, Math.Min(2, Environment.ProcessorCount / 3)) },
                () => (CheckerSection?)null, (request, loop, workerEngine) =>
            {
                token.ThrowIfCancellationRequested();
                try {
                    var force = ReadAction(new JsonRow(request.Values));
                    string cacheKey = key + ":" + request.Id, signature = analysisSignature + J.Node(force)?.ToJsonString();
                    if (!stressCache.TryGetValue(cacheKey, out var cached) || cached.Signature != signature)
                    {
                        workerEngine ??= prepared is null ? new CheckerSection(input, workspace, options) : new CheckerSection(prepared, input, workspace, options);
                        var calculated = workerEngine.Stress(force, key); token.ThrowIfCancellationRequested();
                        cached = (signature, workerEngine, calculated); stressCache[cacheKey] = cached;
                    }
                    var engine = cached.Engine; var state = cached.State;
                    Ntc2018Checks.CrackResult crack;
                    try { crack = workspace.S("normativa") == "NTC 2018" ? Ntc2018Checks.Cracking(engine, state, force, input, workspace, options, key) : new(null, null, null, null, "Fessurazione specifica " + workspace.S("normativa") + ": da implementare"); }
                    catch (Exception ex) { crack = new(null, null, null, null, "Fessurazione non calcolata: " + ex.Message); }
                    results[request.Id] = new(state, state.Ratio, state.Status, crack.Status, crack);
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { results[request.Id] = new(null, null, "Checker: " + ex.Message); }
                return workerEngine;
            }, _ => { });
            return results.ToDictionary(p => p.Key, p => p.Value);
        }, token);
        token.ThrowIfCancellationRequested(); stressResults[key] = outcomes;
        using var notifications = JsonRow.DeferNotifications(actions[key]);
        foreach (var row in actions[key])
        {
            var outcome = outcomes[row.Values.S("id")]; row.Output("sigma_c", EngineeringFormat.Number(outcome.State?.sigma_cls)); row.Output("sigma_s", EngineeringFormat.Number(outcome.State?.sigma_acciaio)); row.Output("eta_sigma", EngineeringFormat.Number(outcome.Ratio)); row.Output("stress_status", outcome.Status); row.Output("wk", outcome.CrackResult?.Width is double width ? $"{EngineeringFormat.Number(width)} / {EngineeringFormat.Number(outcome.CrackResult.Limit)} mm · η={EngineeringFormat.Number(outcome.CrackResult.Ratio)}" : outcome.Cracking);
        }
        UpdateStressSelection(key);
    }
    private void UpdateStressSelection(string key)
    {
        if (synchronizing || !stressPanels.TryGetValue(key, out var panel) || panel.Grid is null || !panel.View.IsVisible) return;
        using var barsRefresh = panel.Bars.Rows.DeferRefresh();
        using var concreteRefresh = panel.Concrete.Rows.DeferRefresh();
        var row = panel.Grid.SelectedItem as JsonRow; var outcome = row is null ? null : stressResults.GetValueOrDefault(key)?.GetValueOrDefault(row.Values.S("id"));
        panel.CrackDetail.Text = (row is null ? "" : SectionWorkspace.Label(key) + " · " + row.Values.S("nome") + "\n\n") + CrackCalculationSummary.Format(outcome?.CrackResult, outcome?.Cracking ?? "Selezionare una combinazione calcolata.");
        panel.View.Stress = outcome?.State; panel.View.InvalidateVisual(); panel.Bars.Rows.Clear(); panel.Concrete.Rows.Clear();
        if (outcome?.State is CheckerStressState state)
        {
            panel.Detail.Text = $"{row!.Values.S("nome")}\n\nσc,min = {state.sigma_cls:0.00} MPa\n|σs|max = {state.sigma_acciaio:0.00} MPa\nησ = {outcome.Ratio?.ToString("0.00") ?? "—"}\n\n{outcome.Status}\n\n{outcome.Cracking}\nwk = {outcome.CrackResult?.Width?.ToString("0.00") ?? "—"} mm\nLimite = {outcome.CrackResult?.Limit?.ToString("0.00") ?? "—"} mm\nηw = {outcome.CrackResult?.Ratio?.ToString("0.00") ?? "—"}";
            var options = settings["sle"]![key]!; var force = ReadAction(row);
            string limits = state.ConcreteStressLimit is double limit ? $"|σc,comp| ≤ {limit:0.00} MPa" : "Nessun limite tensionale automatico";
            if (key == "SLE") limits += $"\nσs ≤ {state.SteelStressLimit:0.00} MPa (armatura ordinaria)";
            panel.Detail.Text = $"{SectionWorkspace.Label(key)} · {options.S("modello")}\n{row.Values.S("nome")} · assi {options.S("assi")}\nN = {force.N:0.00} kN\nMx / My = {force.Mx:0.00} / {force.My:0.00} kNm\n\nTENSIONI · NTC 2018\n{limits}\nησ = {outcome.Ratio?.ToString("0.00") ?? "—"}\n{outcome.Status}\n" + ResponseSummary(state.Response, "STATO ALL’AZIONE APPLICATA") +
                $"\n\nFESSURAZIONE\n{options.S("esposizione")} · armatura {options.S("sensibilita").ToLowerInvariant()}\nCarico di durata {options.S("durata").ToLowerInvariant()}\n{outcome.Cracking}\nwk / limite = {outcome.CrackResult?.Width?.ToString("0.00") ?? "—"} / {outcome.CrackResult?.Limit?.ToString("0.00") ?? "—"} mm\nηw = {outcome.CrackResult?.Ratio?.ToString("0.00") ?? "—"}\nAc,eff = {outcome.CrackResult?.EffectiveArea?.ToString("0.00") ?? "—"} mm²\nAs,eff = {outcome.CrackResult?.EffectiveSteel?.ToString("0.00") ?? "—"} mm²";
            panel.Detail.Text += "\nCoefficienti e formule: scheda «Fessurazione · passaggi» (testo selezionabile e copiabile).";
            panel.Detail.Text += $"\nInterasse barre tese = {EngineeringFormat.Number(outcome.CrackResult?.BarSpacing)} mm · {outcome.CrackResult?.SpacingSource}";
            int ordinary = panel.View.Section?.Bars.Count ?? 0;
            panel.Detail.Text = panel.Detail.Text.Replace("TENSIONI · NTC 2018", "TENSIONI · " + settings.S("normativa"));
            panel.Detail.Text += $"\n\nOMOGENEIZZAZIONE\nn armature = {EngineeringFormat.Number(options.D("n_armature"))} · φ = {EngineeringFormat.Number(options.D("phi"))}";
            if (tendons.Rows.Count > 0) panel.Detail.Text += $"\nn trefoli = {EngineeringFormat.Number(options.D("n_trefoli"))} · φp = {EngineeringFormat.Number(options.D("phi_trefoli"))}\nCon Ep diversi, n trefoli è riferito al primo materiale; φp è comune.";
            panel.Detail.Text += "\nn = Eacciaio (1 + φ) / Ec. Deformazioni incrementali dalle API Checker.";
            for (int i = 0; i < state.tensioni_barre.Length; i++)
            {
                double stress = state.tensioni_barre[i]; string id = i < ordinary ? "B" + (i + 1).ToString("D2") : settings.Array("trefoli").ElementAtOrDefault(i - ordinary).S("id", "T" + (i - ordinary + 1));
                panel.Bars.Rows.Add(new JsonRow(J.Obj(("id", id), ("stress", EngineeringFormat.Number(stress)), ("strain", EngineeringFormat.Number(state.BarStrains.ElementAtOrDefault(i))), ("type", stress > 0 ? "Trazione" : stress < 0 ? "Compressione" : "Nullo"))));
            }
            foreach (var p in state.ConcreteVertices) panel.Concrete.Rows.Add(new JsonRow(J.Obj(("id", p.Id), ("x", EngineeringFormat.Number(p.X)), ("y", EngineeringFormat.Number(p.Y)), ("stress", EngineeringFormat.Number(p.Stress)), ("strain", EngineeringFormat.Number(p.Strain)))));
        }
        else panel.Detail.Text = outcome?.Status ?? (row is null ? "Nessuna combinazione selezionata" : "Tensioni da calcolare");
    }
    private void SynchronizeHomogenization(string key, string? changed = null)
    {
        if (!stressPanels.TryGetValue(key, out var panel) || panel.Options is null) return;
        var options = settings["sle"]![key]!;
        try
        {
            double ec = ConcreteMaterials.Concrete(Input).E, es = Input.Required("steel_modulus_mpa", strict: true);
            double ep = settings.Array("trefoli").FirstOrDefault()?.D("Ep", 195000) ?? settings["materiale_trefolo"].D("Ep", 195000);
            foreach (var (nKey, phiKey, modulus) in new[] { ("n_armature", "phi", es), ("n_trefoli", "phi_trefoli", ep) })
            {
                if (changed == nKey)
                {
                    double n = SectionWorkspace.Number(options.S(nKey), nKey), phi = n * ec / modulus - 1;
                    options[phiKey] = phi.ToString("G17", System.Globalization.CultureInfo.InvariantCulture); panel.Options.Set(phiKey, EngineeringFormat.Number(phi), true);
                }
                else
                {
                    double n = modulus * (1 + SectionWorkspace.Number(options.S(phiKey, "0"), phiKey)) / ec;
                    options[nKey] = n.ToString("G17", System.Globalization.CultureInfo.InvariantCulture); panel.Options.Set(nKey, EngineeringFormat.Number(n), true);
                }
            }
            panel.Options.Set("__ep_ref", EngineeringFormat.Number(ep), true);
        }
        catch (ArgumentException) { if (changed is "n_armature" or "n_trefoli") { string phiKey = changed == "n_armature" ? "phi" : "phi_trefoli"; options[phiKey] = ""; panel.Options.Set(phiKey, "", true); } }
    }
}
