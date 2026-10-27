using Anthea.Calculations;
using Anthea.Presentation.Help;
using Anthea.Wiki;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Forms;

/// <summary>Editor of a field: text box, choice or check box.</summary>
public enum FieldKind { Text, Choice, Bool }

/// <summary>Arrangement of the row of a field: the branches of the historic InputForm, in its order of precedence.</summary>
public enum FieldLayout
{
    /// <summary>Label, editor and unit (with the symbol column when the form has it).</summary>
    Standard,
    /// <summary>A choice without unit: the editor takes also the unit column (always with the symbol columns, otherwise only
    /// when the form is not compact).</summary>
    EditorSpansUnit,
    /// <summary>Check box, or the «metodo» choice, on the whole row without a separate label (forms without symbol columns).</summary>
    EditorOnly,
    /// <summary>Label on its own row, editor on the whole width below (<see cref="FieldSpec.Wide"/>, or a choice of a form with
    /// wide choices).</summary>
    LabelAbove,
}

/// <summary>
/// One field of a <see cref="FormViewModel"/> (proposal F5, section 3.2): the state that the historic InputForm kept in its
/// WPF controls. The view binds <see cref="EditText"/>, <see cref="SelectedChoice"/> or <see cref="IsChecked"/> and reports
/// the keyboard focus through <see cref="IsFocused"/>; every write into the document goes through the form, with the
/// write rule of the field.
/// </summary>
public sealed class FieldViewModel : ObservableObject
{
    private string text = "";
    private string? selectedChoice;
    private bool isChecked, isFocused, isEnabled = true, isDimmed, isVisible = true;
    private bool? numberFormatting;
    private string? note, error;

    internal FieldViewModel(FormViewModel form, FieldSpec spec)
    {
        Form = form; Spec = spec;
        var shown = form.SymbolColumns ? FieldSymbols.Apply(spec) : spec;
        Label = shown.Label; Symbol = shown.Symbol;
        Kind = spec.Bool ? FieldKind.Bool : spec.Choices is not null ? FieldKind.Choice : FieldKind.Text;
        Layout = form.SymbolColumns ? (Kind == FieldKind.Choice && spec.Unit == "" ? FieldLayout.EditorSpansUnit : FieldLayout.Standard)
            : spec.Bool || spec.Key == "metodo" ? FieldLayout.EditorOnly
            : spec.Wide || Kind == FieldKind.Choice && form.WideChoices ? FieldLayout.LabelAbove
            : Kind == FieldKind.Choice && spec.Unit == "" && !form.Compact ? FieldLayout.EditorSpansUnit
            : FieldLayout.Standard;
        FixedHelp = HelpTopics.Find(spec.HelpTopic);
        var values = form.Values;
        // The same reads of the document as InputForm, in the same order: they are observed by the capture of the fallbacks.
        if (Kind == FieldKind.Bool) isChecked = values.B(spec.Key);
        else if (Kind == FieldKind.Choice)
        {
            var choices = spec.Choices!.ToList();
            string value = spec.Default is not null && values[spec.Key] is null ? spec.Default : values.S(spec.Key);
            if (value != "" && !choices.Contains(value)) choices.Add(value);
            Choices = choices.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray();
            selectedChoice = Choices.Contains(value) ? value : null;
        }
        // A text field with a Default (the head depth of the piles, 1.14.3) shows it while the key is missing, without writing it.
        else text = spec.Default is not null && values[spec.Key] is null ? spec.Default : values.S(spec.Key);
    }

    public FormViewModel Form { get; }
    /// <summary>The field as declared by the module.</summary>
    public FieldSpec Spec { get; }
    public string Key => Spec.Key;
    /// <summary>Label shown: with the symbol columns, the one of <see cref="FieldSymbols"/>.</summary>
    public string Label { get; }
    public string Symbol { get; }
    public string Unit => Spec.Unit;
    public bool HasUnit => Spec.Unit != "";
    public FieldKind Kind { get; }
    public FieldLayout Layout { get; }
    /// <summary>A text on more lines (<see cref="FieldSpec.Multiline"/>, S7): the editor wraps and accepts the Enter key.</summary>
    public bool Multiline => Spec.Multiline && Kind == FieldKind.Text;
    /// <summary>Calculated or linked value: typing never writes it (the form can still set it).</summary>
    public bool IsReadOnly => Spec.ReadOnly;
    /// <summary>The text box refuses the typing: a calculated field (<see cref="IsReadOnly"/>) or a read-only form
    /// (<see cref="FormViewModel.IsReadOnly"/>, a previous revision). Only the first one changes the background.</summary>
    public bool IsInputReadOnly => IsReadOnly || Form.Blocks(this);
    /// <summary>The choice or the check box accepts the user: false in a read-only form, where the view disables them.</summary>
    public bool CanChange => !Form.Blocks(this);
    /// <summary>Items of the choice: the declared values plus the current one, without blanks and repetitions. Empty for the other kinds.</summary>
    public IReadOnlyList<string> Choices { get; private set; } = [];

    /// <summary>Raw text of a text field, as typed or set: what <see cref="FormViewModel.Get"/> returns.</summary>
    public string Text => text;

    /// <summary>Text of the editor: the raw text while the field has the focus, otherwise <see cref="Display"/>. Setting it is
    /// the typing of the user.</summary>
    public string EditText
    {
        get => isFocused ? text : Display;
        set
        {
            // As the TextChanged handlers of InputForm: nothing when the shown text does not change; otherwise the raw text,
            // then the write (or the draft), then the presentation again (formatted when the editor has no focus).
            value ??= "";
            if (value == EditText) return;
            // A read-only form refuses the typing: the editor shows the text of the field again.
            if (Form.Blocks(this)) { OnPropertyChanged(); return; }
            SetRaw(value);
            OnEdited();
            RefreshText();
        }
    }

    /// <summary>Text shown without focus: <see cref="NumericDisplay.Format"/> when the numbers are formatted, otherwise the raw text.</summary>
    public string Display => FormatsNumbers ? NumericDisplay.Format(text, Key) : text;

    /// <summary>Presentation of the numbers for this field only: null follows <see cref="FormViewModel.FormatNumbers"/>.</summary>
    public bool? NumberFormatting
    {
        get => numberFormatting;
        set { if (SetProperty(ref numberFormatting, value)) RefreshText(); }
    }
    public bool FormatsNumbers => numberFormatting ?? Form.FormatNumbers;

    /// <summary>Keyboard focus of the editor, reported by the view. Losing it confirms a pending edit of the form that confirms on focus loss.</summary>
    public bool IsFocused
    {
        get => isFocused;
        set
        {
            if (isFocused == value) return;
            isFocused = value;
            if (!value && !IsReadOnly && Form.RemoveDraft(Key)) Form.Store(this, text);
            OnPropertyChanged();
            RefreshText();
        }
    }

    /// <summary>Selected item of a choice. A value that is not an item is ignored, as the selection of the historic combo box.</summary>
    public string? SelectedChoice
    {
        get => selectedChoice;
        set
        {
            if (Kind != FieldKind.Choice || selectedChoice == value) return;
            if (value is not null && !Choices.Contains(value) || Form.Blocks(this)) { OnPropertyChanged(); return; }
            selectedChoice = value;
            OnPropertyChanged();
            Form.Store(this, value ?? "");
        }
    }

    public bool IsChecked
    {
        get => isChecked;
        set
        {
            if (Kind != FieldKind.Bool || isChecked == value) return;
            if (Form.Blocks(this)) { OnPropertyChanged(); return; }
            isChecked = value;
            OnPropertyChanged();
            Form.Store(this, value);
        }
    }

    public bool IsEnabled { get => isEnabled; internal set => SetProperty(ref isEnabled, value); }
    /// <summary>Shown faded (the dim option of <see cref="FormViewModel.Enable"/>).</summary>
    public bool IsDimmed { get => isDimmed; internal set => SetProperty(ref isDimmed, value); }
    public bool IsVisible
    {
        get => isVisible;
        internal set { if (SetProperty(ref isVisible, value)) OnPropertyChanged(nameof(IsNoteVisible)); }
    }

    /// <summary>Notice of <see cref="FieldSpec.Note"/> at its last update; null without notice.</summary>
    public string? Note
    {
        get => note;
        internal set { if (SetProperty(ref note, value)) OnPropertyChanged(nameof(IsNoteVisible)); }
    }
    public bool IsNoteVisible => note is not null && isVisible;

    /// <summary>Messages of the validator of the form for this field, one per line; null without errors.</summary>
    public string? Error
    {
        get => error;
        internal set { if (SetProperty(ref error, value)) OnPropertyChanged(nameof(HasError)); }
    }
    public bool HasError => error is not null;

    /// <summary>Topic fixed by the module (<see cref="FieldSpec.HelpTopic"/>).</summary>
    public Topic? FixedHelp { get; }
    /// <summary>Topic of the «?» label: the fixed one, otherwise the topic of the key in the module of the form.</summary>
    public Topic? HelpTopic => FixedHelp ?? HelpTopics.ForField(Key, Form.EffectiveWikiModule);
    public bool HasHelp => HelpTopic is not null;
    public string? HelpToolTip => HelpTopic is { } topic ? topic.Title + "\nApri questa sezione della Wiki" : null;
    /// <summary>Section of the Wiki opened by F1: the topic of the field, otherwise the guide of the module.</summary>
    public string? HelpUri
    {
        get
        {
            string? module = Form.EffectiveWikiModule;
            return FixedHelp?.Uri ?? HelpTopics.ForField(Key, module)?.Uri ?? (module is null ? null : WikiCatalog.ForModule(module)?.Id);
        }
    }
    public string LabelToolTip => HelpToolTip ?? Spec.ToolTip ?? CalculationHelp.Field(Key) ?? Label;

    /// <summary>
    /// The «?» label: opens the topic of the field with <see cref="FormViewModel.WikiNavigator"/>. False when the field has no
    /// topic or the form has no navigator (the view of a historic host then opens the Wiki itself).
    /// </summary>
    public bool OpenHelp() => Open(HelpTopic?.Uri);

    /// <summary>F1 in the editor: opens <see cref="HelpUri"/> (the topic, otherwise the guide of the module) with the navigator of the form; false as <see cref="OpenHelp"/>.</summary>
    public bool OpenContextHelp() => Open(HelpUri);

    private bool Open(string? uri)
    {
        if (uri is null || Form.WikiNavigator is not { } wiki) return false;
        wiki.Show(uri);
        return true;
    }
    /// <summary>Tooltip of the editor; as in InputForm it looks up the topic only in the module given to the form.</summary>
    public string EditorToolTip => FixedHelp?.Title ?? HelpTopics.Description(Key, Form.WikiModule) ?? Spec.ToolTip ?? CalculationHelp.Field(Key) ?? Label + (Unit != "" ? " [" + Unit + "]" : "");

    /// <summary>Sets the raw text without writing the document.</summary>
    internal void SetRaw(string value)
    {
        if (text == value) return;
        text = value;
        OnPropertyChanged(nameof(Text));
        RefreshText();
    }

    /// <summary>A change of the text of the editor (the TextChanged of InputForm): writes, or keeps a draft while the focus stays.</summary>
    internal void OnEdited()
    {
        if (IsReadOnly || Form.DisplayOnly) return;
        if (Form.CommitOnFocusLoss && isFocused) Form.AddDraft(Key);
        else Form.Store(this, text);
    }

    /// <summary>
    /// Replaces the items of a choice, as a new ItemsSource of the historic combo box (F5.10, the classes of the materials of the RC section
    /// after a change of the standard): the items are taken as given; the selection stays when it is still an item, otherwise it is
    /// cleared without writing the document. Additive: the other forms never call it.
    /// </summary>
    public void ReplaceChoices(IEnumerable<string> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        if (Kind != FieldKind.Choice) throw new InvalidOperationException("Voci sostituite in un campo che non è una scelta: " + Key);
        Choices = choices.ToArray();
        OnPropertyChanged(nameof(Choices));
        if (selectedChoice is not null && !Choices.Contains(selectedChoice)) { selectedChoice = null; OnPropertyChanged(nameof(SelectedChoice)); }
    }

    /// <summary>Selection set by the form: ignored when the value is not an item, written unless the form only displays.</summary>
    internal void Select(string value)
    {
        if (!Choices.Contains(value) || selectedChoice == value) return;
        selectedChoice = value;
        OnPropertyChanged(nameof(SelectedChoice));
        Form.Store(this, value);
    }

    internal void RefreshText()
    {
        OnPropertyChanged(nameof(EditText));
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(FormatsNumbers));
    }

    internal void RefreshEditable()
    {
        OnPropertyChanged(nameof(IsInputReadOnly));
        OnPropertyChanged(nameof(CanChange));
    }

    internal void RefreshHelp()
    {
        OnPropertyChanged(nameof(HelpTopic));
        OnPropertyChanged(nameof(HasHelp));
        OnPropertyChanged(nameof(HelpToolTip));
        OnPropertyChanged(nameof(HelpUri));
        OnPropertyChanged(nameof(LabelToolTip));
    }
}
