namespace Materiali;
public sealed partial class MaterialView
{
    public IReadOnlyList<PropertyRow> ReportProperties() { Refresh(); return presentation!.ReportProperties(); }
}
