using Anthea.Presentation.Help;

namespace Anthea.Presentation.Tests;

/// <summary>Help topics of the fields moved from X.Desktop WikiContextHelp (F5.2): same table, same lookups, identifier = URI.</summary>
internal static class HelpTopicsChecks
{
    internal static int Run()
    {
        var expect = new Expect();
        expect.That(HelpTopics.Topics.Count == 30, "Tabella degli argomenti con 30 voci, come in WikiContextHelp");
        expect.That(HelpTopics.Topics.Select(t => t.Uri).Distinct().Count() == HelpTopics.Topics.Count
            && !HelpTopics.Topics.Any(t => t.Uri == HelpTopics.StressAnalysis.Uri), "URI distinti: l'URI identifica l'argomento");
        expect.That(HelpTopics.ForField("cover_mm", "str_palo")?.Uri.EndsWith("#copriferro-minimo-e-nominale") == true, "Copriferro rinvia alla durabilità");
        expect.That(HelpTopics.ForField("copriferro_fessure", "str_palo")?.Uri.EndsWith("#apertura-delle-fessure") == true, "Primo argomento della tabella che elenca chiave e modulo");
        expect.That(HelpTopics.ForField("N", "geo_palo_orizzontale") is null, "N del palo non eredita la convenzione della sezione CA");
        expect.That(HelpTopics.ForField("cover_mm", null) is null, "Senza modulo nessun argomento");
        expect.That(HelpTopics.Description("axial_force_kn", "geo_palo_verticale") is null
            && HelpTopics.Description("axial_force_kn", "str_palo") == "Azioni, segni e unità della sezione", "Descrizione = titolo dell'argomento");
        expect.That(HelpTopics.ForField("passo", "geo_micropalo_orizzontale")?.Title == "Discretizzazione e vincoli del palo", "Liste di moduli condivise");
        expect.That(ReferenceEquals(HelpTopics.Find(HelpTopics.StressAnalysis.Uri), HelpTopics.StressAnalysis), "Argomento fuori tabella trovato per identificativo");
        expect.That(ReferenceEquals(HelpTopics.Find(HelpTopics.Topics[3].Uri), HelpTopics.Topics[3]), "Argomento della tabella trovato per identificativo");
        expect.That(HelpTopics.Find(null) is null && HelpTopics.Find("sconosciuto") is null, "Identificativo assente o sconosciuto");
        return expect.Count;
    }
}
