using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal sealed partial class ConcreteWorkspace
{
    private void AttachClipboard(JsonGrid grid, Func<string> family, WrapPanel buttons)
    {
        grid.SelectionMode = DataGridSelectionMode.Extended;
        void Copy()
        {
            try
            {
                string[] keys = family() == "Taglio" ? ["nome", "N", "Vx", "Vy"] : ["nome", "N", "Mx", "My"];
                var selected = grid.SelectedItems.OfType<JsonRow>().ToHashSet();
                var rows = grid.Items.OfType<JsonRow>().Where(selected.Contains).ToArray();
                if (rows.Length == 0) return;
                Clipboard.SetText(string.Join(Environment.NewLine, rows.Select(row => string.Join('\t', keys.Select(k => row.Values.S(k))))));
                status.Text = $"Copiate {rows.Length} righe: nome e sollecitazioni. Ctrl+clic / Maiusc+clic per selezionare più righe.";
            }
            catch (Exception ex) { status.Text = "Copia: " + ex.Message; }
        }
        void Paste(bool append)
        {
            try { PasteCells(grid, family(), Clipboard.GetText(), append); }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message + "\nNessun dato incollato.", "Incolla sollecitazioni", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        buttons.Children.Add(Ui.Button("Copia", Copy));
        var paste = Ui.Button("Incolla", () => Paste(false)); paste.ToolTip = "Ctrl+V: dalla cella corrente. 4 colonne: Nome e azioni; 3: azioni. Le righe eccedenti vengono aggiunte."; buttons.Children.Add(paste);
        buttons.Children.Add(Ui.Button("Template Excel", SaveActionTemplate));
        buttons.Children.Add(Ui.Button("Importa Excel", ImportActionWorkbook));
        grid.PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            if (e.Key == Key.C && e.OriginalSource is not TextBox) { Copy(); e.Handled = true; }
            if (e.Key == Key.V)
            {
                if (e.OriginalSource is TextBox && !Clipboard.GetText().Contains('\t') && !Clipboard.GetText().Contains('\n')) return;
                Paste(false); e.Handled = true;
            }
        };
        var menu = new ContextMenu();
        foreach (var (label, action) in new (string, Action)[] { ("Copia righe · Ctrl+C", Copy), ("Incolla dalla cella · Ctrl+V", () => Paste(false)), ("Aggiungi dagli appunti", () => Paste(true)) })
        { var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        grid.ContextMenu = menu;
    }
    private void PasteCells(JsonGrid grid, string family, string text, bool append = false)
    {
        string[] keys = family == "Taglio" ? ["nome", "N", "Vx", "Vy"] : ["nome", "N", "Mx", "My"];
        var matrix = text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Split('\t')).ToList();
        if (matrix.Count == 0) throw new ArgumentException("Gli appunti non contengono righe.");
        if (matrix.Count > 10000) throw new ArgumentException("Massimo 10.000 righe per incolla.");
        int width = matrix[0].Length;
        if (width is < 1 or > 4 || matrix.Any(row => row.Length != width)) throw new ArgumentException("Usare una tabella rettangolare con 1–4 colonne, separate da tabulazioni.");
        string current = grid.CurrentCell.Column?.SortMemberPath ?? "";
        if (width < 3 && !keys.Contains(current)) throw new ArgumentException("Selezionare prima una cella di input (nome o sollecitazione). Per aggiungere righe complete usare 3 o 4 colonne.");
        int startColumn = width == 4 ? 0 : width == 3 ? 1 : Math.Max(0, Array.IndexOf(keys, current));
        if (startColumn + width > 4) throw new ArgumentException("La selezione supera le colonne di input. Selezionare la cella iniziale corretta.");
        bool Header(string cell, int i) => cell.Trim().Equals(keys[i], StringComparison.OrdinalIgnoreCase) || cell.Trim().StartsWith(keys[i] + " [", StringComparison.OrdinalIgnoreCase) || i == 0 && cell.Trim() == "Combinazione";
        if (matrix[0].Select((cell, i) => Header(cell, startColumn + i)).All(v => v)) matrix.RemoveAt(0);
        if (matrix.Count == 0) throw new ArgumentException("Sono presenti solo le intestazioni.");
        // Validate the entire paste before mutating a single existing row.
        for (int r = 0; r < matrix.Count; r++)
            for (int c = 0; c < width; c++)
            {
                if (startColumn + c == 0 && string.IsNullOrWhiteSpace(matrix[r][c])) throw new ArgumentException($"Riga {r + 1}: nome mancante.");
                if (startColumn + c > 0) SectionWorkspace.Number(matrix[r][c], $"Riga {r + 1}, {keys[startColumn + c]}");
            }
        grid.Commit();
        var collection = family == "Taglio" ? shearGrid!.Rows : actions[family];
        var visible = grid.Items.OfType<JsonRow>().ToList();
        int start = append ? visible.Count : Math.Max(0, visible.IndexOf((grid.SelectedItem as JsonRow ?? grid.CurrentItem as JsonRow)!));
        synchronizing = true;
        try
        {
            for (int r = 0; r < matrix.Count; r++)
            {
                JsonRow target;
                if (start + r < visible.Count) target = visible[start + r];
                else
                {
                    string name = "Combo " + (collection.Count + 1);
                    target = family == "Taglio" ? ShearRow(J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", name), ("N", "0"), ("Vx", "0"), ("Vy", "0"))) : CreateAction(family, name, "0", "0", "0");
                    collection.Add(target);
                }
                for (int c = 0; c < width; c++) target[keys[startColumn + c]] = matrix[r][c].Trim();
            }
        }
        finally { synchronizing = false; }
        Commit(); Invalidate(false);
        status.Text = $"{matrix.Count} righe incollate · aggiornamento automatico in attesa…";
    }
    private void SaveActionTemplate()
    {
        var dialog = new SaveFileDialog { Filter = "Cartella Excel|*.xlsx", FileName = "ANTHEA_Sollecitazioni.xlsx" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try { Archivio.ScriviAtomico(dialog.FileName, SectionActionsExcel.Template()); status.Text = "Template salvato: compilare il foglio Azioni e usare Importa Excel."; }
        catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "Template Excel"); }
    }
    private void ImportActionWorkbook()
    {
        var dialog = new OpenFileDialog { Filter = "Cartella Excel|*.xlsx" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 20_000_000) throw new ArgumentException("File troppo grande: massimo 20 MB.");
            var import = SectionActionsExcel.Read(File.ReadAllBytes(dialog.FileName));
            if (import.Rows.Count == 0) { status.Text = "Il foglio Azioni è vuoto: nessuna modifica."; return; }
            string summary = string.Join("\n", import.Rows.GroupBy(r => r.Family).Select(g => (g.Key == "Taglio" ? g.Key : SectionWorkspace.Label(g.Key)) + ": " + g.Count() + " combinazioni"));
            string formulas = import.FormulaCells > 0 ? $"\n\n{import.FormulaCells} formule: si importano i valori memorizzati nel file. Confermare solo se il file è stato ricalcolato e salvato in Excel." : "";
            var choice = MessageBox.Show(Window.GetWindow(this), summary + formulas + "\n\nSì = sostituisci le combinazioni delle sole famiglie elencate.\nNo = aggiungi alle combinazioni presenti.\nAnnulla = nessuna modifica.\n\nN negativo a compressione; kN e kNm. Nessun cambio automatico di segno o unità.", "Importazione sollecitazioni", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            if (choice == MessageBoxResult.Cancel) return;
            ApplyImport(import, choice == MessageBoxResult.Yes);
            status.Text = $"Importate {import.Rows.Count} combinazioni · aggiornamento automatico in attesa…";
        }
        catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message + "\nImportazione non eseguita.", "Importazione Excel", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void ApplyImport(SectionActionsExcel.Import import, bool replace)
    {
        Commit(); synchronizing = true;
        static string Number(double? v) => v?.ToString("G17", CultureInfo.InvariantCulture) ?? "0";
        try
        {
            foreach (var group in import.Rows.GroupBy(r => r.Family))
            {
                var collection = group.Key == "Taglio" ? shearGrid!.Rows : actions[group.Key];
                if (replace) collection.Clear();
                foreach (var row in group)
                    collection.Add(group.Key == "Taglio" ? ShearRow(J.Obj(("id", Guid.NewGuid().ToString("N")), ("nome", row.Name), ("N", Number(row.N)), ("Vx", Number(row.Vx)), ("Vy", Number(row.Vy)))) : CreateAction(group.Key, row.Name, Number(row.N), Number(row.Mx), Number(row.My)));
            }
        }
        finally { synchronizing = false; }
        Commit(); Invalidate(false);
    }
}
