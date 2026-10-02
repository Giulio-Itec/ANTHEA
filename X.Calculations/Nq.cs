using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Piles;

namespace Anthea.Calculations;

/// <summary>
/// NQ-2026-09-09 in GPCChecker.Geotechnics (BearingCapacityFactors): φ in gradi verso la libreria in radianti, risultato nel formato del foglio.
/// Nessuna estrapolazione.
/// </summary>
public static class Nq
{
    public const string Versione = BearingCapacityFactors.Version;
    public const string Descrizione = BearingCapacityFactors.Description;
    public static readonly double[] RapportiMedio = BearingCapacityFactors.MediumRatios.ToArray();
    public static readonly double[] RapportiGrande = BearingCapacityFactors.LargeRatios.ToArray();
    public static double Curva(double phi, int index, bool grande) => BearingCapacityFactors.Curve(phi, index, grande);
    public static JsonObject Dettaglio(double phi, double rapporto, bool grande)
    {
        if (!double.IsFinite(phi) || !double.IsFinite(rapporto) || rapporto <= 0) throw new ArgumentException("Nq: φ e z/D devono essere finiti, z/D positivo.");
        return Json(BearingCapacityFactors.Nq(phi * Math.PI / 180, rapporto, grande));
    }
    /// <summary>Il fattore della libreria come dettaglio del foglio (φ in gradi).</summary>
    public static JsonObject Json(NqResult r) => J.Obj(("versione", Versione), ("fattore", r.Factor), ("phi", r.FrictionAngleDegrees), ("rapporto", r.Slenderness),
        ("rapporto_adottato", r.AdoptedSlenderness), ("r1", r.Ratio1), ("r2", r.Ratio2), ("phi1", r.FrictionAngle1), ("phi2", r.FrictionAngle2), ("n1", r.Value1), ("n2", r.Value2),
        ("t", r.Weight), ("nq", r.Nq), ("limite_phi", r.FrictionAngleClipped), ("limite_rapporto", r.SlendernessClipped));
    public static string MetodoPrecedente(JsonNode? g)
    {
        var method = g.S("metodo_nq","Originale");
        if (method is not ("Originale" or "Raffinata" or "Parametrizzata")) throw new ArgumentException("Metodo Nq non riconosciuto nel documento.");
        return method == "Parametrizzata" ? g.S("metodo_nq_precedente") : method;
    }
}
