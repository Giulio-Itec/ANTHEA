using System.Globalization;
using Anthea.Application.Concrete;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The steps of the domains (F5.10, sub-step S3) driven on <see cref="ConcreteDomainPanelViewModel"/> as the historic trace drives the panels of
/// the tabs 02 and 03 (tests/Desktop/ConcreteSectionHistoricState.cs): the bar of the drawing (forces, check boxes, sliders, mesh, view), the
/// section on the selected action, the filters of the tables; and the part «[domini]» of the state, in the format of the historic trace (the 3D
/// domain as plain data, the chart of the 2D domain as the Plot of the historic panel).
/// </summary>
internal static partial class ConcreteSectionSheetChecks
{
    private sealed partial class Side
    {
        private ConcreteDomainPanelViewModel DomainPanel(string target) => target == "3D" ? Sheet.Domain3D : Sheet.Domain2D;

        /// <summary>A step on the bar or on the table of a domain panel; null when the step is not one.</summary>
        private (string? Message, bool Own)? ApplyDomainStep(ConcreteSectionStep step)
        {
            if (step.Target is not ("3D" or "2D")) return null;
            var panel = DomainPanel(step.Target);
            switch (step.Kind)
            {
                case ConcreteSectionStep.Toggle:
                {
                    // The check boxes of the bar of the drawing and «Asse neutro» stay usable in a read-only revision; «Valori σ/ε sulle barre» does not.
                    var option = panel.DisplayOptions.FirstOrDefault(o => o.Label == step.Key);
                    if (option is not null) option.IsChecked = !option.IsChecked;
                    else if (step.Key == "Asse neutro") panel.NeutralAxis = !panel.NeutralAxis;
                    else if (step.Key == "Valori σ/ε sulle barre") panel.BarValues = !panel.BarValues;
                    else throw new InvalidOperationException("Casella non trovata: " + step.Key);
                    return (null, true);
                }
                case ConcreteSectionStep.Button:
                {
                    var command = step.Key switch
                    {
                        "Mesh" => panel.MeshCommand,
                        "Sezione sull’azione selezionata" => panel.SectionOnActionCommand,
                        _ when step.Key!.StartsWith("Forze", StringComparison.Ordinal) => panel.ForceCommand,
                        _ => throw new InvalidOperationException("Pulsante non trovato: " + step.Key)
                    };
                    if (command.CanExecute(null)) command.Execute(null);
                    return (null, true);
                }
                // The view of the 3D domain moves only the camera.
                case ConcreteSectionStep.Pick when step.Key == "vista": return (null, true);
                case ConcreteSectionStep.Pick when step.Key == "stato":
                    return (ChooseInForm(panel, step), true);
                case ConcreteSectionStep.Slider:
                {
                    double value = double.Parse(step.Text!, CultureInfo.InvariantCulture);
                    if (step.Key == "trasparenza") panel.Transparency = value;
                    else if (step.Key == "dimensione_ed") panel.ActionPointSize = value;
                    else panel.ResistancePointSize = value;
                    return (null, true);
                }
                case ConcreteSectionStep.Filter:
                {
                    // The bar of the filter: column, mode, order and text, each applied when it changes (allowed in a read-only revision).
                    var filter = panel.Table.Table.Filter;
                    filter.Column = step.Key!;
                    filter.Mode = step.Index; filter.Order = step.Row; filter.Query = step.Text ?? "";
                    return (null, true);
                }
                default: return null;
            }
        }

        // The choice of the form of a panel, as the combo box of the historic form (Pick «stato» is the same control).
        private string? ChooseInForm(ConcreteDomainPanelViewModel panel, ConcreteSectionStep step)
        {
            var form = panel.Form;
            if (form.Contains(step.Key!) && form[step.Key!] is { Kind: FieldKind.Choice } field && Usable(form, field)) field.SelectedChoice = step.Text;
            return null;
        }

        /// <summary>The part «[domini]»: for 3D and 2D the form, the table, the detail, the summary, the bar, the drawing.</summary>
        private void TraceDomains(ConcreteSectionBlock block)
        {
            foreach (var panel in new[] { Sheet.Domain3D, Sheet.Domain2D })
            {
                string name = panel.ThreeD ? "3D" : "2D";
                block.Form(FormState(name, panel.Form));
                foreach (string line in TableState(name, panel.Table.Table).Lines()) block.Add(line);
                block.Text(name + " dettaglio", panel.Detail);
                block.Add(Cards(name + "-riepilogo", panel.Summary).Line());
                static string Box(string label, bool value, bool enabled) => label + "=" + ConcreteSectionTrace.YesNo(value) + (enabled ? "" : "(spento)");
                var boxes = panel.DisplayOptions.Select(o => Box(o.Label, o.IsChecked, true))
                    .Concat([Box("Asse neutro", panel.NeutralAxis, true), Box("Valori σ/ε sulle barre", panel.BarValues, Sheet.CanEdit)]);
                string Slider(double value, bool enabled) => ConcreteSectionTrace.Number(value) + (enabled ? "" : "(spento)");
                var sliders = new List<string> { Slider(panel.ActionPointSize, Sheet.CanEdit), Slider(panel.ResistancePointSize, Sheet.CanEdit) };
                if (panel.ThreeD) sliders.Add(Slider(panel.Transparency, true));
                block.Add(name + " comandi " + ConcreteSectionTrace.Quote(panel.ForceText) + " · " + string.Join(" ", boxes) + " · cursori " + string.Join(" ", sliders));
                if (panel.ThreeD) foreach (string line in View3DLines(panel.View3D!)) block.Add("3D vista " + line);
                else block.Add("2D grafico " + ChartLine(panel.Chart!));
            }
        }

        // The 3D domain as the historic trace writes its viewport (DomainViewport3D.TraceDataForChecks).
        internal static IEnumerable<string> View3DLines(ConcreteDomain3DViewModel view)
        {
            var mesh = view.Mesh;
            yield return "mesh " + (mesh is null ? "-" : mesh.Mode + " vertici " + mesh.Vertices.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(mesh.Vertices.Select(v => new[] { v.N, v.Mx, v.My }).ToArray())
                + " triangoli " + view.TriangleCount.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(mesh.Triangles) + " scala " + ConcreteSectionTrace.Hash(new[] { mesh.Scale.N, mesh.Scale.Mx, mesh.Scale.My }));
            var points = view.Actions;
            int selected = view.SelectedId is null ? -1 : points.ToList().FindIndex(p => p.Id == view.SelectedId);
            yield return "azioni " + points.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(points.Select(p => new object[] { p.Force.N, p.Force.Mx, p.Force.My, p.Pass }).ToArray())
                + " · selezionata " + (view.SelectedId is null ? "-" : selected.ToString(CultureInfo.InvariantCulture))
                + " · Ed " + (view.SelectedAction is { } a ? ConcreteSectionTrace.Hash(new[] { a.N, a.Mx, a.My }) : "-") + " · Rd " + (view.SelectedResistance is { } r ? ConcreteSectionTrace.Hash(new[] { r.N, r.Mx, r.My }) : "-");
            yield return "opzioni " + string.Concat(new[] { view.ShowActions, view.OnlySelectedActions, view.ShowResistance, view.ShowVerificationLines, view.ColorByRatio, view.Wireframe, view.FitIncludesActions }.Select(b => b ? "1" : "0"))
                + " · criterio " + ConcreteSectionTrace.Quote(view.VerificationCriterion) + " · punti " + ConcreteSectionTrace.Number(view.ActionPointSize) + "/" + ConcreteSectionTrace.Number(view.ResistancePointSize)
                + " · opacità " + ConcreteSectionTrace.Number(view.SurfaceOpacity) + " · scale " + ConcreteSectionTrace.Number(view.Scale.Mx) + "/" + ConcreteSectionTrace.Number(view.Scale.N) + "/" + ConcreteSectionTrace.Number(view.Scale.My)
                + " · tassi " + (view.Ratios is null ? "-" : view.Ratios.Count.ToString(CultureInfo.InvariantCulture)) + " · resistenze " + (view.Resistances is null ? "-" : view.Resistances.Count.ToString(CultureInfo.InvariantCulture))
                + " · nota " + ConcreteSectionTrace.Quote(view.Note);
        }

        // The chart of the 2D domain as the historic trace writes its Plot (the colours as the brushes of the markers write them).
        private static string ChartLine(ConcreteDomain2DViewModel chart) =>
            ConcreteSectionTrace.Quote(chart.Title) + " · " + ConcreteSectionTrace.Quote(chart.XLabel) + " · " + ConcreteSectionTrace.Quote(chart.YLabel) + " · nota " + ConcreteSectionTrace.Quote(chart.Note)
            + " · vuoto " + ConcreteSectionTrace.Quote(chart.EmptyMessage) + " · serie  " + ConcreteSectionTrace.Hash(Array.Empty<object>())
            + " · segmenti " + chart.Segments.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(chart.Segments.Select(s => new[] { s.A, s.B }).ToArray())
            + " · verifiche " + chart.VerificationSegments.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(chart.VerificationSegments.Select(s => new[] { s.A, s.B }).ToArray())
            + " · punti " + chart.Markers.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(chart.Markers.Select(m => new object?[] { m.X, m.Y, m.Label, "#FF" + m.Color[1..].ToUpperInvariant(), m.Radius }).ToArray())
            + " · scala " + ConcreteSectionTrace.Number(chart.ScaleX) + "/" + ConcreteSectionTrace.Number(chart.ScaleY);
    }
}
