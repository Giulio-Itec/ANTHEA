using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Presentation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.ConcreteSection;

namespace Anthea.Presentation.Tests;

/// <summary>
/// The state of the RC section sheet (F5.10) read from <see cref="ConcreteSheetViewModel"/> in the format of the historic trace
/// (tests/Desktop/ConcreteSectionHistoricState.cs): the common part and the parts «[ingressi]», «[azioni]», «[domini]», «[sle]», «[taglio]»,
/// «[dettagli]», «[ispezione]», «[armature]» and «[wizard]», with the model of the layout of the historic window (the forms laid out so far) that
/// decides which fields the user can use. Shared by the two sides that replay the frozen scenarios: Presentation.Tests (the ViewModel driven without
/// WPF, ConcreteSectionSheetChecks) and UiTests (the XAML view driven through its controls, ConcreteSectionViewChecks: the common part from the
/// controls of the view, the forms, the tables and the panels from the ViewModel, MU19). Moved here from ConcreteSectionSheetChecks without changes
/// to the lines it writes.
/// </summary>
internal sealed class ConcreteSectionSheetReader(Func<ConcreteSheetViewModel> sheet)
{
    /// <summary>The parts of the sheet read here: every part of the sub-steps S1–S9 and the part of the wizard (S1b).</summary>
    internal static readonly string[] Parts = [.. ConcreteSectionScenarios.AllSections, ConcreteSectionScenarios.WizardSection];

    // WPF does not pass the disabled state of a form to its fields until the form is laid out once (the historic trace runs off screen):
    // a form in a closed expander, in a tab not shown or hidden itself keeps its fields enabled. The forms laid out so far: «geometria»
    // and «foro» (group «Geometria», open), «armatura» and the second layers (group «Armature», closed until the user opens it).
    private readonly HashSet<string> laidOut = new(StringComparer.Ordinal);

    internal ConcreteSheetViewModel Sheet => sheet();

    private ConcreteInputViewModel Inputs => Sheet.Inputs;

    /// <summary>A sheet opened again: nothing laid out yet but what the opening lays out.</summary>
    internal void Reset() { laidOut.Clear(); Observe(); }

    /// <summary>What the layout of the historic window lays out after a step: the forms shown in the open groups of the control panel.</summary>
    internal void Observe()
    {
        if (Sheet.SelectedTab != 0) return;
        if (Inputs.Groups.First(g => g.Title == "Geometria").IsExpanded)
        {
            if (Inputs.GeometryShown) laidOut.Add("geometria");
            if (Inputs.HoleFieldsVisible) laidOut.Add("foro");
        }
        if (!Inputs.Groups.First(g => g.Title == "Armature").IsExpanded) return;
        if (Inputs.ReinforcementShown) laidOut.Add("armatura");
        foreach (var layer in Inputs.Layers) if (layer.IsShown && layer.FormVisible) laidOut.Add("strato-" + layer.Key);
    }

    // ---------------------------------------------------------------- the forms named by the steps

    /// <summary>The form named by a step (<see cref="ConcreteSectionStep.Target"/>; the form of the detailing that holds <paramref name="key"/>).</summary>
    internal FormViewModel? Form(string target, string? key = null) => target switch
    {
        "normativa" => Inputs.Standard,
        "coefficienti" => Inputs.Coefficients,
        "materiali" => Inputs.Materials,
        "geometria" => Inputs.Geometry,
        "foro" => Inputs.Hole,
        "armatura" => Inputs.Reinforcement,
        "strato-top" or "strato-bottom" or "strato-inner" => Inputs.Layers.Single(l => l.Key == target[7..]).Form,
        "staffe" => Inputs.Stirrups,
        "schema-staffe" => Inputs.StirrupScheme,
        "cavo" => Inputs.Cable,
        "trefolo" => Inputs.TendonProperties,
        "rapide" => Inputs.QuickResistance,
        "3D" => Sheet.Domain3D.Form,
        "2D" => Sheet.Domain2D.Form,
        "SLE" or "SLE_FREQ" or "SLE_QP" => Sheet.StressPanel(target).Form,
        "taglio" => Sheet.Shear.Form,
        "torsione" => Sheet.Shear.Torsion,
        "curvatura" => Sheet.Curvature.Form,
        "ricerca" => Sheet.Design.Form,
        "limiti" => Sheet.Design.Limits,
        "mostra" => Sheet.Design.Shown,
        "dettagli" => Sheet.Detailing.Forms.Single(f => f.Contains(key!)),
        "ancoraggi" => Sheet.Detailing.Anchorage,
        _ => null
    };

    /// <summary>Whether the form is shown (its own visibility and that of its containers).</summary>
    internal bool FormShown(FormViewModel form)
    {
        if (ReferenceEquals(form, Inputs.Hole)) return Inputs.HoleFieldsVisible;
        if (ReferenceEquals(form, Inputs.Geometry)) return Inputs.GeometryShown;
        if (ReferenceEquals(form, Inputs.Reinforcement)) return Inputs.ReinforcementShown;
        var layer = Inputs.Layers.FirstOrDefault(l => ReferenceEquals(l.Form, form));
        return layer is null || layer.IsShown && layer.FormVisible;
    }

    /// <summary>
    /// The fields of a form disabled as a whole, once laid out: the geometry and the hole of a section of the wizard, the reinforcement
    /// locked by the coordinates of the bars or by the wizard, the locked layers.
    /// </summary>
    internal bool FormDisabled(FormViewModel form)
    {
        if (ReferenceEquals(form, Inputs.Geometry)) return !Inputs.GeometryEnabled && laidOut.Contains("geometria");
        if (ReferenceEquals(form, Inputs.Hole)) return !Inputs.HoleFormEnabled && laidOut.Contains("foro");
        if (ReferenceEquals(form, Inputs.Reinforcement)) return !Inputs.ReinforcementEnabled && laidOut.Contains("armatura");
        var layer = Inputs.Layers.FirstOrDefault(l => ReferenceEquals(l.Form, form));
        return layer is not null && !layer.FormEnabled && laidOut.Contains("strato-" + layer.Key);
    }

    /// <summary>A field as the user sees it: enabled (choices and check boxes disabled in a read-only revision), visible.</summary>
    internal bool FieldEnabled(FormViewModel form, FieldViewModel field) => field.IsEnabled && !FormDisabled(form) && (field.Kind == FieldKind.Text || field.CanChange);

    internal bool Usable(FormViewModel form, FieldViewModel field) => FieldEnabled(form, field) && field.IsVisible && FormShown(form);

    // ---------------------------------------------------------------- state

    /// <summary>
    /// The state after a step: the common part (from <paramref name="view"/> when the view reads it from its controls, otherwise from the
    /// ViewModel), the message of the step, then the parts asked by the scenario.
    /// </summary>
    internal ConcreteSectionTraceState Read(IReadOnlyList<string> sections, string? message, int modified, ConcreteSectionCommonPart? view = null)
    {
        Observe();
        var common = view ?? new ConcreteSectionCommonPart(Sheet.Status, Sheet.SelectedTab, Sheet.IsProgressVisible, !Inputs.Geometry["width_mm"].IsInputReadOnly);
        var state = new ConcreteSectionTraceState
        {
            Data = Sheet.Data, Modified = modified, Busy = Sheet.IsBusy, Queued = Sheet.IsCalculationQueued, HasResults = Sheet.HasResults, Result = Sheet.Result,
            Status = common.Status, Progress = common.Progress, Tab = common.Tab, Editable = common.Editable, Message = message,
        };
        foreach (string section in sections.Where(Parts.Contains))
        {
            var block = new ConcreteSectionBlock(section);
            if (section == "azioni") TraceActions(block);
            else if (section == "domini") TraceDomains(block);
            else if (section == "sle") TraceServiceability(block);
            else if (section == "taglio") TraceShear(block);
            else if (section == "dettagli") TraceDetailing(block);
            else if (section == "ispezione") TraceInspection(block);
            else if (section == "armature") TraceDesign(block);
            else if (section == ConcreteSectionScenarios.WizardSection) TraceWizard(block);
            else TraceInputs(block);
            state.Blocks.Add(block);
        }
        return state;
    }

    private ConcreteFormState FormState(string name, FormViewModel form)
    {
        var fields = form.Fields.Select(field =>
        {
            bool enabled = FieldEnabled(form, field);
            string? note = field.IsNoteVisible ? field.Note : null;
            return field.Kind switch
            {
                FieldKind.Text => new ConcreteFieldState(field.Key, "campo", field.Label, form.Get(field.Key), null, enabled, field.IsDimmed, field.IsVisible, field.IsInputReadOnly, note),
                FieldKind.Choice => new ConcreteFieldState(field.Key, "scelta", field.Label, form.Get(field.Key), field.Choices, enabled, field.IsDimmed, field.IsVisible, false, note),
                _ => new ConcreteFieldState(field.Key, "opzione", field.Label, ConcreteSectionTrace.YesNo(field.IsChecked), null, enabled, field.IsDimmed, field.IsVisible, false, note)
            };
        }).ToList();
        var groups = form.Groups.Select(g => (g.Title, (IReadOnlyList<string>)g.Fields.Select(f => f.Key).ToList(), g.IsExpanded)).ToList();
        return new ConcreteFormState(name, fields, groups);
    }

    private static ConcreteTableState TableState(string name, ConcreteTableViewModel table)
    {
        // R-1162-MAT: project only the historical UI contract. The new columns and real edits have dedicated VM/WPF checks;
        // the frozen expectations and their calculation values remain untouched.
        var columns = table.Columns.Where(c => c.IsVisible && !(name == "barre" && c.Key == "materiale"));
        if (name == "trefoli")
        {
            string[] order = ["id", "x", "y", "diametro", "area", "materiale", "diagramma", "sigma0", "Ep", "fpyk", "fpk", "eps_u"];
            columns = columns.OrderBy(c => Array.IndexOf(order, c.Key));
        }
        var rows = table.Shown.Select(r => (JsonObject)r.Values.DeepClone()).ToList();
        if (name == "barre") foreach (var row in rows) row.Remove("materiale");
        return new(name, columns.Select(c => (name == "trefoli" && c.Key == "sigma0" ? "σp0 [MPa]" : c.Header) + "=" + c.Key).ToList(), rows,
            table.SelectedIndex, !table.IsReadOnly, table.Filter.Description, table.Rows.Count);
    }

    private static string Colour(ConcreteCard card) => "#FF" + (card.Tone switch
    {
        ConcreteCardTone.Passed => "#39A879",
        ConcreteCardTone.Failed => UtilizationColors.Color(2),
        ConcreteCardTone.Utilization => UtilizationColors.Color(card.Ratio),
        ConcreteCardTone.Information => "#0B5CAD",
        _ => UtilizationColors.Color(null)
    })[1..].ToUpperInvariant();

    private static ConcreteCardsState Cards(string name, ConcreteCardsViewModel cards) => new(name, cards.Text, cards.Cards.Select(Colour).ToList());

    private static string Viewport(ConcreteSectionDrawingViewModel view) =>
        "sezione " + ConcreteSectionTrace.Section(view.Section) + " · trefoli " + ConcreteSectionTrace.Hash(view.Tendons.Select(t => new object[] { t.Id, t.X, t.Y, t.Area }).ToArray())
        + " · messaggio " + ConcreteSectionTrace.Quote(view.Message) + " · tensioni " + StressState(view.Stress) + " · contorno " + ConcreteSectionTrace.Quote(view.Contour)
        + " · opzioni " + string.Concat(new[] { view.Labels, view.Axes, view.ShowNeutralAxis, view.BarValues, view.TendonValues, view.ConcreteValues, view.Dimensions, view.CoverDimensions,
            view.SpacingDimensions, view.Stirrups is not null }.Select(b => b ? "1" : "0"))
        + " · barra " + ConcreteSectionTrace.Quote(view.SelectedBar) + " · zona " + ConcreteSectionTrace.Quote(view.EffectiveRegion?.Name);

    // The stress state of a map as the historic trace writes it (TraceStressState).
    private static string StressState(ConcreteResults.StressState? stress) => stress is null ? "-" :
        ConcreteSectionTrace.Hash(new object?[] { stress.sigma_cls, stress.sigma_acciaio, stress.tensioni_barre, stress.BarStrains, stress.Status, stress.Ratio });

    // ---------------------------------------------------------------- [wizard] and [ingressi] (S1b, S1)

    // The part «[wizard]» (S1b): the command, the editor hidden or disabled with the notes of the wizard, the hole, the note of the shear.
    private void TraceWizard(ConcreteSectionBlock block)
    {
        block.Add("comando " + ConcreteSectionTrace.Quote(Inputs.WizardCommandText) + (Inputs.OpenWizardCommand.CanExecute(null) ? "" : " · spento"));
        block.Add("geometria" + (Inputs.GeometryShown ? "" : " · nascosta") + (Inputs.GeometryEnabled ? "" : " · spenta") + " · nota "
            + ConcreteSectionTrace.Quote(Inputs.TemplateGeometryNote) + (Inputs.TemplateNotesShown ? "" : " · nascosta"));
        block.Add("armatura" + (Inputs.ReinforcementShown ? "" : " · nascosta") + " · nota " + ConcreteSectionTrace.Quote(Inputs.TemplateReinforcementNote)
            + (Inputs.TemplateNotesShown ? "" : " · nascosta"));
        block.Add("foro campi" + (Inputs.HoleFormEnabled ? "" : " · spenti"));
        block.Text("nota-taglio", Sheet.ShearAutomaticNote);
    }

    private void TraceInputs(ConcreteSectionBlock block)
    {
        block.Text("nota-normativa", Inputs.StandardNote);
        block.Text("valori-integrati", Inputs.MigratedInput);
        foreach (var (name, form) in new[] { ("normativa", Inputs.Standard), ("coefficienti", Inputs.Coefficients), ("materiali", Inputs.Materials), ("geometria", Inputs.Geometry) })
            block.Form(FormState(name, form));
        block.Add("foro " + ConcreteSectionTrace.YesNo(Inputs.HoleChecked) + (Inputs.ToggleHoleCommand.CanExecute(null) ? "" : " · spento") + (Inputs.HoleFieldsVisible ? "" : " · campi nascosti"));
        block.Form(FormState("foro", Inputs.Hole));
        block.Add("armatura " + (Inputs.ReinforcementEnabled ? "attiva" : "spenta") + " · nota " + ConcreteSectionTrace.Quote(Inputs.ReinforcementToolTip));
        block.Form(FormState("armatura", Inputs.Reinforcement));
        foreach (var layer in Inputs.Layers)
        {
            block.Add("strato " + layer.Key + (layer.IsShown ? "" : " · nascosto") + " · " + ConcreteSectionTrace.Quote(layer.ToggleText)
                + (layer.ToggleCommand.CanExecute(null) ? "" : " · spento") + " · nota " + ConcreteSectionTrace.Quote(layer.ToggleToolTip) + (layer.FormVisible ? "" : " · campi nascosti"));
            block.Form(FormState("strato-" + layer.Key, layer.Form));
        }
        block.Form(FormState("staffe", Inputs.Stirrups)); block.Form(FormState("schema-staffe", Inputs.StirrupScheme));
        block.Form(FormState("cavo", Inputs.Cable));
        block.Add("cavo materiale " + ConcreteSectionTrace.Quote(Inputs.CableMaterial.SelectedItem) + " di " + ConcreteSectionTrace.Quote(string.Join("|", Inputs.CableMaterial.Items))
            + " · messaggio " + ConcreteSectionTrace.Quote(Inputs.CableMessage));
        foreach (string line in TableState("trefoli", Inputs.Tendons).Lines()) block.Add(line);
        block.Add("trefoli materiali " + ConcreteSectionTrace.Quote(string.Join("|", Inputs.Tendons.Columns.First(c => c.Key == "materiale").Choices)));
        block.Add("trefolo standard " + ConcreteSectionTrace.Quote(Inputs.StandardTendon.SelectedItem) + " di " + ConcreteSectionTrace.Quote(string.Join("|", Inputs.StandardTendon.Items)));
        block.Form(FormState("trefolo", Inputs.TendonProperties));
        block.Add("materiali salvati " + Inputs.SavedMaterials.SelectedIndex.ToString(CultureInfo.InvariantCulture) + " di " + ConcreteSectionTrace.Quote(string.Join("|", Inputs.SavedMaterials.Items)));
        block.Form(FormState("rapide", Inputs.QuickResistance));
        block.Add("rapide " + ConcreteSectionTrace.Quote(Inputs.QuickResistanceText) + (Inputs.QuickResistanceTextVisible ? "" : " · nascosto") + " · riquadri "
            + ConcreteSectionTrace.Quote(string.Join(" | ", Inputs.QuickResistanceCards.Select(c => c.Direction + " " + c.Value))));
        block.Add("anteprima " + Viewport(Inputs.Drawing));
        foreach (string line in TableState("barre", Inputs.Bars).Lines()) block.Add(line);
        foreach (var (key, cards) in Sheet.Summaries) block.Add(Cards("riepilogo-" + key, cards).Line());
        block.Add(Cards("riepilogo-taglio", Sheet.ShearDashboard).Line());
    }

    // ---------------------------------------------------------------- [azioni] (S2)

    /// <summary>The part «[azioni]»: unified or historic actions, the file of the actions shown by the tables, the rows of every limit state
    /// and of the shear with every value (inputs and outputs).</summary>
    private void TraceActions(ConcreteSectionBlock block)
    {
        var actions = Sheet.Actions;
        // The tables of the historic actions show the file next to their commands; the unified ones show no file.
        block.Add("sollecitazioni uniche " + ConcreteSectionTrace.YesNo(SectionActionInputs.Unified(Sheet.Data)) + " · file "
            + ConcreteSectionTrace.Quote(actions.Unified ? "" : actions.ExcelPath));
        foreach (var (key, rows) in Sheet.ActionRowSets)
        {
            block.Add("azioni " + key + " · " + rows.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var row in rows) block.Add("  " + ConcreteSectionTrace.Canonical(row.Values));
        }
        block.Add("azioni taglio · " + Sheet.ShearRowCollection.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var row in Sheet.ShearRowCollection) block.Add("  " + ConcreteSectionTrace.Canonical(row.Values));
    }

    /// <summary>What the window «Sollecitazioni della sezione» shows: rows, selection, status, origins, notice (shown with the origins), previous
    /// actions, result, rows.</summary>
    internal static string EditorState(SectionActionsViewModel editor) =>
        "righe " + editor.Rows.Count.ToString(CultureInfo.InvariantCulture) + " · selezionata "
        + (editor.Selected is null ? -1 : editor.Rows.IndexOf(editor.Selected)).ToString(CultureInfo.InvariantCulture)
        + " · stato " + ConcreteSectionTrace.Quote(editor.Status) + " · origini " + ConcreteSectionTrace.YesNo(editor.HasOrigins)
        + " · avviso " + ConcreteSectionTrace.YesNo(editor.HasOrigins) + " · precedente " + ConcreteSectionTrace.YesNo(editor.Legacy)
        + " · risultato " + (editor.Result is null ? "-" : ConcreteSectionTrace.StableHash(editor.Result))
        + "\n" + string.Join("\n", editor.Rows.Select(r => "  " + string.Join(" | ", new[] { r.Id, r.Element, r.Name, r.State, r.N, r.Vx, r.Vy, r.T, r.Mx, r.My, r.Origin }
            .Select(ConcreteSectionTrace.Quote)) + (r.Visible ? "" : " · nascosta")));

    /// <summary>What the window «Nuovo materiale · …» shows: every field, the error and the derived values, the curves of the diagram.</summary>
    internal static string MaterialState(ConcreteMaterialDialogViewModel dialog)
    {
        var texts = (dialog.Error.Length > 0 ? new[] { dialog.Error } : []).Append(dialog.Derived);
        return string.Join(" · ", dialog.Form.Fields.Select(f => f.Key + "=" + ConcreteSectionTrace.Quote(dialog.Form.Get(f.Key))))
            + " · testi " + ConcreteSectionTrace.Quote(string.Join(" ⏎ ", texts))
            + " · curve " + string.Join(" ", dialog.Curves.Select(c => ConcreteSectionTrace.Quote(c.Name) + " " + c.Points.Count.ToString(CultureInfo.InvariantCulture) + " " + ConcreteSectionTrace.Hash(c.Points)));
    }

    // ---------------------------------------------------------------- [domini] (S3)

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

    /// <summary>The 3D domain as the historic trace writes its viewport (DomainViewport3D.TraceDataForChecks).</summary>
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

    // ---------------------------------------------------------------- [sle] (S4)

    /// <summary>The part «[sle]»: the family shown, then for every family form, table, details, tables of the bars and of the concrete, zones,
    /// map and summary.</summary>
    private void TraceServiceability(ConcreteSectionBlock block)
    {
        block.Add("famiglia " + Sheet.StressFamily.ToString(CultureInfo.InvariantCulture));
        foreach (var panel in Sheet.StressPanels)
        {
            string key = panel.Key;
            block.Form(FormState(key, panel.Form));
            foreach (string line in TableState(key, panel.Table.Table).Lines()) block.Add(line);
            block.Add(key + " schede " + panel.DetailTab.ToString(CultureInfo.InvariantCulture) + " · fessurazione " + (panel.CrackTabVisible ? "mostrata" : "nascosta"));
            block.Text(key + " dettaglio", panel.Detail);
            block.Text(key + " passaggi", panel.CrackDetail);
            foreach (string line in TableState(key + "-barre", panel.Bars).Lines()) block.Add(line);
            foreach (string line in TableState(key + "-cls", panel.Concrete).Lines()) block.Add(line);
            block.Add(key + " zone " + panel.SelectedRegion.ToString(CultureInfo.InvariantCulture) + " di " + panel.Regions.Count.ToString(CultureInfo.InvariantCulture));
            block.Add(key + " mappa " + Viewport(panel.Map) + " · legenda " + ConcreteSectionTrace.Quote(panel.Map.Legend));
            block.Add(Cards(key + "-riepilogo", panel.Summary).Line());
        }
    }

    // ---------------------------------------------------------------- [taglio] (S5)

    /// <summary>
    /// The part «[taglio]»: the forms of the shear and of the torsion, the four forms of the links shown read-only (tab 05, then tab 06), the note
    /// of the automatic parameters, the table with its commands, the detail, the summary, the summary of the checks and the section drawn.
    /// </summary>
    private void TraceShear(ConcreteSectionBlock block)
    {
        var shear = Sheet.Shear;
        block.Form(FormState("taglio", shear.Form)); block.Form(FormState("torsione", shear.Torsion));
        var stirrups = new[] { shear.Stirrups.Values, shear.Stirrups.Scheme, Sheet.Detailing.Stirrups.Values, Sheet.Detailing.Stirrups.Scheme };
        for (int i = 0; i < stirrups.Length; i++) block.Form(FormState("staffe-" + (i + 2).ToString(CultureInfo.InvariantCulture), stirrups[i]));
        block.Text("nota-automatici", shear.AutomaticNote);
        foreach (string line in TableState("taglio", shear.Table.Table).Lines()) block.Add(line);
        block.Add("taglio comandi " + string.Join(" ", ShearCommands(shear.Table)));
        block.Text("taglio dettaglio", shear.Detail);
        block.Text("taglio sintesi", shear.Summary);
        block.Add(Cards("taglio-peggiori", shear.Worst).Line());
        block.Add("taglio sezione " + Viewport(shear.Drawing));
    }

    // The buttons of the panel of the table, in the order of the historic panel (the bar of the table, then «×» of the filter bar, which the
    // read-only revision leaves enabled); «(spento)» when the command is not available.
    private static IEnumerable<string> ShearCommands(ConcreteActionTableViewModel table)
    {
        static string Button(string text, bool enabled) => text + (enabled ? "" : "(spento)");
        if (table.Unified) yield return Button("Gestisci sollecitazioni…", table.ImportCommand.CanExecute(null));
        else
        {
            yield return Button("+ Combinazione", table.AddCommand.CanExecute(null));
            yield return Button("−", table.RemoveCommand.CanExecute(null));
            yield return Button("Copia", table.CopyCommand.CanExecute(null));
            yield return Button("Incolla", table.PasteCommand.CanExecute(null));
            yield return Button("Template Excel", table.TemplateCommand.CanExecute(null));
            yield return Button("Importa Excel", table.ImportCommand.CanExecute(null));
            yield return Button("Esporta Excel", table.ExportCommand.CanExecute(null));
            yield return Button("Sfoglia…", table.BrowseCommand.CanExecute(null));
        }
        yield return "×";
    }

    // ---------------------------------------------------------------- [dettagli] (S6)

    // The forms of the detailing in the order of the historic view, the cover of the inputs, the anchorage, the summary, the topics, the
    // checks of the anchorage and the note of the durability.
    private void TraceDetailing(ConcreteSectionBlock block)
    {
        var detailing = Sheet.Detailing;
        for (int i = 0; i < detailing.Forms.Count; i++) block.Form(FormState("dettagli-" + i.ToString(CultureInfo.InvariantCulture), detailing.Forms[i]));
        block.Form(FormState("copriferro", detailing.CoverGeometry));
        block.Form(FormState("ancoraggi", detailing.Anchorage));
        block.Add(Cards("dettagli", detailing.Summary).Line());
        foreach (var topic in detailing.Topics) block.Add(Cards("dettagli-" + topic.Key, topic.Checks).Line());
        block.Add(Cards("ancoraggi", detailing.AnchorageChecks).Line());
        block.Text("durabilita", detailing.DurabilityNote);
    }

    // ---------------------------------------------------------------- [ispezione] (S7)

    /// <summary>The part «[ispezione]»: for 3D and 2D the tab, the solution, the message, the values, the map and the plane; then the curve.</summary>
    private void TraceInspection(ConcreteSectionBlock block)
    {
        foreach (var panel in new[] { Sheet.Domain3D, Sheet.Domain2D })
        {
            var s = panel.Inspection; string name = panel.ThreeD ? "3D" : "2D";
            block.Add(name + " ispezione scheda " + panel.InspectionTab.ToString(CultureInfo.InvariantCulture) + " · " + ConcreteSectionTrace.Quote(s.StateText)
                + " · messaggio " + ConcreteSectionTrace.Quote(s.Message));
            block.Text(name + " ispezione valori", s.Values);
            block.Add(name + " ispezione mappa " + Viewport(s.Map));
            block.Add(name + " ispezione piano " + (s.Plane is not { } plane ? "-" : ConcreteSectionTrace.Hash(new object[] { plane.StrainReferencePoint, plane.ChiX, plane.ChiY,
                plane.Outline.Select(v => new[] { v.X, v.Y, v.Strain }).ToArray() })));
        }
        var curvature = Sheet.Curvature;
        block.Form(FormState("curvatura", curvature.Form));
        block.Text("curvatura testo", curvature.Text);
        block.Add("curvatura grafico " + CurveLine(curvature.Chart));
    }

    // The chart of the curve as the historic trace writes its Plot (no segment, no marker, no note).
    private static string CurveLine(ConcreteCurvatureChartViewModel chart)
    {
        var series = chart.Points.Count == 0 ? [] : new[] { (Name: chart.SeriesName, Points: chart.Points) };
        return ConcreteSectionTrace.Quote(chart.Title) + " · " + ConcreteSectionTrace.Quote(chart.XLabel) + " · " + ConcreteSectionTrace.Quote(chart.YLabel) + " · nota " + ConcreteSectionTrace.Quote("")
            + " · vuoto " + ConcreteSectionTrace.Quote(chart.EmptyMessage) + " · serie " + string.Join(" ", series.Select(s => ConcreteSectionTrace.Quote(s.Name) + ":" + s.Points.Count.ToString(CultureInfo.InvariantCulture)))
            + " " + ConcreteSectionTrace.Hash(series.Select(s => s.Points).ToArray())
            + " · segmenti 0 " + ConcreteSectionTrace.Hash(Array.Empty<object>()) + " · verifiche 0 " + ConcreteSectionTrace.Hash(Array.Empty<object>())
            + " · punti 0 " + ConcreteSectionTrace.Hash(Array.Empty<object>()) + " · scala 1/1";
    }

    // ---------------------------------------------------------------- [armature] (S8)

    /// <summary>The part «[armature]»: the three forms, the commands, the texts, the cards, the alternatives and the section of the proposal.</summary>
    private void TraceDesign(ConcreteSectionBlock block)
    {
        var design = Sheet.Design;
        block.Form(FormState("ricerca", design.Form)); block.Form(FormState("limiti", design.Limits)); block.Form(FormState("mostra", design.Shown));
        static string Button(string text, bool enabled) => text + (enabled ? "" : "(spento)");
        block.Add("ricerca comandi " + string.Join(" ", Button("Calcola armature", design.RunCommand.CanExecute(null)), Button("Ferma ricerca", design.StopCommand.CanExecute(null)),
            Button("Applica configurazione", design.ApplyCommand.CanExecute(null))));
        block.Text("ricerca stato", ConcreteSectionTrace.WithoutTimes(design.Status));
        block.Text("ricerca proposta", design.Description);
        block.Text("ricerca ambito", design.Scope);
        block.Add(Cards("ricerca-metriche", design.Metrics).Line());
        block.Add(Cards("ricerca-verifiche", design.Checks).Line());
        block.Text("ricerca scarti", ConcreteSectionTrace.WithoutTimes(design.Diagnostics));
        foreach (string line in TableState("alternative", design.Ranking).Lines()) block.Add(line);
        block.Add("ricerca anteprima " + Viewport(design.Preview));
    }
}

/// <summary>The common part of the state that the view side reads from the controls of the view: the status line, the main tab, the progress bar,
/// whether the inputs accept typing.</summary>
internal sealed record ConcreteSectionCommonPart(string Status, int Tab, bool Progress, bool Editable);
