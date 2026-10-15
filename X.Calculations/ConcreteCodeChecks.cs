namespace Anthea.Calculations;

/// <summary>Norme per calcestruzzo ordinario e ingresso del taglio della sezione (kN, kNm, mm, MPa; compressione negativa, come Checker).
/// Le verifiche storiche di taglio e fessurazione sono LegacyConcreteCodeChecks in tests/ANTHEA.Legacy (decisione D3 del 15/10/2026).</summary>
public static class ConcreteCodeChecks
{
    public static bool IsEurocode(string name) => name is "EN 1992-1-1" or "UNI EN 1992-1-1" or "DIN EN 1992-1-1" or "DS EN 1992-1-1" or "NS EN 1992-1-1";
    public static void RequireOrdinary(string name)
    {
        if (name != "NTC 2018" && name != "Model Code 2010" && !IsEurocode(name))
            throw new ArgumentException(name + ": modello FRC/pavimentazioni escluso; scegliere una norma per calcestruzzo ordinario.");
    }

    public sealed record ShearInput(string Standard, double N, double V, double M, double Area, double Bw,
        double D, double Asl, double Fck, double Fcd, double Fyd, double GammaC, double Es,
        double Asw, double Spacing, double Alpha = 90, double? CotTheta = null, double LeverFactor = .9,
        double Aggregate = 20, double AxialEccentricity = 0);
}
