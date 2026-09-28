namespace Anthea.Calculations;

public static partial class BridgeConcept
{
    public const string PriceBasis = "Valori orientativi confrontati con ANAS NC-MP 2026 Rev.1 e Regione Emilia-Romagna 2026. Prezzi modificabili; gli archivi conservano il proprio listino. SG e utile già compresi nei riferimenti: la maggiorazione di cantiere copre soltanto oneri aggiuntivi non computati.";
    public static readonly IReadOnlyDictionary<string, string> RateNotes = new Dictionary<string, string>
    {
        ["concrete_deck"] = "260 €/m³: riferimento C45/55, ANAS B.03.040.b (260,21). Adeguare a classe, esposizione e prefabbricazione; armatura e casseri separati.",
        ["concrete_sub"] = "240 €/m³: valore aggregato C35/45 tra fondazioni ed elevazione; ANAS 226,38–241,07; RER fondazioni 257,28. Armatura e casseri separati.",
        ["rebar"] = "1.660 €/t = 1,66 €/kg; ANAS B.05.030, fornitura e posa B450C. RER gabbie pali A02.046.050: 1,59 €/kg.",
        ["prestress"] = "3.600 €/t: accantonamento di sistema. ANAS B.05.057 trefoli 2.020 €/t con ancoraggi separati; non è una voce equivalente completa.",
        ["steel"] = "3.500 €/t: ANAS B.05.000.01.8.b, carpenteria e varo per sollevamento. Protezioni e opere provvisorie da valutare.",
        ["steel_box"] = "4.000 €/t: ipotesi maggiorata per cassoni; ANAS base 3.500–3.750 €/t. Nessun automatismo per classe acciaio o protezione.",
        ["formwork"] = "50 €/m²: ordine di grandezza; ANAS casseforme piane 40,34, a perdere 50,74. Sostegni oltre 5 m e centine da valutare a parte.",
        ["pile_1"] = "300 €/m: ANAS B.02.040.b 280,67; perforazione e cls, armatura separata. Rivedere camicie, roccia, alveo e trasporto.",
        ["pile_15"] = "550 €/m: ANAS B.02.040.d 542,04; armatura separata. Sovrapprezzi dipendono dal sito.",
        ["bearing"] = "5.000 €/cad: sola indennità media. ANAS appoggi POT è in €/kN, dipende da carico e movimento; 1.000 kN multidirezionale ≈ 3.660 €, 2.000 kN ≈ 5.860 €.",
        ["joint"] = "2.400 €/m: compatibile con giunti di corsa moderata; ANAS B.07.050.b.1 2.245,44. Per grandi luci/movimenti serve una voce specifica.",
        ["barrier"] = "360 €/m: ANAS bordo ponte H4 zincato G.02.005.3.a 362,73. Classe e configurazione vanno scelte nel progetto.",
        ["surfacing"] = "32 €/m²: pacchetto convenzionale, non una singola voce verificata; definire spessori, miscela, impermeabilizzazione e drenaggi.",
        ["steel_ortho"] = "5.000 €/t: ipotesi aggregata. ANAS aggiunge 460 €/t solo alla massa della lastra ortotropa; non a tutto il cassone.",
        ["cables"] = "16.000 €/t: ipotesi per sistema installato. Pendini ANAS 14.400–15.530 €/t; terminali specifici e grandi cavi possono richiedere un preventivo specialistico.",
        ["erection_special"] = "1.000 €/t: riserva aggiuntiva per complessità speciale; il varo ordinario è già compreso nella carpenteria. Azzerare se incluso nell'offerta."
    };
    private static void CostWarnings(List<string> warnings, double length, double bearingForce, double bearingCount, double bearingRate)
    {
        double average = bearingForce / Math.Max(1, bearingCount);
        if (average > 1500 || bearingRate < 2500) warnings.Add($"Appoggi: carico medio equivalente {average:0} kN/dispositivo, prezzo {bearingRate:0} €/cad. Verificare portata e corsa: il listino ANAS usa €/kN; nessun dimensionamento dei dispositivi.");
        if (length > 200) warnings.Add("Giunti: il prezzo fisso non segue corsa termica/sismica e lunghezza dilatabile. Per grandi opere può sottostimare il costo.");
        warnings.Add("Computo parziale: scavi, rinterri, drenaggi, protezioni, impermeabilizzazione, centine alte, accessi e sicurezza specifica non computati analiticamente. Oneri aggiuntivi e imprevisti sono riserve, non un computo completo.");
    }
}
