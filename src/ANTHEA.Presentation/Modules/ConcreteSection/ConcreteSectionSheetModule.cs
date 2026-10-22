using System.Collections.Immutable;
using Anthea.Application.Modules;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The RC section (<c>str_palo</c>) as a module migrated to MVVM, F5.10 sub-step S1: identity from <see cref="ModuleDirectory"/>, guide of the
/// module, the interface state its sheets save (S1: main tab, dividers, dimensions of the drawing; the later sub-steps add theirs), the short
/// report, and its sheet, <see cref="ConcreteSheetViewModel"/> with the figures of its report (<see cref="IConcreteSectionFigureRenderer"/>, the WPF
/// drawings in the desktop, S9). Registered in the composition root of the desktop; the Release keeps the historic view until its switch (decision
/// C10), the UiTests bench chooses this module with <c>--module-impl str_palo=mvvm</c>.
/// </summary>
/// <param name="figures">Draws the figures of the report.</param>
public sealed class ConcreteSectionSheetModule(IConcreteSectionFigureRenderer figures) : ISheetModule
{
    private readonly IConcreteSectionFigureRenderer figures = figures ?? throw new ArgumentNullException(nameof(figures));

    /// <summary>The identifier of the module.</summary>
    public const string Id = "str_palo";

    /// <summary>Keys of interface state of <c>workspace_ca</c> written by the shell and the inputs (sub-step S1).</summary>
    public static ImmutableArray<string> UiStateKeys { get; } = ["tab", "layout", "quote_dimensioni", "quote_copriferro", "quote_interferro"];

    public ModuleInfo Info { get; } = ModuleDirectory.Get(Id);

    /// <summary>Read when asked: building the registry at startup does not load the Wiki catalogue.</summary>
    public ModuleHelp Help => ModuleHelp.For(Info.Id);

    public ModulePersistence Persistence { get; } = new(UiStateKeys);

    /// <summary>The report and the short report of the section (the code of the reports is frozen; the ViewModel gives them the data of the sheet, S9).</summary>
    public ModuleReportCapabilities Reports { get; } = new(true);

    public ISheetViewModel CreateSheet(SheetContext context) => new ConcreteSheetViewModel(context, figures);
}
