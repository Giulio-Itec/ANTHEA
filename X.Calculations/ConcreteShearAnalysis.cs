using System.Text.Json.Nodes;

namespace Anthea.Calculations;

public sealed record ConcreteShearResult(Ntc2018Checks.ShearResult[] Shear, TorsionResult? Torsion);

/// <summary>Shared adapter from section inputs to the existing NTC shear/torsion calculators.</summary>
public static class ConcreteShearAnalysis
{
    public static ConcreteShearResult Calculate(JsonObject input, JsonObject settings, JsonObject options, JsonObject row)
    {
        ConcreteCalculationSettings.ValidateStirrups(input, options);
        // Derived geometry cannot depend on a WPF panel having been visited.
        var effective = (JsonObject)options.DeepClone();
        var geometry = ConcreteCalculationSettings.UpdateAutomaticShear(input, effective);
        options = effective;
        if (settings.S("normativa") != "NTC 2018") throw new ArgumentException("Selezionare NTC 2018 nel pannello di controllo");
        bool circular = input.S("shape") == "Circolare";
        if (circular && options.S("modello_circolare", "Da scegliere") == "Da scegliere") throw new ArgumentException("Scegliere esplicitamente il modello di taglio circolare.");
        if (settings.Array("trefoli").Count > 0) throw new ArgumentException("Taglio CAP: includere le componenti di precompressione; modello da definire");
        double fcd = geometry.Fcd * (input.S("gettato_sottile") == "Sì" ? .8 : 1);
        bool stirrups = options.S("modello") == "Con staffe";
        if (stirrups && input.S("staffe_presenti", "Sì") == "No") throw new ArgumentException("Staffe assenti nella sezione: scegliere Senza staffe.");
        double torque = SectionWorkspace.Number(row.S("T", "0"), "T");
        if (!stirrups && options.S("parametri") == "Automatici da sezione" && options.S("ancoraggio") != "Confermato") throw new ArgumentException("Asl ricavata dalla geometria: confermare l’ancoraggio efficace prima della verifica senza staffe.");
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
            double bw = Value("bw"), d = Value("d"), asl = stirrups ? 0 : Value("asl");
            if (bw > (axis == "x" ? geometry.Height : geometry.Width) || d >= (axis == "x" ? geometry.Width : geometry.Height) || asl > geometry.AreaSteel)
                throw new ArgumentException("Taglio " + axis + ": bw, d o Asl superano la geometria/armatura della sezione");
            double lever = circular ? (options.S("modello_circolare").StartsWith("Pile") ? (input.B("foro_presente") ? .60 : .75) : options.Required("z_d")) : .9;
            var check = Ntc2018Checks.Shear(n, v, geometry.AreaCls, bw, d, asl, input.Required("fck_mpa"), fcd, geometry.Fyd, input.Required("gamma_c"), legs * Math.PI * phi * phi / 4, spacing, stirrups ? Value("alpha") : 90, cot, lever);
            results.Add(check);
        }

        return new(results.ToArray(), Torsion(input, options, row, geometry, results.ToArray(), fcd));
    }
    private static TorsionResult? Torsion(JsonObject input, JsonObject options, JsonObject row,
        SezioneCA geometry, Ntc2018Checks.ShearResult[] shear, double fcd)
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
