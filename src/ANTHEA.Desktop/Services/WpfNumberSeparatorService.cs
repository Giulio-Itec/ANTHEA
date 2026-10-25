using System.Globalization;
using System.IO;
using System.Text.Json;
using Anthea.Presentation.Shell.Commands;

namespace X.Desktop.Services;

/// <summary>
/// The decimal separator of ANTHEA (F5.15, V-a): the preference saved next to the appearance (separatore.json in
/// %LOCALAPPDATA%\ANTHEA), applied at start-up before the first window (<see cref="Apply"/>). A change from the menu «Aspetto» applies at
/// the next start-up.
/// </summary>
internal sealed class WpfNumberSeparatorService : INumberSeparatorService
{
    internal static string PreferencePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ANTHEA", "separatore.json");

    public DecimalSeparatorChoice Current { get; private set; } = Read(PreferencePath);

    public void Save(DecimalSeparatorChoice choice)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
        File.WriteAllText(PreferencePath + ".new", JsonSerializer.Serialize(NumberSeparators.Key(choice)));
        File.Move(PreferencePath + ".new", PreferencePath, true);
        Current = choice;
    }

    internal static DecimalSeparatorChoice Read(string path)
    {
        try { return NumberSeparators.FromKey(JsonSerializer.Deserialize<string>(File.ReadAllText(path))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return DecimalSeparatorChoice.System; }
    }

    /// <summary>
    /// The culture of <paramref name="choice"/> for every thread of the application (the threads of the calculations and the operations of
    /// the dispatcher included): <see cref="CultureInfo.DefaultThreadCurrentCulture"/> and the culture of this thread.
    /// </summary>
    internal static void Apply(DecimalSeparatorChoice choice)
    {
        var culture = NumberSeparators.Culture(choice, CultureInfo.CurrentCulture);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.CurrentCulture = culture;
    }
}
