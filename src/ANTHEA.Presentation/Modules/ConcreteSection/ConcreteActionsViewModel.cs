using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The actions of the RC section (F5.10, sub-step S2: ConcreteExchange.cs, the actions of ConcreteWorkspace.cs and the window of
/// SectionActionsView.cs in the historic view, without WPF): the table of the actions of every panel (<see cref="Domain3D"/>,
/// <see cref="Domain2D"/>, <see cref="Stress"/>, <see cref="Shear"/>, the model that the panels of S3–S5 show), the file of the actions
/// (<see cref="ExcelPath"/>) and the window «Sollecitazioni della sezione» (<see cref="SectionActionsViewModel"/> on the dialog service, command
/// <see cref="EditCommand"/>). The rows are those of the sheet (<see cref="ConcreteSheetViewModel"/>, one collection per limit state and one for
/// the shear), which owns their invalidations: every write and its order are those of the historic view.
/// </summary>
public sealed class ConcreteActionsViewModel : ObservableObject
{
    private readonly ConcreteSheetViewModel sheet;
    private string excelPath;

    internal ConcreteActionsViewModel(ConcreteSheetViewModel sheet)
    {
        this.sheet = sheet;
        Unified = SectionActionInputs.Unified(sheet.Data);
        excelPath = sheet.Settings.S("file_sollecitazioni");
        EditCommand = new AsyncRelayCommand(EditAsync, () => sheet.CanEdit);
        // The tables in the order of the tabs of the historic view (02 and 03 the domains, 04 the serviceability, 05 the shear).
        Domain3D = ConcreteActionTableViewModel.Domain(this, sheet, true);
        Domain2D = ConcreteActionTableViewModel.Domain(this, sheet, false);
        Stress = ConcreteEditor.Sets.Skip(2).ToDictionary(key => key, key => ConcreteActionTableViewModel.Serviceability(this, sheet, key), StringComparer.Ordinal);
        Shear = ConcreteActionTableViewModel.ShearTable(this, sheet);
    }

    /// <summary>The actions are the unified ones (one table of elements and limit states, edited only in the window): the tables are read-only
    /// but for «Mostra», and offer «Gestisci sollecitazioni…» instead of their commands.</summary>
    public bool Unified { get; }

    /// <summary>The table of the 3D domain (tab 02); its actions are those of the limit state of the panel.</summary>
    public ConcreteActionTableViewModel Domain3D { get; }
    /// <summary>The table of the 2D domain (tab 03).</summary>
    public ConcreteActionTableViewModel Domain2D { get; }
    /// <summary>The tables of the serviceability families (SLE, SLE_FREQ, SLE_QP; tab 04).</summary>
    public IReadOnlyDictionary<string, ConcreteActionTableViewModel> Stress { get; }
    /// <summary>The table of the shear and torsion (tab 05).</summary>
    public ConcreteActionTableViewModel Shear { get; }

    /// <summary>The table by the name of the traces: «3D», «2D», «SLE», «SLE_FREQ», «SLE_QP», «taglio».</summary>
    public ConcreteActionTableViewModel Table(string name) => name switch
    {
        "3D" => Domain3D, "2D" => Domain2D, "taglio" => Shear,
        _ => Stress.TryGetValue(name, out var table) ? table : throw new ArgumentException("Tabella delle azioni sconosciuta: " + name, nameof(name))
    };

    /// <summary>The Excel file of the actions shown next to the commands of every table (<c>file_sollecitazioni</c>); the window of the actions
    /// proposes it when it imports.</summary>
    public string ExcelPath { get => excelPath; private set => SetProperty(ref excelPath, value); }

    /// <summary>«Sollecitazioni…», «Gestisci sollecitazioni…», «Importa Excel»: the window of the actions (disabled in a read-only revision).</summary>
    public IAsyncRelayCommand EditCommand { get; }

    internal void RefreshEditable()
    {
        EditCommand.NotifyCanExecuteChanged();
        foreach (var table in new[] { Domain3D, Domain2D, Shear }.Concat(Stress.Values)) table.RefreshEditable();
    }

    // RememberExcel of the historic view: the path in the settings and in every table, Modified.
    internal void RememberExcel(string path)
    {
        sheet.Settings["file_sollecitazioni"] = path;
        ExcelPath = path;
        sheet.RaiseModified();
    }

    // «Sfoglia…» (ChooseExcel): the path chosen becomes the file of the actions.
    internal void ChooseExcel()
    {
        string? path = sheet.Files.OpenFile(new OpenFileRequest([ExcelFilter], sheet.Settings.S("file_sollecitazioni")));
        if (path is not null) RememberExcel(path);
    }

    internal static FileFilter ExcelFilter { get; } = new("Cartella Excel", "*.xlsx");

    // OpenActionEditor of the historic view: commit, the window on a copy of the data; «Applica» replaces the actions of the sheet, which opens
    // again on its data (ReloadRequested of the historic view). «Annulla» changes nothing (the commit before the window stays, behaviour 6).
    internal async Task EditAsync()
    {
        sheet.CommitSheet();
        var editor = new SectionActionsViewModel(sheet.Data, sheet.Files, sheet.Settings.S("file_sollecitazioni"));
        var result = await sheet.Dialogs.ShowDialogAsync(editor);
        if (result is null || sheet.IsDisposed) return;
        sheet.ApplyActionEditor(result);
    }

    // «Template Excel» of a table.
    internal void SaveTemplate()
    {
        string? path = sheet.Files.SaveFile(new SaveFileRequest([ExcelFilter], "ANTHEA_Sollecitazioni.xlsx"));
        if (path is null) return;
        try
        {
            Archivio.ScriviAtomico(path, SectionActionsExcel.Template()); RememberExcel(path);
            sheet.ShowStatus("Template salvato. Importa Excel apre la finestra Sollecitazioni: scegliere Importa e sostituisci oppure Importa e aggiungi.");
        }
        catch (Exception error) when (error is not OutOfMemoryException) { sheet.Messages.Inform("Template Excel", error.Message); }
    }

    // «Esporta Excel» of a table: the window of the actions for the historic actions; the unified ones written directly.
    internal async Task ExportAsync()
    {
        try
        {
            if (!SectionActionInputs.Unified(sheet.Data)) { await EditAsync(); return; }
            sheet.CommitSheet(); var bytes = new SectionActionsViewModel(sheet.Data).Export();
            string? path = sheet.Files.SaveFile(new SaveFileRequest([ExcelFilter], "ANTHEA_Sollecitazioni_compilate.xlsx"));
            if (path is null) return;
            Archivio.ScriviAtomico(path, bytes); RememberExcel(path);
            sheet.ShowStatus("Esportate tutte le combinazioni, anche quelle nascoste dai filtri. Per rileggerle aprire Sollecitazioni e scegliere il comando di importazione.");
        }
        catch (Exception error) when (error is not OutOfMemoryException) { sheet.Messages.Inform("Esportazione Excel", error.Message); }
    }
}

/// <summary>
/// The table of the actions of a panel (ActionTable, AttachClipboard and WithFilters of the historic view): the rows of a limit state, or of the
/// shear, shown with filter and order (<see cref="Table"/>), «+ Riga», «Duplica», «− Riga» («+ Combinazione» and «−» for the shear), copy and
/// paste of cells, «Template Excel», «Importa Excel», «Esporta Excel», «Sfoglia…» and the file of the actions. With the unified actions the table
/// is read-only but for «Mostra» (the domains), shows «Elemento» (and «Stato limite» for the shear) and offers only «Gestisci sollecitazioni…».
/// The table of a domain shows the actions of the limit state of its panel (<see cref="State"/>), filtered by the outcome of the domain.
/// For the view (S3–S5 and the XAML of S9): the grid refreshes <see cref="Table"/> at the end of the edit of a row (RowEditEnding of the
/// historic grid), reports its extended selection (<see cref="SetSelectedRows"/>) and its current column (<see cref="CurrentColumn"/>), maps
/// Ctrl+C and Ctrl+V (not inside a cell editor, unless the clipboard holds tabs or lines) and the context menu («Copia righe · Ctrl+C»,
/// «Incolla dalla cella · Ctrl+V», «Aggiungi dagli appunti») to <see cref="CopyCommand"/>, <see cref="PasteCommand"/> and
/// <see cref="AppendCommand"/>; the panels follow the selection with <see cref="ConcreteTableViewModel.SelectionChanged"/>.
/// </summary>
public sealed class ConcreteActionTableViewModel : ObservableObject
{
    private readonly ConcreteActionsViewModel owner;
    private readonly ConcreteSheetViewModel sheet;
    private readonly string? prefix;
    private readonly JsonObject? options;
    private IReadOnlyList<JsonRowViewModel> selectedRows = [];

    private ConcreteActionTableViewModel(ConcreteActionsViewModel owner, ConcreteSheetViewModel sheet, string family, IReadOnlyList<ConcreteColumnViewModel> columns,
        JsonRowCollection rows, string? prefix = null, JsonObject? options = null)
    {
        this.owner = owner; this.sheet = sheet; this.prefix = prefix; this.options = options;
        Family = family; Unified = owner.Unified;
        Table = new ConcreteTableViewModel(columns, rows, () => sheet.IsReadOnly);
        if (prefix is not null) Table.BaseFilter = row => ConcreteDomainChart.Includes(options!, sheet.DomainCheck(prefix, row.Values.S("id")));
        Table.SelectionChanged += (_, _) => { selectedRows = Table.Selected is null ? [] : [Table.Selected]; OnPropertyChanged(nameof(SelectedRows)); };
        bool shear = family == "Taglio";
        AddCommand = new RelayCommand(shear ? AddShear : Add, CanChange);
        DuplicateCommand = new RelayCommand(Duplicate, () => !shear && CanChange());
        RemoveCommand = new RelayCommand(shear ? RemoveShear : Remove, CanChange);
        CopyCommand = new RelayCommand(Copy, () => !Unified);
        PasteCommand = new RelayCommand(() => Paste(false), CanChange);
        AppendCommand = new RelayCommand(() => Paste(true), CanChange);
        TemplateCommand = new RelayCommand(owner.SaveTemplate, () => !Unified);
        ImportCommand = new AsyncRelayCommand(owner.EditAsync, () => sheet.CanEdit);
        ExportCommand = new AsyncRelayCommand(owner.ExportAsync, () => !Unified);
        BrowseCommand = new RelayCommand(owner.ChooseExcel, CanChange);
        owner.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ConcreteActionsViewModel.ExcelPath)) OnPropertyChanged(nameof(ExcelPath)); };
    }

    internal static ConcreteActionTableViewModel Domain(ConcreteActionsViewModel owner, ConcreteSheetViewModel sheet, bool threeD)
    {
        var options = sheet.Settings[threeD ? "dominio3d" : "dominio2d"]!.AsObject();
        string state = options.S("stato", "SLU"), ratio = threeD ? "eta3d" : "eta2d", outcome = threeD ? "esito3d" : "esito2d";
        bool unified = owner.Unified;
        var columns = new List<ConcreteColumnViewModel> { new("visible", "Mostra", ConcreteColumnKind.Bool), new("nome", "Combinazione", readOnly: unified),
            new("N", "N [kN]", readOnly: unified), new("Mx", "Mx [kNm]", readOnly: unified), new("My", "My [kNm]", readOnly: unified),
            new(ratio, "η [-]", readOnly: true), new(outcome, "Esito", readOnly: true) };
        if (unified) columns.Insert(0, new("elemento", "Elemento", readOnly: true));
        var table = new ConcreteActionTableViewModel(owner, sheet, state, columns, sheet.ActionRows(state), threeD ? "3D:" : "2D:", options);
        // The grid gets the view of its filter bar (current item: the first row), then AttachDomainRows keeps that row when its limit state shows it.
        table.Table.StartView();
        table.Reattach();
        return table;
    }

    internal static ConcreteActionTableViewModel Serviceability(ConcreteActionsViewModel owner, ConcreteSheetViewModel sheet, string key)
    {
        bool unified = owner.Unified;
        var columns = new List<ConcreteColumnViewModel> { new("nome", "Combinazione", readOnly: unified), new("N", "N [kN]", readOnly: unified),
            new("Mx", "Mx [kNm]", readOnly: unified), new("My", "My [kNm]", readOnly: unified), new("sigma_c", "σc [MPa]", readOnly: true),
            new("sigma_s", "|σs| [MPa]", readOnly: true), new("eta_sigma", "ησ [-]", readOnly: true), new("stress_status", "Tensioni", readOnly: true),
            new("wk", "Fessurazione", readOnly: true) };
        if (unified) columns.Insert(0, new("elemento", "Elemento", readOnly: true));
        var table = new ConcreteActionTableViewModel(owner, sheet, key, columns, sheet.ActionRows(key));
        table.Table.StartView();
        return table;
    }

    internal static ConcreteActionTableViewModel ShearTable(ConcreteActionsViewModel owner, ConcreteSheetViewModel sheet)
    {
        var columns = new List<ConcreteColumnViewModel> { new("nome", "Combinazione"), new("N", "N [kN]"), new("Mx", "Mx [kNm]"), new("My", "My [kNm]"),
            new("Vx", "Vx [kN]"), new("Vy", "Vy [kN]"), new("T", "T [kNm]"), new("VRdx", "VRd,x [kN]", readOnly: true), new("VRdy", "VRd,y [kN]", readOnly: true),
            new("eta_x", "ηx", readOnly: true), new("eta_y", "ηy", readOnly: true), new("eta_t", "ηT", readOnly: true), new("eta_vt", "ηV+T", readOnly: true),
            new("esito", "Esito", readOnly: true) };
        if (owner.Unified) { columns.Insert(0, new("elemento", "Elemento", readOnly: true)); columns.Insert(1, new("stato", "Stato limite", readOnly: true)); }
        var table = new ConcreteActionTableViewModel(owner, sheet, "Taglio", columns, sheet.ShearRowCollection);
        // The shear table refuses the edits with the unified actions (they are edited in the window).
        if (owner.Unified) table.Table.IsReadOnly = true;
        table.Table.StartView();
        return table;
    }

    /// <summary>The rows, filter, order and selection of the table.</summary>
    public ConcreteTableViewModel Table { get; }

    /// <summary>The limit state of the rows (grid.Tag of the historic view): SLU or SLV for a domain (the state of its panel), SLE, SLE_FREQ,
    /// SLE_QP or «Taglio».</summary>
    public string Family { get; private set; }

    /// <summary>The unified actions: see <see cref="ConcreteActionsViewModel.Unified"/>.</summary>
    public bool Unified { get; }

    /// <summary>The table of a domain panel (its rows follow <see cref="State"/>).</summary>
    public bool IsDomain => prefix is not null;

    /// <summary>The table of the shear («+ Combinazione» and «−», no «Duplica»).</summary>
    public bool IsShear => Family == "Taglio";

    /// <summary>The limit states a domain panel shows.</summary>
    public static ImmutableArray<string> States { get; } = ["SLU", "SLV"];

    /// <summary>
    /// The limit state of a domain panel (the choice «Stato limite» / «Tipo di dominio precedente» of the options of the panel, saved in
    /// <c>dominio3d.stato</c> or <c>dominio2d.stato</c>): the table shows its actions again (the row selected before when it is among them,
    /// otherwise the first), the outputs of the rows follow, Modified. Allowed also in a read-only revision (as the historic choice, which the
    /// revision leaves enabled). Null for the other tables. Since S3 it goes through the field «stato» of the form of the panel.
    /// </summary>
    public string? State
    {
        get => options?.S("stato", "SLU");
        set
        {
            if (options is null || value is null || !States.Contains(value) || sheet.IsDisposed || value == State) return;
            // Through the field «stato» of the options of the panel (S3): its callback attaches the rows again, draws the panel, Modified.
            sheet.ChooseDomainState(prefix!, value);
            OnPropertyChanged();
        }
    }

    /// <summary>The column of the current cell of the view: the paste starts there (empty: the first column of the paste).</summary>
    public string CurrentColumn { get; set; } = "";

    /// <summary>The rows selected in the view (the extended selection of the historic grid), in any order; by default the selected row.</summary>
    public IReadOnlyList<JsonRowViewModel> SelectedRows => selectedRows;

    /// <summary>The view reports its selection (more rows with Ctrl and Maiusc): the copy takes them in the order shown.</summary>
    public void SetSelectedRows(IEnumerable<JsonRowViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        selectedRows = rows.ToArray(); OnPropertyChanged(nameof(SelectedRows));
    }

    /// <summary>The Excel file of the actions (the box next to «Sfoglia…»).</summary>
    public string ExcelPath => owner.ExcelPath;

    /// <summary>«+ Riga» («+ Combinazione» for the shear): a new row, selected.</summary>
    public IRelayCommand AddCommand { get; }
    /// <summary>«Duplica»: a copy of the forces of the selected row with a new name (not for the shear).</summary>
    public IRelayCommand DuplicateCommand { get; }
    /// <summary>«− Riga» («−» for the shear): removes the selected row.</summary>
    public IRelayCommand RemoveCommand { get; }
    /// <summary>«Copia» (Ctrl+C, «Copia righe»): name and forces of the selected rows to the clipboard; allowed in a read-only revision.</summary>
    public IRelayCommand CopyCommand { get; }
    /// <summary>«Incolla» (Ctrl+V, «Incolla dalla cella»): the cells of the clipboard from the selected row and the current column.</summary>
    public IRelayCommand PasteCommand { get; }
    /// <summary>«Aggiungi dagli appunti»: the rows of the clipboard after the last row.</summary>
    public IRelayCommand AppendCommand { get; }
    /// <summary>«Template Excel»: the empty workbook of the actions; allowed in a read-only revision.</summary>
    public IRelayCommand TemplateCommand { get; }
    /// <summary>«Importa Excel» (historic actions) and «Gestisci sollecitazioni…» (unified actions): the window of the actions; not in a
    /// read-only revision.</summary>
    public IAsyncRelayCommand ImportCommand { get; }
    /// <summary>«Esporta Excel»: the window of the actions for the historic actions, the workbook of the unified ones; allowed in a read-only
    /// revision (also when it opens the window, as the historic command).</summary>
    public IAsyncRelayCommand ExportCommand { get; }
    /// <summary>«Sfoglia…»: chooses the Excel file of the actions.</summary>
    public IRelayCommand BrowseCommand { get; }

    // The commands of the bar of the table: not in a read-only revision; with the unified actions the bar has only «Gestisci sollecitazioni…».
    private bool CanChange() => sheet.CanEdit && !Unified;

    internal void RefreshEditable()
    {
        foreach (var command in new IRelayCommand[] { AddCommand, DuplicateCommand, RemoveCommand, CopyCommand, PasteCommand, AppendCommand, TemplateCommand, BrowseCommand })
            command.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged(); ExportCommand.NotifyCanExecuteChanged();
        Table.RefreshReadOnly();
    }

    // AttachDomainRows of the historic view: the rows of the limit state of the panel, filtered and ordered again; the row selected before when
    // it is shown, otherwise the first.
    internal void Reattach()
    {
        if (prefix is null) return;
        string? previous = Table.Selected?.Values.S("id");
        Family = State!;
        Table.Attach(sheet.ActionRows(Family));
        Table.Selected = Table.Shown.FirstOrDefault(r => r.Values.S("id") == previous) ?? Table.Shown.FirstOrDefault();
        OnPropertyChanged(nameof(Family));
    }

    // ---------------------------------------------------------------- rows (ActionTable of the historic view)

    private void Add()
    {
        string set = Family; Table.CommitEdits();
        var row = sheet.NewActionRow(set, ConcreteActionPresentation.NextName(sheet.ActionRows(set).Select(r => r.Values.S("nome"))), "0", "0", "0");
        sheet.ActionRows(set).Add(row); sheet.SyncActions(set); sheet.InvalidateActions(set);
        Table.Selected = row;
    }

    private void Duplicate()
    {
        if (Table.Selected is not { } selected) return;
        string set = Family; var values = selected.Values;
        var row = sheet.NewActionRow(set, ConcreteActionPresentation.NextName(sheet.ActionRows(set).Select(r => r.Values.S("nome"))), values.S("N"), values.S("Mx"), values.S("My"));
        sheet.ActionRows(set).Add(row); sheet.SyncActions(set); sheet.InvalidateActions(set);
        Table.Selected = row;
    }

    private void Remove()
    {
        Table.CommitEdits();
        if (Table.Selected is not { } row) return;
        string set = Family; var rows = sheet.ActionRows(set);
        int index = rows.IndexOf(row);
        rows.Remove(row); sheet.SyncActions(set); sheet.InvalidateActions(set);
        if (rows.Count > 0) Table.Selected = rows[Math.Min(index, rows.Count - 1)];
    }

    // «+ Combinazione» and «−» of the shear (BuildShearPanel): the rows saved in the options of the shear, then the shear calculated again.
    private void AddShear()
    {
        var rows = sheet.ShearRowCollection;
        var row = sheet.NewShearRow(ConcreteActionPresentation.ShearName(rows.Count));
        rows.Add(row); Table.Selected = row;
        sheet.StoreShearRows(); sheet.InvalidateActions("Taglio");
    }

    private void RemoveShear()
    {
        Table.CommitEdits();
        if (Table.Selected is { } row) sheet.ShearRowCollection.Remove(row);
        sheet.StoreShearRows(); sheet.InvalidateActions("Taglio");
    }

    // ---------------------------------------------------------------- clipboard (AttachClipboard of the historic view)

    private void Copy()
    {
        try
        {
            var selected = selectedRows.ToHashSet();
            var rows = Table.Shown.Where(selected.Contains).ToArray();
            if (rows.Length == 0) return;
            sheet.Clipboard.SetText(ConcreteActionPresentation.CopyText(rows.Select(r => r.Values), Family));
            sheet.ShowStatus(ConcreteActionPresentation.CopyStatus(rows.Length));
        }
        catch (Exception error) when (error is not OutOfMemoryException) { sheet.ShowStatus("Copia: " + error.Message); }
    }

    private void Paste(bool append)
    {
        try { PasteCells(sheet.Clipboard.GetText(), append); }
        catch (Exception error) when (error is not OutOfMemoryException)
        { sheet.Messages.Warn("Incolla sollecitazioni", error.Message + "\nNessun dato incollato."); }
    }

    // PasteCells of the historic view: the whole paste is read and validated before a single existing row changes; the rows shown from the
    // selected one (or after the last) take the cells, the rows missing are added; then the commit of the sheet and the invalidation.
    private void PasteCells(string text, bool append)
    {
        string family = Family;
        var plan = ConcreteActionPresentation.Plan(family, text, CurrentColumn);
        var (keys, startColumn, width, matrix) = (plan.Keys, plan.StartColumn, plan.Width, plan.Rows);
        Table.CommitEdits();
        var collection = family == "Taglio" ? sheet.ShearRowCollection : sheet.ActionRows(family);
        var visible = Table.Shown.ToList();
        int start = append ? visible.Count : Math.Max(0, Table.Selected is null ? -1 : visible.IndexOf(Table.Selected));
        sheet.Synchronized(() =>
        {
            using var refresh = collection.DeferRefresh();
            for (int r = 0; r < matrix.Count; r++)
            {
                JsonRowViewModel target;
                if (start + r < visible.Count) target = visible[start + r];
                else
                {
                    string name = ConcreteActionPresentation.PastedName(collection.Count);
                    target = family == "Taglio" ? sheet.NewShearRow(name) : sheet.NewActionRow(family, name, "0", "0", "0");
                    collection.Add(target);
                }
                using var notifications = JsonRowViewModel.DeferNotifications([target]);
                for (int c = 0; c < width; c++) target[keys[startColumn + c]] = matrix[r][c].Trim();
            }
        });
        sheet.CommitSheet(); sheet.InvalidateActions(family);
        sheet.ShowStatus(ConcreteActionPresentation.PasteStatus(matrix.Count));
    }
}
