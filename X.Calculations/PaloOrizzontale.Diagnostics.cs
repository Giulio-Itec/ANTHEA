using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class PaloOrizzontale
{
    public static JsonArray References() => new(
        Reference("Pacini", "G. Pacini — approccio al terreno stratificato", "Checker/PileCalculator.cs; diagrammi per strato e FindDepthForForce", "", "Origine dell'estensione stratificata. Equilibrio globale e chiusura distribuita in GPCChecker.Geotechnics (LateralPileCapacity)."),
        Reference("Viggiani", "C. Viggiani, Fondazioni", "§§13.2.2–13.2.5, pp. 400–415 del testo fornito", "", "Leggi locali e meccanismi classici; non attribuisce al testo l'estensione mista."),
        Reference("Broms-C", "B. B. Broms (1964), Lateral Resistance of Piles in Cohesive Soils", "ASCE, Journal of the Soil Mechanics and Foundations Division, 90(SM2)", "https://doi.org/10.1061/JSFEAQ.0000611", "Riferimento originale per i terreni coesivi, ripreso attraverso Viggiani."),
        Reference("Broms-G", "B. B. Broms (1964), Lateral Resistance of Piles in Cohesionless Soils", "ASCE, Journal of the Soil Mechanics and Foundations Division, 90(SM3)", "https://doi.org/10.1061/JSFEAQ.0000614", "Riferimento originale per i granulari e la chiusura concentrata, ripreso attraverso Viggiani e Wood."),
        Reference("NZGS", "J. Wood (2021), Cantilever Pole Retaining Walls", "New Zealand Geotechnical Society, Geomechanics News 101, 22 giugno 2021; §§2.2–2.3, 2.5, 2.8", "https://www.nzgs.org/libraries/cantilever-pole-retaining-walls/", "Broms Modified: reazioni opposte distribuite per pali corti rigidi in terreno granulare. Supporta questa ipotesi, non valida l'estensione mista o la chiusura sotto la cerniera dei pali lunghi. Il salto di segno è un'idealizzazione limite, non una legge pressione-spostamento."),
        Reference("FHWA", "FHWA (2018), Geotechnical Engineering Circular No. 9", "Design, Analysis, and Testing of Laterally Loaded Deep Foundations that Support Transportation Facilities; FHWA-HIF-18-031, §§6.3 e 6.5", "https://www.fhwa.dot.gov/engineering/geotech/pubs/hif18031.pdf", "Inquadramento dei metodi p-y, della deformabilità e dei limiti di Broms. Le curve p-y non sono implementate qui e richiedono calibrazione appropriata; non sono una validazione del presente modello.")
    );

    private static JsonObject Reference(string id, string title, string detail, string url, string use)
        => J.Obj(("id", id), ("titolo", title), ("dettaglio", detail), ("url", url), ("uso", use));
}
