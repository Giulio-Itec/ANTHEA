namespace Anthea.Presentation.Modules;

/// <summary>
/// The outcome of the verifications of the current results of a sheet (docs/sviluppo/progetto-sistema.md, step 4; decision of the user of
/// 4/11/2026: only the modules that already have a uniform outcome declare it, the concrete section and the composite section; the others
/// are «calcolato» in the tree, and are added one at a time with the rules of the user).
/// </summary>
public interface ISheetOutcome
{
    /// <summary>True when every verification passes, false when one fails, null without an outcome (no actions, or incomplete).</summary>
    bool? Passed { get; }
}
