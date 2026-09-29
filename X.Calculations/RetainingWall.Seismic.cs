using System.Globalization;
using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public const string SeismicManual = "kh e kv assegnati", SeismicSite = "Da parametri del sito (SLV)";
    public const string AmplificationCalculated = "Calcolato", AmplificationAssigned = "Assegnato";
    public const string TopographyFlat = "Pianeggiante", TopographySlope = "Pendio", TopographyRidge = "Rilievo a cresta stretta";
    public static void CompleteSeismicInput(JsonObject d)
    {
        var s = d["seismic"]!.AsObject();
        foreach (var (key, value) in J.Obj(("source", SeismicManual), ("ag_g", ""), ("f0", ""), ("soil_class", "Da scegliere"),
            ("ss_mode", AmplificationCalculated), ("ss", ""), ("st_mode", AmplificationCalculated), ("st", ""),
            ("topography", TopographyFlat), ("slope", ""), ("relief_height", ""), ("site_height", "")))
            if (!s.ContainsKey(key)) s[key] = value?.DeepClone();
    }
    public sealed record SeismicDerivation(double AgG, double Ss, double St, double AmaxG, double Beta, double Kh, double Kv,
        double BetaOverturning, double KhOverturning, double KvOverturning, string SoilFormula, string TopographyFormula)
    {
        public string Description
        {
            get
            {
                string N(double n) => n.ToString("0.#####", CultureInfo.GetCultureInfo("it-IT"));
                return $"SLV · {SoilFormula}\n{TopographyFormula}\nSs={N(Ss)}; St={N(St)}; amax/g = Ss × St × ag/g = {N(Ss)} × {N(St)} × {N(AgG)} = {N(AmaxG)}.\n"
                    + $"βm={N(Beta)} → kh=βm × amax/g = {N(Kh)}; kv=±0,5kh = ±{N(Kv)}.\n"
                    + $"Ribaltamento: βm,rib=min(1; 1,5βm)={N(BetaOverturning)} → kh,rib={N(KhOverturning)}; kv,rib=±{N(KvOverturning)}.\nNTC 2018 §§3.2.2, 3.2.3.2.1 e 7.11.6.2.1. ag/g e F₀ devono riferirsi allo SLV del progetto.";
            }
        }
    }
    public static SeismicDerivation? DeriveSeismic(JsonObject d)
    {
        var s = d["seismic"]!;
        if (s.S("source", SeismicManual) == SeismicManual) return null;
        if (s.S("source") != SeismicSite) throw new ArgumentException("Sisma: modalità di definizione non riconosciuta.");
        double Number(string key, string label, double min, double max)
        {
            if (J.Number(s[key]) is not double n || !double.IsFinite(n) || n < min || n > max) throw new ArgumentException($"Sisma: {label}, inserire un valore fra {min} e {max}.");
            return n;
        }
        double ag = Number("ag_g", "ag/g allo SLV (es. 0,20, non 20)", 0, 1), ss, st;
        string soilFormula, topoFormula;
        if (s.S("ss_mode") == AmplificationAssigned) { ss = Number("ss", "Ss assegnato", .1, 5); soilFormula = "Ss assegnato dal progettista / risposta sismica locale."; }
        else if (s.S("ss_mode") == AmplificationCalculated)
        {
            string soil = s.S("soil_class"); if (!new[] { "A", "B", "C", "D", "E" }.Contains(soil)) throw new ArgumentException("Sisma: scegliere la categoria di sottosuolo A–E dalla relazione geotecnica.");
            double f0 = soil == "A" ? 0 : Number("f0", "F₀ allo SLV", 2.2, 10), x = f0 * ag;
            (ss, soilFormula) = soil switch
            {
                "A" => (1, "Categoria A: Ss=1."),
                "B" => (Math.Clamp(1.4 - .4 * x, 1, 1.2), "Categoria B: Ss=max(1; min(1,2; 1,4−0,4F₀·ag/g))."),
                "C" => (Math.Clamp(1.7 - .6 * x, 1, 1.5), "Categoria C: Ss=max(1; min(1,5; 1,7−0,6F₀·ag/g))."),
                "D" => (Math.Clamp(2.4 - 1.5 * x, .9, 1.8), "Categoria D: Ss=max(0,9; min(1,8; 2,4−1,5F₀·ag/g))."),
                _ => (Math.Clamp(2 - 1.1 * x, 1, 1.6), "Categoria E: Ss=max(1; min(1,6; 2−1,1F₀·ag/g)).")
            };
        }
        else throw new ArgumentException("Sisma: scegliere come definire Ss.");
        if (s.S("st_mode") == AmplificationAssigned) { st = Number("st", "St assegnato", 1, 5); topoFormula = "St assegnato dal progettista / risposta sismica locale."; }
        else if (s.S("st_mode") == AmplificationCalculated)
        {
            string type = s.S("topography");
            if (type == TopographyFlat) { st = 1; topoFormula = "Superficie pianeggiante, T1: St=1."; }
            else
            {
                if (type is not (TopographySlope or TopographyRidge)) throw new ArgumentException("Sisma: scegliere pianeggiante, pendio o rilievo a cresta stretta.");
                double slope = Number("slope", "inclinazione media del pendio/rilievo [°]", 0, 89);
                if (slope <= 15) { st = 1; topoFormula = "Inclinazione media ≤15°, T1: St=1."; }
                else
                {
                    double height = Number("relief_height", "altezza del pendio/rilievo [m]", .01, 9000);
                    double site = Number("site_height", "quota del muro sopra la base del pendio/rilievo [m]", 0, height);
                    string category = type == TopographySlope ? "T2" : slope <= 30 ? "T3" : "T4";
                    double peak = category == "T4" ? 1.4 : 1.2;
                    st = height > 30 ? 1 + (peak - 1) * site / height : 1;
                    topoFormula = height > 30 ? $"{category}: St=1+({peak.ToString("0.0", CultureInfo.GetCultureInfo("it-IT"))}−1)·z/H; z=quota dalla base, H=altezza del rilievo (non del muro)."
                        : "Pendio/rilievo di altezza ≤30 m: amplificazione topografica semplificata non richiesta, St=1 (§3.2.2).";
                }
            }
        }
        else throw new ArgumentException("Sisma: scegliere come definire St.");
        double amax = ss * st * ag, beta = s.S("method") == "Wood semplificato" ? 1 : .38, betaOver = Math.Min(1, 1.5 * beta);
        return new(ag, ss, st, amax, beta, beta * amax, .5 * beta * amax, betaOver, betaOver * amax, .5 * betaOver * amax, soilFormula, topoFormula);
    }
    private static void ResolveSeismic(JsonObject d)
    {
        if (d["seismic"].B("enabled") && DeriveSeismic(d) is { } value)
        { d["seismic"]!["kh"] = value.Kh; d["seismic"]!["kv"] = value.Kv; }
    }
}
