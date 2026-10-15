using Anthea.Application.Concrete;
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
    private readonly List<TextBox> excelPaths = [];
    private void RememberExcel(string path)
    {
        settings["file_sollecitazioni"] = path;
        foreach (var text in excelPaths) { text.Text = path; text.ToolTip = path; }
        Modified?.Invoke();
    }
    private string? ChooseExcel()
    {
        var dialog = new OpenFileDialog { Filter = "Cartella Excel|*.xlsx", FileName = settings.S("file_sollecitazioni") };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return null;
        RememberExcel(dialog.FileName); return dialog.FileName;
    }
    private void AttachClipboard(JsonGrid grid, Func<string> family, WrapPanel buttons)
    {
        grid.SelectionMode = DataGridSelectionMode.Extended;
        void Copy()
        {
            try
            {
                string[] keys = family() == "Taglio" ? ["nome", "N", "Mx", "My", "Vx", "Vy", "T"] : ["nome", "N", "Mx", "My"];
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
        buttons.Children.Add(Ui.Button("Copia", Copy, inspection: true));
        var paste = Ui.Button("Incolla", () => Paste(false)); paste.ToolTip = "Ctrl+V: 4 colonne per N–M (nome,N,Mx,My); 7 per taglio (nome,N,Mx,My,Vx,Vy,T), 6 senza nome. Taglio storico: 4/5 colonne nome,N,Vx,Vy[,T]; 3 colonne N,Vx,Vy. Le righe eccedenti vengono aggiunte."; buttons.Children.Add(paste);
        buttons.Children.Add(Ui.Button("Template Excel", SaveActionTemplate, inspection: true));
        buttons.Children.Add(Ui.Button("Importa Excel", ImportActionWorkbook));
        buttons.Children.Add(Ui.Button("Esporta Excel", ExportActionWorkbook, inspection: true));
        var path = new TextBox { Text = settings.S("file_sollecitazioni"), Width = 210, IsReadOnly = true, ToolTip = "Percorso proposto nella finestra Sollecitazioni quando si importa Excel. Sfoglia permette di scegliere un altro file." };
        excelPaths.Add(path); buttons.Children.Add(path); buttons.Children.Add(Ui.Button("Sfoglia…", () => ChooseExcel()));
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
        string[] keys = family == "Taglio" ? ["nome", "N", "Mx", "My", "Vx", "Vy", "T"] : ["nome", "N", "Mx", "My"];
        var matrix = text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Split('\t')).ToList();
        if (matrix.Count == 0) throw new ArgumentException("Gli appunti non contengono righe.");
        if (matrix.Count > 10000) throw new ArgumentException("Massimo 10.000 righe per incolla.");
        int width = matrix[0].Length;
        // Preserve the existing clipboard format; moments are included only in the new full-width format.
        if (family == "Taglio" && width is >= 3 and <= 5) keys = ["nome", "N", "Vx", "Vy", "T"];
        if (width < 1 || width > keys.Length || matrix.Any(row => row.Length != width)) throw new ArgumentException("Numero di colonne non valido per la famiglia selezionata.");
        string current = grid.CurrentCell.Column?.SortMemberPath ?? "";
        if (width < 3 && !keys.Contains(current)) throw new ArgumentException("Selezionare prima una cella di input (nome o sollecitazione). Per aggiungere righe complete usare 3 o 4 colonne.");
        int startColumn = width == 6 && family == "Taglio" ? 1 : width >= 4 ? 0 : width == 3 ? 1 : Math.Max(0, Array.IndexOf(keys, current));
        if (startColumn + width > keys.Length) throw new ArgumentException("La selezione supera le colonne di input. Selezionare la cella iniziale corretta.");
        bool Header(string cell, int i) => cell.Trim().Equals(keys[i], StringComparison.OrdinalIgnoreCase) || cell.Trim().StartsWith(keys[i] + " [", StringComparison.OrdinalIgnoreCase) || i == 0 && cell.Trim() == "Combinazione";
        if (matrix[0].Select((cell, i) => Header(cell, startColumn + i)).All(v => v)) matrix.RemoveAt(0);
        if (matrix.Count == 0) throw new ArgumentException("Sono presenti solo le intestazioni.");
        // Validate the entire paste before mutating a single existing row.
        for (int r = 0; r < matrix.Count; r++)
            for (int c = 0; c < width; c++)
            {
                if (startColumn + c == 0 && string.IsNullOrWhiteSpace(matrix[r][c])) throw new ArgumentException($"Riga {r + 1}: nome mancante.");
                if (startColumn + c > 0) ConcreteEditor.Number(matrix[r][c], $"Riga {r + 1}, {keys[startColumn + c]}");
            }
        grid.Commit();
        var collection = family == "Taglio" ? shearGrid!.Rows : actions[family];
        var visible = grid.Items.OfType<JsonRow>().ToList();
        int start = append ? visible.Count : Math.Max(0, visible.IndexOf((grid.SelectedItem as JsonRow ?? grid.CurrentItem as JsonRow)!));
        synchronizing = true;
        try
        {
            using var refresh = collection.DeferRefresh();
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
                using var notifications = JsonRow.DeferNotifications([target]);
                for (int c = 0; c < width; c++) target[keys[startColumn + c]] = matrix[r][c].Trim();
            }
        }
        finally { synchronizing = false; }
        Commit(); InvalidateActions(family);
        status.Text = $"{matrix.Count} righe incollate · aggiornamento automatico in attesa…";
    }
    private void SaveActionTemplate()
    {
        var dialog = new SaveFileDialog { Filter = "Cartella Excel|*.xlsx", FileName = "ANTHEA_Sollecitazioni.xlsx" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        try { Archivio.ScriviAtomico(dialog.FileName, SectionActionsExcel.Template()); RememberExcel(dialog.FileName); status.Text = "Template salvato. Importa Excel apre la finestra Sollecitazioni: scegliere Importa e sostituisci oppure Importa e aggiungi."; }
        catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "Template Excel"); }
    }
    private void ImportActionWorkbook()
    {
        OpenActionEditor();
    }
    private void OpenActionEditor()
    {
        Commit();
        var vm = new SectionActionsViewModel(Data);
        var dialog = SectionActionsView.Create(this, vm, settings.S("file_sollecitazioni"));
        if (dialog.ShowDialog() != true || vm.Result is null) return;
        revision++; analysisScheduler.Invalidate(); Result = null;
        Data["combinazioni"] = vm.Result["combinazioni"]!.DeepClone();
        Data["workspace_ca"] = vm.Result["workspace_ca"]!.DeepClone();
        Modified?.Invoke(); ReloadRequested?.Invoke();
    }
    private void ExportActionWorkbook()
    {
        try
        {
            if (!UnifiedActions) { OpenActionEditor(); return; }
            Commit(); var bytes = new SectionActionsViewModel(Data).Export();
            var dialog = new SaveFileDialog { Filter = "Cartella Excel|*.xlsx", FileName = "ANTHEA_Sollecitazioni_compilate.xlsx" };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            Archivio.ScriviAtomico(dialog.FileName, bytes); RememberExcel(dialog.FileName); status.Text = "Esportate tutte le combinazioni, anche quelle nascoste dai filtri. Per rileggerle aprire Sollecitazioni e scegliere il comando di importazione.";
        }
        catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "Esportazione Excel"); }
    }
}
