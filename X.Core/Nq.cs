using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>NQ-2026-09-09: trascrizione di programma/nq.py. Phi in gradi; nessuna estrapolazione.</summary>
public static class Nq
{
    public const string Versione = "NQ-2026-09-09";
    public const string Descrizione = "Parametrizzata NQ-2026-09-09: D ≤ 0,80 m, Nq = 10^(a+b·φ), b=1/(φ100-φ10), a=1-b·φ10. D > 0,80 m: Nq* da cubiche raccordate a 34° e 38°, adattate alla figura fornita. φ in gradi, senza riduzioni. Per una quota z si usa z/D; alla punta finale z=L. Tra le curve: t=ln(r/r1)/ln(r2/r1), interpolazione geometrica di Nq per i pali medi e aritmetica di Nq* per i grandi. Questa regola tra L/D è una scelta numerica mantenuta dal metodo precedente, non desunta dalla figura. Fuori dagli intervalli visibili si usa il bordo, con segnalazione; i valori limitati non costituiscono una validazione fuori campo.";
    public static readonly double[] RapportiMedio = [5, 10, 20, 50];
    public static readonly double[] RapportiGrande = [4, 32];
    private static readonly (double P10, double P100, double Low, double High)[] Medio = [(23,35.6,23.4,38.8),(24.6,37,23.6,40),(25.8,37.8,23.6,41),(27.5,38.8,24.8,41.6)];
    private static readonly double[][] Coefficienti = [
        [27.53212302495899,3.245021450080038,0.3465310545007305,0.01982055661210097,-0.011218279222357186,-0.10608183901207004],
        [23.394960047407167,2.9357818054550844,0.2768404988616615,0.017268449836249766,0.001955243085702829,-0.12061595971538405]];
    public static double Curva(double phi, int index, bool grande)
    {
        if (!grande) { var p = Medio[index]; return Math.Pow(10, 1 + (phi - p.P10) / (p.P100 - p.P10)); }
        var c = Coefficienti[index]; double u = phi - 34;
        return ((c[3]*u+c[2])*u+c[1])*u+c[0]+c[4]*Math.Pow(Math.Max(u,0),3)+c[5]*Math.Pow(Math.Max(u-4,0),3);
    }
    public static JsonObject Dettaglio(double phi, double rapporto, bool grande)
    {
        if (!double.IsFinite(phi) || !double.IsFinite(rapporto) || rapporto <= 0) throw new ArgumentException("Nq: φ e z/D devono essere finiti, z/D positivo.");
        var rr = grande ? RapportiGrande : RapportiMedio;
        double r = Math.Clamp(rapporto, rr[0], rr[^1]);
        int i = Array.IndexOf(rr,r), j;
        if (i >= 0) j = i;
        else { i = Enumerable.Range(0,rr.Length-1).First(k => rr[k] < r && r < rr[k+1]); j = i+1; }
        double p1 = Math.Clamp(phi, grande ? 26 : Medio[i].Low, grande ? 42 : Medio[i].High);
        double p2 = Math.Clamp(phi, grande ? 26 : Medio[j].Low, grande ? 42 : Medio[j].High);
        double n1 = Curva(p1,i,grande), n2 = Curva(p2,j,grande), t = i == j ? 0 : Math.Log(r/rr[i])/Math.Log(rr[j]/rr[i]);
        double nq = i == j ? n1 : grande ? n1+t*(n2-n1) : Math.Exp(Math.Log(n1)+t*(Math.Log(n2)-Math.Log(n1)));
        return J.Obj(("versione",Versione),("fattore",grande ? "Nq*" : "Nq"),("phi",phi),("rapporto",rapporto),("rapporto_adottato",r),("r1",rr[i]),("r2",rr[j]),("phi1",p1),("phi2",p2),("n1",n1),("n2",n2),("t",t),("nq",nq),("limite_phi",p1!=phi || p2!=phi),("limite_rapporto",r!=rapporto));
    }
    public static string MetodoPrecedente(JsonNode? g)
    {
        var method = g.S("metodo_nq","Originale");
        if (method is not ("Originale" or "Raffinata" or "Parametrizzata")) throw new ArgumentException("Metodo Nq non riconosciuto nel documento.");
        return method == "Parametrizzata" ? g.S("metodo_nq_precedente") : method;
    }
}
