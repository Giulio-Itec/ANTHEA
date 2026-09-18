using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace
{
    private readonly TabControl sleTabs = new() { BorderThickness = new Thickness(0), Background = Ui.Bg };
    internal sealed record StressOutcome(StatoElastico? State, double? Ratio, string Status, string Cracking = "Da collegare");
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
            panel.Options = new InputForm(options, [new("modello", "Analisi", Choices: ["Lineare · sezione fessurata", "Non lineare · da collegare"]), new("sigma_c_lim", "Limite σc (utente)", "MPa"), new("sigma_s_lim", "Limite |σs| (utente)", "MPa"), new("wk_lim", "Limite wk (predisp.)", "mm")], _ =>
            {
                if (initializing) return;
                stressResults.Remove(key); foreach (var row in actions[key]) { row.Output("sigma_c", "—"); row.Output("sigma_s", "—"); row.Output("stress_status", "Da calcolare"); row.Output("eta_sigma", "—"); row.Output("wk", "Da collegare"); }
                UpdateStressSelection(key); RefreshSummary(); RebuildExport(); Modified?.Invoke();
            }, true, true);
            var instructions = Notice("Lineare: metodo n con CLS non resistente a trazione. I limiti σ sono manuali. Normativa e wk sono predisposti: nessun esito di fessurazione viene calcolato. Non lineare da collegare a Checker.");
            var optionsPanel = Panel("Opzioni · " + SectionWorkspace.Label(key), Scroller(Ui.Stack(panel.Options, Ui.Button("Calcola tensioni", async () => await RunAnalysis(async token => await CalculateStress(key, token)), true), instructions, Panel("Risultati selezionati", panel.Detail))), "Azioni già combinate · nessun coefficiente ψ applicato automaticamente");
            var viewport = new ViewportFrame("Mappa tensionale della sezione", panel.View, panel.View.ResetView);
            panel.View.BarSelected += index => { if (index < panel.Bars.Rows.Count) { panel.Bars.SelectedIndex = index; panel.Bars.ScrollIntoView(panel.Bars.SelectedItem); } };
            panel.Bars.SelectionChanged += (_, _) => { panel.View.SelectedBar = (panel.Bars.SelectedItem as JsonRow)?.Values.S("id") ?? ""; panel.View.InvalidateVisual(); };
            var upper = Columns((viewport, 7, 320), (Panel("Tensioni nelle barre", panel.Bars, "Segno positivo: compressione"), 3, 200));
            panel.Grid = new JsonGrid([new("nome", "Combinazione"), new("N", "N [kN]"), new("Mx", "Mx [kNm]"), new("My", "My [kNm]"), new("sigma_c", "σc [MPa]", ReadOnly: true), new("sigma_s", "|σs| [MPa]", ReadOnly: true), new("eta_sigma", "ησ [-]", ReadOnly: true), new("stress_status", "Tensioni", ReadOnly: true), new("wk", "Fessurazione", ReadOnly: true)], true, actions[key]);
            panel.Grid.Columns[^2].Width = new DataGridLength(1.8, DataGridLengthUnitType.Star); panel.Grid.Columns[^1].Width = new DataGridLength(1.4, DataGridLengthUnitType.Star);
            panel.Grid.Columns[0].MinWidth = 130; panel.Grid.Columns[^2].MinWidth = 145; panel.Grid.Columns[^1].MinWidth = 125;
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
        var outcomes = new Dictionary<string, StressOutcome>();
        if (settingsSle.S("modello").StartsWith("Non lineare"))
        {
            foreach (var row in actions[key]) outcomes[row.Values.S("id")] = new(null, null, "Non lineare da collegare");
        }
        else
        {
            var input = (JsonObject)Input.DeepClone(); var engine = new SezioneCA(input); double n = Data.B("n_automatico", true) ? engine.NAutomatico : input.Required("n", strict: true);
            double? Limit(string field)
            {
                if (string.IsNullOrWhiteSpace(settingsSle.S(field))) return null;
                double value = SectionWorkspace.Number(settingsSle.S(field), "Limite tensionale");
                if (value <= 0) throw new ArgumentException("I limiti tensionali manuali devono essere positivi oppure vuoti.");
                return value;
            }
            double? cLimit = Limit("sigma_c_lim"), sLimit = Limit("sigma_s_lim");
            // Snapshot values before leaving the UI thread.
            var requests = actions[key].Select(row => (Id: row.Values.S("id"), Values: (JsonObject)row.Values.DeepClone())).ToArray();
            outcomes = await Task.Run(() =>
            {
                var results = new Dictionary<string, StressOutcome>();
                foreach (var request in requests)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var force = ReadAction(new JsonRow(request.Values)); var state = SezioneElastica.Tensioni(engine, n, force.N, force.Mx, force.My);
                        double? ratio = cLimit is null && sLimit is null ? null : Math.Max(cLimit is double c ? state.sigma_cls / c : 0, sLimit is double s ? state.sigma_acciaio / s : 0);
                        string outcome = ratio is null ? "Limiti non impostati" : ratio > 1 ? "Supera limite utente" : cLimit is null || sLimit is null ? "Entro limite parziale" : "Entro limiti utente";
                        results[request.Id] = new(state, ratio, outcome);
                    }
                    catch (ArgumentException ex) { results[request.Id] = new(null, null, ex.Message); }
                }
                return results;
            }, token);
        }
        token.ThrowIfCancellationRequested(); stressResults[key] = outcomes;
        foreach (var row in actions[key])
        {
            var outcome = outcomes[row.Values.S("id")]; row.Output("sigma_c", outcome.State?.sigma_cls.ToString("0.00") ?? "—"); row.Output("sigma_s", outcome.State?.sigma_acciaio.ToString("0.00") ?? "—"); row.Output("eta_sigma", outcome.Ratio?.ToString("0.000") ?? "—"); row.Output("stress_status", outcome.Status); row.Output("wk", "Da collegare");
        }
        UpdateStressSelection(key);
    }
    private void UpdateStressSelection(string key)
    {
        if (!stressPanels.TryGetValue(key, out var panel) || panel.Grid is null) return;
        var row = panel.Grid.SelectedItem as JsonRow; var outcome = row is null ? null : stressResults.GetValueOrDefault(key)?.GetValueOrDefault(row.Values.S("id"));
        panel.View.Stress = outcome?.State; panel.View.InvalidateVisual(); panel.Bars.Rows.Clear();
        if (outcome?.State is StatoElastico state)
        {
            panel.Detail.Text = $"{row!.Values.S("nome")}\n\nσc,max = {state.sigma_cls:0.00} MPa\n|σs|max = {state.sigma_acciaio:0.00} MPa\nησ = {outcome.Ratio?.ToString("0.000") ?? "—"}\n\n{outcome.Status}\nResiduo equilibrio = {state.residuo_relativo:0.0E+0}\n\nFessurazione: da collegare";
            for (int i = 0; i < state.tensioni_barre.Length; i++) { double stress = state.tensioni_barre[i]; panel.Bars.Rows.Add(new JsonRow(J.Obj(("id", "B" + (i + 1)), ("stress", stress.ToString("0.00")), ("type", stress < 0 ? "Trazione" : "Compressione")))); }
        }
        else panel.Detail.Text = outcome?.Status ?? (row is null ? "Nessuna combinazione selezionata" : "Tensioni da calcolare");
    }
}
