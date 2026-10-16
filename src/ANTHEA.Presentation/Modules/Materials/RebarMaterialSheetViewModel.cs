using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Application.Materials;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Materiali;
using X.Core;

namespace Anthea.Presentation.Modules.Materials;

/// <summary>
/// Sheet of the reinforcing steel (mat_acciaio_armatura), F5.5: the behaviour of the historic view RebarMaterialView without WPF,
/// on the same document and with the same writes, in the same order.
/// <list type="bullet">
/// <item>Type: choosing a steel of the catalogue writes its values (<see cref="RebarEditor.Select"/>) and shows them in the fields;
/// «Personalizzato» keeps the values with the generic name.</item>
/// <item>Fields: three forms on «input» with the rule of InputForm (the raw text, written at every change): name, mechanical
/// properties with the diagram, partial coefficient. After every edit a sheet that no longer matches its catalogue steel
/// becomes custom (<see cref="RebarPresentation.MarkCustom"/>). Which fields the catalogue fixes is <see cref="Locked"/>,
/// <see cref="NameLocked"/> and <see cref="DiagramEnabled"/>, a state of the view as the read-only text boxes of the historic
/// view: the ViewModel does not refuse those writes.</item>
/// <item>Certificate or reference: written into «riferimento» at every change, with Modified.</item>
/// <item>The values shown (fyd, εyd, fu / fyk, status and diagram) come from the calculation service of the context, on the UI
/// thread, after every change, as the historic view; no calculation is published (<see cref="SheetCalculationMode.None"/>):
/// «Calcola» only commits.</item>
/// <item>Read-only (a previous revision): type, fields and reference refuse the changes; the forms are rebuilt on detached copies.</item>
/// </list>
/// The rules of the texts and of the fixed fields come from <see cref="RebarPresentation"/>, shared with the historic view. The
/// equivalence is proved without WPF by tests/ANTHEA.Presentation.Tests (MaterialSheetChecks) against the traces frozen in
/// tests/fixtures/f5-materiali and against the F3 reference.
/// </summary>
public sealed class RebarMaterialSheetViewModel : SheetViewModelBase, IMaterialSheetViewModel
{
    /// <summary>The module of the sheet.</summary>
    public const string Module = RebarEditor.Module;

    private const string NameKey = "materiale_acciaio_nome";
    private bool loading, refreshing;
    private string? selectedType;
    private FormViewModel identityForm = null!, propertiesForm = null!, coefficientForm = null!;
    private IReadOnlyDictionary<string, bool> locked = new Dictionary<string, bool>();
    private bool nameLocked, diagramEnabled, statusCoherent, hasDiagram;
    private string description = "", source = "", results = "", status = "", diagramName = "";
    private IReadOnlyList<double[]> diagram = [];

    /// <param name="context">Data of the sheet (owned copy), host, services and the calculation service of the steel.</param>
    public RebarMaterialSheetViewModel(SheetContext context) : base(Module, context, mode: SheetCalculationMode.None)
    {
        Report = new MaterialSheetReport(this);
        Build();
    }

    public override IModuleReportSource Report { get; }

    /// <summary>The «input» object of the data, written by the fields.</summary>
    public JsonObject Input => Data["input"]!.AsObject();

    // ---------------------------------------------------------------- input

    /// <summary>The steels of the catalogue, then «Personalizzato».</summary>
    public IReadOnlyList<string> Types { get; } = RebarPresentation.Types();

    /// <summary>The type of the steel: the catalogue steel the input matches, otherwise «Personalizzato». Choosing one applies it.</summary>
    public string? SelectedType
    {
        get => selectedType;
        set
        {
            if (value == selectedType || refreshing) return;
            if (!CanEdit || value is null || !Types.Contains(value)) { OnPropertyChanged(); return; }
            selectedType = value;
            OnPropertyChanged();
            RebarEditor.Select(Input, value);
            RefreshEditors();
            Refresh();
            Changed();
        }
    }

    /// <summary>Name of the steel (on its own row).</summary>
    public FormViewModel IdentityForm => identityForm;

    /// <summary>fyk, fu, Es, εu and the diagram.</summary>
    public FormViewModel PropertiesForm => propertiesForm;

    /// <summary>The partial coefficient γs.</summary>
    public FormViewModel CoefficientForm => coefficientForm;

    /// <summary>The mechanical properties fixed by the catalogue (<see cref="RebarPresentation.PropertyKeys"/>, true when fixed).</summary>
    public IReadOnlyDictionary<string, bool> Locked { get => locked; private set => SetProperty(ref locked, value); }

    /// <summary>The name is fixed by the catalogue.</summary>
    public bool NameLocked { get => nameLocked; private set => SetProperty(ref nameLocked, value); }

    /// <summary>The diagram can be chosen (custom and historic steels).</summary>
    public bool DiagramEnabled { get => diagramEnabled; private set => SetProperty(ref diagramEnabled, value); }

    /// <summary>Certificate or reference of the values, written into «riferimento» at every change.</summary>
    public string Reference
    {
        get => Data.S("riferimento");
        set
        {
            value ??= "";
            if (value == Reference) return;
            if (!Edit(data => data["riferimento"] = value)) { OnPropertyChanged(); return; }
            OnPropertyChanged();
        }
    }

    // ---------------------------------------------------------------- output

    public string Description { get => description; private set => SetProperty(ref description, value); }
    public string Source { get => source; private set => SetProperty(ref source, value); }
    /// <summary>fyd, εyd and fu / fyk, one per line («—» when the data are incomplete).</summary>
    public string Results { get => results; private set => SetProperty(ref results, value); }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    /// <summary>The status is the coherent one (green); otherwise a warning.</summary>
    public bool StatusCoherent { get => statusCoherent; private set => SetProperty(ref statusCoherent, value); }
    /// <summary>Points (ε ‰, σ MPa) of the characteristic curve; empty when the data are incomplete.</summary>
    public IReadOnlyList<double[]> Diagram { get => diagram; private set => SetProperty(ref diagram, value); }
    /// <summary>Name of the curve: the name of the steel when the diagram was drawn.</summary>
    public string DiagramName { get => diagramName; private set => SetProperty(ref diagramName, value); }
    /// <summary>The diagram is drawn (the data are complete); otherwise the drawing shows its empty message.</summary>
    public bool HasDiagram { get => hasDiagram; private set => SetProperty(ref hasDiagram, value); }

    // ---------------------------------------------------------------- sheet

    /// <summary>Chapter of the material in a report: the sheet is committed first, as the historic sheet does.</summary>
    public ReportProject.SheetContent ReportContent()
    {
        Commit();
        return MaterialReportChapter.Create(Module, Data);
    }

    /// <summary>The Commit of the historic view: the drafts of the three forms (none: the forms write at every change).</summary>
    protected override void OnCommit()
    {
        propertiesForm.Commit(); identityForm.Commit(); coefficientForm.Commit();
    }

    /// <summary>The data were replaced: the sheet is built again on them, as at opening, without Modified.</summary>
    protected override void OnReloaded()
    {
        Build();
        OnPropertyChanged(nameof(Reference));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(CanEdit) || loading || IsDisposed) return;
        BuildForms(keepTexts: true);
    }

    // ---------------------------------------------------------------- construction

    private void Build()
    {
        loading = true;
        try { BuildForms(keepTexts: false); }
        finally { loading = false; }
        Refresh();
    }

    /// <summary>The three forms of the historic view on «input»; on detached copies when read-only, with the texts shown before when kept.</summary>
    private void BuildForms(bool keepTexts)
    {
        var shown = keepTexts ? new[] { identityForm, propertiesForm, coefficientForm }.Where(f => f is not null).SelectMany(f => f.Fields)
            .Where(f => f.Kind == FieldKind.Text).ToDictionary(f => f.Key, f => f.Text, StringComparer.Ordinal) : null;
        identityForm = Form([new(NameKey, "Nome", Wide: true)], wideChoices: false, shown);
        propertiesForm = Form([new("fyk_mpa", "Snervamento fyk", "MPa"), new("steel_fu_mpa", "Rottura fu", "MPa"),
            new("steel_modulus_mpa", "Modulo elastico Es", "MPa"), new("steel_eps_u", "Deformazione ultima εu", "‰"),
            new("steel_diagramma", "Diagramma", Choices: ["Elastoplastico", "Incrudente"])], wideChoices: true, shown);
        coefficientForm = Form([new("gamma_s", "Coefficiente parziale γs")], wideChoices: false, shown);
        OnPropertyChanged(nameof(IdentityForm)); OnPropertyChanged(nameof(PropertiesForm)); OnPropertyChanged(nameof(CoefficientForm));
    }

    /// <summary>
    /// A form of the historic view (compact, help of the module of the view): on «input» with the callback of the edits; when
    /// read-only on a detached copy and without callback. <paramref name="shown"/>: texts shown again, without writing.
    /// </summary>
    private FormViewModel Form(FieldSpec[] fields, bool wideChoices, IReadOnlyDictionary<string, string>? shown)
    {
        var form = CanEdit ? new FormViewModel(Input, fields, OnFieldChanged, compact: true, wideChoices: wideChoices)
            : new FormViewModel((JsonObject)Input.DeepClone(), fields, compact: true, wideChoices: wideChoices);
        if (shown is not null)
            foreach (var field in form.Fields.Where(f => f.Kind == FieldKind.Text && shown.ContainsKey(f.Key))) form.Set(field.Key, shown[field.Key], display: true);
        return form;
    }

    /// <summary>Changed of the historic view: a steel that no longer matches its catalogue becomes custom, then the values again and Modified.</summary>
    private void OnFieldChanged(string key)
    {
        if (refreshing || loading || IsDisposed) return;
        if (RebarPresentation.MarkCustom(Input)) identityForm.Set(NameKey, Input.S(NameKey), display: true);
        Refresh();
        Changed();
    }

    /// <summary>RefreshEditors of the historic view: every field shows the value of the data, without writing.</summary>
    private void RefreshEditors()
    {
        refreshing = true;
        try
        {
            foreach (var form in new[] { identityForm, propertiesForm, coefficientForm })
                foreach (var field in form.Fields) form.Set(field.Key, Input.S(field.Key), display: true);
        }
        finally { refreshing = false; }
    }

    /// <summary>Refresh of the historic view: type, fixed fields, texts, then the calculation of the module and its values.</summary>
    private void Refresh()
    {
        var state = RebarPresentation.Read(Input);
        refreshing = true;
        try { if (selectedType != state.Selected) { selectedType = state.Selected; OnPropertyChanged(nameof(SelectedType)); } }
        finally { refreshing = false; }
        Locked = RebarPresentation.PropertyKeys.ToDictionary(k => k, state.Locked, StringComparer.Ordinal);
        DiagramEnabled = state.DiagramEnabled;
        NameLocked = state.NameLocked;
        Description = state.Description; Source = state.Source;
        RebarResults values;
        bool drawn = false;
        try { values = RebarPresentation.Results(CalculateModule(Data), state.Historical); drawn = true; }
        catch (ArgumentException error) { values = RebarPresentation.Failed(error); }
        Results = values.Text; Status = values.Status; StatusCoherent = values.Coherent;
        DiagramName = drawn ? Input.S(NameKey) : ""; Diagram = values.Diagram; HasDiagram = drawn;
    }
}
