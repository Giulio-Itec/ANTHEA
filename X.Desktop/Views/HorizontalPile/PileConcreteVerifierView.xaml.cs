using System.Windows;
using System.Windows.Controls;
using Anthea.Presentation.Modules.HorizontalPile.Elastic;

namespace X.Desktop.Views.HorizontalPile;

/// <summary>
/// The dialog of the c.a. verifier of a segment (F5.8, E1): until F5.10 it hosts the historic <see cref="ConcreteWorkspace"/> on
/// <see cref="PileConcreteVerifier.Sheet"/>, as CreateConcreteWindow of the historic editor: the workspace prepares and commits the sheet, is built
/// again when it asks to reload, commits before «Applica armatura…» (<see cref="PileConcreteVerifier.Apply"/>) and when the window closes (then the
/// ViewModel keeps the sheet in the segment, <see cref="PileConcreteVerifier.Close"/>). The ViewModel of the pile writes the data.
/// </summary>
public partial class PileConcreteVerifierView : UserControl
{
    private PileConcreteVerifier? verifier;
    private Window? window;

    /// <summary>The historic section on the sheet of the segment.</summary>
    internal ConcreteWorkspace? Workspace { get; private set; }

    public PileConcreteVerifierView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Open(DataContext as PileConcreteVerifier);
        Loaded += (_, _) => HookWindow();
    }

    /// <summary>The window of the dialog, as soon as the view is in it (before it is loaded): its closing commits the workspace.</summary>
    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        HookWindow();
    }

    private void HookWindow()
    {
        if (window is not null || Window.GetWindow(this) is not { } shown) return;
        window = shown;
        window.Closed += (_, _) => CloseWorkspace();
    }

    private void Open(PileConcreteVerifier? next)
    {
        if (ReferenceEquals(verifier, next)) return;
        CloseWorkspace();
        verifier = next;
        if (verifier is null) return;
        Show(new ConcreteWorkspace(verifier.Sheet));
    }

    private void Show(ConcreteWorkspace workspace)
    {
        Workspace = workspace;
        workspace.ReloadRequested += Reload;
        WorkspaceHost.Content = workspace;
    }

    /// <summary>ReloadRequested of the workspace (after the editor of the actions): a new workspace on the same sheet, as the historic dialog.</summary>
    private void Reload()
    {
        if (verifier is null || Workspace is null) return;
        Workspace.ReloadRequested -= Reload;
        Workspace.Dispose();
        Show(new ConcreteWorkspace(verifier.Sheet));
    }

    /// <summary>«Applica armatura…»: the workspace commits, then the reinforcement of the sheet goes into the pile.</summary>
    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        Workspace?.Commit();
        verifier?.Apply();
    }

    /// <summary>The window closes: the workspace commits its sheet and is released.</summary>
    private void CloseWorkspace()
    {
        if (Workspace is not { } workspace) return;
        Workspace = null;
        workspace.ReloadRequested -= Reload;
        workspace.Commit();
        workspace.Dispose();
        WorkspaceHost.Content = null;
    }
}
