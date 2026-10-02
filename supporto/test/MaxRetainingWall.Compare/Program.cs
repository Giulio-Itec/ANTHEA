using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Calculations.Geotechnics;
using X.Core;
using Gpc = GPC.Checkers.Geotechnics.Slopes;

// A reference is entered only after it has been read from MAX. No generated MAX values.
if (args.Length != 2) throw new ArgumentException("Indicare il JSON del confronto e la cartella di output.");
string referencePath = Path.GetFullPath(args[0]), root = Path.GetDirectoryName(referencePath)!, output = Path.GetFullPath(args[1]);
var reference = JsonNode.Parse(File.ReadAllText(referencePath))!.AsObject();
string RequiredFile(string key)
{
    string value = reference.S(key);
    if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Riferimento mancante: " + key);
    string path = Path.GetFullPath(value, root);
    if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new ArgumentException("File di riscontro mancante: " + path);
    return path;
}
string modelPath = RequiredFile("model"), maxPath = RequiredFile("max_model"), evidencePath = RequiredFile("max_evidence");
if (string.IsNullOrWhiteSpace(reference.S("max_version"))) throw new ArgumentException("Indicare la versione MAX rilevata.");
var document = Archivio.Leggi(modelPath);
if (document.S("modulo_id") != RetainingWall.Module) throw new ArgumentException("Il modello ANTHEA non è un muro di sostegno.");
var input = document["dati"]!.AsObject();
var global = RetainingWall.CalculateGlobal(input);
var selected = global.Cases.Single(c => c.Factors.Name == reference.S("combination"));
double Number(JsonNode node, string key)
{
    if (J.Number(node[key]) is not double n || !double.IsFinite(n)) throw new ArgumentException("Valore finito richiesto: " + key);
    return n;
}
var source = reference["circle"] ?? throw new ArgumentException("Cerchio MAX mancante.");
double dx = Number(source, "translation_x"), dy = Number(source, "translation_y");
var circle = new SlipCircle(Number(source, "x") + dx, Number(source, "y") + dy, Number(source, "radius"), Number(source, "left") + dx, Number(source, "right") + dx);
if (circle.Radius <= 0 || circle.Right <= circle.Left || Math.Max(Math.Abs(circle.Left - circle.X), Math.Abs(circle.Right - circle.X)) >= circle.Radius) throw new ArgumentException("Cerchio assegnato non valido.");
// The reference endpoints must belong to the actual surface in the ANTHEA model.
foreach (double x in new[] { circle.Left, circle.Right })
    if (Math.Abs(circle.Base(x) - Slope.Height(global.Section.Surface, x)) > .005) throw new ArgumentException("Estremo del cerchio non allineato al profilo (tolleranza 5 mm per letture arrotondate).");
// The same circle is evaluated by GPCChecker.Geotechnics on the section of the calculation (mm, N/mm, MPa, rad).
var library = global.Source!; var libraryFactors = library.Cases.Single(c => c.Factors.Name == selected.Factors.Name).Factors; var libraryCircle = Slope.Circle(circle);
double maxValue = Number(reference, "max_value");
if (maxValue <= 0) throw new ArgumentException("Fattore MAX non positivo.");
string kind = reference.S("max_value_kind");
if (kind is not ("F" or "F/gammaR" or "gammaR/F")) throw new ArgumentException("Specificare se il valore MAX è F, F/gammaR oppure gammaR/F.");
int maxSlices = checked((int)Number(reference, "max_slices"));
if (maxSlices < 5 || maxSlices > 500) throw new ArgumentException("Numero conci di riferimento non valido.");
var evaluations = new List<object>();
var csv = new List<string> { "conci_richiesti;conci_effettivi;F_anthea;F_su_gammaR;eta;valore_MAX;tipo_MAX;scarto_assoluto;scarto_percentuale;stato" };
string N(double v) => v.ToString("G17", CultureInfo.InvariantCulture);
Directory.CreateDirectory(output);
if (reference["native_printed_slices"] is JsonArray nativeRows)
{
    var nativeSlices = nativeRows.Select((row, i) =>
    {
        // Printed values in kN/m, m, degrees and kPa: forces N/mm, width mm, angles rad, pressures MPa for the library.
        double w = Number(row!, "weight"), v = w + Number(row!, "vertical"), a = Number(row!, "alpha") * Math.PI / 180, b = Number(row!, "width") * Slope.Mm;
        return new Gpc.SlopeSlice(i + 1, 0, b, 0, 1, a, "Concio stampato MAX", w, 0, 0, 0, v - w, 0,
            Number(row!, "u") * Slope.KPa, Number(row!, "phi") * Math.PI / 180, Number(row!, "cohesion") * Slope.KPa, v, v * Math.Sin(a));
    }).ToArray();
    var nativeLibrary = Gpc.BishopSolver.Solve(libraryCircle, nativeSlices, selected.Factors.R);
    var nativeCheck = nativeLibrary is null ? null : Slope.Surface(nativeLibrary);
    File.WriteAllText(Path.Combine(output, "equilibrio-conci-max.json"), JsonSerializer.Serialize(new { Source = evidencePath, Note = "Solver ANTHEA applicato ai conci stampati da MAX: geometria esclusa, precisione limitata dagli arrotondamenti della relazione.", MAX = maxValue, ANTHEA = nativeCheck?.Factor, DifferencePercent = (nativeCheck?.Factor / maxValue - 1) * 100, Result = nativeCheck }, J.Options));
    Console.WriteLine($"Equilibrio sui conci MAX stampati: F={nativeCheck?.Factor:G10}; Δ={(nativeCheck?.Factor / maxValue - 1) * 100:G6}%");
}
foreach (int count in new[] { maxSlices, 60, 120, 200 }.Distinct())
{
    var slices = Gpc.SlopeStability.Slices(library.Section, libraryCircle, libraryFactors, count);
    var solved = Gpc.BishopSolver.Solve(libraryCircle, slices, selected.Factors.R);
    var result = solved is null ? null : Slope.Surface(solved);
    double? compared = result is null ? null : kind switch { "F" => result.Factor, "F/gammaR" => result.Factor / selected.Factors.R, _ => result.Ratio };
    double? delta = compared - maxValue, percent = delta / maxValue * 100;
    string status = result is null ? "Equilibrio non ammissibile o non convergente" : "Confronto numerico; interpretare discretizzazione e ipotesi";
    evaluations.Add(new { RequestedSlices = count, ActualSlices = slices.Length, ComparedValue = compared, Difference = delta, DifferencePercent = percent, Status = status, Result = result });
    csv.Add(string.Join(";", count, slices.Length, result is null ? "" : N(result.Factor), result is null ? "" : N(result.Factor / selected.Factors.R), result is null ? "" : N(result.Ratio), N(maxValue), kind, delta is null ? "" : N(delta.Value), percent is null ? "" : N(percent.Value), status));
    Console.WriteLine($"{count} conci richiesti / {slices.Length} effettivi: F={result?.Factor:G10}; MAX {kind}={maxValue:G10}; Δ={percent:G6}%");
}
File.WriteAllText(Path.Combine(output, "stesso-cerchio.json"), JsonSerializer.Serialize(new { Reference = reference, Model = modelPath, MaxModel = maxPath, MaxEvidence = evidencePath, Input = input, global.Section, Factors = selected.Factors, Circle = circle, Evaluations = evaluations, SearchMinimum = selected.Critical, SearchStatus = selected.Status }, J.Options));
File.WriteAllLines(Path.Combine(output, "stesso-cerchio.csv"), csv);
