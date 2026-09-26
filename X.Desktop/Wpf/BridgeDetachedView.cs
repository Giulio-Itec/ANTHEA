using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    private readonly ContentControl viewportHost = new();
    internal Window? detachedWindow;
    private ContentControl? detachedHost;
    internal void DetachView()
    {
        if (detachedWindow is not null) { detachedWindow.Activate(); return; }
        Pages.SelectedIndex = 1;
        if (DisplayChoice.SelectedIndex == 2) DisplayChoice.SelectedIndex = 0;
        var phase = new ComboBox { MinWidth = 260, Margin = new Thickness(8) };
        phase.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(ItemsControl.ItemsSource)) { Source = StageChoice });
        phase.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedIndexProperty,
            new Binding(nameof(StageChoice.SelectedIndex)) { Source = StageChoice, Mode = BindingMode.TwoWay });
        RevisionInspection.Allow(phase);
        var notice = Ui.Text("", 12, true, Ui.Brush("#8B5916"));
        notice.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = resultNotice });
        viewportHost.Content = Ui.Stack(Ui.Text("Vista aperta in una finestra indipendente", 14, true),
            Ui.Button("Porta in primo piano", () => detachedWindow?.Activate(), inspection: true),
            Ui.Button("Riaggancia vista", () => detachedWindow?.Close(), inspection: true));
        detachedHost = new ContentControl { Content = Viewport };
        var window = new Window { Title = "ANTHEA · Sezione da ponte · sollecitazioni e tensioni", Owner = Window.GetWindow(this),
            Width = 1400, Height = 900, MinWidth = 880, MinHeight = 550, Background = Ui.Bg, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        detachedWindow = window;
        WindowState previousState = WindowState.Normal;
        void FullScreen()
        {
            if (window.WindowStyle == WindowStyle.None)
            { window.WindowStyle = WindowStyle.SingleBorderWindow; window.ResizeMode = ResizeMode.CanResize; window.WindowState = previousState; }
            else { previousState = window.WindowState; window.WindowState = WindowState.Normal; window.WindowStyle = WindowStyle.None; window.ResizeMode = ResizeMode.NoResize; window.WindowState = WindowState.Maximized; }
        }
        window.Content = new Border { Background = Ui.Bg, Child = Ui.Dock(detachedHost, Ui.Stack(Ui.Bar(Ui.Text("Situazione", 12, true), phase,
            Ui.Button("Schermo intero · F11", FullScreen, inspection: true), Ui.Button("Riaggancia", window.Close, inspection: true)), notice)) };
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F11 || e.Key == Key.Escape && window.WindowStyle == WindowStyle.None) { FullScreen(); e.Handled = true; }
        };
        window.Closed += (_, _) =>
        {
            detachedHost.Content = null; detachedHost = null; detachedWindow = null; viewportHost.Content = Viewport;
            if (!disposed) { RefreshDrawing(); Drawing.ResetView(); }
        };
        window.Show(); RefreshDrawing(); Drawing.ResetView();
    }
}
