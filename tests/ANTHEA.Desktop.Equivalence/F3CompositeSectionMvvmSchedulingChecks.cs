using Anthea.Presentation.Modules.CompositeSection;

namespace X.Desktop;

/// <summary>
/// F5.11: the probe of ui/f3-scheduling on the composite bridge section with the MVVM view (bench <c>--module-impl str_mista_ponte=mvvm</c>), in
/// place of BridgeWorkspace.CheckF3Scheduling: the sheet opened by the factory of the bench in MvvmSheetEditor with the gated engine, «Calcola»
/// of the window (the editor commits, then calculates), the edits through the form of the welded steel of the sheet (h_trave, as the historic
/// probe through the form of the view), the result read from the sheet. Same barriers and the same expectations as the historic probe.
/// </summary>
internal static class F3CompositeSectionMvvmSchedulingChecks
{
    /// <summary>True when the bench of this process opens the composite section with the MVVM view.</summary>
    internal static bool Active => ModuleHostBench.Implementations(Environment.GetCommandLineArgs()).For(BridgeSection.Module) == SheetImplementation.Mvvm;

    internal static async Task<int> CheckF3Scheduling()
    {
        using var service = new GatedModuleService(BridgeSection.Module);
        var editor = ModuleHostBench.SheetEditors().Open(BridgeSection.Module, BridgeSection.Defaults(), service);
        var sheet = (CompositeSectionSheetViewModel)((MvvmSheetEditor)editor).Sheet;
        try
        {
            var form = sheet.Forms["carpenteria"];
            return await F3SchedulingProbe.Run(service, () => editor.CalculateAsync(),
                value => { form.Set("h_trave", value.ToString(System.Globalization.CultureInfo.InvariantCulture)); form.Commit(); },
                () => sheet.Calculation?.Input.D("h_trave"), () => editor.Busy, editor.Dispose, 1955, 2055);
        }
        finally { editor.Dispose(); }
    }
}
