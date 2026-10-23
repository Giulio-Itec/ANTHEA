using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.CompositeSection;
using X.Core;
using X.Desktop.Views.CompositeSection;

namespace X.Desktop;

/// <summary>
/// F5.11 (f5-11-vista): the composite bridge section as the bridge smokes drive it (ui/smoke-bridge, ui/smoke-bridge-predalle,
/// ui/smoke-bridge-curves, ui/smoke-neutral-axis), on the XAML view hosted by <see cref="MvvmSheetEditor"/>, as the Release opens it since the
/// switch of the module (23/10/2026; the historic view BridgeWorkspace is archived in supporto/SUPERATI/ui-storica/sezione-mista). The members are
/// those of the historic view that the smokes used, with the same names: the controls of the view (pages, tabs, choices, switches, the scale of the
/// concrete, the menu of the contours, the table of the actions, the curves), the drawings (the WPF classes of the historic view), the forms
/// (<see cref="BridgeSmokeForm"/>), the calculation, the report, the detached view. The controls are those of CompositeSectionSheetView and the
/// values are those of its ViewModel; the content that WPF has not laid out yet is built as the historic view built every control
/// (CompositeSectionViewChecks.Realize).
/// </summary>
internal abstract class BridgeSmoke : IDisposable
{
    /// <summary>The element to put in a window (the host of the XAML view).</summary>
    internal abstract FrameworkElement View { get; }
    /// <summary>The data of the sheet (the live document of the view: on the XAML view a copy of the data it was opened with).</summary>
    internal abstract JsonObject Data { get; }
    internal abstract BridgeResultView? Calculation { get; }
    internal abstract BridgeResultView? DisplayedCalculation { get; }
    internal bool ResultsAreStale => Calculation is null && DisplayedCalculation is not null;
    internal abstract JsonObject? Result { get; }
    internal abstract bool Busy { get; }
    internal abstract BridgeDrawing Drawing { get; }
    internal abstract BridgeDetailSketch DetailSketch { get; }
    internal abstract TabControl Pages { get; }
    internal abstract TabControl Results { get; }
    internal abstract ComboBox StageChoice { get; }
    internal abstract ComboBox DisplayChoice { get; }
    internal abstract ComboBox DetailSectionChoice { get; }
    internal abstract CheckBox GeometryLabels { get; }
    internal abstract CheckBox RebarLabels { get; }
    internal abstract CheckBox NeutralAxisToggle { get; }
    internal abstract CheckBox ConcreteScaleAuto { get; }
    internal abstract TextBox ConcreteScaleEditor { get; }
    internal abstract MenuItem ContourSectionToggle { get; }
    internal abstract MenuItem ContourDiagramToggle { get; }
    internal abstract MenuItem StressLimitsToggle { get; }
    internal abstract TabControl SectionPropertyTabs { get; }
    /// <summary>The forms of the inputs in the order of the historic view: the definition, the accessories, the options, then each phase and its homogenization.</summary>
    internal abstract IReadOnlyList<BridgeSmokeForm> InputForms { get; }
    /// <summary>The form of the slab properties of the side panel.</summary>
    internal abstract BridgeSmokeForm SlabPropertiesForm { get; }
    internal abstract DataGrid ActionsTable { get; }
    /// <summary>The rows of the table of the actions (the rows of JsonGrid in the historic view).</summary>
    internal abstract IReadOnlyList<JsonRowViewModel> ActionRows { get; }
    /// <summary>Commits the cell being edited in the table (JsonGrid.Commit).</summary>
    internal abstract void CommitTable();
    internal abstract void Commit();
    internal abstract Task CalculateAsync(bool commit = true);
    internal abstract byte[] BuildReport(string title, HashSet<string> options);
    internal abstract void DetachView();
    internal abstract Window? DetachedWindow { get; }
    internal abstract JsonObject ResponseData { get; }
    internal abstract BridgeSmokeForm ResponseForm { get; }
    internal abstract ComboBox ResponsePhase { get; }
    internal abstract ComboBox ResponseFiber { get; }
    internal abstract Slider ResponsePoint { get; }
    internal abstract Task CalculateResponseAsync();
    internal abstract SectionResponseResult? ResponseCalculation { get; }
    /// <summary>Scrolls to the options of the box girder, or selects the checks with the results of the torsion.</summary>
    internal abstract void RevealTorsion(bool results);
    internal abstract bool BoxInputsVisible { get; }
    internal abstract bool TorsionResultsVisible { get; }
    internal abstract bool TorqueColumnVisible { get; }
    /// <summary>The toolbar of the drawing (ViewportFrame.Toolbar of the historic view).</summary>
    internal abstract Panel ViewportToolbar { get; }
    /// <summary>The host of the drawing in its frame (ViewportFrame.Host of the historic view).</summary>
    internal abstract ContentControl DrawingHost { get; }
    /// <summary>A splitter grid of the view by its key: «ingressi», «righe», «risultati», «curve_colonne», «curve_righe».</summary>
    internal abstract Grid Split(string key);

    public abstract void Dispose();

    /// <summary>
    /// A click as the user makes it (ButtonBase.OnClick: the Click event, then the command): the buttons of the historic view have a Click
    /// handler, those of the XAML view a command (and, under the table, a Click handler that commits the cell).
    /// </summary>
    internal static void Press(System.Windows.Controls.Primitives.ButtonBase button) =>
        typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(button, null);

    /// <summary>
    /// A composite section opened on its own, as the smokes did with <c>new BridgeWorkspace(data)</c> before the switch: the XAML view opened by
    /// <paramref name="editors"/> (on a copy: read the data back from <see cref="Data"/>), laid out so that its controls exist.
    /// </summary>
    internal static BridgeSmoke Open(SheetEditorFactory editors, JsonObject data)
    {
        var editor = (MvvmSheetEditor)editors.Open(BridgeSection.Module, data);
        return new MvvmBridgeSmoke(editor, editor);
    }

    /// <summary>The composite section of the sheet open in a window.</summary>
    internal static BridgeSmoke Of(ISheetEditor editor) => editor switch
    {
        MvvmSheetEditor { Sheet: CompositeSectionSheetViewModel } mvvm => new MvvmBridgeSmoke(mvvm, null),
        _ => throw new InvalidOperationException("Sezione composta da ponte non aperta con la vista MVVM: " + editor.Module)
    };

    /// <summary>F5.13 g: the composite section of a tab of the shell (its ViewModel and its view), not owned.</summary>
    internal static BridgeSmoke Of(HostedSheet sheet) => sheet.Sheet is CompositeSectionSheetViewModel ? new MvvmBridgeSmoke(sheet, null)
        : throw new InvalidOperationException("Sezione composta da ponte non aperta con la vista MVVM: " + sheet.Module);
}

/// <summary>A form of the composite section as the smokes drive it: its editors (text box, choice, check box), Set and Get as InputForm, its rows.</summary>
internal abstract class BridgeSmokeForm
{
    internal abstract BridgeSmokeEditors Editors { get; }
    internal abstract void Set(string key, string value, bool display = false);
    internal abstract string Get(string key);
    internal abstract void Commit();
    /// <summary>The row of the field is shown (ShowField of InputForm, IsVisible of the field).</summary>
    internal abstract bool Shown(string key);
    /// <summary>The visibility of the form itself (the second bottom plate of the H).</summary>
    internal abstract Visibility Visibility { get; }
    internal abstract bool IsVisible { get; }
}

/// <summary>The editors of a form by key.</summary>
internal sealed class BridgeSmokeEditors(Func<string, bool> contains, Func<string, FrameworkElement> editor)
{
    internal bool ContainsKey(string key) => contains(key);
    internal FrameworkElement this[string key] => contains(key) ? editor(key) : throw new KeyNotFoundException("Campo assente: " + key);
}

// ---------------------------------------------------------------- the XAML view

internal sealed class MvvmBridgeSmoke : BridgeSmoke
{
    private readonly HostedSheet editor;
    private readonly IDisposable? owned;

    /// <param name="editor">The sheet and its view: an MvvmSheetEditor (it converts) or, since F5.13 g, a tab of the shell.</param>
    /// <param name="owned">What closes with the smoke: the editor opened on its own, null for a sheet of a window.</param>
    internal MvvmBridgeSmoke(HostedSheet editor, IDisposable? owned)
    {
        this.editor = editor;
        this.owned = owned;
        // A sheet opened on its own is laid out once, so that its view and its controls exist before it is put in a window.
        if (PresentationSource.FromVisual(this.editor.View) is null)
        {
            this.editor.View.Measure(new Size(1600, 990)); this.editor.View.Arrange(new Rect(0, 0, 1600, 990)); this.editor.View.UpdateLayout();
        }
    }

    internal CompositeSectionSheetViewModel Sheet => (CompositeSectionSheetViewModel)editor.Sheet;
    internal CompositeSectionSheetView SheetView => Ui.Descendants<CompositeSectionSheetView>(editor.View).FirstOrDefault()
        ?? throw new InvalidOperationException("Vista XAML della sezione composta non creata.");
    internal override FrameworkElement View => editor.View;
    internal override JsonObject Data => Sheet.Data;
    internal override BridgeResultView? Calculation => Sheet.Calculation;
    internal override BridgeResultView? DisplayedCalculation => Sheet.DisplayedCalculation;
    internal override JsonObject? Result => Sheet.Result;
    internal override bool Busy => Sheet.IsBusy;
    internal override BridgeDrawing Drawing => SheetView.Drawing;
    internal override BridgeDetailSketch DetailSketch => SheetView.DetailSketch;
    internal override TabControl Pages => SheetView.Pages;
    internal override TabControl Results => SheetView.Results;
    internal override ComboBox StageChoice => SheetView.StageChoice;
    internal override ComboBox DisplayChoice => SheetView.DisplayChoice;
    internal override ComboBox DetailSectionChoice => SheetView.DetailSectionChoice;
    internal override CheckBox GeometryLabels => SheetView.GeometryLabelsSwitch;
    internal override CheckBox RebarLabels => SheetView.RebarLabelsSwitch;
    internal override CheckBox NeutralAxisToggle => SheetView.NeutralAxisSwitch;
    internal override CheckBox ConcreteScaleAuto => SheetView.ScaleAutoSwitch;
    internal override TextBox ConcreteScaleEditor => SheetView.ScaleEditor;
    internal override MenuItem ContourSectionToggle => MenuItem(0);
    internal override MenuItem ContourDiagramToggle => MenuItem(1);
    internal override MenuItem StressLimitsToggle => MenuItem(2);
    private MenuItem MenuItem(int index) => (MenuItem)SheetView.ChecksMenuButton.ContextMenu.Items[index];
    internal override TabControl SectionPropertyTabs => SheetView.PropertyTabs;

    internal override IReadOnlyList<BridgeSmokeForm> InputForms
    {
        get
        {
            var forms = CompositeSectionSheetViewModel.FormNames.Select(n => Sheet.Forms[n])
                .Concat(Sheet.Phases.SelectMany(c => c.Homogenization is null ? new[] { c.Form } : [c.Form, c.Homogenization]));
            return forms.Select(f => (BridgeSmokeForm)new Form(this, f)).ToArray();
        }
    }

    internal override BridgeSmokeForm SlabPropertiesForm => new Form(this, Sheet.SlabPropertiesForm!);
    internal override DataGrid ActionsTable => SheetView.ActionsGrid;
    internal override IReadOnlyList<JsonRowViewModel> ActionRows => Sheet.ActionRows;
    internal override void CommitTable() { ActionsTable.CommitEdit(DataGridEditingUnit.Cell, true); ActionsTable.CommitEdit(DataGridEditingUnit.Row, true); }
    internal override void Commit() => editor.Commit();
    internal override Task CalculateAsync(bool commit = true) => editor.CalculateAsync(commit);
    internal override byte[] BuildReport(string title, HashSet<string> options) => editor.BuildReport(title, options);
    internal override void DetachView() => Sheet.DetachCommand.Execute(null);
    internal override Window? DetachedWindow => SheetView.DetachedWindow;
    internal override JsonObject ResponseData => Sheet.ResponseData;
    internal override BridgeSmokeForm ResponseForm => new Form(this, Sheet.CurveForm!);
    internal override ComboBox ResponsePhase => SheetView.CurvePhaseChoice;
    internal override ComboBox ResponseFiber => SheetView.FiberChoice;
    internal override Slider ResponsePoint => SheetView.PointSlider;
    internal override Task CalculateResponseAsync() => Sheet.CalculateResponseAsync();
    internal override SectionResponseResult? ResponseCalculation => Sheet.ResponseCalculation;

    internal override void RevealTorsion(bool results)
    {
        var view = SheetView;
        if (!results)
        {
            if (CompositeSectionSheetView.Logical<Expander>(view.ControlPanelInputs).FirstOrDefault(e => Equals(e.Tag, "cassoncino")) is { } box) { box.IsExpanded = true; box.BringIntoView(); }
            return;
        }
        view.Results.SelectedItem = view.Results.Items.OfType<TabItem>().First(t => t.Header as string == "Verifiche");
        // after the layout of the selected tab
        view.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => view.TorsionGroup.BringIntoView()));
    }

    internal override bool BoxInputsVisible => CompositeSectionSheetView.Logical<Expander>(SheetView.ControlPanelInputs).Single(e => Equals(e.Tag, "cassoncino")).Visibility == Visibility.Visible;

    /// <summary>The group of the torsion shown with its results (a stack of blocks, as the historic view renders more than one block).</summary>
    internal override bool TorsionResultsVisible => SheetView.TorsionGroup.Visibility == Visibility.Visible && Sheet.TorsionPanel is { Scroll: false, Blocks.Count: not 1 };

    internal override bool TorqueColumnVisible => SheetView.ActionsGrid.Column("T").Visibility == Visibility.Visible;
    internal override Panel ViewportToolbar => SheetView.Toolbar;
    internal override ContentControl DrawingHost => SheetView.DrawingHost;

    internal override Grid Split(string key) => key switch
    {
        "ingressi" => SheetView.MainWorkspace, "righe" => SheetView.RightPanels, "risultati" => SheetView.UpperPanels,
        "curve_colonne" => SheetView.ResponsePage, "curve_righe" => SheetView.CurveRows, _ => throw new ArgumentException("Divisore sconosciuto: " + key)
    };

    public override void Dispose() => owned?.Dispose();

    /// <summary>The editor of a field: the control of FieldForm bound to it, built where WPF has not laid it out yet.</summary>
    private FrameworkElement EditorOf(FieldViewModel field)
    {
        FrameworkElement? Find() => Parts().SelectMany(p => CompositeSectionViewChecks.Walk(p, popups: false)).OfType<Control>()
            .FirstOrDefault(e => ReferenceEquals(e.DataContext, field) && e is TextBox or ComboBox or CheckBox && e.TemplatedParent is not ComboBox);
        if (Find() is { } editor) return editor;
        for (int pass = 0; pass < 3; pass++)
        {
            foreach (var part in Parts())
            {
                // A part out of the tree is laid out on its own first (its content is built as the historic view builds every control).
                if (VisualTreeHelper.GetParent(part) is null && !ReferenceEquals(part, SheetView)) { part.Measure(new Size(760, 900)); part.Arrange(new Rect(0, 0, 760, 900)); }
                CompositeSectionViewChecks.Realize(part); part.UpdateLayout();
            }
            if (Find() is { } built) return built;
        }
        throw new InvalidOperationException("Editore del campo " + field.Key + " non presente nella vista.");
    }

    /// <summary>
    /// The view and the parts that it keeps out of the tree when they are not shown (the other page of the inputs, the other side panel, the page
    /// not shown), where the historic view keeps its controls too: a form of the control panel is reached also from the page of the phases.
    /// </summary>
    private IEnumerable<FrameworkElement> Parts()
    {
        var view = SheetView;
        return new FrameworkElement[] { view, view.MainWorkspace, view.ResponsePage, view.ControlPanelInputs, view.PhaseInputs, view.SummaryPanel, view.PropertiesPanel }.Distinct();
    }

    /// <summary>The host of a form in the view (the ContentControl whose content is the form).</summary>
    private ContentControl? HostOf(FormViewModel form) => Parts().SelectMany(p => CompositeSectionViewChecks.Walk(p, popups: false)).OfType<ContentControl>()
        .FirstOrDefault(c => ReferenceEquals(c.Content, form));

    private sealed class Form(MvvmBridgeSmoke owner, FormViewModel form) : BridgeSmokeForm
    {
        internal override BridgeSmokeEditors Editors { get; } = new(form.Contains, key => owner.EditorOf(form[key]));
        internal override void Set(string key, string value, bool display = false) => form.Set(key, value, display);
        internal override string Get(string key) => form.Get(key);
        internal override void Commit() => form.Commit();
        internal override bool Shown(string key) => form[key].IsVisible;
        internal override Visibility Visibility => owner.HostOf(form)?.Visibility ?? Visibility.Collapsed;
        internal override bool IsVisible => owner.HostOf(form)?.IsVisible ?? false;
    }
}

internal sealed partial class ShellSmokeWindow
{
    /// <summary>F5.11: a composite section opened on its own by the factory of this window (BridgeSmoke.Open), as the smokes did with new BridgeWorkspace.</summary>
    private BridgeSmoke OpenBridge(JsonObject data) => BridgeSmoke.Open(sheetEditors, data);
}
