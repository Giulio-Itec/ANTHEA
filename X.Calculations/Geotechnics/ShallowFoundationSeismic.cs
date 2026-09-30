namespace Anthea.Calculations.Geotechnics;

/// <summary>Strip footing on dry cohesionless soil. EN 1998-5:2004 Annex F,
/// JRC 2011 eqs 4.12–4.13. Inputs per metre: m, kN, kNm, kN/m³, degrees.</summary>
public static class ShallowFoundationSeismic
{
    public sealed record Result(double Capacity, double? Ratio, double NMax, double SoilInertia,
        double NBar, double VBar, double MBar, double VerticalLimit, double? Interaction, string Status);

    public static Result Calculate(double width, double gamma, double phi, double n, double v, double moment,
        double groundKh, double groundKv, double modelFactor, double resistanceFactor)
    {
        if (new[] { width, gamma, phi, n, v, moment, groundKh, groundKv, modelFactor, resistanceFactor }.Any(x => !double.IsFinite(x)) ||
            width <= 0 || gamma <= 0 || phi <= 0 || phi > 45 || n <= 0 || groundKh < 0 || Math.Abs(groundKv) >= 1 || modelFactor < 1 || resistanceFactor < 1)
            throw new ArgumentException("Portanza sismica: controllare geometria, resistenze, accelerazioni e risultante verticale.");
        double angle = phi * Math.PI / 180, tan = Math.Tan(angle);
        double nq = Math.Exp(Math.PI * tan) * Math.Pow(Math.Tan(Math.PI / 4 + angle / 2), 2);
        double ng = 2 * (nq - 1) * tan;
        double nmax = .5 * gamma * (1 - groundKv) * width * width * ng;
        double f = modelFactor * groundKh / tan;
        double cap = 1 - .96 * f;
        double factor = modelFactor * resistanceFactor / nmax;
        double nn = n * factor, vv = Math.Abs(v) * factor, mm = Math.Abs(moment) * factor / width;
        if (cap <= 0 || 1 - .41 * f <= 0 || 1 - .32 * f <= 0)
            return new(0, null, nmax, f, nn, vv, mm, 0, null, "Non soddisfatta: dominio sismico esaurito");
        double limit = Math.Pow(cap, .39);
        double Interaction(double scale)
        {
            double x = nn * scale, gap = limit - x;
            if (x <= 0) return 0;
            if (gap <= 0) return double.PositiveInfinity;
            double denominator = Math.Pow(x, .92) * Math.Pow(gap, 1.25);
            return (Math.Pow(1 - .41 * f, 1.14) * Math.Pow(2.90 * vv * scale, 1.14)
                + Math.Pow(1 - .32 * f, 1.01) * Math.Pow(2.80 * mm * scale, 1.01)) / denominator;
        }
        // Capacity is found along the N,V,M loading ray. Eccentricity is already
        // in M: no effective-width or inclination factors may be applied again.
        double low = 0, high = limit / nn;
        if (vv > 0 || mm > 0)
            for (int i = 0; i < 100; i++) { double mid = (low + high) / 2; if (Interaction(mid) <= 1) low = mid; else high = mid; }
        else low = high;
        double ratio = 1 / low;
        return new(n * low, double.IsFinite(ratio) ? ratio : null, nmax, f, nn, vv, mm, limit, double.IsFinite(Interaction(1)) ? Interaction(1) : null, ratio <= 1 ? "Soddisfatta" : "Non soddisfatta");
    }
}
