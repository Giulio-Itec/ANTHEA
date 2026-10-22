using System.Collections.Immutable;
using System.Collections.Specialized;
using System.ComponentModel;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>Editor of a column: text (numbers right aligned), check box or choice.</summary>
public enum ConcreteColumnKind { Text, Bool, Choice }

/// <summary>A column of a table of the RC section (the Field of the historic JsonGrid): key, header, kind, read-only and the items of a choice.</summary>
public sealed class ConcreteColumnViewModel(string key, string header, ConcreteColumnKind kind = ConcreteColumnKind.Text, bool readOnly = false,
    IReadOnlyList<string>? choices = null) : ObservableObject
{
    private IReadOnlyList<string> choices = choices ?? [];
    private bool isVisible = true;

    public string Key { get; } = key;
    public string Header { get; } = header;
    public ConcreteColumnKind Kind { get; } = kind;
    public bool IsReadOnly { get; } = readOnly;

    /// <summary>Items of a choice column (the ItemsSource of the historic DataGridComboBoxColumn, without blanks and repetitions).</summary>
    public IReadOnlyList<string> Choices { get => choices; set => SetProperty(ref choices, value ?? []); }

    public bool IsVisible { get => isVisible; set => SetProperty(ref isVisible, value); }
}

/// <summary>
/// The filter bar of the tables of the RC section (ConcreteTables.cs of the historic view, «filtri e ordinamenti comuni», F5.10 S1): column,
/// mode and text of the filter, order. As the historic bar, the table opens ordered by its key column (<c>nome</c>, otherwise <c>id</c>)
/// while the order shows «Ordine inserimento»; every change of the bar applies the state of the bar.
/// </summary>
public sealed class ConcreteTableFilterViewModel : ObservableObject
{
    /// <summary>The modes of the filter, in the order of the bar.</summary>
    public static ImmutableArray<string> Modes { get; } = ["Contiene", "Inizia con", "Uguale a", "Non contiene"];
    /// <summary>The orders of the bar.</summary>
    public static ImmutableArray<string> Orders { get; } = ["Ordine inserimento", "A → Z / crescente", "Z → A / decrescente"];

    private readonly ConcreteTableViewModel table;
    private string column, query = "";
    private int mode, order;

    internal ConcreteTableFilterViewModel(ConcreteTableViewModel table)
    {
        this.table = table;
        Fields = table.Columns.Where(c => c.Key != "visible").ToArray();
        Key = table.Columns.Any(c => c.Key == "nome") ? "nome" : "id";
        SortKey = Key;
        column = Fields.FirstOrDefault(c => c.Key == Key)?.Header ?? Fields[0].Header;
    }

    /// <summary>The columns the filter can read (every column but «Mostra»).</summary>
    public IReadOnlyList<ConcreteColumnViewModel> Fields { get; }

    /// <summary>Header of the column of the filter.</summary>
    public string Column { get => column; set { if (SetProperty(ref column, value)) Apply(); } }
    /// <summary>Index of the mode in <see cref="Modes"/>.</summary>
    public int Mode { get => mode; set { if (SetProperty(ref mode, value)) Apply(); } }
    /// <summary>Text of the filter (case insensitive, trimmed); it never removes rows from the calculation.</summary>
    public string Query { get => query; set { if (SetProperty(ref query, value ?? "")) Apply(); } }
    /// <summary>Index of the order in <see cref="Orders"/>.</summary>
    public int Order { get => order; set { if (SetProperty(ref order, value)) Apply(); } }

    /// <summary>Key of the column filtered.</summary>
    public string Key { get; private set; }
    /// <summary>The mode applied.</summary>
    public string Match { get; private set; } = Modes[0];
    /// <summary>The text applied.</summary>
    public string Text { get; private set; } = "";
    /// <summary>Key of the order applied; empty for the order of insertion.</summary>
    public string SortKey { get; private set; }
    public bool Descending { get; private set; }

    /// <summary>«×»: clears the text and goes back to the order of insertion.</summary>
    public void Clear() { Query = ""; Order = 0; }

    /// <summary>«×» of the bar of the XAML view (<see cref="Clear"/>); usable in a read-only revision, as the historic bar.</summary>
    public IRelayCommand ClearCommand => clearCommand ??= new RelayCommand(Clear);
    private IRelayCommand? clearCommand;

    /// <summary>The column whose header shows the order (clicked last), and whether it shows it ascending; null before the first click.</summary>
    public string? SortedColumn { get; private set; }
    public bool SortedAscending { get; private set; }

    /// <summary>
    /// A click on the header of a column, as the historic grid: descending when the header showed it ascending, otherwise ascending; the
    /// other headers lose their arrow. The opening order by the key column shows no arrow.
    /// </summary>
    public void SortBy(string key)
    {
        table.CommitEdits();
        SortKey = key; Descending = SortedColumn == key && SortedAscending;
        SortedColumn = key; SortedAscending = !Descending;
        table.Refresh();
    }

    /// <summary>The state shown by the checks of the historic view when it differs from the opening one; null otherwise.</summary>
    public string? Description => Text.Length > 0 || SortKey != Key || Descending
        ? Key + " " + Match + " «" + Text + "» ordine " + (SortKey.Length == 0 ? "inserimento" : SortKey) + (Descending ? " decrescente" : "") : null;

    /// <summary>Whether a row passes the text of the filter.</summary>
    internal bool Includes(JsonRowViewModel row)
    {
        if (string.IsNullOrWhiteSpace(Text)) return true;
        string value = row.Values.S(Key), text = Text.Trim();
        return Match switch
        {
            "Inizia con" => value.StartsWith(text, StringComparison.CurrentCultureIgnoreCase),
            "Uguale a" => value.Equals(text, StringComparison.CurrentCultureIgnoreCase),
            "Non contiene" => !value.Contains(text, StringComparison.CurrentCultureIgnoreCase),
            _ => value.Contains(text, StringComparison.CurrentCultureIgnoreCase)
        };
    }

    // The Refresh of the historic bar: commit, read the bar, apply.
    private void Apply()
    {
        table.CommitEdits();
        Key = Fields.First(c => c.Header == column).Key;
        Text = query; Match = Modes[Math.Clamp(mode, 0, Modes.Length - 1)];
        SortKey = order == 0 ? "" : Key; Descending = order == 2;
        table.Refresh();
        table.OnFilterApplied();
    }
}

/// <summary>
/// A table of the RC section without WPF (the historic JsonGrid with its ListCollectionView, F5.10 S1): the rows of the document
/// (<see cref="JsonRowCollection"/>), the rows shown (filter and order of <see cref="Filter"/>, plus an optional base filter), the selected row
/// and whether the table accepts edits. As the collection view of the historic grid, an added row takes its place in the order, a removed
/// selected row passes the selection to the row at its place (or to the last), a filtered one to the first row shown, and a change of the
/// values of a row reorders the table only at <see cref="Refresh"/>.
/// </summary>
public sealed class ConcreteTableViewModel : ObservableObject
{
    private readonly Func<bool> sheetReadOnly;
    private List<JsonRowViewModel> shown = [];
    private JsonRowViewModel? selected;
    private bool readOnly;

    /// <param name="columns">Columns, in order.</param>
    /// <param name="rows">Rows of the table (also shared with other tables, as the actions of the domains).</param>
    /// <param name="sheetReadOnly">True while the sheet is a read-only revision: the table refuses the edits.</param>
    public ConcreteTableViewModel(IReadOnlyList<ConcreteColumnViewModel> columns, JsonRowCollection rows, Func<bool> sheetReadOnly)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(sheetReadOnly);
        Columns = columns; this.sheetReadOnly = sheetReadOnly;
        Rows = rows;
        Filter = new ConcreteTableFilterViewModel(this);
        rows.CollectionChanged += OnRowsChanged;
        Recompute();
    }

    public IReadOnlyList<ConcreteColumnViewModel> Columns { get; }
    public JsonRowCollection Rows { get; private set; }
    public ConcreteTableFilterViewModel Filter { get; }

    /// <summary>A filter of the module applied before the text of the bar (the domains); null for none.</summary>
    public Func<JsonRowViewModel, bool>? BaseFilter { get; set; }

    /// <summary>The rows shown, in order.</summary>
    public IReadOnlyList<JsonRowViewModel> Shown => shown;

    public JsonRowViewModel? Selected
    {
        get => selected;
        set
        {
            if (value is not null && !shown.Contains(value)) value = null;
            if (ReferenceEquals(selected, value)) return;
            selected = value;
            OnPropertyChanged(); OnPropertyChanged(nameof(SelectedIndex));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Index of the selected row in <see cref="Shown"/>; -1 without selection. Setting an index out of range does nothing.</summary>
    public int SelectedIndex
    {
        get => selected is null ? -1 : shown.IndexOf(selected);
        set { if (value < 0) Selected = null; else if (value < shown.Count) Selected = shown[value]; }
    }

    /// <summary>The table refuses the edits: by the module, or because the sheet is a read-only revision.</summary>
    public bool IsReadOnly { get => readOnly || sheetReadOnly(); set { readOnly = value; OnPropertyChanged(); } }

    /// <summary>The read-only revision of the sheet changed: <see cref="IsReadOnly"/> is read again (F5.10 S2).</summary>
    internal void RefreshReadOnly() => OnPropertyChanged(nameof(IsReadOnly));

    /// <summary>Raised after every change of the selection (also when a row leaves the table).</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised after the bar of the filter is applied (the domains update their selection).</summary>
    public event EventHandler? FilterApplied;

    /// <summary>Raised before the bar reads its state: the edits of the table are committed (nothing to do without a view).</summary>
    public event EventHandler? CommittingEdits;

    /// <summary>Applies filter and order again (the Refresh of the collection view).</summary>
    public void Refresh()
    {
        Recompute();
        OnPropertyChanged(nameof(Shown)); OnPropertyChanged(nameof(SelectedIndex));
    }

    /// <summary>Replaces the rows of the table (the domains switch between the actions of SLU and SLV): shown again from the start.</summary>
    public void Attach(JsonRowCollection rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Rows.CollectionChanged -= OnRowsChanged;
        Rows = rows;
        rows.CollectionChanged += OnRowsChanged;
        OnPropertyChanged(nameof(Rows));
        Refresh();
    }

    /// <summary>
    /// The view of the table is created over its rows (WithFilters of the historic view): as the collection view of the grid, its current
    /// item, the first row in the order of insertion, becomes the selection. Rows added later leave the selection as it is.
    /// </summary>
    public void StartView()
    {
        if (Rows.Count > 0 && shown.Contains(Rows[0])) Selected = Rows[0];
    }

    internal void CommitEdits() => CommittingEdits?.Invoke(this, EventArgs.Empty);

    internal void OnFilterApplied() => FilterApplied?.Invoke(this, EventArgs.Empty);

    private bool Passes(JsonRowViewModel row) => (BaseFilter is null || BaseFilter(row)) && Filter.Includes(row);

    private int Compare(JsonRowViewModel a, JsonRowViewModel b)
    {
        int result = TableValues.Compare(a.Values.S(Filter.SortKey), b.Values.S(Filter.SortKey));
        return Filter.Descending ? -result : result;
    }

    private void Recompute()
    {
        var rows = Rows.Where(Passes).ToList();
        if (Filter.SortKey.Length > 0) rows = rows.Order(Comparer<JsonRowViewModel>.Create(Compare)).ToList();
        shown = rows;
        // A selected row that the filter hides passes the selection to the first row shown (the current item of the collection view after
        // its Refresh, which the historic grid follows), or to none.
        if (selected is not null && !shown.Contains(selected)) Selected = shown.FirstOrDefault();
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { Count: 1 } added && added[0] is JsonRowViewModel row)
        {
            // An added row takes its place in the order (after the equal ones), or the place of its index in the order of insertion.
            if (Passes(row))
            {
                int index;
                if (Filter.SortKey.Length > 0) { index = shown.FindIndex(r => Compare(row, r) < 0); if (index < 0) index = shown.Count; }
                else index = shown.Count(r => Rows.IndexOf(r) < e.NewStartingIndex);
                shown.Insert(index, row);
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is { Count: 1 } removed && removed[0] is JsonRowViewModel old)
        {
            // The selection follows the current item of the collection view: a removed current row leaves it to the row now at its place,
            // or to the last one.
            int index = shown.IndexOf(old);
            shown.Remove(old);
            if (ReferenceEquals(selected, old)) Selected = shown.Count == 0 ? null : shown[Math.Min(index, shown.Count - 1)];
        }
        else Recompute();
        OnPropertyChanged(nameof(Shown)); OnPropertyChanged(nameof(SelectedIndex));
    }
}

/// <summary>
/// A choice outside the forms (the historic ComboBox of the materials of the cables, of the standard tendon, of the saved materials): items,
/// selection and the change of the selection. As the combo box, new items keep the selected item when it is still among them.
/// </summary>
public sealed class ConcreteChoiceViewModel : ObservableObject
{
    private IReadOnlyList<string> items = [];
    private int selectedIndex = -1;

    public IReadOnlyList<string> Items => items;

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value < -1 || value >= items.Count) value = -1;
            if (selectedIndex == value) return;
            selectedIndex = value;
            OnPropertyChanged(); OnPropertyChanged(nameof(SelectedItem));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The selected item; setting an item selects its first occurrence (an item not listed changes nothing).</summary>
    public string? SelectedItem
    {
        get => selectedIndex >= 0 ? items[selectedIndex] : null;
        set { int index = value is null ? -1 : IndexOf(value); if (value is null || index >= 0) SelectedIndex = index; }
    }

    /// <summary>Raised after every change of the selection, by the user or by the module.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>New items (the ItemsSource of the combo box): the selected item stays selected when it is still listed.</summary>
    public void SetItems(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        string? current = SelectedItem;
        items = values.ToArray();
        OnPropertyChanged(nameof(Items));
        int index = current is null ? -1 : IndexOf(current);
        if (index == selectedIndex) { OnPropertyChanged(nameof(SelectedItem)); return; }
        SelectedIndex = index;
    }

    private int IndexOf(string value)
    {
        for (int i = 0; i < items.Count; i++) if (items[i] == value) return i;
        return -1;
    }
}
