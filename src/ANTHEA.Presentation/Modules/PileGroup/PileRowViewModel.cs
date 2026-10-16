using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Modules.PileGroup;

/// <summary>
/// Row of the table of the coordinates (the PileRow of the historic view): identifier and coordinates as typed. Every change of a
/// cell calls the callback of the sheet, which writes the table into the data with the generic layout and recalculates; the
/// table writes at every keystroke, as the historic binding with UpdateSourceTrigger=PropertyChanged. The public constructor
/// without arguments is the new row of the table.
/// </summary>
public sealed class PileRowViewModel : ObservableObject
{
    private string id = "", x = "0", y = "0";

    /// <summary>Called after every change of a cell, also when the text does not change (as the historic rows).</summary>
    internal Action? Changed { get; set; }

    /// <summary>Read-only revision: the cells refuse the changes (the view shows the table read-only).</summary>
    internal bool Locked { get; set; }

    public string Id { get => id; set => Set(ref id, value); }

    public string X { get => x; set => Set(ref x, value); }

    public string Y { get => y; set => Set(ref y, value); }

    private void Set(ref string field, string value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (Locked) { OnPropertyChanged(name); return; }
        field = value;
        OnPropertyChanged(name);
        Changed?.Invoke();
    }
}
