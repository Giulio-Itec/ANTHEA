using System.Text.Json.Nodes;
using GPC.Design.Sections;
using Materiali;

namespace Anthea.Calculations;

public sealed record ConcreteDetailingResult(double MaximumCompression, NtcCoverResult? Durability,
    string? DurabilityError, IReadOnlyList<DetailingCheck> Checks)
{
    /// <summary>Combination that gives NEd ("nome · stato"); empty when no row is compressed.</summary>
    public string CompressionSource { get; init; } = "";
    /// <summary>NEd comes from a serviceability combination (the most severe compression is a service value).</summary>
    public bool CompressionFromServiceability { get; init; }
}
public sealed record ConcreteAnchorageResult(double Diameter, double Stress, double Fctk05, AnchorageResult Check)
{
    /// <summary>γc usato in fbd: quello della norma effettiva con i coefficienti unitari del foglio del palo, altrimenti gamma_c dell'input.</summary>
    public double GammaC { get; init; }
}

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
        // NEd: the most severe compression of all the combinations, ultimate, seismic, shear and serviceability (user decision of 14/10/2026,
        // GPC.Design). Rows without "stato" are ultimate, as the callers before the decision passed only SLU and SLV. Legacy shear rows
        // explicitly allow an omitted axial component (= 0); a present malformed value is rejected.
        var rows = actions.Select(row => new DesignAxialAction(row.S("nome"), Family(row.S("stato", "SLU")), SectionWorkspace.Number(row.S("N"), "N")));
        rows = rows.Concat(settings["taglio"]!.Array("azioni").OfType<JsonObject>()
            .Select(row => new DesignAxialAction(row.S("nome"), DesignActionFamily.Shear, SectionWorkspace.Number(row.S("N", "0"), "N"))));
        var governing = SectionDesignDemand.DetailingCompression(rows.ToArray());
        double n = governing.Value;
        bool hasStirrups = ConcreteCalculationSettings.HasStirrups(input);
        int legs = kind == ConcreteMemberKind.Beam && hasStirrups
            ? ConcreteCalculationSettings.StirrupLegs(settings["taglio"], "rami_y") : 0;
        var request = new ConcreteDetailingInput(kind, section, n, hasStirrups,
            input.D("transverse_bar_diameter_mm"), input.D("transverse_spacing_mm"), legs,
            Value("aggregato"), durability?.Cover.Durability ?? double.NaN, Value("delta_c"), o.S("zona_sovrapposizione") == "Sì",
            plate ? Value("as_secondaria") : 0, plate ? Value("passo_secondaria") : 0, o.S("zona_critica") == "Sì",
            o.S("barre_trattenute") == "Confermato", o.S("ancoraggio_appoggi") == "Confermato")
        {
            // Coefficienti unitari del foglio c.a. del palo: fyd = fyk della norma effettiva (come TAG-2); senza, SezioneCA.Fyd come prima.
            // Ripiego legittimo (fallbacks.json, ambito dettagli_durabilita): la chiave esiste solo nei fogli del palo (HorizontalSheetPresentation
            // la completa a false); nei fogli della sezione c.a. manca per costruzione e false è il ripiego di ConcreteStandards.Effective.
            DesignYieldStrength = input.B("coefficienti_unitari") ? ConcreteMaterials.DesignValues(input, settings).Fyd : null
        };
        return new(n, durability, durabilityError, new ConcreteDetailingCalculator().Calculate(request))
        {
            CompressionSource = governing.Source is { } s ? s.Name + " · " + FamilyLabel(s.Family) : "",
            CompressionFromServiceability = governing.FromServiceability
        };
    }

    /// <summary>Every combination row of the sheet, marked with its limit state, for the NEd of the detailing rules.</summary>
    public static IEnumerable<JsonObject> AllRows(IReadOnlyDictionary<string, JsonObject[]> rowsBySet)
        => SectionWorkspace.Sets.SelectMany(set => (rowsBySet.TryGetValue(set, out var rows) ? rows : []).Select(row =>
        {
            var copy = (JsonObject)row.DeepClone(); copy["stato"] = set; return copy;
        }));

    static DesignActionFamily Family(string set) => set switch
    {
        "SLU" => DesignActionFamily.Ultimate, "SLV" => DesignActionFamily.Seismic, "SLE" => DesignActionFamily.Characteristic,
        "SLE_FREQ" => DesignActionFamily.Frequent, "SLE_QP" => DesignActionFamily.QuasiPermanent,
        _ => throw new ArgumentException("Stato limite della combinazione non riconosciuto: " + set)
    };

    static string FamilyLabel(DesignActionFamily family) => family switch
    {
        DesignActionFamily.Ultimate => "SLU", DesignActionFamily.Seismic => "SLV", DesignActionFamily.Shear => "taglio",
        DesignActionFamily.Characteristic => "SLE rara", DesignActionFamily.Frequent => "SLE frequente", _ => "SLE quasi permanente"
    };

    public static ConcreteAnchorageResult Anchorage(JsonObject input, JsonObject settings)
    {
        Anthea.ConcreteWorkspace.ConcreteBarMaterials.RequireSheetProperties(input, "Ancoraggi", "fyk_mpa");
        if (settings.S("normativa") != "NTC 2018")
            throw new ArgumentException("Ancoraggi disponibili per NTC 2018 con integrazioni EC2.");
        var section = new SezioneCA(input); var a = settings["ancoraggi"]!;
        double Number(string key) => SectionWorkspace.Number(a.S(key), key);
        // Coefficienti unitari del foglio c.a. del palo (come TAG-2 per taglio e torsione): fyd e γc della norma effettiva, cioè fyk e 1; senza,
        // fyk/γs e gamma_c dell'input come prima, bit per bit e con gli stessi rifiuti.
        bool unit = input.B("coefficienti_unitari");
        double fyd = unit ? ConcreteMaterials.DesignValues(input, settings).Fyd : section.Fyd;
        double gammaC = unit ? ConcreteStandards.Effective(input, settings).GammaC : input.D("gamma_c");
        double phi = a.S("diametro").Trim() == "" ? section.Bars.Max(v => v.Diametro) : Number("diametro");
        double sigma = a.S("sigma").Trim() == "" ? fyd : Number("sigma");
        if (sigma > fyd) throw new ArgumentException("La tensione di ancoraggio non può superare fyd.");
        if (a.S("lunghezza").Trim() == "") throw new ArgumentException("Inserire la lunghezza disponibile per eseguire la verifica.");
        _ = ConcreteMaterials.Concrete(input); // Preserve material/diagram validation; the bond cap belongs to Checker.
        double fct = GPC.Checkers.Concrete.Detailing.AnchorageCalculator.BondFctk05(input.D("fck_mpa"));
        bool lap = a.S("tipo").StartsWith("Sovrapposizione");
        // Tipo di barra dalla sezione (Barre: Migliorata/Liscia delle SLE), con la stessa lettura e lo stesso predefinito della fessurazione (DET-2).
        bool ribbed = settings["sle_comuni"].S("aderenza", "Migliorata") == "Migliorata";
        var result = new ConcreteAnchorageCalculator().Calculate(new(phi, sigma, fct, gammaC,
            a.S("aderenza") == "Buona", Number("lunghezza"), lap, lap ? Number("percentuale") : 100, lap ? Number("interferro") : 0, ribbed));
        return new(phi, sigma, fct, result) { GammaC = gammaC };
    }
}
