using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private sealed class DomainLabelConverter : IValueConverter
    {
        public object Convert(object value, Type type, object parameter, CultureInfo culture) => SectionWorkspace.Label(value?.ToString() ?? "");
        public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
    private readonly List<FrameworkElement> tendonOnlyControls = [];
    private void RefreshTendonOptions()
    {
        bool present = tendons.Rows.Count > 0;
        foreach (var panel in stressPanels.Values)
            foreach (string key in new[] { "n_trefoli", "phi_trefoli", "__ep_ref" }) panel.Options.ShowField(key, present);
        foreach (var control in tendonOnlyControls) control.Visibility = present ? Visibility.Visible : Visibility.Collapsed;
        if (coefficients is not null) foreach (var (key, _) in ConcreteStandards.Coefficients.Where(c => c.Key.Contains("Prestress"))) coefficients.ShowField(key, present);
    }
    private void SynchronizeSharedSle(string source)
    {
        using var notifications = JsonRow.DeferNotifications(actions.Values.SelectMany(r => r));
        var from = settings["sle"]![source]!;
        foreach (string field in SectionWorkspace.SharedSleFields)
        {
            settings["sle_comuni"]![field] = from[field]?.DeepClone();
            foreach (var (key, panel) in stressPanels)
            {
                if (key == source) continue;
                settings["sle"]![key]![field] = from[field]?.DeepClone(); panel.Options.Set(field, from.S(field), true);
            }
        }
        foreach (var (key, panel) in stressPanels)
        {
            var o = settings["sle"]![key]!;
            foreach (string field in new[] { "phi", "phi_trefoli", "n_armature", "n_trefoli" }) panel.Options.Enable(field, o.S("modello") == "Lineare");
            foreach (string field in new[] { "origine_x", "origine_y", "rotazione" }) panel.Options.Enable(field, o.S("assi") == "Personalizzati");
            stressResults.Remove(key);
            foreach (var row in actions[key]) foreach (string field in new[] { "sigma_c", "sigma_s", "stress_status", "eta_sigma", "wk" }) row.Output(field, "Da calcolare");
            UpdateStressSelection(key);
        }
        RefreshDetailing();
    }
    private void InvalidateDomainForces(DomainPanel panel)
    {
        using var notifications = JsonRow.DeferNotifications(actions.Values.SelectMany(r => r));
        foreach (string key in new[] { "SLU", "SLV" })
        {
            domainResults.Remove(panel.Prefix + key);
            foreach (var row in actions[key]) { row.Output(panel.ThreeD ? "eta3d" : "eta2d", "—"); row.Output(panel.ThreeD ? "esito3d" : "esito2d", "Da calcolare"); }
        }
        UpdateSelection(panel); RefreshSummary(); status.Text = "Aggiornamento punti resistenti e tassi · dominio conservato"; InvalidateChecks(panel.Prefix + "SLU", panel.Prefix + "SLV");
    }
    private async void RefreshDisplayMesh(DomainPanel panel)
    {
        var options = (JsonObject)panel.Options.DeepClone();
        string key = panel.Key; int requested = revision;
        Modified?.Invoke();
        if (!checker3D.TryGetValue(key, out var domain)) return; // Pending analysis uses current visual options.
        try
        {
            status.Text = "Aggiornamento della sola mesh · verifiche conservate…";
            var mesh = await Task.Run(() => domain.DisplayMesh(options));
            if (disposed || requested != revision || key != panel.Key || options.S("interpolazione") != panel.Options.S("interpolazione") || options.S("suddivisioni_n") != panel.Options.S("suddivisioni_n")) return;
            meshes[key] = mesh; panel.NeedsVisualRefresh = true; RefreshDomainPanel(panel);
            status.Text = "Mesh aggiornata · punti resistenti e tassi invariati"; exportResult = null;
        }
        catch (ArgumentException ex) { status.Text = "Mesh non aggiornata: " + ex.Message; }
    }
    private void AddDomainScaleControls(DomainPanel panel, ViewportFrame frame)
    {
        if (!panel.ThreeD) panel.Plot.ViewReset += () => { panel.Options["scala_x"] = 1; panel.Options["scala_y"] = 1; Modified?.Invoke(); };
        void Apply()
        {
            if (panel.ThreeD) panel.View3D!.SetAxisScale(panel.Options.D("scala_mx", 1), panel.Options.D("scala_n", 1), panel.Options.D("scala_my", 1));
            else { panel.Plot.ScaleX = panel.Options.D("scala_x", 1); panel.Plot.ScaleY = panel.Options.D("scala_y", 1); panel.Plot.FitIncludesMarkers = panel.Options.B("fit_azioni"); panel.Plot.InvalidateVisual(); }
        }
        Apply();
        frame.Toolbar.Children.Add(Ui.Button("Scala…", () =>
        {
            string[] keys = panel.ThreeD ? ["scala_mx", "scala_n", "scala_my"] : ["scala_x", "scala_y"];
            var draft = new JsonObject(); foreach (string key in keys) draft[key] = panel.Options.D(key, 1).ToString("G12", CultureInfo.InvariantCulture);
            draft["zoom"] = ((panel.ThreeD ? panel.View3D!.Zoom : panel.Plot.Zoom) * 100).ToString("0", CultureInfo.InvariantCulture);
            draft["fit_azioni"] = panel.Options.B("fit_azioni") ? "Sì" : "No";
            var fields = keys.Select(k => new Field(k, "Fattore " + k[6..].ToUpperInvariant(), "×")).ToList();
            fields.Add(new("zoom", "Zoom", "%")); fields.Add(new("fit_azioni", "Adatta anche le azioni", Choices: ["No", "Sì"]));
            var form = new InputForm(draft, fields, _ => { }); var message = Ui.Text("Fattori grafici 0,25–4; zoom 10–2000%. Non modificano i calcoli. Adatta ripristina i fattori unitari.", 12);
            var window = Ui.Dialog(this, "Scala del dominio " + (panel.ThreeD ? "3D" : "2D"), new Border(), 470, 390);
            window.Content = Ui.Paper(Ui.Stack(form, message, Ui.Button("Applica", () =>
            {
                try
                {
                    form.Commit(); var values = keys.ToDictionary(k => k, k => SectionWorkspace.Number(draft.S(k), k)); double zoom = SectionWorkspace.Number(draft.S("zoom"), "Zoom") / 100;
                    if (values.Values.Any(v => v < .25 || v > 4) || zoom < .1 || zoom > 20) throw new ArgumentException("Fattori ammessi 0,25–4; zoom 10–2000%.");
                    foreach (var (key, value) in values) panel.Options[key] = value;
                    panel.Options["fit_azioni"] = draft.S("fit_azioni") == "Sì"; Apply();
                    if (panel.ThreeD) { panel.View3D!.FitIncludesActions = panel.Options.B("fit_azioni"); panel.View3D.FitView(); panel.View3D.Zoom = zoom; } else panel.Plot.Zoom = zoom;
                    Modified?.Invoke(); window.Close();
                }
                catch (ArgumentException ex) { message.Text = ex.Message; }
            })), 16);
            window.ShowDialog();
        }));
        panel.View3D?.SetFitActions(panel.Options.B("fit_azioni"));
        var fit = frame.Toolbar.Children.OfType<Button>().First(b => b.Content?.ToString() == "Adatta");
        int index = frame.Toolbar.Children.IndexOf(fit); frame.Toolbar.Children.Remove(fit);
        frame.Toolbar.Children.Insert(index, Ui.Button("Adatta", () =>
        {
            foreach (string key in new[] { "scala_x", "scala_y", "scala_n", "scala_mx", "scala_my" }) panel.Options[key] = 1;
            Apply(); if (panel.ThreeD) panel.View3D!.FitView(); else panel.Plot.ResetView(); Modified?.Invoke();
        }));
    }
    private Dictionary<string, VerificationSummary> CollectVerificationSummaries()
    {
        var result = new Dictionary<string, VerificationSummary>();
        string Name(string key, string id) => actions[key].FirstOrDefault(r => r.Values.S("id") == id)?.Values.S("nome") ?? id;
        foreach (string key in new[] { "SLU", "SLV" }) foreach (string prefix in new[] { "3D", "2D" })
            result[prefix + ":" + key] = VerificationSummary.Create(prefix + " · " + SectionWorkspace.Label(key), actions[key].Count,
                domainResults.GetValueOrDefault(prefix + ":" + key)?.Select(kv => (Name(key, kv.Key), kv.Value.Utilization, (bool?)null)) ?? []);
        foreach (string key in SectionWorkspace.Sets.Skip(2))
        {
            var values = stressResults.GetValueOrDefault(key);
            if (SleCheckScope.Stress(key)) result[key + ":tensioni"] = VerificationSummary.Create(SectionWorkspace.Label(key) + " · tensioni", actions[key].Count, values?.Select(kv => (Name(key, kv.Key), kv.Value.Ratio, (bool?)null)) ?? []);
            if (SleCheckScope.Cracking(key, settings)) result[key + ":fessurazione"] = VerificationSummary.Create(SectionWorkspace.Label(key) + " · fessurazione", actions[key].Count, values?.Select(kv => (Name(key, kv.Key), kv.Value.CrackResult?.Ratio, kv.Value.CrackResult?.Passed)) ?? []);
        }
        for (int axis = 0; axis < 2; axis++)
        {
            int index = axis;
            result[axis == 0 ? "Taglio:x" : "Taglio:y"] = VerificationSummary.Create(axis == 0 ? "Taglio Vx" : "Taglio Vy", shearGrid?.Rows.Count ?? 0, shearResults.Select(kv =>
                (shearGrid?.Rows.FirstOrDefault(r => r.Values.S("id") == kv.Key)?.Values.S("nome") ?? kv.Key, kv.Value[index].Ratio, (bool?)null)));
        }
        int torques = shearGrid?.Rows.Count(r => J.Number(r.Values["T"]) is double t && t != 0) ?? 0;
        if (torques > 0) result["Taglio:torsione"] = VerificationSummary.Create("Torsione e interazione V–T", torques, torsionResults.Select(kv =>
            (shearGrid?.Rows.FirstOrDefault(r => r.Values.S("id") == kv.Key)?.Values.S("nome") ?? kv.Key,
                kv.Value.TorsionRatio is double et && kv.Value.ConcreteCombinedRatio is double ec && kv.Value.SteelCombinedRatio is double es ? (double?)Math.Max(et, Math.Max(ec, es)) : null, (bool?)kv.Value.Passed)));
        return result;
    }
    private void RefreshVerificationSummaries()
    {
        var results = CollectVerificationSummaries();
        void Fill(VerificationCards cards, Func<string, bool> include)
        { cards.Start(); foreach (var (key, summary) in results.Where(p => include(p.Key))) cards.AddSummary(summary); }
        foreach (var (key, cards) in summaries)
            Fill(cards, k => key is "SLU" or "SLV" ? k.EndsWith(":" + key) : k.StartsWith(key + ":"));
        foreach (var panel in domainPanels) Fill(panel.Summary, k => k.StartsWith(panel.ThreeD ? "3D:" : "2D:"));
        foreach (var panel in stressPanels.Values) Fill(panel.Summary, k => k.StartsWith("SLE"));
        foreach (var cards in new[] { shearWorst, shearDashboard }) Fill(cards, k => k.StartsWith("Taglio:"));
    }
    private static string WorstSummary(string title, int total, IEnumerable<(string Name, double? Ratio, string Status)> source)
    {
        int valid = 0, failed = 0, missing = 0;
        (string Name, double? Ratio, string Status) worst = default;
        var examples = new List<string>();
        foreach (var row in source)
        {
            if (row.Ratio is double ratio && double.IsFinite(ratio))
            {
                if (valid++ == 0 || ratio > worst.Ratio) worst = row;
                if (ratio > 1) failed++;
            }
            else { missing++; if (examples.Count < 5) examples.Add(row.Name + ": " + row.Status); }
        }
        string result = title + (total == 0 ? "\nNessuna combinazione" : valid == 0 ? "\nTasso non disponibile / non applicabile" : $"\nGoverna: {worst.Name} · η = {EngineeringFormat.Number(worst.Ratio)}\n{worst.Status}");
        if (total > 0) result += $"\n{valid}/{total} con tasso · {failed} oltre 1";
        if (missing > 0) result += "\nSenza tasso: " + string.Join("; ", examples) + (missing > examples.Count ? $"; … altre {missing - examples.Count} (dettagli nella tabella)" : "");
        return result;
    }
}
