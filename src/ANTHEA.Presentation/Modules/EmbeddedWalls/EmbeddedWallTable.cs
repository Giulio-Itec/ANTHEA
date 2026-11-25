using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

public sealed class EmbeddedWallRow(JsonObject data, Action changed, Func<bool> editable) : ObservableObject
{
    internal JsonObject Data { get; } = data;
    public string this[string key]
    {
        get => Data[key]?.ToString() ?? "";
        set { if (!editable() || this[key] == value) return; Data[key] = value; Refresh(); changed(); }
    }
    public string Label => Data.ContainsKey("left_ground")
        ? $"{this["id"]} · sx {this["left_ground"]} / dx {this["right_ground"]} m"
        : Data["name"]?.ToString() ?? Data["id"]?.ToString() ?? Data["section"]?.ToString()
            ?? (Data.ContainsKey("support") ? $"{this["support"]} · fase {this["stage"]}" : $"Fase {this["stage"]} · quota {this["elevation"]}");
    internal void Refresh() { OnPropertyChanged("Item[]"); OnPropertyChanged(nameof(Label)); }
}

public sealed class EmbeddedWallTable : ObservableObject
{
    private readonly JsonArray data;
    private readonly Action changed;
    private readonly Func<bool> editable;
    private EmbeddedWallRow? selected;
    private FormViewModel? editor;
    private readonly IWikiNavigator? wikiNavigator;
    private readonly Func<JsonObject>? createRow;
    public EmbeddedWallTable(string key, JsonArray data, Action changed, Func<bool> editable, CommandGuard guard, IWikiNavigator? wikiNavigator = null,
        Func<JsonObject>? createRow = null)
    {
        Key = key; this.data = data; this.changed = changed; this.editable = editable; this.wikiNavigator = wikiNavigator;
        this.createRow = createRow;
        Columns = EmbeddedWallFields.Columns(key); Rows = new(data.Cast<JsonObject>().Select(Wrap));
        AddCommand = GuardedCommands.Create(guard, Add, editable); RemoveCommand = GuardedCommands.Create(guard, Remove, editable);
        UpCommand = GuardedCommands.Create(guard, () => Move(-1), editable); DownCommand = GuardedCommands.Create(guard, () => Move(1), editable);
        Selected = Rows.FirstOrDefault();
    }
    public string Key { get; }
    public string Title => EmbeddedWallFields.Title(Key);
    public IReadOnlyList<FieldSpec> Columns { get; }
    public ObservableCollection<EmbeddedWallRow> Rows { get; }
    public bool IsReadOnly => !editable();
    public EmbeddedWallRow? Selected
    {
        get => selected;
        set
        {
            if (!SetProperty(ref selected, value)) return;
            CreateEditor();
        }
    }
    private void CreateEditor()
    {
        editor = selected is not { } row ? null : new(row.Data, Columns, _ => { row.Refresh(); changed(); }, wikiModule: EmbeddedWallDocument.Module)
            { IsReadOnly = IsReadOnly, WikiNavigator = wikiNavigator };
        OnPropertyChanged(nameof(Editor));
    }
    /// <summary>
    /// After a rule of ANTHEA.Application changed the array in place (the menu Fase and the boxes of the command bar, organizzazione del modulo
    /// paratie, progetto del 16/11): the rows follow the array, keeping the object of every row still present (the work tree keeps its nodes);
    /// labels and editor show the values again. A selected row that is gone leaves no selection. Nothing is written.
    /// </summary>
    internal void Synchronize()
    {
        var current = data.OfType<JsonObject>().ToArray();
        for (int i = Rows.Count - 1; i >= 0; i--) if (!current.Any(r => ReferenceEquals(r, Rows[i].Data))) Rows.RemoveAt(i);
        for (int i = 0; i < current.Length; i++)
        {
            int old = -1;
            for (int j = i; j < Rows.Count && old < 0; j++) if (ReferenceEquals(Rows[j].Data, current[i])) old = j;
            if (old < 0) Rows.Insert(i, Wrap(current[i]));
            else if (old != i) Rows.Move(old, i);
        }
        foreach (var row in Rows) row.Refresh();
        if (selected is not null && !Rows.Contains(selected)) Selected = null;
        else CreateEditor();
    }
    public FormViewModel? Editor => editor;
    public IRelayCommand AddCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand UpCommand { get; }
    public IRelayCommand DownCommand { get; }
    private EmbeddedWallRow Wrap(JsonObject row) => new(row, changed, editable);
    private void Add()
    {
        if (!editable()) return; var row = createRow?.Invoke() ?? EmbeddedWallDocument.NewRow(Key); data.Add(row);
        var vm = Wrap(row); Rows.Add(vm); Selected = vm; changed();
    }
    private void Remove()
    {
        if (!editable() || Selected is null) return; int i = Rows.IndexOf(Selected);
        data.RemoveAt(i); Rows.RemoveAt(i); Selected = Rows.ElementAtOrDefault(Math.Min(i, Rows.Count - 1)); changed();
    }
    private void Move(int delta)
    {
        if (!editable() || Selected is null) return; int i = Rows.IndexOf(Selected), j = i + delta;
        if (j < 0 || j >= Rows.Count) return;
        var row = data[i]; data.RemoveAt(i); data.Insert(j, row); Rows.Move(i, j); changed();
    }
    internal void RefreshAccess()
    {
        if (editor is not null) editor.IsReadOnly = IsReadOnly;
        OnPropertyChanged(nameof(IsReadOnly)); AddCommand.NotifyCanExecuteChanged(); RemoveCommand.NotifyCanExecuteChanged(); UpCommand.NotifyCanExecuteChanged(); DownCommand.NotifyCanExecuteChanged();
    }
}
