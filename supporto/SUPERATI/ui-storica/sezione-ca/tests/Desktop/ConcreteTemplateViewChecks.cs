internal sealed partial class ConcreteWorkspace
{
    internal void CheckTemplateEditor(Action<bool, string> check, int bars)
    {
        check(preview.Section is not null && preview.Message.Length == 0 && barInventory.Rows.Count == bars,
            Input.S("shape") + ": riapertura nell'editor senza geometria o armature");
        if (!ConcreteTemplateAdapter.Uses(Input)) return;
        check(geometry.Visibility == Visibility.Collapsed && reinforcement.Visibility == Visibility.Collapsed &&
            additionalLayers.Values.All(layer => layer.Body.Visibility == Visibility.Collapsed),
            Input.S("shape") + ": controlli di disposizioni estranee ancora visibili");
        check(preview.Section!.TemplateLinks.Count > 0, Input.S("shape") + ": percorsi staffe persi nell'editor");
    }
}
