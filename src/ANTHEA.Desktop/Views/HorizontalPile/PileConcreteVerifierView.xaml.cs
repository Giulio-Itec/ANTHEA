using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Modules.HorizontalPile.Elastic;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>
/// The dialog of the c.a. verifier of a segment (F5.8, E1). Since the switch of the Release of the RC section (23/10/2026) it hosts the section of the
/// Release, <see cref="ConcreteSheetViewModel"/> with its view (ConcreteSectionSheetView), in an <see cref="MvvmSheetEditor"/> on a working copy of
/// <see cref="PileConcreteVerifier.Sheet"/> with the services of the sheet of the pile (<see cref="PileConcreteVerifier.Services"/>; built by
/// <see cref="ConcreteVerifierSection"/>), in place of the
/// historic ConcreteWorkspace (archived in supporto/SUPERATI/ui-storica/sezione-ca), with the same behaviour as CreateConcreteWindow of the historic
/// editor: the sheet of the section commits and its data go back into the sheet of the segment before «Applica armatura…»
/// (<see cref="PileConcreteVerifier.Apply"/>) and when the window closes (then the ViewModel keeps the sheet in the segment and builds the editor of
/// the pile again, <see cref="PileConcreteVerifier.Close"/>). The ViewModel of the pile writes the data.
/// </summary>
public partial class PileConcreteVerifierView : UserControl
{
    private PileConcreteVerifier? verifier;
    private Window? window;

    /// <summary>The RC section of the Release on the sheet of the segment.</summary>
    internal MvvmSheetEditor? Editor { get; private set; }

    public PileConcreteVerifierView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Open(DataContext as PileConcreteVerifier);
        Loaded += (_, _) => HookWindow();
    }

    /// <summary>The window of the dialog, as soon as the view is in it (before it is loaded): its closing gives the sheet back to the segment.</summary>
    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        HookWindow();
    }

    private void HookWindow()
    {
        if (window is not null || Window.GetWindow(this) is not { } shown) return;
        window = shown;
        window.Closed += (_, _) => CloseEditor();
    }

    private void Open(PileConcreteVerifier? next)
    {
        if (ReferenceEquals(verifier, next)) return;
        CloseEditor();
        verifier = next;
        if (verifier is null) return;
        Editor = ConcreteVerifierSection.Open(verifier);
        WorkspaceHost.Content = Editor.View;
    }

    /// <summary>The section commits, then its data become the sheet of the segment (the same object, as the historic workspace edited it).</summary>
    private void Keep()
    {
        if (Editor is not { } editor || verifier is null) return;
        editor.Commit();
        var sheet = verifier.Sheet;
        var data = editor.Data.ToArray();
        sheet.Clear();
        foreach (var (key, value) in data) sheet[key] = value?.DeepClone();
    }

    /// <summary>«Applica armatura…»: the section commits, then the reinforcement of the sheet goes into the pile.</summary>
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        Keep();
        verifier?.Apply();
    }

    /// <summary>The window closes: the section gives its sheet back to the segment and is released.</summary>
    private void CloseEditor()
    {
        if (Editor is not { } editor) return;
        Keep();
        Editor = null;
        WorkspaceHost.Content = null;
        editor.Dispose();
    }
}
