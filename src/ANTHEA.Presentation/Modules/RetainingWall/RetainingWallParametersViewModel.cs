using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;

namespace Anthea.Presentation.Modules.RetainingWall;

/// <summary>
/// The window «Valori di calcolo…» of the wall sheet (F5.9), the CalculationParametersWindow of the historic view: the editable inputs of
/// the data as rows (<see cref="WallParameterTable"/>, path, group, meaning, value, unit), the values used by the calculation for a
/// combination of the result, and «Applica e ricalcola».
/// <list type="bullet">
/// <item>«Applica e ricalcola»: the values written into a copy of the data (an invalid value is reported at once), then the copy checked by
/// the engine of the module on a background thread with its own calculation (<see cref="SheetCalculation{TResult}"/>, independent from
/// that of the sheet): meanwhile the window is disabled. A valid copy replaces the data of the sheet, the window closes and the sheet
/// calculates at once. When the sheet changed since the opening the values are not applied.</item>
/// <item>Closing during the check cancels it: nothing is applied.</item>
/// <item>Read-only revision: the rows and «Applica e ricalcola» are refused (the historic window applies them: a defect, see
/// docs/refactoring/f5-9-muri.md).</item>
/// </list>
/// </summary>
public sealed class RetainingWallParametersViewModel : ViewModelBase
{
    private readonly RetainingWallSheetViewModel sheet;
    private readonly WallParameterTable table;
    private readonly SheetCalculation<JsonObject> validation;
    private readonly int openedRevision;
    private string message = RetainingWallPresentation.ParametersHint;
    private bool messageError, closed;
    private string? selectedCombination;
    private IReadOnlyList<WallPanelBlock> derived = [];
    private Task applying = Task.CompletedTask;

    internal RetainingWallParametersViewModel(RetainingWallSheetViewModel sheet, int openedRevision, IReadOnlyList<string> combinations, string? selected)
    {
        this.sheet = sheet; this.openedRevision = openedRevision;
        table = new WallParameterTable(sheet.Data);
        foreach (var row in table.Rows) Rows.Add(new JsonRowViewModel(row));
        var service = sheet.Service;
        validation = new SheetCalculation<JsonObject>((snapshot, token) => WallParameterTable.Validate(service, snapshot, token), _ => new CalculationReview("", []), TimeSpan.Zero);
        validation.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(SheetCalculation<JsonObject>.State)) OnPropertyChanged(nameof(IsEnabled)); };
        Combinations = combinations.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToArray();
        selectedCombination = selected is not null && Combinations.Contains(selected) ? selected : null;
        Display();
    }

    public string Title => RetainingWallPresentation.ParametersTitle;

    /// <summary>The columns of the rows (only «Valore assegnato» can be edited).</summary>
    public IReadOnlyList<FieldSpec> Columns => RetainingWallFields.ParameterColumns;

    /// <summary>The editable inputs of the data, on the rows of a copy: editing them never changes the sheet before «Applica e ricalcola».</summary>
    public JsonRowCollection Rows { get; } = [];

    /// <summary>The rows cannot be edited: the sheet is a read-only revision.</summary>
    public bool IsReadOnly => !sheet.CanEdit;

    /// <summary>The window can be used: not closed and not checking the values.</summary>
    public bool IsEnabled => !closed && validation.State is not (CalculationState.Pending or CalculationState.Running);

    /// <summary>The hint, «Controllo dati e ricalcolo…», or the error of the values (<see cref="IsMessageError"/>).</summary>
    public string Message { get => message; private set => SetProperty(ref message, value); }
    public bool IsMessageError { get => messageError; private set => SetProperty(ref messageError, value); }

    /// <summary>The combinations of the result at the opening and the one whose values are shown.</summary>
    public IReadOnlyList<string> Combinations { get; }

    public string? SelectedCombination
    {
        get => selectedCombination;
        set
        {
            if (!IsEnabled || value is null || value == selectedCombination || !Combinations.Contains(value)) return;
            selectedCombination = value; OnPropertyChanged(); Display();
        }
    }

    /// <summary>The values used by the calculation for the combination chosen, or the request to calculate again.</summary>
    public IReadOnlyList<WallPanelBlock> Derived { get => derived; private set => SetProperty(ref derived, value); }

    /// <summary>The last «Applica e ricalcola»: the check, then the calculation of the sheet when applied.</summary>
    public Task Applying => applying;

    /// <summary>The window was closed (by the user, after applying, or with the sheet).</summary>
    public bool IsClosed => closed;

    /// <summary>Raised once when the window closes.</summary>
    public event EventHandler? Closed;

    private void Display()
    {
        Derived = sheet.Calculation?.Cases.FirstOrDefault(c => c.Name == selectedCombination) is { } c
            ? [WallPanelBlock.Table(RetainingWallPresentation.DerivedColumns, RetainingWallPresentation.DerivedRows(c), 230)]
            : [WallPanelBlock.Of(RetainingWallPresentation.DerivedMissing)];
    }

    /// <summary>«Applica e ricalcola»; refused when closed, while checking or when the sheet is read-only.</summary>
    public Task ApplyAsync()
    {
        if (closed || !IsEnabled || IsReadOnly) return applying;
        applying = ApplyCoreAsync();
        return applying;
    }

    private async Task ApplyCoreAsync()
    {
        JsonObject snapshot;
        try
        {
            if (sheet.IsDisposed || sheet.EditRevision != openedRevision) throw new ArgumentException(RetainingWallPresentation.ParametersChanged);
            Message = RetainingWallPresentation.ParametersChecking;
            snapshot = table.CreateSnapshot();
        }
        catch (Exception error) when (error is not OutOfMemoryException) { Fail(error.Message); return; }
        bool published = await validation.CalculateAsync(snapshot);
        if (!published || closed || sheet.IsDisposed) return;
        try
        {
            if (sheet.EditRevision != openedRevision) throw new ArgumentException(RetainingWallPresentation.ParametersChanged);
            if (validation.State != CalculationState.Completed || validation.Result is not { } values) throw new ArgumentException(validation.Message);
            sheet.ApplyParameters(values);
        }
        catch (Exception error) when (error is not OutOfMemoryException) { Fail(error.Message); return; }
        Close();
        await sheet.RecalculateAsync();
    }

    private void Fail(string text)
    {
        if (closed || sheet.IsDisposed) return;
        Message = text; IsMessageError = true;
    }

    /// <summary>«Chiudi» (also while checking: the check is cancelled and nothing is applied).</summary>
    public void Close()
    {
        if (closed) return;
        closed = true; validation.Dispose();
        OnPropertyChanged(nameof(IsEnabled)); OnPropertyChanged(nameof(IsClosed));
        Closed?.Invoke(this, EventArgs.Empty); Closed = null;
    }

    /// <summary>The sheet became read-only or was closed.</summary>
    internal void Refresh() => OnPropertyChanged(nameof(IsReadOnly));

    protected override void OnDisposed() => Close();
}
