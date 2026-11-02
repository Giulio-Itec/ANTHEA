using System.Globalization;
using System.Text.Json.Nodes;

namespace Anthea.Application.Actions;

public sealed record ActionSketchPoint(double X, double Y);
public sealed record ActionSketchPart(string Id, string Keys, string Label, string Kind, string Role, IReadOnlyList<ActionSketchPoint> Points);
public sealed record ActionSketch(string Title, string Note, IReadOnlyList<ActionSketchPart> Parts);

/// <summary>Original qualitative diagrams and input associations. No action, coefficient or result is calculated here.</summary>
public static class ActionAssistanceDocument
{
    private const double FaceLeft=220,FaceBottom=245,FaceWidth=560,FaceHeight=190,LayerStep=35;
    public static ActionSketch Sketch(string module, JsonObject document, string? table, int row)
    {
        var input = document["input"] as JsonObject ?? new JsonObject();
        string S(string key) => input[key]?.ToString() ?? "";
        var parts = new List<ActionSketchPart>();
        void Part(string id, string keys, string label, string kind, string role, params double[] xy) =>
            parts.Add(new(id, keys, label, kind, role, Enumerable.Range(0, xy.Length / 2).Select(i => new ActionSketchPoint(xy[2*i], xy[2*i+1])).ToArray()));
        void Line(string keys, string label, params double[] xy) => Part(keys, keys, label, "line", "geometry", xy);
        void Dim(string key, string label, params double[] xy) => Part(key, key, label, "dimension", "dimension", xy);
        void Load(string keys, string label, params double[] xy) => Part(keys, keys, label, "arrow", "action", xy);
        void Box(string keys, string label, double x, double y, double w, double h) => Part(keys, keys, label, "polygon", "geometry", x,y,x+w,y,x+w,y+h,x,y+h,x,y);
        void Beam() => Box("Material Deck ConcreteAreaMm2 AreaMm2 ConcreteInertiaMm4", "Elemento", 180,100,600,65);
        string title = "Geometria e convenzioni";
        const string note = "Schema qualitativo, non in scala. Le quote e i versi indicano a quali dati si riferisce il modello; i valori sono nel campo attivo. I risultati sono nelle schede di calcolo.";
        switch (module)
        {
            case ClimateDocument.Hydraulics:
                title = "Acqua e struttura · " + (S("Element") == "HorizontalSlab" ? "platea" : S("Element") == "RiverPier" ? "pila" : "parete");
                if (S("Element") == "HorizontalSlab")
                {
                    Box("Element SlabThicknessM WidthM", "Platea · B fuori piano",180,110,600,55);
                    Dim("SlabLengthM", "L",180,225,780,225);Dim("SlabThicknessM", "t",820,110,820,165);
                    Load("LeftLevelM Uplift PressureProfile PressureKpa PositionM ProfileMultiplier", "Sottospinta",350,215,350,170);
                    Load("RightLevelM", "Acqua sopra",650,45,650,105);Dim("BottomElevationM", "z₀",120,250,120,165);
                }
                else
                {
                    Box("Element WidthM", "B fuori piano",460,65,60,175);
                    Part("water-left","LeftLevelM","H sinistra / pelo libero","line","dimension",160,100,450,100);
                    if (S("Element") != "RiverPier") Part("water-right","RightLevelM","H destra","line","action",530,170,830,170);
                    Dim("BottomElevationM", "z₀",570,285,570,240);Dim("TopElevationM", "z₁",620,250,620,65);
                    Load("CurrentVelocityMs VelocityMs ElevationM DragCoefficient UseVelocityProfile",S("Element")=="RiverPier"?"v con segno":"+ forza",260,200,445,200);
                }
                break;
            case ClimateDocument.Construction:
                title = "Fase e area di carico in modifica";
                Box("ConstructionLoads LengthM WidthM LeftXM BottomYM", "Area caricata",250,65,430,130);
                Dim("LengthM", "Lx",250,235,680,235);Dim("WidthM", "Ly",760,195,760,65);
                Line("ThicknessM UnitWeightKnM3", "Spessore t",250,195,280,215,710,215,680,195);
                Load("AssignedPressureKpa FormworkKpa Model", "q ↓",350,25,350,95);
                Load("PointXM PointYM PointLoadKn", "F ↓",560,25,560,95);
                Load("HorizontalXKn HorizontalHeightM", "Hx →",680,110,840,110);
                Dim("StartStage StopStage DurationDays ConstructionStages", "Intervallo di attivazione",180,280,800,280);
                break;
            case ClimateDocument.TimeEffects:
                title = "Barra assiale · vincolo selezionato";Beam();
                Line("Restraint", "Riferimento",180,70,180,195);Dim("LengthM", "L",180,250,780,250);
                Load("StressEvents IncrementMpa", "+ P · trazione",720,130,870,130);
                if(S("Restraint")=="EndSpring") Line("Restraint EndSpringKnMm", "Molla K",780,165,800,145,820,185,840,145,860,185,880,165,900,165,900,100,900,205);
                else if(S("Restraint")=="FixedDistance") Line("Restraint Displacements DisplacementMm", "Distanza imposta",790,80,790,205);
                else Line("Restraint", "Estremità libera",780,185,840,185);
                Load("DisplacementMm Displacements IncludeShrinkage IncludeCreep", "+ u · allungamento",470,205,650,205);
                Dim("ReferenceAgeDays DryingStartDays AgeDays Observations TimeSteps", "Tempo t",180,285,780,285);
                break;
            case ClimateDocument.Prestressing:
                title = "Cavo · profilo e estremità attive";Beam();
                if(S("Profile")=="Parabolic") Line("Profile StartEccentricityM MiddleEccentricityM EndEccentricityM", "Cavo (schema)",180,115,280,135,380,150,480,155,580,150,680,135,780,115);
                else Line("Profile StartEccentricityM EndEccentricityM", "Cavo",180,135,780,135);
                Dim("LengthM", "L",180,250,780,250);Dim("MiddleEccentricityM StartEccentricityM EndEccentricityM", "e · +z ↑",480,130,480,155);
                if(S("Jacking")!="Right") Load("Jacking JackingStressMpa AnchorageSlipMm", "Tiro sinistra",175,130,65,130);
                if(S("Jacking")!="Left") Load("Jacking JackingStressMpa AnchorageSlipMm", "Tiro destra",785,130,900,130);
                Dim("JackingAgeDays TransferAgeDays AgeDays IncludeDeferredLosses", "Tiro → trasferimento → esercizio",180,285,780,285);
                break;
            case ClimateDocument.Cranes:
                title = "Pianta · vie, ruote e carrello";
                Line("Rail", "Rotaia 1",150,220,850,220);Line("Rail", "Rotaia 2",150,65,850,65);
                Line("CraneXM Distribution", "Ponte",330,65,330,220,610,220,610,65);
                Dim("LengthM", "ℓ",880,220,880,65);Dim("WheelbaseM", "a",330,270,610,270);
                Box("CrabYM HoistLoadKn CrabWeightKn", "Carrello",430,115,65,45);
                Load("LongitudinalKn DriveForceKn TravelDirection HorizontalXKn", "+x · lungo via",160,25,340,25);
                Load("TransverseKn HorizontalYKn", "+y · verso rotaia 2",120,215,120,90);
                Load("VerticalDownKn CraneWeightKn", "Q ↓ fuori piano",730,105,730,180);
                break;
            case ClimateDocument.Fire:
                title = "Facce · geometria dei dati inseriti";
                if(input["FireFaces"] is JsonArray faces)
                {
                    var valid = faces.Select((f,i)=>(Node:f,Index:i)).Where(f => new[]{"StartXM","StartYM","EndXM","EndYM"}.All(k=>Number(f.Node?[k]) is not null)).ToArray();
                    if(valid.Length>0)
                    {
                        double N(JsonNode? n) => Number(n)!.Value;
                        double xmin=valid.Min(f=>Math.Min(N(f.Node!["StartXM"]),N(f.Node["EndXM"]))), xmax=valid.Max(f=>Math.Max(N(f.Node!["StartXM"]),N(f.Node["EndXM"])));
                        double ymin=valid.Min(f=>Math.Min(N(f.Node!["StartYM"]),N(f.Node["EndYM"]))), ymax=valid.Max(f=>Math.Max(N(f.Node!["StartYM"]),N(f.Node["EndYM"])));
                        if(!double.IsFinite(xmax-xmin)||!double.IsFinite(ymax-ymin))return new(title,"Coordinate non rappresentabili: completare i dati delle facce.",[]);
                        double scale=Math.Min(FaceWidth/Math.Max(xmax-xmin,1e-12),FaceHeight/Math.Max(ymax-ymin,1e-12));
                        foreach(var f in valid)
                            Part("FireFaces:"+f.Index, "FireFaces StartXM StartYM EndXM EndYM Exposure ConvectionWm2K", (f.Index+1)+" · "+f.Node!["Name"], "line", f.Node["Exposure"]?.ToString()=="Adiabatic"?"dimension":f.Node["Exposure"]?.ToString()=="Unexposed"?"geometry":"action",FaceLeft+(N(f.Node["StartXM"])-xmin)*scale,FaceBottom-(N(f.Node["StartYM"])-ymin)*scale,FaceLeft+(N(f.Node["EndXM"])-xmin)*scale,FaceBottom-(N(f.Node["EndYM"])-ymin)*scale);
                    }
                }
                break;
            case ClimateDocument.Snow:
            case ClimateDocument.BuildingWind:
                title = "Copertura selezionata · sezione simbolica";
                string roof=S("Roof");
                Line("HeightM", "Edificio",240,250,240,130,760,130,760,250);
                if(roof=="Cylindrical") Line("Roof RiseM", "Copertura curva",240,130,300,80,400,50,500,40,600,50,700,80,760,130);
                else if(roof=="LowerRoof") Line("Roof StepHeightM UpperWidthM", "Salto di quota",150,70,460,70,460,150,790,150);
                else if(roof=="MultiSpan") Line("Roof PitchLeftDeg PitchRightDeg", "Falde multiple",240,130,370,60,500,130,630,60,760,130);
                else if(roof=="Duopitch") Line("Roof PitchDeg PitchLeftDeg PitchRightDeg", "Due falde",240,130,500,60,760,130);
                else if(roof=="Monopitch") Line("Roof PitchDeg PitchLeftDeg", "Falda singola",240,130,760,65);
                else Line("Roof ParapetM", "Copertura piana",240,130,760,130);
                Dim("WidthM", "b",240,280,760,280);Dim("HeightM StepHeightM RiseM", "h / dislivello",850,250,850,70);
                if(module==ClimateDocument.Snow) Load("GroundLoadKnM2 Exposure ThermalCoefficient SnowGuards", "Neve · verticale",400,10,400,95);
                else {Load("WindDirectionDeg BaseSpeedMs StructuralFactor", "Vento · direzione in pianta",70,180,230,180);Load("InternalPressure InternalCoefficientMin InternalCoefficientMax", "Pressione interna ±",500,210,500,150);}
                break;
            case ClimateDocument.BridgeWind:
                title = "Sezione trasversale dell’impalcato";Box("Deck DepthM", "Impalcato 1",200,130,360,60);
                Dim("WidthM", "d",200,250,560,250);Dim("TotalHeightM DepthM", "htot / h",120,100,120,190);
                Load("DragCoefficient AssignedDragCoefficient BaseSpeedMs", "Vento trasversale",15,145,190,145);
                if(S("AdjacentDeck")=="true") {Box("SecondWidthM CoupledDecks", "Impalcato 2",650,130,220,60);Dim("GapM", "gap",560,215,650,215);}
                Dim("DeckCentreHeightM", "Quota dal suolo",920,280,920,160);
                break;
            case ClimateDocument.BuildingThermal:
            case ClimateDocument.BridgeThermal:
                title = "Componente uniforme e differenza termica";Beam();
                Dim("LengthM", "L dal punto fisso",180,255,780,255);
                Dim("ThicknessM", "Spessore",840,100,840,165);
                Load("InitialTemperatureC MinimumBridgeC MaximumBridgeC MinimumAirC MaximumAirC", "Dilatazione / contrazione ±",620,210,790,210);
                Line("SummerOuterSurfaceC WinterOuterSurfaceC HeatingDifferenceC CoolingDifferenceC", "Superficie esterna / superiore",180,85,780,85);
                Line("SummerInnerSurfaceC WinterInnerSurfaceC", "Superficie interna / inferiore",180,180,780,180);
                break;
            case ClimateDocument.PermanentLoads:
                title = "Strati e sovraccarichi";
                for(int i=0;i<3;i++) Box("Layers ThicknessM UnitWeightKnM3 Structural", "Strato "+(i+1),220,100+i*LayerStep,530,30);
                Load("DistributedKnM2 Occupancy AssignedImposedLoads", "qk ↓",350,25,350,95);Load("ConcentratedKn ContactSideM", "Qk ↓",650,25,650,95);
                Dim("TributaryWidthM", "Larghezza tributaria",220,255,750,255);break;
            case ClimateDocument.Combinations:
                title = "Azioni ed effetti · stessa unità e convenzione di segno";
                Box("Actions Nature Effect", "G₁ · G₂ · P · Qᵢ",70,105,240,75);
                Load("Family Set EnExpression", "Combinazione",340,140,600,140);
                Box("PsiCategory Psi0 Psi1 Psi2", "γ · ψ · effetto con segno",640,105,300,75);
                break;
            case ClimateDocument.Impact:
                title = "Elemento esposto all’urto";
                if(S("Target")=="Superstructure") {Beam();Dim("ClearanceM EnFullImpactClearanceM EnNoImpactClearanceM", "Franco netto",830,270,830,165);Load("ImpactForceKn", "Urto impalcato",80,170,180,130);}
                else {Box("Target MemberWidthM", "Piedritto",610,45,80,205);Load("ImpactForceKn ForkliftWeightKn", "Urto →",220,180,600,180);Dim("EnTruckImpactHeightM", "Quota urto",760,250,760,180);}
                break;
            case ClimateDocument.RoadTraffic:
            case ClimateDocument.Distribution:
                title = "Impalcato · corsie e travi (schema)";
                Box("DeckLeftM DeckRightM CarriagewayWidthM", "Impalcato",150,65,700,160);
                Line("Girders PositionM", "Travi",150,100,850,100);Line("Girders PositionM", "",150,190,850,190);
                Box("Lanes LeftM WidthM", "Corsia",200,115,600,50);Box("Tandems CentreTransverseM CentreLongitudinalM", "Tandem",400,120,100,40);
                Dim("SpanM LoadedLengthM", "L",150,270,850,270);break;
        }
        return new(title, note, parts);
    }

    private static double? Number(JsonNode? value) => double.TryParse(value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;

    // A crane position has several groups: preserve the selected group when possible, otherwise show its first group.
    public static int CaseForRow(string module,JsonObject? result,int row,int currentCase)
    {
        if(module!=ClimateDocument.Cranes)return row;
        if(result?["CraneGroups"] is not JsonArray groups)return -1;
        string? group=groups.ElementAtOrDefault(currentCase)?["Group"]?.ToString();
        int first=-1;
        for(int i=0;i<groups.Count;i++)
            if(groups[i]?["Position"]?.GetValue<int>()==row+1)
            {if(first<0)first=i;if(groups[i]?["Group"]?.ToString()==group)return i;}
        return first;
    }
    public static int RowForCase(string module,JsonObject? result,int selectedCase)=>module==ClimateDocument.Cranes
        ?(result?["CraneGroups"]?.AsArray().ElementAtOrDefault(selectedCase)?["Position"]?.GetValue<int>()??0)-1:selectedCase;

    public static string? CaseTable(string module) => module switch
    {
        ClimateDocument.Hydraulics=>"WaterCases", ClimateDocument.Construction=>"ConstructionStages",
        ClimateDocument.TimeEffects or ClimateDocument.Prestressing=>"Observations",ClimateDocument.Cranes=>"CranePositions", _=>null
    };
}
