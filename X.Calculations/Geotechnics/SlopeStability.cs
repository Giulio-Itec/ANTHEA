namespace Anthea.Calculations.Geotechnics;

/// <summary>Slice preparation and bounded, reproducible search. Geometry lives in SlopeGeometry.</summary>
public static class SlopeStability
{
    private static readonly double[] GaussX = [-.8611363115940526, -.3399810435848563, .3399810435848563, .8611363115940526];
    private static readonly double[] GaussW = [.3478548451374538, .6521451548625461, .6521451548625461, .3478548451374538];

    public static SlopeSlice[] Slices(SlopeSection section, SlipCircle circle, SlopeFactors factors, int count)
    {
        var cuts = SlopeGeometry.Divisions(section, circle, count); var result = new List<SlopeSlice>();
        for (int i = 1; i < cuts.Length; i++)
        {
            double l = cuts[i - 1], r = cuts[i], x = (l + r) / 2, b = r - l, y = circle.Base(x), top = SlopeGeometry.Height(section.Surface, x);
            var soil = section.Soils.First(s => y >= s.Bottom - 1e-9);
            double soilW = 0, bodyW = 0, wx = 0, wy = 0;
            for (int j = 0; j < 4; j++)
            {
                double gx = x + b * GaussX[j] / 2, scale = b * GaussW[j] / 2, bottom = circle.Base(gx), surface = SlopeGeometry.Height(section.Surface, gx);
                double water = section.Water.Length == 0 ? double.NegativeInfinity : SlopeGeometry.Height(section.Water, gx);
                var intervals = section.Bodies.Select(body => (Body: body, Span: SlopeGeometry.VerticalInterval(body.Polygon, gx))).Where(t => t.Span is not null).ToArray();
                double upper = surface;
                void Mass(double low, double high, double gamma, bool rigid)
                {
                    if (high <= low) return;
                    double w = (high - low) * gamma * scale;
                    if (rigid) bodyW += w; else soilW += w;
                    wx += w * gx; wy += w * (high + low) / 2;
                }
                foreach (var layer in section.Soils)
                {
                    double low = Math.Max(bottom, layer.Bottom), high = upper;
                    foreach (bool wet in new[] { false, true })
                    {
                        double lo = wet ? low : Math.Max(low, water), hi = wet ? Math.Min(high, water) : high;
                        double gamma = (wet ? layer.GammaSat : layer.Gamma) * factors.Soil;
                        Mass(lo, hi, gamma, false);
                        foreach (var interval in intervals)
                        {
                            double cutLow = Math.Max(lo, interval.Span!.Value.Bottom), cutHigh = Math.Min(hi, interval.Span.Value.Top);
                            Mass(cutLow, cutHigh, -gamma, false); // Replace displaced soil by the rigid body's own weight.
                        }
                    }
                    upper = Math.Min(upper, layer.Bottom);
                }
                foreach (var interval in intervals) Mass(interval.Span!.Value.Bottom, interval.Span.Value.Top, interval.Body.Gamma * factors.Body, true);
            }
            double wTotal = soilW + bodyW, xg = wTotal > 0 ? wx / wTotal : x, yg = wTotal > 0 ? wy / wTotal : (top + y) / 2;
            double v = (1 - factors.Kv) * wTotal, h = factors.Kh * wTotal;
            double driving = (v * (xg - circle.X) + h * (circle.Y - yg)) / circle.Radius, vl = 0, hl = 0;
            foreach (var load in section.Loads)
            {
                double factor = factors.Loads.GetValueOrDefault(load.Id), length = load.Distributed ? Math.Max(0, Math.Min(r, load.Right) - Math.Max(l, load.Left)) : load.Left >= l && (load.Left < r || i == cuts.Length - 1 && load.Left <= r) ? 1 : 0;
                if (factor == 0 || length == 0) continue;
                double atX = load.Distributed ? (Math.Max(l, load.Left) + Math.Min(r, load.Right)) / 2 : load.Left;
                vl += load.Vertical * factor * length; hl += load.Horizontal * factor * length;
                driving += factor * length * (load.Vertical * (atX - circle.X) + load.Horizontal * (circle.Y - load.Y) + load.Moment) / circle.Radius;
            }
            double u = section.Water.Length == 0 || factors.Undrained ? 0 : 9.81 * Math.Max(0, SlopeGeometry.Height(section.Water, x) - y);
            result.Add(new(i, l, r, y, top, Math.Asin((x - circle.X) / circle.Radius) * 180 / Math.PI, soil.Name,
                soilW, bodyW, xg, yg, vl, hl, u, factors.Undrained ? 0 : Math.Atan(Math.Tan(soil.Phi * Math.PI / 180) / factors.MPhi) * 180 / Math.PI,
                factors.Undrained ? soil.Cu / factors.MCu : soil.Cohesion / factors.MC, v + vl, driving, 0, 0, 0, 0));
        }
        return result.ToArray();
    }

    public static SlopeResult Calculate(SlopeSection section, SlopeSearch search, SlopeFactors[] cases, CancellationToken token = default)
    {
        Validate(section, search, cases); var output = new List<SlopeCaseResult>();
        foreach (var factors in cases)
        {
            int tried = 0, valid = 0, solved = 0, failed = 0; SlopeSurfaceResult? best = null;
            var minima = new List<SlopeSurfaceResult>();
            var seen = new HashSet<(double, double, double)>();
            void Evaluate(SlipCircle? c)
            {
                if (c is null) return;
                double depth = c.Radius - c.Y;
                if (depth < search.DepthMin || depth > search.DepthMax || !seen.Add((c.Left, c.Right, depth))) return;
                tried++;
                if (!SlopeGeometry.Admissible(section, c)) return; valid++;
                var slices = Slices(section, c, factors, search.Slices);
                if (slices.Sum(s => s.Driving) <= 1e-9) return;
                var check = BishopSolver.Solve(c, slices, factors.R, token);
                if (check is null) { failed++; return; } solved++;
                if (best is null || check.Factor < best.Factor) best = check;
                minima.Add(check); if (minima.Count > 80) { minima.Sort((a, b) => a.Factor.CompareTo(b.Factor)); minima.RemoveRange(60, minima.Count - 60); }
            }
            void Scan(double xmin, double xmax, double rmin, double rmax, double dmin, double dmax, int n)
            {
                for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
                {
                    double l = xmin + (xmax - xmin) * i / (n - 1), r = rmin + (rmax - rmin) * j / (n - 1);
                    var exit = new SlopePoint(l, SlopeGeometry.Height(section.Surface, l));
                    var entry = new SlopePoint(r, SlopeGeometry.Height(section.Surface, r));
                    // A volume grid need not hit the tangent boundary, where a critical
                    // surface can occur; sample that two-dimensional family explicitly.
                    Evaluate(SlopeGeometry.TangentAtEntry(exit, entry));
                    for (int k = 0; k < n; k++)
                    {
                        token.ThrowIfCancellationRequested();
                        double depth = dmin + (dmax - dmin) * k / (n - 1);
                        Evaluate(SlopeGeometry.Through(exit, entry, -depth));
                    }
                }
            }
            Scan(search.ExitMin, search.ExitMax, search.EntryMin, search.EntryMax, search.DepthMin, search.DepthMax, search.Grid);
            double dl = (search.ExitMax - search.ExitMin) / (search.Grid - 1), dr = (search.EntryMax - search.EntryMin) / (search.Grid - 1), dd = (search.DepthMax - search.DepthMin) / (search.Grid - 1);
            for (int level = 0; level < search.Refinements && best is not null; level++)
            {
                var seeds = new List<SlipCircle>();
                foreach (var candidate in minima.OrderBy(c => c.Factor))
                {
                    var c = candidate.Circle;
                    if (seeds.All(p => Math.Abs(p.Left - c.Left) > dl * .4 || Math.Abs(p.Right - c.Right) > dr * .4 || Math.Abs((p.Radius - p.Y) - (c.Radius - c.Y)) > dd * .4)) seeds.Add(c);
                    if (seeds.Count == 6) break;
                }
                foreach (var c in seeds)
                {
                    double dep = c.Radius - c.Y;
                    Scan(Math.Max(search.ExitMin, c.Left - dl), Math.Min(search.ExitMax, c.Left + dl), Math.Max(search.EntryMin, c.Right - dr), Math.Min(search.EntryMax, c.Right + dr), Math.Max(search.DepthMin, dep - dd), Math.Min(search.DepthMax, dep + dd), 5);
                }
                dl /= 2; dr /= 2; dd /= 2;
            }
            bool boundary = best is not null && (Near(best.Circle.Left, search.ExitMin, dl) || Near(best.Circle.Left, search.ExitMax, dl) || Near(best.Circle.Right, search.EntryMin, dr) || Near(best.Circle.Right, search.EntryMax, dr) || Near(best.Circle.Radius - best.Circle.Y, search.DepthMin, dd) || Near(best.Circle.Radius - best.Circle.Y, search.DepthMax, dd));
            string status = best is null ? "Nessuna superficie risolta: estendere dominio/profondità o correggere il modello." : failed > 0 ? "Ricerca incompleta: superfici con trazione o equilibrio non risolto." : boundary ? "Minimo sul bordo della ricerca: estendere il dominio." : best.Ratio <= 1 ? "Soddisfatta nel dominio esplorato" : "Non soddisfatta";
            if (best is not null)
            {
                var refined = BishopSolver.Solve(best.Circle, Slices(section, best.Circle, factors, 2 * search.Slices), factors.R, token);
                if (refined is null || Math.Abs(refined.Factor / best.Factor - 1) > .02) { failed++; status = "Discretizzazione non convergente: aumentare i conci."; }
                else best = refined.Factor < best.Factor ? refined : best;
            }
            if (best?.Ratio > 1 && !status.StartsWith("Non soddisfatta")) status = "Non soddisfatta; " + status;
            output.Add(new(factors, best, tried, valid, solved, failed, boundary, status));
        }
        return new(section, search, output.ToArray(), [BishopSolver.Formula,
            "Superfici circolari verso valle, ramo inferiore con centro fra gli estremi, anche con tangente verticale all’ingresso, passanti sotto l’intero muro; interstrato orizzontale. Ricerca a griglia e sulla famiglia tangente, raffinamento da sei minimi distinti: non garantisce il minimo assoluto. Ampliare il dominio e confrontare densità di ricerca e conci.",
            "Bishop semplificato: equilibrio dei momenti e verticale dei conci; forze tangenziali interconcio trascurate, equilibrio orizzontale non imposto. Non sono modellate fessure di trazione, superfici non circolari, pressioni idrodinamiche o degradazione ciclica.",
            "Il muro è una massa rigida; spinte muro–terreno e reazioni di fondazione sono interne alla massa e non si aggiungono. GPC.Geometry per area/baricentro dei corpi; carichi esterni senza inerzia aggiunta alla massa dei sovraccarichi."]);
    }

    private static bool Near(double value, double bound, double step) => Math.Abs(value - bound) < Math.Max(1e-6, step * .1);
    public static void Validate(SlopeSection s, SlopeSearch q, SlopeFactors[] cases)
    {
        void Line(SlopePoint[] points, string name, bool vertical = false)
        {
            if (points.Length < 2 || points.Length > 100 || points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) throw new ArgumentException(name + ": da 2 a 100 punti finiti.");
            for (int i = 1; i < points.Length; i++) if (points[i].X < points[i - 1].X || !vertical && points[i].X == points[i - 1].X) throw new ArgumentException(name + ": ascisse in ordine crescente.");
        }
        Line(s.Surface, "Profilo", true);
        if (s.Soils.Length is < 1 or > 50) throw new ArgumentException("Stabilità globale: inserire gli strati profondi.");
        double top = double.PositiveInfinity;
        foreach (var soil in s.Soils)
        {
            if (!new[] { soil.Bottom, soil.Gamma, soil.GammaSat, soil.Phi, soil.Cohesion, soil.Cu }.All(double.IsFinite) || soil.Bottom >= top || soil.Gamma < 10 || soil.GammaSat < soil.Gamma || soil.GammaSat > 30 || soil.Phi < 0 || soil.Phi > 50 || soil.Cohesion < 0 || soil.Cu < 0) throw new ArgumentException("Strati globali: quote inferiori decrescenti, 10≤γ≤γsat≤30, 0≤φ≤50°, c′ e cu≥0.");
            top = soil.Bottom;
        }
        if (s.Water.Length > 0)
        {
            Line(s.Water, "Falda");
            if (s.Water[0].X > s.Surface[0].X || s.Water[^1].X < s.Surface[^1].X) throw new ArgumentException("La falda deve coprire l’intero profilo.");
            foreach (double x in s.Surface.Select(p => p.X).Concat(s.Water.Select(p => p.X)).Where(x => x >= s.Surface[0].X && x <= s.Surface[^1].X))
                if (SlopeGeometry.Height(s.Water, x) > SlopeGeometry.Height(s.Surface, x) + 1e-6) throw new ArgumentException("Falda sopra il terreno: acqua esterna non supportata dalla stabilità globale.");
        }
        if (!new[] { q.ExitMin, q.ExitMax, q.EntryMin, q.EntryMax, q.DepthMin, q.DepthMax }.All(double.IsFinite) || q.ExitMin >= q.ExitMax || q.EntryMin >= q.EntryMax || q.DepthMin <= 0 || q.DepthMin >= q.DepthMax || q.ExitMin < s.Surface[0].X || q.ExitMax >= s.RequiredLeft || q.EntryMin <= s.RequiredRight || q.EntryMax > s.Surface[^1].X || -q.DepthMax < s.Soils[^1].Bottom) throw new ArgumentException("Dominio di ricerca: uscite a valle del muro, ingressi a monte, profondità positive coperte dagli strati e dal profilo.");
        if (q.Grid < 3 || q.Grid > 21 || q.Slices < 20 || q.Slices > 200 || q.Refinements < 0 || q.Refinements > 4) throw new ArgumentException("Ricerca: 3–21 nodi per direzione, 20–200 conci, 0–4 raffinamenti.");
        if (cases.Length is < 1 or > 256) throw new ArgumentException("Da 1 a 256 combinazioni globali.");
        foreach (var c in cases)
        {
            if (!new[] { c.Soil, c.Body, c.MPhi, c.MC, c.MCu, c.R, c.Kh, c.Kv }.All(double.IsFinite) || c.Soil <= 0 || c.Body <= 0 || c.MPhi <= 0 || c.MC <= 0 || c.MCu <= 0 || c.R <= 0 || Math.Abs(c.Kh) > .5 || Math.Abs(c.Kv) > .5) throw new ArgumentException("Coefficienti globali non validi.");
            if (c.Undrained && s.Soils.Any(x => x.Cu <= 0)) throw new ArgumentException("Analisi non drenata: cu>0 obbligatoria per tutti gli strati.");
        }
    }
}
