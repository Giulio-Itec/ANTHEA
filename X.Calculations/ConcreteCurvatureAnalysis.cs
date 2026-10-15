using System.Collections.Immutable;
using System.Text.Json.Nodes;
using GPC.Model.Materials;

namespace Anthea.Calculations;

/// <summary>Shared section M–χ path; WPF only schedules, renders and exports the result.</summary>
public static class ConcreteCurvatureAnalysis
{
    /// <summary>Chiave del campo «CLS teso nelle risposte» nelle opzioni del M–χ (workspace_ca.momento_curvatura), dal 16/10/2026.</summary>
    public const string ResponseConcreteTensionKey = "cls_teso_risposte";
    /// <summary>
    /// Scelte di «CLS teso nelle risposte»: «Trascurato» (predefinito, anche per i fogli salvati prima del 16/10/2026, senza la chiave) o
    /// «Tension stiffening» (<see cref="ConcreteMaterial.TensionResponseDiagrams.TensionStiffening"/> nell'opzione
    /// StressAnalysisConcreteTension del verificatore di GPCChecker.Concrete). Vale solo per i punti della curva M–χ: mai nelle verifiche SLE
    /// e SLU, che costruiscono il verificatore senza l'opzione.
    /// </summary>
    public static readonly ImmutableArray<string> ResponseConcreteTensionModes = ["Trascurato", "Tension stiffening"];
    public const string ResponseConcreteTensionHelp = "Solo nelle analisi di risposta (curva M–χ): il calcestruzzo teso segue la legge di Collins e Mitchell (1991), "
        + "lineare con Ecm fino a fctm, poi σ = fctm/(1 + √(500 ε)); valori medi, senza coefficienti parziali. Migliora la convergenza; non cambia il punto "
        + "limite né le resistenze SLU, che trascurano la trazione (NTC §4.1.2.3.4.1, EN 1992-1-1 6.1(2)P). Applicata a tutta la zona tesa: nelle sezioni "
        + "poco armate la risposta è molto più rigida e lo snervamento arriva tardi.";
    /// <summary>Frase aggiunta allo stato della curva quando l'opzione è attiva.</summary>
    public const string ResponseConcreteTensionStatus = "CLS teso nelle risposte: tension stiffening (Collins e Mitchell 1991, Ecm e fctm medi); "
        + "punto limite dal dominio, senza trazione.";

    /// <summary>
    /// Scelta del foglio → diagramma della libreria. La chiave assente vale «Trascurato»: è letta senza J.S, perché il predefinito di una
    /// chiave nuova è dichiarato (aiuto e registro) e non è un ripiego da tracciare nelle catture. Un valore sconosciuto è rifiutato.
    /// </summary>
    public static ConcreteMaterial.TensionResponseDiagrams ResponseConcreteTension(JsonObject options)
    {
        if (options[ResponseConcreteTensionKey] is not JsonNode node) return ConcreteMaterial.TensionResponseDiagrams.Default;
        string text = node.ToString();
        if (text == ResponseConcreteTensionModes[0]) return ConcreteMaterial.TensionResponseDiagrams.Default;
        if (text == ResponseConcreteTensionModes[1]) return ConcreteMaterial.TensionResponseDiagrams.TensionStiffening;
        throw new ArgumentException("CLS teso nelle risposte: scegliere «Trascurato» o «Tension stiffening».");
    }

    public static MomentCurvatureResult Calculate(JsonObject input, JsonObject workspace, JsonObject options,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (workspace.Array("trefoli").Count > 0)
            throw new ArgumentException("M–χ con trefoli: definire prima il criterio di snervamento/predeformazione; disponibile per armatura ordinaria.");
        var o = (JsonObject)options.DeepClone();
        var request = new MomentCurvatureRequest(SectionWorkspace.Number(o.S("N"), "N"),
            SectionWorkspace.Number(o.S("theta"), "θ"), SectionWorkspace.Subdivisions(o.S("passi"), "Passi", 10, 500),
            SectionWorkspace.Number(o.S("frazione"), "Frazione"), o.S("campionamento") == "Quadratico",
            SectionWorkspace.Number(o.S("tolleranza_n"), "Tolleranza N"),
            SectionWorkspace.Subdivisions(o.S("raffina_snervamento"), "Bisezioni", 0, 30));
        var tension = ResponseConcreteTension(o);
        o["criterio"] = "N costante"; o["assi"] = "Locali"; o["modello"] = "Non lineare"; o["strategia"] = "Iterativo";
        // «CLS teso nelle risposte» (16/10/2026): solo le analisi tensionali dei punti della curva lo usano; il dominio, da cui viene il punto
        // limite, non legge l'opzione (pagina ca.momento-curvatura, riquadro M-3).
        var engine = new CheckerSection(input, workspace, o, responseConcreteTension: tension);
        // Tolleranza su N del punto limite (SLU-3): almeno la regola della libreria per il legame e la scala della sezione, come la
        // resistenza a N assegnato (SectionMomentResistance, F2-15); la tolleranza dell'utente vale se maggiore. Un valore non valido
        // resta all'analisi, che lo rifiuta come prima.
        double assigned = request.AxialToleranceKn;
        if (double.IsFinite(assigned) && assigned > 0)
            request = request with { AxialToleranceKn = Math.Max(assigned, SectionMomentResistance.AxialToleranceKn(engine, request.AxialKn)), AssignedAxialToleranceKn = assigned };
        var domain = engine.Domain3D(token);
        var result = new MomentCurvatureCalculator().Calculate(request, domain.Check,
            a => engine.Stress(a, "CURVA"), engine.Geometry.Fyd / engine.Geometry.Es, token);
        return tension == ConcreteMaterial.TensionResponseDiagrams.Default ? result
            : result with { Status = result.Status + " " + ResponseConcreteTensionStatus };
    }
}
