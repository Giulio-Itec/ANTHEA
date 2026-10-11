using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using ANTHEA.ModelViewer.Presentation;
using ANTHEA.ModelViewer.Wpf;
using HelixToolkit.Wpf.SharpDX;

static partial class Program
{
    static void SetAppearance(string mode)
    {
        var assembly = typeof(X.Desktop.MainWindow).Assembly;
        var appearance = assembly.GetType("X.Desktop.Appearance")!;
        appearance.GetMethod("Set", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
            [Enum.Parse(assembly.GetType("X.Desktop.AppAppearance")!, mode), false]);
    }
    static void WatchAppearance(Window window) => typeof(X.Desktop.MainWindow).Assembly.GetType("X.Desktop.Appearance")!
        .GetMethod("Watch", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [window]);
    static double Luminance(Color c)
    {
        double Linear(byte value) { double s = value / 255.0; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
        return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
    }
    static double Contrast(Color a, Color b) => (Math.Max(Luminance(a), Luminance(b)) + .05) / (Math.Min(Luminance(a), Luminance(b)) + .05);
    static void ThemeRenderingChecks(ModelViewerControl control, Viewport3DX scene, string output)
    {
        var state = (ModelViewerViewModel)control.DataContext;
        state.ShowInspector = false; state.SelectedPanel = 1; state.SelectedComponent = "Myy"; state.SelectedFamily = 0;
        var originalBackground = scene.BackgroundColor;
        var palette = ((ColorStripeMaterial)scene.Items.OfType<MeshGeometryModel3D>().First(m => m.Material is ColorStripeMaterial).Material!).ColorStripeX!.ToArray();
        var papers = new List<Color>();
        try
        {
            foreach (var mode in new[] { "Light", "Dark", "VeryDark" })
            {
                SetAppearance(mode); Wait(150);
                var paper = ((SolidColorBrush)Application.Current.Resources["AppearancePaper"]).Color;
                var ink = ((SolidColorBrush)Application.Current.Resources["AppearanceInk"]).Color;
                papers.Add(paper);
                Check(Contrast(paper, ink) >= 7, mode + " text/background resources have strong reading contrast");
                Check(mode == "Light" || Luminance(paper) < .1, mode + " uses the requested background brightness");
                for (int panel = 0; panel < 5; panel++)
                {
                    state.SelectedPanel = panel; Wait(150);
                    var tabs = Children<TabItem>(control).Where(t => t.Header is string s && new[] { "Modello", "Risultati", "Vista", "Verifiche", "Report" }.Contains(s)).ToArray();
                    Check(tabs.Length == 5 && tabs.All(t => t.Foreground is SolidColorBrush b && Contrast(b.Color, paper) >= 4.5), mode + " navigation remains readable in panel " + panel);
                    Capture(control, scene, Path.Combine(output, $"tema-{mode.ToLowerInvariant()}-{panel}.png"));
                }
                state.SelectedPanel = 1; Wait(80);
                var mesh=Children<CheckBox>(control).First(c=>c.Content?.ToString()=="Tabella");
                state.ShowTable=false;Wait(30);
                var face=(Border)mesh.Template.FindName("CheckFace",mesh);
                Check(face.Background is SolidColorBrush inputPaper && inputPaper.Color==paper,mode+" unchecked input follows the theme rather than the system white checkbox");
                ((IToggleProvider)new CheckBoxAutomationPeer(mesh).GetPattern(PatternInterface.Toggle)).Toggle();Wait(40);
                Check(state.ShowTable && ((System.Windows.Shapes.Path)mesh.Template.FindName("CheckMark",mesh)).Visibility==Visibility.Visible,mode+" themed checkbox retains its native toggle semantics and viewmodel binding");
                state.ShowTable=false;state.SelectedPanel=2;Wait(60);
                var expander=Children<Expander>(control).First(e=>e.Header?.ToString()=="Oggetti, colori ed etichette");
                ((IExpandCollapseProvider)new ExpanderAutomationPeer(expander).GetPattern(PatternInterface.ExpandCollapse)).Expand();Wait(40);
                Check(state.ShowDisplayOptions && ((ContentPresenter)expander.Template.FindName("ExpandSite",expander)).Visibility==Visibility.Visible,mode+" themed expander retains automation and two-way expansion state");
                expander.IsExpanded=false;state.SelectedPanel=1;Wait(40);
                var combo = Children<ComboBox>(control).First(c => System.Windows.Automation.AutomationProperties.GetName(c) == "Tipo di risultato");
                combo.IsDropDownOpen = true; Wait(100);
                var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
                Check(popup.IsOpen && popup.Child != null, mode + " result selector opens its popup");
                var item = Children<ComboBoxItem>(popup.Child!).First();
                var itemPaper = item.Background is SolidColorBrush fill && fill.Color.A == 255 ? fill.Color : paper;
                Check(item.Foreground is SolidColorBrush itemInk && Contrast(itemInk.Color, itemPaper) >= 4.5, mode + " popup text follows the app theme including the highlighted row");
                CapturePage((FrameworkElement)popup.Child!, Path.Combine(output, $"tema-{mode.ToLowerInvariant()}-menu.png"));
                combo.IsDropDownOpen = false;
                state.ShowTable = true; state.SelectedTableKind = 1; Wait(150);
                var grid = Children<DataGrid>(control).First(g => System.Windows.Automation.AutomationProperties.GetName(g) == "Tabella del modello");
                grid.SelectedIndex = 0; Wait(80);
                Check(grid.IsVisible && grid.Foreground is SolidColorBrush tableInk && Contrast(tableInk.Color, paper) >= 4.5, mode + " model table remains readable");
                Capture(control, scene, Path.Combine(output, $"tema-{mode.ToLowerInvariant()}-tabella.png"));
                state.ShowTable = false;
                var now = ((ColorStripeMaterial)scene.Items.OfType<MeshGeometryModel3D>().First(m => m.Material is ColorStripeMaterial).Material!).ColorStripeX!.ToArray();
                Check(now.SequenceEqual(palette) && scene.BackgroundColor == originalBackground, mode + " preserves engineering colors and the user scene background");
            }
            Check(papers.Distinct().Count() == 3 && Luminance(papers[2]) < Luminance(papers[1]), "VeryDark is distinct from and darker than Dark");
        }
        finally { SetAppearance("Light"); state.ShowTable = false; }
    }
}
