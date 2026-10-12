using System.Globalization;
using System.Text.Json;
using GPC.Converter.CivilNx;

namespace ANTHEA.ModelWorkspace;

public sealed partial class GpcModelCombinationService
{
    static CivilNxSnapshot Source(ModelSnapshot snapshot)
    {
        if(snapshot.SourceTables==null||new[]{"UNIT","NODE","ELEM","STLD"}.Any(t=>!snapshot.SourceTables.ContainsKey(t)))
            throw new InvalidDataException("Per questo collegamento GPC servono le tabelle sorgente MIDAS complete. Reimporta geometria, attributi e risultati.");
        return new(snapshot.SourceTables.ToDictionary(p=>p.Key,p=>new CivilNxResponse("db/"+p.Key,
            JsonSerializer.Serialize(new Dictionary<string,JsonElement>{{p.Key,p.Value}}))));
    }

    static List<CivilNxResponse> ResultTables(ModelSnapshot snapshot,ModelResultSet[] basis,List<string> warnings,CancellationToken token)
    {
        var tables=new List<CivilNxResponse>();var elements=snapshot.Elements.ToDictionary(e=>e.Id);
        static string N(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
        void Table(string name,string[] headers,List<string[]> rows)
        {
            if(rows.Count==0)return;
            tables.Add(new CivilNxResponse("post/TABLE",JsonSerializer.Serialize(new Dictionary<string,object>
                {{name,new {FORCE="kN",DIST="m",HEAD=headers,DATA=rows}}})));
        }
        foreach(var result in basis)
        {
            token.ThrowIfCancellationRequested();
            var plate=new List<string[]>();
            foreach(var v in result.Values)
                plate.Add([ (plate.Count+1).ToString(CultureInfo.InvariantCulture),v.Element.ToString(CultureInfo.InvariantCulture),result.Name,v.Node.ToString(CultureInfo.InvariantCulture),..v.Values.Select(N)]);
            Table("ANTHEAPlate",["Index","Elem","Load","Node",..ModelSnapshot.Components],plate);
            var beam=new List<string[]>();
            var beamFields=ResultFields.Components("BEAM","LOCAL").Select(c=>ResultFields.Read(result,"BEAM","LOCAL",c)
                .ToDictionary(v=>(v.Element,v.Node,v.Station),v=>v.Value)).ToArray();
            foreach(var key in beamFields.SelectMany(f=>f.Keys).Distinct())
            {
                if(beamFields.Any(f=>!f.ContainsKey(key)))throw new InvalidDataException("Componenti beam incomplete nel caso "+result.Name+".");
                if(!key.Station.HasValue)throw new InvalidDataException("Stazione beam non disponibile.");
                var e=elements[key.Element];
                string part=key.Station switch {0=>"I["+e.Nodes[0]+"]",1=>"J["+e.Nodes[1]+"]",.25=>"1/4",.5=>"2/4",.75=>"3/4",_=>throw new InvalidDataException("Stazione beam non gestita dal collegamento MIDAS.")};
                beam.Add([(beam.Count+1).ToString(CultureInfo.InvariantCulture),key.Element.ToString(CultureInfo.InvariantCulture),result.Name,part,..beamFields.Select(f=>N(f[key]))]);
            }
            Table("ANTHEABeam",["Index","Elem","Load","Part","Axial","Shear-y","Shear-z","Torsion","Moment-y","Moment-z"],beam);
            var nodal=new List<string[]>();
            foreach(var d in result.Displacements??[])
                if(d.Rotation is {} r)nodal.Add([(nodal.Count+1).ToString(CultureInfo.InvariantCulture),d.Node.ToString(CultureInfo.InvariantCulture),result.Name,N(d.X),N(d.Y),N(d.Z),N(r.X),N(r.Y),N(r.Z)]);
            Table("ANTHEADisplacements",["Index","Node","Load","DX","DY","DZ","RX","RY","RZ"],nodal);
        }
        // The historical preview contains translations only. GPC requires the complete six-component state:
        // do not manufacture zero rotations or mark an incomplete vector as available.
        if(basis.Any(r=>r.Displacements?.Any(d=>d.Rotation==null)==true))warnings.Add("Spostamenti: la copia contiene nodi con sole traslazioni. Le deformate delle combinazioni richiedono anche le rotazioni nodali: aggiorna i risultati per acquisirle. Nessuna rotazione è stata inventata.");
        if(tables.Count==0)throw new InvalidDataException("Nessuna sollecitazione plate o beam disponibile nei casi richiesti.");
        return tables;
    }
}
