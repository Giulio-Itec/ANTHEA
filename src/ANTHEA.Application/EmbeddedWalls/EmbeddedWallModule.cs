using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Reports;
using GPC.Checkers.Geotechnics.EmbeddedWalls;
using GPC.Design.EmbeddedWalls;
using GPC.Model.Checking.Contracts;
using GPC.Model.Geotechnics;
using static Anthea.Application.EmbeddedWalls.EmbeddedWallAdapter;

namespace Anthea.Application.EmbeddedWalls;

public sealed record EmbeddedWallProgress(string Stage, double? Fraction, int Iterations);

public sealed class EmbeddedWallModule : IModuleService
{
    public event Action<EmbeddedWallProgress>? ProgressChanged;
    public string ModuleId => EmbeddedWallDocument.Module;
    public JsonObject Prepare(JsonObject source)
    { var copy = EmbeddedWallDocument.Snapshot(source); copy.Remove("result_snapshot"); return copy; }
    public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default) => Calculate(source, null, cancellation);
    public ModuleCalculation Calculate(JsonObject source, IProgress<WallAnalysisProgress>? progress, CancellationToken cancellation = default)
    {
        var data = Prepare(source); var model = EmbeddedWallAdapter.ToModel(data);
        string basis = EmbeddedWallAdapter.Text(data, "action_basis"), kindText = EmbeddedWallAdapter.Text(data, "check_kind");
        EmbeddedWallSectionCheckKind? kind = kindText == "Analisi" ? null : Enum.TryParse<EmbeddedWallSectionCheckKind>(kindText, out var choice) && Enum.IsDefined(choice)
            ? choice : throw new ArgumentException("Tipo di verifica delle sezioni non supportato.");
        if (kind.HasValue && (string.IsNullOrWhiteSpace(basis) || basis == "Da definire"))
            throw new ArgumentException("Dichiarare combinazione e provenienza delle azioni: nessun coefficiente viene applicato implicitamente.");
        var rc = kind.HasValue ? EmbeddedWallAdapter.ConcreteSections(data) : [];
        if (kind.HasValue && rc.Count == 0) throw new ArgumentException("Definire le sezioni in c.a. da verificare.");
        foreach (var section in rc) section.Validate();
        var hydraulicAnalysis = EmbeddedWallHydraulics.Calculate(data, model, cancellation, p => ProgressChanged?.Invoke(p));
        var hydraulics = hydraulicAnalysis.Reports;
        var hydraulicChecks = EmbeddedWallHydraulicChecks.Calculate(data, model, hydraulicAnalysis.Fields, cancellation);
        var globalChecks = EmbeddedWallGlobalChecks.Calculate(data, model, hydraulicAnalysis.Fields, cancellation, p => ProgressChanged?.Invoke(p));
        var hydraulicFailure = hydraulics.FirstOrDefault(h => !h.Complete);
        if (hydraulicFailure is not null)
        {
            var failed = new EmbeddedWallReportData(model.Name, EmbeddedWallDocument.Fingerprint(data), model.CaseId, basis,
                "HydraulicNonConvergent", false, hydraulicFailure.Diagnostic, data.ToJsonString(new() { WriteIndented = true }), [], [],
                ["Filtrazione non convergente nella fase " + hydraulicFailure.Stage + ": analisi meccanica non eseguita. I campi delle sole fasi idrauliche convergenti restano consultabili. Le verifiche dipendenti da campi assenti sono non valutate."], hydraulics, hydraulicChecks, globalChecks);
            cancellation.ThrowIfCancellationRequested();
            return new ModuleCalculation(ModuleId, data, EmbeddedWallDocument.ResultValues(failed), failed);
        }
        var analysis = EmbeddedWallAnalysis.Calculate(model, EmbeddedWallAdapter.Options(data), cancellation,
            new ProgressRelay(p => { progress?.Report(p); ProgressChanged?.Invoke(new(p.StageId, p.Fraction, p.Iterations)); }));
        var checks = analysis.IsComplete && kind.HasValue ? EmbeddedWallConcrete.Check(analysis, model.TributaryWidth, rc, kind.Value, cancellation) : [];
        var steel = EmbeddedWallSteelChecks.Calculate(data, analysis, cancellation);
        var anchors = EmbeddedWallAnchorChecks.Calculate(data, analysis, cancellation);
        var tubes = EmbeddedWallTubeChecks.Calculate(data, analysis, model.TributaryWidth, cancellation);
        var rcPiles = EmbeddedWallRcPileChecks.Calculate(data, analysis, model.TributaryWidth, cancellation);
        var scope = new List<string>
        {
            "Modello piano di trave su molle elastoplastiche indipendenti; piccoli spostamenti, strati orizzontali e parete verticale liscia. Pressioni Rankine, carico uniforme sul terreno.",
            "Le fasi interpolano le pressioni fra configurazioni successive e conservano la memoria plastica. I sostegni sono installati sulla deformata precedente; le attivazioni elencano tutti i sostegni presenti in ciascuna fase.",
            "Carichi distribuiti sulla struttura: andamento lineare per intervalli, positivo verso destra, in kN/m verticale sull'intera fascia. Gli intervalli sovrapposti e il carico uniforme si sommano. Ogni fase definisce l'intero campo presente; un intervallo non elencato viene rimosso nella transizione. Il diagramma q netto comprende anche le reazioni del terreno.",
            "N positivo a compressione; V e M riferiti alla sezione della fascia analizzata. I sostegni riportano la forza per singolo elemento. Quote positive verso l'alto.",
            "La convergenza numerica non costituisce una verifica completa dell'opera. Instabilità geotecnica del fondo scavo e sisma restano da verificare; i controlli locali disponibili sono elencati separatamente.",
            Text(data, "rc_pile_standard") == "Non richieste" ? "Verifiche dei pali circolari in c.a. non richieste."
                : "Pali circolari: Checker Concrete, NTC 2018 statiche; resistenza nominale N–M–V e SLE, con azioni concomitanti per singolo palo, N positivo a trazione. Fila unica e ripartizione uniforme esplicite. EI lordo del cerchio; integrazione della sezione sul poligono dichiarato. Dettagli costruttivi separati; ancoraggi, giunti, instabilità e sisma non certificati.",
            Text(data, "tube_standard") == "Non richieste" ? "Verifiche delle sezioni tubolari non richieste."
                : "Sezioni tubolari: Checker Steel, NTC 2018 SLU statici, CHS integre di classe 1–2. Le azioni N–M–V concomitanti sono riferite al singolo tubo, N positivo a trazione; EI lordo equivalente da GPC quando selezionato. Fila unica e ripartizione uniforme esplicite. Il taglio elevato riduce cautelativamente tutta la sezione. Instabilità della membratura, guscio, collegamenti e altre famiglie richiedono verifiche distinte.",
            Text(data, "steel_standard") == "Non richieste" ? "Verifiche dei sostegni metallici non richieste."
                : "Sostegni metallici: verifiche Steel per aste circolari piene o CHS soggette alla sola azione assiale centrata, NTC 2018 SLU statici. N positivo a trazione, per singolo sostegno. Area netta e lunghezze libere d'inflessione esplicite; classe 4, profili non disponibili e dati mancanti producono Non valutato. La rigidezza assiale del modello resta quella assegnata. Collegamenti, ancoraggi, bulbi, durabilità e verifica dell'intero sistema sono distinti.",
            Text(data, "anchor_standard") == "Non richieste" ? "Sfilamento dei tiranti non richiesto."
                : "Sfilamento: capacità del singolo bulbo da indagini pressiometriche complete o prove preliminari, NTC 2018 statiche A1+M1+R3. Pressione limite del terreno distinta dalla pressione d'iniezione. La predizione empirica richiede conferma in sito; campo e limiti sono indicati negli esiti.",
            globalChecks.Count == 0 ? "Stabilità globale non richiesta." : "Stabilità globale: Bishop circolare del motore GPC esistente, parete continua e terreni orizzontali. Dati caratteristici confermati, combinazione statica distinta da quelle locali, selezione esplicita delle azioni. I sostegni esterni non modellati producono Non valutato; le reazioni locali non sono azioni esterne. Le ricerche non conclusive sono distinte dalle verifiche soddisfatte.",
            hydraulics.Count == 0 ? "Pressioni interstiziali idrostatiche dai livelli di falda assegnati."
                : "Filtrazione stazionaria FEM 2D per ciascuna fase, terreno saturo/non saturo, parete impermeabile di spessore finito. Condizioni laterali a carico imposto, fondo impermeabile e superficie drenante. Le pressioni convergenti sulle due facce alimentano il modello meccanico; nessuna analisi transitoria o verifica HYD/UPL è implicita.",
            Text(data, "hydraulic_weight_mode") == "Pressioni FEM" ? "Pesi del terreno: γsat dove la pressione FEM è positiva, γ naturale altrove; nessun peso interpolato con il grado di saturazione né frangia capillare."
                : "Pesi del terreno distinti dalla quota di falda assegnata, anche quando le pressioni provengono dal FEM.",
            hydraulicChecks.Count == 0 ? "Verifiche idrauliche UPL/HYD non richieste." : "UPL/HYD limitate ai corpi, punti e colonne esplicitamente assegnati. Azioni caratteristiche e coefficienti del profilo normativo dichiarato; Rd aggiuntiva già di progetto. Questi controlli non verificano erosione interna o instabilità non drenata del fondo. Le azioni variabili della combinazione devono essere definite dall'utente.",
            kind.HasValue ? "Sezioni in c.a.: verifiche NTC 2018 su azioni della combinazione dichiarata; rettangolo senza staffe. Nessun coefficiente parziale aggiunto dal servizio. Progetto delle armature e dettagli non eseguiti."
                : "Verifiche di resistenza ed esercizio delle fasce rettangolari in c.a. non richieste."
        };
        var snapshot = new EmbeddedWallReportData(model.Name, EmbeddedWallDocument.Fingerprint(data), model.CaseId, basis,
            analysis.Status.ToString(), analysis.IsComplete, analysis.Diagnostic, data.ToJsonString(new() { WriteIndented = true }),
            Array.AsReadOnly(analysis.Stages.Select(s => new EmbeddedWallStageData(s.Id, s.AcceptedIncrements, s.Iterations, s.ForceResidual, s.MomentResidual,
                Array.AsReadOnly(s.Points.Select(p => new EmbeddedWallPointData(p.Elevation / SoilUnits.Metre, p.Side, p.SectionId, p.Displacement, p.Rotation,
                    p.NormalForce / NewtonsPerKilonewton, p.Shear / NewtonsPerKilonewton, p.Moment / NmmPerKilonewtonMetre, p.LeftPressure / SoilUnits.KiloPascal, p.RightPressure / SoilUnits.KiloPascal, p.LeftPlastic, p.RightPlastic, p.NetDistributedLoad)).ToArray()),
                Array.AsReadOnly(s.Supports.Select(t => new EmbeddedWallSupportData(t.Id, t.AxialForce / NewtonsPerKilonewton, t.HorizontalReaction / NewtonsPerKilonewton, t.ReferenceDisplacement, t.AtCapacity)).ToArray()))).ToArray()),
            Array.AsReadOnly(checks.SelectMany(c => c.Checks.Count == 0 ? new[] { new EmbeddedWallCheckData(c.CaseId, c.StageId, c.SectionId, c.Elevation / SoilUnits.Metre, c.Side,
                -c.Actions.N / NewtonsPerKilonewton, c.Actions.V / NewtonsPerKilonewton, -c.Actions.M / NmmPerKilonewtonMetre, "Sezione non definita", "", 0, null, null, EngineeringOutcome.NotEvaluated, c.Diagnostic) }
                : c.Checks.Select(v => new EmbeddedWallCheckData(c.CaseId, c.StageId, c.SectionId, c.Elevation / SoilUnits.Metre, c.Side,
                    -c.Actions.N / NewtonsPerKilonewton, c.Actions.V / NewtonsPerKilonewton, -c.Actions.M / NmmPerKilonewtonMetre, v.Method, v.Unit, v.Demand, v.Capacity, v.Ratio, v.Outcome, v.Diagnostic ?? c.Diagnostic))).ToArray()),
            scope.AsReadOnly(), hydraulics, hydraulicChecks, globalChecks, steel, anchors, tubes, rcPiles);
        cancellation.ThrowIfCancellationRequested();
        return new ModuleCalculation(ModuleId, data, EmbeddedWallDocument.ResultValues(snapshot), snapshot);
    }
    private sealed class ProgressRelay(Action<WallAnalysisProgress> report) : IProgress<WallAnalysisProgress>
    { public void Report(WallAnalysisProgress value) => report(value); }
}
