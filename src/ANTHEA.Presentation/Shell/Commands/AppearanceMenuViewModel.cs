using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Shell.Commands;

/// <summary>An appearance of the application: the three of the menu «Aspetto» (MainWindow.BuildMenu).</summary>
public enum AppearanceChoice
{
    Light,
    Dark,
    VeryDark
}

/// <summary>
/// The appearance of the application (F5.13 c): the palette is WPF, so the desktop applies it (Appearance.Set, with the preference saved as
/// today); the checks record the calls.
/// </summary>
public interface IAppearanceService
{
    /// <summary>The appearance in use.</summary>
    AppearanceChoice Current { get; }

    /// <summary>Applies <paramref name="choice"/> to every window and saves it as the preference of the user.</summary>
    void Apply(AppearanceChoice choice);
}

/// <summary>An item of the menu «Aspetto»: title, appearance, check mark and command.</summary>
public sealed class AppearanceItemViewModel : ViewModelBase
{
    private bool isChecked;

    internal AppearanceItemViewModel(string title, AppearanceChoice choice, bool isChecked, IRelayCommand select)
    {
        Title = title; Choice = choice; this.isChecked = isChecked; SelectCommand = select;
    }

    public string Title { get; }
    public AppearanceChoice Choice { get; }
    public IRelayCommand SelectCommand { get; }

    public bool IsChecked { get => isChecked; internal set => SetProperty(ref isChecked, value); }
}

/// <summary>
/// The menu «Aspetto» (F5.13 c; MainWindow.BuildMenu): «Chiaro», «Scuro», «Molto scuro» in this order, checkable, the current one checked;
/// a choice applies the appearance and checks only that item; opening the menu reads the current appearance again (the appearance can change
/// elsewhere, as SubmenuOpened does today). Errors go to the messages as the commands of the window (Safe, Q19).
/// </summary>
public sealed class AppearanceMenuViewModel : ViewModelBase
{
    /// <summary>Header of the menu, with the access key of today.</summary>
    public const string Title = "_Aspetto";

    private readonly IAppearanceService appearance;

    public AppearanceMenuViewModel(IAppearanceService appearance, IMessageService messages)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(messages);
        this.appearance = appearance;
        var guard = new CommandGuard(messages);
        Items = new (string Title, AppearanceChoice Choice)[] { ("Chiaro", AppearanceChoice.Light), ("Scuro", AppearanceChoice.Dark), ("Molto scuro", AppearanceChoice.VeryDark) }
            .Select(item => new AppearanceItemViewModel(item.Title, item.Choice, appearance.Current == item.Choice,
                GuardedCommands.Create(guard, () => Select(item.Choice)))).ToArray();
        OpenedCommand = new RelayCommand(Refresh);
    }

    /// <summary>The items, in the order of today.</summary>
    public IReadOnlyList<AppearanceItemViewModel> Items { get; }

    /// <summary>The menu opens (SubmenuOpened): the marks follow the current appearance.</summary>
    public IRelayCommand OpenedCommand { get; }

    /// <summary>Applies <paramref name="choice"/>; only its item stays checked.</summary>
    public void Select(AppearanceChoice choice)
    {
        appearance.Apply(choice);
        foreach (var item in Items) item.IsChecked = item.Choice == choice;
    }

    /// <summary>The marks of the current appearance.</summary>
    public void Refresh()
    {
        var current = appearance.Current;
        foreach (var item in Items) item.IsChecked = item.Choice == current;
    }
}
