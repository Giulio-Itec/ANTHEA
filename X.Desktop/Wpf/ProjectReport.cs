using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private static JsonObject SectionReportSnapshot(JsonObject section)
    {
        var top = ProjectSharedData.Ancestors(section).LastOrDefault() ?? section;
        int index = ProjectSharedData.Sections(top).ToList().IndexOf(section);
        return ProjectSharedData.Sections((JsonObject)top.DeepClone()).ElementAt(index);
    }

    private void ExportSectionReport(JsonObject section)
    {
        Commit();
        if (!ProjectSharedData.SubtreeSheets(section).Any()) return;
        var differences = ProjectSharedData.Differences(section);
        var notices = SectionReportWarnings(section);
        if (differences.Count > 0 || notices.Length > 0)
        {
            var panel = Ui.Stack(Ui.Text("Controllo prima del report", 22, true),
                Ui.Text("Il report comprende tutte le schede della sezione e delle sottosezioni. Le differenze vengono conservate e segnalate nel documento; nessun valore viene uniformato automaticamente.", 13));
            panel.Margin = new Thickness(20);
            foreach (var group in differences.GroupBy(d => d.Key))
            {
                panel.Children.Add(Ui.Text(SharedFieldLabel(group.Key), 15, true));
                foreach (var sheet in group.SelectMany(d => new[] { d.First, d.Second }).Distinct())
                    panel.Children.Add(Ui.Text(ProjectSharedData.Location(sheet) + " › " + sheet.S("nome") + ": " + ProjectSharedData.Text(ProjectSharedData.Fields(sheet)[group.Key].Value), 12));
            }
            if (notices.Length > 0) { panel.Children.Add(Ui.Text("Avvisi", 16, true)); foreach (string warning in notices) panel.Children.Add(Ui.Text(warning, 12)); }
            var dialog = Ui.Dialog(this, "Report · " + section.S("nome"), new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 780, 560);
            bool compare = false;
            panel.Children.Add(Ui.Bar(Ui.Button("Apri confronto", () => { compare = true; dialog.DialogResult = false; }),
                Ui.Button("Genera con segnalazioni", () => dialog.DialogResult = true, true), Ui.Button("Annulla", () => dialog.DialogResult = false)));
            if (dialog.ShowDialog() != true) { if (compare) ShowCoherence(section); return; }
        }
        var name = string.Concat(section.S("nome", "Sezione").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var save = new SaveFileDialog { Filter = "Documento Word|*.docx", FileName = "Relazione " + name + ".docx" };
        if (save.ShowDialog(this) != true) return;
        var snapshot = SectionReportSnapshot(section);
        var status = Ui.Text("Preparazione delle schede…", 14);
        using var cancellation = new CancellationTokenSource(); bool finished = false; Exception? failure = null; int unavailable = 0;
        var progress = Ui.Dialog(this, "Generazione del report", new Grid(), 580, 190);
        var cancel = Ui.Button("Annulla", () => cancellation.Cancel());
        var panelProgress = Ui.Stack(status, new ProgressBar { IsIndeterminate = true, Height = 5, Margin = new Thickness(0, 12, 0, 12) }, cancel);
        panelProgress.Margin = new Thickness(22); progress.Content = panelProgress;
        cancellation.Token.Register(() => { cancel.IsEnabled = false; status.Text = "Annullamento al termine del calcolo in corso…"; });
        progress.Closing += (_, e) => { if (!finished) { cancellation.Cancel(); e.Cancel = true; } };
        progress.ContentRendered += Run;
        async void Run(object? sender, EventArgs args)
        {
            progress.ContentRendered -= Run;
            try { unavailable = await GenerateSectionReport(snapshot, save.FileName, text => status.Text = text, cancellation.Token); }
            catch (Exception ex) { failure = ex; }
            finally { finished = true; progress.Close(); }
        }
        progress.ShowDialog();
        if (failure is OperationCanceledException) return;
        if (failure is not null) throw failure;
        MessageBox.Show(this, "Report salvato in:\n" + save.FileName + (unavailable > 0 ? $"\n\n{unavailable} schede contengono risultati mancanti o incompleti, segnalati nel documento." : ""), "ANTHEA", MessageBoxButton.OK, unavailable > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private static string[] SectionReportWarnings(JsonObject section) => ProjectSharedData.Limitations(section)
        .Concat(CoverChecks(section).Where(c => c.Passed != true).Select(c => c.Text)).Distinct().ToArray();

    private static async Task<int> GenerateSectionReport(JsonObject snapshot, string filename, Action<string> progress, CancellationToken cancellation)
    {
        var sheets = ProjectSharedData.SubtreeSheets(snapshot).ToArray();
        if (sheets.Length == 0) throw new ArgumentException("La sezione non contiene schede.");
        var reports = new Dictionary<JsonObject, ReportProject.SheetContent>();
        for (int i = 0; i < sheets.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested(); var sheet = sheets[i]; string module = sheet.S("modulo_id");
            progress($"Scheda {i + 1} di {sheets.Length}: {sheet.S("nome")}\nAggiornamento dei calcoli e preparazione del report…");
            await Dispatcher.Yield(DispatcherPriority.Background);
            try
            {
                using var reportEditor = new SheetEditor(module, sheet["dati"] as JsonObject ?? Archivio.NuovoFoglio(module));
                await reportEditor.CalculateAsync(); reportEditor.Commit();
                progress($"Scheda {i + 1} di {sheets.Length}: {sheet.S("nome")}\nCalcolo completato, composizione del capitolo…");
                sheet["dati"] = reportEditor.Data.DeepClone(); cancellation.ThrowIfCancellationRequested();
                if (module is RebarMaterial.Module or "mat_calcestruzzo")
                { reports[sheet] = reportEditor.MaterialReportContent(); continue; }
                if (!reportEditor.HasResults) throw new ArgumentException("Dati incompleti o calcolo non riuscito. Aprire la scheda per correggere gli input.");
                string? error = reportEditor.Result?["errori_calcolo"] is JsonObject errors && errors.Count > 0 ? string.Join("; ", errors.Select(p => p.Key + ": " + p.Value)) : null;
                var options = module == BridgeSection.Module ? ReportBridge.DefaultSections() : module == "str_palo" ? ReportConcrete.Sections.Where(s => s.Key is not ("dettagli" or "sle_tutte")).Select(s => s.Key).ToHashSet() :
                    ReportWord.Sezioni.Where(s => s.Key != "dettagli").Select(s => s.Key).ToHashSet();
                var bytes = reportEditor.BuildReport(sheet.S("nome"), options, projectReport: true);
                progress($"Scheda {i + 1} di {sheets.Length}: capitolo pronto.");
                reports[sheet] = new(bytes, error);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not IOException && ex is not UnauthorizedAccessException)
            { reports[sheet] = new(Error: ex.Message); }
        }
        cancellation.ThrowIfCancellationRequested(); progress("Unione dei dati comuni e impaginazione…");
        var plan = new ProjectReportPlan(snapshot); var warnings = SectionReportWarnings(snapshot);
        await Task.Run(() => { cancellation.ThrowIfCancellationRequested(); ReportProject.Write(filename, plan, reports, warnings, cancellation); }, cancellation);
        return reports.Values.Count(r => r.Error is not null);
    }
}
