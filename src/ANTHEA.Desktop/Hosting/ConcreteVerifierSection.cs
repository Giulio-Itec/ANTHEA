using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Modules.HorizontalPile.Elastic;

namespace X.Desktop;

/// <summary>
/// The RC section of the c.a. verifier of the elastic pile (PileConcreteVerifierView, since the switch of 23/10/2026 in place of the historic
/// ConcreteWorkspace): the MVVM editor of the module of the RC section, as the composition root registers it (DesktopComposition.CreateModules, with
/// <see cref="ConcreteSectionFigureRenderer"/>), on a working copy of the sheet of the segment, with the services of the sheet of the pile, the
/// calculation service of the module and a limiter of its own (the historic workspace scheduled its calculations by itself). Here and not in the view:
/// the views hold no calculation service (rule P6).
/// </summary>
internal static class ConcreteVerifierSection
{
    internal static MvvmSheetEditor Open(PileConcreteVerifier verifier) =>
        new(new ConcreteSectionSheetModule(new ConcreteSectionFigureRenderer()), verifier.Sheet, verifier.Services,
            ModuleServices.Get(PileConcreteVerifier.ConcreteModule), new CalculationLimiter(CalculationLimiter.DefaultMaximum));
}
