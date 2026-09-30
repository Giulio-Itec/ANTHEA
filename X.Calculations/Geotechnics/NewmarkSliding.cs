namespace Anthea.Calculations.Geotechnics;

/// <summary>Unidirectional rigid sliding block. Piecewise-linear accelerogram in g;
/// exact integration between threshold crossings and stops. No magnitude regression.</summary>
public static class NewmarkSliding
{
    public sealed record Sample(double Time, double AccelerationG);
    public sealed record Point(double Time, double AccelerationG, double Velocity, double DisplacementMm);
    public sealed record Result(double DisplacementMm, double PeakVelocity, double PgaG, IReadOnlyList<Point> Points);
    public static Result Calculate(IReadOnlyList<Sample> samples, double yieldG, double scale = 1)
    {
        if (samples.Count < 2 || samples.Count > 200000 || !double.IsFinite(yieldG + scale) || yieldG <= 0 || scale <= 0 ||
            samples.Any(s => !double.IsFinite(s.Time + s.AccelerationG)) || samples[0].Time < 0 || samples.Zip(samples.Skip(1)).Any(p => p.Second.Time <= p.First.Time))
            throw new ArgumentException("Newmark: accelerogramma con tempi crescenti, accelerazioni finite, scala e accelerazione critica positive.");
        double speed = 0, displacement = 0, peak = 0; var output = new List<Point> { new(samples[0].Time, samples[0].AccelerationG * scale, 0, 0) };
        for (int i = 1; i < samples.Count; i++)
        {
            double dt = samples[i].Time - samples[i - 1].Time;
            double aa = 9.81 * (samples[i - 1].AccelerationG * scale - yieldG), bb = 9.81 * (samples[i].AccelerationG * scale - yieldG);
            var cuts = new List<double> { 0, dt }; if (aa * bb < 0) cuts.Insert(1, dt * -aa / (bb - aa));
            for (int j = 1; j < cuts.Count; j++)
            {
                double start = cuts[j - 1], length = cuts[j] - start, acceleration = aa + (bb - aa) * start / dt, jerk = (bb - aa) / dt;
                double end = speed + acceleration * length + .5 * jerk * length * length;
                if (speed <= 0 && acceleration + jerk * length / 2 <= 0) continue;
                double run = length;
                if (end < 0)
                {
                    double lo = 0, hi = length;
                    for (int k = 0; k < 60; k++) { double mid = (lo + hi) / 2; if (speed + acceleration * mid + .5 * jerk * mid * mid > 0) lo = mid; else hi = mid; }
                    run = (lo + hi) / 2;
                }
                displacement += speed * run + .5 * acceleration * run * run + jerk * run * run * run / 6;
                speed = Math.Max(0, speed + acceleration * run + .5 * jerk * run * run); peak = Math.Max(peak, speed);
            }
            output.Add(new(samples[i].Time, samples[i].AccelerationG * scale, speed, displacement * 1000));
        }
        // After the record, complete the physical stop under zero ground acceleration.
        if (speed > 0) { displacement += speed * speed / (2 * 9.81 * yieldG); output.Add(new(samples[^1].Time + speed / (9.81 * yieldG), 0, 0, displacement * 1000)); }
        return new(displacement * 1000, peak, samples.Max(s => Math.Abs(s.AccelerationG * scale)), output);
    }
}
