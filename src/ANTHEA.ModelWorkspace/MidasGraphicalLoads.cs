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
        // Pressure direction «VECTOR»: the unit vector of VECTORS, as in the GPC converter (CivilNxModelProfile.Pressures).
        static ModelDirection? Vector(JsonElement r)
        {
            if(!r.TryGetProperty("VECTORS",out var values)||values.ValueKind!=JsonValueKind.Array||values.GetArrayLength()!=3)return null;
            var v=new GPC.Geometry.Vector3d(values[0].GetDouble(),values[1].GetDouble(),values[2].GetDouble());double n=v.Length;
            return n>1e-12&&double.IsFinite(n)?new(v.X/n,v.Y/n,v.Z/n):null;
        }
        // Uniform pressure in the two layouts the GPC converter reads as uniform: [P,0,0,0,0] and [0,P1..P4] with equal corner values
        // (P4 unused on triangles). A varying pressure has no single glyph and is counted as not represented.
        static double? Uniform(JsonElement forces,int corners)
        {
            if(forces.ValueKind!=JsonValueKind.Array||forces.GetArrayLength()==0)return null;
            double[] f=forces.EnumerateArray().Select(x=>x.GetDouble()).ToArray();
            if(f.Skip(1).All(x=>x==0))return f[0];
            if(f.Length!=5||f[0]!=0)return null;
            var values=f.Skip(1).Take(corners).ToArray();
            return values.All(x=>x==values[0])&&(corners==4||f[4]==0||f[4]==values[0])?values[0]:null;
        }
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
                        if(!elements.TryGetValue(id,out var element)||(table=="PRES"&&Text(item,"DIRECTION")=="VECTOR"?Vector(item):Direction(Text(item,"DIRECTION"),id)) is not {} direction) {Skip();continue;}
                        double magnitude;string symbol;
                        if(table=="BMLD"&&ElementFamilies.IsLine(element.Type)&&Text(item,"TYPE")=="UNILOAD"&&!Flag(item,"USE_PROJECTION")&&!Flag(item,"USE_ECCEN")&&!Flag(item,"USE_ADDITIONAL"))
                        {
                            var distances=item.GetProperty("D");var intensities=item.GetProperty("P");
                            if(distances[0].GetDouble()!=0||distances[1].GetDouble()!=1||intensities[0].GetDouble()!=intensities[1].GetDouble()) {Skip();continue;}
                            magnitude=intensities[0].GetDouble()*k/m;symbol="kN/m";
                            if(!double.IsFinite(magnitude)){Skip();continue;}
                            for(int station=0;station<=4;station++) loads.Add(new(loadCase,0,id,station/4.0,new(direction.X*magnitude,direction.Y*magnitude,direction.Z*magnitude),symbol));
                        }
                        else if(table=="PRES"&&element.Type=="PLATE"&&Text(item,"CMD")=="PRES"&&Text(item,"FACE_EDGE_TYPE")=="FACE"&&!Flag(item,"OPT_PROJECTION"))
                        {
                            if(Uniform(item.GetProperty("FORCES"),element.Nodes.Length) is not {} pressure) {Skip();continue;}
                            magnitude=pressure*k/(m*m);symbol="kN/m²";
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
