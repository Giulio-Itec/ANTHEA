using System.Windows;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    internal void ExportReport(string filename, string title, HashSet<string> options)
    {
        Commit();
        if (Busy || !HasResults || calculationQueued) throw new InvalidOperationException("Attendere l’aggiornamento automatico e correggere i dati non validi prima di esportare il report.");
        settings["report_sezioni"] = J.Node(options.OrderBy(k => k).ToArray()); Modified?.Invoke();
        var result = Result!;
        result["dati"] = Data.DeepClone();
        var images = new List<ImmagineReport>();
        if (options.Contains("grafici"))
        {
            if (options.Contains("geometria")) images.Add(new("Sezione e schema indicativo delle staffe", preview.Png(), "geometria"));
            foreach (var panel in domainPanels)
            {
                string category = panel.ThreeD ? "dominio3d" : "dominio2d";
                if (!options.Contains(category)) continue;
                if (panel.ThreeD && checker3D.TryGetValue(panel.Key, out var domain))
                {
                    var mesh = domain.DisplayMesh(panel.Options);
                    // A detached viewport gives consistent image dimensions even when its tab is not active.
                    var view = new DomainViewport3D { Width = 1200, Height = 750, ShowActions = panel.Options.B("mostra_ed", true), ShowResistance = panel.Options.B("mostra_rd", true), ShowVerificationLines = panel.Options.B("mostra_linee", true), ColorByRatio = panel.Options.B("colora_eta"), ActionPointSize = panel.Options.D("dimensione_ed", 5), ResistancePointSize = panel.Options.D("dimensione_rd", 5) };
                    view.Measure(new Size(1200, 750)); view.Arrange(new Rect(0, 0, 1200, 750)); view.SetMesh(mesh);
                    var selected = (panel.Grid.SelectedItem as JsonRow)?.Values.S("id"); var checks = domainResults.GetValueOrDefault(panel.Prefix + panel.Key);
                    var points = new List<(string, ActionPoint, bool)>();
                    foreach (var row in panel.Grid.Items.OfType<JsonRow>().Where(row => row.Values.B("visible", true) && (!panel.Options.B("solo_selezionata") || row.Values.S("id") == selected)))
                        try { points.Add((row.Values.S("id"), CheckerSection.Point(checker3D[panel.Key].Section.Force(ReadAction(row))), checks?.GetValueOrDefault(row.Values.S("id"))?.Utilization is <= 1)); } catch (ArgumentException) { /* Invalid actions remain documented in the results table. */ }
                    view.Ratios = checks?.ToDictionary(kv => kv.Key, kv => kv.Value.Utilization);
                    view.Resistances = panel.Options.B("tutte_rd") ? panel.Grid.Items.OfType<JsonRow>().Where(r => r.Values.B("visible", true)).Select(r => (Id: r.Values.S("id"), Check: checks?.GetValueOrDefault(r.Values.S("id")))).Where(r => r.Check?.Resistance is not null).ToDictionary(r => r.Id, r => r.Check!.Resistance!.Value) : null;
                    view.SetActions(points, selected, selected is null ? null : checks?.GetValueOrDefault(selected)?.Resistance); view.SurfaceOpacity = 1 - panel.Options.D("trasparenza", 35) / 100; view.UpdateLayout();
                    images.Add(new("Dominio 3D " + SectionWorkspace.Label(panel.Key) + " in vista isometrica con filtri e livelli correnti", Ui.Snapshot(view), category));
                }
                else if (!panel.ThreeD && checker2D.ContainsKey(panel.Key))
                {
                    RefreshDomainPanel(panel, renderHidden: true);
                    images.Add(new(panel.Plot.Title + " nella configurazione grafica corrente", panel.Plot.Png(), category));
                }
            }
            foreach (var (key, panel) in stressPanels)
            {
                if (!options.Contains(key) || !stressResults.TryGetValue(key, out var outcomes)) continue;
                var rows = result["tensioni"]?[key] as System.Text.Json.Nodes.JsonObject ?? new();
                var governors = new Dictionary<string, string>();
                foreach (bool cracking in new[] { false, true })
                    if ((cracking ? SleCheckScope.Cracking(key, settings) : SleCheckScope.Stress(key)) && ReportConcrete.Governing(rows, cracking) is { } governing)
                    {
                        string reason = cracking ? "fessurazione" : "tensioni";
                        governors[governing.Key] = governors.TryGetValue(governing.Key, out var previous) ? previous + " e " + reason : reason;
                    }
                // No arbitrary current selection is substituted when a numerical governor is unavailable.
                foreach (var (id, reason) in governors)
                {
                    if (outcomes.GetValueOrDefault(id)?.State is not { } state) continue;
                    var view = new ConcreteSectionViewport { Section = panel.View.Section, Stirrups = panel.View.Stirrups, Tendons = panel.View.Tendons, Stress = state, Contour = panel.View.Contour, Axes = true, Labels = true };
                    string name = actions[key].FirstOrDefault(row => row.Values.S("id") == id)?.Values.S("nome") ?? id;
                    images.Add(new("SLE " + SectionWorkspace.Label(key) + " · governante " + reason + " · " + name + " · " + view.Contour, view.Png(), key));
                }
            }
            if (options.Contains("taglio")) images.Add(new("Schema indicativo delle staffe · diametro rappresentato in scala", shearView.Png(), "taglio"));
        }
        ReportConcrete.Write(filename, title, Data, result, options, images);
    }
}
