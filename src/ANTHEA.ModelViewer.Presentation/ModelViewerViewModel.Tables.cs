using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Data;
using System.Globalization;
using System.Text;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    public IReadOnlyList<string> TableKinds { get; } = ["Nodi", "Elementi", "Sezioni", "Spessori e offset", "Vincoli e collegamenti", "Risultato corrente", "Attributi della selezione"];
    public bool CanFilterTableVisibility => SelectedTableKind != 5;
    [ObservableProperty] private bool showTable;
    [ObservableProperty] private int selectedTableKind = 1;
    [ObservableProperty] private bool tableOnlyVisible = true;
    [ObservableProperty] private bool tableOnlySelected;
    [ObservableProperty] private string tableSearch = "";
    [ObservableProperty] private DataView? tableRows;
    [ObservableProperty] private DataRowView? selectedTableRow;
    [ObservableProperty] private string tableSummary = "";
    private string? tableState;
    partial void OnShowTableChanged(bool value) { if (value) RefreshTable(true); }
    partial void OnSelectedTableKindChanged(int value)
    {
        if (value == 5) TableOnlyVisible = true;
        OnPropertyChanged(nameof(CanFilterTableVisibility)); RefreshTable(true);
    }
    partial void OnTableOnlyVisibleChanged(bool value) => RefreshTable(true);
    partial void OnTableOnlySelectedChanged(bool value) => RefreshTable(true);
    partial void OnTableSearchChanged(string value) => RefreshTable(true);
    partial void OnSelectedTableRowChanged(DataRowView? value)
    {
        if (value?.Row["ObjectKey"] is string key && FindTreeItem(key) is { } item) SelectedTreeItem = item;
    }

    void RefreshTable(bool force = false)
    {
        if (!ShowTable || Snapshot == null || disposed || updating) return;
        string state = string.Join("|", SelectedTableKind, TableOnlyVisible ? VisibleElementIds == null ? "all" : string.Join(",", VisibleElementIds.Order()) : "all",
            TableOnlySelected ? string.Join(",", SelectedElementIds.Order()) : "all", TableSearch,
            SelectedTableKind == 5 ? $"{SelectedCase}/{Family}/{Axes}/{SelectedComponent}/{ForceUnit}/{LengthUnit}/{SelectedBeamLocation}" : "",
            SelectedTableKind == 6 ? SelectedTreeItem?.Key : "");
        if (!force && tableState == state) return;
        tableState = state;
        var table = new DataTable(TableKinds[Math.Clamp(SelectedTableKind, 0, 6)]) { Locale = CultureInfo.CurrentCulture };
        table.Columns.Add("ObjectKey", typeof(string));
        void Columns(params (string Name, Type Type)[] columns) { foreach (var c in columns) table.Columns.Add(c.Name, c.Type); }
        void Add(string key, params object?[] values)
        {
            if (!string.IsNullOrWhiteSpace(TableSearch) && !values.Any(v => Convert.ToString(v, CultureInfo.CurrentCulture)?.Contains(TableSearch.Trim(), StringComparison.OrdinalIgnoreCase) == true)) return;
            table.Rows.Add(new object[] { key }.Concat(values.Select(v => v ?? DBNull.Value)).ToArray());
        }
        bool Accept(int id) => (!TableOnlyVisible || VisibleElementIds == null || VisibleElementIds.Contains(id)) && (!TableOnlySelected || SelectedElementIds.Contains(id));
        var elements = Snapshot.Elements.Where(e => Accept(e.Id)).ToArray();
        var nodes = elements.SelectMany(e => e.Nodes).ToHashSet();
        bool AllNodes = !TableOnlySelected && (!TableOnlyVisible || VisibleElementIds == null);
        switch (SelectedTableKind)
        {
            case 0:
                Columns(("ID", typeof(int)), ("X [m]", typeof(double)), ("Y [m]", typeof(double)), ("Z [m]", typeof(double)));
                foreach (var n in Snapshot.Nodes.Where(n => AllNodes || nodes.Contains(n.Id))) Add("NODE:" + n.Id, n.Id, n.X, n.Y, n.Z);
                break;
            case 1:
                Columns(("ID", typeof(int)), ("Tipo", typeof(string)), ("Proprietà", typeof(int)), ("Nodi", typeof(string)), ("Rotazione [°]", typeof(double)));
                foreach (var e in elements) Add("ELEM:" + e.Id, e.Id, e.Type, e.Property, string.Join(", ", e.Nodes), e.Angle);
                break;
            case 2:
                Columns(("ID", typeof(int)), ("Nome", typeof(string)), ("Forma", typeof(string)), ("Larghezza [m]", typeof(double)), ("Altezza [m]", typeof(double)), ("Centrata", typeof(bool)));
                foreach (var s in Snapshot.Sections.Where(s => elements.Any(e => e.Type != "PLATE" && e.Property == s.Id))) Add("SECT:" + s.Id, s.Id, s.Name, s.Shape, s.Width > 0 ? s.Width : null, s.Height > 0 ? s.Height : null, s.Width > 0 ? s.Centered : null);
                break;
            case 3:
                Columns(("ID", typeof(int)), ("Nome", typeof(string)), ("Spessore [m]", typeof(double)), ("Offset [m]", typeof(double)));
                foreach (var p in Snapshot.Plates.Where(p => elements.Any(e => e.Type == "PLATE" && e.Property == p.Id))) Add("THIK:" + p.Id, p.Id, p.Name, p.Thickness, p.Offset);
                break;
            case 4:
                Columns(("Sorgente", typeof(string)), ("Tipo", typeof(string)), ("Nodi", typeof(string)), ("Elemento", typeof(int)), ("DOF / informazioni", typeof(string)));
                foreach (var b in Snapshot.Boundaries ?? [])
                {
                    if (!AllNodes && !b.Nodes.Any(nodes.Contains)) continue;
                    string key = string.Join(":", b.SourceRecord.Split('/').Take(2));
                    Add(key, b.SourceRecord, b.Kind, string.Join(", ", b.Nodes), b.Element, b.Dofs);
                }
                break;
            case 5:
                Columns(("Elemento", typeof(int)), ("Nodo", typeof(int)), ("x/L", typeof(double)), ("Caso", typeof(string)), ("Famiglia", typeof(string)), ("Assi", typeof(string)), ("Componente", typeof(string)), ("Unità", typeof(string)), ("Valore", typeof(double)));
                // Use exactly the displayed field and units; never average shared nodes or combine envelope extrema.
                foreach (var v in ActiveValues.Where(v => !TableOnlySelected || SelectedElementIds.Contains(v.Element)))
                    Add("ELEM:" + v.Element, v.Element, v.Node == 0 ? null : v.Node, v.Station, SelectedCase, Family, Axes, SelectedComponent, ResultUnit, v.Value);
                break;
            default:
                Columns(("Attributo", typeof(string)), ("Valore", typeof(string)));
                foreach (var a in Attributes) Add(SelectedTreeItem?.Key ?? "", a.Name, a.Value);
                break;
        }
        TableRows = table.DefaultView;
        TableSummary = $"{table.Rows.Count:N0} righe · sola lettura · copia con Ctrl+C · ordina dalle intestazioni";
        if (SelectedTableKind == 5) TableSummary += " · campo e unità della vista";
        ExportTableCommand.NotifyCanExecuteChanged();
    }

    public string TableCsv()
    {
        if (TableRows?.Table == null) return "";
        var columns = TableRows.Table.Columns.Cast<DataColumn>().Where(c => c.ColumnName != "ObjectKey").ToArray();
        static string Cell(object value)
        {
            string text = value == DBNull.Value ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            // Text from an imported model must remain text when opened in a spreadsheet.
            if (value is string && text.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        var csv = new StringBuilder(); csv.AppendLine(string.Join(";", columns.Select(c => Cell(c.ColumnName))));
        foreach (DataRowView row in TableRows) csv.AppendLine(string.Join(";", columns.Select(c => Cell(row[c.ColumnName]))));
        return csv.ToString();
    }
    private bool CanExportTable() => !disposed && TableRows?.Count > 0;
    [RelayCommand(CanExecute = nameof(CanExportTable))]
    private void ExportTable()
    {
        try { services.ExportTable(TableKinds[Math.Clamp(SelectedTableKind, 0, 6)], TableCsv()); }
        catch (Exception ex) { Status = "Tabella non esportata: " + ex.Message; }
    }
}
