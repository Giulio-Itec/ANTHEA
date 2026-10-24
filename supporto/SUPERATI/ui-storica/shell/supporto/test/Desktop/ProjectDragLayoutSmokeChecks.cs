using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using X.Core;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private async Task CheckProjectDragLayout(JsonObject section, JsonObject sourceSheet, JsonObject anchorSheet, string directory)
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        TreeViewItem Find(JsonObject node) => Ui.Descendants<TreeViewItem>(tree).Single(item => ReferenceEquals(item.Tag, node));
        Rect Bounds(FrameworkElement element) => new(element.TranslatePoint(new Point(), this), element.RenderSize);
        async Task Layout() { await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); UpdateLayout(); }
        DragEventArgs Drag(TreeViewItem target, string format, object value, RoutedEvent routedEvent, bool after = false)
        {
            var header = (FrameworkElement)target.Header;
            var args = (DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, [new DataObject(format, value), DragDropKeyStates.None, DragDropEffects.Copy | DragDropEffects.Move, header,
                    new Point(10, after ? header.ActualHeight - 1 : 1)], null)!;
            args.RoutedEvent = routedEvent; target.RaiseEvent(args); return args;
        }
        var documentBefore = document.DeepClone();
        string originalName = section.S("nome");
        try
        {
            // Long names must not turn the destination hint into extra lines above the tree.
            section["nome"] = originalName + " — " + string.Join(" ", Enumerable.Repeat("Fondazioni della struttura principale", 6));
            RefreshTree(section); await Layout();
            var target = Find(section); var anchor = Find(anchorSheet);
            var panels = new FrameworkElement[] { projectLayout, projectTreePane, projectDetailPane, projectCatalogPane, tree, (FrameworkElement)target.Header, (FrameworkElement)anchor.Header };
            var initial = panels.Select(Bounds).ToArray();
            var scroll = Ui.Descendants<ScrollViewer>(tree).First();
            double initialOffset = scroll.VerticalOffset;
            void Stable(string phase)
            {
                for (int i = 0; i < panels.Length; i++)
                {
                    var now = Bounds(panels[i]); var before = initial[i];
                    Check(Math.Abs(now.X - before.X) < .1 && Math.Abs(now.Y - before.Y) < .1 &&
                        Math.Abs(now.Width - before.Width) < .1 && Math.Abs(now.Height - before.Height) < .1,
                        $"Layout instabile durante {phase}: {panels[i].GetType().Name}, prima {before}, dopo {now}");
                }
                Check(Math.Abs(scroll.VerticalOffset - initialOffset) < .1, "Il trascinamento cambia lo scorrimento dell’albero");
            }
            for (int i = 0; i < 5; i++)
            {
                var over = Drag(target, ModuleDragFormat, "str_palo", DragDrop.DragOverEvent);
                await Layout();
                Check(over.Effects == DragDropEffects.Copy && projectDropHint.IsVisible && projectDropHint.Text.Contains(section.S("nome")), "Destinazione della scheda non indicata");
                Stable("inserimento nella sezione");
                if (i == 0) File.WriteAllBytes(Path.Combine(directory, "trascinamento-scheda-stabile.png"), Ui.Snapshot(this));
                Drag(target, ModuleDragFormat, "str_palo", DragDrop.DragLeaveEvent); await Layout();
                Check(projectDropHint.Text.Length == 0, "Indicazione residua dopo l’uscita dalla sezione");
                Stable("uscita dalla sezione");
            }
            foreach (bool after in new[] { false, true, false, true })
            {
                var over = Drag(anchor, SheetDragFormat, sourceSheet, DragDrop.DragOverEvent, after); await Layout();
                Check(over.Effects == DragDropEffects.Move, "Riordino schede rifiutato");
                Stable("riordino delle schede");
                Drag(anchor, SheetDragFormat, sourceSheet, DragDrop.DragLeaveEvent); await Layout();
                Stable("annullamento del riordino");
            }
        }
        finally
        {
            ClearProjectSectionDropIndicator(); section["nome"] = originalName; RefreshTree(section); await Layout();
        }
        Check(JsonNode.DeepEquals(documentBefore, document), "L’anteprima del trascinamento modifica il documento");
        File.WriteAllText(Path.Combine(directory, "trascinamento-layout.txt"), "OK: posizione e dimensioni dei pannelli, righe e scorrimento stabili durante inserimento, riordino e uscita; nomi lunghi; documento invariato.");
    }
}
