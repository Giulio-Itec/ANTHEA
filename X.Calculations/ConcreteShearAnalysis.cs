using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public sealed record ConcreteShearResult(Ntc2018Checks.ShearResult[] Shear, TorsionResult? Torsion);

/// <summary>Shared section adapter for the ordinary-concrete code profiles; no WPF dependencies. Shear and torsion are computed by
/// <see cref="ConcreteShearTorsionAdapter"/>: the argument engine = null uses its default engine (refactoring F2.5-F2.6).</summary>
public static class ConcreteShearAnalysis
{
    public static ConcreteShearResult Calculate(JsonObject input, JsonObject settings, JsonObject options, JsonObject row, ShearTorsionEngine? engine = null)
    {
        ConcreteCalculationSettings.ValidateStirrups(input, options);
        // Derived geometry cannot depend on a WPF panel having been visited.
        var effective = (JsonObject)options.DeepClone();
        var geometry = ConcreteCalculationSettings.UpdateAutomaticShear(input, effective);
        options = effective;
        string code = settings.S("normativa", "NTC 2018");
        ConcreteCodeChecks.RequireOrdinary(code);
        bool circular = input.S("shape") == "Circolare";
        if (circular && options.S("modello_circolare", "Da scegliere") == "Da scegliere") throw new ArgumentException("Scegliere esplicitamente il modello di taglio circolare.");
        if (circular && code != "NTC 2018" && options.S("modello_circolare").StartsWith("Pile"))
            throw new ArgumentException("Il modello delle pile è specifico NTC: per " + code + " assegnare esplicitamente z/d e confermare la schematizzazione circolare.");
        if (settings.Array("trefoli").Count > 0) throw new ArgumentException("Taglio CAP: includere le componenti di precompressione; modello da definire");
        var materials = ConcreteMaterials.DesignValues(input, settings);
        double fcd = materials.Fcd;
        bool stirrups = options.S("modello") == "Con staffe";
        if (stirrups && input.S("staffe_presenti", "Sì") == "No") throw new ArgumentException("Staffe assenti nella sezione: scegliere Senza staffe.");
        if (stirrups && code.StartsWith("DS") && (input.D("steel_eps_u") < 50 || input.D("steel_fu_mpa") < 1.08*input.D("fyk_mpa")))
            throw new ArgumentException("DS: il modello richiede proprietà di duttilità almeno B (εuk ≥ 5%, fu/fyk ≥ 1,08). Classe A: verifica di capacità deformativa non descritta.");
        double torque = SectionWorkspace.Number(row.S("T", "0"), "T");
        if ((!stirrups || code == "Model Code 2010") && options.S("parametri") == "Automatici da sezione" && options.S("ancoraggio") != "Confermato") throw new ArgumentException("Asl ricavata dalla geometria: confermare l’ancoraggio efficace per la verifica senza staffe e per Model Code 2010.");
        double n = SectionWorkspace.Number(row.S("N"), "N"), phi = stirrups ? input.Required("transverse_bar_diameter_mm", strict: true) : 0, spacing = stirrups ? input.Required("transverse_spacing_mm", strict: true) : 1;
        var results = new List<Ntc2018Checks.ShearResult>();
        foreach (var axis in new[] { "x", "y" })
        {
            double Value(string key) => SectionWorkspace.Number(options.S(key + "_" + axis), key + " " + axis);
            double v = SectionWorkspace.Number(row.S("V" + axis), "V" + axis);
            double legs = stirrups ? Value("rami") : 0;
            if (legs < 0 || legs != Math.Truncate(legs) || stirrups && legs == 0) throw new ArgumentException("Numero rami staffa non valido");
            double? cot = !stirrups || string.IsNullOrWhiteSpace(options.S("cot_" + axis)) ? null : Value("cot");
            if (torque != 0) cot = options.Required("cot_torsione");
            double bw = Value("bw"), d = Value("d"), asl = stirrups && code != "Model Code 2010" ? 0 : Value("asl");
            if (bw > (axis == "x" ? geometry.Height : geometry.Width) || d >= (axis == "x" ? geometry.Width : geometry.Height) || asl > geometry.AreaSteel)
                throw new ArgumentException("Taglio " + axis + ": bw, d o Asl superano la geometria/armatura della sezione");
            double lever = circular ? (options.S("modello_circolare").StartsWith("Pile") ? (input.B("foro_presente") ? .60 : .75) : options.Required("z_d")) : .9;
            if (code.StartsWith("DIN"))
            {
                double cv = input.Required("cover_mm") + (input.S("staffe_presenti", "Sì") == "Sì" ? input.Required("transverse_bar_diameter_mm") : 0);
                lever = Math.Min(lever, Math.Max(d - cv - 30, d - 2*cv) / d);
            }
            double moment = code == "Model Code 2010" ? SectionWorkspace.Number(row.S(axis == "x" ? "My" : "Mx"), "Momento per taglio MC2010") : 0;
            var check = ConcreteShearTorsionAdapter.Shear(new(code, n, v, moment, geometry.AreaCls, bw, d, asl,
                input.Required("fck_mpa"), fcd, materials.Fyd, input.Required("gamma_c"), geometry.Es,
                stirrups ? legs * ReinforcementGeometry.Area(phi) : 0, spacing, stirrups ? Value("alpha") : 90, cot, lever,
                code == "Model Code 2010" || code.StartsWith("NS") ? settings["dettagli_costruttivi"]!.AsObject().Required("aggregato") : 20,
                code == "Model Code 2010" ? options.Required("eccentricita_mc_" + axis) : 0), engine);
            results.Add(check);
        }

        if (torque != 0 && code != "NTC 2018") throw new ArgumentException("Torsione accoppiata: il modello attuale è NTC 2018; separare il caso T = 0 per il controllo di taglio " + code + ".");
        return new(results.ToArray(), Torsion(input, options, row, geometry, results.ToArray(), fcd, engine));
    }
    private static TorsionResult? Torsion(JsonObject input, JsonObject options, JsonObject row,
        SezioneCA geometry, Ntc2018Checks.ShearResult[] shear, double fcd, ShearTorsionEngine? engine)
    {
        double torque = SectionWorkspace.Number(row.S("T", "0"), "T");
        if (torque == 0) return null;
        if (input.S("staffe_presenti", "Sì") != "Sì" || options.S("modello") != "Con staffe") throw new ArgumentException("Torsione: occorrono staffe resistenti chiuse.");
        if (options.S("chiusura_torsione") != "Confermato") throw new ArgumentException("Confermare staffe chiuse e barre contenute nel profilo resistente, incluse quelle di spigolo.");
        if (options.D("alpha_x") != 90 || options.D("alpha_y") != 90) throw new ArgumentException("Torsione: modello implementato con staffe a 90°.");
        if (input.S("shape") == "Circolare" && options.S("tipo_staffa") == "Spirale") throw new ArgumentException("Torsione: selezionare staffa chiusa; spirale non equivalente automaticamente.");
        double al = options.Required("as_torsione"); if (al > geometry.AreaSteel) throw new ArgumentException("As disponibile per torsione supera l’armatura totale.");
        var g = ConcreteTorsionCalculator.Geometry(geometry);
        var result = new ConcreteTorsionCalculator().Calculate(new(torque, g, fcd, geometry.Fyd, Math.PI * Math.Pow(input.D("transverse_bar_diameter_mm"), 2) / 4, input.D("transverse_spacing_mm"), al, options.Required("cot_torsione"), row.D("Vx"), row.D("Vy"), shear[0], shear[1]));
        return result;
    }
}
