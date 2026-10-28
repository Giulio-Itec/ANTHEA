using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Presentation;

namespace Anthea.Application.Concrete;

/// <summary>Un riquadro dei risultati della colonna modello: un valore (<see cref="Passed"/> null, blu) o una verifica (verde o rosso).</summary>
public sealed record ConcreteModelColumnCard(string Title, string Value, string Explanation, string Reference, bool IsCheck, bool? Passed);

/// <summary>
/// La scheda «Colonna modello» della sezione c.a. (1.15.0): messaggi, firma dei dati, riquadri e testo dei risultati, grafici M(χ) con la retta
/// del momento esterno e M − M2, CSV dei punti. Solo presentazione dei valori della libreria: le rette del grafico usano M2 = k·χ dei punti.
/// </summary>
public static class ConcreteModelColumnPresentation
{
    /// <summary>Il messaggio durante il calcolo.</summary>
    public const string Running = "Calcolo della colonna modello (curva a curvatura imposta)…";
    /// <summary>Il messaggio dopo una modifica dei dati.</summary>
    public const string Stale = "Dati modificati · ricalcolare la colonna modello.";
    /// <summary>Il messaggio di un calcolo interrotto.</summary>
    public const string Interrupted = "Calcolo interrotto; nessun risultato parziale pubblicato.";
    /// <summary>La nota sotto il form.</summary>
    public const string Notice = "Pilastro isolato, sezione e N costanti, sezione critica unica: e2 = χ·l0²/c, M2 = NEd·e2 (EN 1992-1-1 5.8.6(6) e 5.8.8.2; "
        + "NTC 2018 §4.1.2.3.9.3). Curvatura dalla risposta della sezione a N costante con i legami di progetto, calcestruzzo teso trascurato, deformazioni "
        + "del calcestruzzo moltiplicate per (1 + φef). M0Ed è il momento del primo ordine nella sezione critica, senza imperfezione, nella direzione θ "
        + "(assi locali); vuoto: solo capacità. L'eccentricità minima vale come minimo del momento totale, non si somma all'imperfezione.";
    /// <summary>Il file proposto da «Esporta CSV…».</summary>
    public const string CsvFileName = "ANTHEA_Colonna_modello.csv";
    /// <summary>Titoli e assi dei due grafici.</summary>
    public const string CapacityTitle = "M(χ) a N costante e momento esterno", FirstOrderTitle = "Capacità del primo ordine M − M2",
        XLabel = "Curvatura χ [1/m]", CapacityYLabel = "Momento M [kNm]", FirstOrderYLabel = "M − M2 [kNm]",
        EmptyMessage = "Inserire N, l0 e l, poi «Calcola»";
    /// <summary>Le categorie delle figure della relazione (nel capitolo della colonna modello, con la scelta «curvatura»).</summary>
    public const string FigureCategory = "colonna_modello";

    private const string Blue = "#0B5CAD", Red = "#C62828", Green = "#16703B", Grey = "#64748B", Orange = "#B45309";

    /// <summary>Il messaggio di una colonna non calcolabile.</summary>
    public static string Unavailable(string error) => "Colonna modello non disponibile: " + error;

    /// <summary>I dati da cui dipende il risultato: ingressi, trefoli, coefficienti, norma e opzioni della colonna (stessa firma: nessun nuovo calcolo).</summary>
    public static string Signature(JsonObject input, JsonObject workspace)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(workspace);
        return input.ToJsonString() + workspace["trefoli"]?.ToJsonString() + workspace["coefficienti"]?.ToJsonString() + workspace["normativa"]?.ToJsonString()
            + workspace[ConcreteModelColumn.OptionsKey]?.ToJsonString();
    }

    private static string F(double value, string format = "0.##") => value.ToString(format, CultureInfo.CurrentCulture);
    private static string F(double? value, string format = "0.##") => value is double v ? F(v, format) : "—";
    private static string Curvature(double? value) => F(value, "G4");
    private static string YesNo(bool? value) => value is null ? "non noto" : value.Value ? "sì" : "no";

    /// <summary>I riquadri dei risultati: snellezza, momento del primo ordine, viscosità, capacità, verifica, curvatura nominale, avvisi.</summary>
    public static IReadOnlyList<ConcreteModelColumnCard> Cards(ConcreteModelColumnResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var cards = new List<ConcreteModelColumnCard>();
        var s = r.Slenderness; var f = r.FirstOrder; var i = r.Imperfection; var c = r.Creep; var q = r.Capacity; var n = r.Nominal;
        cards.Add(new("Snellezza", "λ = l0/i = " + F(s.Lambda) + " · λlim = " + F(s.Limit),
            "i = " + F(r.Section.RadiusOfGyration) + " mm; " + s.Text + " (" + s.Reference + "). Il calcolo si esegue comunque.", "", false, null));
        string imperfection = "Imperfezione " + i.Kind + ": ei = " + F(i.Eccentricity, "0.#") + " mm, NEd·ei = " + F(i.MomentKnm) + " kNm";
        string minimum = f.MinimumEccentricity is null ? "eccentricità minima: nessuna"
            : "eccentricità minima " + f.MinimumRule + ": e0 = " + F(f.MinimumEccentricity, "0.#") + " mm, NEd·e0 = " + F(f.MinimumKnm) + " kNm"
                + (f.MinimumGoverns ? ", governa" : "");
        cards.Add(new("Momento del primo ordine", f.TotalKnm is null ? "Solo capacità (M0Ed vuoto)" : "M0Ed,tot = " + F(f.TotalKnm) + " kNm"
            + (f.AppliedKnm is null ? "" : " (M0Ed = " + F(f.AppliedKnm) + " kNm)"), imperfection + "; " + minimum + ".", "", false, null));
        string conditions = "condizioni di EN 1992-1-1 5.8.4(4) per φef = 0: φ(∞, t0) ≤ 2 " + YesNo(c.FinalCoefficientCondition) + ", λ ≤ 75 "
            + YesNo(c.SlendernessCondition) + ", M0Ed/NEd ≥ h " + YesNo(c.EccentricityCondition) + (c.NeglectAdmissible ? ": dimostrate" : ": non dimostrate");
        cards.Add(new("Viscosità", "φef = " + F(c.EffectiveRatio, "0.###"), c.Kind + (c.FinalCoefficient is null ? "" : "; φ(∞, t0) = " + F(c.FinalCoefficient))
            + (c.MomentRatio is null ? "" : "; M0Eqp,tot/M0Ed,tot = " + F(c.MomentRatio, "0.###")) + "; " + conditions + ".", "", false, null));
        cards.Add(new("Capacità", "M1Rd,max = " + F(q.MaximumFirstOrderKnm) + " kNm · modo: " + q.Mode,
            "χ* = " + Curvature(q.CurvatureAtMaximum) + " 1/m, e2* = " + F(q.DeflectionAtMaximum, "0.#") + " mm, M2* = " + F(q.SecondOrderAtMaximumKnm) + " kNm; χy = "
            + (q.YieldCurvature is null ? "non raggiunta" : Curvature(q.YieldCurvature) + " 1/m") + ", χu = " + Curvature(q.UltimateCurvature) + " 1/m, M(χu) = "
            + F(q.UltimateMomentKnm) + " kNm" + (q.MaximumAppliedKnm is null ? "" : "; M0Ed massimo con imperfezione e minimo = " + F(q.MaximumAppliedKnm) + " kNm") + ".",
            "", false, null));
        if (r.Equilibrium is { } e)
            cards.Add(new("Verifica della colonna modello", "η = M0Ed,tot/M1Rd,max = " + F(e.Ratio, "0.###") + (e.Found ? " · MEd = " + F(e.DesignKnm) + " kNm" : ""),
                e.Found ? "Equilibrio: χeq = " + Curvature(e.Curvature) + " 1/m, e2 = " + F(e.Deflection, "0.#") + " mm, M2 = " + F(e.SecondOrderKnm) + " kNm, MEd = M0Ed,tot + M2."
                    : "M0Ed,tot oltre M1Rd,max: nessuna configurazione di equilibrio (instabilità).",
                "EN 1992-1-1 5.8.6(6) e 5.8.8.2; " + r.Profile, true, e.Verified));
        cards.Add(new("Curvatura nominale (confronto, EN 1992-1-1 5.8.8)", n.DesignKnm is null
                ? "MRd(N) − M2 = " + F(n.MaximumFirstOrderKnm) + " kNm" : "MEd = " + F(n.DesignKnm) + " kNm · MRd(N) = " + F(n.ResistingKnm) + " kNm",
            "Kr = " + F(n.Kr, "0.###") + ", Kφ = " + F(n.Kphi, "0.###") + ", 1/r = " + Curvature(n.Curvature) + " 1/m, e2 = " + F(n.Deflection, "0.#") + " mm, M2 = "
            + F(n.SecondOrderKnm) + " kNm" + (n.Ratio is null ? "" : ", MEd/MRd = " + F(n.Ratio, "0.###")) + ". Informativo: l'esito è quello della colonna modello.",
            "", false, null));
        foreach (string warning in r.Warnings) cards.Add(new("Avviso", warning, "", "", false, null));
        return cards;
    }

    /// <summary>Il testo dei risultati: stato, avvisi, passaggi della libreria e tabella dei punti (numeri nella cultura corrente).</summary>
    public static string Text(ConcreteModelColumnResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var b = new StringBuilder(r.Status).AppendLine().AppendLine();
        b.AppendLine("Esito: " + r.Outcome);
        foreach (string warning in r.Warnings) b.AppendLine("Avviso: " + warning);
        b.AppendLine().AppendLine("Passaggi");
        foreach (var step in r.Trace)
            b.AppendLine(step.Description + ": " + step.Formula + (step.Value is double v ? " = " + v.ToString("G6", CultureInfo.CurrentCulture) + " " + step.Unit : "")
                + (step.Reference.Length > 0 ? " [" + step.Reference + "]" : ""));
        b.AppendLine().AppendLine("χ [1/m]   M [kNm]   M2 [kNm]   M − M2 [kNm]   ε0 [‰]   εc [‰]   εs [‰]");
        foreach (var p in r.Points)
            b.AppendLine(string.Join("   ", new[] { p.Curvature, p.MomentKnm, p.SecondOrderKnm, p.FirstOrderCapacityKnm, p.ReferenceStrain, p.ConcreteCompressionStrain, p.SteelStrain }
                .Select(x => x.ToString("G6", CultureInfo.CurrentCulture))) + Flags(p));
        return b.ToString();
    }

    private static string Flags(ConcreteModelColumnResult.Point p)
        => (p.Maximum ? " · massimo" : "") + (p.Yielded ? " · snervata" : "") + (p.Ultimate ? " · ultimo" : "");

    private static List<double[]> Points(ConcreteModelColumnResult r, Func<ConcreteModelColumnResult.Point, double> y)
        => r.Points.Select(p => new[] { p.Curvature, y(p) }).ToList();

    /// <summary>
    /// Il grafico M(χ): la curva della sezione a N costante, la retta del momento esterno M0Ed,tot + k·χ (con M0Ed), la tangente al massimo
    /// M1Rd,max + k·χ e la retta M2 = k·χ, con i punti del massimo, dell'equilibrio, del primo snervamento e della curvatura ultima.
    /// </summary>
    public static PlotData CapacityChart(ConcreteModelColumnResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var q = r.Capacity;
        var chart = new PlotData { Title = CapacityTitle, XLabel = XLabel, YLabel = CapacityYLabel, XMinimum = 0,
            Note = "k = NEd·l0²/c = " + F(r.SecondOrderStiffness, "G5") + " kN·m²; l0 = " + F(r.EffectiveLength) + " m, c = " + F(r.CurvatureFactor) };
        chart.Series.Add(new("M(χ)", Points(r, p => p.MomentKnm), Blue));
        if (r.Equilibrium is { } e) chart.Series.Add(new("M0Ed,tot + k·χ", Points(r, p => e.FirstOrderKnm + p.SecondOrderKnm), Red, true, DashPattern: "Dash"));
        chart.Series.Add(new("M1Rd,max + k·χ (tangente al massimo)", Points(r, p => q.MaximumFirstOrderKnm + p.SecondOrderKnm), Green, true, DashPattern: "Dash"));
        chart.Series.Add(new("M2 = k·χ", Points(r, p => p.SecondOrderKnm), Grey, true, DashPattern: "Dot"));
        chart.Markers.Add(new(q.CurvatureAtMaximum, q.MomentAtMaximumKnm, "M1Rd,max = " + F(q.MaximumFirstOrderKnm) + " kNm", Green));
        if (r.Equilibrium is { Found: true, Curvature: double chi, DesignKnm: double moment }) chart.Markers.Add(new(chi, moment, "MEd = " + F(moment) + " kNm", Red));
        if (q.YieldCurvature is double yield && q.YieldMomentKnm is double my) chart.Markers.Add(new(yield, my, "χy", Orange, Radius: 4));
        chart.Markers.Add(new(q.UltimateCurvature, q.UltimateMomentKnm, "χu", Grey, Radius: 4));
        return chart;
    }

    /// <summary>Il grafico M − M2 in funzione di χ con il massimo M1Rd,max (punto e retta orizzontale) e, con M0Ed, la retta M0Ed,tot.</summary>
    public static PlotData FirstOrderChart(ConcreteModelColumnResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        var q = r.Capacity;
        var chart = new PlotData { Title = FirstOrderTitle, XLabel = XLabel, YLabel = FirstOrderYLabel, XMinimum = 0,
            Note = "M1Rd,max = " + F(q.MaximumFirstOrderKnm) + " kNm a χ* = " + Curvature(q.CurvatureAtMaximum) + " 1/m (" + q.Mode + ")" };
        chart.Series.Add(new("M − M2", Points(r, p => p.FirstOrderCapacityKnm), Blue));
        chart.Series.Add(new("M1Rd,max = " + F(q.MaximumFirstOrderKnm) + " kNm", Points(r, _ => q.MaximumFirstOrderKnm), Green, true, DashPattern: "Dot"));
        if (r.Equilibrium is { } e) chart.Series.Add(new("M0Ed,tot", Points(r, _ => e.FirstOrderKnm), Red, true, DashPattern: "Dash"));
        chart.Markers.Add(new(q.CurvatureAtMaximum, q.MaximumFirstOrderKnm, "M1Rd,max = " + F(q.MaximumFirstOrderKnm) + " kNm", Green));
        return chart;
    }

    /// <summary>Il CSV di «Esporta CSV…»: un punto per riga, numeri invarianti a 17 cifre, separati da punto e virgola.</summary>
    public static string Csv(ConcreteModelColumnResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        static string G(double v) => v.ToString("G17", CultureInfo.InvariantCulture);
        var b = new StringBuilder("chi_1_m;M_kNm;M2_kNm;M_meno_M2_kNm;M_trasversale_kNm;epsilon0_permille;epsilon_c_permille;epsilon_s_permille;snervata;ultimo;massimo\n");
        foreach (var p in r.Points)
            b.AppendLine(string.Join(";", new[] { p.Curvature, p.MomentKnm, p.SecondOrderKnm, p.FirstOrderCapacityKnm, p.TransverseKnm, p.ReferenceStrain,
                p.ConcreteCompressionStrain, p.SteelStrain }.Select(G)) + ";" + p.Yielded + ";" + p.Ultimate + ";" + p.Maximum);
        return b.ToString();
    }
}
