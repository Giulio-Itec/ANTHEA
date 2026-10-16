using Anthea.Calculations;
using Anthea.Presentation.Mvvm;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace Anthea.Presentation.Forms;

/// <summary>
/// Row of a table bound to a <see cref="JsonObject"/> of the document: the former JsonRow of X.Desktop Ui.cs, moved without
/// changes in F5.2 (proposal F5, sections 2.1 and 3.2). It keeps the file format and the unparsed input of the user: the
/// indexer returns a JSON boolean as bool and anything else as text, and writes <c>J.Node(value)</c>. Notifications can be
/// deferred for a batch of rows (<see cref="DeferNotifications"/>). The historic views write JsonRow, an alias of this class.
/// </summary>
public sealed class JsonRowViewModel : INotifyPropertyChanged
{
    /// <summary>Object of the view that owns the row (e.g. the soil layer it edits); never read by the row.</summary>
    public object? Context { get; set; }
    private int notificationDepth;
    private bool notificationPending;
    /// <summary>Holds the change notifications of the rows until the scope is disposed, then raises one per changed row.</summary>
    public static IDisposable DeferNotifications(IEnumerable<JsonRowViewModel> rows)
    {
        var snapshot = rows.Distinct().ToArray();
        foreach (var row in snapshot) row.notificationDepth++;
        return new UpdateScope(() =>
        {
            foreach (var row in snapshot)
                if (--row.notificationDepth == 0 && row.notificationPending)
                { row.notificationPending = false; row.PropertyChanged?.Invoke(row, new("Item[]")); }
        });
    }
    private void Notify()
    {
        if (notificationDepth > 0) notificationPending = true;
        else PropertyChanged?.Invoke(this, new("Item[]"));
    }
    public JsonObject Values { get; }
    private readonly Action<string>? changed;
    public event PropertyChangedEventHandler? PropertyChanged;
    public JsonRowViewModel(JsonObject values, Action<string>? changed = null) { Values = values; this.changed = changed; }
    public void Refresh() => Notify();
    public object? this[string key]
    {
        get => Values[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : Values.S(key);
        set { Values[key] = J.Node(value); Notify(); changed?.Invoke(key); }
    }
    /// <summary>Writes a calculated value into the row, notifying only when it changes; no change callback.</summary>
    public void Output(string key, object? value) { var node = J.Node(value); if (JsonNode.DeepEquals(Values[key], node)) return; Values[key] = node; Notify(); }
}

/// <summary>
/// Rows of a table: the former JsonRows of X.Desktop Ui.cs, moved without changes in F5.2. While a <see cref="DeferRefresh"/>
/// scope is open the collection raises nothing; at the end it raises Count, Item[] and one reset if anything changed.
/// </summary>
public sealed class JsonRowCollection : ObservableCollection<JsonRowViewModel>
{
    private int updateDepth;
    private bool pending;
    /// <summary>Holds the notifications of the collection until the scope is disposed, then raises one reset.</summary>
    public IDisposable DeferRefresh()
    {
        updateDepth++;
        return new UpdateScope(() =>
        {
            if (--updateDepth != 0 || !pending) return;
            pending = false;
            base.OnPropertyChanged(new("Count"));
            base.OnPropertyChanged(new("Item[]"));
            base.OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
        });
    }
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    { if (updateDepth > 0) pending = true; else base.OnCollectionChanged(e); }
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    { if (updateDepth == 0) base.OnPropertyChanged(e); }
}
