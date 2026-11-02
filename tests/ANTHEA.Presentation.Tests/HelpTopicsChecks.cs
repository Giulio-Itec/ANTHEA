using Anthea.Presentation.Help;
using Anthea.Wiki;

namespace Anthea.Presentation.Tests;

/// <summary>Help topics of the fields moved from X.Desktop WikiContextHelp (F5.2): same table, same lookups, identifier = URI.</summary>
internal static class HelpTopicsChecks
{
    internal static int Run()
    {
        var expect = new Expect();
        expect.That(HelpTopics.Topics.Count == 54 && HelpTopics.Topics.Take(30).All(t => !t.Modules.Contains("geo_paratie"))
            && HelpTopics.Topics.Skip(30).Take(8).All(t => t.Modules.Contains("geo_paratie")),
            "Trenta argomenti storici conservati, otto per le paratie e sedici per i fogli Azioni (revisione dell'interfaccia del 2/11)");
        // The Actions sheets: one guide section per module, whose destination exists in the Wiki; no topic per key.
        var actions = HelpTopics.Topics.Skip(38).ToArray();
        expect.That(actions.Length == 16 && actions.All(t => t.Keys.Count == 0 && t.Modules.Count == 1 && t.Modules[0].StartsWith("act_", StringComparison.Ordinal))
            && actions.Select(t => t.Modules[0]).Distinct().Count() == 16, "Sedici argomenti Azioni, uno per foglio");
        foreach (var topic in actions)
        {
            var target = topic.Uri.Split('#');
            var article = WikiCatalog.Resolve(topic.Uri);
            expect.That(article is not null && article.Modules.Contains(topic.Modules[0]) && (target.Length == 1 || article.Sections.Contains(target[1])),
                "Sezione della guida del foglio Azioni: " + topic.Uri);
            expect.That(ReferenceEquals(HelpTopics.ActionsGuide(topic.Modules[0]), topic), "Guida del foglio " + topic.Modules[0]);
        }
        expect.That(HelpTopics.ActionsGuide("str_palo") is null && HelpTopics.ActionsGuide("act_spettri_ntc") is null, "Nessuna guida Azioni per gli altri moduli");
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
        expect.That(HelpTopics.ForField("q_top", "geo_paratie")?.Uri == "analisi-paratie#carichi-distribuiti-per-intervalli", "Carico sulla fascia rinvia alle proprie unità");
        expect.That(HelpTopics.ForField("kh", "geo_paratie")?.Uri == "analisi-paratie#stato-iniziale-e-pressioni-limite", "kh della paratia rinvia alla propria legge costitutiva");
        expect.That(HelpTopics.ForField("kh", "geo_palo_orizzontale")?.Uri == "palo-elastico#modulo-di-reazione-e-conversioni", "Il palo conserva la propria spiegazione di kh");
        foreach (var topic in HelpTopics.Topics.Where(t => t.Modules.Contains("geo_paratie")))
        {
            var target = topic.Uri.Split('#');
            var article = WikiCatalog.Resolve(topic.Uri);
            expect.That(article is not null && article.Sections.Contains(target[1]), "Destinazione reale della Wiki: " + topic.Uri);
        }
        return expect.Count;
    }
}
