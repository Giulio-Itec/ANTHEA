using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Bridge between compression-positive geotechnical inputs and the common Checker RC solver.</summary>
public static class HorizontalConcreteSection
{
    public static JsonObject Calculate(JsonObject data)
    {
        var g = data["generali"]!;
        double diameter = g.Required("diametro", strict: true);
        double axial = SectionWorkspace.Number(g.S("azione_assiale"), "N del palo");
        var input = (JsonObject)data["sezione"]!.DeepClone();
        input["shape"] = "Circolare"; input["diameter_mm"] = diameter * 1000;
        double bars = input.D("longitudinal_bar_count", 16);
        if (bars < 4 || bars > 512 || bars % 2 != 0 || input["barre_manuali"] is JsonArray { Count: > 0 })
            throw new ArgumentException("Calcolo automatico Broms: usare da 4 a 512 barre circolari, in numero pari. Per armature manuali assegnare una resistenza da analisi dedicata.");
        var workspace = J.Obj(("normativa", "NTC 2018"), ("trefoli", new JsonArray()));
        var options = J.Obj(("criterio", "N costante"), ("assi", "Locali"), ("strategia", "Iterativo"), ("modello", "Non lineare"));
        var engine = new CheckerSection(input, workspace, options);
        // Broms may form hinges of opposite signs: use the smaller resistance of the two directions.
        var directions = SectionMomentResistance.Calculate(engine, -axial, [("Mx+", 1d, 0d), ("Mx−", -1d, 0d)]);
        if (directions.Any(r => r.Moment is null)) throw new ArgumentException("Sezione c.a. del palo: " + string.Join("; ", directions.Where(r => r.Moment is null).Select(r => r.Status)));
        var governing = directions.MinBy(r => Math.Abs(r.Moment!.Value))!;
        double moment = Math.Abs(governing.Moment!.Value);
        if (!(moment > 0)) throw new ArgumentException("La sezione non dispone di resistenza flessionale per N assegnato.");
        var material = ConcreteMaterials.DesignValues(input, workspace);
        return J.Obj(("momento_knm", moment), ("n_kn", axial), ("n_checker_kn", -axial),
            ("asse_neutro_mm", governing.Section?.NeutralDistance),
            ("residuo_n_kn", -governing.Resistance!.Value.N - axial),
            ("tolleranza_n_kn", SectionMomentResistance.AxialToleranceKn(axial)),
            ("fcd_mpa", material.Fcd), ("fyd_mpa", material.Fyd), ("area_acciaio_mm2", engine.Geometry.AreaSteel),
            ("outline", engine.Geometry.Outline), ("bars", engine.Geometry.Bars.Select(b => new[] { b.X, b.Y, b.Area, b.Diametro })),
            ("motore", "GPCChecker.Concrete"), ("direzioni", directions), ("lati_contorno", engine.Geometry.CircularSides),
            ("modello", "Resistenza N–Mx da GPCChecker.Concrete, come nel modulo cemento armato. N geotecnico positivo a compressione è convertito in N negativo per Checker. Minimo fra Mx+ e Mx−; geometria, legami e coefficienti della sezione inserita. La duttilità della cerniera resta da verificare."));
    }
}
