// Archived with MainWindow (F5.13 g3): fragment of tests/Desktop/ShellProjectCapture.cs at refactoring/f5 3e6c6f77, the partial of MainWindow.
public sealed partial class MainWindow
{
    /// <summary>
    /// F5.13 e (UiTests): the scenario of <see cref="ShellProjectCapture"/> through MainWindow, with its methods as its controls call them (LoadFile, ShowSheet,
    /// Commit, CompleteProjectRevision, ShowCoherence with «Uniforma a questo», SwitchProjectRevision, RestoreProjectHistory, Save), off screen.
    /// </summary>
    internal static async Task CaptureProjectForChecks(string file, string folder)
    {
        var window = new MainWindow(TestServices.Create) { Left = -32000, Top = -32000, WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false };
        window.Show();
        try { await window.CaptureProject(file, folder); }
        finally { window.FinishSmoke(); window.Close(); }
    }

    private async Task CaptureProject(string file, string folder)
    {
        int step = 0;
        async Task Capture()
        {
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
            ShellProjectCapture.Write(folder, step++, new ShellProjectCapture.State(WorkingProjectDocument, document, Title, dirty, projectReadOnly, displayedRevision,
                currentSheet?.S("id"), editor?.Data, sharedStatus.Visibility == Visibility.Visible ? sharedStatus.Text : ""));
        }
        JsonObject Find(string id) => ProjectRevisions.Find(document, id)!;
        LoadFile(file);
        await Capture();
        ShowSheet(Find(ShellProjectCapture.ConcreteId));
        await Capture();
        var state = (JsonObject)editor!.PreviewData.DeepClone(); state["classe"] = "C40/50"; openSheet.SetConcreteState(state);
        Scripted.SharedUpdate = () => true;
        Commit();
        await Capture();
        ShowSheet(Find(ShellProjectCapture.PilesId)); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        openSheet.SetGeometryForSmoke("1.25");
        Commit();
        Scripted.SharedUpdate = null;
        await Capture();
        CompleteProjectRevision(RevisionScope(currentSheet) ?? Find(ShellProjectCapture.SectionId), "Prima emissione");
        await Capture();
        openSheet.SetGeometryForSmoke("1.4");
        Scripted.SharedUpdate = () => false;
        Commit();
        Scripted.SharedUpdate = null;
        await Capture();
        Exception? failure = null;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = System.Windows.Application.Current.Windows.OfType<Window>().Single(w => w != this && w.Title.StartsWith("Confronto"));
            try
            {
                Ui.Descendants<Button>(dialog).Single(b => b.Tag is ValueTuple<JsonObject, string> t && t.Item1.S("id") == ShellProjectCapture.PilesId && t.Item2 == "diameter_mm")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception error) { failure = error; }
            finally { dialog.Close(); }
        }));
        ShowCoherence(Find(ShellProjectCapture.SectionId));
        if (failure is not null) throw failure;
        await Capture();
        SwitchProjectRevision(ShellProjectCapture.SectionId, 0);
        await Capture();
        SwitchProjectRevision(ShellProjectCapture.SectionId, null);
        await Capture();
        RestoreProjectHistory(false);
        await Capture();
        RestoreProjectHistory(true);
        await Capture();
        Save(false);
        await Capture();
        ShellProjectCapture.Finish(folder, file);
    }
}
