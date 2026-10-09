using System.Text.Json.Nodes;
namespace Anthea.Calculations;
public static partial class ElasticHorizontalPile
{
    /// <summary>Independent inspection sheet, with persisted user load combinations. Common pile geometry is refreshed on opening.</summary>
    public static JsonObject CreateConcreteSheet(JsonObject root,JsonObject? result,int index)
    {
        if(root.S("tipo_sezione")=="CHS")throw new ArgumentException("Verificatore c.a. non applicabile a CHS.");
        var row=root["elastico"]!.Array("tratti")[index]!.AsObject();
        var sheet=row["foglio_cls"] is JsonObject saved?(JsonObject)saved.DeepClone():new JsonObject();
        sheet["input"]=SegmentSection(root,row);sheet["sorgente_palo"]=J.Obj(("tratto",row.S("id")),("nota","Scheda di approfondimento: azioni proprie modificabili; nessuna modifica ai carichi FEM. Geometria e materiali aggiornati dal palo all'apertura."));
        if(sheet["workspace_ca"] is not JsonObject)sheet["workspace_ca"]=J.Obj(("versione",2),("normativa","NTC 2018"),("convenzione","N negativo a compressione"));
        if(sheet["combinazioni"]==null)
        {
            var action=result?["armature"]?.Array("tratti").FirstOrDefault(s=>s.S("id")==row.S("id"))?["critica"]?["Action"];
            sheet["combinazioni"]=J.Obj(("SLU",new JsonArray(J.Obj(("nome",action==null?"Azioni da assegnare":$"Critica del palo · x={action.D("Depth"):G5} m"),("azioni",new[]{-action.D("N"),action.D("M"),0d})))));
            // Shear loads are editable in the native sheet. The transverse axes are those used by the existing section model.
            sheet["sorgente_palo"]!["azioni_concomitanti"]=action?.DeepClone();
            sheet["workspace_ca"]!["taglio"]=J.Obj(("azioni",new JsonArray(J.Obj(("id",Guid.NewGuid().ToString("N")),("nome","Critica concomitante del palo"),("N",-action.D("N")),("Mx",action.D("M")),("My",0),("Vx",0),("Vy",action.D("V")),("T",0)))));
        }
        sheet.Remove("risultati");sheet.Remove("output");SectionWorkspace.Prepare(sheet);
        var settings=sheet["workspace_ca"]!.AsObject();
        if(root["sezione"].B("coefficienti_unitari")){var custom=ConcreteStandards.PileWorkspace(root["sezione"]!.AsObject());settings["coefficienti"]=custom["coefficienti"]!.DeepClone();settings["normativa_custom"]=custom["normativa_custom"]!.DeepClone();}
        else if(settings.S("normativa_custom").Contains("coefficienti unitari")){settings["coefficienti"]=ConcreteStandards.Defaults("NTC 2018");settings.Remove("normativa_custom");}
        settings["sle_comuni"]!["esposizione"]=root["sezione"]!["esposizione"]?.DeepClone()??JsonValue.Create("Da scegliere");
        settings["dettagli_costruttivi"]!["elemento"]=root["elastico"]!["dettagli"].S("elemento","Pilastro") is "Trave"?"Trave":"Pilastro";
        settings["dettagli_costruttivi"]!["vita_durabilita"]=root["sezione"].S("vita_durabilita","50");
        settings["dettagli_costruttivi"]!["qualita_copriferro"]=root["sezione"].B("qualita_copriferro")?"Sì":"No";
        return sheet;
    }
    public static void ApplyConcreteSheetReinforcement(JsonObject root,JsonObject sheet,int index)
    {
        var input=sheet["input"]!;foreach(string key in ReinforcementKeys)if(J.Number(input[key]) is not double n||n<=0)throw new ArgumentException("Armature della scheda non valide: "+key);
        // Second ring of the sheet: same limits as the c.a. section (at least four bars, positive diameter and clear distance).
        if(input.B("second_inner_enabled"))
        {
            if(J.Number(input["second_inner_count"]) is not double count||count<4||count!=Math.Truncate(count))throw new ArgumentException("Secondo anello interno: almeno quattro barre, in numero intero.");
            foreach(string key in new[]{"second_inner_diameter","second_inner_gap"})if(J.Number(input[key]) is not double v||v<=0)throw new ArgumentException("Armature della scheda non valide: "+key);
        }
        var row=root["elastico"]!.Array("tratti")[index]!.AsObject();var target=index==0?root["sezione"]!.AsObject():row;
        foreach(string key in ReinforcementKeys)target[key]=input[key]!.DeepClone();if(index>0)row["collegato"]=false;
        // A pile without a second ring keeps its archive unchanged; otherwise the sheet decides whether the ring is active.
        if(input["second_inner_enabled"]!=null||SegmentSection(root,row)["second_inner_enabled"]!=null)
            foreach(string key in InnerRingKeys){if(key=="second_inner_enabled")target[key]=input.B(key);else if(input[key] is JsonNode value)target[key]=value.DeepClone();}
        row["origine_armatura"]="Armatura applicata esplicitamente dal foglio c.a.; geometria, materiali e azioni del foglio non trasferiti.";
    }
}
