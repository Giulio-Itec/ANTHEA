using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Modules.RetainingWall;
using X.Desktop.Services;
using X.Desktop.Views.RetainingWall;

namespace X.Desktop;

/// <summary>
/// F5.9: the probes of ui/f3-scheduling on the retaining walls with the MVVM view (the Release since the switch of 23/10/2026), in place of
/// RetainingWallWorkspace.CheckF3Scheduling and CheckParameterRevision (archived with the historic view in supporto/SUPERATI/ui-storica/muri):
/// the sheet opened by the factory of the bench in MvvmSheetEditor, the edits through its geometry form, the window of the values shown by
/// the dialog service of the bench off screen and driven through its view (RetainingWallParametersView). Same barriers and the same
/// expectations as the historic probes.
/// </summary>
internal static class F3WallMvvmSchedulingChecks
{
    internal static async Task<int> CheckF3Scheduling()
    {
        using var service = new GatedModuleService(RetainingWall.Module);
        var editor = ModuleHostBench.SheetEditors().Open(RetainingWall.Module, RetainingWall.Defaults(), service);
        var sheet = (RetainingWallSheetViewModel)((MvvmSheetEditor)editor).Sheet;
        try
        {
            return await F3SchedulingProbe.Run(service, () => sheet.RecalculateAsync(),
                value => { sheet.Forms["geometry"].Set("height", value.ToString(System.Globalization.CultureInfo.InvariantCulture)); sheet.Forms["geometry"].Commit(); },
                () => sheet.Calculation?.Input["geometry"].D("height"), () => editor.Busy, editor.Dispose, 3.2, 3.4);
        }
        finally { editor.Dispose(); }
    }

    /// <summary>
    /// CheckParameterRevision on the MVVM dialog: «Applica e ricalcola» while the engine checks the values, then the sheet edited (the values
    /// are not applied and the window explains why) or the window closed (nothing applied).
    /// </summary>
    internal static async Task<int> CheckParameterRevision()
    {
        int checks = 0;
        void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); checks++; }
        using var service = new GatedModuleService(RetainingWall.Module); service.Release();
        var dialogs = new OffScreenDialogs();
        var bench = ModuleHostBench.Services();
        var services = new Anthea.Presentation.Services.PresentationServices(bench.Messages, bench.Confirmations, bench.Files, dialogs, bench.Clipboard, bench.Links, bench.Wiki);
        var factory = new SheetEditorFactory(ModuleHostBench.Implementations(Environment.GetCommandLineArgs()), DesktopComposition.CreateModules(), services,
            new Anthea.Presentation.Calculation.CalculationLimiter(Anthea.Presentation.Calculation.CalculationLimiter.DefaultMaximum));
        using var editor = factory.Open(RetainingWall.Module, RetainingWall.Defaults(), service);
        var sheet = (RetainingWallSheetViewModel)((MvvmSheetEditor)editor).Sheet;
        await sheet.RecalculateAsync();
        async Task Attempt(bool close)
        {
            // «Valori di calcolo…» calculates first (as the historic button), then shows the window; the engine is armed for the check.
            var shown = dialogs.Next();
            var showing = sheet.ShowParametersAsync();
            await shown.WaitAsync(TimeSpan.FromSeconds(15));
            var window = dialogs.Window ?? throw new InvalidOperationException("Finestra dei valori non mostrata");
            service.Arm();
            try
            {
                var view = Ui.Descendants<RetainingWallParametersView>(window).Single();
                var row = Ui.Descendants<WallInputGrid>(view).Single().Items.Cast<JsonRowViewModel>().Single(r => r.Values.S("path") == "geometry/height");
                row["value"] = "3.3";
                var apply = Ui.Descendants<Button>(view).Single(b => b.Content?.ToString() == "Applica e ricalcola");
                typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(apply, null);
                var applying = dialogs.Dialog!.Applying;
                await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(15));
                if (close) window.Close();
                else { sheet.Forms["geometry"].Set("height", "3.7"); sheet.Forms["geometry"].Commit(); }
                string expected = sheet.Data.ToJsonString();
                service.Release(); await applying.WaitAsync(TimeSpan.FromSeconds(20));
                Check(sheet.Data.ToJsonString() == expected, close ? "Parametri applicati dopo chiusura" : "Parametri sovrascrivono la revisione corrente");
                if (!close)
                {
                    Check(Ui.Descendants<StackPanel>(view).First(p => p.Name == "Body").IsEnabled, "Finestra non recupera dal conflitto di revisione");
                    Check(Ui.Descendants<TextBlock>(view).Any(t => t.Text.Contains("Il foglio è cambiato")), "Conflitto non spiegato all’utente");
                }
            }
            finally { service.Release(); window.Close(); await showing.WaitAsync(TimeSpan.FromSeconds(20)); }
            await sheet.RecalculateAsync();
        }
        await Attempt(false); await Attempt(true);
        return checks;
    }

    /// <summary>The dialog of the values shown as WpfDialogService shows it, outside the screen and without waiting for it to close.</summary>
    private sealed class OffScreenDialogs : Anthea.Presentation.Services.IDialogService
    {
        private TaskCompletionSource shown = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Window? Window { get; private set; }
        internal RetainingWallParametersViewModel? Dialog { get; private set; }

        /// <summary>Completed when the next dialog is shown.</summary>
        internal Task Next() { shown = new(TaskCreationOptions.RunContinuationsAsynchronously); return shown.Task; }

        public Task<TResult?> ShowDialogAsync<TResult>(Anthea.Presentation.Services.IDialogViewModel<TResult> dialog)
        {
            var window = new Window
            {
                Title = dialog.Title, Content = dialog, SizeToContent = SizeToContent.WidthAndHeight, Left = -32000, Top = -32000,
                WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false
            };
            var done = new TaskCompletionSource<TResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Close(object? sender, EventArgs e) => window.Close();
            dialog.CloseRequested += Close;
            window.Closed += (_, _) => { dialog.CloseRequested -= Close; done.TrySetResult(dialog.Result); };
            Window = window; Dialog = dialog as RetainingWallParametersViewModel;
            window.Show(); window.UpdateLayout();
            shown.TrySetResult();
            return done.Task;
        }
    }
}
