using System.Text.Json.Nodes;
using Materiali;

namespace Anthea.Calculations;

public sealed record ConcreteDetailingResult(double MaximumCompression, NtcCoverResult? Durability,
    string? DurabilityError, IReadOnlyList<DetailingCheck> Checks);
public sealed record ConcreteAnchorageResult(double Diameter, double Stress, double Fctk05, AnchorageResult Check);

/// <summary>Same section, durability and anchorage adapters for interactive and nonvisual calculations.</summary>
public static class ConcreteDetailingAnalysis
{
    public static ConcreteDetailingResult Calculate(JsonObject input, JsonObject settings, IEnumerable<JsonObject> actions)
    {
        if (settings.S("normativa") != "NTC 2018")
            throw new ArgumentException("I dettagli implementati sono riferiti a NTC 2018 con integrazioni EC2.");
        var o = settings["dettagli_costruttivi"]!.AsObject();
        var kind = o.S("elemento") switch
        {
            "Trave" => ConcreteMemberKind.Beam,
            "Pilastro" => ConcreteMemberKind.Column,
            "Soletta piena" => ConcreteMemberKind.Slab,
            "Parete" => ConcreteMemberKind.Wall,
            _ => throw new ArgumentException("Scegliere il tipo di elemento.")
        };
        var section = new SezioneCA(input);
        bool plate = kind is ConcreteMemberKind.Slab or ConcreteMemberKind.Wall;
        double Value(string key) => SectionWorkspace.Number(o.S(key), key);
        NtcCoverResult? durability = null; string? durabilityError = null;
        try
        {
            string exposure = settings["sle_comuni"].S("esposizione");
            var exposures = new[] { Durability.Exposures.FirstOrDefault(e => e.Code == exposure)
                ?? throw new ArgumentException("Scegliere la classe di esposizione nella scheda Tensioni e fessurazione (SLE).") };
            var cp = new CoverInput(SectionWorkspace.Subdivisions(o.S("vita_durabilita"), "Vita utile", 50, 100),
                false, false, false, section.Bars.Max(b => b.Diametro), Value("aggregato"), Value("delta_c"), false, 0, 0);
            durability = NtcCover.Calculate(exposures, input.D("fck_mpa"), cp, plate,
                o.S("qualita_copriferro") == "Sì", MinimumConcrete.Required(exposures));
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { durabilityError = ex.Message; }
        double n = actions.Select(row => Math.Max(0, -SectionWorkspace.Number(row.S("N"), "N"))).DefaultIfEmpty(0).Max();
        var request = new ConcreteDetailingInput(kind, section, n, input.S("staffe_presenti", "Sì") == "Sì",
            input.D("transverse_bar_diameter_mm"), input.D("transverse_spacing_mm"), (int)settings["taglio"].D("rami_y", 2),
            Value("aggregato"), durability?.Cover.Durability ?? double.NaN, Value("delta_c"), o.S("zona_sovrapposizione") == "Sì",
            plate ? Value("as_secondaria") : 0, plate ? Value("passo_secondaria") : 0, o.S("zona_critica") == "Sì",
            o.S("barre_trattenute") == "Confermato", o.S("ancoraggio_appoggi") == "Confermato");
        return new(n, durability, durabilityError, new ConcreteDetailingCalculator().Calculate(request));
    }

    public static ConcreteAnchorageResult Anchorage(JsonObject input, JsonObject settings)
    {
        if (settings.S("normativa") != "NTC 2018")
            throw new ArgumentException("Ancoraggi disponibili per NTC 2018 con integrazioni EC2.");
        var section = new SezioneCA(input); var a = settings["ancoraggi"]!;
        double Number(string key) => SectionWorkspace.Number(a.S(key), key);
        double phi = a.S("diametro").Trim() == "" ? section.Bars.Max(v => v.Diametro) : Number("diametro");
        double sigma = a.S("sigma").Trim() == "" ? section.Fyd : Number("sigma");
        if (sigma > section.Fyd) throw new ArgumentException("La tensione di ancoraggio non può superare fyd.");
        if (a.S("lunghezza").Trim() == "") throw new ArgumentException("Inserire la lunghezza disponibile per eseguire la verifica.");
        var bondInput = (JsonObject)input.DeepClone(); bondInput["fck_mpa"] = Math.Min(input.D("fck_mpa"), 60);
        double fct = Math.Abs(ConcreteMaterials.Concrete(bondInput).Fctk05);
        bool lap = a.S("tipo").StartsWith("Sovrapposizione");
        var result = new ConcreteAnchorageCalculator().Calculate(new(phi, sigma, fct, input.D("gamma_c"),
            a.S("aderenza") == "Buona", Number("lunghezza"), lap, lap ? Number("percentuale") : 100, lap ? Number("interferro") : 0));
        return new(phi, sigma, fct, result);
    }
}
