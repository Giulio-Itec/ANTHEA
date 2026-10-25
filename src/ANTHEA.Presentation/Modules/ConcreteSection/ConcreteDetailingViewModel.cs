using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>A topic of the detailing (a row of the tab 06): its key in <see cref="ConcreteDetailingPresentation.Topics"/>, its title and its checks.</summary>
public sealed record ConcreteDetailingTopic(string Key, string Title, ConcreteCardsViewModel Checks);

/// <summary>
/// The detailing and the anchorages of the RC section (tab 06, F5.10 sub-step S6: BuildDetailingPanel and RefreshDetailing of the historic
/// ConcreteDetailing.cs), without WPF:
/// <list type="bullet">
/// <item>the forms on <c>dettagli_costruttivi</c>, in the order of the historic view (<see cref="Forms"/>): the element, the cover and the
/// durability (<c>esposizione_sle</c> and <c>cmin_dur</c> calculated), the bars, the compressed bars retained, the supports; the net cover of the
/// inputs read-only (<see cref="CoverGeometry"/>), the links read-only (<see cref="Stirrups"/>) and the anchorage or lap
/// (<see cref="Anchorage"/>, on <c>ancoraggi</c>);</item>
/// <item>the summary of the detailing, the checks of every topic, the checks of the anchorage and the note of the durability, from
/// <see cref="ConcreteDetailingPresentation"/> (ANTHEA.Application).</item>
/// </list>
/// The sheet calculates the detailing at every change and at every change of tab (RefreshDetailing of ConcreteSheetViewModel.Detailing.cs) and
/// shows it here; the fields shown follow the element and the anchorage, as the historic view.
/// </summary>
public sealed class ConcreteDetailingViewModel : ObservableObject
{
    /// <summary>The note under the bars.</summary>
    public const string BarsNotice = "La zona riguarda i limiti di armatura: includere anche le barre sovrapposte. Per la lunghezza scegliere sotto Sovrapposizione rettilinea. As ortogonale: totale delle due facce per metro.";
    /// <summary>The note under the anchorages.</summary>
    public const string AnchorageNotice = "Riscontri sul disegno: le conferme registrano una verifica manuale della disposizione longitudinale.";

    private readonly ConcreteSheetViewModel sheet;
    private readonly List<FormViewModel> forms = [];
    private string durabilityNote = "";

    internal ConcreteDetailingViewModel(ConcreteSheetViewModel sheet)
    {
        this.sheet = sheet;
        var options = sheet.DetailingOptions;
        // BuildDetailingPanel of the historic view, in its order; nothing is written.
        FormViewModel Form(FieldSpec[] fields)
        {
            var form = new FormViewModel(options, fields, DetailingChanged, compact: true, wideChoices: true)
            {
                CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
            };
            forms.Add(form);
            return form;
        }
        Element = Form([new("elemento", "Elemento", Choices: ["Da scegliere", "Trave", "Pilastro", "Soletta piena", "Parete"])]);
        Cover = Form([new("aggregato", "Inerte massimo dg", "mm"), new("esposizione_sle", "Esposizione condivisa SLE", ReadOnly: true),
            new("vita_durabilita", "Vita utile di progetto", "anni", Choices: ["50", "100"]), new("qualita_copriferro", "Controllo qualità dei copriferri", Choices: ["No", "Sì"]),
            new("cmin_dur", "cmin,dur calcolato", "mm", ReadOnly: true), new("delta_c", "Tolleranza Δcdev", "mm")]);
        CoverGeometry = new FormViewModel(sheet.Input, [new("cover_mm", "Copriferro netto adottato", "mm", ReadOnly: true)], compact: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        Bars = Form([new("zona_sovrapposizione", "Sezione dentro una giunzione", Choices: ["No", "Sì"]), new("as_secondaria", "As ortogonale totale / m", "mm²/m"),
            new("passo_secondaria", "Interasse barre ortogonali", "mm"), new("zona_critica", "Zona di massimo momento / carico concentrato", Choices: ["No", "Sì"])]);
        Retained = Form([new("barre_trattenute", "Barre compresse trattenute", Choices: ["Da confermare", "Confermato"])]);
        Supports = Form([new("ancoraggio_appoggi", "Traslazione e ancoraggio agli appoggi", Choices: ["Da confermare", "Confermato"])]);
        Anchorage = new FormViewModel(sheet.Settings["ancoraggi"]!.AsObject(), [new("tipo", "Verifica", Choices: ["Ancoraggio rettilineo", "Sovrapposizione rettilinea"]),
            new("diametro", "Ø barra (vuoto: max sezione)", "mm"), new("sigma", "|σsd| (vuoto: fyd)", "MPa"), new("aderenza", "Condizioni di aderenza", Choices: ["Buona", "Altre condizioni"]),
            new("lunghezza", "Lunghezza disponibile", "mm"), new("percentuale", "Barre sovrapposte", "%"), new("interferro", "Interferro fra barre giuntate", "mm"),
            new("confinamento", "Confinamento / armatura trasversale", Choices: ["Da verificare", "Verificato sul disegno", "Non conforme"]),
            new("posizione", "Posizione e sfalsamento giunzioni", Choices: ["Da verificare", "Verificato sul disegno", "Non conforme"]),
            new("cautele", "Cautele per barre Ø > 32 mm", Choices: ["Da verificare", "Verificato sul disegno", "Non conforme"])], AnchorageChanged, compact: true, wideChoices: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        // The topics, in the order of the rows of the tab; the links read-only in the topic of the links (BuildStirrups(readOnly: true)).
        Topics = [new("cover", "Interferro / copriferro", new(2)), new("bars", "Armatura", new(2)), new("stirrups", "Staffe", new(2)), new("anchors", "Ancoraggi / appoggi", new(2))];
        Stirrups = new ConcreteStirrupsViewModel(sheet);
        UpdateAnchoragesCommand = new RelayCommand(UpdateAnchorages, () => sheet.CanEdit);
        ShowControlPanelCommand = new RelayCommand(() => sheet.SelectedTab = 0);
    }

    /// <summary>«Tipo di elemento».</summary>
    public FormViewModel Element { get; }
    /// <summary>«Interferro / copriferro»: the aggregate, the exposure shared with the serviceability, the durability, cmin,dur and Δcdev.</summary>
    public FormViewModel Cover { get; }
    /// <summary>The net cover of the inputs, read-only («Copriferro netto adottato»).</summary>
    public FormViewModel CoverGeometry { get; }
    /// <summary>«Armatura»: lap zone, orthogonal bars of the slabs and walls, zone of maximum moment.</summary>
    public FormViewModel Bars { get; }
    /// <summary>«Barre compresse trattenute» (topic of the links).</summary>
    public FormViewModel Retained { get; }
    /// <summary>«Traslazione e ancoraggio agli appoggi» (topic of the anchorages, beams only).</summary>
    public FormViewModel Supports { get; }
    /// <summary>The anchorage or lap checked (<c>ancoraggi</c>).</summary>
    public FormViewModel Anchorage { get; }
    /// <summary>The links of the section, read-only (topic of the links).</summary>
    public ConcreteStirrupsViewModel Stirrups { get; }

    /// <summary>The forms on <c>dettagli_costruttivi</c>, in the order of the historic view (element, cover, bars, retained bars, supports).</summary>
    public IReadOnlyList<FormViewModel> Forms => forms;

    /// <summary>«Riepilogo dei dettagli».</summary>
    public ConcreteCardsViewModel Summary { get; } = new();
    /// <summary>The checks of every topic, in the order of the rows of the tab.</summary>
    public IReadOnlyList<ConcreteDetailingTopic> Topics { get; }
    /// <summary>The checks of the anchorage or lap, under the topic «Ancoraggi / appoggi».</summary>
    public ConcreteCardsViewModel AnchorageChecks { get; } = new(2);
    /// <summary>The note of the durability, under the cover.</summary>
    public string DurabilityNote { get => durabilityNote; private set => SetProperty(ref durabilityNote, value); }

    /// <summary>«Aggiorna verifiche ancoraggi»: the texts typed in the anchorage written, then the detailing calculated again (not in a read-only revision).</summary>
    public IRelayCommand UpdateAnchoragesCommand { get; }
    /// <summary>
    /// «Modifica geometria / armature nel pannello di controllo →»: the tab 01. F5.15, S6-a (F5-46, rule T6): allowed in a read-only revision,
    /// since it only changes the tab; the historic buttons were disabled.
    /// </summary>
    public IRelayCommand ShowControlPanelCommand { get; }

    // The callback of the forms of the detailing: the detailing calculated again; the aggregate is shared with the shear, which is calculated
    // again; Modified.
    private void DetailingChanged(string key)
    {
        sheet.RefreshDetailing();
        if (key == "aggregato") sheet.InvalidateActions("Taglio");
        sheet.RaiseModified();
    }

    // The callback of the anchorage: the detailing calculated again, Modified.
    private void AnchorageChanged(string key)
    {
        sheet.RefreshDetailing(); sheet.RaiseModified();
    }

    private void UpdateAnchorages()
    {
        if (!sheet.CanEdit) return;
        Anchorage.Commit(); sheet.RefreshDetailing();
    }

    // ---------------------------------------------------------------- RefreshDetailing

    /// <summary>The first part of RefreshDetailing: the cover of the inputs, the fields of the lap and those of the element chosen.</summary>
    internal void ShowFields()
    {
        CoverGeometry.Set("cover_mm", sheet.Input.S("cover_mm"), true);
        bool lap = ConcreteDetailingPresentation.Lap(sheet.Settings);
        foreach (string key in ConcreteDetailingPresentation.LapFields) Anchorage.ShowField(key, lap);
        foreach (var form in forms)
            foreach (var field in form.Fields)
                if (ConcreteDetailingPresentation.ElementFieldShown(sheet.Settings, field.Key) is bool shown) form.ShowField(field.Key, shown);
    }

    /// <summary>The second part of RefreshDetailing: the values calculated, the note, the precautions for large bars and the checks.</summary>
    internal void Show(ConcreteDetailingPresentation presentation)
    {
        if (presentation.Exposure is not null) Cover.Set("esposizione_sle", presentation.Exposure, true);
        Cover.Set("cmin_dur", ConcreteDetailingPresentation.DurabilityCoverText(presentation.DurabilityCover), true);
        DurabilityNote = presentation.DurabilityNote;
        if (presentation.LargeBarPrecautions is bool largeBars) Anchorage.ShowField("cautele", largeBars);
        Summary.Fill(presentation.Summary); AnchorageChecks.Fill(presentation.AnchorageDetails);
        foreach (var topic in Topics) topic.Checks.Fill(presentation.Topics[topic.Key]);
    }

    /// <summary>The values of the forms shown again from the data (after «Applica configurazione» of the reinforcement design, S8).</summary>
    internal void ReloadFields()
    {
        foreach (var form in forms) foreach (var field in form.Fields) form.Set(field.Key, sheet.DetailingOptions.S(field.Key), true);
        foreach (var field in Anchorage.Fields) Anchorage.Set(field.Key, Anchorage.Values.S(field.Key), true);
    }

    /// <summary>The read-only revision changed: the forms and the commands follow.</summary>
    internal void RefreshEditable(bool readOnly)
    {
        foreach (var form in forms.Append(CoverGeometry).Append(Anchorage)) form.IsReadOnly = readOnly;
        Stirrups.RefreshEditable(readOnly);
        UpdateAnchoragesCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Every form of the tab, for the help of the fields: the forms of the detailing, the cover, the anchorage and the links.</summary>
    public IEnumerable<FormViewModel> AllForms => forms.Append(CoverGeometry).Append(Anchorage).Concat(Stirrups.Forms);
}
