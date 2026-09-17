using System.Globalization;
using System.Text.Json.Nodes;

namespace X.Core;

public sealed record Tabella(string Titolo,string[] Colonne,List<string[]> Righe,string Categoria="dettagli");
public static class Tabelle
{
    public static string F(JsonNode? value)=>value is null?"—":J.Number(value) is double d?F(d):value.ToString();
    public static string F(double value)=>value.ToString("0.####",CultureInfo.GetCultureInfo("it-IT"));
    public static List<Tabella> Crea(JsonObject r,bool micro)
    {
        var tables=new List<Tabella>();if(r.S("errore")!="")return tables;
        string axis=micro?"s [m]":"z [m]";
        var details=r.Array("dettagli").Where(d=>Math.Abs(d.D("z")*2-Math.Round(d.D("z")*2))<1e-8||Math.Abs(d.D("z")-r.D("profondita_massima"))<1e-8).ToArray();
        string[] conditions=micro?["compressione"]:["drenante","non_drenante"];
        void Add(string title,string[] headers,IEnumerable<string[]> rows,string category="dettagli")=>tables.Add(new(title,headers,rows.ToList(),category));
        for(int i=0;i<(int)r.D("numero_stratigrafie");i++)
        {
            string prefix=$"Sondaggio {i+1} — ";
            if(micro)
            {
                Add(prefix+"aderenza per strato",[axis,"Strato","Da [m]","A [m]","Δl [m]","Aderenza","Tipo","α [−]","ds [m]","pi=pl [MPa]","s [kPa]","ΔRs [kN]"],
                    details.SelectMany(d=>d!.Array("sondaggi")[i]!.Array("tratti").Select(t=>new[]{F(d!["z"]),F(t?["strato"]),F(t?["cielo"]),F(t?["fondo"]),F(t.D("fondo")-t.D("cielo")),t.B("laterale_attiva",true)?"Attiva":"Esclusa",t.S("iniezione"),F(t?["alpha"]),F(t?["ds"]),F(t?["pl"]),F(t?["s"]),F(t?["laterale"])})));
                Add(prefix+"peso proprio assiale",[axis,"Gk [kN]"],details.Select(d=>new[]{F(d?["z"]),F(d?["peso"])}));
            }
            else
            {
                foreach(var group in new[]{new[]{"rapporto","rapporto_adottato","r1","r2","t"},new[]{"phi","phi1","phi2","n1","n2","nq"}})
                {
                    var headers=group[0]=="rapporto"?new[]{axis,"z/D","r adott.","r1","r2","t [-]"}:new[]{axis,"φ [°]","φ1 [°]","φ2 [°]","Nq1","Nq2","Nq adott."};
                    Add(prefix+"Nq — "+(group[0]=="rapporto"?"selezione curve":"valutazione curve"),headers,details.Where(d=>d!.Array("sondaggi")[i]?["dettaglio_nq"] is not null).Select(d=>new[]{F(d?["z"])}.Concat(group.Select(k=>F(d!.Array("sondaggi")[i]!["dettaglio_nq"]![k]))).ToArray()));
                }
                Add(prefix+"parametri alla punta",[axis,"σ′v [kPa]","σv totale [kPa]","φ′ [°]","Nq [-]","Nc [-]","Gk [kN]"],details.Select(d=>new[]{F(d?["z"])}.Concat(new[]{"sigma_punta","sigma_totale_punta","phi_punta","nq","nc"}.Select(k=>F(d!.Array("sondaggi")[i]?[k]))).Append(F(d?["peso"])).ToArray()));
                Add(prefix+"integrazione per strato",[axis,"Strato","Da [m]","A [m]","σ′media [kPa]","k [-]","μ [-]","α [-]"],details.SelectMany(d=>d!.Array("sondaggi")[i]!.Array("tratti").Select((t,j)=>new[]{F(d!["z"]),(j+1).ToString()}.Concat(new[]{"cielo","fondo","sigma_media","k","mu","alfa"}.Select(k=>F(t?[k]))).ToArray())));
                Add(prefix+"contributi laterali",[axis,"Strato","Laterale","τ dren. [kPa]","τ non dr. [kPa]","ΔRs dren. [kN]","ΔRs non dr. [kN]"],details.SelectMany(d=>d!.Array("sondaggi")[i]!.Array("tratti").Select((t,j)=>new[]{F(d!["z"]),(j+1).ToString(),t.B("laterale_attiva",true)?"Attiva":"Esclusa"}.Concat(new[]{"tau_d","tau_u","laterale_d","laterale_u"}.Select(k=>F(t?[k]))).ToArray())));
            }
            foreach(var c in conditions)Add(prefix+c.Replace('_',' ')+" — resistenze calcolate",[axis,"Rs,calc [kN]","Rb,calc [kN]","Rc,calc [kN]"],details.Select(d=>new[]{F(d?["z"]),F(d!.Array("sondaggi")[i]?[c]?["laterale"]),F(d.Array("sondaggi")[i]?[c]?["base"]),F(d.Array("sondaggi")[i]?[c].D("laterale")+d.Array("sondaggi")[i]?[c].D("base")??0)}));
        }
        foreach(var c in conditions)foreach(var branch in new[]{"Minimo","Media"})Add(c.Replace('_',' ')+" — "+branch+" — componenti",[axis,"L calc [kN]","P calc [kN]","L k [kN]","P k [kN]","L d [kN]","P d [kN]"],details.Select(d=>new[]{F(d?["z"])}.Concat(new[]{"calc","k","d"}.SelectMany(k=>d!["componenti"]![c]![branch]!.Array(k).Select(F))).ToArray()));
        foreach(var (key,curve) in r["curve"]!.AsObject())
        {
            var branchMaps=new[]{"media","minima","progetto"}.ToDictionary(k=>k,k=>curve!.Array(k).ToDictionary(p=>p![0]!.GetValue<double>(),p=>p![1]!.GetValue<double>()));
            var actions=r["azioni"]!.Array(key.Contains("trazione")?"trazione":"compressione").ToDictionary(p=>p![0]!.GetValue<double>(),p=>p![1]!.GetValue<double>());
            Add(key.Replace('_',' ')+" — verifica a ogni quota",[axis,"Rd media [kN]","Rd minimo [kN]","Rd [kN]","Ed [kN]","Ed/Rd [-]"],details.Select(d=>
            {
                double z=d.D("z"),rd=branchMaps["progetto"][z];bool present=actions.TryGetValue(z,out double ed);
                return new[]{F(z),F(branchMaps["media"][z]),F(branchMaps["minima"][z]),F(rd),present?F(ed):"—",present&&rd>0?F(ed/rd):"—"};
            }));
        }
        return tables;
    }
    public static Tabella Parametri(string titolo,JsonObject data,string category)=>new(titolo,["Parametro","Valore"],data.Select(p=>new[]{p.Key,F(p.Value)}).ToList(),category);
}
