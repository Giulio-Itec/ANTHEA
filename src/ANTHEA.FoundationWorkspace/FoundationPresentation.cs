using System.Globalization;
using System.Text.Json.Nodes;

namespace Anthea.FoundationWorkspace;

/// <summary>Display units, visibility and report text; no engineering formula is evaluated here.</summary>
public static class FoundationPresentation
{
    static readonly CultureInfo Italian=CultureInfo.GetCultureInfo("it-IT");
    public static string Value(JsonNode? node,double scale=1,string format="0.###") => node is null ? "—" : (FoundationDocument.Number(node,"Valore")*scale).ToString(format,Italian);
    public static bool Visible(string key,JsonObject input)
    {
        int method=input["Method"]!.GetValue<int>(),shape=input["Shape"]!.GetValue<int>();
        bool drained=input["Condition"]!.GetValue<int>()==0,water=input["WaterEnabled"]!.GetValue<bool>();
        return key switch {
            "Length" => shape==0,
            "FrictionAngle" or "Cohesion" or "Compression" => drained,
            "UndrainedStrength" => !drained,
            "WaterDepth" or "WaterUnitWeight" or "SaturatedUnitWeight" or "CoverSaturatedUnitWeight" => water,
            "RelativeDensity" => drained && input["Compression"]!.GetValue<int>()==2,
            "MomentX" or "HorizontalY" => shape==0,
            "MomentY" => shape!=2,
            "IncludeDepthFactors" => method is 2 or 3 or 4,
            "RoughBase" => method==1 || method==0 && drained,
            _ => true
        };
    }
    public static IReadOnlyList<(string Label,string Value)> Summary(JsonObject r) => [
        ("Esito",r["Status"]!.ToString()),
        ("Domanda N [kN]",Value(r["dati"]?["input"]?["Normal"])),
        ("Resistenza R [kN]",Value(r["Resistance"],.001)),
        ("Indice N/R",Value(r["Utilization"],1,"0.000")),
        ("Combinazione esaminata",r["dati"]?["nome_combinazione"]?.ToString() ?? ""),
        ("B′ × L′ [m]",Value(r["EffectiveWidth"],.001)+" × "+Value(r["EffectiveLength"],.001)),
        ("Area efficace [m²]",Value(r["EffectiveArea"],1e-6)),
        ("ex / ey [m]",Value(r["EccentricityX"],.001)+" / "+Value(r["EccentricityY"],.001)),
        ("Pressione agente [kPa]",Value(r["DemandPressure"],1000)),
        ("Pressione limite lorda [kPa]",Value(r["UltimatePressure"],1000)),
        ("Pressione limite netta [kPa]",Value(r["NetUltimatePressure"],1000)),
        ("Pressione resistente [kPa]",Value(r["DesignPressure"],1000)),
        ("γR",Value(r["ResistanceFactor"])),
        ("q al piano posa [kPa] · efficace drenato / totale non drenato",Value(r["Overburden"],1000)),
        ("γ medio sotto la base [kN/m³]",Value(r["BearingUnitWeight"],1e6)),
        ("φ adottato [°]",Value(r["AdoptedFrictionAngle"],180/Math.PI)),
        ("c / cu adottato [kPa]",Value(r["AdoptedCohesion"],1000))
    ];
    public static string[] ReportLines(JsonObject r)
    {
        var lines=new List<string> { "Capacità portante · fondazioni superficiali",r["Profile"]!.ToString(),r["Reference"]!.ToString(),
            "Unica combinazione esaminata; nessun inviluppo automatico delle combinazioni." };
        lines.AddRange(Summary(r).Select(x=>x.Label+": "+x.Value));
        lines.Add("Coefficienti e contributi");
        foreach(string key in new[]{"Cohesion","Surcharge","Weight"})
        {
            var t=r[key]!;lines.Add($"{(key=="Cohesion"?"Coesione":key=="Surcharge"?"Sovraccarico":"Peso terreno")}: N={Value(t["N"])}, s={Value(t["Shape"])}, d={Value(t["Depth"])}, i={Value(t["Inclination"])}, contributo={Value(t["Pressure"],1000)} kPa");
        }
        lines.Add("Hansen non drenato: s, d e i sono incrementi additivi; il contributo non è il loro prodotto.");
        lines.Add("Dati al piano di posa già combinati e comprensivi dei pesi. Drenato: N efficace al netto della sottospinta; non drenato: N totale senza sottrarre la pressione interstiziale alla base. q non viene aggiunto nuovamente a N.");
        var data=r["dati"]!.AsObject();var input=data["input"]!.AsObject();
        bool commonSoils=data["stratigrafie"] is not null;
        if(commonSoils){input=input.DeepClone().AsObject();input["WaterEnabled"]=data["generali"]!["presenza_falda"]!.DeepClone();}
        lines.Add("Dati di ingresso");
        foreach(var f in FoundationDocument.Fields.Where(f=>Visible(f.Key,input)))
            if(!commonSoils||!FoundationMaterials.LegacySoilKeys.Contains(f.Key) && f.Key!="WaterDepth")lines.Add(f.Label+": "+input[f.Key]);
        if(commonSoils)
        {
            var profile=FoundationMaterials.Profile(data)!;lines.Add("Profilo esaminato: "+profile.Name);
            int index=0;foreach(var layer in profile.Layers)
            {
                string D(double n)=>n.ToString("0.###",Italian);var soil=layer.Soil;
                lines.Add($"Strato {++index} · {soil.Name}: z={D(-layer.Top/1000)}–{D(-layer.Bottom/1000)} m; γ={D(soil.UnitWeight*1e6)}, γsat={D(soil.SaturatedUnitWeight*1e6)} kN/m³; φ′={D(soil.FrictionAngle*180/Math.PI)}°; c′={D(soil.EffectiveCohesion*1000)} kPa; cu={(soil.UndrainedShearStrength.HasValue?D(soil.UndrainedShearStrength.Value*1000):"non assegnato")} kPa.");
            }
            lines.Add("Falda: "+(profile.GroundwaterElevation.HasValue?(-profile.GroundwaterElevation.Value/1000).ToString("0.###",Italian)+" m dal piano campagna":"assente"));
            lines.Add("Ipotesi: terreno omogeneo sotto il piano di posa, prolungato come semispazio. Nessuna portanza multistrato o media delle resistenze.");
            lines.Add("Calcestruzzo: "+data["sezione"]?["classe_cls"]+"; fck="+data["sezione"]?["fck_mpa"]+" MPa; "+data["sezione"]?["cls_diagramma"]+". Dato descrittivo condiviso nel progetto; resistenza strutturale e peso proprio automatico non calcolati.");
        }
        lines.Add("Impostazioni: "+FoundationDocument.Shapes[input["Shape"]!.GetValue<int>()]+"; "+FoundationDocument.Conditions[input["Condition"]!.GetValue<int>()]+"; "+FoundationDocument.Compressions[input["Compression"]!.GetValue<int>()]);
        foreach(var (key,label) in new[]{("WaterEnabled","Falda"),("RoughBase","Base ruvida"),("IncludeDepthFactors","Fattori di profondità")})
            if(Visible(key,input) && !(commonSoils&&key=="WaterEnabled"))lines.Add(label+": "+(input[key]!.GetValue<bool>()?"sì":"no"));
        if(r["Graphics"] is JsonObject graphics)
        {
            lines.Add("Pressioni sotto la fondazione: pmin="+Value(graphics["MinimumPressure"],1000)+" kPa; pmax="+Value(graphics["MaximumPressure"],1000)+" kPa; area compressa="+Value(graphics["CompressedArea"],1e-6)+" m². Area compressa e area efficace A′ sono distinte.");
            lines.Add(graphics["ContactDescription"]!.ToString());lines.Add(graphics["MechanismDescription"]!.ToString());
        }
        lines.AddRange(r["Warnings"]!.AsArray().Select(v=>v!.ToString()));
        lines.Add("Libreria GPCChecker.Geotechnics "+r["library_version"]);
        return lines.ToArray();
    }
}
