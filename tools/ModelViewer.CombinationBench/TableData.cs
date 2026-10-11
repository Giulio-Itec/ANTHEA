using System.Globalization;
using System.Text.Json;

readonly record struct SampleKey(string Family,int Element,string Position,string Component);
sealed record DenseFields(SampleKey[] Keys,Dictionary<string,double[]> Cases,long Bytes,double ReadSeconds);
static class TableData
{
    public static readonly string[] PlateComponents=["Mxx","Myy","Mxy","Fxx","Fyy","Fxy","Vxx","Vyy"];
    public static readonly string[] BeamComponents=["Axial","Shear-y","Shear-z","Torsion","Moment-y","Moment-z"];
    public static DenseFields Read(IEnumerable<(string File,string Family)> files)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew(); long bytes=0;
        var rows=new Dictionary<string,List<(SampleKey Key,double Value)>>(StringComparer.Ordinal);
        foreach(var (file,family) in files)
        {
            bytes+=new FileInfo(file).Length;
            using var stream=File.OpenRead(file); using var doc=JsonDocument.Parse(stream);
            var table=doc.RootElement.EnumerateObject().Single(p=>p.Value.ValueKind==JsonValueKind.Object&&p.Value.TryGetProperty("HEAD",out _)).Value;
            if(!string.Equals(table.GetProperty("FORCE").GetString(),"N",StringComparison.OrdinalIgnoreCase)||!string.Equals(table.GetProperty("DIST").GetString(),"mm",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Expected N/mm source tables.");
            var heads=table.GetProperty("HEAD").EnumerateArray().Select(h=>h.GetString()!).ToArray();
            int Column(string name) => Array.IndexOf(heads,name) is var n && n>=0?n:throw new InvalidDataException("Missing column "+name);
            int load=Column("Load"),element=Column("Elem"),position=Column(family=="PLATE"?"Node":"Part");
            var columns=(family=="PLATE"?PlateComponents:BeamComponents).Select(c=>(Name:c,Index:Column(c))).ToArray();
            foreach(var row in table.GetProperty("DATA").EnumerateArray())
            {
                string name=row[load].GetString()!; if(!rows.TryGetValue(name,out var samples))rows[name]=samples=[];
                int id=int.Parse(row[element].GetString()!,CultureInfo.InvariantCulture);string part=row[position].GetString()!;
                foreach(var column in columns)
                {
                    double value=double.Parse(row[column.Index].GetString()!,CultureInfo.InvariantCulture);
                    if(!double.IsFinite(value))throw new InvalidDataException("Non-finite source sample.");
                    samples.Add((new(family,id,part,column.Name),value));
                }
            }
        }
        SampleKey[]? keys=null; var fields=new Dictionary<string,double[]>(StringComparer.Ordinal);
        foreach(var (name,samples) in rows)
        {
            var ordered=samples.OrderBy(s=>s.Key.Family,StringComparer.Ordinal).ThenBy(s=>s.Key.Element).ThenBy(s=>s.Key.Position,StringComparer.Ordinal).ThenBy(s=>s.Key.Component,StringComparer.Ordinal).ToArray();
            var theseKeys=ordered.Select(s=>s.Key).ToArray();
            if(keys==null) { keys=theseKeys; if(keys.Distinct().Count()!=keys.Length)throw new InvalidDataException("Duplicate source samples."); }
            else if(!keys.SequenceEqual(theseKeys))throw new InvalidDataException("Result sampling differs between cases: "+name);
            fields.Add(name,ordered.Select(s=>s.Value).ToArray());
        }
        return new(keys??[],fields,bytes,watch.Elapsed.TotalSeconds);
    }
}
