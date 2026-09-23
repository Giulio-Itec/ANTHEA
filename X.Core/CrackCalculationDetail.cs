using System.Globalization;
using System.Text;

namespace X.Core;

/// <summary>Actual calculation values, captured before display rounding.</summary>
public sealed record CrackCalculationDetail(string Symbol, double? Value, string Unit, string Expression, string Note = "")
{
    public string Format()
    {
        if (Value is not double v) return $"{Symbol}: {Expression}" + (Note.Length > 0 ? "\n  " + Note : "");
        string number = v.ToString("G10", CultureInfo.GetCultureInfo("it-IT"));
        string unit = Unit == "−" ? "[−]" : Unit;
        return $"{Symbol} = {number}{(unit.Length > 0 ? " " + unit : "")}\n  {Expression}" + (Note.Length > 0 ? "\n  " + Note : "");
    }
}

public static class CrackCalculationSummary
{
    public const int MaxValues = 30;

    /// <summary>Presentation only: the full, unrounded diagnostic trace remains in Details.</summary>
    public static CrackCalculationDetail[] Values(Ntc2018Checks.CrackResult result)
    {
        var source = result.Details.GroupBy(d => d.Symbol).ToDictionary(g => g.Key, g => g.Last());
        var selected = new List<CrackCalculationDetail>();
        void Add(string key, string description, string? alternate = null)
        {
            if (!source.TryGetValue(key, out var value) && (alternate is null || !source.TryGetValue(alternate, out value))) return;
            if (value.Value.HasValue) selected.Add(value with { Symbol = key, Expression = description, Note = "" });
        }
        if (source.ContainsKey("σct,lim"))
        {
            Add("fctm", "Resistenza media a trazione");
            Add("σct,max", "Massima tensione di trazione nel CLS");
            Add("σct,lim", "Limite di decompressione / formazione");
            Add("Divisore formazione", "Divisore della resistenza a trazione");
        }
        else
        {
            Add("h", "Altezza nella direzione della deformazione");
            Add("d", "Altezza utile delle armature tese");
            Add("x", "Profondità dell'asse neutro");
            Add("hc,eff", "min[2,5(h−d); (h−x)/3; h/2] · Fig. C4.1.10");
            Add("Ac,eff", "Area efficace di CLS teso · Fig. C4.1.10");
            Add("As,eff", "Area delle barre tese nella fascia efficace");
            Add("ρp,eff", "As,eff / Ac,eff");
            Add("c", "Copriferro adottato");
            Add("Øeq", "Diametro equivalente · C4.1.8");
            Add("s", "Interasse delle barre tese adottato");
            Add("Es", "Modulo elastico dell'acciaio");
            Add("Ecm", "Modulo elastico medio del CLS");
            Add("fct,eff = fctm", "Resistenza efficace a trazione: assunzione adottata");
            Add("σs", "Massima tensione nelle barre efficaci");
            Add("αe", "Es / Ecm, distinto da n dell'analisi viscosa");
            Add("kt", "Coefficiente di durata del carico");
            Add("k₁", "Coefficiente di aderenza · C4.1.7");
            Add("k₂", "Criterio k₂ adottato: barre compresse → flessione; altrimenti trazione", "Criterio k₂");
            Add("k₃", "Coefficiente del copriferro · C4.1.7");
            Add("k₄", "Coefficiente dell'armatura · C4.1.7");
            Add("Δε calcolata", "[σs − kt·fct,eff·(1+αe·ρp,eff)/ρp,eff] / Es");
            Add("Δε minima", "0,6·σs / Es");
            Add("εsm − εcm", "max[Δε calcolata; Δε minima]");
            Add("s_lim", "5·(c + Øeq/2): soglia di interasse");
            Add("Δsm,vicino", "(k₃·c + k₁·k₂·k₄·Øeq/ρp,eff) / 1,7 · C4.1.7");
            Add("Δsm,distante", "0,75·(h−x) · C4.1.10");
            Add("Δsm adottata", "s ≤ s_lim: vicino; altrimenti max[vicino; distante]");
        }
        if (result.Width is double width) selected.Add(new("wk", width, "mm", "1,7·Δsm·(εsm−εcm): apertura calcolata"));
        if (result.Limit is double limit) selected.Add(new("wlim", limit, "mm", "Limite per ambiente e combinazione"));
        if (result.Ratio is double ratio) selected.Add(new("ηw", ratio, "−", "Tasso di lavoro della verifica"));
        return selected.ToArray();
    }

    public static string Number(double? value) => EngineeringFormat.Number(value);

    public static string Format(Ntc2018Checks.CrackResult? result, string fallback = "Fessurazione non calcolata")
    {
        if (result is null) return fallback;
        var text = new StringBuilder();
        text.AppendLine(result.Status);
        text.AppendLine("Riepilogo essenziale · NTC 2018 e Circolare 2019 § C4.1.2.2.4.5");
        text.AppendLine("Due decimali; notazione scientifica per valori molto piccoli. Deformazioni adimensionali. Traccia completa nel JSON.");
        text.AppendLine();
        foreach (var detail in Values(result))
            text.AppendLine($"{detail.Symbol} = {Number(detail.Value)} {detail.Unit} · {detail.Expression}");
        if (result.Details.Length == 0) text.AppendLine("Passaggi numerici non disponibili per questa verifica.");
        return text.ToString();
    }
}
