using X.Core;

namespace X.Desktop;

public sealed partial class FoglioEditor
{
    private void RefreshPileNq()
    {
        if(PileCapacity&&GetAll(outputs).OfType<Plot>().FirstOrDefault(p=>p.Name=="nq") is Plot reference)BuildReferencePlot(reference);
    }
    private void AddActualPileNq(Plot target,List<Serie> series,bool big)
    {
        target.Markers=[];target.Note="";
        double length=Data["generali"].D("lunghezza"),diameter=Data["generali"].D("diametro");
        if(length<=0||diameter<=0||!double.IsFinite(length/diameter))return;
        double ratio=length/diameter,low=25,high=big?42:41.6;
        int selected=sondages?.SelectedIndex??-1;
        var tip=Result?.B("copertura_completa")==true?Result.Array("dettagli").LastOrDefault(d=>Math.Abs(d.D("z")-length)<1e-8):null;
        var surveys=tip?.Array("sondaggi");
        if(selected>=0&&surveys is not null&&selected<surveys.Count&&surveys[selected]?["dettaglio_nq"] is { } detail)
        {
            double phi=detail.D("phi"),nq=detail.D("nq");low=Math.Min(low,phi);high=Math.Max(high,phi);
            target.Markers.Add(new(phi,nq,$"S{selected+1}: φ={phi:0.##}° · {(big?"Nq*":"Nq")}={nq:0.###}",Color.Crimson));
            target.Note=$"Stratigrafia {selected+1} · punta L={length:0.###} m · L/D={ratio:0.###}"+(detail.B("limite_phi")||detail.B("limite_rapporto")?" · valore limitato al bordo dell'abaco":"");
        }
        else target.Note=$"L/D effettivo = {ratio:0.###} · punto disponibile con dati completi fino alla punta";
        target.XMinimum=low;
        series.Add(new($"L/D effettivo = {ratio:0.###}",Enumerable.Range(0,201).Select(i=>{double phi=low+(high-low)*i/200;return new[]{phi,Nq.Dettaglio(phi,ratio,big).D("nq")};}).ToList(),Color.Crimson,Highlighted:true));
    }
}
