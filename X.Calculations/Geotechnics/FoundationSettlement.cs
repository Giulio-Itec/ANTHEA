namespace Anthea.Calculations.Geotechnics;

/// <summary>One-dimensional constrained-modulus integration of stresses from
/// an infinitely long strip. Geometry/influence functions are separate from soil input.</summary>
public static class FoundationSettlement
{
    public sealed record Layer(string Name, double Thickness, double Modulus);
    public sealed record Slice(string Soil, double Top, double Bottom, double Stress, double Modulus, double SettlementMm);
    public sealed record Result(double SettlementMm, double BottomStress, IReadOnlyList<Slice> Slices);

    // Exact integral of 2 z³/(pi((x-s)²+z²)²) for a linearly varying strip load.
    public static double Stress(double x, double z, double left, double right, double pLeft, double pRight)
    {
        if (z <= 0 || right <= left) throw new ArgumentException("Influenza: profondità e larghezza positive richieste.");
        double slope = (pRight - pLeft) / (right - left), atX = pLeft + slope * (x - left);
        double A(double u) => (Math.Atan(u / z) + z * u / (u * u + z * z)) / Math.PI;
        double B(double u) => -z * z * z / (Math.PI * (u * u + z * z));
        double l = left - x, r = right - x;
        return atX * (A(r) - A(l)) + slope * (B(r) - B(l));
    }

    public static Result Calculate(IReadOnlyList<Layer> layers, double x, double left, double right,
        double pLeft, double pRight, double removedPressure, double footingWidth, int subdivisions = 40)
    {
        if (layers.Count == 0 || layers.Any(l => !double.IsFinite(l.Thickness + l.Modulus) || l.Thickness <= 0 || l.Modulus <= 0) ||
            !double.IsFinite(x + pLeft + pRight + removedPressure + footingWidth) || footingWidth <= 0 || subdivisions < 10 || subdivisions > 1000)
            throw new ArgumentException("Cedimenti: completare spessori e moduli edometrici positivi degli strati sotto la fondazione.");
        var slices = new List<Slice>(); double z = 0;
        double Delta(double depth) => Stress(x, depth, left, right, pLeft, pRight) - Stress(x, depth, 0, footingWidth, removedPressure, removedPressure);
        foreach (var layer in layers)
        {
            int count = Math.Max(subdivisions, (int)Math.Ceiling(layer.Thickness / footingWidth * subdivisions));
            if (count > 20000) throw new ArgumentException("Discretizzazione dei cedimenti eccessiva: controllare gli spessori.");
            double dz = layer.Thickness / count;
            for (int i = 0; i < count; i++)
            {
                // Two-point Gauss integration, also used in the independent refinement check.
                double mid = z + dz / 2, off = dz / (2 * Math.Sqrt(3));
                double stress = (Delta(mid - off) + Delta(mid + off)) / 2;
                if (stress < -1e-8) throw new ArgumentException("Scarico netto: serve un modulo di ricompressione e una storia tensionale dedicata; nessun cedimento positivo fittizio.");
                slices.Add(new(layer.Name, z, z + dz, stress, layer.Modulus, stress / layer.Modulus * dz * 1000)); z += dz;
            }
        }
        return new(slices.Sum(s => s.SettlementMm), Delta(z), slices);
    }
}
