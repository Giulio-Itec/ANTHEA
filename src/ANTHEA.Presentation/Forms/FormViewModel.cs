using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Forms;

/// <summary>Error of a field returned by an <see cref="IFieldValidator"/>.</summary>
public sealed record FieldError(string Key, string Message);

/// <summary>
/// Validation of the fields of a form, supplied by the module from its existing validators (proposal F5, section 3.2): no new
/// rule in the interface. The invalid text stays in the field and in the document, as today.
/// </summary>
public interface IFieldValidator
{
    /// <summary>Errors for the current values of the form; a key without errors is simply absent.</summary>
    IEnumerable<FieldError> Validate(JsonObject values);
}

/// <summary>Expandable group of fields (the GroupFields of InputForm).</summary>
public sealed class FieldGroupViewModel(string title, IReadOnlyList<FieldViewModel> fields, bool expanded) : ObservableObject
{
    private bool isExpanded = expanded;
    public string Title { get; } = title;
    public IReadOnlyList<FieldViewModel> Fields { get; } = fields;
    public bool IsExpanded { get => isExpanded; set => SetProperty(ref isExpanded, value); }
}

/// <summary>
/// Form on a <see cref="JsonObject"/> of the document (proposal F5, section 3.2): the model of the historic InputForm without
/// WPF, with the same operations (<see cref="Set"/>, <see cref="Get"/>, <see cref="Enable"/>, <see cref="ShowField"/>,
/// <see cref="GroupFields"/>, <see cref="Commit"/>, <see cref="UpdateNote"/>) and the same writes into the document: the
/// FieldForm/InputForm parity check of the ui stage compares the bytes of the resulting document.
/// <para>A default choice is shown and never written until the user chooses. With <see cref="CommitOnFocusLoss"/> a text
/// typed in a field is written when the field loses the focus or at <see cref="Commit"/>; otherwise at every change.</para>
/// <para>With <see cref="IsReadOnly"/> (a previous revision of a project, decision D4 of F5.4) the form refuses what the user
/// types, chooses or ticks, as RevisionInspection does on the controls of the historic views; the module can still set values.</para>
/// </summary>
public sealed class FormViewModel : ObservableObject, INotifyDataErrorInfo
{
    private readonly Dictionary<string, FieldViewModel> byKey = new(StringComparer.Ordinal);
    private readonly HashSet<string> drafts = [];
    private readonly Dictionary<string, string[]> errors = new(StringComparer.Ordinal);
    private readonly Action<string>? changed;
    private readonly IFieldValidator? validator;
    private bool commitOnFocusLoss, formatNumbers, isReadOnly;
    private string? inheritedWikiModule;

    /// <param name="values">Object of the document written by the form.</param>
    /// <param name="fields">Fields in their order.</param>
    /// <param name="changed">Called with the key after every write, as the callback of InputForm.</param>
    /// <param name="compact">Narrow columns and smaller text.</param>
    /// <param name="wideChoices">Choices below their label, on the whole width.</param>
    /// <param name="symbolColumns">Symbol column, labels and symbols of <see cref="FieldSymbols"/>.</param>
    /// <param name="wikiModule">Module of the help topics; otherwise the module of the view (<see cref="InheritedWikiModule"/>).</param>
    /// <param name="validator">Errors of the fields, from the existing validators of the module.</param>
    public FormViewModel(JsonObject values, IEnumerable<FieldSpec> fields, Action<string>? changed = null, bool compact = false,
        bool wideChoices = false, bool symbolColumns = false, string? wikiModule = null, IFieldValidator? validator = null)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(fields);
        Values = values; this.changed = changed; this.validator = validator;
        Compact = compact; WideChoices = wideChoices; SymbolColumns = symbolColumns; WikiModule = wikiModule;
        var list = new List<FieldViewModel>();
        foreach (var spec in fields)
        {
            var field = new FieldViewModel(this, spec);
            list.Add(field); byKey[spec.Key] = field;
            UpdateNote(spec.Key);
        }
        Fields = list;
        TableFields = new(list);
        Validate();
    }

    public JsonObject Values { get; }
    /// <summary>All the fields, in their order.</summary>
    public IReadOnlyList<FieldViewModel> Fields { get; }
    /// <summary>Fields of the main table: those not moved into a group.</summary>
    public ObservableCollection<FieldViewModel> TableFields { get; }
    /// <summary>Expandable groups, after the table, in the order of <see cref="GroupFields"/>.</summary>
    public ObservableCollection<FieldGroupViewModel> Groups { get; } = [];
    public bool Compact { get; }
    public bool WideChoices { get; }
    public bool SymbolColumns { get; }
    public string? WikiModule { get; }

    /// <summary>Module of the view that hosts the form (the inherited WikiContextHelp.Module of the historic views).</summary>
    public string? InheritedWikiModule
    {
        get => inheritedWikiModule;
        set
        {
            if (!SetProperty(ref inheritedWikiModule, value)) return;
            OnPropertyChanged(nameof(EffectiveWikiModule));
            foreach (var field in Fields) field.RefreshHelp();
        }
    }
    public string? EffectiveWikiModule => WikiModule ?? inheritedWikiModule;

    /// <summary>Text typed in a field is written when the field loses the focus (or at <see cref="Commit"/>), not at every change.</summary>
    public bool CommitOnFocusLoss { get => commitOnFocusLoss; set => SetProperty(ref commitOnFocusLoss, value); }

    /// <summary>
    /// Read-only form (a previous revision, decision D4 of F5.4): the text boxes refuse the typing and the choices and check boxes
    /// are disabled (<see cref="FieldViewModel.IsInputReadOnly"/>, <see cref="FieldViewModel.CanChange"/>), the «?» labels stay
    /// usable; <see cref="Set"/> from the module still writes. The FieldForm binds it.
    /// </summary>
    public bool IsReadOnly
    {
        get => isReadOnly;
        set
        {
            if (!SetProperty(ref isReadOnly, value)) return;
            foreach (var field in Fields) field.RefreshEditable();
        }
    }

    /// <summary>
    /// Where the «?» labels and F1 of the fields open the Wiki (<see cref="FieldViewModel.OpenHelp"/>): the navigator of the
    /// services of the sheet. Null for the forms of the historic views, whose FieldForm opens the Wiki of its window itself.
    /// </summary>
    public IWikiNavigator? WikiNavigator { get; set; }

    /// <summary>Numbers shown with <see cref="NumericDisplay.Format"/> in the fields without focus (presentation only).</summary>
    public bool FormatNumbers
    {
        get => formatNumbers;
        set { if (SetProperty(ref formatNumbers, value)) foreach (var field in Fields) field.RefreshText(); }
    }

    /// <summary>Texts typed and not yet written (forms that confirm on focus loss).</summary>
    public bool HasDrafts => drafts.Count > 0;

    /// <summary>Raised after every write into the document, with the key, after the callback of the constructor.</summary>
    public event Action<string>? Changed;

    public FieldViewModel this[string key] => byKey[key];
    public bool Contains(string key) => byKey.ContainsKey(key);

    /// <summary>True while <see cref="Set"/> only displays a value: no write.</summary>
    internal bool DisplayOnly { get; private set; }

    /// <summary>Current value of a field: raw text, selected choice or empty for a check box. An unknown key throws.</summary>
    public string Get(string key)
    {
        var field = byKey[key];
        return field.Kind switch { FieldKind.Text => field.Text, FieldKind.Choice => field.SelectedChoice ?? "", _ => "" };
    }

    /// <summary>
    /// Sets a text or a choice from the module. With <paramref name="display"/> the value is only shown, otherwise it is
    /// written (also into a read-only field). A choice that is not an item is ignored; check boxes are not set. Unknown keys
    /// are ignored. The same sequence as InputForm.Set, so the same writes and the same callbacks.
    /// </summary>
    public void Set(string key, string value, bool display = false)
    {
        if (!byKey.TryGetValue(key, out var field)) return;
        RemoveDraft(key);
        DisplayOnly = display;
        try
        {
            if (field.Kind == FieldKind.Text)
            {
                string shown = field.EditText;
                field.SetRaw(value);
                if (shown != value) field.OnEdited();
                if (!display && Values.S(key) != value) Store(field, value);
            }
            else if (field.Kind == FieldKind.Choice) field.Select(value);
        }
        finally { DisplayOnly = false; UpdateNote(key); }
    }

    /// <summary>Enables or disables the row of a field; with <paramref name="dim"/> the row is also faded while disabled.</summary>
    public void Enable(string key, bool enabled, bool dim = false)
    {
        if (!byKey.TryGetValue(key, out var field)) return;
        field.IsEnabled = enabled;
        if (dim) field.IsDimmed = !enabled;
    }

    public void ShowField(string key, bool show)
    {
        if (!byKey.TryGetValue(key, out var field)) return;
        field.IsVisible = show;
        UpdateNote(key);
    }

    /// <summary>Moves the given fields (unknown keys are skipped) from the table into a new expandable group.</summary>
    public FieldGroupViewModel GroupFields(string title, string[] keys, bool expanded = false)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var members = keys.Where(byKey.ContainsKey).Select(k => byKey[k]).ToList();
        if (members.Any(f => !TableFields.Contains(f))) throw new InvalidOperationException("Campo già in un gruppo del form.");
        foreach (var field in members) TableFields.Remove(field);
        var group = new FieldGroupViewModel(title, members, expanded);
        Groups.Add(group);
        return group;
    }

    /// <summary>Recomputes the notice of a field from the document (after a change made outside the form).</summary>
    public void UpdateNote(string key)
    {
        if (!byKey.TryGetValue(key, out var field) || field.Spec.Note is null) return;
        field.Note = field.Spec.Note(Values);
    }

    /// <summary>Writes the texts typed and not yet confirmed (navigation, save, calculation).</summary>
    public void Commit()
    {
        foreach (string key in drafts.ToArray())
        {
            RemoveDraft(key);
            if (byKey[key] is { Kind: FieldKind.Text, IsReadOnly: false } field) Store(field, field.Text);
        }
    }

    /// <summary>Recomputes the errors of the fields with the validator of the module (after a change made outside the form).</summary>
    public void Validate()
    {
        if (validator is null) return;
        var found = validator.Validate(Values).GroupBy(e => e.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray(), StringComparer.Ordinal);
        bool before = HasErrors;
        var keys = errors.Keys.Concat(found.Keys).Distinct(StringComparer.Ordinal).ToArray();
        foreach (string key in keys)
        {
            var previous = errors.GetValueOrDefault(key);
            var current = found.GetValueOrDefault(key);
            if (previous is not null && current is not null && previous.SequenceEqual(current)) continue;
            if (current is null) errors.Remove(key); else errors[key] = current;
            if (byKey.TryGetValue(key, out var field)) field.Error = current is null ? null : string.Join("\n", current);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(key));
        }
        if (before != HasErrors) OnPropertyChanged(nameof(HasErrors));
    }

    public bool HasErrors => errors.Count > 0;
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
    /// <summary>Errors of a field (by key); all the errors for a null or empty name.</summary>
    public IEnumerable GetErrors(string? propertyName) => string.IsNullOrEmpty(propertyName)
        ? errors.Values.SelectMany(e => e).ToArray()
        : errors.GetValueOrDefault(propertyName) ?? [];

    /// <summary>The Store of InputForm: write with the rule of the field, notice, callback, then validation.</summary>
    internal void Store(FieldViewModel field, object value)
    {
        if (DisplayOnly) return;
        FieldWrite.Apply(Values, field.Key, value, field.Spec.Write);
        UpdateNote(field.Key);
        changed?.Invoke(field.Key);
        Changed?.Invoke(field.Key);
        Validate();
    }

    internal void AddDraft(string key) { if (drafts.Add(key) && drafts.Count == 1) OnPropertyChanged(nameof(HasDrafts)); }
    internal bool RemoveDraft(string key)
    {
        if (!drafts.Remove(key)) return false;
        if (drafts.Count == 0) OnPropertyChanged(nameof(HasDrafts));
        return true;
    }
}
