using System.Collections.ObjectModel;
using Anthea.Application.EmbeddedWalls;
using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

/// <summary>
/// The tree of the model in the left panel (organizzazione del modulo paratie, progetto del 16/11, step 4). It shows the case chosen in the
/// box above it (D3: the case takes the place of the design assumption): Impostazioni, Fasi, Parete, Elementi strutturali (each support with
/// its presence in the phases and its check data), Terreni, Carichi, Falda e filtrazione, Verifiche, Casi, Ricerca dell'infissione. The rows of
/// the other defined cases appear when their case is chosen; those of a case that is not defined stay under «Casi», and references to
/// complete in their own group, so that every row of the case on screen and every row without a case appears exactly once.
/// </summary>
public sealed partial class EmbeddedWallSheetViewModel
{
    private sealed record WorkGroup(string Area, string Case = "", EmbeddedWallRow? Stage = null);
    private readonly Dictionary<object, EmbeddedWallWorkNode> workNodes = [];
    private bool workTreeCollapsed, refreshingWorkTree, selectingWorkNode;
    private EmbeddedWallWorkNode? selectedWorkNode;
    private string? workTreeCase;
    public ObservableCollection<EmbeddedWallWorkNode> WorkTree { get; } = [];
    public bool IsWorkTreeCollapsed { get => workTreeCollapsed; set => SetProperty(ref workTreeCollapsed, value); }
    public IRelayCommand ToggleWorkTreeCommand { get; private set; } = null!;
    public IRelayCommand<EmbeddedWallWorkNode> SelectWorkNodeCommand { get; private set; } = null!;
    public string WorkTreeSummary => $"{Tables.Sum(t => t.Rows.Count)} righe di dati · {InputStages.Count} fasi nel caso «{InputCase}»";

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
            if (node.Target == "hydraulics") { Documents.Show(EmbeddedWallDocuments.Hydraulics); return; }
            if (node.Target == "embedment") { Documents.Show(EmbeddedWallDocuments.Embedment); return; }
            if (node.Target == "general") { EditModelCommand.Execute("general"); return; }
            if (node.TableKey is null) return;
            EditModelCommand.Execute(node.TableKey);
            if (node.Row is not null && InspectorTable!.Rows.Contains(node.Row)) InspectorTable.Selected = node.Row;
        }
        finally { selectingWorkNode = false; }
    }

    /// <summary>The phase on screen in bold among the phases of the tree, without rebuilding it.</summary>
    private void MarkCurrentStage()
    {
        foreach (var row in InputStages) if (workNodes.TryGetValue(row, out var node)) node.IsCurrent = ReferenceEquals(row, inputStage);
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
            string caseId = InputCases.Contains(InputCase) ? InputCase : principal;
            string RowCase(EmbeddedWallRow row) => string.IsNullOrWhiteSpace(row["case"]) ? principal : row["case"];
            var defined = new[] { principal }.Concat(tables["cases"].Rows.Select(r => r["id"])).ToHashSet(StringComparer.Ordinal);
            var stageRows = tables["stages"].Rows.Where(r => RowCase(r) == caseId).ToArray();
            EmbeddedWallRow? StageOf(EmbeddedWallRow row)
            {
                // Duplicate or missing identifiers never silently assign actions to an arbitrary phase.
                var matches = stageRows.Where(s => !string.IsNullOrWhiteSpace(s["id"]) && s["id"] == row["stage"]).Take(2).ToArray();
                return matches.Length == 1 ? matches[0] : null;
            }
            bool Current(EmbeddedWallRow row) => RowCase(row) == caseId;
            EmbeddedWallWorkNode Node(object key, string title, string detail = "")
            {
                used.Add(key);
                if (!workNodes.TryGetValue(key, out var node)) workNodes[key] = node = new();
                node.Title = title; node.Detail = detail;
                node.TableKey = null; node.Row = null; node.CaseId = null; node.Stage = null; node.Target = null; node.IsCurrent = false;
                return node;
            }
            EmbeddedWallWorkNode Leaf(EmbeddedWallTable table, EmbeddedWallRow row, string title, IReadOnlyList<EmbeddedWallWorkNode>? children = null)
            {
                represented.Add(row);
                var node = Node(row, title,
                    string.Join(Environment.NewLine, table.Columns.Where(f => !string.IsNullOrWhiteSpace(row[f.Key]))
                        .Select(f => f.Label + ": " + row[f.Key] + (string.IsNullOrEmpty(f.Unit) ? "" : " " + f.Unit))));
                node.TableKey = table.Key; node.Row = row;
                if (EmbeddedWallDocument.HistoryTables.Contains(table.Key))
                {
                    node.CaseId = RowCase(row);
                    node.Stage = table.Key == "stages" ? row : Current(row) ? StageOf(row) : null;
                }
                SynchronizeWorkNodes(node.Children, children ?? []);
                return node;
            }
            string HistoryTitle(string key, EmbeddedWallRow row) => key == "stages" || !EmbeddedWallDocument.HistoryTables.Contains(key) ? WorkRowTitle(key, row)
                : (string.IsNullOrWhiteSpace(row["stage"]) ? "fase ?" : row["stage"]) + " · " + WorkRowTitle(key, row);
            EmbeddedWallWorkNode? TableGroup(string key, IEnumerable<EmbeddedWallRow>? rows = null, string scope = "", string? title = null)
            {
                var table = tables[key]; var selected = (rows ?? table.Rows).ToArray();
                if (selected.Length == 0) return null;
                var node = Node(new WorkGroup(key, scope), $"{title ?? table.Title} ({selected.Length})");
                node.TableKey = key;
                SynchronizeWorkNodes(node.Children, selected.Select(row => Leaf(table, row, HistoryTitle(key, row))).ToArray());
                return node;
            }
            EmbeddedWallWorkNode Group(string key, string title, IEnumerable<EmbeddedWallWorkNode?> children, string detail = "")
            {
                var node = Node(new WorkGroup(key), title, detail);
                SynchronizeWorkNodes(node.Children, children.OfType<EmbeddedWallWorkNode>().ToArray());
                return node;
            }
            // History rows of the case on screen whose phase resolves; the others go to «Riferimenti di fase da completare».
            IEnumerable<EmbeddedWallRow> Resolved(string key) => tables[key].Rows.Where(r => Current(r) && StageOf(r) is not null);
            var roots = new List<EmbeddedWallWorkNode>();
            void Add(EmbeddedWallWorkNode? node) { if (node is not null && (node.Children.Count > 0 || node.Target is not null || node.TableKey is not null)) roots.Add(node); }

            var general = Node(new WorkGroup("general"), "Impostazioni", Data["name"]?.ToString() ?? "");
            general.Target = "general"; SynchronizeWorkNodes(general.Children, []); roots.Add(general);

            var phases = Node(new WorkGroup("phases", caseId), $"Fasi ({stageRows.Length})", "Fasi del caso «" + caseId + "», nell'ordine della storia.");
            phases.TableKey = "stages"; phases.CaseId = caseId;
            SynchronizeWorkNodes(phases.Children, stageRows.Select(stage =>
            {
                var leaf = Leaf(tables["stages"], stage, WorkRowTitle("stages", stage));
                leaf.IsCurrent = ReferenceEquals(stage, InputStage);
                return leaf;
            }).ToArray());
            roots.Add(phases);

            Add(Group("group:geometry", "Parete", [TableGroup("segments"), TableGroup("rc_sections"), TableGroup("steel_tubes"), TableGroup("rc_piles")]));

            // Every support with its presence in the phases of the case and its check data; rows citing a missing or repeated support apart.
            var supports = tables["supports"].Rows;
            string[] supportData = ["steel_members", "anchor_checks", "anchor_layers", "anchor_tests"];
            bool UniqueSupport(string id) => !string.IsNullOrWhiteSpace(id) && supports.Count(s => s["id"] == id) == 1;
            EmbeddedWallWorkNode SupportLeaf(EmbeddedWallRow support)
            {
                var children = new List<EmbeddedWallWorkNode>();
                if (UniqueSupport(support["id"]))
                {
                    children.AddRange(Resolved("activations").Where(a => a["support"] == support["id"]).Select(a =>
                        Leaf(tables["activations"], a, HistoryTitle("activations", a))));
                    foreach (string key in supportData)
                        children.AddRange(tables[key].Rows.Where(r => r["support"] == support["id"]).Select(r => Leaf(tables[key], r, EmbeddedWallFields.Title(key) + " · " + WorkRowTitle(key, r))));
                }
                return Leaf(tables["supports"], support, WorkRowTitle("supports", support), children);
            }
            EmbeddedWallWorkNode? Kind(string key, string title, Func<EmbeddedWallRow, bool> member)
            {
                var rows = supports.Where(member).ToArray();
                if (rows.Length == 0) return null;
                var node = Node(new WorkGroup("support:" + key), $"{title} ({rows.Length})");
                node.TableKey = "supports";
                SynchronizeWorkNodes(node.Children, rows.Select(SupportLeaf).ToArray());
                return node;
            }
            var orphanSupportRows = Resolved("activations").Where(a => !UniqueSupport(a["support"])).Select(a => (Key: "activations", Row: a))
                .Concat(supportData.SelectMany(key => tables[key].Rows.Where(r => !UniqueSupport(r["support"])).Select(r => (Key: key, Row: r)))).ToArray();
            var supportOrphans = orphanSupportRows.Length == 0 ? null : Group("group:support-orphans", "Riferimenti ai sostegni da completare",
                orphanSupportRows.GroupBy(r => r.Key).Select(g => TableGroup(g.Key, g.Select(r => r.Row), "support-orphans")),
                "Righe che citano un sostegno assente o con identificativo ripetuto. Selezionarle per correggere il riferimento.");
            Add(Group("group:support", "Elementi strutturali", [Kind("anchor", "Tiranti", s => s["kind"] == "Anchor"), Kind("strut", "Puntoni", s => s["kind"] == "Strut"),
                Kind("bilateral", "Vincoli bilaterali", s => s["kind"] == "Bilateral"),
                Kind("other", "Sostegni di tipo da completare", s => s["kind"] is not ("Anchor" or "Strut" or "Bilateral")), supportOrphans]));

            Add(Group("group:soil", "Terreni", [TableGroup("left_soils"), TableGroup("right_soils")]));
            Add(Group("group:loads", "Carichi", [TableGroup("loads", Resolved("loads"), caseId), TableGroup("distributed_loads", Resolved("distributed_loads"), caseId)]));
            var hydraulic = Group("group:hydraulics", "Falda e filtrazione", [TableGroup("hydraulic_left"), TableGroup("hydraulic_right"),
                TableGroup("hydraulic_upl", Resolved("hydraulic_upl"), caseId), TableGroup("hydraulic_actions", Resolved("hydraulic_actions"), caseId),
                TableGroup("hydraulic_hyd", Resolved("hydraulic_hyd"), caseId)]);
            hydraulic.Target = "hydraulics"; roots.Add(hydraulic);
            Add(Group("group:checks", "Verifiche", [TableGroup("global_checks", Resolved("global_checks"), caseId), TableGroup("global_actions", Resolved("global_actions"), caseId)]));

            var historyKeys = EmbeddedWallDocument.HistoryTables.Where(k => k != "stages").ToArray();
            var unresolved = historyKeys.Select(key => TableGroup(key, tables[key].Rows.Where(r => Current(r) && !represented.Contains(r) && StageOf(r) is null),
                "unresolved:" + caseId)).OfType<EmbeddedWallWorkNode>().ToArray();
            if (unresolved.Length > 0)
                roots.Insert(2, Group("group:unresolved", "Riferimenti di fase da completare", unresolved,
                    "Righe con fase mancante, inesistente o ambigua. Selezionarle per correggere il riferimento."));

            // «Casi»: the main case (its settings), the other defined cases and the cases cited without a definition, with all their rows.
            var cases = new List<EmbeddedWallWorkNode>();
            var principalNode = Node(new WorkGroup("case", principal), (string.IsNullOrWhiteSpace(principal) ? "Caso senza identificativo" : principal) + " · principale");
            principalNode.CaseId = principal; principalNode.Target = "general"; principalNode.IsCurrent = caseId == principal;
            SynchronizeWorkNodes(principalNode.Children, []); cases.Add(principalNode);
            foreach (var definition in tables["cases"].Rows)
            {
                var leaf = Leaf(tables["cases"], definition, string.IsNullOrWhiteSpace(definition["id"]) ? "Caso senza identificativo" : definition["id"]);
                leaf.CaseId = definition["id"]; leaf.IsCurrent = definition["id"] == caseId;
                cases.Add(leaf);
            }
            var undefinedCases = EmbeddedWallDocument.HistoryTables.SelectMany(k => tables[k].Rows).Select(RowCase)
                .Where(c => !defined.Contains(c)).Distinct(StringComparer.Ordinal).ToArray();
            foreach (string missing in undefinedCases)
            {
                var node = Group("undefined:" + missing, (string.IsNullOrWhiteSpace(missing) ? "Caso senza identificativo" : missing) + " · non definito",
                    EmbeddedWallDocument.HistoryTables.Select(key => TableGroup(key, tables[key].Rows.Where(r => RowCase(r) == missing), "undefined:" + missing)),
                    "Righe assegnate a un caso che non è definito in «Altri casi / combinazioni». Selezionarle per correggere il caso.");
                cases.Add(node);
            }
            roots.Add(Group("group:histories", "Casi", cases));

            // Retain every configured row of the case on screen even when a future schema introduces another table.
            var remaining = Tables.Select(t => TableGroup(t.Key, t.Rows.Where(r => !represented.Contains(r)
                && (!EmbeddedWallDocument.HistoryTables.Contains(t.Key) || Current(r))), "other")).OfType<EmbeddedWallWorkNode>().ToArray();
            if (remaining.Length > 0) roots.Add(Group("group:other", "Altri dati impostati", remaining));
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
        "supports" => $"{row["id"]} · {(row["kind"] == "Anchor" ? "Tirante" : row["kind"] == "Strut" ? "Puntone" : row["kind"] == "Bilateral" ? "Vincolo bilaterale" : row["kind"])} · z={row["elevation"]} m",
        "activations" => string.IsNullOrWhiteSpace(row["prestress"]) ? $"{row["support"]} · pretensione mantenuta" : $"{row["support"]} · pretensione {row["prestress"]} kN",
        "loads" => $"z={row["elevation"]} m · H={row["horizontal"]} kN · M={row["moment"]} kNm",
        "distributed_loads" => $"{row["id"]} · {row["top"]} → {row["bottom"]} m · q={row["q_top"]} → {row["q_bottom"]} kN/m",
        "anchor_layers" => $"{row["profile"]} · {row["axis_start"]} → {row["axis_end"]} m",
        "anchor_tests" => row["test_id"],
        "steel_members" => row["family"],
        "anchor_checks" => row["injection"],
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
