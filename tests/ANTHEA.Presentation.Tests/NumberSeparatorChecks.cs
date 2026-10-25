using System.Globalization;
using Anthea.Presentation.Shell.Commands;

namespace Anthea.Presentation.Tests;

/// <summary>The setting «Sistema / Virgola / Punto» (F5.15, V-a): the rule of the culture and the submenu of «Aspetto».</summary>
internal static class NumberSeparatorChecks
{
    private sealed class Preference(DecimalSeparatorChoice current) : INumberSeparatorService
    {
        internal List<DecimalSeparatorChoice> Saved { get; } = [];
        public DecimalSeparatorChoice Current { get; private set; } = current;
        public void Save(DecimalSeparatorChoice choice) { Saved.Add(choice); Current = choice; }
    }

    internal static int Run()
    {
        var expect = new Expect();
        // A user culture as the one of the machine of the checks: it-IT with the point as decimal separator.
        var user = (CultureInfo)CultureInfo.GetCultureInfo("it-IT").Clone();
        user.NumberFormat.NumberDecimalSeparator = "."; user.NumberFormat.NumberGroupSeparator = "'";
        expect.That(ReferenceEquals(NumberSeparators.Culture(DecimalSeparatorChoice.System, user), user), "«Sistema» non è la cultura dell'utente");
        var comma = NumberSeparators.Culture(DecimalSeparatorChoice.Comma, user);
        expect.That(1234.5.ToString("N1", comma) == "1.234,5" && comma.Name == "it-IT", "«Virgola»: virgola decimale e punto delle migliaia sulla cultura dell'utente");
        var point = NumberSeparators.Culture(DecimalSeparatorChoice.Point, user);
        expect.That(1234.5.ToString("N1", point) == "1,234.5", "«Punto»: punto decimale e virgola delle migliaia");
        expect.That(user.NumberFormat.NumberDecimalSeparator == ".", "La cultura dell'utente non cambia");
        foreach (var choice in new[] { DecimalSeparatorChoice.System, DecimalSeparatorChoice.Comma, DecimalSeparatorChoice.Point })
            expect.That(NumberSeparators.FromKey(NumberSeparators.Key(choice)) == choice, "Chiave del file non reversibile: " + choice);
        expect.That(NumberSeparators.FromKey("altro") == DecimalSeparatorChoice.System && NumberSeparators.FromKey(null) == DecimalSeparatorChoice.System,
            "Una preferenza sconosciuta non è «Sistema»");

        var preference = new Preference(DecimalSeparatorChoice.System);
        var messages = new ScriptedMessages();
        var menu = new DecimalSeparatorMenuViewModel(preference, messages);
        expect.That(menu.Items.Select(i => i.Title).SequenceEqual(["Sistema", "Virgola", "Punto"]), "Voci del sottomenu");
        expect.That(menu.Items.Single(i => i.IsChecked).Choice == DecimalSeparatorChoice.System, "Spuntata la preferenza salvata");
        menu.Items[1].SelectCommand.Execute(null);
        expect.That(preference.Saved.SequenceEqual([DecimalSeparatorChoice.Comma]) && menu.Items.Single(i => i.IsChecked).Choice == DecimalSeparatorChoice.Comma,
            "«Virgola» salvata e spuntata");
        expect.That(messages.Shown.Count == 1 && messages.Shown[0].Message == DecimalSeparatorMenuViewModel.RestartNotice, "Avviso del prossimo avvio");
        menu.Items[1].SelectCommand.Execute(null);
        expect.That(preference.Saved.Count == 1 && messages.Shown.Count == 1, "La stessa scelta non si salva di nuovo");
        return expect.Count;
    }
}
