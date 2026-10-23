internal sealed partial class SheetEditor
{
    internal Task CalculateCurvatureForReportCheck() => concrete!.CalculateCurvatureForReportCheck();
}
internal sealed partial class ConcreteWorkspace
{
    internal async Task CalculateCurvatureForReportCheck()
    {
        await CalculateCurvature();
        if (curvatureResult is null) throw new InvalidOperationException("Curva della fixture non disponibile: " + curvatureText.Text);
    }
}
