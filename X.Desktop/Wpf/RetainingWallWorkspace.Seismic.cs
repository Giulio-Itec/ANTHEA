using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    private readonly TextBlock seismicAudit = Ui.Text("", 12);
    private Expander BuildSeismicCard()
    {
        if (seismicAudit.Parent is Panel parent) parent.Children.Remove(seismicAudit);
        var fields = new Field[] { new("enabled", "Considera il sisma", Bool: true),
            new("method", "Comportamento del muro / modello di spinta", Choices: ["Mononobe–Okabe", "Wood semplificato"]),
            new("source", "Come definire l’azione sismica", Choices: [RetainingWall.SeismicSite, RetainingWall.SeismicManual]),
            new("ag_g", "Accelerazione su suolo rigido allo SLV · ag/g", "−"),
            new("ss_mode", "Amplificazione del sottosuolo · Ss", Choices: [RetainingWall.AmplificationCalculated, RetainingWall.AmplificationAssigned]),
            new("soil_class", "Categoria dalla relazione geotecnica", Choices: ["Da scegliere", "A", "B", "C", "D", "E"]),
            new("f0", "Fattore spettrale del sito allo SLV · F₀", "−"), new("ss", "Coefficiente Ss assegnato", "−"),
            new("st_mode", "Amplificazione topografica · St", Choices: [RetainingWall.AmplificationCalculated, RetainingWall.AmplificationAssigned]),
            new("topography", "Forma del terreno circostante", Choices: [RetainingWall.TopographyFlat, RetainingWall.TopographySlope, RetainingWall.TopographyRidge]),
            new("slope", "Inclinazione media del pendio / rilievo", "°"),
            new("relief_height", "Altezza del pendio / rilievo (non del muro)", "m"), new("site_height", "Quota del muro sopra la base del pendio / rilievo", "m"),
            new("st", "Coefficiente St assegnato", "−"), new("kh", "Coefficiente orizzontale kh assegnato", "−"), new("kv", "Coefficiente verticale |kv| assegnato", "−") };
        var form = new InputForm(Data["seismic"]!.AsObject(), fields, _ => { UpdateFields(); Changed(); }, compact: true, wideChoices: true); Forms["seismic"] = form;
        return Group("Sisma · dai dati del sito alle spinte", Ui.Stack(
            Ui.Text("Mononobe–Okabe: muro libero di spostarsi, spinta attiva. Wood semplificato: muro rigido vincolato, spinta a riposo. Scegliere in base ai vincoli effettivi dell’opera.", 11, color: Ui.Muted), form, seismicAudit,
            Ui.Text("ag/g è un rapporto: per 0,20 g scrivere 0,20. ag/g e F₀ provengono dai parametri sismici SLV del progetto; non vengono ricavati dalla località. A–E richiede la classificazione geotecnica, non il solo φ′. Cresta stretta: larghezza in sommità molto minore di quella alla base; topografia semplice prevalentemente bidimensionale.", 11, color: Ui.Muted),
            Ui.Text("Le combinazioni automatiche usano i coefficienti ricavati qui, con casi separati per ribaltamento. Una matrice personalizzata conserva i valori delle proprie righe: rigenerarla o aggiornarla e confermarla dopo una modifica dei dati del sito.", 11, color: Ui.Muted),
            Group("Ipotesi e campo del modello sismico", Ui.Text(RetainingWall.SeismicHelp, 11, color: Ui.Muted), false)), Data["seismic"].B("enabled"));
    }
    private void UpdateSeismicFields()
    {
        if (!Forms.TryGetValue("seismic", out var form)) return;
        var s = Data["seismic"]!; bool enabled = s.B("enabled"), site = s.S("source") == RetainingWall.SeismicSite;
        bool ssAuto = s.S("ss_mode") == RetainingWall.AmplificationCalculated, stAuto = s.S("st_mode") == RetainingWall.AmplificationCalculated;
        bool relief = s.S("topography") != RetainingWall.TopographyFlat;
        foreach (var key in form.Editors.Keys.Where(k => k != "enabled")) form.Enable(key, enabled, true);
        foreach (string key in new[] { "kh", "kv" }) form.ShowField(key, !site);
        foreach (string key in new[] { "ag_g", "ss_mode", "st_mode" }) form.ShowField(key, site);
        form.ShowField("soil_class", site && ssAuto); form.ShowField("f0", site && ssAuto && s.S("soil_class") != "A"); form.ShowField("ss", site && !ssAuto);
        form.ShowField("topography", site && stAuto); form.ShowField("st", site && !stAuto); form.ShowField("slope", site && stAuto && relief);
        foreach (string key in new[] { "relief_height", "site_height" }) form.ShowField(key, site && stAuto && relief && s.D("slope") > 15);
        seismicAudit.Foreground = Ui.Navy;
        if (!enabled) { seismicAudit.Text = "Sisma escluso. Attivarlo per calcolare e verificare i casi sismici."; return; }
        if (!site) { seismicAudit.Text = "Modalità manuale: kh e |kv| sono dati assegnati. Per ricavarli da ag/g, selezionare Da parametri del sito (SLV)."; return; }
        try
        {
            var result = RetainingWall.DeriveSeismic(Data)!;
            s["kh"] = result.Kh; s["kv"] = result.Kv;
            form.Set("kh", s.S("kh"), display: true); form.Set("kv", s.S("kv"), display: true);
            seismicAudit.Text = result.Description;
        }
        catch (ArgumentException ex) { seismicAudit.Text = ex.Message; seismicAudit.Foreground = Brushes.Firebrick; }
    }
}
