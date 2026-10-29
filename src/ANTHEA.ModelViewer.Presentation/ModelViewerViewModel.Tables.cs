using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Data;
using System.Globalization;
using System.Text;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    // 1.16.0: reactions of the current case, masses, temperatures and stages of the copy (Straus7), after the historical kinds (saved indexes unchanged).
    public IReadOnlyList<string> TableKinds { get; } = ["Nodi", "Elementi", "Sezioni", "Spessori e offset", "Vincoli e collegamenti", "Risultato corrente", "Attributi della selezione",
        "Reazioni vincolari", "Masse", "Temperature", "Fasi"];
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
    partial void OnShowTableChanged(bool value) { if (value) {ShowMemberDiagram=false;ShowRevisionDetails=false;RefreshTable(true);} }
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
            TableOnlySelected ? string.Join(",", SelectedElementIds.Order()) + "/" + string.Join(",", SelectedNodeIds.Order()) : "all", TableSearch,
            SelectedTableKind == 5 ? $"{SelectedCase}/{Family}/{Axes}/{SelectedComponent}/{ForceUnit}/{LengthUnit}/{DisplacementUnit}/{SelectedBeamLocation}" : SelectedTableKind == 7 ? SelectedCase : "",
            SelectedTableKind == 6 ? SelectedTreeItem?.Key : "");
        if (!force && tableState == state) return;
        tableState = state;
        var table = new DataTable(TableKinds[Math.Clamp(SelectedTableKind, 0, TableKinds.Count - 1)]) { Locale = CultureInfo.CurrentCulture };
        string? extraSummary = null;
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
                foreach (var s in Snapshot.Sections.Where(s => !TableOnlyVisible && !TableOnlySelected || elements.Any(e => e.Type != "PLATE" && e.Property == s.Id))) Add("SECT:" + s.Id, s.Id, s.Name, s.Shape, s.Width > 0 ? s.Width : null, s.Height > 0 ? s.Height : null, s.Width > 0 ? s.Centered : null);
                break;
            case 3:
                Columns(("ID", typeof(int)), ("Nome", typeof(string)), ("Spessore [m]", typeof(double)), ("Offset [m]", typeof(double)));
                foreach (var p in Snapshot.Plates.Where(p => !TableOnlyVisible && !TableOnlySelected || elements.Any(e => e.Type == "PLATE" && e.Property == p.Id))) Add("THIK:" + p.Id, p.Id, p.Name, p.Thickness, p.Offset);
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
                var derivation=Snapshot.Results.FirstOrDefault(r=>r.Name==SelectedCase)?.Combination;
                var governants=derivation?.Governing.Where(g=>g.Component==SelectedComponent&&g.Family==Family).ToDictionary(g=>(g.Element,g.Node,g.Station));
                if (IsDisplacementResult)
                {
                    Columns(("Nodo", typeof(int)), ("Caso", typeof(string)), ("Assi", typeof(string)), ("Componente", typeof(string)), ("Unità", typeof(string)), ("Valore", typeof(double)));
                    foreach (var v in ActiveValues.Where(v => !TableOnlySelected || SelectedNodeIds.Contains(v.Node)).DistinctBy(v => v.Node).OrderBy(v => v.Node))
                        Add("NODE:" + v.Node, v.Node, SelectedCase, Axes, SelectedComponent, ResultUnit, v.Value);
                    break;
                }
                Columns(("Elemento", typeof(int)), ("Nodo", typeof(int)), ("x/L", typeof(double)), ("Caso", typeof(string)), ("Famiglia", typeof(string)), ("Assi", typeof(string)), ("Componente", typeof(string)), ("Unità", typeof(string)), ("Valore", typeof(double)));
                if(governants?.Count>0)Columns(("Governante",typeof(string)),("Alternativa",typeof(string)),("Stato concomitante [kN, m]",typeof(string)));
                // Use exactly the displayed field and units; never average shared nodes or combine envelope extrema.
                foreach (var v in ActiveValues.Where(v => !TableOnlySelected || SelectedElementIds.Contains(v.Element)))
                {
                    object?[] row=[v.Element,v.Node==0?null:v.Node,v.Station,SelectedCase,Family,Axes,SelectedComponent,ResultUnit,v.Value];
                    if(governants?.Count>0)
                    {
                        governants.TryGetValue((v.Element,v.Node,v.Station),out var g);
                        string label=g?.Alternative??"";
                        string alternative=int.TryParse(label.AsSpan(Math.Min(1,label.Length)),out int n)&&derivation?.Alternatives is {} alternatives&&n>0&&n<=alternatives.Length?alternatives[n-1]:label;
                        row=[..row,label,alternative,g==null?"":string.Join("; ",ANTHEA.ModelWorkspace.ResultFields.Components(Family,"LOCAL").Zip(g.ConcomitantValues,(c,x)=>c+"="+x.ToString("G9",CultureInfo.CurrentCulture)))];
                    }
                    Add("ELEM:"+v.Element,row);
                }
                break;
            case 7:
                {
                    // Reported reactions of the current case (force of the support on the structure, global axes); the resultants come from the GPC check.
                    var result = Snapshot.Results.FirstOrDefault(r => r.Name == SelectedCase);
                    Columns(("Nodo", typeof(int)), ("Fx [kN]", typeof(double)), ("Fy [kN]", typeof(double)), ("Fz [kN]", typeof(double)),
                        ("Mx [kN·m]", typeof(double)), ("My [kN·m]", typeof(double)), ("Mz [kN·m]", typeof(double)));
                    foreach (var r in result?.Reactions ?? [])
                        if ((AllNodes || nodes.Contains(r.Node)) && (!TableOnlySelected || SelectedNodeIds.Contains(r.Node))) Add("NODE:" + r.Node, r.Node, r.Fx, r.Fy, r.Fz, r.Mx, r.My, r.Mz);
                    extraSummary = result == null ? "nessun caso scelto" : result.Reactions == null ? "il caso non ha reazioni importate" : ReactionSummary(result.Equilibrium);
                    break;
                }
            case 8:
                Columns(("Tipo", typeof(string)), ("Nodo / elemento", typeof(int)), ("Caso", typeof(string)), ("Valori", typeof(string)), ("Unità", typeof(string)),
                    ("Offset [m]", typeof(string)), ("Fattore dinamico", typeof(double)), ("Sorgente", typeof(string)));
                foreach (var m in Snapshot.Masses ?? [])
                {
                    bool node = m.Kind.StartsWith("NODE", StringComparison.Ordinal);
                    if (node ? !AllNodes && !nodes.Contains(m.Entity) || TableOnlySelected && !SelectedNodeIds.Contains(m.Entity) : !Accept(m.Entity)) continue;
                    Add((node ? "NODE:" : "ELEM:") + m.Entity, MassLabel(m.Kind), m.Entity, m.Case, Values(m.Values, m.Stations), MassUnit(m.Kind),
                        m.Offset is { } o ? Values([o.X, o.Y, o.Z], null) : null, m.DynamicFactor, m.Record);
                }
                extraSummary = "dati del modello: nessun carico ne deriva";
                break;
            case 9:
                Columns(("Tipo", typeof(string)), ("Nodo / elemento", typeof(int)), ("Caso", typeof(string)), ("Valori", typeof(string)), ("Unità", typeof(string)),
                    ("Tipo di temperatura", typeof(string)), ("Sorgente", typeof(string)));
                foreach (var x in Snapshot.Temperatures ?? [])
                {
                    bool node = x.Kind is "NODE" or "REFERENCE";
                    if (x.Kind == "NODE" && (!AllNodes && !nodes.Contains(x.Entity) || TableOnlySelected && !SelectedNodeIds.Contains(x.Entity)) || !node && !Accept(x.Entity)) continue;
                    Add(x.Kind == "REFERENCE" ? "" : (node ? "NODE:" : "ELEM:") + x.Entity, TemperatureLabel(x.Kind), x.Kind == "REFERENCE" ? null : x.Entity, x.Case,
                        Values(x.Values, null), TemperatureUnit(x.Kind), x.Type, x.Record);
                }
                extraSummary = "dati del modello: nessun carico ne deriva";
                break;
            case 10:
                Columns(("Fase", typeof(string)), ("Tipo", typeof(string)), ("Gruppi attivi", typeof(string)), ("Elementi attivi", typeof(int)), ("Risultati per fase", typeof(int)));
                foreach (var s in Snapshot.ConstructionStages ?? [])
                    Add("", s.Label, StageTypeLabel(s.Type), string.Join(", ", s.Groups), s.Elements.Count(e => !TableOnlyVisible || VisibleElementIds == null || VisibleElementIds.Contains(e)),
                        Snapshot.Results.Count(r => r.Stage?.Phase == s.Label));
                extraSummary = "fasi della sorgente: attivazione dei gruppi, nessuna simulazione in ANTHEA";
                break;
            default:
                Columns(("Attributo", typeof(string)), ("Valore", typeof(string)));
                foreach (var a in Attributes) Add(SelectedTreeItem?.Key ?? "", a.Name, a.Value);
                break;
        }
        TableRows = table.DefaultView;
        TableSummary = $"{table.Rows.Count:N0} righe · sola lettura · copia con Ctrl+C · ordina dalle intestazioni";
        if (SelectedTableKind == 5) TableSummary += " · campo e unità della vista";
        if (extraSummary != null) TableSummary += " · " + extraSummary;
        ExportTableCommand.NotifyCanExecuteChanged();
    }

    static string Values(double[] values, double[]? stations) => string.Join("; ", values.Select((v, i) =>
        (stations != null && i < stations.Length ? "x/L " + stations[i].ToString("G4", CultureInfo.CurrentCulture) + ": " : "") + v.ToString("G6", CultureInfo.CurrentCulture)));
    static string MassLabel(string kind) => kind switch
    {
        "NODE" => "Massa nodale", "NODE-ROTATION" => "Massa rotazionale nodale", "NODE-NS" => "Massa non strutturale · nodo",
        "BEAM-NS" => "Massa non strutturale · asta", "PLATE-NS" => "Massa non strutturale · piastra", _ => kind
    };
    static string MassUnit(string kind) => kind switch
    {
        "NODE" => "t (X, Y, Z globali)", "NODE-ROTATION" => "t·m² (assi 1, 2, 3)", "BEAM-NS" => "t/m", "PLATE-NS" => "t/m²", _ => "t"
    };
    static string TemperatureLabel(string kind) => kind switch
    {
        "REFERENCE" => "Temperatura di riferimento del caso", "NODE" => "Temperatura nodale", "BEAM-GRADIENT" => "Gradiente termico · asta",
        "PLATE-GRADIENT" => "Gradiente termico · piastra", _ => kind
    };
    static string TemperatureUnit(string kind) => kind switch { "BEAM-GRADIENT" => "°C/m (assi principali 1, 2)", "PLATE-GRADIENT" => "°C/m (z locale)", _ => "°C" };
    static string StageTypeLabel(string type) => type switch { "Morphed" => "Trasformata (morphed)", "Reset" => "Ripartenza (reset)", "Unmorphed" => "Non trasformata (unmorphed)", _ => type };
    static string ReactionSummary(ANTHEA.ModelWorkspace.ModelEquilibrium? balance)
    {
        if (balance == null) return "equilibrio non controllato per questo risultato";
        string Vector(double[] v) => string.Join(", ", v.Select(x => x.ToString("G6", CultureInfo.CurrentCulture)));
        return $"risultante delle reazioni [{Vector(balance.ReactionForce)}] kN, dei carichi [{Vector(balance.AppliedForce)}] kN · " + balance.Note;
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
        try { services.ExportTable(TableKinds[Math.Clamp(SelectedTableKind, 0, TableKinds.Count - 1)], TableCsv()); }
        catch (Exception ex) { Status = "Tabella non esportata: " + ex.Message; }
    }
}
