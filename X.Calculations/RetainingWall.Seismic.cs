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
    /// <summary>
    /// The seismic coefficients of the site at the SLV (NTC 2018 §§3.2.2, 3.2.3.2.1, 7.11.6.2.1) from GPCChecker.Geotechnics: NtcSiteAmplification
    /// for Ss, St and amax/g, WallSeismic for βm and the coefficients of the general and overturning checks. Null with assigned kh and kv.
    /// </summary>
    public static SeismicDerivation? DeriveSeismic(JsonObject d)
    {
        var s = d["seismic"]!;
        if (s.S("source", SeismicManual) == SeismicManual) return null;
        if (s.S("source") != SeismicSite) throw new ArgumentException("Sisma: modalità di definizione non riconosciuta.");
        var site = Site(s); var method = s.S("method") == "Wood semplificato" ? GPC.Checkers.Geotechnics.Walls.WallSeismicMethod.Wood : GPC.Checkers.Geotechnics.Walls.WallSeismicMethod.MononobeOkabe;
        var wall = GPC.Checkers.Geotechnics.Walls.WallSeismic.FromSite(method, site);
        return new(site.AgG, site.Ss, site.St, site.AmaxG, wall.Beta, wall.Kh, wall.Kv, wall.BetaOverturning, wall.KhOverturning, wall.KvOverturning, site.SoilFormula, site.TopographyFormula);
    }
    private static void ResolveSeismic(JsonObject d)
    {
        if (d["seismic"].B("enabled") && DeriveSeismic(d) is { } value)
        { d["seismic"]!["kh"] = value.Kh; d["seismic"]!["kv"] = value.Kv; }
    }
}
