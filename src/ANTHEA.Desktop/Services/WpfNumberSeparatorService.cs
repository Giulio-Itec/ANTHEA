using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;
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
    /// the dispatcher included): <see cref="CultureInfo.DefaultThreadCurrentCulture"/> and the culture of this thread. Since 16/11/2026 also the
    /// numbers of the bindings of the views follow it (<see cref="ViewNumberCulture"/>, installed by <see cref="InstallViewNumbers"/>).
    /// </summary>
    internal static void Apply(DecimalSeparatorChoice choice)
    {
        var culture = NumberSeparators.Culture(choice, CultureInfo.CurrentCulture);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.CurrentCulture = culture;
        InstallViewNumbers();
    }

    /// <summary>
    /// Makes <see cref="ViewNumberCulture"/> the culture of the bindings of every element with the default language of WPF (no view of ANTHEA sets
    /// another one), before the first window (16/11/2026, item d). WPF takes the culture of a binding without ConverterCulture from
    /// <c>XmlLanguage.GetSpecificCulture()</c> of the language of the element and keeps it in the private field <c>_specificCulture</c> of the shared
    /// <see cref="XmlLanguage"/>, built from the name of the culture only (no separators of «Virgola»/«Punto», no customizations of Windows). Setting
    /// that field is the only way to change the culture of every binding at once; the check ui/check-climate-view (ViewNumberChecks) verifies it after every
    /// update of WPF. Returns false, and the bindings keep en-US as before, when the field is missing. Idempotent.
    /// </summary>
    internal static bool InstallViewNumbers()
    {
        var language = (XmlLanguage)FrameworkElement.LanguageProperty.GetMetadata(typeof(FrameworkElement)).DefaultValue;
        if (language.GetSpecificCulture() is ViewNumberCulture) return true;
        var field = typeof(XmlLanguage).GetField("_specificCulture", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null || field.FieldType != typeof(CultureInfo)
            || !string.Equals(language.IetfLanguageTag, ViewNumberCulture.Instance.IetfLanguageTag, StringComparison.OrdinalIgnoreCase)) return false;
        field.SetValue(language, ViewNumberCulture.Instance);
        return language.GetSpecificCulture() is ViewNumberCulture;
    }
}
