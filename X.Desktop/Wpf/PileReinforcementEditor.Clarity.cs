using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;
internal sealed partial class PileReinforcementEditor
{
    Button ApproveButton(bool seismic)
    {
        var button=Ui.Button(seismic?"Approva tutto · sisma":"Approva tutto · ipotesi",()=>
        {
            Commit();ElasticHorizontalPile.ApproveReinforcementAssumptions(root,seismic);changed();Build();
        });
        button.ToolTip=seismic?"Conferma anche le ipotesi ordinarie, N/V sismici e M elastico q=1 con N concomitante. Non modifica azioni, staffe, coefficienti o risultati.":"Conferma azioni di progetto, modello di taglio, barre trattenute, zone di estremità e buona aderenza. Non modifica i valori numerici o gli esiti dei controlli.";
        return button;
    }
    internal Window CreateNtcWindow()
    {
        var explanation=Ui.Stack(Ui.Text("NTC 2018 · §7.2.5 · Fondazioni su pali",20,true),
            Ui.Text("Sintesi dei controlli disponibili in ANTHEA",15,true),
            Ui.Text("Palo: As ≥ 0,3% Ac; armatura trasversale φ ≥ 8 mm e passo ≤ 8φL. Zona dissipativa presso la testa, quando la capacità non è esclusa: estensione almeno 10D, As ≥ 1% Ac, staffe singole e passo ≤ 6φL.",14),
            Ui.Text("Senza valutazione specifica di duttilità: VRd ≥ 1,3|VEd|; compressione media in zona dissipativa < 0,45fcd; |Mel| < 1,5MRd(N). Le azioni devono appartenere alla combinazione pertinente. Il momento elastico non ridotto deve essere confermato, non viene ricavato dal carico ultimo di Broms.",14),
            Ui.Text("Il comando Approva tutto conferma le ipotesi selezionate: non rende soddisfatto un controllo fallito. La spirale è rappresentabile, ma non soddisfa la richiesta di staffe singole nella zona dissipativa e il suo modello resistente a taglio non è implementato.",14),
            Ui.Text("Perimetro",15,true),
            Ui.Text("Restano separati: zone dissipative profonde (5D presso i contatti pertinenti), generazione delle azioni sismiche, interazione cinematica, duttilità esplicita, nodo palo-plinto, SLE, ganci e confinamento dei giunti. L'esito generale distingue controlli non soddisfatti, dati da completare e verifiche escluse.",14),
            Ui.Text("Fonte consultata: D.M. 17 gennaio 2018, G.U. 20 febbraio 2018, S.O. n.8. Pagina PDF 217, pagina stampata 213. La scheda Pagina originale contiene la copia locale della pagina consultata; è leggibile senza Internet.",12),
            Ui.Text(ElasticHorizontalPile.SeismicSourceUrl,11,color:Ui.Muted));
        var page=new Image{Source=Ui.Asset("ntc-pali-7-2-5.png"),Width=1000,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left};
        var zoom=new Slider{Minimum=600,Maximum=1800,Value=1000,Width=210,TickFrequency=200,IsSnapToTickEnabled=true};zoom.ValueChanged+=(_,_)=>page.Width=zoom.Value;
        var original=Ui.Dock(new ScrollViewer{Content=page,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},bottom:Ui.Bar(Ui.Text("Zoom pagina originale",12),zoom));
        var tabs=new TabControl{Items={new TabItem{Header="Criteri e campo di applicazione",Content=new ScrollViewer{Content=Ui.Paper(explanation,20),VerticalScrollBarVisibility=ScrollBarVisibility.Auto}},new TabItem{Header="Pagina originale · 213",Content=original}}};
        return Ui.Dialog(this,"NTC 2018 · dettagli dei pali",tabs,1040,820);
    }
    static Brush SummaryColor(JsonNode summary)=>summary.S("colore") switch{"rosso"=>Brushes.Firebrick,"verde"=>Brushes.DarkGreen,_=>Brushes.DarkGoldenrod};
    void AddVerdict(StackPanel content,JsonNode record)
    {
        var value=record["riepilogo"]??ElasticHorizontalPile.ReinforcementSummary(record.AsObject());
        content.Children.Add(new Border{BorderBrush=SummaryColor(value),BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(4),Padding=new Thickness(8),Margin=new Thickness(0,6,0,6),Child=Ui.Text(value.S("stato"),13,true,SummaryColor(value))});
        foreach(var (key,title,color) in new[]{("non_soddisfatti","NON SODDISFATTI",Brushes.Firebrick),("da_completare","DA COMPLETARE",Brushes.DarkGoldenrod),("esclusi","FUORI DAL PERIMETRO",Brushes.SlateGray)})
        {
            var items=value.Array(key);if(items.Count==0)continue;
            content.Children.Add(new Expander{Header=$"{title} · {items.Count}",IsExpanded=key!="esclusi",Content=Ui.Text(string.Join("\n\n",items.Select(v=>"• "+v)),12,color:color),Margin=new Thickness(0,3,0,3)});
        }
        if(value.B("eseguiti_soddisfatti"))content.Children.Add(Ui.Text("Tutti i controlli eseguiti sul tratto sono soddisfatti. Le verifiche escluse restano elencate separatamente.",12,color:Brushes.DarkGreen));
    }
}
