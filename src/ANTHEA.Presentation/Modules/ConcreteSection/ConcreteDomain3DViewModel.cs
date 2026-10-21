using Anthea.Application.Concrete;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The 3D domain of a panel of the RC section as plain data (F5.10 S3, decisions Q3 and C8): what the historic DomainViewport3D keeps besides
/// its camera and its WPF models. The mesh shown, the points of the actions (with their outcome), the selected action and its resistance,
/// the display options, the factors of the axes, the ratios and the resistances of the rows, the note under the drawing. <see cref="SetMesh"/>,
/// <see cref="SetActions"/> and <see cref="SetAxisScale"/> keep the rules of the historic viewport; the view draws these data and raises
/// <see cref="ActionPicked"/> when the user clicks a point.
/// </summary>
public sealed class ConcreteDomain3DViewModel : ObservableObject
{
    private readonly List<(string Id, ConcreteResults.Action Force, bool Pass)> actions = [];

    /// <summary>The mesh of the domain shown; null while the domain is to be calculated.</summary>
    public ConcreteResults.Mesh? Mesh { get; private set; }
    /// <summary>The points of the actions shown, with their outcome (η ≤ 1).</summary>
    public IReadOnlyList<(string Id, ConcreteResults.Action Force, bool Pass)> Actions => actions;
    /// <summary>The row selected in the table.</summary>
    public string? SelectedId { get; private set; }
    /// <summary>The selected action, when it is among the points and a mesh is shown.</summary>
    public ConcreteResults.Action? SelectedAction { get; private set; }
    /// <summary>The resistance of the selected action.</summary>
    public ConcreteResults.Action? SelectedResistance { get; private set; }
    public bool ShowActions { get; set; } = true;
    public bool OnlySelectedActions { get; set; }
    public bool ShowResistance { get; set; } = true;
    public bool ShowVerificationLines { get; set; } = true;
    public bool ColorByRatio { get; set; }
    /// <summary>«Mesh»: the edges of the triangles drawn over the surface.</summary>
    public bool Wireframe { get; private set; }
    /// <summary>«Adatta anche le azioni» of the window «Scala…».</summary>
    public bool FitIncludesActions { get; set; }
    public string VerificationCriterion { get; set; } = "N costante";
    public double ActionPointSize { get; set; } = 5;
    public double ResistancePointSize { get; set; } = 5;
    /// <summary>The opacity of the surface (1 − the transparency of the panel).</summary>
    public double SurfaceOpacity { get => opacity; set => opacity = Math.Clamp(value, 0, 1); }
    private double opacity = 1;
    /// <summary>The factors of the axes Mx, N, My (0.25–4).</summary>
    public (double Mx, double N, double My) Scale { get; private set; } = (1, 1, 1);
    /// <summary>The ratio of every row (colours η); null before the results.</summary>
    public IReadOnlyDictionary<string, double?>? Ratios { get; set; }
    /// <summary>The resistances of every visible row («Tutti i punti resistenti»); null otherwise.</summary>
    public IReadOnlyDictionary<string, ConcreteResults.Action>? Resistances { get; set; }
    /// <summary>The note under the drawing.</summary>
    public string Note { get; private set; } = "Dominio in attesa di aggiornamento automatico";
    /// <summary>The triangles of the mesh.</summary>
    public int TriangleCount => ConcreteDomainChart.TriangleCount(Mesh);

    /// <summary>The data changed: the view draws them again.</summary>
    public event EventHandler? Changed;
    /// <summary>The user clicked the point of a row (the view raises it, the panel selects the row).</summary>
    public event EventHandler<string>? ActionPicked;

    /// <summary>The view reports the click on the point of a row.</summary>
    public void Pick(string id) => ActionPicked?.Invoke(this, id);

    /// <summary>The mesh of the domain: the points and the selection start again empty (SetMesh of the historic viewport).</summary>
    public void SetMesh(ConcreteResults.Mesh? value, bool force = false)
    {
        if (!force && ReferenceEquals(Mesh, value) && value is not null) return;
        Mesh = value; actions.Clear(); SelectedAction = SelectedResistance = null;
        if (value is null) { Note = "Dominio da calcolare · nessuna mesh valida"; Changed?.Invoke(this, EventArgs.Empty); return; }
        Note = $"{TriangleCount:N0} triangoli · N [kN], Mx / My [kNm] · assi scalati separatamente";
        SetActions([], null, null);
    }

    /// <summary>The points of the actions, the selected row and its resistance (SetActions of the historic viewport).</summary>
    public void SetActions(IEnumerable<(string Id, ConcreteResults.Action Force, bool Pass)> points, string? selected, ConcreteResults.Action? resistant)
    {
        ArgumentNullException.ThrowIfNull(points);
        var list = points.ToList();
        actions.Clear(); actions.AddRange(list); SelectedId = selected; SelectedAction = null; SelectedResistance = resistant;
        if (Mesh is not null)
            foreach (var action in actions)
                if (action.Id == selected) SelectedAction = action.Force;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The factors of the axes (each between 0.25 and 4): the mesh and the points are placed again.</summary>
    public void SetAxisScale(double mx, double n, double my)
    {
        mx = Math.Clamp(mx, .25, 4); n = Math.Clamp(n, .25, 4); my = Math.Clamp(my, .25, 4);
        if (Scale == (mx, n, my)) return;
        Scale = (mx, n, my);
        var points = actions.ToArray(); var id = SelectedId; var resistance = SelectedResistance;
        SetMesh(Mesh, true); SetActions(points, id, resistance);
    }

    /// <summary>«Mesh»: shows or hides the edges of the triangles.</summary>
    public void ToggleWireframe() { Wireframe = !Wireframe; Changed?.Invoke(this, EventArgs.Empty); }
}
