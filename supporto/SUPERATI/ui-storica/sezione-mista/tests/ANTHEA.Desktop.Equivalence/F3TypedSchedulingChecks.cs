namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    internal static async Task<int> CheckF3Scheduling()
    {
        using var service = new GatedModuleService(BridgeSection.Module);
        using var view = new BridgeWorkspace(BridgeSection.Defaults(), service);
        var form = view.inputForms.Single(f => f.Editors.ContainsKey("h_trave"));
        return await F3SchedulingProbe.Run(service, () => view.CalculateAsync(),
            value => { form.Set("h_trave", value.ToString(System.Globalization.CultureInfo.InvariantCulture)); form.Commit(); },
            () => view.Calculation?.Input.D("h_trave"), () => view.Busy, view.Dispose, 1955, 2055);
    }
}
