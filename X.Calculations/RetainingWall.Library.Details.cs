using System.Text.Json.Nodes;
using GPC.Design.Walls;
using GPC.Model.Checking.Contracts;

namespace Anthea.Calculations;

public static partial class RetainingWall
{
    private static readonly IReadOnlyDictionary<WallRebarZone,string> NativeZoneKeys = new Dictionary<WallRebarZone,string>
    { [WallRebarZone.Stem]="stem",[WallRebarZone.StemUpper]="stem_upper",[WallRebarZone.Toe]="toe",[WallRebarZone.Heel]="heel" };
    private static string NativeFaceText(WallRebarFace face) => face switch
    { WallRebarFace.Upstream=>"monte",WallRebarFace.Downstream=>"valle",WallRebarFace.Bottom=>"inferiore",WallRebarFace.Top=>"superiore",_=>throw new ArgumentOutOfRangeException(nameof(face)) };
    private static WallDetailInput NativeDetailInput(JsonObject d, bool includeInactiveQuantities = false)
    {
        var g=d["geometry"]!;var m=d["materials"]!;var r=d["reinforcement"]!;var opt=d["detailing"]!;
        var section=SectionInput(d,"Fusto",g.Required("stem_base"));
        var strengths=ConcreteMaterials.DesignValues(section,SectionWorkspace.Prepare(SezioneCA.DefaultData()));
        var concrete=ConcreteMaterials.Concrete(section);
        // F5.15, R10 (EN 1992-1-1 8.4.2(2)): fctk,0.05 of the bond limited to the C60/75 value, as the RC section and the elastic pile
        // (AnchorageCalculator.BondFctk05). Up to C60/75 the value of the material, unchanged; before, anchorages and laps of the walls
        // took fctk,0.05 of the class (lb,rqd −11 % with C90/105).
        double fck=section.Required("fck_mpa");
        double bondFctk05=fck>GPC.Checkers.Concrete.Detailing.AnchorageCalculator.BondStrengthClassLimit
            ? GPC.Checkers.Concrete.Detailing.AnchorageCalculator.BondFctk05(fck) : concrete.Fctk05;
        var zones=new Dictionary<WallRebarZone,WallZoneBars>();
        foreach(var (zone,key) in NativeZoneKeys)
        {
            if (zone == WallRebarZone.StemUpper && !r.B("two_zones") || !includeInactiveQuantities && (zone == WallRebarZone.Toe && g.Required("toe") == 0 || zone == WallRebarZone.Heel && g.Required("heel") == 0)) continue;
            var a=r[key]!;bool symmetric=a.B("symmetric",true);
            WallFaceBars Face(bool first)
            {
                double n=a.Required(first || symmetric?"count":"opposite_count");
                if(n%1!=0 || n<2 || n>30) throw new ArgumentException("Armature: diametri 6–40 mm, 2–30 barre intere per metro.");
                return new(a.Required(first || symmetric?"diameter":"opposite_diameter"),(int)n);
            }
            zones[zone]=new(Face(true),Face(false),a.Required("secondary_diameter"),a.Required("secondary_spacing"),a.Required("anchor_length"),a.Required("lap_length"),a.Required("bend_diameter"));
        }
        int life=(int)AdvancedNumber(opt,"life",50,100);
        if(life is not (50 or 100)) throw new ArgumentException("Vita per il copriferro: 50 o 100 anni.");
        var state=CoverMaterialState(d);
        var cover=Materiali.MaterialCover.Input(state,m.Required("cover"),opt.Required("aggregate"));
        string code=m.S("exposure");
        var (profile,specification)=ConcreteDurabilityAdapter.WallCoverInput(opt.S("cover_method"),code,m.Required("fck"),cover,
            opt.S("cover_element")=="Piastra / soletta / parete",opt.B("cover_quality"));
        // 1.16.0: the stock bar decides whether toe and heel bars of a layer are one continuous bar (also in the details of the result).
        double stock=d["bar_schedule"] is JsonObject schedule && schedule["stock_length"] is not null ? schedule.Required("stock_length") : 12;
        return new(new(g.Required("height"),g.Required("slab"),g.Required("toe"),g.Required("stem_base"),g.Required("stem_top"),g.Required("heel")),
            new(r.B("two_zones"),r.B("two_zones") ? r.Required("lower_height", strict: true) : 0,zones),new(strengths.Fcd,strengths.Fyd,bondFctk05,section.Required("gamma_c")),m.Required("cover"),
            new(opt.Required("aggregate"),opt.Required("lap_clear"),opt.Required("lap_percent"),opt.B("good_bond"),opt.Required("tie_diameter"),opt.Required("tie_spacing"),profile,specification) { StockLength=stock });
    }
    private static string NativeFootText(WallFootDirection foot) => foot switch { WallFootDirection.Valley=>"valle",WallFootDirection.Mountain=>"monte",_=>"" };
    private static BarPoint NativePoint(WallBarPoint p) => new(p.X,p.Y);
    private static BarLeg NativeLeg(WallBarLeg l) => new(l.Kind==WallBarLegKind.Arc ? "arco "+Math.Round(l.Angle).ToString(System.Globalization.CultureInfo.InvariantCulture)+"°" : l.Name,l.Length)
        { Arc=l.Kind==WallBarLegKind.Arc,Angle=l.Angle,From=l.From is { } f ? NativePoint(f) : null,To=l.To is { } t ? NativePoint(t) : null };
    private static readonly IReadOnlyDictionary<WallDetailRule,string> NativeDetailLabels = new Dictionary<WallDetailRule,string>
    {
        [WallDetailRule.ClearSpacing]="interferro",[WallDetailRule.MainSpacing]="interasse principale",[WallDetailRule.Cover]="copriferro",
        [WallDetailRule.CommonLap]="sovrapposizione effettiva comune",[WallDetailRule.LapPairClearSpacing]="interferro tra coppie giuntate",
        [WallDetailRule.LapClearDistance]="distanza barre giuntate",[WallDetailRule.LapPercentAtBoundary]="percentuale giuntata alla quota h₁",
        [WallDetailRule.LapExtent]="ingombro sovrapposizione",[WallDetailRule.BarCountContinuity]="corrispondenza barre nelle due zone",
        [WallDetailRule.SecondaryArea]="armatura secondaria",[WallDetailRule.SecondarySpacing]="passo secondaria",
        [WallDetailRule.Mandrel]="mandrino (acciaio e pressione CLS)",[WallDetailRule.BendHeight]="altezza per la piega",
        [WallDetailRule.TailExtent]="ingombro coda",[WallDetailRule.FootAnchorage]="ancoraggio al piede",[WallDetailRule.RootAnchorage]="ancoraggio oltre la radice",
        [WallDetailRule.AnchorageExtent]="ingombro ancoraggio",[WallDetailRule.JunctionTieSpacing]="passo collegamenti fra facce",
        [WallDetailRule.JunctionTieArea]="area trasversale in ciascun terzo estremo"
    };
    private static string NativeOutcomeText(EngineeringOutcome outcome,double? capacity) => outcome switch
    { EngineeringOutcome.Satisfied=>"Soddisfatta",EngineeringOutcome.NotSatisfied=>capacity==0?"Non soddisfatta: resistenza nulla":"Non soddisfatta",_=>"Non disponibile" };
    private static ReinforcementDetails CalculateReinforcementDetailsNative(JsonObject input)
    {
        var d=(JsonObject)input.DeepClone();CompleteAdvancedInput(d);
        var native=WallReinforcementDetailing.Calculate(NativeDetailInput(d));
        var bars=native.Bars.Select(b=>new BarDetail(b.Mark,NativeZoneKeys[b.Zone],NativeFaceText(b.Face),b.Diameter,b.Count,b.Spacing,b.Area,b.Length,
            b.RequiredAnchor,b.Anchor,b.RequiredLap,b.Lap,b.Mandrel,b.Fbd,b.Path.Select(NativePoint).ToArray()) {Foot=NativeFootText(b.Foot),Continuous=b.Continuous}).ToArray();
        var checks=native.Checks.Select(c=>new Check((c.Zone.HasValue?NativeZoneKeys[c.Zone.Value]+" · "+NativeFaceText(c.Face!.Value):"Giunzione")+" · "+NativeDetailLabels[c.Rule],
            "Dettagli",c.Demand,c.Capacity,c.Unit=="bars"?"barre":c.Unit,c.Ratio,NativeOutcomeText(c.Outcome,c.Capacity)) {Outcome=c.Outcome,Execution=c.Execution,Data=c.Data}).ToArray();
        var others=new List<SectionBar>();
        if(native.Head is { } head) others.Add(new(head.Mark,"Forcella",NativeZoneKeys[head.Zone],"testa",head.Diameter,head.Spacing,head.Length,head.Path.Select(NativePoint).ToArray(),[]));
        if(native.Tie is { } tie)
        {
            double middle=(tie.Path[0].X+tie.Path[tie.Path.Count-1].X)/2;
            others.Add(new(tie.Mark,"Spillo","stem","fra facce",tie.Diameter,tie.Spacing,tie.Length,tie.Path.Select(NativePoint).ToArray(),tie.Levels.Select(y=>new BarPoint(middle,y)).ToArray()));
        }
        others.AddRange(native.Longitudinals.Select(l=>new SectionBar(l.Mark,"Longitudinale",NativeZoneKeys[l.Zone],NativeFaceText(l.Face),l.Diameter,l.Spacing,0,[],l.Points.Select(NativePoint).ToArray())));
        return new(bars,checks,native.SteelKg,DetailingScopeNote) {Others=others.ToArray()};
    }
    private static BarSchedule CalculateBarScheduleNative(JsonObject input)
    {
        var d=(JsonObject)input.DeepClone();Upgrade(d);
        if(d.S("family")!="cantilever") throw new ArgumentException("La distinta ferri richiede un muro a mensola in calcestruzzo armato.");
        var o=d["bar_schedule"]!;double perMetre=AdvancedNumber(o,"ties_per_m",1,30);
        if(perMetre%1!=0) throw new ArgumentException("Collegamenti per fila: inserire un numero intero per metro.");
        var n=WallBarScheduling.Calculate(NativeDetailInput(d),new(o.Required("panel_length"),o.Required("end_cover"),o.Required("stock_length"),(int)perMetre,
            o.Required("tie_cut_length"),d["detailing"].B("enabled")));
        var ci=System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        string Cm(double metres) => (metres*100).ToString("0",ci);
        string Note(WallScheduledBar b) => b.Note switch
        {
            WallBarNote.None=>"",
            WallBarNote.FootTowardsValley=>"Piede verso valle, sulla rete inferiore della fondazione.",
            WallBarNote.FootTowardsMountain=>"Piede verso monte, sulla rete inferiore della fondazione.",
            WallBarNote.Continuous=>"Barra unica fra le due mensole, risvolti ai bordi liberi.",
            WallBarNote.Longitudinal=>b.Pieces>1?$"{b.Lines} fili × {b.Pieces} pezzi uguali, {b.Pieces-1} sovrapposizioni l₀ = {Cm(b.Lap/1000)} cm per filo.":$"{b.Lines} fili, un pezzo per filo.",
            WallBarNote.Tie=>$"{b.TieRows} file nella sovrapposizione, {perMetre:0}/m per fila; ganci a 135° prolungati di 10Ø.",
            WallBarNote.TieAssignedLength=>$"{b.TieRows} file nella sovrapposizione, {perMetre:0}/m per fila; sviluppo assegnato dall’utente.",
            WallBarNote.Head=>"Forcella di testa (bordo libero), gambe 2·spessore in testa.",
            _=>throw new ArgumentOutOfRangeException(nameof(b))
        };
        var bars=n.Bars.Select(b=>new ScheduledBar(b.Mark,NativeZoneKeys[b.Zone],b.Face.HasValue?NativeFaceText(b.Face.Value):b.Kind==WallBarKind.Head?"testa":"fra facce",
            b.Kind switch {WallBarKind.Main=>"Principale",WallBarKind.Secondary=>"Longitudinale",WallBarKind.Tie=>"Spillo",WallBarKind.Head=>"Forcella",_=>throw new ArgumentOutOfRangeException(nameof(b))},
            b.Diameter,b.Quantity,b.CutLength,b.Spacing,b.Mandrel,b.Lap,b.Legs.Select(NativeLeg).ToArray(),
            b.Shape.Select(NativePoint).ToArray(),Note(b)) {Lines=b.Lines,Pieces=b.Pieces,SectionPoints=b.SectionPoints.Select(NativePoint).ToArray()}).ToArray();
        string Warning(WallScheduleWarning w) => w.Code switch
        {
            WallScheduleWarningCode.DetailChecksIncomplete=>$"{w.Count} controlli dei dettagli non soddisfatti o non disponibili: consultare le verifiche prima dell’esecuzione.",
            WallScheduleWarningCode.DetailSummaryDisabled=>"Verifica dei dettagli non attivata nel riepilogo del muro.",
            WallScheduleWarningCode.LongerThanStock=>$"pos. {w.Mark}: sviluppo {n.Bars.Single(b=>b.Mark==w.Mark).CutLength:0.###} m maggiore della barra commerciale {n.StockLength:0.###} m; definire un giunto, nessun taglio automatico.",
            WallScheduleWarningCode.FootReversed=>$"pos. {w.Mark}: il piede non sta nella mensola dalla parte opposta e piega dalla sua parte; verificare il nodo fusto–fondazione.",
            _=>throw new ArgumentOutOfRangeException(nameof(w))
        };
        return new(n.PanelLength,n.EndCover,n.StockLength,bars,n.Warnings.Select(Warning).ToArray())
            {Diameters=n.Diameters.Select(x=>new DiameterTotal(x.Diameter,x.Length,x.KgPerMetre,x.Weight)).ToArray(),ConcreteVolume=n.ConcreteVolume};
    }
}
