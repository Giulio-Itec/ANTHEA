using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    public static (double Compression, double Tension, double E, double Shear0, double ShearCap, double Gamma) GravityMaterial(JsonObject d)
    {
        var p = d["gravity_design"]!; var m = d["materials"]!;
        if (p.S("type") == "Calcestruzzo non armato")
        {
            var i = SezioneCA.DefaultInput(); i["fck_mpa"] = m.D("fck");
            var mat = ConcreteMaterials.Concrete(i); var strengths = ConcreteMaterials.DesignValues(i, SectionWorkspace.Prepare(SezioneCA.DefaultData()));
            return (strengths.Fcd, .85 * mat.Fctk05 / 1.5, mat.Ecm / (1 + m.D("creep")), 0, 0, 1.5);
        }
        if (p.S("type") == "Muratura")
        {
            double gamma = AdvancedNumber(p, "gamma_m", 1, 5) * AdvancedNumber(p, "confidence", 1, 2);
            return (.85 * AdvancedNumber(p, "fk", .01, 100) / gamma, 0, AdvancedNumber(p, "elastic_modulus", 1, 100000) / (1 + m.D("creep")),
                AdvancedNumber(p, "fvk0", 0, 10), AdvancedNumber(p, "fvk_limit", .001, 20), gamma);
        }
        return (m.D("compression_rd"), m.D("tension_rd"), AdvancedNumber(p, "elastic_modulus", 1, 100000), m.D("shear_rd"), m.D("shear_rd"), 1);
    }

    private static List<Check> GravityChecks(JsonObject d, List<LoadCase> cases)
    {
        var result = new List<Check>(); var p = d["gravity_design"]!; var g = d["geometry"]!;
        (double Compression, double Tension, double E, double Shear0, double ShearCap, double Gamma) mat;
        try { mat = GravityMaterial(d); }
        catch (ArgumentException ex) { result.Add(CheckValue("Materiale muro a gravità", "Dettagli", 0, null, "MPa", ex.Message)); return result; }
        double height = g.D("height"), effective = p.D("effective_height") > 0 ? p.D("effective_height") : 2 * height;
        if (effective < 2 * height) throw new ArgumentException("Gravità a mensola libera: la lunghezza efficace non può essere inferiore a 2H.");
        double inertia = Math.Pow(g.D("stem_top"), 3) / 12;
        double ncr = Math.PI * Math.PI * mat.E * 1000 * inertia / (effective * effective);
        foreach (var c in cases)
        {
            double maxN = c.Sections.Where(f => f.Name == "Fusto").Max(f => f.N);
            bool stable = maxN < .8 * ncr; double amplification = stable ? 1 / (1 - Math.Max(0, maxN) / ncr) : 1;
            if (!c.State.StartsWith("SLE")) result.Add(CheckValue("Gravità · stabilità elastica (EI minimo)", c.Name, Math.Max(0, maxN), .8 * ncr, "kN/m"));
            foreach (var f in c.Sections.Where(f => f.N != 0 || f.M != 0 || f.V != 0))
            {
                bool stem = f.Name == "Fusto"; double e0 = stem ? Math.Max(height / 200, AdvancedNumber(p, "eccentricity", 0, 1000) / 1000) : 0;
                double moment = (Math.Abs(f.M) + Math.Max(0, f.N) * e0) * (stem ? amplification : 1);
                double width = f.Thickness, elasticC = f.N / width / 1000 + 6 * moment / (width * width) / 1000;
                double elasticT = Math.Max(0, 6 * moment / (width * width) / 1000 - f.N / width / 1000);
                void Add(string name, double demand, double? resistance, string unit, string missing = "Non disponibile")
                    => result.Add(CheckValue(f.Name + $" z={f.Position:0.00} · " + name, c.Name, demand, resistance, unit, missing) with { Member = f.Name, Position = f.Position });
                if (!stem && !c.Contact.Valid) { Add("resistenza", 0, null, "MPa", "Contatto fondazione non disponibile"); continue; }
                // The linear EI magnifier is accepted only in the no-tension domain.
                bool uncracked = f.N > 0 && moment <= f.N * width / 6;
                if (c.State.StartsWith("SLE"))
                {
                    if (stem && uncracked && stable) c.Curvatures.Add(new(height - f.Position, Math.Sign(f.M) * moment / (mat.E * 1000 * Math.Pow(width, 3) / 12)));
                    continue;
                }
                if (stem)
                {
                    Add("assenza trazione / applicabilità rigidezza", moment, Math.Max(0, f.N * width / 6), "kNm/m");
                    double compressed = f.N > 0 ? Math.Max(0, width - 2 * moment / f.N) : 0;
                    Add("pressoflessione · blocco compresso", Math.Max(0, f.N), stable && uncracked ? mat.Compression * compressed * 1000 : null, "kN/m", "Fuori campo: fessurazione o instabilità; necessario modello non lineare");
                    if (p.S("type") == "Muratura")
                    {
                        double sigma = compressed > 0 ? Math.Max(0, f.N) / (compressed * 1000) : 0;
                        Add("scorrimento giunto", Math.Abs(f.V), stable && uncracked ? Math.Min(mat.Shear0 + .4 * sigma, mat.ShearCap) / mat.Gamma * compressed * 1000 : null, "kN/m");
                    }
                    else
                    {
                        double sigma = compressed > 0 ? Math.Max(0, f.N) / (compressed * 1000) : 0;
                        double limit = mat.Compression - 2 * Math.Sqrt(mat.Tension * mat.Tension + mat.Compression * mat.Tension);
                        double delta = Math.Max(0, sigma - limit);
                        double tau = Math.Sqrt(Math.Max(0, mat.Tension * mat.Tension + sigma * mat.Tension - delta * delta / 4));
                        Add("taglio NTC 4.1.11", Math.Abs(f.V), stable && uncracked ? tau * compressed * 1000 / 1.5 : null, "kN/m");
                    }
                }
                else
                {
                    Add("compressione fondazione", elasticC, mat.Compression, "MPa");
                    Add("trazione fondazione", elasticT, mat.Tension, "MPa");
                    Add("taglio fondazione", 1.5 * Math.Abs(f.V) / width / 1000, p.S("type") == "Muratura" ? mat.Shear0 / mat.Gamma : mat.Tension, "MPa");
                }
            }
        }
        return result;
    }
}
