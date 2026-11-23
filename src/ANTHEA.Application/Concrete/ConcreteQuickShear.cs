using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Anthea.Application.Concrete;

/// <summary>Document adapter for the quick shear panel. All resistances and geometric parameters come from the existing section services.</summary>
public static class ConcreteQuickShear
{
    public const string Key = "taglio";
    public const string EffectiveLink = "staffa_effettiva";
    public const string Diameter = "diametro_mm";
    public const string Spacing = "passo_mm";
    public const string ConditionField = "verifica";
    public const string WithLinks = "Con staffe";
    public const string WithoutLinks = "Senza staffe";
    public static IReadOnlyList<ConcreteQuickShearChoice> Choices { get; } = [
        new(WithLinks, "Verifica con armatura a taglio", "Con armatura"),
        new(WithoutLinks, "Verifica sezione non armata", "Non armata")];
    public static string Condition(JsonObject data, JsonObject row) => row.S(ConditionField, Options(data).S("modello", WithLinks));
    public static string Model(JsonObject data, JsonObject row) => row.S("modello", Condition(data, row));
    public static string Label(string code) => Choices.FirstOrDefault(c => c.Code == code)?.Label ?? code;
    public static string ShortLabel(string code) => Choices.FirstOrDefault(c => c.Code == code)?.ShortLabel ?? code;
    public static bool Enabled(JsonObject data) => data[ConcreteQuickSection.ViewKey]?[Key] is JsonObject;
    public static bool Expanded(JsonObject data) => data[ConcreteQuickSection.ViewKey]?[Key]?.B("aperto") == true;
    public static bool UsesGeometryLink(JsonObject data) => Options(data).B(EffectiveLink);
    private static string GeometrySignature(JsonObject data) => data["input"]!.ToJsonString() + "\n"
        + data[ConcreteQuickSection.ViewKey]?[ConcreteQuickSection.LayersKey]?.ToJsonString() + "\n"
        + data[ConcreteQuickSection.ViewKey]?[ConcreteQuickSection.SecondRingLinkKey]?.ToJsonString();

    /// <summary>A detached editable representation; reading a closed or old sheet writes nothing.</summary>
    public static JsonObject Options(JsonObject data)
    {
        if (data[ConcreteQuickSection.ViewKey]?[Key] is JsonObject saved)
        {
            var copy = (JsonObject)saved.DeepClone();
            if (copy.S("ancoraggio") == "Confermato" && copy.S("input_ancoraggio") != GeometrySignature(data)) copy["ancoraggio"] = "Da verificare";
            return copy;
        }
        var options = data["workspace_ca"]?["taglio"] is JsonObject source ? (JsonObject)source.DeepClone() : new JsonObject();
        options["azioni"] = new JsonArray();
        options[EffectiveLink] = data["input"].S("shape") != ConcreteQuickSection.Dxf && data["input"].S("staffe_presenti", "Sì") == "Sì";
        options[Diameter] = "";
        options[Spacing] = data["input"].S("transverse_spacing_mm", "200");
        options["aperto"] = false;
        options["ancoraggio"] = "Da verificare";
        return options;
    }

    public static void Write(JsonObject data, string key, JsonNode? value)
    {
        var options = Options(data);
        options[key] = value?.DeepClone();
        if (key == "ancoraggio") options["input_ancoraggio"] = GeometrySignature(data);
        ConcreteQuickSection.View(data)[Key] = options;
    }

    public static string LinkDiameter(JsonObject data) => UsesGeometryLink(data)
        ? data["input"].S("transverse_bar_diameter_mm") : Options(data).S(Diameter);

    public static IReadOnlyList<JsonObject> Combinations(JsonObject data) => Options(data).Array("azioni").OfType<JsonObject>().ToArray();
    public static string? Selected(JsonObject data)
    {
        var rows = Combinations(data); string id = Options(data).S("selezionata");
        return rows.FirstOrDefault(r => r.S("id") == id)?.S("id") ?? rows.FirstOrDefault()?.S("id");
    }
    private static JsonObject NewRow(string name) => new()
    {
        ["id"] = Guid.NewGuid().ToString("N"), ["nome"] = name, [ConditionField] = WithLinks, ["N"] = "0", ["Vx"] = "0", ["Vy"] = "0", ["Mx"] = "0", ["My"] = "0"
    };
    private static void StoreRows(JsonObject data, IEnumerable<JsonObject> rows) => Write(data, "azioni", new JsonArray(rows.Select(r => (JsonNode?)r.DeepClone()).ToArray()));
    public static void EnsureCombination(JsonObject data)
    {
        if (Combinations(data).Count > 0) return;
        var row = NewRow("Combo 1"); row["iniziale"] = true;
        StoreRows(data, [row]); Write(data, "selezionata", row["id"]);
    }
    public static void AddCombination(JsonObject data)
    {
        var rows = Combinations(data).ToList(); int number = 1;
        while (rows.Any(r => r.S("nome") == "Combo " + number)) number++;
        var row = NewRow("Combo " + number); rows.Add(row); StoreRows(data, rows); Write(data, "selezionata", row["id"]);
    }
    /// <summary>Copy names and N/M actions; preserve shear forces, condition/model choices and independently edited rows.</summary>
    public static void CopyCombinations(JsonObject data)
    {
        var existing = Combinations(data).Where(r => !r.B("iniziale")).ToList(); var copied = new List<JsonObject>();
        foreach (var source in ConcreteQuickSection.Combinations(data))
        {
            string sourceId = source.S("id");
            var row = existing.FirstOrDefault(r => r.S("origine_id") == sourceId) ?? NewRow(source.S("nome"));
            existing.Remove(row);
            row["origine_id"] = sourceId; row["nome"] = source.S("nome");
            foreach (string field in new[] { "N", "Mx", "My" }) row[field] = source.S(field, "0");
            copied.Add(row);
        }
        copied.AddRange(existing); StoreRows(data, copied);
        if (Selected(data) is { } selected) Write(data, "selezionata", JsonValue.Create(selected));
    }
    public static void WriteCombination(JsonObject data, string id, string key, string value)
    {
        if (key is not ("nome" or ConditionField or "modello" or "N" or "Vx" or "Vy" or "Mx" or "My")) throw new ArgumentException("Campo della combinazione a taglio non valido.");
        if (key is ConditionField or "modello" && !Choices.Any(c => c.Code == value)) throw new ArgumentException("Tipologia di verifica a taglio non valida.");
        var rows = Combinations(data); var row = rows.Single(r => r.S("id") == id);
        row[key] = value; row.Remove("iniziale");
        if (key == ConditionField) row.Remove("modello");
        StoreRows(data, rows);
    }
    public static bool CanResetConditions(JsonObject data) => Combinations(data).Any(r => Model(data, r) != Condition(data, r));
    public static void ResetConditions(JsonObject data)
    {
        var rows = Combinations(data);
        foreach (var row in rows) row.Remove("modello");
        StoreRows(data, rows);
    }
    public static void RemoveCombination(JsonObject data, string id)
    {
        if (Combinations(data).Count > 1) StoreRows(data, Combinations(data).Where(r => r.S("id") != id));
    }
    public static void ReplaceCombinations(JsonObject data, IReadOnlyList<ConcreteQuickShearExcel.Row> source)
    {
        ConcreteQuickShearExcel.Validate(source);
        var rows = source.Select(r =>
        {
            var row = NewRow(r.Name.Trim()); row[ConditionField] = r.Condition;
            row["N"] = r.N.ToString(System.Globalization.CultureInfo.InvariantCulture);
            row["Vx"] = r.Vx.ToString(System.Globalization.CultureInfo.InvariantCulture);
            row["Vy"] = r.Vy.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return row;
        }).ToArray();
        StoreRows(data, rows); Write(data, "selezionata", rows[0]["id"]);
    }
    public static void MoveCombination(JsonObject data, string id, int direction)
    {
        var rows = Combinations(data).ToList(); int index = rows.FindIndex(r => r.S("id") == id), next = index + direction;
        if (index < 0 || next < 0 || next >= rows.Count) return;
        var row = rows[index]; rows.RemoveAt(index); rows.Insert(next, row); StoreRows(data, rows);
    }

    /// <summary>Build a calculation-only input: the original longitudinal bars are frozen before selecting the actual shear reinforcement.</summary>
    public static (JsonObject Input, JsonObject Settings) Prepare(JsonObject data, JsonObject? combination = null)
    {
        // Use the effective longitudinal layout, exactly as the drawing and flexural calculation do, including report-only callers.
        var prepared = (JsonObject)data.DeepClone();
        if (ConcreteQuickSection.ApplyLayers(prepared) is { } layerError) throw new ArgumentException(layerError);
        var input = prepared["input"]!.AsObject();
        var settings = (JsonObject)data["workspace_ca"]!.DeepClone();
        var options = Options(data);
        combination ??= Combinations(data).FirstOrDefault(r => r.S("id") == Selected(data));
        if (combination is not null) options["modello"] = Model(data, combination);
        if (input.S("shape") == ConcreteQuickSection.Dxf && options.S("parametri") != "Manuali")
            throw new ArgumentException("Sezione DXF: il taglio richiede parametri geometrici «Manuali» (bw, d e Asl) già assegnati per ciascuna direzione; il pannello rapido non ne consente la modifica.");
        if (options.S("modello") == WithLinks && UsesGeometryLink(data) && (input.S("shape") == ConcreteQuickSection.Dxf || input.S("staffe_presenti", "Sì") != "Sì"))
            throw new ArgumentException("Staffa geometrica non disponibile: togliere «Staffa effettiva?» e assegnare l’armatura a taglio.");
        var geometry = ConcretePreview.PrepareGeometry(input);
        input["barre_manuali"] = new JsonArray(geometry.Bars.Select(b => (JsonNode?)new JsonObject
            { ["x"] = b.X, ["y"] = b.Y, ["phi"] = b.Diametro }).ToArray());
        bool links = options.S("modello", "Con staffe") == "Con staffe";
        if (options.S("modello") is not ("Con staffe" or "Senza staffe")) throw new ArgumentException("Scegliere il modello di taglio.");
        if (links)
        {
            if (string.IsNullOrWhiteSpace(LinkDiameter(data))) throw new ArgumentException("Inserire il diametro della nuova armatura a taglio.");
            input["transverse_bar_diameter_mm"] = LinkDiameter(data);
            input["transverse_spacing_mm"] = options.S(Spacing);
            input["staffe_presenti"] = "Sì";
        }
        else input["staffe_presenti"] = "No";
        // Derive on the actual longitudinal layout. Do not overwrite a user's explicit anchorage confirmation while preparing an isolated calculation.
        string anchorage = options.S("ancoraggio");
        ConcreteSheetPreparation.UpdateAutomaticShear(input, options);
        options["ancoraggio"] = anchorage;
        settings["taglio"] = options;
        return (input, settings);
    }

    public static IReadOnlyList<ConcreteQuickShearCase> Calculate(JsonObject data, CancellationToken cancellation = default)
    {
        if (!Enabled(data)) return [];
        var results = new List<ConcreteQuickShearCase>();
        foreach (var source in Combinations(data))
        {
            try
            {
                cancellation.ThrowIfCancellationRequested();
                var (input, settings) = Prepare(data, source);
                var row = (JsonObject)source.DeepClone();
                row["Vx"] = source.S("Vx", "0"); row["Vy"] = source.S("Vy", "0"); row["T"] = "0";
                if (ConcreteQuickSection.View(data).S("flessione") == "Retta") row["My"] = "0";
                var output = new List<ConcreteQuickShearDirection>();
                string error = "";
                ConcreteShearBatch.Calculate(input, settings, [row], (_, result, failure) =>
                {
                    error = failure ?? "";
                    if (result is null) return;
                    foreach (var (check, i) in result.Shear.Select((v, i) => (v, i)))
                    {
                        string axis = i == 0 ? "x" : "y";
                        var options = settings["taglio"]!;
                        output.Add(new(axis.ToUpperInvariant(), row.S("V" + axis), check.VRsd, check.VRcd, check.VRd, check.Ratio,
                            check.CotTheta, check.Status, check.Reference, string.Join("\n", check.Details.Select(d => d.Format())),
                            options.S("bw_" + axis), options.S("d_" + axis), options.S("asl_" + axis)));
                    }
                }, cancellation);
                results.Add(new(source.S("id"), source.S("nome"), Condition(data, source), Model(data, source), output, error));
            }
            catch (ArgumentException failure)
            {
                results.Add(new(source.S("id"), source.S("nome"), Condition(data, source), Model(data, source), [], failure.Message));
            }
        }
        return results;
    }
}

public sealed record ConcreteQuickShearChoice(string Code, string Label, string ShortLabel);
public sealed record ConcreteQuickShearCase(string Id, string Name, string Condition, string Model, IReadOnlyList<ConcreteQuickShearDirection> Directions, string Error);
public sealed record ConcreteQuickShearDirection(string Axis, string Action, double VRsd, double VRcd, double VRd, double? Ratio,
    double CotTheta, string Status, string Reference, string Detail, string Width, string Depth, string SteelArea)
{
    public bool? Passed => Ratio is double ratio ? ratio <= 1 : null;
    public string Resistance => EngineeringFormat.Number(VRd);
    public string SteelResistance => EngineeringFormat.Number(VRsd);
    public string ConcreteResistance => EngineeringFormat.Number(VRcd);
    public string Utilization => EngineeringFormat.Number(Ratio);
}
