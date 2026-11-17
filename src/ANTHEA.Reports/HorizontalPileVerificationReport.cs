using System.Globalization;
using System.Text.Json.Nodes;
using X.Core;

namespace Anthea.Reports;

/// <summary>Presentation of the capacity check already evaluated by the calculation engine.</summary>
public static class HorizontalPileVerificationReport
{
    public const string Title = "Verifica della capacità portante orizzontale";
    public const string Introduction = "La verifica confronta l’azione orizzontale di progetto HEd con la resistenza di progetto Rd. La resistenza caratteristica Rk deriva dal minore tra la capacità media divisa per ξ₃ e la capacità minima divisa per ξ₄; Rd si ottiene applicando a Rk l’efficienza η e il coefficiente parziale γR.";
    public const string MissingResistance = "Rk e Rd non determinate: verifica della capacità portante orizzontale non eseguibile.";
    public static bool Available(JsonObject result) => J.Number(result["resistenza_progetto_manuale_kn"]) is > 0 && J.Number(result["azione_kn"]) is not null;
    public static string Outcome(JsonObject result) => Available(result) ? result.S("esito_manuale", "Esito non disponibile") : "Verifica non eseguibile";
    public static Tabella Table(JsonObject result)
    {
        static string Number(JsonNode? value) => J.Number(value) is double n ? n.ToString("G5", CultureInfo.GetCultureInfo("it-IT")) : "—";
        return new(Title, ["Rk [kN]", "Rd [kN]", "HEd [kN]", "HEd/Rd", "Esito"],
            [[Number(result["resistenza_caratteristica_manuale_kn"]), Number(result["resistenza_progetto_manuale_kn"]), Number(result["azione_kn"]),
                Available(result) ? Number(result["utilizzo_manuale"]) : "—", Outcome(result)]]);
    }
}
