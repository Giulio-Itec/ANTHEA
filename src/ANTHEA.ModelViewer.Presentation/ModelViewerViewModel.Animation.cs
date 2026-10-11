using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    public event EventHandler? AnimationStateChanged;
    public event EventHandler? DeformationFrameChanged;
    public bool CanAnimate => CanDeform && !ClipEnabled && !CompareEnabled && !IsReportRunning && !disposed;
    public string AnimationLabel => IsAnimationPlaying ? "Pausa" : "Riproduci";
    public string DeformationPhaseLabel => $"Fase grafica {DeformationPhase:P0} · fattore ×{EffectiveDeformationScale * DeformationPhase:G5}";
    public string AnimationHelp => ClipEnabled ? "Per animare disattiva il piano di taglio. La deformata statica resta disponibile."
        : CompareEnabled ? "Per animare chiudi il confronto affiancato."
        : "Interpolazione grafica 0 → 100% → 0, non una storia temporale di analisi. Colori e valori restano quelli del caso; etichette sospese durante la riproduzione.";
    [ObservableProperty] private bool isAnimationPlaying;
    [ObservableProperty] private double deformationPhase = 1;
    [ObservableProperty] private double animationDuration = 4;
    private double animationProgress;

    partial void OnIsAnimationPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(AnimationLabel));
        AnimationStateChanged?.Invoke(this, EventArgs.Empty);
    }
    partial void OnDeformationPhaseChanged(double value)
    {
        OnPropertyChanged(nameof(DeformationPhaseLabel)); OnPropertyChanged(nameof(DeformationHeading));
        if (!updating && !disposed) DeformationFrameChanged?.Invoke(this, EventArgs.Empty);
    }
    [RelayCommand(CanExecute = nameof(CanAnimate))]
    void ToggleAnimation()
    {
        if (IsAnimationPlaying) { IsAnimationPlaying = false; return; }
        ShowDeformation = true;
        animationProgress = Math.Acos(1 - 2 * Math.Clamp(DeformationPhase, 0, 1)) / (2 * Math.PI);
        IsAnimationPlaying = true;
    }
    [RelayCommand] void ResetDeformationFrame() { IsAnimationPlaying = false; DeformationPhase = 1; }
    public void PauseAnimation() => IsAnimationPlaying = false;
    public void AdvanceAnimation(TimeSpan elapsed)
    {
        if (!IsAnimationPlaying || !CanAnimate) return;
        animationProgress = (animationProgress + Math.Clamp(elapsed.TotalSeconds, 0, .5) / Math.Clamp(AnimationDuration, 1, 20)) % 1;
        DeformationPhase = (1 - Math.Cos(animationProgress * 2 * Math.PI)) / 2;
    }
    void UpdateAnimationAvailability()
    {
        foreach (var name in new[] { nameof(CanAnimate), nameof(AnimationHelp), nameof(DeformationPhaseLabel) }) OnPropertyChanged(name);
        ToggleAnimationCommand.NotifyCanExecuteChanged();
    }
}
