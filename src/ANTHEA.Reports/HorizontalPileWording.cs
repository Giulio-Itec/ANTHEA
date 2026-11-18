namespace Anthea.Reports;

/// <summary>Presentation wording shared by the lateral pile reports and application.</summary>
public static class HorizontalPileWording
{
    public const string Adaptation = "Le formulazioni per la valutazione della capacità portante orizzontale sono state riadattate per tenere conto della presenza di più strati di terreno con caratteristiche geotecniche differenti. Le reazioni del terreno sono valutate lungo il palo considerando le proprietà di ciascuno strato.";

    // Keep the calculation result, its provenance and flags intact; only rephrase displayed text.
    public static string Display(string text) => text
        .Replace("MODELLO STRATIFICATO SPERIMENTALE:", "MODELLO DI CALCOLO PER TERRENO STRATIFICATO:")
        .Replace("MODALITÀ MULTISTRATO SPERIMENTALE", "MODELLO DI CALCOLO PER TERRENO STRATIFICATO")
        .Replace("Modello sperimentale, non formula originale di Broms.", "Formule riadattate per tenere conto della presenza di più strati; la chiusura distribuita è un’ipotesi del modello.")
        .Replace("nessuna validazione sperimentale indipendente", "ipotesi di equilibrio limite, senza una legge di interazione elastoplastica terreno–palo")
        .Replace("validazione sperimentale", "validazione mediante prove")
        .Replace("sperimentale", "riadattato per terreno stratificato", StringComparison.OrdinalIgnoreCase);
}
