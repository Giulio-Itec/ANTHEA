using Anthea.Application.Walls;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.RetainingWall;
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
        var form = new InputForm(Data["seismic"]!.AsObject(), RetainingWallFields.Seismic, _ => { UpdateFields(); Changed(); }, compact: true, wideChoices: true); Forms["seismic"] = form;
        return Group("Sisma · dai dati del sito alle spinte", Ui.Stack(
            Ui.Text("Mononobe–Okabe: muro libero di spostarsi, spinta attiva. Wood semplificato: muro rigido vincolato, spinta a riposo. Scegliere in base ai vincoli effettivi dell’opera.", 11, color: Ui.Muted), form, seismicAudit,
            Ui.Text("ag/g è un rapporto: per 0,20 g scrivere 0,20. ag/g e F₀ provengono dai parametri sismici SLV del progetto; non vengono ricavati dalla località. A–E richiede la classificazione geotecnica, non il solo φ′. Cresta stretta: larghezza in sommità molto minore di quella alla base; topografia semplice prevalentemente bidimensionale.", 11, color: Ui.Muted),
            Ui.Text("Le combinazioni automatiche usano i coefficienti ricavati qui, con casi separati per ribaltamento. Una matrice personalizzata conserva i valori delle proprie righe: rigenerarla o aggiornarla e confermarla dopo una modifica dei dati del sito.", 11, color: Ui.Muted),
            Group("Ipotesi e campo del modello sismico", Ui.Text(WallModel.SeismicHelp, 11, color: Ui.Muted), false)), Data["seismic"].B("enabled"));
    }
    private void UpdateSeismicFields()
    {
        if (!Forms.TryGetValue("seismic", out var form)) return;
        // Visibility and enabled fields: RetainingWallPresentation.InputRules (UpdateFields); here the coefficients of the site.
        var s = Data["seismic"]!; bool enabled = s.B("enabled"), site = s.S("source") == WallModel.SeismicSite;
        seismicAudit.Foreground = Appearance.Ink;
        if (!enabled) { seismicAudit.Text = RetainingWallPresentation.SeismicDisabled; return; }
        if (!site) { seismicAudit.Text = RetainingWallPresentation.SeismicManualText; return; }
        try
        {
            var result = WallModel.DeriveSeismic(Data)!;
            s["kh"] = result.Kh; s["kv"] = result.Kv;
            form.Set("kh", s.S("kh"), display: true); form.Set("kv", s.S("kv"), display: true);
            seismicAudit.Text = result.Description;
        }
        catch (ArgumentException ex) { seismicAudit.Text = ex.Message; seismicAudit.Foreground = Appearance.Foreground(Brushes.Firebrick); }
    }
}
