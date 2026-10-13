using System.Text.Json.Nodes;
using System.Windows;
using Anthea.Application.Concrete;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using X.Core;
namespace X.Desktop;

internal sealed record ConcreteDomainGraphic(bool ThreeD, JsonObject Options, IReadOnlyList<JsonObject> Rows, string? Selected,
    ConcreteResults.Domain3D? Three, ConcreteResults.Domain2D? Two, IReadOnlyDictionary<string, ConcreteResults.Check>? Checks, Plot Plot)
{
    internal string Key => Options.S("stato", "SLU");
}
internal sealed record ConcreteStressGraphic(ConcreteSectionViewport View, IReadOnlyDictionary<string, StressOutcome>? Checks, IReadOnlyList<JsonObject> Rows);
internal sealed record ConcreteReportGraphics(JsonObject Data, JsonObject Result, ConcreteSectionViewport Preview,
    IReadOnlyList<ConcreteDomainGraphic> Domains, IReadOnlyDictionary<string, ConcreteStressGraphic> Stresses, Plot? CurvaturePlot = null);

internal sealed partial class ModuleFigureRenderer
{
    internal static ConcreteReportGraphics ConcreteGraphics(ConcreteReportAnalysis analysis)
    {
        var data = analysis.Data; var settings = data["workspace_ca"]!.AsObject();
        var shear = settings["taglio"]!.AsObject(); var section = analysis.Geometry;
        var tendons = new List<TendonPoint>();
        foreach (var t in settings.Array("trefoli").OfType<JsonObject>())
            if (J.Number(t["x"]) is double x && J.Number(t["y"]) is double y && J.Number(t["area"]) is double area && area > 0)
                tendons.Add(new(t.S("id"), x, y, area));
        var preview = new ConcreteSectionViewport { Section = section, Stirrups = shear, Tendons = tendons,
            Dimensions = settings.B("quote_dimensioni", true), CoverDimensions = settings.B("quote_copriferro", true), SpacingDimensions = settings.B("quote_interferro", true) };
        var domains = new List<ConcreteDomainGraphic>();
        foreach (bool three in new[] { true, false })
        {
            var o = settings[three ? "dominio3d" : "dominio2d"]!.AsObject(); string key = o.S("stato", "SLU");
            var checks = analysis.DomainChecks.GetValueOrDefault((three ? "3D:" : "2D:") + key);
            var rows = analysis.Actions[key].Where(r => ConcreteDomainChart.Includes(o, checks?.GetValueOrDefault(r.S("id"))))
                .Order(Comparer<JsonObject>.Create((a, b) => TableValues.Compare(a.S("nome"), b.S("nome")))).ToArray();
            string? selected = rows.FirstOrDefault()?.S("id");
            var plot = new Plot { InvertY = false, CenteredAxes = true, Title = "Sezione del dominio", EmptyMessage = "Dominio in attesa di aggiornamento automatico",
                ScaleX = o.D("scala_x", 1), ScaleY = o.D("scala_y", 1), FitIncludesMarkers = o.B("fit_azioni") };
            if (!three)
            {
                PlotPresentation.ApplyDomainGeometry(plot, ConcreteDomainChart.Geometry(key, o, analysis.Domains2D.GetValueOrDefault(key)));
                PlotPresentation.ApplyDomainActions(plot, ConcreteDomainChart.Actions(o, analysis.Domains2D.GetValueOrDefault(key), rows, selected, checks));
            }
            domains.Add(new(three, o, rows, selected, analysis.Domains3D.GetValueOrDefault(key), analysis.Domains2D.GetValueOrDefault(key), checks, plot));
        }
        var stresses = ConcreteEditor.Sets.Skip(2).ToDictionary(key => key, key => new ConcreteStressGraphic(
            new ConcreteSectionViewport { Section = section, Stirrups = shear, Tendons = tendons, Contour = settings["sle"]![key].S("contour", ConcreteSectionViewport.Contours[0]) },
            analysis.StressChecks.GetValueOrDefault(key), analysis.Actions[key]));
        return new(data, analysis.Result, preview, domains, stresses);
    }
    internal static IReadOnlyList<ReportFigure> ConcreteFigures(ConcreteReportGraphics graphics, IReadOnlySet<string> options)
    {
        var Data = graphics.Data; var result = graphics.Result; var settings = Data["workspace_ca"]!.AsObject();
        var ShearOptions = settings["taglio"]!.AsObject();
        var images = new List<ReportFigure>();
        if (options.Contains("grafici"))
        {
            if (options.Contains("geometria")) images.Add(new("Sezione e schema indicativo delle staffe", graphics.Preview.Png(), "geometria"));
            foreach (var panel in graphics.Domains)
            {
                string category = panel.ThreeD ? "dominio3d" : "dominio2d";
                if (!options.Contains(category)) continue;
                if (panel.ThreeD && panel.Three is { } domain)
                {
                    var mesh = domain.DisplayMesh(panel.Options);
                    // A detached viewport gives consistent image dimensions even when its tab is not active.
                    var view = new DomainViewport3D { Width = 1200, Height = 750, ShowActions = panel.Options.B("mostra_ed", true), ShowResistance = panel.Options.B("mostra_rd", true), ShowVerificationLines = panel.Options.B("mostra_linee", true), ColorByRatio = panel.Options.B("colora_eta"), ActionPointSize = panel.Options.D("dimensione_ed", 5), ResistancePointSize = panel.Options.D("dimensione_rd", 5) };
                    view.Measure(new Size(1200, 750)); view.Arrange(new Rect(0, 0, 1200, 750)); view.SetMesh(mesh);
                    var selected = panel.Selected; var checks = panel.Checks;
                    var points = ConcreteDomainChart.VisibleActions(panel.Options, panel.Rows, selected, checks, domain, report3D: true);
                    view.Ratios = checks?.ToDictionary(kv => kv.Key, kv => kv.Value.Utilization);
                    view.Resistances = panel.Options.B("tutte_rd") ? panel.Rows.Where(r => r.B("visible", true)).Select(r => (Id: r.S("id"), Check: checks?.GetValueOrDefault(r.S("id")))).Where(r => r.Check?.Resistance is not null).ToDictionary(r => r.Id, r => r.Check!.Resistance!.Value) : null;
                    view.SetActions(points, selected, selected is null ? null : checks?.GetValueOrDefault(selected)?.Resistance); view.SurfaceOpacity = 1 - panel.Options.D("trasparenza", 35) / 100; view.UpdateLayout();
                    images.Add(new("Dominio 3D " + (panel.Options.ContainsKey("modello_resistente") ? panel.Key + " · " + panel.Options.S("modello_resistente") : ConcreteEditor.Label(panel.Key)) + " in vista isometrica con filtri e livelli correnti", Ui.DocumentSnapshot(view), category));
                }
                else if (!panel.ThreeD && panel.Two is not null)
                {
                    images.Add(new(panel.Plot.Title + " nella configurazione grafica corrente", panel.Plot.Png(), category));
                }
            }
            foreach (var (key, panel) in graphics.Stresses)
            {
                if (!options.Contains(key) || panel.Checks is not { } outcomes) continue;
                var rows = result["tensioni"]?[key] as System.Text.Json.Nodes.JsonObject ?? new();
                var governors = new Dictionary<string, string>();
                foreach (bool cracking in new[] { false, true })
                    if ((cracking ? ConcreteEditor.HasCrackCheck(key, settings) : ConcreteEditor.HasStressCheck(key)) && ReportConcrete.Governing(rows, cracking) is { } governing)
                    {
                        string reason = cracking ? "fessurazione" : "tensioni";
                        governors[governing.Key] = governors.TryGetValue(governing.Key, out var previous) ? previous + " e " + reason : reason;
                    }
                // No arbitrary current selection is substituted when a numerical governor is unavailable.
                foreach (var (id, reason) in governors)
                {
                    if (outcomes.GetValueOrDefault(id)?.State is not { } state) continue;
                    var view = new ConcreteSectionViewport { Section = panel.View.Section, Stirrups = panel.View.Stirrups, Tendons = panel.View.Tendons, Stress = state, Contour = panel.View.Contour, Axes = true, Labels = true };
                    string name = panel.Rows.FirstOrDefault(row => row.S("id") == id)?.S("nome") ?? id;
                    images.Add(new("SLE " + ConcreteEditor.Label(key) + " · governante " + reason + " · " + name + " · " + view.Contour, view.Png(), key));
                }
            }
            if (options.Contains("taglio"))
            {
                var view = new ConcreteSectionViewport { Section = graphics.Preview.Section, Stirrups = ShearOptions };
                images.Add(new("Schema indicativo delle staffe · diametro rappresentato in scala", view.Png(), "taglio"));
            }
            if(options.Contains("curvatura")&&graphics.CurvaturePlot is not null)images.Add(new("Momento–curvatura · percorso assegnato",graphics.CurvaturePlot.Png(),"curvatura"));
        }
        return images;
    }
}
