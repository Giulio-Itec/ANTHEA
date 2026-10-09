using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Testing.Normalization;
using X.Core;

namespace Anthea.Testing.Capture;

/// <summary>
/// Per module: the headless reports written by X.Core (figures null) and the engines that CalculationService does not run.
/// Report options are those proposed by the export dialog of the application (MainWindow.ExportReport).
/// </summary>
public sealed partial class CaptureRunner
{
    /// <summary>What B0 does not cover, declared in the manifest.</summary>
    public static readonly (string Item, string Reason)[] NotCovered =
    [
        ("Figure dei report (PNG, grafici, disegni)", "richiedono WPF: i report headless sono catturati con figure nulle; i grafici sono coperti solo dalle prove WPF"),
        ("Report di progetto (ReportProject) e scheda materiali", "esistono solo nel percorso WPF (X.Desktop/Wpf/ProjectReport.cs): coperti dalle uscite normalizzate delle prove --smoke-project-report e --smoke-material-report"),
        ("Moduli mat_calcestruzzo e mat_acciaio_armatura: report", "nessun writer headless in X.Core"),
        ("Default e migrazioni applicati dalle viste WPF", "i default headless sono quelli di ModuleCatalog.CreateData; le differenze progetto/foglio sono il fallimento noto di --project-calculations (F0.5)"),
        ("ConcreteReinforcementDesign.Optimize (progetto delle armature della sezione c.a.)", "ricerca con parallelismo e limiti di tempo: coperta da ConcreteDesign.Checks; da aggiungere con opzioni deterministiche (MaxParallelism = 1)"),
        ("BridgeConcept.Optimize con obiettivi diversi dal costo e con vincoli", "catturata la sola ottimizzazione con le opzioni predefinite"),
        ("BridgeSection: report e curve con metodi storici non lineari oltre ai default", "catturati i default di ResponseDefaults (momento–curvatura, forza–deformazione, dopo la fase 0)"),
        ("Stabilità globale dei muri con sisma e matrici personalizzate", "catturate la proposta automatica del profilo e il benchmark di GlobalStability.Checks"),
        ("Griglie dense di CheckerMigration.Capture (c.a., muri, pali)", "strumento separato (supporto/test/CheckerMigration.Capture): passo F0.4b"),
        ("Uscite delle prove WPF (--smoke*, --check-*)", "normalizzabili con 'normalize': passo F0.4b, profilo full"),
        ("Impronta delle mesh DelaunayMesh delle sezioni usuali", "passo F0.4b"),
        ("Esportazioni Excel delle sollecitazioni (SectionActionsExcel)", "non catturate"),
        ("Micropalo orizzontale (geo_micropalo_orizzontale): nessun caso numerico", "il default è un modulo vuoto e supporto/esempi non contiene micropali orizzontali: serve un esempio CHS"),
        ("Pali e micropali verticali: solo i casi della regressione", "i default sono moduli vuoti; coperti dai 94 casi palo e micropalo di casi_confronto.json")
    ];

    static readonly HashSet<string> WordDefaults = ReportWord.Sezioni.Where(s => !s.Key.StartsWith("grafico")).Select(s => s.Key).ToHashSet();
    static readonly HashSet<string> ConcreteDefaults = ReportConcrete.Sections.Where(s => s.Key is not ("dettagli" or "sle_tutte")).Select(s => s.Key).ToHashSet();
    static readonly HashSet<string> ConcreteComplete = ReportConcrete.Sections.Select(s => s.Key).ToHashSet();

    void Extras(CaptureCase item, GuidMap guids, JsonObject? result)
    {
        string m = item.Module, id = item.Id, title = "Baseline · " + m + " · " + id; var data = item.Data!;
        string report = $"reports/{m}/{id}";
        switch (m)
        {
            case "geo_palo_verticale" or "geo_micropalo_verticale":
                if (result is not null) Docx(report + ".txt", guids, () => ReportWord.Create(title, m, Clone(data), Clone(result), WordDefaults, null, true));
                break;
            case HorizontalPileGroup.Module:
                if (result is not null) Docx(report + ".txt", guids, () => ReportPileGroup.Create(title, Clone(result)));
                break;
            case PaloOrizzontale.Module or MicropaloOrizzontale.Module:
                if (result is not null)
                {
                    bool elastic = Str(result, "tipo_risultato") == "palo_elastico";
                    Docx(report + ".txt", guids, () => elastic ? ReportElasticPile.Create(title, Clone(result)) : ReportOrizzontale.Create(title, Clone(result), true, null));
                    if (elastic) Text(report + ".elastico.csv", guids, () => ElasticHorizontalPile.Csv(Clone(result)));
                }
                if (Str(data, "vista_orizzontale") == "elastico")
                {
                    // The two halves of CalculateShared, as the workspace runs them: FEM response, then sections and reinforcement of the segments.
                    var response = Json($"engines/palo_elastico/{m}.{id}.risposta.json", guids, () => ElasticHorizontalPile.CalculateResponse(Clone(data)));
                    if (response is not null)
                        Json($"engines/palo_elastico/{m}.{id}.armature.json", guids, () => ElasticHorizontalPile.CompleteReinforcement(Clone(data), Clone(response))["armature"] ?? throw new InvalidOperationException("Risultato senza armature."));
                }
                break;
            case RetainingWall.Module:
                Walls(item, guids, title, report);
                break;
            case "str_palo":
                if (result is not null)
                {
                    var prepared = result["dati"] as JsonObject ?? data;
                    Docx(report + ".txt", guids, () => ReportConcrete.Create(title, Clone(prepared), Clone(result), ConcreteDefaults, null, true));
                    Docx(report + ".completo.txt", guids, () => ReportConcrete.Create(title, Clone(prepared), Clone(result), ConcreteComplete, null, true));
                    Docx(report + ".sintetico.txt", guids, () => ReportConcreteShort.Create(title, Clone(prepared), Clone(result)));
                    // Durability part of the detailing (refactoring F2.9, baseline B6): ConcreteDetailingAnalysis is not run by CalculationService.
                    Json($"engines/dettagli_durabilita/{id}.json", guids, () => DetailingDurability(Clone(prepared)));
                }
                break;
            case BridgeSection.Module:
                {
                    var bridge = Once("reports/" + m + "/" + id, () => BridgeSection.Calculate(Clone(data)));
                    Docx(report + ".txt", guids, () => ReportBridge.Create(title, bridge.Value, ReportBridge.DefaultSections()));
                    foreach (var (name, mode, origin) in new[] { ("mc", 0, 0), ("fd", 1, 0), ("mc-storico", 0, 1) })
                        Text($"engines/ponte_risposta/{id}.{name}.csv", guids, () =>
                        {
                            var request = BridgeSection.ResponseDefaults();
                            request["tipo"] = BridgeSection.ResponseModes[mode]; request["origine"] = BridgeSection.ResponseOrigins[origin];
                            return BridgeSection.ResponseCsv(BridgeSection.CalculateResponse(Clone(data), request));
                        });
                }
                break;
            case BridgeConcept.Module:
                {
                    var concept = Once("reports/" + m + "/" + id, () => BridgeConcept.Calculate(Clone(data)));
                    Docx(report + ".txt", guids, () => BridgeConceptExport.Report(title, Clone(data), concept.Value));
                    Text(report + ".quantita.csv", guids, () => BridgeConceptExport.Csv(concept.Value));
                    Text(report + ".tecnica.csv", guids, () => BridgeConceptExport.TechnicalCsv(Clone(data), concept.Value));
                    Json($"engines/bridge_design_ottimizzazione/{id}.json", guids, () => BridgeConcept.Optimize(Clone(data), new BridgeConcept.OptimizationOptions()));
                }
                break;
        }
    }

    void Walls(CaptureCase item, GuidMap guids, string title, string report)
    {
        var data = item.Data!; string id = item.Id;
        var wall = Once(report, () => RetainingWall.Calculate(Clone(data), default, options.ServiceabilityEngine));
        Docx(report + ".txt", guids, () => ReportRetainingWall.Create(title, wall.Value));
        Text(report + ".csv", guids, () => ReportRetainingWall.Csv(wall.Value));

        // Global stability: the profile proposed by the application, then the benchmark set-up of GlobalStability.Checks.
        foreach (var (variant, prepare) in new (string, Action<JsonObject>)[] { ("proposta", Proposed), ("benchmark", Benchmark) })
        {
            var input = Once($"engines/muri_stabilita_globale/{id}.{variant}", () => { var d = Clone(data); prepare(d); return d; });
            var slope = Once($"engines/muri_stabilita_globale/{id}.{variant}", () => RetainingWall.CalculateGlobal(Clone(input.Value)));
            Json($"engines/muri_stabilita_globale/{id}.{variant}.json", guids, () => new JsonObject
            {
                ["stabilita_globale"] = input.Value["global_stability"]?.DeepClone(),
                ["risultato"] = CanonicalJson.FromObject(slope.Value)
            });
            Docx($"{report}.globale-{variant}.txt", guids, () => ReportRetainingWall.CreateGlobal(Clone(input.Value), slope.Value));
            Text($"{report}.globale-{variant}.csv", guids, () => ReportRetainingWall.GlobalCsv(slope.Value));
        }

        if (Str(data, "family") != "cantilever") return;
        Json($"engines/muri_progetto_armature/{id}.json", guids, () => RetainingWall.DesignReinforcement(Clone(data), default, options.ServiceabilityEngine));
        var schedule = Once($"engines/muri_distinta/{id}", () => RetainingWall.CalculateBarSchedule(wall.Value));
        Json($"engines/muri_distinta/{id}.json", guids, () => schedule.Value);
        Docx($"{report}.distinta.txt", guids, () => ReportRetainingWall.CreateBarSchedule(Clone(wall.Value.Input), schedule.Value));
        Text($"{report}.distinta.csv", guids, () => ReportRetainingWall.BarScheduleCsv(schedule.Value));
    }

    /// <summary>
    /// Durability part of ConcreteDetailingAnalysis.Calculate on the prepared sheet (refactoring F2.9, baseline B6), with the SLU and SLV
    /// actions as the detailing tab passes them: an explicit projection of Durability and DurabilityError with named fields, not the DTO.
    /// </summary>
    static JsonObject DetailingDurability(JsonObject prepared)
    {
        var settings = prepared["workspace_ca"]!.AsObject();
        var actions = new[] { "SLU", "SLV" }.SelectMany(set => prepared["combinazioni"]?[set] as JsonArray ?? [])
            .OfType<JsonObject>().Select(row => J.Obj(("N", row["azioni"]![0]))).ToArray();
        var detailing = ConcreteDetailingAnalysis.Calculate(prepared["input"]!.AsObject(), settings, actions);
        var d = detailing.Durability;
        return new JsonObject
        {
            ["esposizione"] = settings["sle_comuni"]?["esposizione"]?.DeepClone(),
            ["errore_durabilita"] = detailing.DurabilityError,
            ["ambiente"] = d?.Environment, ["gruppo"] = d?.Severity, ["cmin_pertinente"] = d?.Cmin, ["c0"] = d?.C0,
            ["tabella"] = d?.TableCover, ["vita"] = d?.LifeExtra, ["classe_inferiore"] = d?.LowStrengthExtra, ["riduzione_qualita"] = d?.QualityReduction,
            ["cmin_b"] = d?.Cover.Bond, ["cmin_dur"] = d?.Cover.Durability, ["cmin"] = d?.Cover.Minimum, ["cnom"] = d?.Cover.Nominal
        };
    }

    /// <summary>The proposal of the global stability panel (PrepareGlobalProfile), confirmed as it is.</summary>
    static void Proposed(JsonObject d)
    {
        RetainingWall.PrepareGlobalProfile(d);
        var g = d["global_stability"]!; g["enabled"] = true; g["profile_confirmed"] = true;
    }

    /// <summary>The model of GlobalStability.Checks (Program.cs, Model): deep single column, assigned search.</summary>
    static void Benchmark(JsonObject d)
    {
        d["layers"]![0]!["thickness"] = 25;
        RetainingWall.PrepareGlobalProfile(d); d["global_stability"]!["soil_mode"] = "Profilo unico";
        var g = d["global_stability"]!; g["enabled"] = true; g["profile_confirmed"] = true;
        g["grid"] = 9; g["slices"] = 60; g["refinements"] = 4; g["depth_min"] = .1; g["depth_max"] = 10; g["search_mode"] = "Assegnata";
    }
}
