using Anthea.Application.Concrete;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;
internal sealed partial class ConcreteWorkspace
{
    private readonly VerificationCards detailingText = new();
    private readonly VerificationCards anchorageText = new(2);
    private InputForm? detailingForm;
    private IReadOnlyList<ConcreteResults.DetailingCheck> detailingResults = [];
    private ConcreteResults.AnchorageResult? anchorageResult;
    private JsonObject DetailingOptions => settings["dettagli_costruttivi"]!.AsObject();
    private readonly Dictionary<string, VerificationCards> detailingTopics = new();
    private readonly List<InputForm> detailingForms = [];
    private InputForm coverDetailingForm = null!;
    private InputForm coverDetailingGeometry = null!;
    private InputForm anchorageForm = null!;
    private readonly TextBlock durabilityNote = Ui.Text("",11);
    private UIElement BuildDetailingPanel()
    {
        var o=DetailingOptions;
        InputForm Form(Field[] fields)
        {
            var form = new InputForm(o, fields, key => { RefreshDetailing(); exportResult=null; if (key == "aggregato") InvalidateActions("Taglio"); Modified?.Invoke(); },true,true);
            detailingForms.Add(form); return form;
        }
        detailingForm = Form([new("elemento","Elemento",Choices:["Da scegliere","Trave","Pilastro","Soletta piena","Parete"])]);
        coverDetailingForm = Form([new("aggregato","Inerte massimo dg","mm"),new("esposizione_sle","Esposizione condivisa SLE",ReadOnly:true),new("vita_durabilita","Vita utile di progetto","anni",Choices:["50","100"]),new("qualita_copriferro","Controllo qualità dei copriferri",Choices:["No","Sì"]),new("cmin_dur","cmin,dur calcolato","mm",ReadOnly:true),new("delta_c","Tolleranza Δcdev","mm")]);
        var cover = new InputForm(Input,[new("cover_mm","Copriferro netto adottato","mm",ReadOnly:true)],_=>{},true);
        coverDetailingGeometry = cover;
        var bars = Form([new("zona_sovrapposizione","Sezione dentro una giunzione",Choices:["No","Sì"]),new("as_secondaria","As ortogonale totale / m","mm²/m"),new("passo_secondaria","Interasse barre ortogonali","mm"),new("zona_critica","Zona di massimo momento / carico concentrato",Choices:["No","Sì"])]);
        var stirrups = Form([new("barre_trattenute","Barre compresse trattenute",Choices:["Da confermare","Confermato"])]);
        var supports = Form([new("ancoraggio_appoggi","Traslazione e ancoraggio agli appoggi",Choices:["Da confermare","Confermato"])]);
        var anchorOptions=settings["ancoraggi"]!.AsObject();
        var af = anchorageForm = new InputForm(anchorOptions,[new("tipo","Verifica",Choices:["Ancoraggio rettilineo","Sovrapposizione rettilinea"]),new("diametro","Ø barra (vuoto: max sezione)","mm"),new("sigma","|σsd| (vuoto: fyd)","MPa"),new("aderenza","Condizioni di aderenza",Choices:["Buona","Altre condizioni"]),new("lunghezza","Lunghezza disponibile","mm"),new("percentuale","Barre sovrapposte","%"),new("interferro","Interferro fra barre giuntate","mm"),new("confinamento","Confinamento / armatura trasversale",Choices:["Da verificare","Verificato sul disegno","Non conforme"]),new("posizione","Posizione e sfalsamento giunzioni",Choices:["Da verificare","Verificato sul disegno","Non conforme"]),new("cautele","Cautele per barre Ø > 32 mm",Choices:["Da verificare","Verificato sul disegno","Non conforme"])],_=>{RefreshDetailing();exportResult=null;Modified?.Invoke();},true,true);
        var rows = new StackPanel();
        rows.Children.Add(WorkspaceLayout(Panel("Tipo di elemento",detailingForm),Panel("Riepilogo dei dettagli",detailingText)));
        void Topic(string key,string title,UIElement inputs,UIElement? extra=null)
        {
            var checks = new VerificationCards(2); detailingTopics[key]=checks;
            rows.Children.Add(WorkspaceLayout(Panel(title,inputs),Panel("Verifiche · "+title,extra is null?checks:Ui.Stack(checks,extra))));
        }
        Topic("cover","Interferro / copriferro",Ui.Stack(cover,Ui.Button("Modifica geometria nel pannello di controllo →",()=>tabs.SelectedIndex=0),coverDetailingForm,durabilityNote));
        Topic("bars","Armatura",Ui.Stack(bars,Ui.Button("Modifica armature nel pannello di controllo →",()=>tabs.SelectedIndex=0),Ui.Text("La zona riguarda i limiti di armatura: includere anche le barre sovrapposte. Per la lunghezza scegliere sotto Sovrapposizione rettilinea. As ortogonale: totale delle due facce per metro.",11)));
        Topic("stirrups","Staffe",Ui.Stack(BuildStirrups(readOnly:true),stirrups));
        Topic("anchors","Ancoraggi / appoggi",Ui.Stack(supports,af,Ui.Button("Aggiorna verifiche ancoraggi",()=>{af.Commit();RefreshDetailing();}),Ui.Text("Riscontri sul disegno: le conferme registrano una verifica manuale della disposizione longitudinale.",11)),anchorageText);
        return Scroller(rows);
    }
    private void RefreshDetailing()
    {
        if(detailingForm is null)return;
        coverDetailingGeometry.Set("cover_mm",Input.S("cover_mm"),true);
        bool lap=ConcreteDetailingPresentation.Lap(settings);
        foreach(string key in ConcreteDetailingPresentation.LapFields)anchorageForm.ShowField(key,lap);
        foreach(var form in detailingForms) foreach(string k in form.Editors.Keys) if(ConcreteDetailingPresentation.ElementFieldShown(settings,k) is bool shown)form.ShowField(k,shown);
        var presentation = Anthea.Application.Concrete.ConcreteDetailingPresentation.Calculate(
            Input, settings, actions.ToDictionary(p => p.Key, p => p.Value.Select(r => r.Values).ToArray()));
        presentation.ApplyDerivedValues(settings);
        detailingResults = presentation.Checks; anchorageResult = presentation.Anchorage; exportResult = null;
        if (presentation.Exposure is not null) coverDetailingForm.Set("esposizione_sle", presentation.Exposure, true);
        coverDetailingForm.Set("cmin_dur", ConcreteDetailingPresentation.DurabilityCoverText(presentation.DurabilityCover), true);
        durabilityNote.Text = presentation.DurabilityNote;
        if (presentation.LargeBarPrecautions is bool largeBars) anchorageForm.ShowField("cautele", largeBars);
        void Fill(VerificationCards view, Anthea.Application.Presentation.VerificationDetails source)
        {
            if (source.Items.Count == 0) { if (source.Text.Length == 0) view.Start(); else view.Text = source.Text; }
            else { view.Start(); foreach (var detail in source.Items) view.AddDetail(detail.Title, detail.Passed, detail.Values, detail.Explanation, detail.Reference); }
        }
        Fill(detailingText, presentation.Summary); Fill(anchorageText, presentation.AnchorageDetails);
        foreach (var topic in detailingTopics) Fill(topic.Value, presentation.Topics[topic.Key]);
    }
}
