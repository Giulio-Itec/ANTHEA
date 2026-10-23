using Anthea.Application.Concrete;
using Anthea.Presentation.Modules.ConcreteSection;
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
                var selected = grid.SelectedItems.OfType<JsonRow>().ToHashSet();
                var rows = grid.Items.OfType<JsonRow>().Where(selected.Contains).ToArray();
                if (rows.Length == 0) return;
                Clipboard.SetText(ConcreteActionPresentation.CopyText(rows.Select(row => row.Values), family()));
                status.Text = ConcreteActionPresentation.CopyStatus(rows.Length);
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
        // The whole paste is read and validated before a single existing row changes.
        var plan = ConcreteActionPresentation.Plan(family, text, grid.CurrentCell.Column?.SortMemberPath ?? "");
        var (keys, startColumn, width, matrix) = (plan.Keys, plan.StartColumn, plan.Width, plan.Rows);
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
                    string name = ConcreteActionPresentation.PastedName(collection.Count);
                    target = family == "Taglio" ? ShearRow(ConcreteActionPresentation.NewShearRow(name)) : CreateAction(family, name, "0", "0", "0");
                    collection.Add(target);
                }
                using var notifications = JsonRow.DeferNotifications([target]);
                for (int c = 0; c < width; c++) target[keys[startColumn + c]] = matrix[r][c].Trim();
            }
        }
        finally { synchronizing = false; }
        Commit(); InvalidateActions(family);
        status.Text = ConcreteActionPresentation.PasteStatus(matrix.Count);
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
