using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    public IReadOnlyList<string> ResultTypes { get; } = ["Sollecitazioni", "Spostamenti nodali"];
    public IReadOnlyList<string> DeformationScaleModes { get; } = ["Automatica", "Reale · ×1", "Fattore manuale"];
    public IReadOnlyList<string> DisplacementUnits { get; } = ["mm", "cm", "m"];
    public IReadOnlyList<ModelDisplacement> Displacements { get; private set; } = [];
    public bool IsDisplacementResult => SelectedResultType == 1;
    public bool IsForceResult => !IsDisplacementResult;
    public bool HasDisplacements => Displacements.Count > 0;
    public bool IsDisplacementEnvelope => Snapshot?.Results.FirstOrDefault(r => r.Name == SelectedCase)?.IsEnvelope == true;
    public bool CanDeform => HasDisplacements && !IsDisplacementEnvelope;
    public bool IsManualDeformationScale => SelectedDeformationScale == 2;
    public double EffectiveDeformationScale { get; private set; } = 1;
    public string DeformationScaleLabel => $"Fattore applicato ×{EffectiveDeformationScale:G5}";
    public string DeformationHeading => ShowDeformation && CanDeform ? $"Deformata nodale · ×{EffectiveDeformationScale * DeformationPhase:G5}" + (DeformationPhase != 1 ? $" · fase grafica {DeformationPhase:P0}" : "") : "Geometria indeformata";
    public string DeformationMessage { get; private set; } = "Spostamenti non acquisiti per questo caso.";

    [ObservableProperty] private int selectedResultType;
    [ObservableProperty] private string displacementUnit = "mm";
    [ObservableProperty] private bool showDeformation;
    [ObservableProperty] private int selectedDeformationScale;
    [ObservableProperty] private double deformationScale = 100;
    [ObservableProperty] private bool showUndeformed = true;
    [ObservableProperty] private bool showUndeformedMesh;
    [ObservableProperty] private double undeformedOpacity = .35;
    [ObservableProperty] private bool showDeformationSettings;

    private string forceComponent = "Myy", displacementComponent = "|u|";
    partial void OnSelectedResultTypeChanged(int value)
    {
        bool prior = updating; updating = true;
        if (value == 1) { forceComponent = SelectedComponent; SelectedComponent = displacementComponent; ShowDeformationSettings = true; ShowMemberDiagram = false; }
        else { displacementComponent = SelectedComponent; SelectedComponent = forceComponent; }
        if (!Components.Contains(SelectedComponent)) SelectedComponent = Components.FirstOrDefault() ?? "";
        updating = prior;
        foreach (var name in new[] { nameof(IsDisplacementResult), nameof(IsForceResult), nameof(IsBeamFamily), nameof(Components) }) OnPropertyChanged(name);
        RefreshScene();
    }
    partial void OnDisplacementUnitChanged(string value) => RefreshScene();
    partial void OnShowDeformationChanged(bool value) => RefreshScene();
    partial void OnSelectedDeformationScaleChanged(int value) { OnPropertyChanged(nameof(IsManualDeformationScale)); RefreshScene(); }
    partial void OnDeformationScaleChanged(double value) => RefreshScene();
    partial void OnShowUndeformedChanged(bool value) => RefreshScene();
    partial void OnShowUndeformedMeshChanged(bool value) => RefreshScene();
    partial void OnUndeformedOpacityChanged(double value) => RefreshScene();

    static double DisplacementMagnitude(ModelDisplacement d) => ModelMeasurements.DisplacementMagnitude(d);
    void UpdateDeformations()
    {
        Displacements = Snapshot?.Results.FirstOrDefault(r => r.Name == SelectedCase)?.Displacements ?? [];
        var ids = Displacements.Select(d => d.Node).ToHashSet();
        int covered = Snapshot!.Elements.Count(e => e.Nodes.All(ids.Contains));
        double max = Displacements.Count == 0 ? 0 : Displacements.Max(DisplacementMagnitude);
        double extent = Math.Max(Snapshot.Nodes.Max(n => n.X) - Snapshot.Nodes.Min(n => n.X),
            Math.Max(Snapshot.Nodes.Max(n => n.Y) - Snapshot.Nodes.Min(n => n.Y), Snapshot.Nodes.Max(n => n.Z) - Snapshot.Nodes.Min(n => n.Z)));
        EffectiveDeformationScale = SelectedDeformationScale switch
        {
            1 => 1,
            2 => double.IsFinite(DeformationScale) ? Math.Clamp(DeformationScale, .001, 1_000_000) : 1,
            _ => max > 0 && double.IsFinite(max) ? Math.Clamp(extent * .08 / max, .001, 1_000_000) : 1
        };
        DeformationMessage = !HasDisplacements ? "Spostamenti non acquisiti per questo caso. Importa i risultati nodali per attivare la deformata."
            : IsDisplacementEnvelope ? "Inviluppo: estremi delle componenti non simultanei. Disponibili i contouring ux, uy e uz; forma deformata e modulo vettoriale non ricostruibili."
            : $"{Displacements.Count:N0} nodi · {covered:N0}/{Snapshot.Elements.Length:N0} elementi completi. Traslazioni globali importate; interpolazione nodale, senza curvature interne o rotazioni di sezione."
                + (covered < Snapshot.Elements.Length ? " Gli elementi incompleti restano grigi nella posizione originale." : "");
        if (IsDisplacementResult && !Components.Contains(SelectedComponent))
        {
            bool prior = updating; updating = true; SelectedComponent = Components.First(); updating = prior;
        }
        foreach (var name in new[] { nameof(Displacements), nameof(HasDisplacements), nameof(CanDeform), nameof(IsDisplacementEnvelope),
            nameof(DeformationMessage), nameof(EffectiveDeformationScale), nameof(DeformationScaleLabel), nameof(DeformationHeading), nameof(Components) }) OnPropertyChanged(name);
    }
    IReadOnlyList<ResultValue> DisplacementField()
    {
        var nodes = Displacements.ToDictionary(d => d.Node);
        double Value(ModelDisplacement d) => SelectedComponent switch { "ux" => d.X, "uy" => d.Y, "uz" => d.Z, _ => DisplacementMagnitude(d) };
        // One imported nodal value is shared by all adjoining objects. No averaging or extrapolation.
        return Snapshot!.Elements.SelectMany(e => e.Nodes.Where(nodes.ContainsKey).Select(n => new ResultValue(e.Id, n, Value(nodes[n]),
            ElementFamilies.IsLine(e.Type) ? n == e.Nodes[0] ? 0 : 1 : null))).ToArray();
    }
}
