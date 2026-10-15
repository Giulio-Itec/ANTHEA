using Anthea.Application.Concrete;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace
{
    private static readonly System.Collections.Immutable.ImmutableArray<string> CrackFields = ConcreteAnalysisSession.CrackFields;
    // SLE inputs are shared; their presentation errors must also stay identical in all three tabs.
    private readonly Dictionary<string, string> homogenizationErrors = [];
    private readonly TabControl sleTabs = new() { BorderThickness = new Thickness(0), Background = Appearance.Surface };

    private sealed class StressPanel
    {
        internal readonly ConcreteSectionViewport View = new();
        internal readonly TextBlock Detail = Ui.Text("Selezionare una combinazione", 12);
        internal readonly TextBox CrackDetail = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0), FontSize = 12, Padding = new Thickness(8) };
        internal readonly VerificationCards Summary = new();
        internal readonly JsonGrid Bars = new([new("id", "Barra", ReadOnly: true), new("stress", "σs [MPa]", ReadOnly: true), new("strain", "ε [‰]", ReadOnly: true), new("type", "Stato", ReadOnly: true)], true);
        internal readonly JsonGrid Concrete = new([new("id", "Vertice", ReadOnly: true), new("x", "x [mm]", ReadOnly: true), new("y", "y [mm]", ReadOnly: true), new("stress", "σc [MPa]", ReadOnly: true), new("strain", "εc [‰]", ReadOnly: true)], true);
        internal InputForm Options = null!;
        internal JsonGrid Grid = null!;
        internal TabItem CrackTab = null!;
        internal readonly ComboBox Regions = new();
    }
    private UIElement BuildStressTabs()
    {
        // Keep the common workspace origin; select the SLE combination in the input column.
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.Name="PART_SelectedContentHost";
        presenter.SetBinding(ContentPresenter.ContentProperty,new System.Windows.Data.Binding("SelectedContent"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
        sleTabs.Template=new ControlTemplate(typeof(TabControl)){VisualTree=presenter};
        foreach (string key in ConcreteEditor.Sets.Skip(2))
        {
            var panel = new StressPanel(); stressPanels[key] = panel; var options = settings["sle"]![key]!.AsObject();
            var nonLinearWarning = Notice("Analisi non lineare: le tensioni di esercizio sono calcolate con i legami costitutivi di progetto (valori caratteristici ridotti con αcc e γ). La norma non prescrive il metodo dell'analisi tensionale; per le verifiche SLE il riferimento abituale è l'analisi lineare a sezione fessurata. Vedere la guida (?) accanto ad «Analisi».");
            System.Windows.Automation.AutomationProperties.SetName(nonLinearWarning, "Avviso analisi non lineare");
            void EnableOptions()
            {
                nonLinearWarning.Visibility = options.S("modello") == "Non lineare" ? Visibility.Visible : Visibility.Collapsed;
                foreach (var field in new[] { "phi", "phi_trefoli", "n_armature", "n_trefoli" }) panel.Options.Enable(field, options.S("modello") == "Lineare");
                foreach (var field in new[] { "origine_x", "origine_y", "rotazione" }) panel.Options.Enable(field, options.S("assi") == "Personalizzati");
            }
            panel.Options = new InputForm(options, [new("modello", "Analisi", Choices: ["Lineare", "Non lineare"], Help: WikiContextHelp.StressAnalysis), new("n_armature", "n armature", Note: _ => homogenizationErrors.GetValueOrDefault("n_armature")), new("phi", "Viscosità φ armature", Note: _ => homogenizationErrors.GetValueOrDefault("phi")), new("n_trefoli", "n trefoli (Ep riferimento)", Note: _ => homogenizationErrors.GetValueOrDefault("n_trefoli")), new("phi_trefoli", "Viscosità φ trefoli", Note: _ => homogenizationErrors.GetValueOrDefault("phi_trefoli")), new("__ep_ref", "Ep di riferimento", "MPa", ReadOnly: true), new("trazione_cls", "CLS resistente a trazione", Choices: ["No", "Sì"]), new("assi", "Assi delle azioni", Choices: ["Locali", "Principali", "Personalizzati"]), new("origine_x", "Origine x", "mm"), new("origine_y", "Origine y", "mm"), new("rotazione", "Rotazione assi", "°"), new("esposizione", "Esposizione", Choices: ConcreteEditor.ExposureChoices.ToArray()), new("sensibilita", "Armatura", Choices: ["Poco sensibile", "Sensibile"]), new("durata", "Durata del carico", Choices: ["Lunga", "Breve"]), new("aderenza", "Barre", Choices: ["Migliorata", "Liscia"]), new("copriferro_fessure", "c barra (vuoto: auto)", "mm"), new("spaziatura_fessure", "Interasse massimo (vuoto: auto)", "mm"), new("limite_fessure", "wlim di progetto (EC2/MC)", "mm")], field =>
            {
                if (initializing) return;
                using var notifications = JsonRow.DeferNotifications(actions.Values.SelectMany(r => r));
                SynchronizeHomogenization(key, field); SynchronizeSharedSle(key); EnableOptions();
                if (CrackFields.Contains(field)) { InvalidateChecks(ConcreteEditor.Sets.Skip(2).ToArray()); return; }
                stressResults.Remove(key); foreach (var row in actions[key]) { row.Output("sigma_c", "—"); row.Output("sigma_s", "—"); row.Output("stress_status", "Da calcolare"); row.Output("eta_sigma", "—"); row.Output("wk", "Da calcolare"); }
                UpdateStressSelection(key); RefreshSummary(); InvalidateChecks(ConcreteEditor.Sets.Skip(2).ToArray());
            }, true, true);
            panel.Options.GroupFields("Analisi e omogeneizzazione", ["modello", "n_armature", "phi", "n_trefoli", "phi_trefoli", "__ep_ref"], true);
            panel.Options.GroupFields("Verifiche SLE · ambiente e armatura", ["esposizione", "sensibilita", "durata", "aderenza", "limite_fessure"], true);
            panel.Options.GroupFields("Fessurazione · disposizione delle barre", ["copriferro_fessure", "spaziatura_fessure"]);
            panel.Options.GroupFields("Avanzate · CLS teso e assi", ["trazione_cls", "assi", "origine_x", "origine_y", "rotazione"]);
            SynchronizeHomogenization(key); EnableOptions();
            var instructions = Notice("Analisi GPC Engine lineare/non lineare. Rara: limiti CLS e acciaio; quasi permanente: limite CLS. Frequente: nessun limite tensionale automatico. φ è il coefficiente di viscosità. Fessurazione: NTC/UNI frequente e quasi permanente; altri EC2 quasi permanente, salvo NS XD3/XS3 frequente. Model Code richiede wlim di progetto. Il limite assegnato sostituisce quello EC2 (tranne UNI) e va motivato nel progetto. L’apertura richiede analisi lineare con CLS teso escluso.");
            var combinationChoice=Ui.Choice(ConcreteEditor.Sets.Skip(2).Select(ConcreteEditor.Label).ToArray(),ConcreteEditor.Label(key));
            RevisionInspection.Allow(combinationChoice); RevisionInspection.Allow(panel.Regions);
            combinationChoice.SelectionChanged+=(_,_)=>{if(combinationChoice.SelectedIndex>=0)sleTabs.SelectedIndex=combinationChoice.SelectedIndex;};
            sleTabs.SelectionChanged+=(_,e)=>{if(e.Source==sleTabs && sleTabs.SelectedIndex>=0)combinationChoice.SelectedIndex=sleTabs.SelectedIndex;};
            var optionsPanel = Panel("Opzioni SLE comuni", Scroller(Ui.Stack(Ui.Text("Combinazione SLE",12,true),combinationChoice,panel.Options, Ui.Button("Reologia del calcestruzzo…", () => ConcreteTimeEffectsView.CreateWindow(this, Math.Abs(Input.D("fck_mpa"))).Show(), inspection: true), nonLinearWarning, instructions)), "Modifiche valide per Rara, Frequente e Quasi permanente. Azioni separate, già combinate.");
            var viewport = new ViewportFrame("Mappa tensionale della sezione", panel.View, panel.View.ResetView);
            panel.Regions.SelectionChanged += (_,_)=>{panel.View.EffectiveRegion=panel.Regions.SelectedItem as ConcreteResults.EffectiveRegion;panel.View.InvalidateVisual();};
            panel.Regions.DisplayMemberPath="Name";
            panel.Regions.ToolTip="Zona efficace per la fessurazione · barre incluse evidenziate in arancione";
            viewport.Toolbar.Children.Add(panel.Regions);
            var contour = Ui.Choice(ConcreteSectionViewport.Contours, options.S("contour", ConcreteSectionViewport.Contours[0])); contour.Width = 205;
            RevisionInspection.Allow(contour);
            panel.View.Contour = contour.SelectedItem as string ?? ConcreteSectionViewport.Contours[0];
            options["contour"] = panel.View.Contour;
            contour.SelectionChanged += (_, _) => { if (contour.SelectedItem is not string selected) return; options["contour"] = selected; panel.View.Contour = selected; panel.View.InvalidateVisual(); Modified?.Invoke(); };
            viewport.Toolbar.Children.Insert(0, contour);
            viewport.Toolbar.Children.Add(NeutralAxisToggle(panel.View, options));
            foreach (var (field, label) in new[] { ("testi_barre", "σ/ε barre"), ("testi_trefoli", "σ/ε trefoli"), ("testi_cls", "σ/ε vertici CLS") })
            {
                var toggle = new CheckBox { Content = label, IsChecked = options.B(field), Margin = new Thickness(5, 3, 5, 3) };
                if (field == "testi_trefoli") tendonOnlyControls.Add(toggle);
                void ShowValues() { panel.View.BarValues = options.B("testi_barre"); panel.View.TendonValues = options.B("testi_trefoli"); panel.View.ConcreteValues = options.B("testi_cls"); panel.View.InvalidateVisual(); }
                toggle.Click += (_, _) => { options[field] = toggle.IsChecked == true; ShowValues(); Modified?.Invoke(); }; ShowValues(); viewport.Toolbar.Children.Add(toggle);
            }
            var details = new TabControl { BorderThickness = new Thickness(0), Background = Appearance.Paper };
            Ui.Tab(details, "Riepilogo", Scroller(panel.Detail));
            Ui.Tab(details, "Barre e trefoli", WithFilters(panel.Bars));
            Ui.Tab(details, "Calcestruzzo", WithFilters(panel.Concrete));
            panel.CrackTab = Ui.Tab(details, "Fessurazione · passaggi", panel.CrackDetail);
            panel.View.BarSelected += index => { if (index < panel.Bars.Rows.Count) { details.SelectedIndex = 1; panel.Bars.SelectedIndex = index; panel.Bars.ScrollIntoView(panel.Bars.SelectedItem); } };
            panel.Bars.SelectionChanged += (_, _) => { panel.View.SelectedBar = (panel.Bars.SelectedItem as JsonRow)?.Values.S("id") ?? ""; panel.View.InvalidateVisual(); };
            var verificationTabs = new TabControl { SelectedIndex = 1 }; Ui.Tab(verificationTabs, "Dettagli combinazione", details); Ui.Tab(verificationTabs, "Riepilogo verifiche", Scroller(panel.Summary));
            panel.Grid = new JsonGrid([new("nome", "Combinazione"), new("N", "N [kN]"), new("Mx", "Mx [kNm]"), new("My", "My [kNm]"), new("sigma_c", "σc [MPa]", ReadOnly: true), new("sigma_s", "|σs| [MPa]", ReadOnly: true), new("eta_sigma", "ησ [-]", ReadOnly: true), new("stress_status", "Tensioni", ReadOnly: true), new("wk", "Fessurazione", ReadOnly: true)], true, actions[key]);
            panel.Grid.Columns[^2].Width = new DataGridLength(1.8, DataGridLengthUnitType.Star); panel.Grid.Columns[^1].Width = new DataGridLength(1.4, DataGridLengthUnitType.Star);
            panel.Grid.Columns[0].MinWidth = 130; panel.Grid.Columns[^2].MinWidth = 145; panel.Grid.Columns[^1].MinWidth = 125;
            var resultStyle = new Style(typeof(TextBlock)); resultStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            panel.Grid.RowHeight = double.NaN; panel.Grid.MinRowHeight = 30;
            foreach (var column in panel.Grid.Columns.TakeLast(2).Cast<DataGridTextColumn>()) column.ElementStyle = resultStyle;
            var table = Panel("Combinazioni · " + ConcreteEditor.Label(key), ActionTable(key, panel.Grid, () => UpdateStressSelection(key)));
            var body = AnalysisLayout(optionsPanel, viewport, verificationTabs, table);
            Ui.Tab(sleTabs, ConcreteEditor.Label(key), body);
            panel.View.IsVisibleChanged += (_, _) => { if (panel.View.IsVisible) UpdateStressSelection(key); };
            if (actions[key].Count > 0) panel.Grid.SelectedIndex = 0;
        }
        sleTabs.SelectedIndex=0;
        return sleTabs;
    }
    private void PublishStress(string key, Dictionary<string, StressOutcome> outcomes)
    {
        stressResults[key] = outcomes;
        using var notifications = JsonRow.DeferNotifications(actions[key]);
        foreach (var row in actions[key])
        {
            var outcome = outcomes[row.Values.S("id")]; row.Output("sigma_c", EngineeringFormat.Number(outcome.State?.sigma_cls)); row.Output("sigma_s", EngineeringFormat.Number(outcome.State?.sigma_acciaio)); row.Output("eta_sigma", EngineeringFormat.Number(outcome.Ratio)); row.Output("stress_status", outcome.Status); row.Output("wk", outcome.CrackResult?.Width is double width ? $"{EngineeringFormat.Number(width)} / {EngineeringFormat.Number(outcome.CrackResult.Limit)} mm · η={EngineeringFormat.Number(outcome.CrackResult.Ratio)}" : outcome.Cracking);
        }
        UpdateStressSelection(key);
    }
    private void UpdateStressSelection(string key)
    {
        if (synchronizing || !stressPanels.TryGetValue(key, out var panel) || panel.Grid is null) return;
        bool stressRequired = ConcreteEditor.HasStressCheck(key), crackRequired = ConcreteEditor.HasCrackCheck(key, settings);
        panel.Grid.Columns[6].Visibility = panel.Grid.Columns[7].Visibility = stressRequired ? Visibility.Visible : Visibility.Collapsed;
        panel.Grid.Columns[8].Visibility = panel.CrackTab.Visibility = crackRequired ? Visibility.Visible : Visibility.Collapsed;
        if (!panel.View.IsVisible) return;
        using var barsRefresh = panel.Bars.Rows.DeferRefresh();
        using var concreteRefresh = panel.Concrete.Rows.DeferRefresh();
        var row = panel.Grid.SelectedItem as JsonRow; var outcome = row is null ? null : stressResults.GetValueOrDefault(key)?.GetValueOrDefault(row.Values.S("id"));
        panel.CrackDetail.Text = (row is null ? "" : ConcreteEditor.Label(key) + " · " + row.Values.S("nome") + "\n\n") + ConcreteEditor.CrackSummary(outcome?.CrackResult, outcome?.Cracking ?? "Selezionare una combinazione calcolata.");
        if(outcome?.CrackResult is { } crackTrace && crackTrace.Regions.Length>0)
            panel.CrackDetail.Text+="\n\nZONE EFFICACI\n"+string.Join("\n",crackTrace.Regions.Select(r=>$"{r.Name}: Ac,eff={r.Area:0.###} mm²; As,eff={r.SteelArea:0.###} mm²; wk={r.Width:0.######} mm · "+string.Join(", ",r.BarIndices.Select(i=>$"B{i+1:00}"))))+"\n\nPASSAGGI COMPLETI\n"+string.Join("\n\n",crackTrace.Details.Select(d=>d.Format()));
        panel.View.Stress = outcome?.State;
        panel.Regions.ItemsSource=outcome?.CrackResult?.Regions??[];
        panel.Regions.SelectedIndex=panel.Regions.Items.Count>0?0:-1;
        panel.View.EffectiveRegion=panel.Regions.SelectedItem as ConcreteResults.EffectiveRegion;
        panel.View.InvalidateVisual(); panel.Bars.Rows.Clear(); panel.Concrete.Rows.Clear();
        if (outcome?.State is ConcreteResults.StressState state)
        {
            panel.Detail.Text = $"{row!.Values.S("nome")}\n\nσc,min = {state.sigma_cls:0.00} MPa\n|σs|max = {state.sigma_acciaio:0.00} MPa\nησ = {outcome.Ratio?.ToString("0.00") ?? "—"}\n\n{outcome.Status}\n\n{outcome.Cracking}\nwk = {outcome.CrackResult?.Width?.ToString("0.00") ?? "—"} mm\nLimite = {outcome.CrackResult?.Limit?.ToString("0.00") ?? "—"} mm\nηw = {outcome.CrackResult?.Ratio?.ToString("0.00") ?? "—"}";
            var options = settings["sle"]![key]!; var force = ReadAction(row);
            string limits = state.ConcreteStressLimit is double limit ? $"|σc,comp| ≤ {limit:0.00} MPa" : "Nessun limite tensionale automatico";
            if (key == "SLE") limits += $"\nσs ≤ {state.SteelStressLimit:0.00} MPa (armatura ordinaria)";
            panel.Detail.Text = $"{ConcreteEditor.Label(key)} · {options.S("modello")}\n{row.Values.S("nome")} · assi {options.S("assi")}\nN = {force.N:0.00} kN\nMx / My = {force.Mx:0.00} / {force.My:0.00} kNm\n\nTENSIONI · NTC 2018\n{limits}\nησ = {outcome.Ratio?.ToString("0.00") ?? "—"}\n{outcome.Status}\n" + ResponseSummary(state.Response, "STATO ALL’AZIONE APPLICATA") +
                $"\n\nFESSURAZIONE\n{options.S("esposizione")} · armatura {options.S("sensibilita").ToLowerInvariant()}\nCarico di durata {options.S("durata").ToLowerInvariant()}\n{outcome.Cracking}\nwk / limite = {outcome.CrackResult?.Width?.ToString("0.00") ?? "—"} / {outcome.CrackResult?.Limit?.ToString("0.00") ?? "—"} mm\nηw = {outcome.CrackResult?.Ratio?.ToString("0.00") ?? "—"}\nAc,eff = {outcome.CrackResult?.EffectiveArea?.ToString("0.00") ?? "—"} mm²\nAs,eff = {outcome.CrackResult?.EffectiveSteel?.ToString("0.00") ?? "—"} mm²";
            panel.Detail.Text += "\nCoefficienti e formule: scheda «Fessurazione · passaggi» (testo selezionabile e copiabile).";
            panel.Detail.Text += $"\nInterasse barre tese = {EngineeringFormat.Number(outcome.CrackResult?.BarSpacing)} mm · {outcome.CrackResult?.SpacingSource}";
            if (!crackRequired) panel.Detail.Text = panel.Detail.Text.Split("\n\nFESSURAZIONE")[0];
            if (!stressRequired)
            {
                int start = panel.Detail.Text.IndexOf("\n\nTENSIONI", StringComparison.Ordinal), end = panel.Detail.Text.IndexOf("\nSTATO ALL’AZIONE APPLICATA", StringComparison.Ordinal);
                if (start >= 0 && end > start) panel.Detail.Text = panel.Detail.Text[..start] + "\n\nTensioni calcolate a supporto della fessurazione; verifica tensionale non richiesta.\n" + panel.Detail.Text[end..];
            }
            int ordinary = panel.View.Section?.Bars.Count ?? 0;
            panel.Detail.Text = panel.Detail.Text.Replace("TENSIONI · NTC 2018", "TENSIONI · " + settings.S("normativa"));
            panel.Detail.Text += $"\n\nOMOGENEIZZAZIONE\nn armature = {EngineeringFormat.Number(options.D("n_armature"))} · φ = {EngineeringFormat.Number(options.D("phi"))}";
            if (tendons.Rows.Count > 0) panel.Detail.Text += $"\nn trefoli = {EngineeringFormat.Number(options.D("n_trefoli"))} · φp = {EngineeringFormat.Number(options.D("phi_trefoli"))}\nCon Ep diversi, n trefoli è riferito al primo materiale; φp è comune.";
            panel.Detail.Text += "\nn = Eacciaio (1 + φ) / Ec. Deformazioni incrementali da GPC Engine.";
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
        foreach (var update in Anthea.Application.Concrete.ConcreteSheetPreparation.SynchronizeHomogenization(Input, settings, key, changed))
        {
            homogenizationErrors.Remove(update.NKey); homogenizationErrors.Remove(update.PhiKey);
            panel.Options.Set(update.Target, update.Value is double value ? EngineeringFormat.Number(value) : "", true);
            if (update.Modulus is double modulus) panel.Options.Set("__ep_ref", EngineeringFormat.Number(modulus), true);
            if (update.Error is not null) homogenizationErrors[update.Source] = update.Error;
            panel.Options.UpdateNote(update.NKey); panel.Options.UpdateNote(update.PhiKey);
        }
    }
}
