using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

/// <summary>
/// Boxes «Scavo», «Falda» and «Sovraccarico» of the page Modello of the command bar (organizzazione del modulo paratie, progetto del 16/11,
/// step 3): the values of the phase chosen in the tabs, read from and written into its row of «Fasi costruttive» with the rule of the field
/// of the table (the text as typed; an empty water level means no water, as in the table). Nothing is computed. Without a phase, or in a
/// read-only sheet, the boxes refuse the typing. A confirmed value is one operation (contract C1): what was typed before is confirmed
/// first, then the value, each one step of the history of the project; an unchanged value records nothing.
/// </summary>
public sealed class EmbeddedWallStageLevels : ObservableObject
{
    private readonly Func<bool> editable;
    private readonly Action before, edited;
    private EmbeddedWallRow? stage;

    internal EmbeddedWallStageLevels(Func<bool> editable, Action before, Action edited)
    {
        this.editable = editable; this.before = before; this.edited = edited;
    }

    public bool HasStage => stage is not null;
    public bool IsReadOnly => stage is null || !editable();
    /// <summary>Identifier of the phase the boxes show.</summary>
    public string Stage => stage?["id"] ?? "";
    /// <summary>Ground level on the left [m].</summary>
    public string LeftGround { get => Get("left_ground"); set => Set("left_ground", value); }
    /// <summary>Ground level on the right [m].</summary>
    public string RightGround { get => Get("right_ground"); set => Set("right_ground", value); }
    /// <summary>Water level on the left [m]; empty: no water.</summary>
    public string LeftWater { get => Get("left_water"); set => Set("left_water", value); }
    /// <summary>Water level on the right [m]; empty: no water.</summary>
    public string RightWater { get => Get("right_water"); set => Set("right_water", value); }
    /// <summary>Surcharge on the left [kPa].</summary>
    public string LeftSurcharge { get => Get("left_surcharge"); set => Set("left_surcharge", value); }
    /// <summary>Surcharge on the right [kPa].</summary>
    public string RightSurcharge { get => Get("right_surcharge"); set => Set("right_surcharge", value); }
    /// <summary>Uniform load on the whole strip [kN/m].</summary>
    public string WallLoad { get => Get("distributed_load"); set => Set("distributed_load", value); }

    /// <summary>The phase on screen (or none): every box shows its value again.</summary>
    internal void Show(EmbeddedWallRow? row)
    {
        stage = row;
        OnPropertyChanged(string.Empty);
    }

    private string Get(string key) => stage?[key] ?? "";

    private void Set(string key, string? value, [CallerMemberName] string property = "")
    {
        value ??= "";
        // Refused or unchanged: the box shows the stored value again.
        if (stage is null || !editable() || stage[key] == value) { OnPropertyChanged(property); return; }
        var row = stage;
        before();
        row[key] = value;
        edited();
    }
}
