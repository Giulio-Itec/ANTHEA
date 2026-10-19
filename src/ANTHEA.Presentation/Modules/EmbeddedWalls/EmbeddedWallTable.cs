using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

public sealed class EmbeddedWallRow(JsonObject data, Action changed, Func<bool> editable) : ObservableObject
{
    internal JsonObject Data { get; } = data;
    public string this[string key]
    {
        get => Data[key]?.ToString() ?? "";
        set { if (!editable() || this[key] == value) return; Data[key] = value; OnPropertyChanged("Item[]"); changed(); }
    }
    internal void Refresh() => OnPropertyChanged("Item[]");
}

public sealed class EmbeddedWallTable : ObservableObject
{
    private readonly JsonArray data;
    private readonly Action changed;
    private readonly Func<bool> editable;
    private EmbeddedWallRow? selected;
    private FormViewModel? editor;
    public EmbeddedWallTable(string key, JsonArray data, Action changed, Func<bool> editable, CommandGuard guard)
    {
        Key = key; this.data = data; this.changed = changed; this.editable = editable;
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
            editor = value is null ? null : new(value.Data, Columns, _ => { value.Refresh(); changed(); }) { IsReadOnly = IsReadOnly };
            OnPropertyChanged(nameof(Editor));
        }
    }
    public FormViewModel? Editor => editor;
    public IRelayCommand AddCommand { get; }
    public IRelayCommand RemoveCommand { get; }
    public IRelayCommand UpCommand { get; }
    public IRelayCommand DownCommand { get; }
    private EmbeddedWallRow Wrap(JsonObject row) => new(row, changed, editable);
    private void Add()
    {
        if (!editable()) return; var row = EmbeddedWallDocument.NewRow(Key); data.Add(row);
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
