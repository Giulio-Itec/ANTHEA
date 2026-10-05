using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    internal async Task CheckAppearance(string directory)
    {
        testing = true; Directory.CreateDirectory(directory); int count = 0;
        wiki = new WikiView((_, _) => { }, new WikiProgress(persist: false));
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); count++; }
        async Task Capture(string name)
        {
            var root = (FrameworkElement)Content;
            root.Measure(new Size(1600, 1000)); root.Arrange(new Rect(0, 0, 1600, 1000)); root.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Appearance.ApplyTree(root); root.UpdateLayout();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), Ui.Snapshot(root));
        }
        var text = new TextBox { Text = "123,45" };
        var probe = Ui.Paper(text); Appearance.ApplyTree(probe);
        var light = ((SolidColorBrush)probe.Background).Color;
        var backgrounds = new List<Color>();
        var sealedStyle = new Style(typeof(Button)); sealedStyle.Setters.Add(new Setter(Control.BackgroundProperty, Appearance.Paper)); sealedStyle.Seal();
        Check(!Appearance.Paper.CanFreeze, "Palette modificabile anche dopo il blocco degli stili WPF");
        var triggered = new Button(); var triggerStyle = new Style(typeof(Button));
        triggerStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Green));
        var trigger = new Trigger { Property = IsEnabledProperty, Value = false }; trigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Red));
        triggerStyle.Triggers.Add(trigger); triggered.Style = triggerStyle;
        Appearance.ApplyTree(triggered); triggered.IsEnabled = false;
        Check(ReferenceEquals(triggered.Foreground, Brushes.Red), "Trigger di stato non sovrascritto dal tema");
        triggered.IsEnabled = true;
        Check(ReferenceEquals(triggered.Foreground, Brushes.Green), "Ripristino stile dopo cambio di stato");
        var inheritedText = new TextBlock { Text = "Testo ereditato" };
        var inheritedHost = new ContentControl { Content = inheritedText, Foreground = Brushes.Black };
        Appearance.ApplyTree(inheritedHost);
        Check(inheritedText.ReadLocalValue(TextBlock.ForegroundProperty) == DependencyProperty.UnsetValue, "Il tema non blocca il colore ereditato");
        inheritedHost.Foreground = Brushes.Red;
        Check(ReferenceEquals(inheritedText.Foreground, Brushes.Red), "Il testo segue il colore del contenitore");
        var scroller = new ScrollViewer { Width = 300, Height = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new Border { Width = 1200, Height = 2000 } };
        foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light })
        {
            Appearance.Set(mode, false); Appearance.SetDark(scroller, mode != AppAppearance.Light);
            scroller.Measure(new Size(300, 200)); scroller.Arrange(new Rect(0, 0, 300, 200)); scroller.UpdateLayout();
            Appearance.ApplyTree(scroller);
            scroller.ScrollToVerticalOffset(0); scroller.ScrollToHorizontalOffset(0);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); scroller.UpdateLayout();
            var vertical = Ui.Descendants<ScrollBar>(scroller).First(s => s.Orientation == Orientation.Vertical);
            var horizontal = Ui.Descendants<ScrollBar>(scroller).First(s => s.Orientation == Orientation.Horizontal);
            ScrollBar.LineDownCommand.Execute(null, vertical); ScrollBar.LineRightCommand.Execute(null, horizontal);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); scroller.UpdateLayout();
            Check(scroller.VerticalOffset > 0 && scroller.HorizontalOffset > 0, "Comandi scrollbar funzionanti " + mode);
            Check(vertical.ReadLocalValue(Control.ForegroundProperty) == DependencyProperty.UnsetValue, "ScrollBar mantiene eredità e stile " + mode);
        }
        foreach (var mode in Enum.GetValues<AppAppearance>())
        {
            Appearance.Set(mode, false);
            var path = Path.Combine(directory, "preference.json"); Appearance.SavePreference(path, mode);
            Check(Appearance.ReadPreference(path) == mode, "Preferenza persistita " + mode);
            Appearance.ApplyTree(probe);
            Check(text.Text == "123,45", "Input conservato " + mode);
            Check(((SolidColorBrush)probe.Background).Color == ((SolidColorBrush)Appearance.Paper).Color, "Superficie aggiornata " + mode);
            var background = ((SolidColorBrush)probe.Background).Color; backgrounds.Add(background);
            if (mode == AppAppearance.VeryDark) Check(Math.Abs(background.R - background.B) < 8, "Molto scuro usa grigi neutri");
            if (mode == AppAppearance.Dark) Check(background.B > background.R + 20, "Scuro usa toni blu");
            ShowHome(); await Capture(mode + "-home");
            ShowProjects(); await Capture(mode + "-projects");
            document = Archivio.Documento("geo_palo_verticale"); ShowSheet(document);
            await editor!.WaitForAutomatic();
            var data = editor.Data.ToJsonString(); var originalEditor = editor;
            Appearance.Set(mode == AppAppearance.Light ? AppAppearance.Dark : AppAppearance.Light, false);
            Check(ReferenceEquals(originalEditor, editor) && data == editor.Data.ToJsonString(), "Cambio aspetto non ricrea o modifica il calcolo " + mode);
            Appearance.Set(mode, false);
            await Capture(mode + "-module");
            ShowWiki("beam"); await Capture(mode + "-wiki");
            editor.Dispose(); editor = null;
        }
        Appearance.Set(AppAppearance.Light, false);
        ShowHome(); await Capture("Light-after-switches");
        foreach (var module in new[] { "str_palo", "geo_palo_orizzontale", BridgeSection.Module, RetainingWall.Module })
        {
            document = Archivio.Documento(module); ShowSheet(document);
            var active = editor!; var data = active.Data.ToJsonString();
            foreach (var mode in new[] { AppAppearance.Light, AppAppearance.Dark, AppAppearance.VeryDark, AppAppearance.Light })
            {
                Appearance.Set(mode, false);
                await Capture(module + "-" + mode);
                Check(ReferenceEquals(editor, active) && active.Data.ToJsonString() == data, "Vista e dati conservati " + module + " " + mode);
            }
            active.Dispose(); editor = null;
        }
        Check(backgrounds.Distinct().Count() == 3, "Tre palette distinte");
        Check(((SolidColorBrush)probe.Background).Color == light, "Ripristino colori chiari");
        var invalid = Path.Combine(directory, "invalid.json"); File.WriteAllText(invalid, "broken");
        Check(Appearance.ReadPreference(invalid) == AppAppearance.Light, "Preferenza danneggiata recuperata");
        File.WriteAllText(Path.Combine(directory, "completato.txt"), $"PASS {count} controlli aspetti");
    }
}
