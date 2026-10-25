using System.Globalization;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Shell.Commands;

/// <summary>The decimal separator of ANTHEA (F5.15, V-a, decision of 24/10): that of Windows, the comma or the point.</summary>
public enum DecimalSeparatorChoice
{
    System,
    Comma,
    Point
}

/// <summary>
/// The rule of the setting «Sistema / Virgola / Punto» (f5-15-revisione.md, note 15.2, way «a»): the culture of the numbers that the
/// application uses where today it follows Windows (fields, tables, states, details). «Sistema» is the culture of the user; «Virgola» and
/// «Punto» are a copy of it with that decimal separator and the opposite group separator. The reports and drawings that fix it-IT stay
/// as they are. The desktop applies the culture at start-up; the checks fix «Virgola».
/// </summary>
public static class NumberSeparators
{
    public static CultureInfo Culture(DecimalSeparatorChoice choice, CultureInfo user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (choice == DecimalSeparatorChoice.System) return user;
        var culture = (CultureInfo)user.Clone();
        string separator = choice == DecimalSeparatorChoice.Comma ? "," : ".", group = choice == DecimalSeparatorChoice.Comma ? "." : ",";
        var format = culture.NumberFormat;
        format.NumberDecimalSeparator = format.CurrencyDecimalSeparator = format.PercentDecimalSeparator = separator;
        format.NumberGroupSeparator = format.CurrencyGroupSeparator = format.PercentGroupSeparator = group;
        return culture;
    }

    /// <summary>The text of the preference file for <paramref name="choice"/> and back; an unknown text is «Sistema».</summary>
    public static string Key(DecimalSeparatorChoice choice) => choice switch
    {
        DecimalSeparatorChoice.Comma => "virgola",
        DecimalSeparatorChoice.Point => "punto",
        _ => "sistema"
    };

    public static DecimalSeparatorChoice FromKey(string? key) => key switch
    {
        "virgola" => DecimalSeparatorChoice.Comma,
        "punto" => DecimalSeparatorChoice.Point,
        _ => DecimalSeparatorChoice.System
    };
}

/// <summary>The saved preference of the decimal separator; the desktop keeps it next to the appearance and applies it at start-up.</summary>
public interface INumberSeparatorService
{
    /// <summary>The saved preference (in use since the start-up when nobody changed it).</summary>
    DecimalSeparatorChoice Current { get; }

    /// <summary>Saves <paramref name="choice"/>: it applies at the next start-up.</summary>
    void Save(DecimalSeparatorChoice choice);
}

/// <summary>An item of «Separatore decimale» in the menu «Aspetto»: title, choice, check mark and command.</summary>
public sealed class DecimalSeparatorItemViewModel : ViewModelBase
{
    private bool isChecked;

    internal DecimalSeparatorItemViewModel(string title, DecimalSeparatorChoice choice, bool isChecked, IRelayCommand select)
    {
        Title = title; Choice = choice; this.isChecked = isChecked; SelectCommand = select;
    }

    public string Title { get; }
    public DecimalSeparatorChoice Choice { get; }
    public IRelayCommand SelectCommand { get; }

    public bool IsChecked { get => isChecked; internal set => SetProperty(ref isChecked, value); }
}

/// <summary>
/// «Separatore decimale» in the menu «Aspetto» (V-a): «Sistema», «Virgola», «Punto», the saved one checked. A different choice is saved and
/// applies at the next start-up, with a notice: the texts and drawings already on screen do not change by themselves (note 15.2).
/// </summary>
public sealed class DecimalSeparatorMenuViewModel
{
    /// <summary>Header of the submenu.</summary>
    public const string Title = "Separatore decimale";

    /// <summary>The notice after a change of the choice.</summary>
    public const string RestartNotice = "Il separatore decimale scelto vale dal prossimo avvio di ANTHEA.";

    private readonly INumberSeparatorService separators;
    private readonly IMessageService messages;

    public DecimalSeparatorMenuViewModel(INumberSeparatorService separators, IMessageService messages)
    {
        ArgumentNullException.ThrowIfNull(separators);
        ArgumentNullException.ThrowIfNull(messages);
        this.separators = separators; this.messages = messages;
        var guard = new CommandGuard(messages);
        Items = new (string Title, DecimalSeparatorChoice Choice)[] { ("Sistema", DecimalSeparatorChoice.System), ("Virgola", DecimalSeparatorChoice.Comma), ("Punto", DecimalSeparatorChoice.Point) }
            .Select(item => new DecimalSeparatorItemViewModel(item.Title, item.Choice, separators.Current == item.Choice,
                GuardedCommands.Create(guard, () => Select(item.Choice)))).ToArray();
    }

    public IReadOnlyList<DecimalSeparatorItemViewModel> Items { get; }

    /// <summary>Saves <paramref name="choice"/> when it changes, with the notice of the next start-up; only its item stays checked.</summary>
    public void Select(DecimalSeparatorChoice choice)
    {
        if (separators.Current != choice)
        {
            separators.Save(choice);
            messages.Inform("ANTHEA", RestartNotice);
        }
        foreach (var item in Items) item.IsChecked = item.Choice == choice;
    }
}
