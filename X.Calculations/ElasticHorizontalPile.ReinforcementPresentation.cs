using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Immutable;

namespace Anthea.Calculations;
public static partial class ElasticHorizontalPile
{
    public static readonly ImmutableArray<string> OrdinaryApprovalKeys=["azioni_progetto","taglio_confermato","barre_trattenute","zone_estremita","aderenza_buona"];
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
    public static JsonObject ReinforcementSummary(JsonObject record)=>TypedReinforcementSummary(record);
    private static JsonObject TypedReinforcementSummary(JsonObject record)
    {
        var issues=record["da_completare_tipizzati"]?.Deserialize<PileIssue[]>();
        if(issues is null || issues.Any(i=>!Enum.IsDefined(i.Kind)))
            return J.Obj(("stato","VERIFICA INCOMPLETA"),("eseguiti_soddisfatti",false),("colore","ambra"),
                ("non_soddisfatti",new JsonArray()),("da_completare",new JsonArray("Ricalcolare per aggiornare gli esiti delle verifiche.")),("esclusi",new JsonArray()));
        var failures=issues.Where(i=>i.Kind==PileIssueKind.Failure).Select(i=>i.Summary).Distinct().ToArray();
        var pending=issues.Where(i=>i.Kind==PileIssueKind.Pending).Select(i=>i.Summary).Distinct().ToList();
        var excluded=issues.Where(i=>i.Kind==PileIssueKind.Excluded).Select(i=>i.Summary).Distinct().ToArray();
        if(record.Array("verifiche").Count==0&&pending.Count==0)pending.Add("Nessun risultato di sezione disponibile.");
        return J.Obj(("stato",failures.Length>0?"NON VERIFICATO":pending.Count>0?"VERIFICA INCOMPLETA":"CONTROLLI ESEGUITI SODDISFATTI"),
            ("eseguiti_soddisfatti",failures.Length==0&&pending.Count==0),("colore",failures.Length>0?"rosso":pending.Count>0?"ambra":"verde"),
            ("non_soddisfatti",J.Node(failures)),("da_completare",J.Node(pending.ToArray())),("esclusi",J.Node(excluded)));
    }

}
