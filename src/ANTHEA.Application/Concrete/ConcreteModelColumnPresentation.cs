using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Presentation;

namespace Anthea.Application.Concrete;

/// <summary>Un riquadro dei risultati della colonna modello: un valore (<see cref="Passed"/> null, blu) o una verifica (verde o rosso).</summary>
public sealed record ConcreteModelColumnCard(string Title, string Value, string Explanation, string Reference, bool IsCheck, bool? Passed);

/// <summary>
/// La scheda «Colonna modello» della sezione c.a. (1.15.0; fase 2 nella 1.16.0): messaggi, firma dei dati, riquadri e testo dei risultati, grafici
/// M(χ) con la retta del momento esterno e M − M2, dominio ridotto N–M1Rd,max con le azioni, CSV dei punti, del dominio e delle combinazioni.
/// Solo presentazione dei valori della libreria: le rette del grafico usano M2 = k·χ dei punti, la retta del minimo NEd·e0.
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
    /// <summary>Titoli e assi dei grafici.</summary>
    public const string CapacityTitle = "M(χ) a N costante e momento esterno", FirstOrderTitle = "Capacità del primo ordine M − M2",
        XLabel = "Curvatura χ [1/m]", CapacityYLabel = "Momento M [kNm]", FirstOrderYLabel = "M − M2 [kNm]",
        EmptyMessage = "Inserire N, l0 e l, poi «Calcola»";
    /// <summary>Titolo e assi del dominio ridotto (1.16.0): momento nella direzione θ (negativo per θ + 180°) e N, compressione negativa.</summary>
    public const string DomainTitle = "Dominio ridotto N–M1Rd,max", DomainXLabel = "Momento del primo ordine M [kNm]", DomainYLabel = "N [kN]";
    /// <summary>Le categorie delle figure della relazione (nel capitolo della colonna modello, con la scelta «curvatura»).</summary>
    public const string FigureCategory = "colonna_modello";
    /// <summary>La categoria della figura del dominio ridotto (1.16.0), nel suo paragrafo del capitolo.</summary>
    public const string DomainFigureCategory = "colonna_modello_dominio";

    private const string Blue = "#0B5CAD", Red = "#C62828", Green = "#16703B", Grey = "#64748B", Orange = "#B45309";
    // e0 is in mm, the moment of the NEd·e0 line in kNm
    private const double MillimetresPerMetre = 1000;

    /// <summary>Il messaggio di una colonna non calcolabile.</summary>
    public static string Unavailable(string error) => "Colonna modello non disponibile: " + error;

    /// <summary>
    /// I dati da cui dipende il risultato: ingressi, trefoli, coefficienti, norma, opzioni della colonna e, con le combinazioni, le combinazioni
    /// del foglio (stessa firma: nessun nuovo calcolo).
    /// </summary>
    public static string Signature(JsonObject input, JsonObject workspace, JsonNode? combinations = null)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(workspace);
        return input.ToJsonString() + workspace["trefoli"]?.ToJsonString() + workspace["coefficienti"]?.ToJsonString() + workspace["normativa"]?.ToJsonString()
            + workspace[ConcreteModelColumn.OptionsKey]?.ToJsonString() + combinations?.ToJsonString();
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
        if (r.Combinations.Count > 0)
        {
            var worst = r.Combinations.FirstOrDefault(x => x.Governing);
            int Count(string status) => r.Combinations.Count(x => x.Status == status);
            cards.Add(new("Inviluppo delle combinazioni", "η max = " + (worst is null ? "—" : worst.Ratio is double eta ? F(eta, "0.###") : "∞")
                + (worst is null ? "" : " · governa «" + worst.Name + "»"),
                r.Combinations.Count + " " + r.Source.Replace("Combinazioni", "combinazioni", StringComparison.Ordinal) + ", θ = " + F(r.Theta) + "°: " + Count("Verificata")
                + " verificate, " + Count("Non verificata") + " non verificate, " + Count("Trazione") + " di trazione (non pertinenti)"
                + (Count("Non calcolabile") > 0 ? ", " + Count("Non calcolabile") + " non calcolabili" : "") + ". I riquadri e i grafici seguenti sono della combinazione «"
                + r.Governing + "»; ogni combinazione con il suo N costante.", "EN 1992-1-1 5.8.6(6) e 5.8.8.2; " + r.Profile, true, r.Verified));
        }
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
            cards.Add(new(r.Governing is null ? "Verifica della colonna modello" : "Verifica della combinazione «" + r.Governing + "»", "η = M0Ed,tot/M1Rd,max = " + F(e.Ratio, "0.###") + (e.Found ? " · MEd = " + F(e.DesignKnm) + " kNm" : ""),
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
        if (r.Combinations.Count > 0)
        {
            b.AppendLine().AppendLine("Combinazioni (Mθ = Mx·cos θ + My·sin θ, θ = " + F(r.Theta) + "°)");
            b.AppendLine("Combinazione   N [kN]   Mθ [kNm]   M⊥ [kNm]   M0Ed,tot [kNm]   M1Rd,max [kNm]   η   Esito");
            foreach (var x in r.Combinations)
                b.AppendLine(string.Join("   ", x.Name, G6(x.AxialKn), G6(x.MomentKnm), G6(x.TransverseKnm), G6(x.FirstOrderTotalKnm), G6(x.MaximumFirstOrderKnm),
                    x.Ratio is double eta ? G6(eta) : x.BeyondResistance ? "∞" : "—", x.Outcome) + (x.Governing ? " · governa" : "") + (x.Shown && !x.Governing ? " · mostrata" : "")
                    + (x.TransverseNeglected ? " · M⊥ non verificato" : ""));
        }
        foreach (var d in r.Domains)
        {
            b.AppendLine().AppendLine("Dominio ridotto N–M1Rd,max (θ = " + F(d.DirectionDegrees) + "°, φef = " + F(d.EffectiveCreepRatio, "0.###") + ", NRd,c = "
                + G6(d.CompressionResistanceKn) + " kN)");
            b.AppendLine("N [kN]   M1Rd,max [kNm]   MRd sezione [kNm]");
            foreach (var p in d.Points)
                b.AppendLine(string.Join("   ", G6(p.AxialKn), p.Rejected ? "rifiutato (momento trasversale)" : G6(p.MaximumFirstOrderKnm), G6(p.SectionKnm)));
        }
        b.AppendLine().AppendLine("Passaggi" + (r.Governing is null ? "" : " della combinazione «" + r.Governing + "»"));
        foreach (var step in r.Trace)
            b.AppendLine(step.Description + ": " + step.Formula + (step.Value is double v ? " = " + v.ToString("G6", CultureInfo.CurrentCulture) + " " + step.Unit : "")
                + (step.Reference.Length > 0 ? " [" + step.Reference + "]" : ""));
        b.AppendLine().AppendLine("χ [1/m]   M [kNm]   M2 [kNm]   M − M2 [kNm]   ε0 [‰]   εc [‰]   εs [‰]");
        foreach (var p in r.Points)
            b.AppendLine(string.Join("   ", new[] { p.Curvature, p.MomentKnm, p.SecondOrderKnm, p.FirstOrderCapacityKnm, p.ReferenceStrain, p.ConcreteCompressionStrain, p.SteelStrain }
                .Select(x => x.ToString("G6", CultureInfo.CurrentCulture))) + Flags(p));
        return b.ToString();
    }

    private static string G6(double value) => value.ToString("G6", CultureInfo.CurrentCulture);
    private static string G6(double? value) => value is double v ? G6(v) : "—";

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

    /// <summary>
    /// Il CSV di «Esporta CSV…», numeri invarianti a 17 cifre separati da punto e virgola: i punti della curva (della combinazione mostrata), poi,
    /// dopo una riga vuota, i punti dei domini ridotti (1.16.0) e, con le combinazioni, una riga per combinazione; ogni blocco con la sua intestazione.
    /// </summary>
    public static string Csv(ConcreteModelColumnResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        static string G(double v) => v.ToString("G17", CultureInfo.InvariantCulture);
        var b = new StringBuilder("chi_1_m;M_kNm;M2_kNm;M_meno_M2_kNm;M_trasversale_kNm;epsilon0_permille;epsilon_c_permille;epsilon_s_permille;snervata;ultimo;massimo\n");
        foreach (var p in r.Points)
            b.AppendLine(string.Join(";", new[] { p.Curvature, p.MomentKnm, p.SecondOrderKnm, p.FirstOrderCapacityKnm, p.TransverseKnm, p.ReferenceStrain,
                p.ConcreteCompressionStrain, p.SteelStrain }.Select(G)) + ";" + p.Yielded + ";" + p.Ultimate + ";" + p.Maximum);
        static string Gn(double? v) => v is double x ? G(x) : "";
        if (r.Domains.Count > 0)
        {
            b.AppendLine().AppendLine("dominio_theta_gradi;phi_ef;N_kN;M1Rd_max_kNm;MRd_sezione_kNm;modo");
            foreach (var d in r.Domains) foreach (var p in d.Points)
                b.AppendLine(string.Join(";", G(d.DirectionDegrees), G(d.EffectiveCreepRatio), G(p.AxialKn), Gn(p.MaximumFirstOrderKnm), Gn(p.SectionKnm), p.Mode));
        }
        if (r.Combinations.Count > 0)
        {
            b.AppendLine().AppendLine("combinazione;stato_limite;N_kN;Mx_kNm;My_kNm;M_theta_kNm;M_trasversale_kNm;direzione_gradi;M0Ed_tot_kNm;M1Rd_max_kNm;eta;esito;governa");
            foreach (var x in r.Combinations)
                b.AppendLine(string.Join(";", x.Name.Replace(';', ','), x.State, G(x.AxialKn), G(x.MxKnm), G(x.MyKnm), G(x.MomentKnm), G(x.TransverseKnm), Gn(x.DirectionDegrees),
                    Gn(x.FirstOrderTotalKnm), Gn(x.MaximumFirstOrderKnm), x.Ratio is double eta ? G(eta) : x.BeyondResistance ? "inf" : "", x.Outcome.Replace(';', ','),
                    x.Governing.ToString()));
        }
        return b.ToString();
    }

    /// <summary>
    /// Il grafico del dominio ridotto (1.16.0): per ogni direzione la curva M1Rd,max(N) e il momento resistente della sezione MRd(N) = M(χu), con i
    /// momenti negativi per θ + 180°, la retta del minimo NEd·e0 e le azioni (N, M0Ed,tot) colorate per esito, con il nome della combinazione
    /// mostrata e di quella che governa (Ed per l'azione singola).
    /// </summary>
    public static PlotData DomainChart(ConcreteModelColumnResult r)
    {
        ArgumentNullException.ThrowIfNull(r);
        double Sign(double direction) => Math.Abs(direction - r.Theta) < 1e-9 ? 1 : -1;
        var first = r.Domains.FirstOrDefault();
        var chart = new PlotData { Title = DomainTitle, XLabel = DomainXLabel, YLabel = DomainYLabel,
            Note = "l0 = " + F(r.EffectiveLength) + " m, c = " + F(r.CurvatureFactor) + (first is null ? "" : ", φef = " + F(first.EffectiveCreepRatio, "0.###") + ", ei = "
                + F(first.ImperfectionEccentricity, "0.#") + " mm") + "; azioni con M0Ed,tot (imperfezione e minimo)" };
        foreach (var d in r.Domains)
        {
            double sign = Sign(d.DirectionDegrees);
            // the direction in the names only with the two domains (θ and θ + 180°)
            string direction = r.Domains.Count > 1 ? " · θ = " + F(d.DirectionDegrees) + "°" : "";
            var valid = d.Points.Where(p => !p.Rejected && p.MaximumFirstOrderKnm is not null).ToList();
            chart.Series.Add(new("M1Rd,max (dominio ridotto)" + direction, valid.Select(p => new[] { sign * p.MaximumFirstOrderKnm!.Value, p.AxialKn }).ToList(), Blue));
            chart.Series.Add(new("MRd della sezione" + direction, valid.Select(p => new[] { sign * p.SectionKnm!.Value, p.AxialKn }).ToList(), Grey, true, DashPattern: "Dash"));
            if (d.MinimumEccentricity is double e0)
                chart.Series.Add(new("NEd·e0, e0 = " + F(e0, "0.#") + " mm" + direction,
                    [[0, 0], [sign * Math.Abs(d.CompressionResistanceKn) * e0 / MillimetresPerMetre, d.CompressionResistanceKn]], Orange, true, DashPattern: "Dot"));
        }
        static string Color(bool? verified) => verified switch { true => Green, false => Red, _ => Grey };
        if (r.Combinations.Count > 0)
        {
            foreach (var x in r.Combinations.Where(x => x.AxialKn <= 0))
            {
                double moment = x.FirstOrderTotalKnm ?? Math.Abs(x.MomentKnm);
                double sign = x.DirectionDegrees is double direction ? Sign(direction) : 1;
                chart.Markers.Add(new(sign * moment, x.AxialKn, x.Governing || x.Shown ? x.Name : "", Color(x.Verified), Radius: x.Governing || x.Shown ? 5 : 3.5));
            }
        }
        else if (r.FirstOrder.TotalKnm is double total) chart.Markers.Add(new(total, r.AxialKn, "Ed", Color(r.Verified)));
        return chart;
    }
}
