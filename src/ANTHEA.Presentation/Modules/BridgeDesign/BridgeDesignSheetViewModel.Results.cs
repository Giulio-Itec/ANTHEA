using System.Text.Json.Nodes;
using Anthea.Application.BridgeDesign;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.BridgeDesign;

public sealed partial class BridgeDesignSheetViewModel
{
    private IReadOnlyList<BridgeDesignItem>? quantities, details, comparison, advice, technical;
    private JsonObject? comparedA;
    private readonly List<IRelayCommand> alternativeCommands = [];

    /// <summary>The panel «Quantità e costi», null without a result.</summary>
    public IReadOnlyList<BridgeDesignItem>? Quantities { get => quantities; private set => SetProperty(ref quantities, value); }

    /// <summary>The panel «Dettagli del predimensionamento», null without a result.</summary>
    public IReadOnlyList<BridgeDesignItem>? Details { get => details; private set => SetProperty(ref details, value); }

    /// <summary>The panel «Confronto A / B», null without a result.</summary>
    public IReadOnlyList<BridgeDesignItem>? Comparison { get => comparison; private set => SetProperty(ref comparison, value); }

    /// <summary>«Alternative per ridurre le quantità e le emissioni» under the results, null without a result.</summary>
    public IReadOnlyList<BridgeDesignItem>? Advice { get => advice; private set => SetProperty(ref advice, value); }

    /// <summary>The panel «Sezioni e quote», null without a result.</summary>
    public IReadOnlyList<BridgeDesignItem>? Technical { get => technical; private set => SetProperty(ref technical, value); }

    /// <summary>«Ripristina A».</summary>
    public IRelayCommand RestoreACommand { get; private set; } = null!;
    /// <summary>«Rimuovi confronto».</summary>
    public IRelayCommand RemoveComparisonCommand { get; private set; } = null!;
    /// <summary>«Ripristina fattori ambientali ordinari».</summary>
    public IRelayCommand RestoreEnvironmentCommand { get; private set; } = null!;
    /// <summary>«Esporta sezioni e quote CSV».</summary>
    public IRelayCommand ExportTechnicalCommand { get; private set; } = null!;

    private void ShowQuantities(BridgeDesignModel.Result r) => Quantities =
    [
        new BridgeDesignTable(BridgeDesignPresentation.QuantityColumns, BridgeDesignPresentation.QuantityRows(r)),
        new BridgeDesignText(BridgeDesignPresentation.CostSummary(Data, r), BridgeDesignTextRole.Highlight)
    ];

    private void ShowDetails(BridgeDesignModel.Result r)
    {
        var items = new List<BridgeDesignItem>
        {
            new BridgeDesignText(BridgeDesignModel.Scope, BridgeDesignTextRole.Highlight),
            new BridgeDesignText(BridgeDesignPresentation.DetailScope(r), BridgeDesignTextRole.Note),
            new BridgeDesignMomentChart(r),
            new BridgeDesignTable(BridgeDesignPresentation.DetailColumns, BridgeDesignPresentation.DetailRows(r)),
            new BridgeDesignTable(BridgeDesignPresentation.SupportColumns, BridgeDesignPresentation.SupportRows(r))
        };
        foreach (string warning in r.Warnings) items.Add(new BridgeDesignText(BridgeDesignPresentation.Warning(warning), BridgeDesignTextRole.Warning));
        items.Add(new BridgeDesignText(BridgeDesignPresentation.Exclusions, BridgeDesignTextRole.Note));
        Details = items;
    }

    // The comparison with the alternative A, as ShowComparison of the historic view: «Ripristina A» restores the alternative A pinned when
    // the comparison was shown.
    private void ShowComparison()
    {
        comparedA = null; RestoreACommand.NotifyCanExecuteChanged(); RemoveComparisonCommand.NotifyCanExecuteChanged();
        if (Data["alternative_a"] is not JsonObject a || calculationResult is null)
        { Comparison = [new BridgeDesignText(BridgeDesignPresentation.ComparisonPlaceholder)]; return; }
        try
        {
            if (analysis?.ComparisonError is { } error) throw new ArgumentException(error);
            var old = analysis?.Comparison ?? throw new ArgumentException(BridgeDesignPresentation.ComparisonUnavailable); var r = calculationResult;
            comparedA = a;
            Comparison =
            [
                new BridgeDesignText(BridgeDesignPresentation.ComparisonTitle(old, r), BridgeDesignTextRole.Highlight),
                new BridgeDesignTable(BridgeDesignPresentation.ComparisonColumns, BridgeDesignPresentation.ComparisonRows(old, r)),
                new BridgeDesignButton(BridgeDesignPresentation.RestoreAButton, RestoreACommand),
                new BridgeDesignButton(BridgeDesignPresentation.RemoveComparisonButton, RemoveComparisonCommand)
            ];
        }
        catch (ArgumentException ex) { Comparison = [new BridgeDesignText(BridgeDesignPresentation.ComparisonError(ex.Message), BridgeDesignTextRole.Error)]; }
        RestoreACommand.NotifyCanExecuteChanged(); RemoveComparisonCommand.NotifyCanExecuteChanged();
    }

    private void ShowAdvice()
    {
        alternativeCommands.Clear();
        var items = new List<BridgeDesignItem> { new BridgeDesignText(BridgeDesignPresentation.AdviceTitle, BridgeDesignTextRole.Title) };
        foreach (var alternative in analysis!.Alternatives)
        {
            var command = new RelayCommand(() => ApplyAlternative(alternative), () => CanEdit);
            alternativeCommands.Add(command);
            items.Add(new BridgeDesignButton(BridgeDesignPresentation.AlternativeCaption(alternative), command));
        }
        items.Add(new BridgeDesignText(BridgeDesignPresentation.AdviceNote, BridgeDesignTextRole.Note));
        if (BridgeDesignPresentation.EnvironmentRestorable(Input)) items.Add(new BridgeDesignButton(BridgeDesignPresentation.RestoreEnvironmentButton, RestoreEnvironmentCommand));
        Advice = items;
    }

    private void ShowTechnical(BridgeDesignModel.Result r) => Technical =
    [
        new BridgeDesignText(BridgeDesignPresentation.TechnicalTitle(r), BridgeDesignTextRole.Title),
        new BridgeDesignText(BridgeDesignPresentation.TechnicalNote, BridgeDesignTextRole.Note),
        new BridgeDesignTable(BridgeDesignPresentation.TechnicalColumns, BridgeDesignPresentation.TechnicalRows(Data, r)),
        new BridgeDesignText(BridgeDesignPresentation.SpanTitle, BridgeDesignTextRole.Title),
        new BridgeDesignTable(BridgeDesignPresentation.SpanColumns, BridgeDesignPresentation.SpanRows(r)),
        new BridgeDesignText(BridgeDesignPresentation.SupportTitle, BridgeDesignTextRole.Title),
        new BridgeDesignText(BridgeDesignPresentation.SupportNote, BridgeDesignTextRole.Note),
        new BridgeDesignTable(BridgeDesignPresentation.SupportScheduleColumns, BridgeDesignPresentation.SupportScheduleRows(Data, r)),
        new BridgeDesignButton(BridgeDesignPresentation.TechnicalCsvButton, ExportTechnicalCommand)
    ];

    /// <summary>«Ripristina A»: the data become those of the alternative A shown in the comparison (which stays pinned), the tabs rebuilt.</summary>
    public void RestoreA()
    {
        if (!CanEdit || comparedA is null) return;
        var copy = (JsonObject)comparedA.DeepClone(); var pinned = Data["alternative_a"]?.DeepClone();
        Mutate(() => BridgeDesignPresentation.Restore(Data, copy, pinned), true);
    }

    /// <summary>«Rimuovi confronto».</summary>
    public void RemoveComparison() { if (CanEdit && comparedA is not null) Mutate(() => Data.Remove("alternative_a")); }

    /// <summary>The button of an alternative: its changes of the inputs, the tabs rebuilt.</summary>
    public void ApplyAlternative(BridgeDesignAlternative alternative) { if (CanEdit) Mutate(() => BridgeDesignPresentation.ApplyAlternative(Input, alternative), true); }

    /// <summary>«Ripristina fattori ambientali ordinari».</summary>
    public void RestoreEnvironment() { if (CanEdit) Mutate(() => BridgeDesignPresentation.RestoreEnvironment(Input)); }
}
