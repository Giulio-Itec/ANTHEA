internal sealed partial class SheetEditor
{
    // The automatic c.a. checks (domains, SLE, shear) queued after the first calculation, without a window.
    internal async Task WaitForConcreteChecks()
    {
        for (int i = 0; i < 1200 && (Busy || !HasResults); i++) { await Task.Delay(50); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); }
        if (Busy || !HasResults) throw new Exception("Verifiche c.a. non completate");
    }
    internal DomainViewport3D ShowDomain3DForChecks() => concrete!.ShowDomain3DForChecks();
}

internal sealed partial class ConcreteWorkspace
{
    internal DomainViewport3D ShowDomain3DForChecks()
    {
        // Without a window IsVisible stays false: fill the view as BuildReport does for hidden panels.
        tabs.SelectedIndex = 1; UpdateLayout();
        var panel = domainPanels.First(p => p.ThreeD); RefreshDomainPanel(panel, renderHidden: true);
        // The PNG has the size of the view on screen, which follows the window layout (the dark templates of tabs and
        // choices are a few pixels taller): a fixed size compares only the colours. At the origin of its frame, as when
        // it stretches over it: RenderTargetBitmap draws the element at its offset in the parent.
        panel.View3D!.Width = 620; panel.View3D.Height = 360;
        panel.View3D.HorizontalAlignment = HorizontalAlignment.Left; panel.View3D.VerticalAlignment = VerticalAlignment.Top;
        return panel.View3D;
    }
}
