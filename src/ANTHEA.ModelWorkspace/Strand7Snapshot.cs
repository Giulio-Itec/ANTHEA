using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GPC.Converter.Straus7;
using GPC.Model.Core.Diagnostics;
using GPC.Model.Elements;

namespace ANTHEA.ModelWorkspace;

/// <summary>Straus7 conventions for <see cref="GpcModelSnapshot"/> on a model imported by the GPC Straus7 R3 converter. Reads only the
/// records the converter preserves ("Straus7 API" evidence: unit codes, element and property records); no native code, no file access.
/// Numbering: nodes and plates keep their Straus7 API numbers; beams, trusses and cables get <see cref="BeamIdOffset"/> + their number,
/// because Straus7 numbers beams and plates separately and the document needs one identity per element.
/// Cases: "1: Dead", as Straus7 names its linear static result cases. Properties: Straus7 number and name.</summary>
public static partial class Strand7Snapshot
{
    public const int BeamIdOffset = 1000000;
    public const string CombinationSource = "STRAUS7";
    const string Evidence = "Straus7 API";
    static readonly IReadOnlyList<double> LengthToMillimetres = [1000, 10, 1, 304.8, 25.4];
    // 1.16.0: GPC mm, t·mm², t/mm, t/mm², °C/mm, N and N·mm of Straus7ModelData and of the equilibrium check to the m, t·m², t/m, t/m², °C/m, kN and kN·m of the copy.
    const double MillimetresPerMetre = 1000, SquareMillimetresPerSquareMetre = 1e6, NewtonsPerKilonewton = 1000, NewtonMillimetresPerKilonewtonMetre = 1e6;

    [GeneratedRegex(@"^Case (\d+): (.*)$", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex ModelCase();

    /// <summary>"Case 1: Dead" (GPC load case of the converter) → "1: Dead" (the Straus7 result case).</summary>
    public static string CaseName(string gpcName) => ModelCase().Match(gpcName) is { Success: true } m ? m.Groups[1].Value + ": " + m.Groups[2].Value : gpcName;

    /// <summary>Name of a staged nonlinear result case in the catalog: "NLA · phase · increment".</summary>
    public static string NonlinearCaseName(string phase, string step) => "NLA · " + phase + " · " + step;

    /// <summary>
    /// 17/11/2026 (GPC.Converter 2.25; decision of the user of 17/11/2026: the code of the converter, not the recognition of its English text):
    /// the reason why the converter does not map a result case to a load case, in Italian for the import panel. It comes from the code
    /// (<see cref="Straus7LinearStaticCase.ReasonCode"/>) and the numbers of the case: <paramref name="loadCase"/>, <paramref name="freedomCase"/>
    /// and <paramref name="listedFreedomCaseName"/> (the name of the freedom case in the listing) of <see cref="Straus7LinearStaticCase"/>, the
    /// freedom case data of the import (<paramref name="data"/>: the names in the model and the freedom cases of the linear static settings) and
    /// the freedom cases under which the listing has the load case (<paramref name="listedFreedomCases"/>). The English text
    /// (<paramref name="reason"/>) is used only for a code this version does not translate, or one without the numbers it needs, after
    /// «motivo del convertitore GPC:». Until GPC.Converter 2.24 (the AY candidate) ANTHEA recognised the English sentences.
    /// </summary>
    public static string ExclusionReason(Straus7CaseExclusionReason code, int? loadCase, int? freedomCase, string? listedFreedomCaseName, string? reason,
        Straus7FreedomCaseData? data, IReadOnlyList<int> listedFreedomCases)
    {
        const string OneCasePerLoadCase = "il modello GPC tiene i risultati di un caso di carico, non di ogni freedom case";
        static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        static string Labels(IEnumerable<int> freedomCases) => string.Join(", ", freedomCases.Select(f => Straus7FreedomCaseData.Label(f)));
        switch (code)
        {
            case Straus7CaseExclusionReason.NoMatchingLoadCase:
                return "nessun caso di carico del modello ha il numero e il nome di questo caso di risultato";
            case Straus7CaseExclusionReason.FreedomCaseMissing when freedomCase is int missing:
                return "il modello non ha il freedom case " + Number(missing);
            case Straus7CaseExclusionReason.FreedomCaseRenamed when freedomCase is int renamed && data?.FreedomCases.FirstOrDefault(f => f.Number == renamed) is { } named:
                return "il freedom case " + Number(renamed) + " del modello si chiama «" + named.Name + "», non «" + listedFreedomCaseName + "»";
            case Straus7CaseExclusionReason.NotSolvedUnderFreedomCase when loadCase is int load && freedomCase is int other && data is not null:
            {
                var settings = data.FreedomCasesOf(load);
                return "l’analisi statica lineare del modello risolve il caso di carico " + Number(load) +
                    (settings.Count == 0 ? " sotto nessun freedom case" : " sotto " + Labels(settings)) + ", non sotto " + Straus7FreedomCaseData.Label(other);
            }
            case Straus7CaseExclusionReason.SolvedUnderSeveralFreedomCases when loadCase is int shared && data is not null:
                return "l’analisi statica lineare risolve il caso di carico " + Number(shared) + " sotto " + Labels(data.FreedomCasesOf(shared)) + ": " + OneCasePerLoadCase;
            case Straus7CaseExclusionReason.ListedUnderSeveralFreedomCases when loadCase is int listed && listedFreedomCases.Count > 0:
                return "il file dei risultati risolve il caso di carico " + Number(listed) + " sotto i freedom case " + string.Join(", ", listedFreedomCases.Select(Number)) + ": " + OneCasePerLoadCase;
        }
        string text = (reason ?? "").Trim();
        return text.Length == 0 ? "il convertitore GPC non lo associa a un caso di carico del modello" : "motivo del convertitore GPC: " + text;
    }

    /// <summary>
    /// 17/11/2026 (GPC.Converter 2.24; revision P3: built here, not in the worker, so that a check without Straus7 can run it on a GPC Model):
    /// the result cases of a linear static listing (St7GetResultCaseName, <paramref name="listed"/>) for the catalogue, from
    /// <see cref="Straus7LinearStaticResults.Cases"/>, the same mapping as <see cref="Straus7LinearStaticResults.CaseMap"/>. A selectable case keeps
    /// its result case number (the one the worker reads) and the name of its load case in the copy («2: Q»), with its freedom case in a solution of
    /// several freedom cases («2: Q · FC 2»); a case left out keeps its native name, with the reason of the converter in Italian, from its code
    /// (<see cref="ExclusionReason"/>). Null when the converter does not describe each listed case once, in the order of the listing (note N1 of
    /// the revision: the numbers, not only the count).
    /// </summary>
    public static Strand7LinearCases? LinearCases(GPC.Model.Models.Model model, IReadOnlyList<Straus7ResultCase> listed)
    {
        var described = Straus7LinearStaticResults.Cases(model, listed);
        if (described.Count != listed.Count || described.Where((d, i) => d.Number != listed[i].Number).Any()) return null;
        var pairs = listed.Zip(described, (c, d) => (Case: c, Described: d)).ToArray();
        var cases = pairs.Where(p => p.Described.ModelCase != null)
            .Select(p => new Strand7ResultCase(p.Case.Number, CaseName(p.Described.ModelCase))
            { StageNumber = p.Case.Stage == 0 ? null : p.Case.Stage, FreedomCaseLabel = p.Described.FreedomCase is null ? null : p.Described.Label }).ToArray();
        var data = Straus7FreedomCaseData.Read(model);
        // The freedom cases under which the listing has a load case (the result cases of the pairs with that load case), for the reason of a
        // load case listed under several freedom cases.
        int[] Listed(int? load) => load is int l ? described.Where(d => d.LoadCase == l && d.FreedomCase is not null).Select(d => d.FreedomCase!.Value).Distinct().Order().ToArray() : [];
        var excluded = pairs.Where(p => p.Described.ModelCase == null)
            .Select(p => new Strand7ExcludedResultCase(p.Case.Number, p.Case.Name, ExclusionReason(p.Described.ReasonCode, p.Described.LoadCase, p.Described.FreedomCase,
                p.Described.FreedomCaseName, p.Described.Reason, data, Listed(p.Described.LoadCase)))).ToArray();
        return new(cases, excluded);
    }

    /// <summary>17/11/2026 (GPC.Converter 2.24): the freedom cases of a model imported with several of them (<see cref="Straus7FreedomCaseData"/>)
    /// and the load cases the linear static settings solve under each one, «FC 2 «Freedom Case 1»: casi di carico 5, 6»; empty otherwise.</summary>
    public static IReadOnlyList<string> FreedomCases(GPC.Model.Models.Model model)
    {
        var data = Straus7FreedomCaseData.Read(model);
        if (data is null) return [];
        return data.FreedomCases.Select(f =>
        {
            var loads = data.LinearCases.Where(p => p.FreedomCase == f.Number).Select(p => p.LoadCase).Distinct().Order().ToArray();
            return Straus7FreedomCaseData.Label(f.Number) + " «" + f.Name + "»: " +
                (loads.Length == 0 ? "nessun caso di carico" : "casi di carico " + string.Join(", ", loads.Select(l => l.ToString(CultureInfo.InvariantCulture))));
        }).ToArray();
    }

    /// <summary>"1: name", the label of a Straus7 stage in the copy (phase of the stage results, stage definitions).</summary>
    public static string StageLabel(int number, string? name) => number.ToString(CultureInfo.InvariantCulture) + ": " + (string.IsNullOrWhiteSpace(name) ? "fase" : name.Trim());

    /// <summary>1.16.0: masses, temperatures and stages of <see cref="Straus7ModelData"/> as data of the copy, in m, t and °C: node and plate numbers as
    /// they are, beams with <see cref="BeamIdOffset"/>; data of nodes or elements outside the copy are left out and counted in the returned message.</summary>
    public static ModelImportMessage? AddModelData(GPC.Model.Models.Model model, ModelSnapshot snapshot)
    {
        var data = Straus7ModelData.Read(model);
        var elements = snapshot.Elements.ToDictionary(e => e.Id); var nodes = snapshot.Nodes.Select(n => n.Id).ToHashSet();
        static ModelDirection? Offset(double[]? o) => o is { Length: 3 } ? new(o[0] / MillimetresPerMetre, o[1] / MillimetresPerMetre, o[2] / MillimetresPerMetre) : null;
        string Case(string? gpc) => CaseName(gpc ?? "");
        int skipped = 0;
        var masses = new List<ModelMass>();
        foreach (var m in data.Masses)
        {
            int beam = BeamIdOffset + m.Number;
            bool node = m.Kind is Straus7MassKind.NodeTranslational or Straus7MassKind.NodeRotational or Straus7MassKind.NodeNonStructural;
            if (node ? !nodes.Contains(m.Number) : m.Kind == Straus7MassKind.BeamNonStructural ? !elements.ContainsKey(beam) : !elements.TryGetValue(m.Number, out var plate) || plate.Type != "PLATE")
            { skipped++; continue; }
            masses.Add(m.Kind switch
            {
                Straus7MassKind.NodeTranslational => new ModelMass("NODE", m.Number, null, m.Values) { Record = m.Record },
                Straus7MassKind.NodeRotational => new ModelMass("NODE-ROTATION", m.Number, null, m.Values.Select(v => v / SquareMillimetresPerSquareMetre).ToArray())
                {
                    Axes = m.Axes is { Length: 9 } a ? new ModelNodeFrame(m.Number, new(a[0], a[1], a[2]), new(a[3], a[4], a[5]), new(a[6], a[7], a[8])) : null, Record = m.Record
                },
                Straus7MassKind.NodeNonStructural => new ModelMass("NODE-NS", m.Number, Case(m.Case), m.Values) { Offset = Offset(m.Offset), DynamicFactor = m.DynamicFactor, Record = m.Record },
                Straus7MassKind.BeamNonStructural => new ModelMass("BEAM-NS", beam, Case(m.Case), m.Values.Select(v => v * MillimetresPerMetre).ToArray())
                    { Stations = m.Stations, Offset = Offset(m.Offset), DynamicFactor = m.DynamicFactor, Record = m.Record },
                _ => new ModelMass("PLATE-NS", m.Number, Case(m.Case), m.Values.Select(v => v * SquareMillimetresPerSquareMetre).ToArray()) { Offset = Offset(m.Offset), DynamicFactor = m.DynamicFactor, Record = m.Record }
            });
        }
        var temperatures = new List<ModelTemperature>();
        foreach (var t in data.Temperatures)
        {
            int entity = t.Kind switch { Straus7TemperatureKind.BeamGradient => BeamIdOffset + t.Number, Straus7TemperatureKind.ReferenceTemperature => 0, _ => t.Number };
            if (t.Kind == Straus7TemperatureKind.NodeTemperature ? !nodes.Contains(entity) : t.Kind != Straus7TemperatureKind.ReferenceTemperature
                && (!elements.TryGetValue(entity, out var owner) || (owner.Type == "PLATE") != (t.Kind == Straus7TemperatureKind.PlateGradient))) { skipped++; continue; }
            temperatures.Add(t.Kind switch
            {
                Straus7TemperatureKind.ReferenceTemperature => new ModelTemperature("REFERENCE", 0, Case(t.Case), t.Values) { Record = t.Record },
                Straus7TemperatureKind.NodeTemperature => new ModelTemperature("NODE", entity, Case(t.Case), t.Values) { Type = t.Type, Record = t.Record },
                Straus7TemperatureKind.BeamGradient => new ModelTemperature("BEAM-GRADIENT", entity, Case(t.Case), t.Values.Select(v => v * MillimetresPerMetre).ToArray()) { Record = t.Record },
                _ => new ModelTemperature("PLATE-GRADIENT", entity, Case(t.Case), t.Values.Select(v => v * MillimetresPerMetre).ToArray()) { Record = t.Record }
            });
        }
        var stages = data.Stages.Select(s => new ModelStageDefinition(s.Number, s.Name ?? "", StageLabel(s.Number, s.Name), s.Type.ToString(), s.EnabledGroups ?? [],
            (s.ActiveBeams ?? []).Select(b => BeamIdOffset + b).Where(elements.ContainsKey).Concat((s.ActivePlates ?? []).Where(elements.ContainsKey)).Order().ToArray())).ToArray();
        snapshot.Masses = masses.Count > 0 ? masses.ToArray() : null;
        snapshot.Temperatures = temperatures.Count > 0 ? temperatures.ToArray() : null;
        snapshot.ConstructionStages = stages.Length > 0 ? stages : null;
        if (masses.Count + temperatures.Count + stages.Length == 0 && skipped == 0) return null;
        return new("STRAUS7-MODEL-DATA", skipped > 0 ? "Warning" : "info", "Masse, temperature e fasi",
            $"{stages.Length} fasi, {masses.Count} masse e {temperatures.Count} temperature come dati del modello (nessun carico ne deriva): masse in t, t/m, t/m², t·m²; temperature in °C, gradienti in °C/m." +
            (skipped > 0 ? $" {skipped} dati di nodi o elementi assenti dalla copia non sono riportati." : ""));
    }

    /// <summary>The GPC stage name of a state (Phase) as the label of its Straus7 stage.</summary>
    public static Func<string, string> StagePhase(GPC.Model.Models.Model model)
    {
        var stages = Straus7ModelData.Read(model).Stages.ToDictionary(s => s.ModelName ?? "", s => StageLabel(s.Number, s.Name), StringComparer.Ordinal);
        return phase => stages.TryGetValue(phase, out var label) ? label : phase;
    }

    /// <summary>The equilibrium check of GPC (applied loads against the reactions) as data of the copy: kN and kN·m about the global origin.</summary>
    public static ModelEquilibrium Equilibrium(Straus7CaseBalance balance)
    {
        static double[] Kilo(double[]? v, double factor) => v is { Length: 3 } ? v.Select(x => x / factor).ToArray() : [0, 0, 0];
        string note = balance.Balanced switch
        {
            true => "Carichi applicati e reazioni vincolari in equilibrio (GPC Equilibrium.Check, tolleranza relativa " + Straus7Equilibrium.RelativeTolerance.ToString("G2", CultureInfo.InvariantCulture) + ").",
            false => "Carichi applicati e reazioni vincolari NON in equilibrio: la copia GPC non rappresenta tutte le azioni della sorgente" +
                (balance.NotRepresented.Count > 0 ? " (" + string.Join("; ", balance.NotRepresented) + ")" : "") + ". Vedi gli avvisi del convertitore.",
            null => "Equilibrio non valutato: " + string.Join(" ", balance.Diagnostics.Select(d => d.Message))
        };
        return new(Kilo(balance.AppliedForce, NewtonsPerKilonewton), Kilo(balance.AppliedMoment, NewtonMillimetresPerKilonewtonMetre), Kilo(balance.ReactionForce, NewtonsPerKilonewton), Kilo(balance.ReactionMoment, NewtonMillimetresPerKilonewtonMetre), balance.Balanced, note);
    }

    public static GpcSnapshotOptions Options(GPC.Model.Models.Model model, string name, string source, string? datasetId, bool forces, bool displacements,
        IEnumerable<ModelImportMessage> messages, string? sourceRevision = null, Func<string, ModelEquilibrium?>? equilibrium = null)
    {
        var records = model.PreservedSourceData.Where(p => p.Kind == Evidence && p.SourceRecord != null && p.RawData != null)
            .GroupBy(p => p.SourceRecord, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().RawData, StringComparer.Ordinal);
        JsonElement? Record(string key)
        {
            if (!records.TryGetValue(key, out var raw)) return null;
            try { using var document = JsonDocument.Parse(raw); return document.RootElement.Clone(); } catch (JsonException) { return null; }
        }
        double? lengthToMm = Record("St7GetUnits") is { ValueKind: JsonValueKind.Array } units && units.GetArrayLength() > 0 && units[0].TryGetInt32(out int code)
            && code >= 0 && code < LengthToMillimetres.Count ? LengthToMillimetres[code] : null;
        int? Property(string family, Element element) => element.Source?.OriginalId is { } id && Record(family + "/" + id) is { ValueKind: JsonValueKind.Object } e
            && e.TryGetProperty("Property", out var p) && p.TryGetInt32(out int number) && number > 0 ? number : null;
        string Name(string family, int number) => Record("Property/" + family + "/" + number.ToString(CultureInfo.InvariantCulture)) is { ValueKind: JsonValueKind.Object } p
            && p.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(n.GetString()) ? n.GetString()!.Trim()
            : "Proprietà " + number.ToString(CultureInfo.InvariantCulture);
        // Thickness of the plate property when the converter mapped no physical thickness (for example a non-isotropic material).
        double? Thickness(int number) => lengthToMm is double factor && Record("Property/Plate/" + number.ToString(CultureInfo.InvariantCulture)) is { ValueKind: JsonValueKind.Object } p
            && p.TryGetProperty("Geometry", out var g) && g.ValueKind == JsonValueKind.Array && g.GetArrayLength() > 0 && g[0].TryGetDouble(out double t) && t > 0 ? t * factor : null;
        return new GpcSnapshotOptions
        {
            Name = name, Source = source, BeamIdOffset = BeamIdOffset, CaseName = CaseName, DatasetId = datasetId, Forces = forces, Displacements = displacements,
            CombinationSource = CombinationSource, Messages = messages.ToArray(), SourceRevision = sourceRevision, StagePhase = StagePhase(model), Equilibrium = equilibrium,
            BeamProperty = beam => Property("Beam", beam) is int number ? new GpcPropertyIdentity(number, Name("Beam", number)) : null,
            PlateProperty = plate => Property("Plate", plate) is int number ? new GpcPropertyIdentity(number, Name("Plate", number), plate.Assignments.PhysicalThickness ?? Thickness(number)) : null
        };
    }

    /// <summary>GPC diagnostics as import messages; repeated codes are kept, each with its record.</summary>
    public static IEnumerable<ModelImportMessage> Diagnostics(IEnumerable<ModelDiagnostic> diagnostics) =>
        diagnostics.Select(d => new ModelImportMessage(d.Code, d.Severity.ToString(), d.Record ?? "", d.Message ?? ""));

    public static IEnumerable<ModelImportMessage> Conventions(bool results, bool staged = false) =>
    [
        new("STRAUS7-NUMBERING", "info", "Numerazione", $"Nodi e piastre con i numeri API di Straus7; aste (beam, truss, cavi) con {BeamIdOffset:N0} + numero Straus7, perché Straus7 numera beam e plate separatamente. Proprietà con numero e nome di Straus7; casi «numero: nome» come i casi di risultato di Straus7."),
        new("STRAUS7-CONVENTIONS", "info", "Unità e segni", "Convertitore GPC Straus7 R3 (sola lettura): N e mm convertiti in m, kN/m e kN·m/m (piastre), kN e kN·m (aste). Segni come la copia MIDAS: Mxx, Myy, Mxy delle piastre positivi con trazione sulla faccia −z (opposti alla convenzione Straus7 e GPC, trazione su +z); My delle aste opposto a M1 GPC. Spostamenti globali in m, rotazioni in rad. Reazioni vincolari: forza e momento del vincolo (o della molla a terra) sulla struttura, assi globali, kN e kN·m, segno della sorgente come per MIDAS."),
        .. results && !staged ? new ModelImportMessage[] { new("STRAUS7-RESULTS", "info", "Risultati", "Statico lineare, casi primari scelti: piastre ai nodi di ogni elemento senza media fra elementi (valori estrapolati dal solutore), aste alle stazioni restituite da Straus7, spostamenti, rotazioni e reazioni vincolari nodali globali, con il controllo di equilibrio di GPC fra carichi applicati e reazioni. Le combinazioni LSA sono definizioni: i loro risultati (truss e cavi compresi) si costruiscono in ANTHEA con GPC dai casi importati.") } : [],
        .. results && staged ? new ModelImportMessage[] { new("STRAUS7-RESULTS", "info", "Risultati per fase", "Statico non lineare per fasi (NLA): ogni caso di risultato è lo stato alla fine di un incremento di una fase, con tutti i carichi applicati fino a lì; risultati per fase «NLA [CS · fase · incremento]», spostamenti accumulati dall’inizio dell’analisi. Solo gli elementi attivi nella fase hanno risultati. Non si combinano.") } : []
    ];
}
