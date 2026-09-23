using System.IO;
using System.Windows.Controls;
using System.Windows.Threading;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task SmokeMaterials(string directory)
    {
        testing = true; Directory.CreateDirectory(directory);
        ShowModules("Materiali");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout();
        File.WriteAllBytes(Path.Combine(directory, "materiali-catalogo.png"), Ui.Snapshot(this));
        var open = Ui.Descendants<Button>(dashboardBody).Single(b => b.Content?.ToString() == "Apri");
        open.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));
        var active = materialWindow ?? throw new Exception("La scheda materiali non si apre dal catalogo");
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        active.Check();
        OpenMaterials();
        if (!ReferenceEquals(active, materialWindow)) throw new Exception("Apertura duplicata della scheda materiali");
        File.WriteAllBytes(Path.Combine(directory, "materiali-scheda.png"), Ui.Snapshot(active));
        active.Close();
        if (materialWindow is not null) throw new Exception("Chiusura scheda materiali non gestita");
        File.WriteAllText(Path.Combine(directory, "smoke.txt"), "OK: filtro Materiali, apertura dal catalogo, controlli numerici e UI originali, riattivazione senza duplicati e chiusura.");
    }
}
