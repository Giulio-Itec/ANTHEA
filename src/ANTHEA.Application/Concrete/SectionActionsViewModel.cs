using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Application.Concrete;

public sealed partial class SectionActionRow : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Origin { get; init; } = "";
    public bool Visible { get; init; } = true;
    [ObservableProperty] private string element = "";
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string state = "";
    [ObservableProperty] private string n = "";
    [ObservableProperty] private string vx = "";
    [ObservableProperty] private string vy = "";
    [ObservableProperty] private string t = "";
    [ObservableProperty] private string mx = "";
    [ObservableProperty] private string my = "";
    internal JsonObject Values() => J.Obj(("id", Id), ("elemento", Element.Trim()), ("nome", Name.Trim()),
        ("stato", SectionWorkspace.Sets.FirstOrDefault(k => SectionActionInputs.StateLabel(k) == State) ?? State),
        ("N", N), ("Vx", Vx), ("Vy", Vy), ("T", T), ("Mx", Mx), ("My", My), ("visible", Visible));
    internal static SectionActionRow From(JsonObject r, string state, string origin = "") => new()
    {
        Id = r.S("id", Guid.NewGuid().ToString("N")), Element = r.S("elemento"), Name = r.S("nome"),
        State = SectionActionInputs.StateLabel(state), N = r.S("N"), Vx = r.S("Vx"), Vy = r.S("Vy"), T = r.S("T"), Mx = r.S("Mx"), My = r.S("My"), Origin = origin, Visible = r.B("visible", true)
    };
}

/// <summary>Transactional editor: no mutation of the sheet until an explicitly applied valid draft.</summary>
public sealed partial class SectionActionsViewModel : ObservableObject
{
    private readonly JsonObject source;
    public ObservableCollection<SectionActionRow> Rows { get; } = [];
    public string[] States { get; } = SectionWorkspace.Sets.Select(SectionActionInputs.StateLabel).ToArray();
    public bool Legacy { get; }
    public bool HasOrigins => Rows.Any(r => r.Origin.Length > 0);
    public string Guidance => "Elemento è facoltativo. Tutte le righe sono verificate sulla sezione di questo foglio. N negativo a compressione; kN e kNm. Nessun coefficiente aggiunto.";
    // Audit of 14/10/2026: the old notice did not say that the former "Elastico" rows lose their own criterion
    public string MigrationNotice => "Dati precedenti: assegnare lo stato limite alle righe Plastico/Elastico e Taglio. Completare le componenti mancanti, oppure usare ‘Vuoti a zero’ solo se effettivamente nulle. Le righe non vengono fuse per nome. Dopo la conversione il criterio resistente non dipende più dalla riga: domini e verifiche N–M usano quello scelto nel pannello (inizialmente Elastico per lo stato SLV e Plastico per SLU), anche per le ex righe Elastico.";
    [ObservableProperty] private SectionActionRow? selected;
    [ObservableProperty] private string status = "Le modifiche saranno applicate solo con Applica.";
    public JsonObject? Result { get; private set; }
    public SectionActionsViewModel(JsonObject data)
    {
        Rows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasOrigins));
        source = (JsonObject)data.DeepClone(); SectionWorkspace.Prepare(source);
        Legacy = !SectionActionInputs.Unified(source);
        foreach (var (key, rows) in ConcreteSheetPreparation.ReadActions(source))
            foreach (var row in rows) Rows.Add(SectionActionRow.From(row, Legacy && key is "SLU" or "SLV" ? "" : key, Legacy ? SectionWorkspace.Label(key) : ""));
        if (Legacy) foreach (var row in SectionActionInputs.ShearRows(source)) Rows.Add(SectionActionRow.From(row, "", "Taglio"));
    }
    [RelayCommand] private void Add() { Selected = new() { Name = "Combo " + (Rows.Count + 1) }; Rows.Add(Selected); }
    [RelayCommand] private void Remove() { if (Selected is not null) Rows.Remove(Selected); }
    [RelayCommand] private void Duplicate()
    {
        if (Selected is null) return;
        var copy = SectionActionRow.From(Selected.Values(), Selected.State, Selected.Origin); copy.Id = Guid.NewGuid().ToString("N"); Rows.Add(copy); Selected = copy;
    }
    [RelayCommand] private void FillMissingWithZero()
    {
        if (Selected is null) return;
        foreach (string key in new[] { nameof(SectionActionRow.N), nameof(SectionActionRow.Vx), nameof(SectionActionRow.Vy), nameof(SectionActionRow.T), nameof(SectionActionRow.Mx), nameof(SectionActionRow.My) })
        {
            var property = typeof(SectionActionRow).GetProperty(key)!;
            if (string.IsNullOrWhiteSpace((string?)property.GetValue(Selected))) property.SetValue(Selected, "0");
        }
    }
    public void Import(byte[] bytes, bool replace)
    {
        var import = SectionActionsExcel.Read(bytes);
        if (import.Rows.Count == 0) throw new ArgumentException("Il foglio Azioni è vuoto: nessuna modifica.");
        string F(double? value) => value?.ToString("G17", CultureInfo.InvariantCulture) ?? "";
        var drafts = import.Rows.Select(r => new SectionActionRow
        {
            Element = r.Element, Name = r.Name, State = import.Legacy && r.Family is "SLU" or "SLV" or "Taglio" ? "" : SectionActionInputs.StateLabel(r.Family),
            N = F(r.N), Mx = F(r.Mx), My = F(r.My), Vx = F(r.Vx), Vy = F(r.Vy), T = F(r.T), Origin = import.Legacy ? SectionWorkspace.Label(r.Family) : ""
        }).ToArray();
        if (drafts.Length + (replace ? 0 : Rows.Count) > 10000) throw new ArgumentException("Massimo 10.000 combinazioni.");
        if (replace) Rows.Clear();
        foreach (var row in drafts) Rows.Add(row);
        Status = $"Lette {drafts.Length} righe. " + (import.FormulaCells > 0 ? $"{import.FormulaCells} formule: usati i valori memorizzati; verificare che Excel sia stato ricalcolato e salvato. " : "") + "Applica per confermare.";
    }
    public bool Apply()
    {
        try
        {
            var candidate = (JsonObject)source.DeepClone();
            SectionActionInputs.Replace(candidate, Rows.Select(r => r.Values()));
            Result = candidate; Status = "Sollecitazioni aggiornate."; return true;
        }
        catch (ArgumentException error) { Result = null; Status = error.Message; return false; }
    }
    public byte[] Export()
    {
        var candidate = (JsonObject)source.DeepClone(); SectionActionInputs.Replace(candidate, Rows.Select(r => r.Values()));
        return SectionActionsExcel.Write(Rows.Select(r =>
        {
            var v = r.Values(); double F(string k) => SectionWorkspace.Number(v.S(k), k);
            return new SectionActionsExcel.Row(v.S("stato"), r.Name, F("N"), F("Mx"), F("My"), F("Vx"), F("Vy"), F("T"), r.Element);
        }));
    }
}
