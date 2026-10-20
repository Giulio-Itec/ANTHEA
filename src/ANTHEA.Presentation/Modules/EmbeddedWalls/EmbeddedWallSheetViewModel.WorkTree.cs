using System.Collections.ObjectModel;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

public sealed partial class EmbeddedWallSheetViewModel
{
    private sealed record WorkGroup(string Area, string Case = "", EmbeddedWallRow? Stage = null);
    private readonly Dictionary<object, EmbeddedWallWorkNode> workNodes = [];
    private bool workTreeCollapsed, refreshingWorkTree, selectingWorkNode;
    private EmbeddedWallWorkNode? selectedWorkNode;
    public ObservableCollection<EmbeddedWallWorkNode> WorkTree { get; } = [];
    public bool IsWorkTreeCollapsed { get => workTreeCollapsed; set => SetProperty(ref workTreeCollapsed, value); }
    public IRelayCommand ToggleWorkTreeCommand { get; private set; } = null!;
    public IRelayCommand<EmbeddedWallWorkNode> SelectWorkNodeCommand { get; private set; } = null!;
    public string WorkTreeSummary => $"{Tables.Sum(t => t.Rows.Count)} righe di dati · {Tables.FirstOrDefault(t => t.Key == "stages")?.Rows.Count ?? 0} fasi";

    private void InitializeWorkTree()
    {
        ToggleWorkTreeCommand = GuardedCommands.Create(Guard, () => IsWorkTreeCollapsed = !IsWorkTreeCollapsed);
        SelectWorkNodeCommand = new RelayCommand<EmbeddedWallWorkNode>(node => Guard.Run(() => SelectWorkNode(node)));
        RefreshWorkTree();
    }

    private void SelectWorkNode(EmbeddedWallWorkNode? node)
    {
        if (refreshingWorkTree || selectingWorkNode || node is null || !workNodes.Values.Contains(node)) return;
        selectingWorkNode = true;
        try
        {
            if (selectedWorkNode is not null && selectedWorkNode != node) selectedWorkNode.IsSelected = false;
            selectedWorkNode = node; node.IsSelected = true;
            if (node.CaseId is not null && InputCases.Contains(node.CaseId)) InputCase = node.CaseId;
            if (node.Stage is not null && InputStages.Contains(node.Stage)) InputStage = node.Stage;
            if (node.Target == "hydraulics") { WorkspaceTab = 5; return; }
            if (node.Target == "embedment") { WorkspaceTab = 2; return; }
            if (node.Target == "general") { EditModelCommand.Execute("general"); return; }
            if (node.TableKey is null) return;
            EditModelCommand.Execute(node.TableKey);
            if (node.Row is not null && InspectorTable!.Rows.Contains(node.Row)) InspectorTable.Selected = node.Row;
        }
        finally { selectingWorkNode = false; }
    }

    private void SyncWorkTreeSelection()
    {
        if (refreshingWorkTree || selectingWorkNode) return;
        EmbeddedWallWorkNode? node = InspectorTable?.Selected is { } row
            ? workNodes.GetValueOrDefault(row)
            : InspectorTable is null ? workNodes.GetValueOrDefault(new WorkGroup("general")) : null;
        if (node is null || ReferenceEquals(selectedWorkNode, node)) return;
        if (selectedWorkNode is not null) selectedWorkNode.IsSelected = false;
        selectedWorkNode = node; node.IsSelected = true;
    }

    private void RefreshWorkTree()
    {
        if (refreshingWorkTree || Tables.Count == 0) return;
        refreshingWorkTree = true;
        try
        {
            var used = new HashSet<object>();
            var represented = new HashSet<EmbeddedWallRow>();
            var tables = Tables.ToDictionary(t => t.Key);
            string principal = Data["case_id"]?.ToString() ?? "";
            string RowCase(EmbeddedWallRow row) => string.IsNullOrWhiteSpace(row["case"]) ? principal : row["case"];
            EmbeddedWallWorkNode Node(object key, string title, string detail = "")
            {
                used.Add(key);
                if (!workNodes.TryGetValue(key, out var node)) workNodes[key] = node = new();
                node.Title = title; node.Detail = detail;
                node.TableKey = null; node.Row = null; node.CaseId = null; node.Stage = null; node.Target = null;
                return node;
            }
            EmbeddedWallWorkNode Leaf(EmbeddedWallTable table, EmbeddedWallRow row, string? caseId = null, EmbeddedWallRow? stage = null)
            {
                represented.Add(row);
                var node = Node(row, WorkRowTitle(table.Key, row),
                    string.Join(Environment.NewLine, table.Columns.Where(f => !string.IsNullOrWhiteSpace(row[f.Key]))
                        .Select(f => f.Label + ": " + row[f.Key] + (string.IsNullOrEmpty(f.Unit) ? "" : " " + f.Unit))));
                node.TableKey = table.Key; node.Row = row; node.CaseId = caseId; node.Stage = stage;
                if (table.Key != "stages") SynchronizeWorkNodes(node.Children, []);
                return node;
            }
            EmbeddedWallWorkNode? TableGroup(string key, IEnumerable<EmbeddedWallRow>? rows = null, string scope = "", EmbeddedWallRow? stage = null)
            {
                var table = tables[key]; var selected = (rows ?? table.Rows).ToArray();
                if (selected.Length == 0) return null;
                var node = Node(new WorkGroup(key, scope, stage), $"{table.Title} ({selected.Length})");
                node.TableKey = key;
                node.CaseId = stage is null ? null : RowCase(stage); node.Stage = stage;
                SynchronizeWorkNodes(node.Children, selected.Select(row => Leaf(table, row, node.CaseId, stage)).ToArray());
                return node;
            }
            EmbeddedWallWorkNode Group(string key, string title, IEnumerable<EmbeddedWallWorkNode?> children)
            {
                var node = Node(new WorkGroup(key), title);
                SynchronizeWorkNodes(node.Children, children.OfType<EmbeddedWallWorkNode>().ToArray());
                return node;
            }
            var roots = new List<EmbeddedWallWorkNode>();
            var general = Node(new WorkGroup("general"), "Impostazioni", Data["name"]?.ToString() ?? "");
            general.Target = "general"; SynchronizeWorkNodes(general.Children, []); roots.Add(general);
            void AddGroup(string key, string title, params string[] keys)
            {
                var group = Group(key, title, keys.Select(k => TableGroup(k)));
                if (group.Children.Count > 0) roots.Add(group);
            }
            AddGroup("geometry", "Parete e sezioni", "segments", "rc_sections", "steel_tubes", "rc_piles");
            AddGroup("soil", "Terreni", "left_soils", "right_soils");
            AddGroup("support", "Sostegni e verifiche", "supports", "steel_members", "anchor_checks", "anchor_layers", "anchor_tests");
            var historyKeys = EmbeddedWallDocument.HistoryTables.Where(k => k != "stages").ToArray();
            var stageRows = tables["stages"].Rows;
            var caseIds = new[] { principal }.Concat(tables["cases"].Rows.Select(r => r["id"]))
                .Concat(EmbeddedWallDocument.HistoryTables.SelectMany(k => tables[k].Rows).Select(RowCase))
                .Distinct(StringComparer.Ordinal).ToArray();
            var cases = new List<EmbeddedWallWorkNode>();
            foreach (string caseId in caseIds)
            {
                var definitions = tables["cases"].Rows.Where(r => r["id"] == caseId).ToArray();
                bool defined = caseId == principal || definitions.Length > 0;
                string title = string.IsNullOrWhiteSpace(caseId) ? "Caso senza identificativo" : caseId;
                if (caseId == principal) title += " · principale";
                else if (!defined) title += " · non definito";
                var caseNode = Node(new WorkGroup("case", caseId), title);
                caseNode.CaseId = caseId;
                if (caseId == principal) caseNode.Target = "general";
                var caseChildren = definitions.Select(row => Leaf(tables["cases"], row, caseId)).ToList();
                foreach (var stage in stageRows.Where(r => RowCase(r) == caseId))
                {
                    var stageNode = Leaf(tables["stages"], stage, caseId, stage);
                    var stageChildren = new List<EmbeddedWallWorkNode>();
                    // Duplicate or missing identifiers never silently assign actions to an arbitrary phase.
                    if (!string.IsNullOrWhiteSpace(stage["id"]) && stageRows.Count(r => RowCase(r) == caseId && r["id"] == stage["id"]) == 1)
                        foreach (string key in historyKeys)
                        {
                            var child = TableGroup(key, tables[key].Rows.Where(r => RowCase(r) == caseId && r["stage"] == stage["id"]), caseId, stage);
                            if (child is not null) stageChildren.Add(child);
                        }
                    SynchronizeWorkNodes(stageNode.Children, stageChildren);
                    caseChildren.Add(stageNode);
                }
                var unresolved = historyKeys.Select(key =>
                    TableGroup(key, tables[key].Rows.Where(r => RowCase(r) == caseId && !represented.Contains(r)), "unresolved:" + caseId)).OfType<EmbeddedWallWorkNode>().ToArray();
                if (unresolved.Length > 0)
                {
                    var unassigned = Node(new WorkGroup("unresolved", caseId), "Riferimenti di fase da completare",
                        "Righe con fase mancante, inesistente o ambigua. Selezionarle per correggere il riferimento.");
                    SynchronizeWorkNodes(unassigned.Children, unresolved); caseChildren.Add(unassigned);
                }
                SynchronizeWorkNodes(caseNode.Children, caseChildren); cases.Add(caseNode);
            }
            roots.Add(Group("histories", "Casi e fasi", cases));
            var hydraulic = Group("hydraulics", "Falda e filtrazione",
                new[] { TableGroup("hydraulic_left"), TableGroup("hydraulic_right") });
            hydraulic.Target = "hydraulics"; roots.Add(hydraulic);
            // Retain every configured row even when a future schema introduces another table.
            var remaining = Tables.Select(t => TableGroup(t.Key, t.Rows.Where(r => !represented.Contains(r)), "other"))
                .OfType<EmbeddedWallWorkNode>().ToArray();
            if (remaining.Length > 0) roots.Add(Group("other", "Altri dati impostati", remaining));
            var embedmentNode = Node(new WorkGroup("embedment"), "Ricerca dell'infissione");
            embedmentNode.Target = "embedment"; SynchronizeWorkNodes(embedmentNode.Children, []); roots.Add(embedmentNode);
            SynchronizeWorkNodes(WorkTree, roots);
            foreach (object key in workNodes.Keys.Where(k => !used.Contains(k)).ToArray()) workNodes.Remove(key);
            if (selectedWorkNode is not null && !workNodes.Values.Contains(selectedWorkNode)) selectedWorkNode = null;
            OnPropertyChanged(nameof(WorkTreeSummary));
        }
        finally { refreshingWorkTree = false; }
    }

    private static string WorkRowTitle(string table, EmbeddedWallRow row) => table switch
    {
        "segments" => $"{row["section"]} · {row["top"]} → {row["bottom"]} m",
        "left_soils" or "right_soils" or "hydraulic_left" or "hydraulic_right" =>
            $"{row["name"]} · {row["top"]} → {row["bottom"]} m",
        "supports" => $"{row["id"]} · {(row["kind"] == "Anchor" ? "Tirante" : row["kind"] == "Strut" ? "Puntone" : row["kind"])} · z={row["elevation"]} m",
        "activations" => $"{row["support"]} · pretensione {row["prestress"]} kN",
        "loads" => $"z={row["elevation"]} m · H={row["horizontal"]} kN · M={row["moment"]} kNm",
        "distributed_loads" => $"{row["id"]} · {row["top"]} → {row["bottom"]} m · q={row["q_top"]} → {row["q_bottom"]} kN/m",
        _ => row.Label
    };

    private static void SynchronizeWorkNodes(ObservableCollection<EmbeddedWallWorkNode> target, IReadOnlyList<EmbeddedWallWorkNode> source)
    {
        for (int i = target.Count - 1; i >= 0; i--) if (!source.Contains(target[i])) target.RemoveAt(i);
        for (int i = 0; i < source.Count; i++)
        {
            int old = target.IndexOf(source[i]);
            if (old < 0) target.Insert(i, source[i]);
            else if (old != i) target.Move(old, i);
        }
    }
}
