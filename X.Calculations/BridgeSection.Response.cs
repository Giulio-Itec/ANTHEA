using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using GPC.Checkers.CompositeBridge.History;

namespace Anthea.Calculations;

public static partial class BridgeSection
{
    public static readonly string[] ResponseModes = ["Momento–curvatura · N costante", "Forza–deformazione · κ costante"];
    public static readonly string[] ResponseOrigins = ["Sezione composta vergine", "Dopo una fase dello storico"];
    public static JsonObject ResponseDefaults() => new() { ["tipo"] = ResponseModes[0], ["origine"] = ResponseOrigins[0], ["fase"] = 0,
        ["incremento_k"] = .003, ["incremento_e"] = -2000, ["punti"] = 100, ["sottopassi"] = 4,
        ["n_storico"] = true, ["k_storico"] = true, ["N"] = 0, ["k"] = 0, ["y"] = 0 };

    public static SectionResponseResult CalculateResponse(JsonObject data, JsonObject request, CancellationToken cancellation = default)
    {
        string mode = request.S("tipo"), origin = request.S("origine");
        if (!ResponseModes.Contains(mode) || !ResponseOrigins.Contains(origin)) throw new ArgumentException("Tipo o origine della curva sconosciuto.");
        bool mc = mode == ResponseModes[0], history = origin == ResponseOrigins[1];
        int Integer(string key, int min, int max)
        {
            double value = request.Required(key, min);
            if (value != Math.Truncate(value) || value > max) throw new ArgumentException(key + ": intero da " + min + " a " + max + ".");
            return (int)value;
        }
        var input = ToCheckerInput(data); input = input with { Options = input.Options with { Class4 = false } };
        var nonlinearData = (JsonObject)data.DeepClone(); nonlinearData["metodo_analisi"] = CalculationMethods[2];
        var settings = HistoryOptions(nonlinearData);
        var control = new SectionResponseOptions { Control = mc ? SectionResponseControl.MomentCurvature : SectionResponseControl.AxialForceStrain,
            ReferenceY = request.Required("y", double.NegativeInfinity),
            AxialForce = mc ? request.B("n_storico") ? null : request.Required("N", double.NegativeInfinity) * 1000 : null,
            FixedCurvature = !mc ? request.B("k_storico") ? null : request.Required("k", double.NegativeInfinity) / 1000 : null,
            SubstepsPerTarget = Integer("sottopassi", 1, 1000) };
        double delta = request.Required(mc ? "incremento_k" : "incremento_e", double.NegativeInfinity) * (mc ? .001 : 1e-6);
        if (delta == 0) throw new ArgumentException("L'incremento finale della curva deve essere diverso da zero.");
        return HBridgeSectionResponse.Trace(input, delta, Integer("punti", 1, 5000), control, settings, history ? Integer("fase", 0, int.MaxValue) : null, cancellation);
    }

    public static string ResponseCsv(SectionResponseResult result)
    {
        var text = new StringBuilder("Stato;Punto;Obiettivo_raggiunto;epsilon_rif;curvatura_1_mm;N_N;M0_Nmm;Mriferimento_Nmm;y_rif_mm;Residuo_N_N;Componente;Fibra;y_mm;Attiva;epsilon_tot;epsilon_getto;epsilon_imposta;epsilon_meccanica;sigma_MPa;epsilon_plastica;epsilon_plastica_accumulata\n");
        string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        foreach (var p in result.Points)
            foreach (var f in p.State.Fibers)
            {
                var plastic = f.MaterialState as HistoryPlasticState;
                text.AppendLine(string.Join(";", Q(result.Stop.ToString()), p.Index, p.ReachedTarget, F(p.ReferenceStrain), F(p.State.TotalPlane.Curvature), F(p.State.N),
                    F(p.State.MomentAtOrigin), F(p.MomentAtReference), F(result.Options.ReferenceY), F(p.State.ForceResidual), Q(f.ComponentId), Q(f.Fiber.Id), F(f.Fiber.Y), f.Active,
                    F(f.TotalStrain), F(f.ActivationStrain), F(f.ImposedStrain), F(f.MechanicalStrain), F(f.Stress), plastic is null ? "" : F(plastic.PlasticStrain), plastic is null ? "" : F(plastic.AccumulatedPlasticStrain)));
            }
        return text.ToString();
    }
}
