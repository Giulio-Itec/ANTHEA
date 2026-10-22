using System.Text.Json.Nodes;
namespace Anthea.Application.Concrete;

/// <summary>
/// What the shear and torsion tab of the RC section shows (tab 05 «Taglio e torsione», F5.10 sub-step S5): the options that enable the
/// fields of the shear, the note of the automatic parameters, the outputs of the rows after the calculation, the summary of the tab and the
/// detail of the selected combination with its torsion. Moved without changes from the historic view (ConcreteShear.cs, ConcreteTorsion.cs
/// and RefreshAutomaticShear), which calls them; no rule of engineering: the values come from the results of the libraries.
/// </summary>
public static class ConcreteShearPresentation
{
    /// <summary>The detail without a selected combination.</summary>
    public const string NoSelection = "Inserire o selezionare una combinazione N–Vx–Vy.";
    /// <summary>The summaries of the tab after a change of the data, until the calculation.</summary>
    public const string Pending = "Taglio e torsione · da calcolare";
    /// <summary>The detail after a change of the data, until the calculation.</summary>
    public const string DetailPending = "Dati modificati · aggiornamento automatico in attesa";

    /// <summary>The geometric parameters of the shear come from the section («Automatici da sezione», the default).</summary>
    public static bool Automatic(JsonObject shear) => shear.S("parametri", "Automatici da sezione") == "Automatici da sezione";

    /// <summary>The shear model with links («Con staffe»).</summary>
    public static bool WithLinks(JsonObject shear) => shear.S("modello") == "Con staffe";

    /// <summary>The standard of the sheet is the Model Code 2010 (its eccentricities and the anchored Asl with links).</summary>
    public static bool ModelCode(JsonObject settings) => settings.S("normativa") == "Model Code 2010";

    /// <summary>The note of the automatic parameters for the shape of the section.</summary>
    public static string AutomaticNote(string shape) => shape == "Circolare"
        ? "Circolare: bw = D (piena) o D−Di (cava); d dal baricentro delle barre di ciascun semicerchio, minimo fra i due versi. Asl minima dei due semicerchi. Suggerimenti geometrici: scegliere il modello e confermare la schematizzazione, oppure impostare parametri manuali."
        : "Automatici: bw minima (somma delle pareti per sezione cava); d dal baricentro delle barre di lembo, minimo fra i due versi; Asl minima dei due lembi. Per T: Vx usa lo spessore ala, Vy l’anima. Ancoraggio e disposizione resistente restano da verificare.";

    /// <summary>
    /// The outputs of a row after its calculation, in the order written: VRd and η of each direction, the outcome, then the ratios of the
    /// torsion and the outcome with the torsion (a row without torsion keeps the outcome of the shear).
    /// </summary>
    public static IReadOnlyList<(string Key, string Value)> Outputs(ConcreteResults.ShearResult[] results, ConcreteResults.TorsionResult? torsion)
    {
        ArgumentNullException.ThrowIfNull(results);
        var outputs = new List<(string, string)>();
        for (int i = 0; i < results.Length; i++)
        {
            string axis = i == 0 ? "x" : "y";
            outputs.Add(("VRd" + axis, results[i].VRd.ToString("0.00")));
            outputs.Add(("eta_" + axis, results[i].Ratio?.ToString("0.00") ?? "—"));
        }
        string outcome = string.Join(" · ", results.Select((r, i) => (i == 0 ? "x: " : "y: ") + r.Status));
        outputs.Add(("esito", outcome));
        if (torsion is null) { outputs.Add(("eta_t", "—")); outputs.Add(("eta_vt", "—")); return outputs; }
        outputs.Add(("eta_t", torsion.TorsionRatio?.ToString("0.00") ?? "∞"));
        outputs.Add(("eta_vt", torsion.ConcreteCombinedRatio is double concrete && torsion.SteelCombinedRatio is double steel ? Math.Max(concrete, steel).ToString("0.00") : "∞"));
        outputs.Add(("esito", outcome + " · " + torsion.Status));
        return outputs;
    }

    /// <summary>The outputs of a row whose calculation stopped: no value, the error as outcome.</summary>
    public static IReadOnlyList<(string Key, string? Value)> FailedOutputs(string? error) =>
        [("VRdx", "—"), ("VRdy", "—"), ("eta_x", "—"), ("eta_y", "—"), ("eta_t", "—"), ("eta_vt", "—"), ("esito", error)];

    /// <summary>The summary of the shear: combinations calculated, beyond the resistance, without outcome.</summary>
    public static string ShearSummary(int rows, IEnumerable<ConcreteResults.ShearResult[]> shear)
    {
        ArgumentNullException.ThrowIfNull(shear);
        var checks = shear.ToArray();
        return $"Taglio: {checks.Length}/{rows} combinazioni calcolate · {checks.Count(r => r.Any(v => v.Ratio > 1))} oltre resistenza · {checks.Count(r => r.Any(v => v.Ratio is null))} senza esito · dettagli da verificare.";
    }

    /// <summary>The summary under the table: the summary of the shear, then the torsions calculated and not satisfied.</summary>
    public static string Summary(int rows, IEnumerable<ConcreteResults.ShearResult[]> shear, IEnumerable<ConcreteResults.TorsionResult> torsion)
    {
        ArgumentNullException.ThrowIfNull(torsion);
        var torsions = torsion.ToArray();
        return ShearSummary(rows, shear) + $" Torsione: {torsions.Length} calcolate, {torsions.Count(r => !r.Passed)} non soddisfatte.";
    }

    /// <summary>
    /// «Dettagli combinazione» of the selected row: name, model and actions; without results the outcome of the row; otherwise for each
    /// direction the parameters, the resistances (links and strut with the links) and the outcome, then the references and steps of the
    /// checks, the torsion and the note of the circular sections.
    /// </summary>
    public static string Detail(JsonObject row, JsonObject shear, string shape, ConcreteResults.ShearResult[]? checks, ConcreteResults.TorsionResult? torsion)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(shear);
        string text = $"{row.S("nome")} · {shear.S("modello")}\nN = {EngineeringFormat.Number(J.Number(row["N"]))} kN (compressione negativa)\nVx / Vy = {EngineeringFormat.Number(J.Number(row["Vx"]))} / {EngineeringFormat.Number(J.Number(row["Vy"]))} kN\n\n";
        if (checks is null) return text + row.S("esito", "Da calcolare");
        for (int i = 0; i < checks.Length; i++)
        {
            var check = checks[i]; string axis = i == 0 ? "x" : "y";
            text += $"DIREZIONE V{axis}\nbw = {EngineeringFormat.Number(J.Number(shear["bw_" + axis]))} mm · d = {EngineeringFormat.Number(J.Number(shear["d_" + axis]))} mm\n";
            if (shear.S("modello") == "Con staffe") text += $"VRsd (staffe) = {check.VRsd:0.00} kN\nVRcd (puntone) = {check.VRcd:0.00} kN\ncot θ = {check.CotTheta:0.00}\nGoverna: {(check.VRsd <= check.VRcd ? "armatura trasversale" : "calcestruzzo compresso")}\n";
            text += $"VRd = {check.VRd:0.00} kN · η = {check.Ratio?.ToString("0.00") ?? "—"}\n{check.Status}\n\n";
        }
        foreach (var check in checks) text += check.Reference + "\n" + string.Join("\n", check.Details.Select(d => d.Format())) + "\n\n";
        text += TorsionSummary(row, torsion);
        if (shape == "Circolare") text += "\nTaglio circolare · " + shear.S("modello_circolare") + " · verificare la schematizzazione assegnata.";
        return text;
    }

    /// <summary>The torsion of the row in the detail (NTC §4.1.2.3.6): geometry, resistances, ratios, outcome; empty without torsion.</summary>
    public static string TorsionSummary(JsonObject row, ConcreteResults.TorsionResult? r)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (r is null) return "";
        return $"\nTORSIONE · NTC §4.1.2.3.6\nTEd = {row.S("T")} kNm\nAk = {r.Geometry?.Area:0.###} mm² · uk = {r.Geometry?.Perimeter:0.###} mm · t = {r.Geometry?.Thickness:0.###} mm · cot θ = {r.CotTheta:0.###}\nTRcd = 2 Ak t (0,5 fcd) cot θ/(1+cot²θ)\nTRsd = 2 Ak (Asta/s) fyd cot θ · Asta = area di un ramo\nTRld = 2 Ak (As,l/uk) fyd / cot θ\nTRcd / TRsd / TRld = {r.TRcd:0.###} / {r.TRsd:0.###} / {r.TRld:0.###} kNm\nTRd = {r.TRd:0.###} kNm · ηT = {r.TorsionRatio?.ToString("0.###") ?? "non definito (resistenza nulla)"}\nAs,l richiesta per sola torsione = {r.RequiredLongitudinalArea:0.###} mm²\nη calcestruzzo = |T|/TRcd + |Vx|/VRcd,x + |Vy|/VRcd,y = {r.ConcreteCombinedRatio:0.###}\nη staffe = |T|/TRsd + max(|Vx|/VRsd,x; |Vy|/VRsd,y) = {r.SteelCombinedRatio:0.###}\n{r.Status}\nLa quota longitudinale deve essere aggiuntiva a quella impegnata in N–M.\n";
    }
}
