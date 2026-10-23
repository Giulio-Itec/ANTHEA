using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Anthea.Calculations;
using X.Core;

static partial class Program
{
    /// <summary>
    /// --stress-block-note-only [--probe]: notice «Stress block: risultati SLU meno precisi» (decision of the user of 8/10,
    /// registro F2-15) under the concrete law of the c.a. section and of the horizontal pile, only with the stress block.
    /// With --probe the findings are written to nota-stress-block.txt without stopping. Images in the same folder.
    /// </summary>
    static int StressBlockNoteChecks(string dir, bool probe)
    {
        capture = true;
        var lines = new List<string>();
        void Fact(bool ok, string text) { lines.Add((ok ? "PASS " : "FAIL ") + text); if (!probe) Check(ok, text); else Console.WriteLine((ok ? "PASS " : "FAIL ") + text); }
        const string Notice = "Stress block: risultati SLU meno precisi";
        // Off screen IsVisible is always false: the notice counts when neither it nor a container is hidden.
        static bool Displayed(DependencyObject item, DependencyObject root) { for (var d = item; d is not null; d = ReferenceEquals(d, root) ? null : System.Windows.Media.VisualTreeHelper.GetParent(d)) if (d is UIElement { Visibility: not Visibility.Visible }) return false; return true; }
        static bool Shows(FrameworkElement view) => Descendants<TextBlock>(view).Any(t => t.Text.StartsWith(Notice) && Displayed(t, view));
        try
        {
            var app = new TestApp(); app.LoadStyles(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var assembly = typeof(X.Desktop.MainWindow).Assembly;

            // Horizontal pile: concrete law among the optional data of the section.
            foreach (var law in new[] { "Parabola-rettangolo", "Stress block" })
            {
                var root = Input(); root["sezione"]!["cls_diagramma"] = law; root.Remove("vista_orizzontale");
                // f5-pass-orizzontale: the form of the section in the XAML view of the sheet (tab of the capacity), its groups opened.
                using var host = new PileHost(root);
                var form = (FrameworkElement)PileHost.Get(host.SheetView, "SectionFormHost");
                foreach (var group in Descendants<Expander>(form)) group.IsExpanded = true;
                host.Settle();
                Picture(form, Path.Combine(dir, "palo-" + (law == "Stress block" ? "stress-block" : "parabola") + ".png"));
                Fact(Shows(form) == (law == "Stress block"), "horizontal pile: notice " + (law == "Stress block" ? "shown with the stress block" : "absent with " + law));
            }

            // c.a. section: materials of the control panel, then the law changed by the user.
            var sheet = SezioneCA.DefaultData(); sheet["input"]!["cls_diagramma"] = "Parabola-rettangolo";
            var caType = assembly.GetType("X.Desktop.ConcreteWorkspace")!;
            var ca = (FrameworkElement)Activator.CreateInstance(caType, flags, null, [sheet], null)!;
            var materials = (FrameworkElement)caType.GetField("materials", flags)!.GetValue(ca)!;
            foreach (var group in Descendants<Expander>(materials)) group.IsExpanded = true;
            Snapshot(materials, Path.Combine(dir, "sezione-ca-parabola.png"), 520, 900);
            Fact(!Shows(materials), "c.a. section: notice absent with the parabola-rectangle");
            var lawBox = Descendants<ComboBox>(materials).First(c => c.Items.Contains("Stress block")); lawBox.SelectedItem = "Stress block";
            materials.Measure(new Size(520, 900)); materials.Arrange(new Rect(0, 0, 520, 900)); materials.UpdateLayout();
            Snapshot(materials, Path.Combine(dir, "sezione-ca-stress-block.png"), 520, 900);
            Fact(sheet["input"].S("cls_diagramma") == "Stress block" && Shows(materials), "c.a. section: notice shown as soon as the stress block is chosen");
            lawBox.SelectedItem = "Bilineare"; materials.UpdateLayout();
            Fact(!Shows(materials), "c.a. section: notice hidden again with another law");
            ((IDisposable)ca).Dispose();
            return lines.Any(l => l.StartsWith("FAIL")) && !probe ? 1 : 0;
        }
        finally { File.WriteAllLines(Path.Combine(dir, "nota-stress-block.txt"), lines); }
    }
}
