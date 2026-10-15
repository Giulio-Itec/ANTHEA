// Spostati da X.Desktop/Wpf (ConcreteDomains.cs, ConcreteStress.cs, ConcreteExchange.cs, ConcreteWorkspace.cs; commit 4144fe9e) con la
// decisione D3 del 15/10/2026: membri usati solo dalle prove WPF, compilati solo nella configurazione UiTests, fuori dall'installer.
using Anthea.Application.Concrete;
using System.Globalization;
using System.Text.Json.Nodes;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace
{
    // Da ConcreteDomains.cs: calcolo di un solo dominio, chiamato dalle prove (il prodotto calcola tutto con CalculateAllAsync).
    private async Task CalculateDomain(DomainPanel panel, CancellationToken token, string? requestedKey = null)
    {
        string key = requestedKey ?? panel.Key;
        status.Text = $"GPC Engine · {ActionSetLabel(key)} · dominio {(panel.ThreeD ? "3D" : "2D")}…";
        var input = (JsonObject)Input.DeepClone(); var workspace = (JsonObject)settings.DeepClone(); var options = (JsonObject)panel.Options.DeepClone();
        var snapshots = actions[key].Select(row => (JsonObject)row.Values.DeepClone()).ToArray();
        var calculated = await Task.Run(() => analysisSession.Domain(input, workspace, options, key, panel.ThreeD, snapshots, token), token);
        token.ThrowIfCancellationRequested();
        PublishDomain(panel, key, calculated);
    }

    // Da ConcreteStress.cs: analisi tensionale di una sola famiglia SLE, chiamata dalle prove.
    private async Task CalculateStress(string key, CancellationToken token)
    {
        status.Text = "Analisi tensionale · " + ConcreteEditor.Label(key) + "…"; var settingsSle = settings["sle"]![key]!;
        var input = (JsonObject)Input.DeepClone(); var workspace = (JsonObject)settings.DeepClone(); var options = (JsonObject)settingsSle.DeepClone();
        var requests = actions[key].Select(row => (JsonObject)row.Values.DeepClone()).ToArray();
        var outcomes = await Task.Run(() => analysisSession.Stress(input, workspace, options, key, requests, token), token);
        token.ThrowIfCancellationRequested(); PublishStress(key, outcomes);
    }

    // Da ConcreteExchange.cs: righe e importazione Excel lette e scritte dalle prove (il prodotto passa da SectionActionsViewModel).
    private IEnumerable<SectionActionsExcel.Row> ExportActionRows()
    {
        foreach (var (key, rows) in actions) foreach (var row in rows) { var p = ReadAction(row); yield return new(key, row.Values.S("nome"), p.N, p.Mx, p.My, null, null); }
        if (shearGrid is not null) foreach (var row in shearGrid.Rows)
            yield return new("Taglio", row.Values.S("nome"), ConcreteEditor.Number(row.Values.S("N"), "N taglio"), ConcreteEditor.Number(row.Values.S("Mx", "0"), "Mx"), ConcreteEditor.Number(row.Values.S("My", "0"), "My"), ConcreteEditor.Number(row.Values.S("Vx"), "Vx"), ConcreteEditor.Number(row.Values.S("Vy"), "Vy"),ConcreteEditor.Number(row.Values.S("T","0"),"T"));
    }
    private void ApplyImport(SectionActionsExcel.Import import, bool replace)
    {
        Commit(); synchronizing = true;
        var selections = grids.ToDictionary(grid => grid, grid => grid.SelectedItem as JsonRow);
        static string Number(double? v) => v?.ToString("G17", CultureInfo.InvariantCulture) ?? "0";
        try
        {
            foreach (var group in import.Rows.GroupBy(r => r.Family))
            {
                var collection = group.Key == "Taglio" ? shearGrid!.Rows : actions[group.Key];
                using var refresh = collection.DeferRefresh();
                if (replace) collection.Clear();
                foreach (var row in group)
                    collection.Add(group.Key == "Taglio" ? ShearRow(J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", row.Name), ("N", Number(row.N)), ("Mx", Number(row.Mx)), ("My", Number(row.My)), ("Vx", Number(row.Vx)), ("Vy", Number(row.Vy)),("T",Number(row.T)))) : CreateAction(group.Key, row.Name, Number(row.N), Number(row.Mx), Number(row.My)));
            }
            foreach (var (grid, selected) in selections)
                grid.SelectedItem = selected is not null && grid.Items.Contains(selected) ? selected : grid.Items.OfType<JsonRow>().FirstOrDefault();
        }
        finally { synchronizing = false; }
        Commit(); foreach (string family in import.Rows.Select(r => r.Family).Distinct()) InvalidateActions(family);
    }

    // Da ConcreteWorkspace.cs: scorciatoia delle prove per cambiare il diametro.
    internal void SetGeometry(string value) => geometry.Set("diameter_mm", value);

    // Da ConcreteWorkspace.cs: lettura storica degli appunti, codice morto nel prodotto (l'incolla usa PasteCells di
    // ConcreteExchange.cs). Resta per la prova che accetta 3 e 4 colonne nello stesso incolla, caso che PasteCells rifiuta.
    internal static List<string[]> ParsePaste(string text)
    {
        var result = new List<string[]>();
        foreach (string line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var cells = line.Split('\t');
            if (cells.Length == 4 && cells[1].Trim().StartsWith("N", StringComparison.OrdinalIgnoreCase)) continue;
            if (cells.Length == 3) cells = new[] { "", cells[0], cells[1], cells[2] };
            if (cells.Length != 4) throw new ArgumentException("Usare 3 colonne N–Mx–My oppure 4 colonne Nome–N–Mx–My, separate da tabulazioni.");
            for (int i = 1; i < 4; i++) ConcreteEditor.Number(cells[i], "Riga " + (result.Count + 1));
            result.Add(cells);
        }
        if (result.Count == 0) throw new ArgumentException("Nessuna combinazione da incollare.");
        return result;
    }
}
