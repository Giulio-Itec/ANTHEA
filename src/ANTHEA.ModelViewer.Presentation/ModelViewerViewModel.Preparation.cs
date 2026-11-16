using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ANTHEA.ModelViewer.Presentation;

/// <summary>
/// Rilievo R7 (16/11/2026, proposal of the user, approved): the base cases of GPC are prepared in the background as soon as the model is on
/// screen, not only when the catalogue of the combinations opens (refactoring of 29/10/2026): on the MIDAS model of 16/11 the preparation is
/// 25 of the 27 s of the first combination. It starts once for each copy opened or imported, after the view says the scene is ready
/// (<see cref="SceneReady"/>), only when there are combinations to calculate, below normal priority, and not when the computer has less than
/// <see cref="BackgroundPreparationMemory"/> of free memory; a new copy or the closing of the view cancels it. The preparation is the one of the
/// catalogue and of the calculation (GpcModelCombinationService): whichever comes first does it, the others wait for it and reuse it.
/// </summary>
public sealed partial class ModelViewerViewModel
{
    /// <summary>
    /// Free physical memory under which the preparation does not start by itself: 8 GiB. The preparation of the MIDAS model of 16/11 keeps
    /// 2.3 GB of GPC data and takes about 4 GB at its peak (3.9 GB for the process of a single combination outside the interface): with less
    /// than twice that free, work the user did not ask for could push the computer to the page file. The catalogue and the first calculation
    /// still prepare, as before.
    /// </summary>
    public const long BackgroundPreparationMemory = 8_589_934_592;

    CancellationTokenSource? preparation;
    bool preparationPending;

    /// <summary>The free memory of the computer, read when the preparation would start (the checks give their own).</summary>
    public Func<long> AvailableMemory { get; init; } = SystemMemory.AvailableBytes;

    /// <summary>True while the base cases of GPC are prepared in the background.</summary>
    [ObservableProperty] bool isPreparingCombinations;

    /// <summary>What the preparation in the background did, for the catalogue; empty before it starts.</summary>
    [ObservableProperty] string combinationPreparationText = "";

    /// <summary>The catalogue shows <see cref="CombinationPreparationText"/> only when there is something to say (the layout is unchanged otherwise).</summary>
    public bool HasCombinationPreparationText => CombinationPreparationText.Length > 0;
    partial void OnCombinationPreparationTextChanged(string value) => OnPropertyChanged(nameof(HasCombinationPreparationText));

    /// <summary>The view tells that the scene of the copy is on screen: the first time after a copy is opened or imported, the preparation
    /// of the base cases starts in the background when it is worth it.</summary>
    public void SceneReady()
    {
        if (!preparationPending || disposed || Snapshot is not { } snapshot) return;
        preparationPending = false;
        if (!CanManageCombinations || !ToCalculate(Combinations).Any()) return;
        long free = AvailableMemory();
        if (free < BackgroundPreparationMemory)
        {
            CombinationPreparationText = $"Casi base GPC non preparati in anticipo: memoria libera {ModelCombinations.CopyLimit(free)}, meno di {ModelCombinations.CopyLimit(BackgroundPreparationMemory)}. Si preparano con il primo calcolo.";
            return;
        }
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        preparation = cancellation;
        IsPreparingCombinations = true; CombinationPreparationText = "Preparazione dei casi base GPC in background, a bassa priorità…";
        _ = PrepareInBackgroundAsync(snapshot, cancellation);
    }

    async Task PrepareInBackgroundAsync(ModelSnapshot snapshot, CancellationTokenSource cancellation)
    {
        bool prepared = false;
        try { prepared = await combinationService.PrepareAsync(snapshot, true, cancellation.Token); }
        finally
        {
            if (ReferenceEquals(preparation, cancellation))
            {
                preparation = null;
                if (!disposed)
                {
                    IsPreparingCombinations = false;
                    CombinationPreparationText = prepared ? "Casi base GPC pronti: la prima combinazione parte senza prepararli." : "";
                }
            }
            cancellation.Dispose();
        }
    }

    /// <summary>A new copy on screen (an import, a copy read again): the preparation of the previous one stops; the new one waits for its scene.</summary>
    void ResetPreparation()
    {
        preparation?.Cancel(); preparation = null;
        preparationPending = true;
        if (IsPreparingCombinations) IsPreparingCombinations = false;
        CombinationPreparationText = "";
    }
}
