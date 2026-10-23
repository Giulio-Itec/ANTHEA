using Anthea.Presentation.Modules.CompositeSection;

namespace X.Desktop;

/// <summary>
/// F5.11: the probe of ui/f3-scheduling on the composite bridge section with the MVVM view, as the Release opens it since the switch of 23/10/2026,
/// in place of BridgeWorkspace.CheckF3Scheduling (archived with the historic view): the sheet opened by the factory of the bench in MvvmSheetEditor with the gated engine, «Calcola»
/// of the window (the editor commits, then calculates), the edits through the form of the welded steel of the sheet (h_trave, as the historic
/// probe through the form of the view), the result read from the sheet. Same barriers and the same expectations as the historic probe.
/// </summary>
internal static class F3CompositeSectionMvvmSchedulingChecks
{
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
