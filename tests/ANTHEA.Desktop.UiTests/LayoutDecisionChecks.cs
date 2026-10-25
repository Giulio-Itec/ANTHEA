using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Desktop.Services;
using X.Desktop.Views.ConcreteSection;
using X.Desktop.Views.RetainingWall;
using X.Desktop.Views.Stratigraphy;

namespace X.Desktop;

/// <summary>
/// Layout decisions of F5.15, step d (decisions of 25/10). The dialogs of WpfDialogService (family 5, <see cref="DialogSizing"/>): the
/// window opens at the size of the view, then the view follows the window; the minimum is the one the view declares, otherwise the
/// opening size. The columns of the RC tables are never narrower than their header (V-c). Windows outside the screen, never activated.
/// Writes test.txt and esito.txt («PASS n controlli»).
/// </summary>
internal static class LayoutDecisionChecks
{
    internal static async Task Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var log = new List<string>(); int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); log.Add("OK " + message); checks++; }
        bool Near(double a, double b) => Math.Abs(a - b) < 0.5;
        try
        {
            // A view with a declared minimum, as the values of the retaining walls.
            var declared = new Border { Width = 400, Height = 300, MinWidth = 200, MinHeight = 150 };
            var window = await Show(declared);
            double chromeWidth = window.ActualWidth - declared.ActualWidth, chromeHeight = window.ActualHeight - declared.ActualHeight;
            Check(window.SizeToContent == SizeToContent.Manual, "La finestra non segue più il contenuto dopo l'apertura");
            Check(double.IsNaN(declared.Width) && double.IsNaN(declared.Height), "La vista non ha più una misura fissa");
            Check(Near(declared.ActualWidth, 400) && Near(declared.ActualHeight, 300), "La finestra si apre con la misura della vista");
            Check(Near(window.MinWidth, 200 + chromeWidth) && Near(window.MinHeight, 150 + chromeHeight), "Minimo della finestra dal minimo dichiarato dalla vista");
            Check(window.GetValue(DisplayAdaptation.MinimumSizeProperty) is Size requested && Near(requested.Width, window.MinWidth),
                "Il minimo passa all'adattamento allo schermo");
            window.Width += 200; window.Height += 100;
            await Settle();
            Check(Near(declared.ActualWidth, 600) && Near(declared.ActualHeight, 400), "La vista segue la finestra allargata");
            window.Width = window.MinWidth; window.Height = window.MinHeight;
            await Settle();
            Check(Near(declared.ActualWidth, 200) && Near(declared.ActualHeight, 150), "La vista scende fino al minimo dichiarato");
            window.Close();

            // A view without a minimum: the opening size is the minimum.
            var plain = new Border { Width = 414, Height = 171 };
            window = await Show(plain);
            chromeWidth = window.ActualWidth - plain.ActualWidth; chromeHeight = window.ActualHeight - plain.ActualHeight;
            Check(Near(plain.MinWidth, 414) && Near(plain.MinHeight, 171), "Senza minimo dichiarato il minimo è la misura di apertura");
            Check(Near(window.MinWidth, 414 + chromeWidth) && Near(window.MinHeight, 171 + chromeHeight), "Minimo della finestra uguale alla misura di apertura");
            window.Close();

            // The historic minimums declared by the views (decision: 780×530 for the walls, 850×600 for the AI import).
            var walls = new RetainingWallParametersView();
            Check(walls.Width == 1134 && walls.Height == 741 && walls.MinWidth == 780 && walls.MinHeight == 530, "Valori dei muri: 1134×741, minimo 780×530");
            var import = new StratigraphyImportDialogView();
            Check(import.Width == 1184 && import.Height == 811 && import.MinWidth == 850 && import.MinHeight == 600, "Importazione IA: 1184×811, minimo 850×600");

            // V-c: in a narrow table that shares its width, the long headers of the shear keep their width; a short one keeps the minimum 65.
            var grid = new ConcreteTableGrid { Width = 300 };
            var shear = new DataGridTextColumn { Header = "VRd,x [kN]", SortMemberPath = "VRdx" };
            var moment = new DataGridTextColumn { Header = "Mx [kNm]", SortMemberPath = "Mx" };
            var name = new DataGridTextColumn { Header = "N", SortMemberPath = "N" };
            foreach (var column in new[] { shear, moment, name }) grid.Columns.Add(column);
            grid.StretchColumns = true;
            Check(shear.MinWidth > 65 && moment.MinWidth > 65, $"Le intestazioni lunghe allargano il minimo delle colonne: {shear.MinWidth:0} e {moment.MinWidth:0}");
            Check(shear.MinWidth > moment.MinWidth, "Il minimo segue la lunghezza dell'intestazione");
            Check(name.MinWidth == 65, "Un'intestazione corta resta al minimo storico di 65");
            log.Add($"PASS {checks} controlli");
            File.WriteAllText(Path.Combine(directory, "esito.txt"), $"PASS {checks} controlli: dialoghi ridimensionabili con il minimo della vista, intestazioni intere\r\n");
        }
        finally
        {
            File.WriteAllLines(Path.Combine(directory, "test.txt"), log);
        }
    }

    // As WpfDialogService builds the window, without the owner, outside the screen and not activated.
    private static async Task<Window> Show(FrameworkElement view)
    {
        var window = new Window
        {
            Content = view, SizeToContent = SizeToContent.WidthAndHeight, ShowInTaskbar = false, ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000
        };
        DialogSizing.Attach(window);
        window.Show();
        await Settle();
        return window;
    }

    private static async Task Settle()
    {
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }
}
