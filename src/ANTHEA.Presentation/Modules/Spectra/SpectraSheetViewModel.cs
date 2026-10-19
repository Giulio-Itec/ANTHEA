using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Actions;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using Anthea.Reports;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.Spectra;

public sealed record SpectraCurve(string Name, IReadOnlyList<double[]> Points, bool Dashed = false);
public sealed record SpectraMarker(double X, double Y, string Label);
public sealed record BearingChoice(string Name, double Displacement, double K)
{ public string Label => $"{Name} · ±{Displacement:g} mm · {K:g} kN/mm"; }

public sealed class SpectraSheetViewModel : SheetViewModelBase
{
    private string search = "", region = "Tutte", province = "Tutte"; private SpectraTown? town; private int stage = 2, state = 2, period = 6;
    private bool building;
    public SpectraSheetViewModel(SheetContext context) : base(SpectraDocument.Module, context, TimeSpan.FromMilliseconds(450))
    {
        Report = new ReportSource(this);
        ExportCommand = GuardedCommands.Create(Guard, Export, () => HasResults);
        CopyCommand = GuardedCommands.Create(Guard, () => Context.Services.Clipboard.SetText(SpectraDocument.Csv(Result!)), () => HasResults);
        ImportCommand = GuardedCommands.Create(Guard, Import, () => CanEdit);
        ApplyPendulumCommand = GuardedCommands.Create(Guard, ApplyPendulum, () => CanEdit && Result?["pendulum"] is JsonObject);
        BuildForms();
    }
    public override IModuleReportSource Report { get; }
    public IRelayCommand ExportCommand { get; }
    public IRelayCommand CopyCommand { get; }
    public IRelayCommand ImportCommand { get; }
    public IRelayCommand ApplyPendulumCommand { get; }
    public FormViewModel SiteForm { get; private set; } = null!;
    public FormViewModel LifeForm { get; private set; } = null!;
    public FormViewModel DesignForm { get; private set; } = null!;
    public FormViewModel IsolationForm { get; private set; } = null!;
    public FormViewModel PendulumForm { get; private set; } = null!;
    public FormViewModel AssignedForm { get; private set; } = null!;
    private IEnumerable<FormViewModel> Forms => new[] { SiteForm, LifeForm, DesignForm, IsolationForm, PendulumForm, AssignedForm }.Where(f => f is not null);
    public string Search { get => search; set { if (SetProperty(ref search, value)) OnPropertyChanged(nameof(Towns)); } }
    public IReadOnlyList<string> Regions => new[] { "Tutte" }.Concat(SpectraDocument.Towns.Select(t => t.Region).Distinct().Order()).ToArray();
    public IReadOnlyList<string> Provinces => new[] { "Tutte" }.Concat(SpectraDocument.Towns.Where(t => region == "Tutte" || t.Region == region).Select(t => t.Province).Distinct().Order()).ToArray();
    public string Region { get => region; set { if (SetProperty(ref region, value)) { Province = "Tutte"; OnPropertyChanged(nameof(Provinces)); OnPropertyChanged(nameof(Towns)); } } }
    public string Province { get => province; set { if (SetProperty(ref province, value)) OnPropertyChanged(nameof(Towns)); } }
    public IReadOnlyList<SpectraTown> Towns => SpectraDocument.Towns.Where(t => (region == "Tutte" || t.Region == region) && (province == "Tutte" || t.Province == province) && (search.Length == 0 || t.Label.Contains(search, StringComparison.CurrentCultureIgnoreCase))).OrderBy(t => t.Name).ToArray();
    public SpectraTown? Town
    {
        get => town; set
        {
            if (value is null || !CanEdit || value == town) return;
            town = value; Edit(d => { d["longitude"] = value.Longitude; d["latitude"] = value.Latitude; d["location"] = value.Label; }); BuildForms(); OnPropertyChanged();
        }
    }
    public IReadOnlyList<BearingChoice> Catalog => Data["catalog"]?.AsArray().OfType<JsonObject>().Select(c => new BearingChoice(SpectraDocument.Text(c, "name"), SpectraDocument.Number(c, "displacement"), SpectraDocument.Number(c, "k"))).ToArray() ?? [];
    public BearingChoice? Bearing1 { get => null; set => ChooseBearing(value, 1); }
    public BearingChoice? Bearing2 { get => null; set => ChooseBearing(value, 2); }
    private void ChooseBearing(BearingChoice? value, int group) { if (value is null || !CanEdit) return; Edit(d => { d[$"group{group}_name"] = value.Name; d[$"group{group}_k"] = value.K; }); BuildForms(); }
    public IReadOnlyList<string> Stages { get; } = ["1 · Pericolosità del sito", "2 · Periodo di riferimento", "3 · Spettro di progetto", "4 · Isolamento sismico"];
    public IReadOnlyList<string> States { get; } = ["SLO", "SLD", "SLV", "SLC"];
    public IReadOnlyList<string> Periods { get; } = ["30 anni", "50 anni", "72 anni", "101 anni", "140 anni", "201 anni", "475 anni", "975 anni", "2475 anni"];
    public int Stage { get => stage; set { if (value >= 0 && value < 4 && SetProperty(ref stage, value)) RefreshResults(); } }
    public int State { get => state; set { if (value >= 0 && value < 4 && SetProperty(ref state, value)) RefreshResults(); } }
    public int Period { get => period; set { if (value >= 0 && value < 9 && SetProperty(ref period, value)) RefreshResults(); } }
    public string Location => SpectraDocument.Text(Data, "location");
    public string Reference => Result?["reference"]?.ToString() ?? "Spettri NTC · compatibilità CSLP 1.0.3";
    public string Source => Result?["hazard_source"]?.ToString() ?? "Premere Calcola per determinare gli spettri.";
    public string ImportSource => Data["import_source"] is JsonObject s ? $"Importato: {s["file"]} · valori salvati nel file" : "";
    public bool ShowGrid => Stage == 0;
    public bool ShowState => Stage >= 2;
    public string PlotTitle => Stages[Stage] + (Stage >= 2 ? " · " + States[State] : "");
    public string Note => Stage == 3 ? "NTC 2018 §7.10.5.3.2: smorzamento dell’isolamento da T ≥ 0,8 Tis. Campo Tis ≤ 4 s (§3.2.3.2). Forza e spostamento valutati a Tis. Il campione Excel è mostrato solo per confronto. Restano da verificare regolarità, eccentricità, azioni verticali, dispositivi e condizioni di applicabilità dell’analisi." :
        "Coordinate in gradi decimali; catalogo comunale e reticolo dell’edizione CSLP 1.0.3. Accelerazioni in g; tempi in secondi. La modalità compatibile conserva le convenzioni numeriche del riferimento NTC 2008.";
    private JsonObject? SelectedState => Result?["states"]?.AsArray().ElementAtOrDefault(State)?.AsObject();
    private JsonObject? SelectedIsolation => Result?["isolation"]?.AsArray().ElementAtOrDefault(State)?.AsObject();
    public IReadOnlyList<SpectraCurve> Curves
    {
        get
        {
            if (Result is null) return [];
            SpectraCurve Curve(string name, JsonNode? array, bool dashed = false) => new(name, array?.AsArray().OfType<JsonObject>().Select(p => new[] { SpectraDocument.Number(p, "PeriodSeconds"), SpectraDocument.Number(p, "AccelerationG") }).ToArray() ?? [], dashed);
            if (Stage == 0) { var periods = Result["site"]!["Periods"]!.AsArray(); return periods.OfType<JsonObject>().Select(p => Curve(p["ReturnYears"] + " anni", p["ReferenceSpectrum"])).Append(Curve("Tabulato · " + Periods[Period], periods[Period]!["TabulatedSpectrum"], true)).ToArray(); }
            if (Stage == 1) return Result["states"]!.AsArray().OfType<JsonObject>().Select(s => Curve(s["State"]!.ToString(), s["Reference"])).ToArray();
            if (Stage == 2) return [Curve("Orizzontale", SelectedState?["Horizontal"]), Curve("Verticale", SelectedState?["Vertical"]), Curve("Riferimento A", SelectedState?["Reference"], true)];
            return SelectedIsolation is null ? [] : [Curve("Isolamento", SelectedIsolation["Spectrum"]), Curve("Riferimento A", SelectedState?["Reference"], true)];
        }
    }
    public IReadOnlyList<SpectraMarker> Nodes => Result?["site"]?["Nodes"]?.AsArray().OfType<JsonObject>().Select(n => new SpectraMarker(SpectraDocument.Number(n, "XKm"), SpectraDocument.Number(n, "YKm"), n["Id"]!.ToString())).ToArray() ?? [];
    public DataView Summary
    {
        get
        {
            var table = new DataTable();
            void Headers(params string[] names) { foreach (string s in names) table.Columns.Add("C" + table.Columns.Count).Caption = s; }
            string V(JsonNode? obj, string key) => Format(obj?[key]);
            if (Stage == 0) { Headers("TR [anni]", "ag [g]", "F0", "Tc* [s]"); foreach (var p in Result?["site"]?["Periods"]?.AsArray() ?? []) table.Rows.Add(V(p, "ReturnYears"), V(p?["Parameters"], "AgG"), V(p?["Parameters"], "F0"), V(p?["Parameters"], "TcStarSeconds")); }
            else if (Stage < 3) { Headers("SL", "VR [anni]", "TR [anni]", "ag [g]", "F0", "Tc* [s]", "SS", "CC", "ST", "η", "q", "TB [s]", "TC [s]", "TD [s]", "Fv"); foreach (var s in Result?["states"]?.AsArray() ?? []) table.Rows.Add(V(s, "State"), V(s, "ReferenceYears"), V(s, "ReturnYears"), V(s?["Parameters"], "AgG"), V(s?["Parameters"], "F0"), V(s?["Parameters"], "TcStarSeconds"), V(s, "Ss"), V(s, "Cc"), V(s, "St"), V(s, "Eta"), V(s, "Q"), V(s, "Tb"), V(s, "Tc"), V(s, "Td"), V(s, "VerticalF")); }
            else { Headers("SL", "K [kN/mm]", "Tis [s]", "0,8 Tis [s]", "Sa(Tis) [g]", "F [kN]", "d [mm]", "Sa campione Excel [g]"); foreach (var s in Result?["isolation"]?.AsArray() ?? []) table.Rows.Add(V(s, "State"), V(s, "StiffnessKnPerMm"), V(s, "PeriodSeconds"), V(s, "ThresholdSeconds"), V(s, "AccelerationG"), V(s, "ForceKn"), V(s, "DisplacementMm"), V(s, "ExampleSampleAccelerationG")); }
            return table.DefaultView;
        }
    }
    public DataView Ordinates
    {
        get
        {
            var table = new DataTable(); table.Columns.Add("C0").Caption = "Serie"; table.Columns.Add("C1").Caption = "T [s]"; table.Columns.Add("C2").Caption = "Sa [g]";
            foreach (var c in Curves) foreach (var p in c.Points) table.Rows.Add(c.Name, p[0].ToString("G12", CultureInfo.CurrentCulture), p[1].ToString("G12", CultureInfo.CurrentCulture));
            return table.DefaultView;
        }
    }
    public string IsolationDetails => Stage != 3 ? "" : SelectedIsolation?["BearingForces"] is JsonArray groups ? string.Join("\n", groups.Select(g => $"{g!["Name"]}: n = {g["Count"]}; forza per isolatore = {Format(g["ForcePerBearingKn"])} kN")) : "Attivare l’isolamento e calcolare per vedere i risultati.";
    public string PendulumDetails => Result?["pendulum"] is JsonObject p ? $"Pendolo: Fμ = {Format(p["FrictionForceKn"])} kN; Ke = {Format(p["EffectiveStiffnessKnPerMm"])} kN/mm; ξ = {Format(p["DampingPercent"])}%; T = {Format(p["PeriodSeconds"])} s." : "";
    private static string Format(JsonNode? n) => n?.ToString() == "NaN" ? "Non disponibile" : n is JsonValue v && v.TryGetValue<double>(out var d) ? d.ToString("G10", CultureInfo.CurrentCulture) : n?.ToString() ?? "";
    private static FieldSpec N(string key, string label, string unit = "") => new(key, label, unit, Write: FieldWriteRule.NumberOrText, ToolTip: label);
    private static FieldSpec C(string key, string label, params string[] choices) => new(key, label, Choices: choices, ToolTip: label, Wide: choices.Any(c => c.Length > 10));
    private static FieldSpec B(string key, string label) => new(key, label, Bool: true, ToolTip: label);
    private FormViewModel Form(params FieldSpec[] fields) => new(Data, fields, Edited, compact: true, wikiModule: SpectraDocument.Module) { IsReadOnly = !CanEdit, WikiNavigator = Context.Services.Wiki };
    private void BuildForms()
    {
        building = true;
        SiteForm = Form(N("longitude", "Longitudine", "°"), N("latitude", "Latitudine", "°"), C("method", "Interpolazione", "Superficie rigata", "Distanza inversa"));
        LifeForm = Form(N("nominal", "Vita nominale VN", "anni"), N("use", "Coefficiente d’uso CU"));
        DesignForm = Form(C("soil", "Sottosuolo", "A", "B", "C", "D", "E"), C("topography", "Topografia", "T1", "T2", "T3", "T4"), N("height", "Quota relativa h/H"), C("mode", "Spettro orizzontale", "Elastico", "Progetto con q"), N("damping", "Smorzamento elastico ξ", "%"), N("q0", "Fattore q0"), B("regular", "Regolare in altezza"), N("qv", "Fattore q verticale"));
        IsolationForm = Form(B("isolation", "Attiva isolamento"), N("mass", "Massa sismica M", "kN·s²/m"), N("structure_damping", "Smorzamento struttura", "%"), N("isolation_damping", "Smorzamento isolamento", "%"), N("factor_sle", "Fattore rigidezza SLE"), N("factor_slu", "Fattore rigidezza SLU"), new("group1_name", "Gruppo 1"), N("group1_count", "Numero gruppo 1"), N("group1_k", "Rigidezza singola 1", "kN/mm"), new("group2_name", "Gruppo 2"), N("group2_count", "Numero gruppo 2"), N("group2_k", "Rigidezza singola 2", "kN/mm"));
        PendulumForm = Form(B("pendulum", "Calcola pendolo"), N("normal_force", "Carico verticale N", "kN"), N("friction", "Coefficiente attrito μ"), N("displacement", "Spostamento d", "mm"), N("radius", "Raggio equivalente R", "mm"));
        AssignedForm = Form(new[] { B("assigned_hazard", "Usa parametri assegnati") }.Concat(new[] { "slo", "sld", "slv", "slc" }.SelectMany(s => new[] { N(s + "_ag", s.ToUpperInvariant() + " · ag", "g"), N(s + "_f0", s.ToUpperInvariant() + " · F0"), N(s + "_tc", s.ToUpperInvariant() + " · Tc*", "s") })).ToArray());
        building = false;
        foreach (string p in new[] { nameof(SiteForm), nameof(LifeForm), nameof(DesignForm), nameof(IsolationForm), nameof(PendulumForm), nameof(AssignedForm), nameof(Location), nameof(ImportSource), nameof(Catalog) }) OnPropertyChanged(p);
    }
    private void Edited(string key) { if (building || IsDisposed) return; if (key is "longitude" or "latitude") { Data["location"] = "Coordinate assegnate"; town = null; OnPropertyChanged(nameof(Town)); OnPropertyChanged(nameof(Location)); } Changed(); }
    protected override void OnCommit() { foreach (var f in Forms) f.Commit(); SpectraDocument.Validate(Data); }
    protected override void OnReloaded() { town = null; BuildForms(); }
    protected override void OnCalculationPublished() => RefreshResults();
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(CanEdit)) { foreach (var f in Forms) f.IsReadOnly = !CanEdit; ImportCommand?.NotifyCanExecuteChanged(); ApplyPendulumCommand?.NotifyCanExecuteChanged(); }
        if (e.PropertyName == nameof(Result)) RefreshResults();
    }
    private void RefreshResults() { foreach (string p in new[] { nameof(Reference), nameof(Source), nameof(ShowGrid), nameof(ShowState), nameof(PlotTitle), nameof(Note), nameof(Curves), nameof(Nodes), nameof(Summary), nameof(Ordinates), nameof(IsolationDetails), nameof(PendulumDetails) }) OnPropertyChanged(p); ExportCommand?.NotifyCanExecuteChanged(); CopyCommand?.NotifyCanExecuteChanged(); ApplyPendulumCommand?.NotifyCanExecuteChanged(); }
    private void Export() { if (Result is null) return; var path = Context.Services.Files.SaveFile(new([new("Spettri CSV", "*.csv")], "Spettri.csv")); if (path is not null) File.WriteAllText(path, SpectraDocument.Csv(Result), new System.Text.UTF8Encoding(true)); }
    private void Import() { if (!CanEdit) return; var path = Context.Services.Files.OpenFile(new([new("Esempio isolamento Excel", "*.xlsx")])); if (path is null) return; var imported = IsolationExampleImport.Read(path, Data); Edit(d => { d.Clear(); foreach (var entry in imported) d[entry.Key] = entry.Value?.DeepClone(); }); BuildForms(); }
    private void ApplyPendulum() { if (!CanEdit || Result?["pendulum"] is not JsonObject p) return; double k = SpectraDocument.Number(p, "EffectiveStiffnessKnPerMm"), damping = SpectraDocument.Number(p, "DampingPercent"); Edit(d => { d["group1_name"] = "Pendolo"; d["group1_k"] = k; d["isolation_damping"] = damping; }); BuildForms(); }
    private sealed class ReportSource(SpectraSheetViewModel sheet) : IModuleReportSource
    { public byte[] BuildReport(string title, IReadOnlySet<string> options, bool projectReport = false) { sheet.Commit(); var result = sheet.CalculateModule(sheet.Data); return DocxWriter.Write(SpectraReport.Create(title, sheet.Data, result.Values)); } }
}
