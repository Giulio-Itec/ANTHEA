// Spostato da X.Calculations/ElasticHorizontalPile.ReinforcementPresentation.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: ramo
// legacy di ReinforcementSummary (G2), riepilogo ricavato dai testi usato solo dalle prove. Codice storico conservato per le prove, fuori dall'installer.
using System.Text.Json.Nodes;

namespace Anthea.Calculations;
public static partial class LegacyElasticHorizontalPile
{
    /// <summary>Explicit diagnostic comparison only; production summaries use typed issues (prima ElasticHorizontalPile.ReinforcementSummary
    /// con legacy: true).</summary>
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
