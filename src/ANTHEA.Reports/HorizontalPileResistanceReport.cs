using System.Globalization;
using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Reports;

/// <summary>Readable projections of the calculated capacity and section; no resistance is recalculated here.</summary>
public static class HorizontalPileResistanceReport
{
    public const string CapacityTitle = "Capacità portante e meccanismo governante";
    public const string ConcreteTitle = "Sezione resistente in calcestruzzo armato";
    public const string SteelTitle = "Sezione resistente del tubolare in acciaio";
    public const string DiagnosticsTitle = "Controlli numerici della sezione";
    public const string ConcreteNote = "La duttilità delle cerniere plastiche resta da verificare.";
    public const string ConcreteBrief = "Resistenza flessionale valutata a N costante, adottando il minore dei momenti resistenti nei due versi di flessione. " + ConcreteNote;
    public static bool IsTable(string title) => title is CapacityTitle or ConcreteTitle or SteelTitle or DiagnosticsTitle;
    static string Number(JsonNode? value, string format = "#,0.###") => J.Number(value) is double n ? n.ToString(format, CultureInfo.GetCultureInfo("it-IT")) : "—";
    public static Tabella Capacity(JsonObject result) => new(CapacityTitle, ["Grandezza", "Valore", "Unità"],
        [["Capacità ultima orizzontale Hu", Number(result["capacita_kn"]), "kN"],
         ["Sondaggio governante", Number(result["sondaggio_governante"]), "—"],
         ["Meccanismo governante", result.S("meccanismo"), "—"],
         ["Momento resistente adottato My", Number(result["momento_resistente_knm"]), "kNm"]]);
    public static Tabella Section(JsonObject section)
    {
        List<string[]> rows = [["Sforzo normale N (compressione positiva)", Number(section["n_kn"]), "kN"]];
        if (section.S("tipo") == "CHS")
        {
            rows.Add(["Resistenza di progetto dell’acciaio fyd", Number(section["fyd_mpa"]), "MPa"]);
            rows.Add(["Classe della sezione", Number(section["classe"]), "—"]);
        }
        else
        {
            rows.Add(["Resistenza di progetto del calcestruzzo fcd", Number(section["fcd_mpa"]), "MPa"]);
            rows.Add(["Resistenza di progetto dell’acciaio fyd", Number(section["fyd_mpa"]), "MPa"]);
            rows.Add(["Area dell’armatura longitudinale As", Number(section["area_acciaio_mm2"]), "mm²"]);
        }
        return new(section.S("tipo") == "CHS" ? SteelTitle : ConcreteTitle, ["Grandezza", "Valore", "Unità"], rows);
    }
    public static string SectionDescription(JsonObject section) => section.S("tipo") == "CHS"
        ? "Il momento resistente è valutato per il tubolare in acciaio, senza il contributo del riempimento. La riduzione dovuta allo sforzo normale assegnato è determinata mediante un’interazione lineare N–M. La resistenza dell’acciaio deve essere coerente con il materiale e lo spessore del tubolare."
        : "Il momento resistente è determinato dall’analisi della sezione a sforzo normale costante, considerando la geometria, l’armatura e i legami costitutivi dei materiali assegnati. Si adotta il minore dei momenti resistenti nei due versi della flessione.";
    public static string SectionNote(JsonObject section) => section.S("tipo") == "CHS"
        ? "Restano da verificare l’instabilità globale, il taglio, i giunti, gli effetti della corrosione e la capacità di rotazione delle connessioni."
        : ConcreteNote;
    public static Tabella Diagnostics(JsonObject section) => new(DiagnosticsTitle, ["Grandezza", "Valore", "Unità"],
        [["Residuo dell’equilibrio assiale", Number(section["residuo_n_kn"], "G5"), "kN"],
         ["Tolleranza dell’equilibrio assiale", Number(section["tolleranza_n_kn"], "G5"), "kN"],
         ["Discretizzazione del contorno", Number(section["lati_contorno"]), "lati"],
         ["Motore di calcolo", section.S("motore", "Non disponibile"), "—"]]);
}
