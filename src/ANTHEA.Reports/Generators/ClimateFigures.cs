using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Anthea.Reports;

/// <summary>Original shared schematic assets. No engineering calculation.</summary>
public static class ClimateFigures
{
    public static IReadOnlyList<string> Names(JsonObject document)
    {
        string type=document["input_type"]?.ToString()??"";
        return type switch
        {
            "SnowInput"=>["neve"],
            "BuildingWindInput"=>["vento-edifici", "vento-"+(document["input"]?["Roof"]?.ToString() switch {"Monopitch"=>"mono","Duopitch"=>"doppia",_=>"piana"})],
            "BuildingThermalInput"=>["temperatura-edifici"],
            "BridgeWindInput"=>["vento-ponti"],
            "BridgeThermalInput"=>["temperatura-ponti"],
            "BuildingLoadsInput"=>["carichi"],
            "ActionCombinationsInput"=>["combinazioni"],
            "RoadTrafficInput"=>["traffico"],
            "TransverseDistributionInput"=>["ripartizione"],
            "VehicleImpactInput"=>["urti"],
            _=>[]
        };
    }
    internal static void Append(DocxDocument package,XElement body,JsonObject input)
    {
        XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main",wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing",a="http://schemas.openxmlformats.org/drawingml/2006/main",pic="http://schemas.openxmlformats.org/drawingml/2006/picture",r="http://schemas.openxmlformats.org/officeDocument/2006/relationships",rel="http://schemas.openxmlformats.org/package/2006/relationships";
        var relationships=new XElement(rel+"Relationships");int id=0;
        foreach(string name in Names(input))
        {
            id++;using var stream=typeof(ClimateFigures).Assembly.GetManifestResourceStream("Anthea.Climate.clima-"+name+".png")??throw new InvalidOperationException("Schema climatico mancante: "+name);
            using var buffer=new MemoryStream();stream.CopyTo(buffer);package.SetBytes("word/media/climate"+id+".png",buffer.ToArray());
            relationships.Add(new XElement(rel+"Relationship",new XAttribute("Id","climate"+id),new XAttribute("Type",r.NamespaceName+"/image"),new XAttribute("Target","media/climate"+id+".png")));
            long cx=6096000,cy=name=="neve"?4064000:2438400;
            var picture=new XElement(pic+"pic",new XElement(pic+"nvPicPr",new XElement(pic+"cNvPr",new XAttribute("id",id),new XAttribute("name",name)),new XElement(pic+"cNvPicPr")),new XElement(pic+"blipFill",new XElement(a+"blip",new XAttribute(r+"embed","climate"+id)),new XElement(a+"stretch",new XElement(a+"fillRect"))),new XElement(pic+"spPr",new XElement(a+"xfrm",new XElement(a+"off",new XAttribute("x",0),new XAttribute("y",0)),new XElement(a+"ext",new XAttribute("cx",cx),new XAttribute("cy",cy))),new XElement(a+"prstGeom",new XAttribute("prst","rect"),new XElement(a+"avLst"))));
            body.Add(new XElement(w+"p",new XElement(w+"r",new XElement(w+"drawing",new XElement(wp+"inline",new XElement(wp+"extent",new XAttribute("cx",cx),new XAttribute("cy",cy)),new XElement(wp+"docPr",new XAttribute("id",id),new XAttribute("name",name),new XAttribute("descr","Schema qualitativo e convenzioni; dimensioni non in scala.")),new XElement(a+"graphic",new XElement(a+"graphicData",new XAttribute("uri",pic.NamespaceName),picture)))))));
        }
        package.SetXml("word/_rels/document.xml.rels",relationships.ToString());
    }
}
