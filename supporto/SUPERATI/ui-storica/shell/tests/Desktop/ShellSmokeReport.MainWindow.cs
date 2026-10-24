// Archived with MainWindow (F5.13 g3): fragment of tests/Desktop/ShellSmokeReport.cs at refactoring/f5 3e6c6f77, the partial of MainWindow.
public sealed partial class MainWindow
{
    /// <summary>
    /// F5.13 e (UiTests): the report of a section as MainWindow writes it (ExportSectionReport after its dialogs: the snapshot of the section and
    /// GenerateSectionReport of ProjectReport.cs, unchanged), for the comparison with the report of the new shell.
    /// </summary>
    internal static Task<int> SectionReportForChecks(JsonObject section, string path) =>
        GenerateSectionReport(SectionReportSnapshot(section), path, _ => { }, CancellationToken.None);
}
