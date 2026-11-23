using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.QuickSection;

public sealed record QuickShearParameterRow(string Label, FieldViewModel X, FieldViewModel Y, bool Cotangent = false);

public sealed class QuickShearCombinationRow : ObservableObject
{
    private readonly QuickSectionSheetViewModel owner;
    private readonly JsonObject row;
    internal QuickShearCombinationRow(QuickSectionSheetViewModel owner, JsonObject row, IRelayCommand remove)
    { this.owner = owner; this.row = row; RemoveCommand = remove; }
    public string Id => row.S("id");
    public bool CanEdit => owner.CanEdit;
    public bool IsReadOnly => !CanEdit;
    public string Name { get => row.S("nome"); set => Write("nome", value); }
    public string Condition { get => ConcreteQuickShear.Condition(owner.Data, row); set => Write(ConcreteQuickShear.ConditionField, value); }
    public string N { get => row.S("N"); set => Write("N", value); }
    public string Vx { get => row.S("Vx"); set => Write("Vx", value); }
    public string Vy { get => row.S("Vy"); set => Write("Vy", value); }
    public IReadOnlyList<ConcreteQuickShearChoice> Conditions => ConcreteQuickShear.Choices;
    public bool IsSelected { get => owner.SelectedShearCombinationId == Id; set { if (value) owner.SelectShearCombination(Id); } }
    private ConcreteQuickShearCase? Result => owner.Current?.Shear?.FirstOrDefault(r => r.Id == Id);
    public string RatioX => Result?.Directions.FirstOrDefault(d => d.Axis == "X")?.Utilization ?? "—";
    public string RatioY => Result?.Directions.FirstOrDefault(d => d.Axis == "Y")?.Utilization ?? "—";
    public bool? Passed => Result is { Directions.Count: > 0 } result
        ? result.Directions.Any(d => d.Passed == false) ? false : result.Directions.All(d => d.Passed == true) ? true : null : null;
    public string Status => Result is { Error.Length: > 0 } result ? result.Error : string.Join(" · ", Result?.Directions.Select(d => d.Axis + ": " + d.ResistanceStatus) ?? []);
    public IRelayCommand RemoveCommand { get; }
    private void Write(string key, string? value, [System.Runtime.CompilerServices.CallerMemberName] string property = "")
    {
        if (value is not null && value != row.S(key) && owner.EditShearCombination(Id, key, value)) row[key] = value;
        OnPropertyChanged(property);
    }
    internal void Refresh()
    {
        foreach (string property in new[] { nameof(IsSelected), nameof(RatioX), nameof(RatioY), nameof(Passed), nameof(Status), nameof(CanEdit), nameof(IsReadOnly) }) OnPropertyChanged(property);
        RemoveCommand.NotifyCanExecuteChanged();
    }
}

public sealed partial class QuickSectionSheetViewModel
{
    private IRelayCommand? toggleShearCommand;
    private IRelayCommand? copyShearCombinationsCommand, addShearCombinationCommand, moveShearUpCommand, moveShearDownCommand, resetShearConditionsCommand;
    private bool? shearView;
    private string? shearSelection;
    public IRelayCommand ToggleShearCommand => toggleShearCommand ??= GuardedCommands.Create(Guard, () =>
    {
        bool next = !ShearExpanded;
        if (CanEdit) Edit(d => { ConcreteQuickShear.Write(d, "aperto", JsonValue.Create(next)); if (next) ConcreteQuickShear.EnsureCombination(d); });
        shearView = next;
        BuildShearForms();
        BuildShearCombinations();
    });
    public bool ShearExpanded => shearView ?? ConcreteQuickShear.Expanded(Data);
    public string ShearToggleText => ShearExpanded ? "◂ Chiudi taglio" : "Taglio ▸";
    public double SheetMinimumWidth => ShearExpanded ? 1370 : 850;
    public bool EffectiveStirrup
    {
        get => ConcreteQuickShear.UsesGeometryLink(Data);
        set
        {
            if (value == EffectiveStirrup || !Edit(d => ConcreteQuickShear.Write(d, ConcreteQuickShear.EffectiveLink, JsonValue.Create(value)))) return;
            BuildShearForms(); NotifyShear();
        }
    }
    public bool IndependentShearReinforcement => !EffectiveStirrup;
    public string ShearGeometryDiameter => "Ø staffa geometrica: " + Input.S("transverse_bar_diameter_mm") + " mm";
    public string ShearReinforcementNote => EffectiveStirrup
        ? "Il diametro della staffa geometrica è usato anche a taglio. Passo e braccia sono quelli assegnati qui."
        : "Armatura a taglio indipendente: non modifica la posizione delle barre longitudinali. La staffa geometrica non viene conteggiata a taglio.";
    public IReadOnlyList<QuickShearCombinationRow> ShearCombinationRows { get; private set; } = [];
    public string? SelectedShearCombinationId => shearSelection ?? ConcreteQuickShear.Selected(Data);
    private JsonObject? SelectedShearRow => ConcreteQuickShear.Combinations(Data).FirstOrDefault(r => r.S("id") == SelectedShearCombinationId);
    public string ShearCombination => SelectedShearRow is { } row ? row.S("nome") + " · " + ConcreteQuickShear.ShortLabel(ConcreteQuickShear.Model(Data, row)) : "Nessuna combinazione a taglio";
    public IReadOnlyList<string> ShearModels => ConcreteQuickShear.Choices.Select(c => c.Label).ToArray();
    public string? ShearModel
    {
        get => SelectedShearRow is { } row ? ConcreteQuickShear.Label(ConcreteQuickShear.Model(Data, row)) : null;
        set
        {
            if (value == ShearModel || building || IsDisposed || SelectedShearCombinationId is not { } id) return;
            if (ConcreteQuickShear.Choices.FirstOrDefault(c => c.Label == value) is { } choice) EditShearCombination(id, "modello", choice.Code);
        }
    }
    public bool CanEditShearReinforcement => CanEdit && ShearWithLinks;
    public bool ShearWithoutLinks => !ShearWithLinks && SelectedShearRow is not null;
    public bool CanEditSelectedShear => CanEdit && SelectedShearRow is not null;
    public string ShearMomentX { get => SelectedShearRow?.S("Mx", "0") ?? "0"; set => WriteShearMoment("Mx", value); }
    public string ShearMomentY { get => SelectedShearRow?.S("My", "0") ?? "0"; set => WriteShearMoment("My", value); }
    private void WriteShearMoment(string key, string value)
    {
        if (CanEditSelectedShear && SelectedShearCombinationId is { } id) EditShearCombination(id, key, value);
    }
    public IRelayCommand CopyShearCombinationsCommand => copyShearCombinationsCommand ??= GuardedCommands.Create(Guard,
        () => ChangeShearRows(ConcreteQuickShear.CopyCombinations), () => CanEdit);
    public IRelayCommand AddShearCombinationCommand => addShearCombinationCommand ??= GuardedCommands.Create(Guard,
        () => ChangeShearRows(ConcreteQuickShear.AddCombination), () => CanEdit);
    public IRelayCommand ResetShearConditionsCommand => resetShearConditionsCommand ??= GuardedCommands.Create(Guard,
        () => ChangeShearRows(ConcreteQuickShear.ResetConditions), () => CanEdit && ConcreteQuickShear.CanResetConditions(Data));
    public IRelayCommand MoveShearUpCommand => moveShearUpCommand ??= GuardedCommands.Create(Guard, () => MoveShear(-1), () => CanMoveShear(-1));
    public IRelayCommand MoveShearDownCommand => moveShearDownCommand ??= GuardedCommands.Create(Guard, () => MoveShear(1), () => CanMoveShear(1));
    private bool CanMoveShear(int direction)
    {
        int index = ShearCombinationRows.ToList().FindIndex(r => r.Id == SelectedShearCombinationId);
        return CanEdit && index >= 0 && index + direction >= 0 && index + direction < ShearCombinationRows.Count;
    }
    private void MoveShear(int direction)
    {
        if (CanMoveShear(direction) && SelectedShearCombinationId is { } id) ChangeShearRows(d => ConcreteQuickShear.MoveCombination(d, id, direction));
    }
    private void ChangeShearRows(Action<JsonObject> change)
    {
        if (!Edit(change)) return;
        shearSelection = null; BuildShearForms(); BuildShearCombinations();
    }
    private void BuildShearCombinations()
    {
        ShearCombinationRows = ConcreteQuickShear.Combinations(Data).Select(row =>
        {
            string id = row.S("id");
            return new QuickShearCombinationRow(this, row, GuardedCommands.Create(Guard,
                () => ChangeShearRows(d => ConcreteQuickShear.RemoveCombination(d, id)), () => CanEdit && ShearCombinationRows.Count > 1));
        }).ToArray();
        OnPropertyChanged(nameof(ShearCombinationRows)); NotifyShear();
    }
    internal bool EditShearCombination(string id, string key, string value)
    {
        bool done = Edit(d => ConcreteQuickShear.WriteCombination(d, id, key, value));
        if (done && key is ConcreteQuickShear.ConditionField or "modello") BuildShearForms();
        NotifyShear(); return done;
    }
    internal void SelectShearCombination(string id)
    {
        if (IsDisposed || id == SelectedShearCombinationId) return;
        if (CanEdit) Edit(d => ConcreteQuickShear.Write(d, "selezionata", JsonValue.Create(id)));
        shearSelection = id;
        FollowSelectedActionCombination("Taglio");
        BuildShearForms();
        NotifyShear();
    }
    public FormViewModel ShearForm { get; private set; } = null!;
    public FormViewModel ShearAdvancedForm { get; private set; } = null!;
    public IReadOnlyList<FieldViewModel> ShearMainFields => ShearForm?.Fields.Where(f => f.Key != ConcreteQuickShear.Diameter).ToArray() ?? [];
    public FieldViewModel? ShearDiameterField => ShearForm?.Fields.FirstOrDefault(f => f.Key == ConcreteQuickShear.Diameter);
    public FieldViewModel? ShearSpacingField => ShearForm?.Fields.FirstOrDefault(f => f.Key == ConcreteQuickShear.Spacing);
    public IReadOnlyList<QuickShearParameterRow> ShearParameterRows => ShearForm is null ? [] : new[] {
        new QuickShearParameterRow("Braccia", ShearForm.Fields.Single(f => f.Key == "rami_x"), ShearForm.Fields.Single(f => f.Key == "rami_y")),
        new QuickShearParameterRow("α [°]", ShearForm.Fields.Single(f => f.Key == "alpha_x"), ShearForm.Fields.Single(f => f.Key == "alpha_y")),
        new QuickShearParameterRow("cot θ", ShearForm.Fields.Single(f => f.Key == "cot_x"), ShearForm.Fields.Single(f => f.Key == "cot_y"), true) };
    private IRelayCommand? balanceShearCommand;
    public IRelayCommand BalanceShearCommand => balanceShearCommand ??= GuardedCommands.Create(Guard, () =>
    {
        if (!Edit(ConcreteQuickShear.BalanceShear)) return;
        BuildShearForms(); NotifyShear();
    }, () => CanEdit && ShearWithLinks);
    public string ShearAutomaticCotangents => string.Join(" · ", ShearResults
        .Where(r => string.IsNullOrWhiteSpace(ConcreteQuickShear.Options(Data).S("cot_" + r.Axis.ToLowerInvariant())))
        .Select(r => "cot θ V" + r.Axis.ToLowerInvariant() + " = " + EngineeringFormat.Number(r.CotTheta)));
    private IEnumerable<FormViewModel> ShearForms => new[] { ShearForm, ShearAdvancedForm }.Where(f => f is not null);
    public ConcreteQuickShearCase? SelectedShearResult => Current?.Shear?.FirstOrDefault(r => r.Id == SelectedShearCombinationId);
    public IReadOnlyList<ConcreteQuickShearDirection> ShearResults => SelectedShearResult?.Directions ?? [];
    public string ShearStatus => ShearCombinationRows.Count == 0 ? "Copia le combinazioni del momento oppure aggiungi una combinazione e inserisci i tagli."
        : SelectedShearResult is { Error.Length: > 0 } failed ? failed.Error
        : SelectedShearResult is { Directions.Count: > 0 } ? ""
        : "I risultati si aggiornano dopo ogni modifica. Inserire le azioni di taglio della combinazione selezionata.";
    public string ShearDetail => string.Join("\n\n", ShearResults.Select(d => "Direzione " + d.Axis + " · bw = " + d.Width + " mm · d = " + d.Depth
        + " mm · Asl = " + d.SteelArea + " mm²\n" + d.Reference + "\n" + d.Detail));
    public bool ShearWithLinks => SelectedShearRow is { } row && ConcreteQuickShear.Model(Data, row) == ConcreteQuickShear.WithLinks;

    private void BuildShearForms()
    {
        var options = ConcreteQuickShear.Options(Data);
        void ChangedOption(string key)
        {
            if (building || IsDisposed) return;
            if (!Edit(d => ConcreteQuickShear.Write(d, key, options[key]))) return;
            if (key == "parametri") BuildShearForms();
            NotifyShear();
        }
        ShearForm = Form(options, [T(ConcreteQuickShear.Diameter, "Nuovo Ø", "mm", "Diametro della nuova armatura resistente a taglio, indipendente dalla staffa geometrica"),
            T(ConcreteQuickShear.Spacing, "Passo", "mm", "Passo dell’armatura effettivamente resistente a taglio"),
            T("rami_x", "Braccia Vx", "", "Numero di braccia resistenti al taglio Vx"), T("rami_y", "Braccia Vy", "", "Numero di braccia resistenti al taglio Vy"),
            T("alpha_x", "α Vx", "°", "Inclinazione dell’armatura a taglio Vx"), T("alpha_y", "α Vy", "°", "Inclinazione dell’armatura a taglio Vy"),
            T("cot_x", "cot θ Vx", "", "Vuoto: scelta automatica del motore"), T("cot_y", "cot θ Vy", "", "Vuoto: scelta automatica del motore")], ChangedOption);
        foreach (var field in ShearForm.Fields.Where(f => f.Key != "modello")) ShearForm.Enable(field.Key, ShearWithLinks);
        ShearAdvancedForm = Form(options, [new("parametri", "Geometria", Choices: ["Automatici da sezione", "Manuali"]),
            T("bw_x", "bw Vx", "mm", "Larghezza resistente per Vx"), T("d_x", "d Vx", "mm", "Altezza utile per Vx"), T("asl_x", "Asl Vx", "mm²", "Armatura longitudinale efficacemente ancorata per Vx"),
            T("bw_y", "bw Vy", "mm", "Larghezza resistente per Vy"), T("d_y", "d Vy", "mm", "Altezza utile per Vy"), T("asl_y", "Asl Vy", "mm²", "Armatura longitudinale efficacemente ancorata per Vy"),
            new("modello_circolare", "Circolare", Choices: ["Da scegliere", "Pile · NTC §7.9.5.2", "Parametri assegnati"]), T("z_d", "z/d", "", "Valore assegnato per il modello circolare")], ChangedOption);
        foreach (string field in new[] { "bw_x", "d_x", "asl_x", "bw_y", "d_y", "asl_y" })
        {
            ShearAdvancedForm.Enable(field, options.S("parametri") == "Manuali");
            ShearAdvancedForm.ShowField(field, options.S("parametri") == "Manuali");
        }
        foreach (string field in new[] { "modello_circolare", "z_d" }) ShearAdvancedForm.ShowField(field, IsCircular);
        foreach (string property in new[] { nameof(ShearForm), nameof(ShearAdvancedForm), nameof(ShearMainFields), nameof(ShearDiameterField), nameof(ShearSpacingField), nameof(ShearParameterRows) }) OnPropertyChanged(property);
    }

    private void NotifyShear()
    {
        NotifyActions();
        foreach (string property in new[] { nameof(ShearExpanded), nameof(ShearToggleText), nameof(SheetMinimumWidth), nameof(EffectiveStirrup), nameof(IndependentShearReinforcement),
            nameof(ShearGeometryDiameter), nameof(ShearReinforcementNote), nameof(ShearCombination), nameof(ShearModel), nameof(SelectedShearCombinationId),
            nameof(ShearWithoutLinks), nameof(CanEditShearReinforcement), nameof(ShearAutomaticCotangents),
            nameof(ShearResults), nameof(ShearStatus), nameof(ShearDetail), nameof(ShearWithLinks), nameof(CanEditSelectedShear), nameof(ShearMomentX), nameof(ShearMomentY) }) OnPropertyChanged(property);
        foreach (var row in ShearCombinationRows) row.Refresh();
        foreach (var command in new[] { copyShearCombinationsCommand, addShearCombinationCommand, moveShearUpCommand, moveShearDownCommand, resetShearConditionsCommand, exportShearExcelCommand, importShearExcelCommand, balanceShearCommand }) command?.NotifyCanExecuteChanged();
    }
}
