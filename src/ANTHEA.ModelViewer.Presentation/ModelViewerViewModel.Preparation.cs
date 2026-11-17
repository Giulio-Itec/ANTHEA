using ANTHEA.ModelWorkspace;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ANTHEA.ModelViewer.Presentation;

/// <summary>
/// Rilievo R7 (16/11/2026, proposal of the user, approved): the base cases of GPC are prepared in the background as soon as the model is on
/// screen, not only when the catalogue of the combinations opens (refactoring of 29/10/2026): on the MIDAS model of 16/11 the preparation is
/// 25 of the 27 s of the first combination. It starts once for each copy opened or imported, after the view says the scene is ready
/// (<see cref="SceneReady"/>) and as soon as the viewer is free (an import, a verification, a report or a revision ended: P1 of the review of
/// 17/11/2026), only when there are combinations to calculate, below normal priority, and not when the computer has less than
/// <see cref="BackgroundPreparationMemory"/> of free memory; a new copy cancels it. The preparation is the one of the catalogue and of the
/// calculation (GpcModelCombinationService): whichever comes first does it, the others wait for it and reuse it. With the service of the tab of
/// the model (P2 of the review) it survives the viewer: a viewer created again on the same copy finds the base cases ready, or their
/// preparation still in progress, and closing the viewer does not stop it (the tab does, when it closes).
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

    /// <summary>The text of the catalogue when the base cases of the copy are prepared.</summary>
    public const string PreparedText = "Casi base GPC pronti: la prima combinazione parte senza prepararli.";

    CancellationTokenSource? preparation;
    bool preparationPending;
    bool sceneShown;
    bool settingSnapshot;

    /// <summary>The free memory of the computer, read when the preparation would start (the checks give their own).</summary>
    public Func<long> AvailableMemory { get; init; } = SystemMemory.AvailableBytes;

    /// <summary>True while the base cases of GPC are prepared in the background.</summary>
    [ObservableProperty] bool isPreparingCombinations;

    /// <summary>What the preparation in the background did, for the catalogue; empty before it starts.</summary>
    [ObservableProperty] string combinationPreparationText = "";

    /// <summary>The catalogue shows <see cref="CombinationPreparationText"/> only when there is something to say (the layout is unchanged otherwise).</summary>
    public bool HasCombinationPreparationText => CombinationPreparationText.Length > 0;
    partial void OnCombinationPreparationTextChanged(string value) => OnPropertyChanged(nameof(HasCombinationPreparationText));

    /// <summary>The view tells that the scene of the copy is on screen (ModelViewerControl.UpdateScene, at every scene): the preparation of the
    /// base cases of the copy may start now, or as soon as the viewer is free.</summary>
    public void SceneReady()
    {
        if (disposed || Snapshot is null) return;
        sceneShown = true;
        TryStartPreparation();
    }

    /// <summary>
    /// P1 of the review of 17/11/2026: the preparation starts with the scene on screen, the copy waiting for it, the viewer free
    /// (<see cref="CanManageCombinations"/>) and its catalogue ready. Until 17/11 <see cref="SceneReady"/> consumed the waiting copy before
    /// these checks, during the import (busy, the catalogue still the one of the previous copy): after «Importa/Aggiorna modello…» the
    /// preparation never started. The copy stops waiting only when the preparation starts or when it is decided not to make it (sola lettura,
    /// nothing to calculate, little memory, already prepared).
    /// </summary>
    void TryStartPreparation()
    {
        if (!preparationPending || !sceneShown || settingSnapshot || disposed || Snapshot is not { } snapshot) return;
        if (IsReadOnly) { preparationPending = false; return; }
        if (!CanManageCombinations) return;
        preparationPending = false;
        if (!ToCalculate(Combinations).Any()) return;
        if (combinationService.IsPreparedFor(snapshot)) { CombinationPreparationText = PreparedText; return; }
        long free = AvailableMemory();
        if (free < BackgroundPreparationMemory)
        {
            CombinationPreparationText = $"Casi base GPC non preparati in anticipo: memoria libera {ModelCombinations.CopyLimit(free)}, meno di {ModelCombinations.CopyLimit(BackgroundPreparationMemory)}. Si preparano con il primo calcolo.";
            return;
        }
        // P2: with the service of the tab the preparation is not tied to this viewer (a change of tab does not stop it).
        var cancellation = ownsCombinationService ? CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token) : new CancellationTokenSource();
        preparation = cancellation;
        IsPreparingCombinations = true; CombinationPreparationText = "Preparazione dei casi base GPC in background, a bassa priorità…";
        _ = PrepareInBackgroundAsync(snapshot, cancellation);
    }

    async Task PrepareInBackgroundAsync(ModelSnapshot snapshot, CancellationTokenSource cancellation)
    {
        bool prepared = false; string? failure = null;
        try { prepared = await combinationService.PrepareAsync(snapshot, true, cancellation.Token); }
        // N1 of the review of 17/11/2026: nothing is lost in an unobserved task; the first calculation prepares again and explains.
        catch (Exception ex) when (ex is not OperationCanceledException) { failure = ex.Message; }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(preparation, cancellation))
            {
                preparation = null;
                if (!disposed)
                {
                    IsPreparingCombinations = false;
                    CombinationPreparationText = prepared ? PreparedText
                        : failure is null ? "" : "Casi base GPC non preparati in anticipo (" + failure + "). Si preparano con il primo calcolo.";
                }
            }
            cancellation.Dispose();
        }
    }

    /// <summary>A new copy on screen (an import, a copy read again): the preparation of the previous one stops; the new one waits for its scene.</summary>
    void ResetPreparation()
    {
        preparation?.Cancel(); preparation = null;
        preparationPending = true; sceneShown = false;
        if (IsPreparingCombinations) IsPreparingCombinations = false;
        CombinationPreparationText = "";
    }
}
