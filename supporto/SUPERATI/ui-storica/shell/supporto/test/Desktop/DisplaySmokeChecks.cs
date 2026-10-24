using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeDisplay(string directory)
    {
        Directory.CreateDirectory(directory);
        var cases = SmokeTestData.LoadCases();
        var dpi = VisualTreeHelper.GetDpi(this);
        var log = new List<string> { $"Scala effettiva Windows: {dpi.DpiScaleX * 100:0}% × {dpi.DpiScaleY * 100:0}%" };
        foreach (var (width, height) in new[] { (1600, 990), (1280, 720), (960, 640), (760, 480) })
        {
            Width = width; Height = height;
            foreach (string page in new[] { "home", "moduli", "geo_palo_verticale", "geo_micropalo_verticale" })
            {
                if (page == "home") ShowHome();
                else if (page == "moduli") ShowModules();
                else
                {
                    string caseName = page == "geo_palo_verticale" ? "palo_storico_0" : "micropalo_IRS_45_Feld";
                    document = Archivio.Documento(page);
                    document["dati"] = cases.First(c => c.S("nome") == caseName)!["input"]!.DeepClone();
                    ShowSheet(document); await editor!.CalculateAsync();
                    if (editor.Result is null || editor.Result.S("errore") != "") throw new Exception("Calcolo fallito: " + page);
                }
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                UpdateLayout();
                var root = (FrameworkElement)Content;
                if (!double.IsFinite(root.ActualWidth) || root.ActualWidth <= 0 || root.ActualHeight <= 0)
                    throw new Exception("Area di lavoro non valida: " + page);
                if (page.StartsWith("geo_"))
                {
                    openSheet.VerifyDisplayLayout();
                    var position = editor!.View.TranslatePoint(new Point(), root);
                    if (position.X < 0 || position.Y < 0 || position.X + editor.View.ActualWidth > root.ActualWidth + 1 || position.Y + editor.View.ActualHeight > root.ActualHeight + 1)
                        throw new Exception("Il modulo esce dalla finestra visibile");
                    await openSheet.VerifyDisplayScrolling(directory, $"{page}_{width}");
                    if (width == 1600)
                    {
                        string checksDirectory = Path.Combine(directory, page); Directory.CreateDirectory(checksDirectory);
                        await openSheet.VerifyStratigraphyEditing(); openSheet.VerifyPileOutcomes(); await openSheet.VerifyPresentation(checksDirectory);
                        openSheet.VerifyReport(checksDirectory);
                        var retainedEditor = editor;
                        await openSheet.VerifyNavigationThrough(() => { Commit(); ShowHome(); }, () =>
                            Ui.Descendants<Button>(dashboardBody).First(b => b.Content is string text && text.StartsWith("Riprendi ·")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                        await openSheet.VerifyNavigationThrough(() => { Commit(); ShowHome(); ShowModules(); }, () =>
                            Ui.Descendants<Button>(dashboardBody).First(b => Equals(b.Content, "Riprendi")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                        if (!ReferenceEquals(editor, retainedEditor) || body.Content != moduleView) throw new Exception("Riprendi ha ricreato il modulo");
                    }
                }
                File.WriteAllBytes(Path.Combine(directory, $"{page}_{width}.png"), Ui.Snapshot(this));
            }
            log.Add($"{width} × {height} unità logiche: modulo contenuto nella finestra, nessuno scorrimento orizzontale esterno, ultimo pannello raggiungibile, espansione e ripristino OK");
        }
        File.WriteAllLines(Path.Combine(directory, "display.txt"), log);
    }
}
