namespace Anthea.Presentation.Mvvm;

/// <summary>
/// The inner tabs chosen in the views of a ViewModel (SH49 reduced, F5.15, decision of 24/10): state of the view kept in memory and never
/// written in the document (rule T6). A view keeps here the index of each TabControl whose selection is not bound to the ViewModel,
/// under the key of the TabControl, so that the view rebuilt when the tab of the shell comes back on screen shows the same inner tab.
/// </summary>
public interface IViewTabMemory
{
    IDictionary<string, int> SelectedTabs { get; }
}
