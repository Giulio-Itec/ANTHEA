using System.ComponentModel;
using System.Windows.Controls;
using Anthea.Presentation.Modules.Stratigraphy;

namespace X.Desktop.Views.Stratigraphy;

/// <summary>
/// View of <see cref="StratigraphyImportDialogViewModel"/> (F5.6b). The code-behind only passes the key of the service between the
/// PasswordBox and the ViewModel, because the password of a PasswordBox cannot be bound: the box shows the key proposed when the dialog
/// opens, every change goes to <see cref="StratigraphyImportDialogViewModel.ApiKey"/>, and the box is cleared when the ViewModel clears the
/// key (the dialog closed). The key is never written anywhere else.
/// </summary>
public partial class StratigraphyImportDialogView : UserControl
{
    private StratigraphyImportDialogViewModel? dialog;

    public StratigraphyImportDialogView()
    {
        InitializeComponent();
        KeyBox.PasswordChanged += (_, _) => { if (dialog is not null && dialog.ApiKey != KeyBox.Password) dialog.ApiKey = KeyBox.Password; };
        DataContextChanged += (_, _) => Attach(DataContext as StratigraphyImportDialogViewModel);
    }

    private void Attach(StratigraphyImportDialogViewModel? next)
    {
        if (ReferenceEquals(dialog, next)) return;
        if (dialog is not null) dialog.PropertyChanged -= OnDialogChanged;
        dialog = next;
        if (dialog is null) { KeyBox.Clear(); return; }
        dialog.PropertyChanged += OnDialogChanged;
        Show();
    }

    private void OnDialogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StratigraphyImportDialogViewModel.ApiKey)) Show();
    }

    private void Show()
    {
        if (dialog is null || KeyBox.Password == dialog.ApiKey) return;
        if (dialog.ApiKey.Length == 0) KeyBox.Clear();
        else KeyBox.Password = dialog.ApiKey;
    }
}
