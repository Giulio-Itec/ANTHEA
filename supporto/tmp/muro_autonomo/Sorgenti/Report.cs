using System.Net;
using System.Text;

namespace Anthea.Muro;

public static class Report
{
    public static readonly (string,string)[] MethodSections=[
        ("Campo di applicazione",Engine.Scope),
        ("Combinazioni",Engine.Factors),
        ("Spinte e pesi","Rankine: Ka = tan²(45° − φ′/2). Equilibrio esterno sul blocco muro + terreno sopra la mensola di monte. Spinta delle terre = ½ Ka γ (H+t)², quota (H+t)/3; sovraccarico = Ka q (H+t), quota (H+t)/2. Peso del terreno sul tallone = γ H b; carico variabile sul tallone = q b. Il piano virtuale di spinta è al bordo di monte fino al piano di posa. Il fusto riceve la spinta sulla sola altezza H."),
        ("Equilibrio e contatto","Origine al piede di valle. x = (ΣW·x − ΣP·z)/N; e = B/2 − x. Scorrimento: Rd = N tan δb / 1,10. Ribaltamento: Rd = Mstabilizzante / 1,15. Contatto intero per |e| ≤ B/6; altrimenti distribuzione triangolare di compressione con lunghezza 3·min(x,B−x). Se x è esterno alla base, il contatto è assente e la portanza è nulla. pmax non è confrontata direttamente con la portanza: la verifica utilizza N e la resistenza sulla base efficace."),
        ("Capacità portante drenata","Fondazione nastriforme senza incasso, terreno granulare, base ruvida (δb ≥ φ′f/2). B′ = B − 2|e|; Nq = exp(π tan φ′f) tan²(45° + φ′f/2); Nγ = 2(Nq−1) tan φ′f; iγ = max(0,1−H/N)³. Rd = ½ γf B′² Nγ iγ / 1,40. Non si utilizzano contributi di coesione o sovraccarico di confinamento. Per |e| > B/3 l’esito è fuori campo: sono necessarie precauzioni e un’analisi specifica."),
        ("Sollecitazioni per il c.a.","Fusto: M = Ka γ H³/6 + Ka q H²/2, V = Ka γ H²/2 + Ka q H, con i coefficienti della combinazione. Mensole: integrazione esatta a tratti della pressione di contatto, sottraendo il peso della soletta e, a monte, peso del terreno e sovraccarico. M positivo nelle mensole tende il lembo inferiore. Nessuna verifica di resistenza strutturale è eseguita; i valori di taglio sono al filo del fusto."),
        ("Verifiche da completare",Engine.Exclusions),
        ("Fonti","D.M. 17/01/2018, NTC 2018: §§ 6.2.4.1 e 6.5.3.1.1, tabelle 6.2.I, 6.2.II e 6.5.I. Testo ufficiale: https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg\nJRC, Eurocode 7: Geotechnical Design – Worked examples (2013), capitoli 3 e 4, formule di portanza e verifica delle opere di sostegno: https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/2013_06_WS_GEO.pdf\nModello limitato ai casi descritti; i riferimenti non costituiscono dichiarazione di conformità completa alle NTC.")
    ];
    public static string Html(Result r)
    {
        string E(string x)=>WebUtility.HtmlEncode(x);
        string F(double x)=>MainWindow.F(x,3);
        var sb=new StringBuilder("<!doctype html><html lang='it'><meta charset='utf-8'><title>ANTHEA · Relazione muro</title><style>body{font:14px Segoe UI,Arial;color:#0b2a4a;max-width:1050px;margin:35px auto;padding:0 20px}h1{border-bottom:3px solid #0b2a4a;padding-bottom:15px}h2{margin-top:30px}table{border-collapse:collapse;width:100%;margin:15px 0;font-size:12px}th,td{padding:7px;border:1px solid #dce2e9;text-align:right}th:first-child,td:first-child{text-align:left}th{background:#e8eff7}.note{padding:14px;background:#fff4dc}p{line-height:1.5;white-space:pre-line}@media print{body{margin:0;font-size:10pt}h2{break-after:avoid}tr{break-inside:avoid}thead{display:table-header-group}}</style><h1>ANTHEA · Muro di sostegno</h1>");
        sb.Append($"<h2>{E(r.Input.Title)}</h2><p>Calcolo statico per metro di muro. Esportazione: {DateTime.Now:dd/MM/yyyy HH:mm}</p><p class='note'>{E(Engine.Exclusions)}</p>");
        void Table(string[] heads,IEnumerable<string[]> rows){sb.Append("<table><thead><tr>");foreach(var h in heads)sb.Append($"<th>{E(h)}</th>");sb.Append("</tr></thead><tbody>");foreach(var row in rows){sb.Append("<tr>");foreach(var cell in row)sb.Append($"<td>{E(cell)}</td>");sb.Append("</tr>");}sb.Append("</tbody></table>");}
        sb.Append("<h2>Dati di ingresso</h2>");Table(["Parametro","Simbolo","Valore","Unità"],Input.Fields.Select(f=>new[]{f.Label,f.Symbol,r.Input.Values[f.Key],f.Unit}));
        sb.Append($"<p>B = {F(r.Width)} m; Ka = {F(r.Ka)}; Nq = {F(r.Nq)}; Nγ = {F(r.Ngamma)}.</p><h2>Inviluppo delle verifiche locali</h2>");
        Table(["Verifica","Caso","Ed","Rd","Unità","Ed/Rd","Esito"],r.Checks.Select(c=>new[]{c.Name,c.Case,F(c.Demand),F(c.Resistance),c.Unit,c.Ratio is double n?F(n):"n.d.",c.Status}));
        foreach(var n in r.Notes)sb.Append($"<p class='note'>{E(n)}</p>");
        sb.Append("<h2>Combinazioni di carico</h2>");
        Table(["Caso","γG,c","γG,t","γQ","H kN/m","N kN/m","Mstab kNm/m","Mrib kNm/m","e m","B′ m"],r.Cases.Select(c=>new[]{c.Name,F(c.ConcreteFactor),F(c.SoilFactor),F(c.LiveFactor),F(c.Horizontal),F(c.Vertical),F(c.Stabilizing),F(c.Overturning),F(c.Eccentricity),F(c.EffectiveWidth)}));
        sb.Append("<h2>Contatto e resistenze</h2>");
        Table(["Caso","Lc m","p valle kPa","p monte kPa","pmax kPa","Rd scorr. kN/m","Rd rib. kNm/m","Rd port. kN/m"],r.Cases.Append(r.Characteristic).Select(c=>new[]{c.Name,F(c.ContactWidth),c.ContactWidth>0?F(c.PressureToe):"n.d.",c.ContactWidth>0?F(c.PressureHeel):"n.d.",c.ContactWidth>0?F(c.MaxPressure):"n.d.",F(c.SlidingResistance),F(c.OverturningResistance),F(c.BearingResistance)}));
        sb.Append("<p>Nella riga caratteristica tutti i coefficienti, comprese le resistenze, sono unitari: i valori non sono resistenze di progetto.</p><h2>Sollecitazioni agli incastri</h2>");
        Table(["Caso","M fusto","V fusto","M valle","V valle","M monte","V monte"],r.Cases.Select(c=>new[]{c.Name,F(c.StemMoment),F(c.StemShear),c.ContactWidth>0?F(c.ToeMoment):"n.d.",c.ContactWidth>0?F(c.ToeShear):"n.d.",c.ContactWidth>0?F(c.HeelMoment):"n.d.",c.ContactWidth>0?F(c.HeelShear):"n.d."}));
        sb.Append("<p>Momenti in kNm/m; tagli in kN/m. M fusto positivo tende monte. M mensole positivo tende il lembo inferiore. Verifiche del c.a. non eseguite.</p>");
        foreach(var (head,body) in MethodSections)sb.Append($"<h2>{E(head)}</h2><p>{E(body)}</p>");
        return sb.Append("</html>").ToString();
    }
}
