using System.ComponentModel;
using System.Windows.Controls;
using Anthea.Presentation.Modules.Stratigraphy;

namespace X.Desktop.Views.Stratigraphy;

/// <summary>
/// View of <see cref="StratigraphyEditorViewModel"/> (F5.6b). The code-behind does only what decision Q3 allows, the technical drawing: the
/// historic <see cref="StratigraphyProfileView"/> receives <see cref="StratigraphyEditorViewModel.Profile"/> and
/// <see cref="StratigraphyEditorViewModel.ProfileSelection"/> at every change and is redrawn (Refresh of the historic view); a click on a
/// layer of the profile selects it in the editor, as it selected the item of the list. No calculation and no write of the document.
/// </summary>
public partial class StratigraphyEditorView : UserControl
{
    private StratigraphyEditorViewModel? editor;

    /// <summary>The profile, as the historic view creates it.</summary>
    internal StratigraphyProfileView Profile { get; } = new();

    public StratigraphyEditorView()
    {
        InitializeComponent();
        ProfileHost.Content = Profile;
        Profile.Selected += index => { if (editor is not null) editor.SelectedLayerIndex = index; };
        DataContextChanged += (_, _) => Attach(DataContext as StratigraphyEditorViewModel);
        Unloaded += (_, _) => Attach(null);
        Loaded += (_, _) => Attach(DataContext as StratigraphyEditorViewModel);
    }

    private void Attach(StratigraphyEditorViewModel? next)
    {
        if (ReferenceEquals(editor, next)) return;
        if (editor is not null) editor.PropertyChanged -= OnEditorChanged;
        editor = next;
        if (editor is null) return;
        editor.PropertyChanged += OnEditorChanged;
        Draw();
    }

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StratigraphyEditorViewModel.Profile) or nameof(StratigraphyEditorViewModel.ProfileSelection)) Draw();
    }

    private void Draw()
    {
        if (editor is null) return;
        Profile.Data = editor.Profile;
        Profile.Selection = editor.ProfileSelection;
        Profile.InvalidateVisual();
    }
}
