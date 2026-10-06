using System.Text.Json.Nodes;

namespace Anthea.Calculations;
public static partial class ElasticHorizontalPile
{
    public static readonly string[] OrdinaryApprovalKeys=["azioni_progetto","taglio_confermato","barre_trattenute","zone_estremita","aderenza_buona"];
    public static void ApproveReinforcementAssumptions(JsonObject root,bool seismic)
    {
        PrepareReinforcement(root);var d=root["elastico"]!["dettagli"]!;
        foreach(string key in OrdinaryApprovalKeys)d[key]=true;
        if(seismic){d["sisma_azioni"]=true;d["sisma_elastico"]=true;}
        // Confirmation never changes reinforcement, numerical constraints or calculated verdicts.
        d[seismic?"approvazione_sismica":"approvazione_ipotesi"]="Approva tutto: conferma esplicita dell'utente";
    }
    public static string TransverseKind(JsonObject root,JsonNode? segment=null)
    {
        string? local=segment!=null&&!segment.B("collegato")?segment.S("tipo_trasversale"):null;
        string adopted=string.IsNullOrWhiteSpace(local)?root["elastico"]!["dettagli"].S("sisma_staffe","Staffe singole"):local;
        return adopted is "Staffe singole" or "Spirale" or "Da definire"?adopted:throw new ArgumentException("Tipo di armatura trasversale non riconosciuto.");
    }
    public static JsonObject ReinforcementSummary(JsonObject record)
    {
        var failures=new List<string>();var pending=new List<string>();var excluded=new List<string>();
        foreach(string issue in record.Array("da_completare").Select(v=>v!.ToString()))
        {
            if(issue.StartsWith("Non eseguiti:"))excluded.Add(issue[13..].Trim());
            else if(issue.StartsWith("NON SODDISFATTO:"))failures.Add(issue["NON SODDISFATTO:".Length..].Trim());
            else if(issue.StartsWith("Sviluppo barre insufficiente")||issue.StartsWith("Giunti non utilizzabili"))failures.Add(issue);
            else pending.Add(issue);
        }
        if(record.Array("verifiche").Count==0&&!pending.Any())pending.Add("Nessun risultato di sezione disponibile.");
        bool all=failures.Count==0&&pending.Count==0;
        return J.Obj(("stato",failures.Count>0?"NON VERIFICATO":pending.Count>0?"VERIFICA INCOMPLETA":"CONTROLLI ESEGUITI SODDISFATTI"),("eseguiti_soddisfatti",all),("colore",failures.Count>0?"rosso":pending.Count>0?"ambra":"verde"),("non_soddisfatti",J.Node(failures.Distinct().ToArray())),("da_completare",J.Node(pending.Distinct().ToArray())),("esclusi",J.Node(excluded.Distinct().ToArray())));
    }
}
