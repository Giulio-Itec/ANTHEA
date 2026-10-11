using System.Text.Json;

namespace ANTHEA.ModelWorkspace;

public sealed record GraphicalLoad(string Case,int Node,int? Element,double Station,ModelDirection Vector,string Unit,bool Moment=false);
public sealed record GraphicalLoadData(GraphicalLoad[] Loads,IReadOnlyDictionary<string,int> Unsupported);

/// <summary>Read-only glyph data for explicit nodal and uniform loads. No equivalent forces or combinations.</summary>
public static class MidasGraphicalLoads
{
    public static GraphicalLoadData Read(ModelSnapshot model)
    {
        var loads=new List<GraphicalLoad>();var skipped=new Dictionary<string,int>();
        var nodes=model.Nodes.Select(n=>n.Id).ToHashSet();var elements=model.Elements.ToDictionary(e=>e.Id);
        var frames=(model.LocalFrames??[]).ToDictionary(f=>f.Element);
        var unit=model.SourceTables?.GetValueOrDefault("UNIT");
        var row=unit is {ValueKind:JsonValueKind.Object} tableUnit?tableUnit.EnumerateObject().FirstOrDefault().Value:default;
        string force=row is {} u&&u.ValueKind==JsonValueKind.Object&&u.TryGetProperty("FORCE",out var f)?f.ValueKind==JsonValueKind.String?f.GetString()??"":"":"";
        string length=row is {} d&&d.ValueKind==JsonValueKind.Object&&d.TryGetProperty("DIST",out var l)?l.ValueKind==JsonValueKind.String?l.GetString()??"":"":"";
        double k=force.ToUpperInvariant() switch {"KN"=>1,"N"=>.001,"MN"=>1000,_=>double.NaN};
        double m=length.ToUpperInvariant() switch {"M"=>1,"MM"=>.001,"CM"=>.01,_=>double.NaN};
        static string Text(JsonElement r,string key)=>r.ValueKind==JsonValueKind.Object&&r.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
        static bool Flag(JsonElement r,string key)=>r.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.True;
        static double Value(JsonElement r,string key)=>r.TryGetProperty(key,out var value)?value.GetDouble():0;
        ModelDirection? Direction(string name,int id)=>name switch
        {
            "GX"=>new(1,0,0),"GY"=>new(0,1,0),"GZ"=>new(0,0,1),
            "LX" when frames.ContainsKey(id)=>frames[id].X,"LY" when frames.ContainsKey(id)=>frames[id].Y,"LZ" when frames.ContainsKey(id)=>frames[id].Z,_=>null
        };
        foreach(string table in new[]{"CNLD","BMLD","PRES"})
        {
            if(model.SourceTables?.TryGetValue(table,out var data)!=true||data.ValueKind!=JsonValueKind.Object)continue;
            foreach(var record in data.EnumerateObject())
            {
                if(!int.TryParse(record.Name,out int id)||record.Value.ValueKind!=JsonValueKind.Object||!record.Value.TryGetProperty("ITEMS",out var items)||items.ValueKind!=JsonValueKind.Array)continue;
                foreach(var item in items.EnumerateArray())
                {
                    string loadCase=Text(item,"LCNAME");
                    void Skip()=>skipped[loadCase]=skipped.GetValueOrDefault(loadCase)+1;
                    if(item.ValueKind!=JsonValueKind.Object||!double.IsFinite(k)||!double.IsFinite(m)) {Skip();continue;}
                    try
                    {
                        if(table=="CNLD"&&nodes.Contains(id))
                        {
                            foreach(var moment in new[]{false,true})
                            {
                                string prefix=moment?"M":"F";double factor=k*(moment?m:1);
                                var v=new ModelDirection(Value(item,prefix+"X")*factor,Value(item,prefix+"Y")*factor,Value(item,prefix+"Z")*factor);
                                if(!double.IsFinite(v.X)||!double.IsFinite(v.Y)||!double.IsFinite(v.Z)) {Skip();continue;}
                                if(v.X!=0||v.Y!=0||v.Z!=0)loads.Add(new(loadCase,id,null,0,v,moment?"kN·m":"kN",moment));
                            }
                            continue;
                        }
                        if(!elements.TryGetValue(id,out var element)||Direction(Text(item,"DIRECTION"),id) is not {} direction) {Skip();continue;}
                        double magnitude;string symbol;
                        if(table=="BMLD"&&ElementFamilies.IsLine(element.Type)&&Text(item,"TYPE")=="UNILOAD"&&!Flag(item,"USE_PROJECTION")&&!Flag(item,"USE_ECCEN")&&!Flag(item,"USE_ADDITIONAL"))
                        {
                            var distances=item.GetProperty("D");var intensities=item.GetProperty("P");
                            if(distances[0].GetDouble()!=0||distances[1].GetDouble()!=1||intensities[0].GetDouble()!=intensities[1].GetDouble()) {Skip();continue;}
                            magnitude=intensities[0].GetDouble()*k/m;symbol="kN/m";
                            if(!double.IsFinite(magnitude)){Skip();continue;}
                            for(int station=0;station<=4;station++) loads.Add(new(loadCase,0,id,station/4.0,new(direction.X*magnitude,direction.Y*magnitude,direction.Z*magnitude),symbol));
                        }
                        else if(table=="PRES"&&element.Type=="PLATE"&&Text(item,"CMD")=="PRES"&&Text(item,"FACE_EDGE_TYPE")=="FACE")
                        {
                            var forces=item.GetProperty("FORCES");
                            if(forces.ValueKind!=JsonValueKind.Array||forces.GetArrayLength()==0||forces.EnumerateArray().Skip(1).Any(x=>x.GetDouble()!=0)) {Skip();continue;}
                            magnitude=forces[0].GetDouble()*k/(m*m);symbol="kN/m²";
                            if(!double.IsFinite(magnitude)){Skip();continue;}
                            loads.Add(new(loadCase,0,id,.5,new(direction.X*magnitude,direction.Y*magnitude,direction.Z*magnitude),symbol));
                        }
                        else Skip();
                    }
                    catch(Exception ex) when(ex is InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or FormatException) {Skip();}
                }
            }
        }
        return new(loads.ToArray(),skipped);
    }
}
