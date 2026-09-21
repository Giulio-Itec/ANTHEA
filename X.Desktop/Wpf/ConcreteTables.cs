using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private sealed class TableFilter
    {
        internal string Key = "nome", Query = "", Match = "Contiene", SortKey = "nome";
        internal bool Descending;
        internal Predicate<object>? Base;
        internal ListCollectionView? View;
    }
    private readonly Dictionary<JsonGrid, TableFilter> tableFilters = new();
    private UIElement WithFilters(JsonGrid grid)
    {
        var state = new TableFilter { Key = grid.Columns.Any(c => c.SortMemberPath == "nome") ? "nome" : "id" };
        state.SortKey = state.Key; tableFilters[grid] = state;
        var fields = grid.Columns.Where(c => c.SortMemberPath != "visible").ToArray();
        var column = Ui.Choice(fields.Select(c => c.Header.ToString()!).ToArray(), fields.FirstOrDefault(c => c.SortMemberPath == state.Key)?.Header.ToString() ?? fields[0].Header.ToString()!); column.MinWidth = 105;
        var mode = Ui.Choice(["Contiene", "Inizia con", "Uguale a", "Non contiene"], "Contiene"); mode.Width = 112;
        var query = new TextBox { MinWidth = 95, Width = 140, ToolTip = "Filtro testo (senza distinzione maiuscole/minuscole). Non elimina righe né esclude combinazioni dal calcolo." };
        var order = Ui.Choice(["Ordine inserimento", "A → Z / crescente", "Z → A / decrescente"], "Ordine inserimento"); order.Width = 158;
        void Refresh()
        {
            grid.Commit(); state.Key = fields.First(c => c.Header.ToString() == column.SelectedItem?.ToString()).SortMemberPath;
            state.Query = query.Text; state.Match = mode.SelectedItem?.ToString() ?? "Contiene"; state.SortKey = order.SelectedIndex == 0 ? "" : state.Key; state.Descending = order.SelectedIndex == 2;
            ApplyTableFilter(grid);
            foreach (var panel in domainPanels.Where(p => p.Grid == grid)) UpdateSelection(panel);
        }
        query.TextChanged += (_, _) => Refresh(); mode.SelectionChanged += (_, _) => Refresh(); column.SelectionChanged += (_, _) => Refresh(); order.SelectionChanged += (_, _) => Refresh();
        grid.CanUserSortColumns = true;
        grid.Sorting += (_, e) =>
        {
            e.Handled = true; grid.Commit(); state.SortKey = e.Column.SortMemberPath; state.Descending = e.Column.SortDirection == ListSortDirection.Ascending;
            foreach (var c in grid.Columns) c.SortDirection = null;
            e.Column.SortDirection = state.Descending ? ListSortDirection.Descending : ListSortDirection.Ascending; ApplyTableFilter(grid);
        };
        ApplyTableFilter(grid);
        return Ui.Dock(grid, Ui.Bar(Ui.Text("Filtra", 11), column, mode, query, order, Ui.Button("×", () => { query.Clear(); order.SelectedIndex = 0; })));
    }
    private void ApplyTableFilter(JsonGrid grid)
    {
        if (!tableFilters.TryGetValue(grid, out var f)) return;
        var view = grid.ItemsSource as ListCollectionView;
        if (view is null) { view = new ListCollectionView(grid.Rows); grid.ItemsSource = view; }
        if (view.IsEditingItem || view.IsAddingNew) return;
        if (!ReferenceEquals(f.View, view)) { f.View = view; f.Base = view.Filter; }
        using (view.DeferRefresh())
        {
            view.Filter = item =>
            {
                if (f.Base is not null && !f.Base(item)) return false;
                if (item is not JsonRow row || string.IsNullOrWhiteSpace(f.Query)) return true;
                string value = row.Values.S(f.Key), query = f.Query.Trim();
                return f.Match switch { "Inizia con" => value.StartsWith(query, StringComparison.CurrentCultureIgnoreCase), "Uguale a" => value.Equals(query, StringComparison.CurrentCultureIgnoreCase), "Non contiene" => !value.Contains(query, StringComparison.CurrentCultureIgnoreCase), _ => value.Contains(query, StringComparison.CurrentCultureIgnoreCase) };
            };
            view.CustomSort = string.IsNullOrEmpty(f.SortKey) ? null : new RowComparer(f.SortKey, f.Descending);
        }
    }
    private sealed class RowComparer(string key, bool descending) : IComparer
    {
        public int Compare(object? x, object? y)
        {
            string a = (x as JsonRow)?.Values.S(key) ?? "", b = (y as JsonRow)?.Values.S(key) ?? "";
            int result = J.Number(System.Text.Json.Nodes.JsonValue.Create(a)) is double na && J.Number(System.Text.Json.Nodes.JsonValue.Create(b)) is double nb ? na.CompareTo(nb) : StringComparer.CurrentCultureIgnoreCase.Compare(a, b);
            return descending ? -result : result;
        }
    }
}
