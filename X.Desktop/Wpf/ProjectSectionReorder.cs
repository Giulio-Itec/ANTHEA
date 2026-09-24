using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace X.Desktop;

public sealed partial class MainWindow
{
    private (AdornerLayer Layer, SectionDropAdorner Adorner)? projectSectionDrop;

    private bool CanReorderProjectSection(JsonObject section, JsonObject anchor) =>
        !ReferenceEquals(section, anchor) && !section.ContainsKey("modulo_id") && !anchor.ContainsKey("modulo_id") &&
        ReferenceEquals(section.Root, document) && section.Parent is JsonArray siblings &&
        ReferenceEquals(anchor.Parent, siblings) && siblings.Parent is JsonObject owner &&
        (ReferenceEquals(owner["strutture"], siblings) || ReferenceEquals(document["progetti"], siblings));

    private bool HandleProjectSectionDrag(FrameworkElement header, JsonObject anchor, DragEventArgs e, bool drop)
    {
        if (!e.Data.GetDataPresent(SectionDragFormat)) return false;
        e.Handled = true;
        var section = e.Data.GetData(SectionDragFormat) as JsonObject;
        e.Effects = section is not null && CanReorderProjectSection(section, anchor) ? DragDropEffects.Move : DragDropEffects.None;
        if (e.Effects == DragDropEffects.None) { ClearProjectSectionDropIndicator(); return true; }
        bool after = e.GetPosition(header).Y >= header.ActualHeight / 2;
        if (drop)
        {
            ClearProjectSectionDropIndicator();
            Safe(() => MoveProjectSection(section!, anchor, after));
        }
        else if (projectSectionDrop is not { } current || !ReferenceEquals(current.Adorner.AdornedElement, header) || current.Adorner.After != after)
        {
            ClearProjectSectionDropIndicator();
            if (AdornerLayer.GetAdornerLayer(header) is AdornerLayer layer)
            {
                var indicator = new SectionDropAdorner(header, after);
                layer.Add(indicator); projectSectionDrop = (layer, indicator);
            }
        }
        return true;
    }

    private void ClearProjectSectionDropIndicator()
    {
        if (projectSectionDrop is not { } current) return;
        current.Layer.Remove(current.Adorner); projectSectionDrop = null;
    }

    private void MoveProjectSection(JsonObject section, JsonObject anchor, bool after)
    {
        // Keep the parent unchanged: visual order must not change shared-data authority.
        if (!CanReorderProjectSection(section, anchor)) return;
        var siblings = (JsonArray)section.Parent!;
        int previous = siblings.IndexOf(section), index = siblings.IndexOf(anchor) + (after ? 1 : 0);
        if (previous < index) index--;
        if (previous == index) return;
        Commit(); siblings.Remove(section); siblings.Insert(index, section);
        MarkDirty(); RefreshTree(section); RefreshSharedStatus();
    }

    private sealed class SectionDropAdorner : Adorner
    {
        internal bool After { get; }
        internal SectionDropAdorner(UIElement header, bool after) : base(header)
        { After = after; IsHitTestVisible = false; }

        protected override void OnRender(DrawingContext context)
        {
            double y = After ? AdornedElement.RenderSize.Height : 0;
            context.DrawLine(new Pen(Ui.Blue, 2), new Point(0, y), new Point(AdornedElement.RenderSize.Width, y));
            context.DrawEllipse(Ui.Blue, null, new Point(0, y), 3, 3);
        }
    }
}
