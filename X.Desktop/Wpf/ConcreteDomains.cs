using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace
{
    private sealed class DomainPanel
    {
        internal bool ThreeD;
        internal JsonObject Options = null!;
        internal InputForm Form = null!;
        internal ComboBox Mode = null!;
        internal JsonGrid Grid = null!;
        internal DomainViewport3D? View3D;
        internal readonly Plot Plot = new() { InvertY = false, Title = "Sezione del dominio", EmptyMessage = "Calcolare il dominio per visualizzare la sezione" };
        internal readonly TextBlock Detail = Ui.Text("Selezionare una combinazione", 12);
        internal string Key => Options.S("stato", "SLU");
        internal string Prefix => ThreeD ? "3D:" : "2D:";
    }
    private UIElement BuildDomainPanel(bool threeD)
    {
        var panel = new DomainPanel { ThreeD = threeD, Options = settings[threeD ? "dominio3d" : "dominio2d"]!.AsObject() }; domainPanels.Add(panel);
        var fields = new List<Field> { new("stato", "Stato limite", Choices: ["SLU", "SLV"]) };
        if (threeD) fields.AddRange([new("angoli", "Direzioni angolari"), new("campioni", "Campioni / profilo"), new("criterio", "Ricerca resistenza", Choices: ["Eccentricità costante", "N costante"])]);
        else fields.AddRange([new("tipo", "Sezione dominio", Choices: ["N–M", "Mx–My"]), new("theta", "Direzione θ", "°"), new("N", "N fissato", "kN")]);
        fields.Add(new("filtro", "Filtro azioni", Choices: ["Tutte", "Entro il dominio", "Fuori dominio", "Da controllare"]));
        panel.Form = new(panel.Options, fields, key =>
        {
            if (initializing) return;
            if (key is "filtro" or "stato") { AttachDomainRows(panel); RefreshDomainPanel(panel); Modified?.Invoke(); }
            else if (key is "angoli" or "campioni") Invalidate();
            else
            {
                domainResults.Remove(panel.Prefix + "SLU"); domainResults.Remove(panel.Prefix + "SLV");
                RefreshDomainPanel(panel); RefreshSummary(); RebuildExport(); Modified?.Invoke();
            }
            panel.Form.Enable("theta", panel.Options.S("tipo") == "N–M"); panel.Form.Enable("N", panel.Options.S("tipo") == "Mx–My");
        }, true, true);
        panel.Mode = (ComboBox)panel.Form.Editors["stato"];
        var calculate = Ui.Button(threeD ? "Calcola dominio 3D" : "Calcola sezione 2D", async () => await RunAnalysis(async token => await CalculateDomain(panel, token)), true);
        var options = Ui.Stack(panel.Form, calculate);
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
        options.Children.Add(Notice(threeD ? "SLU: campo plastico. SLV: campo elastico al limite dell’acciaio del motore attuale. La mesh è campionata: aumentare la discretizzazione per verificarne la stabilità." : "N–M è un taglio effettivo della mesh nella direzione θ. Mx–My è un taglio a N fissato. Le azioni esterne al piano non ricevono un esito 2D."));
        options.Children.Add(Panel("Azione selezionata", panel.Detail));
        options.Children.Add(Ui.Text("Arancio: azione selezionata\nViola: punto resistente\nVerde / rosso: posizione rispetto al dominio", 11, color: Ui.Muted));
        var left = Panel("Opzioni di calcolo", Scroller(options), "N positivo a compressione · assi locali x, y");
        UIElement viewport;
        if (threeD)
        {
            panel.View3D = new(); var frame = new ViewportFrame("Dominio N–Mx–My", panel.View3D, panel.View3D.ResetView);
            frame.Toolbar.Children.Insert(0, Ui.Button("Mesh", panel.View3D.ToggleWireframe));
            var view = Ui.Choice(["Isometrica", "N–Mx", "N–My", "Mx–My"], "Isometrica"); view.Width = 100; view.SelectionChanged += (_, _) => panel.View3D.StandardView(view.Text); frame.Toolbar.Children.Insert(0, view);
            panel.View3D.ActionSelected += id => { panel.Grid.SelectedItem = actions[panel.Key].FirstOrDefault(r => r.Values.S("id") == id); if (panel.Grid.SelectedItem is not null) panel.Grid.ScrollIntoView(panel.Grid.SelectedItem); };
            viewport = frame;
        }
        else viewport = new ViewportFrame("Sezione del dominio", panel.Plot, panel.Plot.ResetView);
        string ratio = threeD ? "eta3d" : "eta2d", outcome = threeD ? "esito3d" : "esito2d";
        panel.Grid = new JsonGrid([new("visible", "Mostra", Bool: true), new("nome", "Combinazione"), new("N", "N [kN]"), new("Mx", "Mx [kNm]"), new("My", "My [kNm]"), new(ratio, "η [-]", ReadOnly: true), new(outcome, "Esito", ReadOnly: true)], true, actions[panel.Key]);
        panel.Grid.Columns[^1].Width = new DataGridLength(2, DataGridLengthUnitType.Star);
        var table = Panel("Combinazioni N–Mx–My", ActionTable(panel.Key, panel.Grid, () => UpdateSelection(panel)), "Le azioni SLU / SLV sono condivise fra le schede 3D e 2D");
        var body = Columns((left, 2.65, 265), (Rows(viewport, table, 3.7, 2), 7.35, 640)); body.Margin = new Thickness(0, 10, 0, 0);
        AttachDomainRows(panel); panel.Form.Enable("theta", panel.Options.S("tipo") == "N–M"); panel.Form.Enable("N", panel.Options.S("tipo") == "Mx–My"); return body;
    }
    private void AttachDomainRows(DomainPanel panel)
    {
        if (panel.Grid is null) return;
        var previous = (panel.Grid.SelectedItem as JsonRow)?.Values.S("id"); panel.Grid.Commit(); panel.Grid.Tag = panel.Key;
        var view = new ListCollectionView(actions[panel.Key]);
        view.Filter = item =>
        {
            if (item is not JsonRow row) return false;
            domainResults.TryGetValue(panel.Prefix + panel.Key, out var results); results?.TryGetValue(row.Values.S("id"), out _);
            DomainCheck? check = results?.GetValueOrDefault(row.Values.S("id"));
            return panel.Options.S("filtro") switch { "Entro il dominio" => check?.Utilization is <= 1, "Fuori dominio" => check?.Utilization is > 1, "Da controllare" => check?.Utilization is null, _ => true };
        };
        panel.Grid.ItemsSource = view;
        panel.Grid.SelectedItem = view.Cast<JsonRow>().FirstOrDefault(r => r.Values.S("id") == previous) ?? view.Cast<JsonRow>().FirstOrDefault();
    }
    private static ActionPoint ReadAction(JsonRow row)
    {
        if (string.IsNullOrWhiteSpace(row.Values.S("nome"))) throw new ArgumentException("Assegnare un nome alla combinazione.");
        return new(SectionWorkspace.Number(row.Values.S("N"), "N"), SectionWorkspace.Number(row.Values.S("Mx"), "Mx"), SectionWorkspace.Number(row.Values.S("My"), "My"));
    }
    private void ValidateEngine()
    {
        if (tendons.Rows.Count > 0) throw new ArgumentException("Trefoli presenti: calcolo sospeso fino al collegamento del motore di precompressione Checker.");
        _ = new SezioneCA(Input);
    }
    private async Task<SectionDomainMesh> GetMesh(string key, CancellationToken token)
    {
        if (meshes.TryGetValue(key, out var existing)) return existing;
        ValidateEngine(); var options = settings["dominio3d"]!;
        int angles = SectionWorkspace.Subdivisions(options.S("angoli"), "Direzioni angolari", 12, 144), samples = SectionWorkspace.Subdivisions(options.S("campioni"), "Campioni", 21, 321);
        var snapshot = (JsonObject)Input.DeepClone();
        var mesh = await Task.Run(() => SectionDomainMesh.Build(snapshot, key == "SLV" ? "Elastico" : "Plastico", angles, samples, token), token);
        token.ThrowIfCancellationRequested(); meshes[key] = mesh; return mesh;
    }
    private async Task CalculateDomain(DomainPanel panel, CancellationToken token, string? requestedKey = null)
    {
        string key = requestedKey ?? panel.Key; status.Text = $"Calcolo {SectionWorkspace.Label(key)} · dominio {(panel.ThreeD ? "3D" : "2D")}…";
        var mesh = await GetMesh(key, token);
        bool nm = panel.Options.S("tipo") == "N–M"; double value = panel.ThreeD ? 0 : SectionWorkspace.Number(panel.Options.S(nm ? "theta" : "N"), nm ? "Direzione θ" : "N fissato") * (nm ? Math.PI / 180 : 1);
        bool constantN = panel.ThreeD ? panel.Options.S("criterio") == "N costante" : !nm;
        var snapshots = actions[key].Select(row => (JsonObject)row.Values.DeepClone()).ToArray();
        var results = await Task.Run(() =>
        {
            var rows = new Dictionary<string, DomainCheck>();
            foreach (var snapshot in snapshots)
            {
                token.ThrowIfCancellationRequested(); DomainCheck result;
                try
                {
                    var force = ReadAction(new JsonRow(snapshot));
                    bool inPlane = panel.ThreeD || (nm ? Math.Abs(-force.Mx * Math.Sin(value) + force.My * Math.Cos(value)) <= 1e-6 * Math.Max(1, double.Hypot(force.Mx, force.My)) : Math.Abs(force.N - value) <= 1e-6 * Math.Max(1, Math.Abs(value)));
                    result = inPlane ? mesh.Check(force, constantN) : new(null, null, "Fuori dal piano selezionato");
                }
                catch (ArgumentException ex) { result = new(null, null, ex.Message); }
                rows[snapshot.S("id")] = result;
            }
            return rows;
        }, token);
        token.ThrowIfCancellationRequested();
        domainResults[panel.Prefix + key] = results;
        if (key == panel.Key) { AttachDomainRows(panel); RefreshDomainPanel(panel); }
    }
    private void RefreshDomainViews() { foreach (var panel in domainPanels) RefreshDomainPanel(panel); }
    private void RefreshDomainPanel(DomainPanel panel)
    {
        if (panel.Grid is null) return;
        meshes.TryGetValue(panel.Key, out var mesh); domainResults.TryGetValue(panel.Prefix + panel.Key, out var checks);
        string eta = panel.ThreeD ? "eta3d" : "eta2d", outcome = panel.ThreeD ? "esito3d" : "esito2d";
        foreach (var row in actions[panel.Key])
        {
            var check = checks?.GetValueOrDefault(row.Values.S("id")); row.Output(eta, check?.Utilization?.ToString("0.000") ?? "—"); row.Output(outcome, check?.Status ?? "Da calcolare");
        }
        if (panel.ThreeD) panel.View3D!.SetMesh(mesh);
        else
        {
            panel.Plot.Series = []; panel.Plot.Markers = [];
            if (mesh is not null)
            {
                try
                {
                    bool nm = panel.Options.S("tipo") == "N–M";
                    double value = SectionWorkspace.Number(panel.Options.S(nm ? "theta" : "N"), nm ? "θ" : "N") * (nm ? Math.PI / 180 : 1);
                    var cut = mesh.Cut(nm, value); var segments = cut.Select(s => (Project(s.A, nm, value), Project(s.B, nm, value))).ToArray();
                    panel.Plot.Segments = segments;
                    panel.Plot.Title = $"{SectionWorkspace.Label(panel.Key)} · " + (nm ? $"N–M, θ = {value * 180 / Math.PI:0.##}°" : $"Mx–My, N = {value:0.##} kN");
                    panel.Plot.XLabel = nm ? "Mθ [kNm]" : "Mx [kNm]"; panel.Plot.YLabel = nm ? "N [kN]" : "My [kNm]";
                    panel.Plot.Note = "Taglio della superficie campionata · azioni fuori piano mostrate solo in proiezione";
                    panel.Plot.EmptyMessage = "Nessuna intersezione del dominio con il piano selezionato";
                }
                catch (ArgumentException ex) { panel.Plot.Segments = []; panel.Plot.EmptyMessage = ex.Message; }
            }
            else { panel.Plot.Segments = []; panel.Plot.EmptyMessage = "Calcolare il dominio per visualizzare la sezione"; }
            panel.Plot.InvalidateVisual();
        }
        UpdateSelection(panel);
    }
    private static double[] Project(ActionPoint p, bool nm, double value) => nm ? [p.Mx * Math.Cos(value) + p.My * Math.Sin(value), p.N] : [p.Mx, p.My];
    private void UpdateSelection(DomainPanel panel)
    {
        if (panel.Grid is null) return;
        var row = panel.Grid.SelectedItem as JsonRow; string? id = row?.Values.S("id"); domainResults.TryGetValue(panel.Prefix + panel.Key, out var checks); DomainCheck? selectedCheck = id is null ? null : checks?.GetValueOrDefault(id);
        var visible = new List<(string Id, ActionPoint Force, bool Pass)>();
        foreach (var item in panel.Grid.Items.OfType<JsonRow>())
        {
            if (!item.Values.B("visible", true)) continue;
            try { visible.Add((item.Values.S("id"), ReadAction(item), checks?.GetValueOrDefault(item.Values.S("id"))?.Utilization is <= 1)); } catch (ArgumentException) { }
        }
        if (panel.ThreeD) panel.View3D!.SetActions(visible, id, selectedCheck?.Resistance);
        else
        {
            panel.Plot.Markers = [];
            try
            {
                bool nm = panel.Options.S("tipo") == "N–M"; double value = SectionWorkspace.Number(panel.Options.S(nm ? "theta" : "N"), "Piano") * (nm ? Math.PI / 180 : 1);
                foreach (var action in visible) { var p = Project(action.Force, nm, value); panel.Plot.Markers.Add(new(p[0], p[1], action.Id == id ? "Ed" : "", action.Id == id ? Ui.Brush("#E09620") : action.Pass ? Ui.Brush("#257761") : Ui.Brush("#CE4C4C"))); }
                if (selectedCheck?.Resistance is ActionPoint r) { var p = Project(r, nm, value); panel.Plot.Markers.Add(new(p[0], p[1], "Rd", Ui.Brush("#A23BC4"))); }
            }
            catch (ArgumentException) { }
            panel.Plot.InvalidateVisual();
        }
        if (row is null) { panel.Detail.Text = "Nessuna combinazione selezionata"; return; }
        try
        {
            var a = ReadAction(row); panel.Detail.Text = $"{row.Values.S("nome")}\n\nNEd = {a.N:0.##} kN\nMx,Ed = {a.Mx:0.##} kNm\nMy,Ed = {a.My:0.##} kNm\n\n" + (selectedCheck is null ? "Da calcolare" : $"η = {selectedCheck.Utilization?.ToString("0.000") ?? "—"}\n{selectedCheck.Status}");
            if (selectedCheck?.Resistance is ActionPoint r) panel.Detail.Text += $"\n\nNRd = {r.N:0.##} kN\nMx,Rd = {r.Mx:0.##} kNm\nMy,Rd = {r.My:0.##} kNm";
        }
        catch (ArgumentException ex) { panel.Detail.Text = ex.Message; }
    }
    private async Task RunAnalysis(Func<CancellationToken, Task> work)
    {
        if (Busy || disposed) return; Commit(); int requested = revision; Busy = true; cancellation = new CancellationTokenSource(); var token = cancellation.Token;
        tabs.IsEnabled = false; runAll.IsEnabled = false; cancel.Visibility = progress.Visibility = Visibility.Visible;
        try
        {
            ValidateEngine(); await work(token); token.ThrowIfCancellationRequested();
            if (disposed || requested != revision) return;
            RefreshDomainViews(); RefreshSummary(); RebuildExport(); status.Text = "Calcolo completato · risultati riferiti ai dati correnti · verifiche normative non complete";
        }
        catch (OperationCanceledException) { status.Text = "Calcolo annullato"; RefreshSummary(); RebuildExport(); }
        catch (Exception ex) { status.Text = "Calcolo non completato: " + ex.Message; RefreshSummary(); RebuildExport(); }
        finally
        {
            Busy = false; tabs.IsEnabled = true; runAll.IsEnabled = true; cancel.Visibility = progress.Visibility = Visibility.Collapsed; cancellation.Dispose(); cancellation = null;
        }
    }
    internal Task CalculateAllAsync() => RunAnalysis(async token =>
    {
        foreach (string key in new[] { "SLU", "SLV" }) { await CalculateDomain(domainPanels[0], token, key); await CalculateDomain(domainPanels[1], token, key); }
        foreach (string key in SectionWorkspace.Sets.Skip(2)) await CalculateStress(key, token);
    });
    private void RebuildExport()
    {
        if (domainResults.Count == 0 && stressResults.Count == 0) { Result = null; return; }
        var domains = new JsonObject(); foreach (var (key, rows) in domainResults) domains[key] = J.Node(rows);
        var stresses = new JsonObject(); foreach (var (key, rows) in stressResults) stresses[key] = J.Node(rows);
        Result = J.Obj(("errore", ""), ("motore", "ANTHEA · superficie campionata / metodo n"), ("normativa_riferimento", settings.S("normativa")), ("verifica_normativa_completa", false), ("domini", domains), ("tensioni", stresses), ("dati", Data), ("avvisi", new[] { "Checker non ancora collegato", "Fessurazione e precompressione non implementate", "I limiti tensionali sono valori manuali, non automaticamente derivati dalla normativa" }));
    }
    private void RefreshSummary()
    {
        foreach (var (key, text) in summaries)
        {
            int count = actions[key].Count;
            if (tendons.Rows.Count > 0) { text.Text = "Sospesa · trefoli da collegare a Checker"; text.Foreground = Ui.Brush("#865D16"); continue; }
            if (count == 0) { text.Text = "Nessuna combinazione inserita"; text.Foreground = Ui.Muted; continue; }
            if (key is "SLU" or "SLV")
            {
                string Summary(string prefix)
                {
                    var checks = domainResults.GetValueOrDefault(prefix + ":" + key);
                    if (checks is null) return prefix + " · da calcolare";
                    var values = checks.Values.Where(c => c.Utilization is not null).ToArray(); int missing = count - values.Length;
                    return $"{prefix} · ηmax = {(values.Length == 0 ? "—" : values.Max(c => c.Utilization)!.Value.ToString("0.000"))} · {values.Count(c => c.Utilization > 1)} fuori" + (missing > 0 ? $"\n{prefix} · {missing} fuori piano / da controllare" : "");
                }
                text.Text = $"{count} combinazioni\n{Summary("3D")}\n{Summary("2D")}";
                var all = new[] { "3D:", "2D:" }.SelectMany(prefix => domainResults.GetValueOrDefault(prefix + key)?.Values.AsEnumerable() ?? []);
                text.Foreground = all.Any(c => c.Utilization is > 1) ? Ui.Brush("#BB4B41") : Ui.Muted;
            }
            else
            {
                var checks = stressResults.GetValueOrDefault(key); var values = checks?.Values.Where(s => s.State is not null).ToArray();
                text.Text = values is not { Length: > 0 } ? $"{count} combinazioni · tensioni da calcolare\nFessurazione: da collegare" : $"σc,max = {values.Max(s => s.State!.sigma_cls):0.00} MPa\n|σs|max = {values.Max(s => s.State!.sigma_acciaio):0.00} MPa\n{values.Length}/{count} analizzate · " + (values.Any(s => s.Ratio > 1) ? $"{values.Count(s => s.Ratio > 1)} oltre limite utente" : values.All(s => s.Ratio is null) ? "limiti non impostati" : "entro i limiti inseriti") + "\nFessurazione: da collegare";
                text.Foreground = values?.Any(s => s.Ratio > 1) == true ? Ui.Brush("#BB4B41") : Ui.Muted;
            }
        }
    }
}
