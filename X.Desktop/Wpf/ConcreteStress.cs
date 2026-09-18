using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace
{
    private readonly TabControl sleTabs = new() { BorderThickness = new Thickness(0), Background = Ui.Bg };
    internal sealed record StressOutcome(CheckerStressState? State, double? Ratio, string Status, string Cracking = "Da calcolare", Ntc2018Checks.CrackResult? CrackResult = null);
    private sealed class StressPanel
    {
        internal readonly ConcreteSectionViewport View = new();
        internal readonly TextBlock Detail = Ui.Text("Selezionare una combinazione", 12);
        internal readonly JsonGrid Bars = new([new("id", "Barra", ReadOnly: true), new("stress", "σs [MPa]", ReadOnly: true), new("type", "Stato", ReadOnly: true)], true);
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
                foreach (var field in new[] { "phi", "phi_trefoli" }) panel.Options.Enable(field, options.S("modello") == "Lineare");
                foreach (var field in new[] { "origine_x", "origine_y", "rotazione" }) panel.Options.Enable(field, options.S("assi") == "Personalizzati");
            }
            panel.Options = new InputForm(options, [new("modello", "Analisi", Choices: ["Lineare", "Non lineare"]), new("phi", "Viscosità φ armature"), new("phi_trefoli", "Viscosità φ trefoli"), new("trazione_cls", "CLS resistente a trazione", Choices: ["No", "Sì"]), new("assi", "Assi delle azioni", Choices: ["Locali", "Principali", "Personalizzati"]), new("origine_x", "Origine x", "mm"), new("origine_y", "Origine y", "mm"), new("rotazione", "Rotazione assi", "°"), new("esposizione", "Esposizione", Choices: Ntc2018Checks.Exposures), new("sensibilita", "Armatura", Choices: ["Poco sensibile", "Sensibile"]), new("durata", "Durata del carico", Choices: ["Lunga", "Breve"]), new("aderenza", "Barre", Choices: ["Migliorata", "Liscia"]), new("copriferro_fessure", "c barra (vuoto: auto)", "mm"), new("spaziatura_fessure", "Spaziatura massima barre tese", "mm")], _ =>
            {
                if (initializing) return;
                EnableOptions();
                stressResults.Remove(key); foreach (var row in actions[key]) { row.Output("sigma_c", "—"); row.Output("sigma_s", "—"); row.Output("stress_status", "Da calcolare"); row.Output("eta_sigma", "—"); row.Output("wk", "Da calcolare"); }
                UpdateStressSelection(key); RefreshSummary(); InvalidateChecks();
            }, true, true);
            panel.Options.GroupFields("Analisi e viscosità", ["modello", "phi", "phi_trefoli"], true);
            panel.Options.GroupFields("Verifiche SLE · ambiente e armatura", ["esposizione", "sensibilita", "durata", "aderenza"], true);
            panel.Options.GroupFields("Fessurazione · disposizione delle barre", ["copriferro_fessure", "spaziatura_fessure"]);
            panel.Options.GroupFields("Avanzate · CLS teso e assi", ["trazione_cls", "assi", "origine_x", "origine_y", "rotazione"]);
            EnableOptions();
            var instructions = Notice("Analisi Checker lineare/non lineare. Rara: limiti CLS e acciaio; quasi permanente: limite CLS. Frequente: tensioni calcolate, nessun limite tensionale automatico. φ è il coefficiente di viscosità.");
            var optionsPanel = Panel("Opzioni · " + SectionWorkspace.Label(key), Scroller(Ui.Stack(panel.Options, instructions)), "Azioni già combinate · nessun coefficiente ψ applicato automaticamente");
            var viewport = new ViewportFrame("Mappa tensionale della sezione", panel.View, panel.View.ResetView);
            var contour = Ui.Choice(ConcreteSectionViewport.Contours, options.S("contour", ConcreteSectionViewport.Contours[0])); contour.Width = 205;
            panel.View.Contour = contour.Text;
            contour.SelectionChanged += (_, _) => { options["contour"] = contour.Text; panel.View.Contour = contour.Text; panel.View.InvalidateVisual(); Modified?.Invoke(); };
            viewport.Toolbar.Children.Insert(0, contour);
            var details = new TabControl { BorderThickness = new Thickness(0), Background = Brushes.White };
            Ui.Tab(details, "Riepilogo", Scroller(panel.Detail));
            Ui.Tab(details, "Barre e trefoli", panel.Bars);
            panel.View.BarSelected += index => { if (index < panel.Bars.Rows.Count) { details.SelectedIndex = 1; panel.Bars.SelectedIndex = index; panel.Bars.ScrollIntoView(panel.Bars.SelectedItem); } };
            panel.Bars.SelectionChanged += (_, _) => { panel.View.SelectedBar = (panel.Bars.SelectedItem as JsonRow)?.Values.S("id") ?? ""; panel.View.InvalidateVisual(); };
            var upper = Columns((viewport, 6, 310), (Panel("Dettaglio combinazione", details), 4, 240));
            panel.Grid = new JsonGrid([new("nome", "Combinazione"), new("N", "N [kN]"), new("Mx", "Mx [kNm]"), new("My", "My [kNm]"), new("sigma_c", "σc [MPa]", ReadOnly: true), new("sigma_s", "|σs| [MPa]", ReadOnly: true), new("eta_sigma", "ησ [-]", ReadOnly: true), new("stress_status", "Tensioni", ReadOnly: true), new("wk", "Fessurazione", ReadOnly: true)], true, actions[key]);
            panel.Grid.Columns[^2].Width = new DataGridLength(1.8, DataGridLengthUnitType.Star); panel.Grid.Columns[^1].Width = new DataGridLength(1.4, DataGridLengthUnitType.Star);
            panel.Grid.Columns[0].MinWidth = 130; panel.Grid.Columns[^2].MinWidth = 145; panel.Grid.Columns[^1].MinWidth = 125;
            var resultStyle = new Style(typeof(TextBlock)); resultStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            panel.Grid.RowHeight = double.NaN; panel.Grid.MinRowHeight = 30;
            foreach (var column in panel.Grid.Columns.TakeLast(2).Cast<DataGridTextColumn>()) column.ElementStyle = resultStyle;
            var table = Panel("Combinazioni · " + SectionWorkspace.Label(key), ActionTable(key, panel.Grid, () => UpdateStressSelection(key)));
            var body = Columns((optionsPanel, 2.6, 260), (Rows(upper, table, 3.5, 2), 7.4, 680)); body.Margin = new Thickness(0, 8, 0, 0);
            Ui.Tab(sleTabs, SectionWorkspace.Label(key), body);
            if (actions[key].Count > 0) panel.Grid.SelectedIndex = 0;
        }
        var header = Ui.Text("SLE · tensioni e fessurazione", 17, true); header.Margin = new Thickness(4, 12, 4, 10);
        return Ui.Dock(sleTabs, header);
    }
    private async Task CalculateStress(string key, CancellationToken token)
    {
        status.Text = "Analisi tensionale · " + SectionWorkspace.Label(key) + "…"; var settingsSle = settings["sle"]![key]!;
        var input = (JsonObject)Input.DeepClone(); var workspace = (JsonObject)settings.DeepClone(); var options = (JsonObject)settingsSle.DeepClone();
        var requests = actions[key].Select(row => (Id: row.Values.S("id"), Values: (JsonObject)row.Values.DeepClone())).ToArray();
        var outcomes = await Task.Run(() =>
        {
            var results = new Dictionary<string, StressOutcome>(); if (requests.Length == 0) return results;
            var engine = new CheckerSection(input, workspace, options);
            foreach (var request in requests)
            {
                token.ThrowIfCancellationRequested();
                try {
                    var force = ReadAction(new JsonRow(request.Values)); var state = engine.Stress(force, key);
                    Ntc2018Checks.CrackResult crack;
                    try { crack = Ntc2018Checks.Cracking(engine, state, force, input, workspace, options, key); }
                    catch (Exception ex) { crack = new(null, null, null, null, "Fessurazione non calcolata: " + ex.Message); }
                    results[request.Id] = new(state, state.Ratio, state.Status, crack.Status, crack);
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { results[request.Id] = new(null, null, "Checker: " + ex.Message); }
            }
            return results;
        }, token);
        token.ThrowIfCancellationRequested(); stressResults[key] = outcomes;
        foreach (var row in actions[key])
        {
            var outcome = outcomes[row.Values.S("id")]; row.Output("sigma_c", outcome.State?.sigma_cls.ToString("0.00") ?? "—"); row.Output("sigma_s", outcome.State?.sigma_acciaio.ToString("0.00") ?? "—"); row.Output("eta_sigma", outcome.Ratio?.ToString("0.000") ?? "—"); row.Output("stress_status", outcome.Status); row.Output("wk", outcome.CrackResult?.Width is double width ? $"{width:0.000} / {outcome.CrackResult.Limit:0.000} mm · η={outcome.CrackResult.Ratio:0.000}" : outcome.Cracking);
        }
        UpdateStressSelection(key);
    }
    private void UpdateStressSelection(string key)
    {
        if (!stressPanels.TryGetValue(key, out var panel) || panel.Grid is null) return;
        var row = panel.Grid.SelectedItem as JsonRow; var outcome = row is null ? null : stressResults.GetValueOrDefault(key)?.GetValueOrDefault(row.Values.S("id"));
        panel.View.Stress = outcome?.State; panel.View.InvalidateVisual(); panel.Bars.Rows.Clear();
        if (outcome?.State is CheckerStressState state)
        {
            panel.Detail.Text = $"{row!.Values.S("nome")}\n\nσc,min = {state.sigma_cls:0.00} MPa\n|σs|max = {state.sigma_acciaio:0.00} MPa\nησ = {outcome.Ratio?.ToString("0.000") ?? "—"}\n\n{outcome.Status}\n\n{outcome.Cracking}\nwk = {outcome.CrackResult?.Width?.ToString("0.000") ?? "—"} mm\nLimite = {outcome.CrackResult?.Limit?.ToString("0.000") ?? "—"} mm\nηw = {outcome.CrackResult?.Ratio?.ToString("0.000") ?? "—"}";
            var options = settings["sle"]![key]!; var force = ReadAction(row);
            string limits = state.ConcreteStressLimit is double limit ? $"|σc,comp| ≤ {limit:0.###} MPa" : "Nessun limite tensionale automatico";
            if (key == "SLE") limits += $"\nσs ≤ {Input.D("fyk_mpa") * .8:0.###} MPa (armatura ordinaria)";
            panel.Detail.Text = $"{SectionWorkspace.Label(key)} · {options.S("modello")}\n{row.Values.S("nome")} · assi {options.S("assi")}\nN = {force.N:0.##} kN\nMx / My = {force.Mx:0.##} / {force.My:0.##} kNm\n\nTENSIONI · NTC 2018\n{limits}\nησ = {outcome.Ratio?.ToString("0.000") ?? "—"}\n{outcome.Status}\n" + ResponseSummary(state.Response, "STATO ALL’AZIONE APPLICATA") +
                $"\n\nFESSURAZIONE\n{options.S("esposizione")} · armatura {options.S("sensibilita").ToLowerInvariant()}\nCarico di durata {options.S("durata").ToLowerInvariant()}\n{outcome.Cracking}\nwk / limite = {outcome.CrackResult?.Width?.ToString("0.000") ?? "—"} / {outcome.CrackResult?.Limit?.ToString("0.000") ?? "—"} mm\nηw = {outcome.CrackResult?.Ratio?.ToString("0.000") ?? "—"}\nAc,eff = {outcome.CrackResult?.EffectiveArea?.ToString("0.##") ?? "—"} mm²\nAs,eff = {outcome.CrackResult?.EffectiveSteel?.ToString("0.##") ?? "—"} mm²";
            int ordinary = panel.View.Section?.Bars.Count ?? 0;
            for (int i = 0; i < state.tensioni_barre.Length; i++)
            {
                double stress = state.tensioni_barre[i]; string id = i < ordinary ? "B" + (i + 1) : settings.Array("trefoli").ElementAtOrDefault(i - ordinary).S("id", "T" + (i - ordinary + 1));
                panel.Bars.Rows.Add(new JsonRow(J.Obj(("id", id), ("stress", stress.ToString("0.00")), ("type", stress > 0 ? "Trazione" : stress < 0 ? "Compressione" : "Nullo"))));
            }
        }
        else panel.Detail.Text = outcome?.Status ?? (row is null ? "Nessuna combinazione selezionata" : "Tensioni da calcolare");
    }
}
