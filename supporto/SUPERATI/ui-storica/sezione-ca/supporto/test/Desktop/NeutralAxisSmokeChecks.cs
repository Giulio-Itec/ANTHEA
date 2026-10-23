internal sealed partial class ConcreteWorkspace
{
    internal void VerifyNeutralAxisControls(Action<bool,string> check)
    {
        var options = new JsonObject(); var view = new ConcreteSectionViewport();
        var toggle = NeutralAxisToggle(view, options);
        check(toggle.IsChecked == true && view.ShowNeutralAxis, "CLS: opzione inizialmente attiva");
        toggle.IsChecked = false; toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        var reopened = new ConcreteSectionViewport(); _ = NeutralAxisToggle(reopened, options);
        check(!view.ShowNeutralAxis && !reopened.ShowNeutralAxis, "CLS: preferenza persistita");
    }
}
