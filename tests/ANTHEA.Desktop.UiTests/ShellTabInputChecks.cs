using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Anthea.Presentation.Shell.Tabs;
using X.Desktop.Views.Shell;

namespace X.Desktop;

internal static class ShellTabInputChecks
{
    internal static async Task Run(Window window, TabHostViewModel host, Action<bool, string> check, Func<string, Task> capture, string prefix)
    {
        var tabs = host.Tabs.ToArray(); var selected = host.Selected;
        var snapshots = tabs.OfType<SheetTabViewModel>().ToDictionary(t => t, t => (t.Content, Data: t.Content.Data.ToJsonString(), t.Content.Revision));
        FrameworkElement Header(ShellTabViewModel tab) => Descendants(window).OfType<FrameworkElement>()
            .Single(e => ShellTabInput.GetIsEnabled(e) && ReferenceEquals(e.DataContext, tab));
        bool Indicator(FrameworkElement header) => AdornerLayer.GetAdornerLayer(header)?.GetAdorners(header)?.Length > 0;
        var last = Header(tabs[^1]);
        check(tabs.Length > 1 && tabs.All(t => Header(t).AllowDrop), "Schede trascinabili: titoli collegati al comportamento di trascinamento");
        check(ShellTabInput.Over(last, tabs[0], last.ActualWidth - 1) == DragDropEffects.Move && host.Tabs.SequenceEqual(tabs),
            "Trascinamento schede: anteprima dopo la destinazione senza modificare l'ordine");
        await capture(prefix + "-indicatore.png");
        check(Indicator(last), "Trascinamento schede: linea visibile nella posizione di inserimento");
        var appearance = Appearance.Current;
        try
        {
            Appearance.Set(AppAppearance.Dark, persist: false);
            ShellTabInput.Over(last, tabs[0], last.ActualWidth - 1);
            await capture(prefix + "-indicatore-scuro.png");
        }
        finally { Appearance.Set(appearance, persist: false); }
        check(ShellTabInput.CompleteDrop(last, tabs[0], last.ActualWidth - 1) && host.Tabs.SequenceEqual(tabs.Skip(1).Append(tabs[0]))
            && !Indicator(last), "Trascinamento schede: rilascio sposta a destra e rimuove la linea");
        await capture(prefix + "-riordinate.png");
        check(host.Selected == selected && snapshots.All(p => ReferenceEquals(p.Key.Content, p.Value.Content)
            && p.Key.Content.Data.ToJsonString() == p.Value.Data && p.Key.Content.Revision == p.Value.Revision),
            "Trascinamento schede: selezione, istanze, dati e revisioni invariati");
        var first = Header(tabs[1]);
        check(ShellTabInput.Over(first, tabs[0], 1) == DragDropEffects.Move && ShellTabInput.CompleteDrop(first, tabs[0], 1)
            && host.Tabs.SequenceEqual(tabs), "Trascinamento schede: rilascio prima della destinazione ripristina l'ordine originale");
        last = Header(tabs[^1]); ShellTabInput.Over(last, tabs[0], last.ActualWidth - 1);
        check(ShellTabInput.Over(last, null, 1) == DragDropEffects.None && !Indicator(last) && !ShellTabInput.CompleteDrop(last, null, 1)
            && host.Tabs.SequenceEqual(tabs), "Trascinamento schede annullato o non valido: nessun riordino e nessuna linea residua");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var inner in Descendants(child)) yield return inner;
        }
    }
}
