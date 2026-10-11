using System.Diagnostics;
using System.Windows.Threading;

namespace ANTHEA.ModelViewer.Wpf;

public partial class ModelViewerControl
{
    private readonly DispatcherTimer animationTimer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch animationClock = new();
    private bool preparingDeformation;

    void AttachAnimation()
    {
        viewModel.AnimationStateChanged += AnimationStateChanged;
        viewModel.DeformationFrameChanged += DeformationFrameChanged;
        animationTimer.Tick += AnimationTick;
    }
    void DetachAnimation()
    {
        animationTimer.Stop(); animationTimer.Tick -= AnimationTick;
        viewModel.AnimationStateChanged -= AnimationStateChanged;
        viewModel.DeformationFrameChanged -= DeformationFrameChanged;
    }
    void AnimationStateChanged(object? sender, EventArgs args)
    {
        animationTimer.Stop();
        if (disposed || renderer == null || preparingDeformation) return;
        if (!viewModel.IsAnimationPlaying) { UpdateScene(); return; }
        preparingDeformation = true;
        try
        {
            var options = Options(viewModel);
            if (!renderer.PrepareAnimation(options with { Exploration = options.Exploration! with { DeformationScale = viewModel.EffectiveDeformationScale } }))
            {
                viewModel.PauseAnimation(); UpdateScene();
                viewModel.Status = "Animazione non disponibile per questa geometria. La deformata statica rimane attiva."; return;
            }
            renderer.RenderAnimationFrame(viewModel.DeformationPhase);
            animationClock.Restart(); animationTimer.Start();
            viewModel.Status = $"Animazione grafica · {renderer.AnimationVertexCount:N0} vertici · valori del caso invariati";
        }
        catch (Exception ex)
        {
            viewModel.PauseAnimation(); UpdateScene(); viewModel.Status = "Animazione interrotta: " + ex.Message;
        }
        finally { preparingDeformation = false; }
    }
    void AnimationTick(object? sender, EventArgs args)
    {
        if (disposed || !viewModel.IsAnimationPlaying) { animationTimer.Stop(); return; }
        var elapsed = animationClock.Elapsed; animationClock.Restart();
        var frame = Stopwatch.StartNew();
        viewModel.AdvanceAnimation(elapsed);
        animationTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, 33 - frame.Elapsed.TotalMilliseconds));
    }
    void DeformationFrameChanged(object? sender, EventArgs args)
    {
        if (disposed || renderer == null) return;
        if (viewModel.IsAnimationPlaying) renderer.RenderAnimationFrame(viewModel.DeformationPhase);
        else UpdateScene();
    }
}
