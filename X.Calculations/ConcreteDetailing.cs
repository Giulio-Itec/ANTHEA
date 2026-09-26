namespace Anthea.Calculations;

/// <summary>UI-independent contracts, mm/MPa/kN. Reusable by section and future anchorage modules.</summary>
public enum ConcreteMemberKind { Beam, Column, Slab, Wall }
public sealed record DetailingCheck(string Name, double? Actual, double? Limit, string Unit, bool? Passed, string Reference, string Explanation);
public sealed record AnchorageInput(double Diameter, double Stress, double Fctk05, double GammaC, bool GoodBond,
    double AvailableLength, bool Lap, double LapPercent, double LapClearDistance);
public sealed record AnchorageResult(double Fbd, double BasicLength, double RequiredLength, bool Passed, string Expression)
{
    public double Eta1 { get; init; }
    public double Eta2 { get; init; }
    public double Alpha6 { get; init; }
    public double MaximumLapClearDistance { get; init; }
    public bool LengthPassed { get; init; }
    public bool LapClearDistancePassed { get; init; }
}
public interface IConcreteAnchorageCalculator { AnchorageResult Calculate(AnchorageInput input); }
public sealed class ConcreteAnchorageCalculator : IConcreteAnchorageCalculator
{
    public AnchorageResult Calculate(AnchorageInput p)
    {
        if (new[] {p.Diameter,p.Fctk05,p.GammaC}.Any(v=>!double.IsFinite(v)||v<=0) ||
            new[]{p.Stress,p.AvailableLength}.Any(v=>!double.IsFinite(v)||v<0) ||
            (p.Lap && (!double.IsFinite(p.LapClearDistance)||p.LapClearDistance<0||!double.IsFinite(p.LapPercent)||p.LapPercent<=0||p.LapPercent>100)))
            throw new ArgumentException("Ancoraggio: controllare diametro, tensione, materiali e lunghezze.");
        double eta2 = p.Diameter <= 32 ? 1 : (132-p.Diameter)/100;
        if(eta2<=0) throw new ArgumentException("Diametro fuori campo per l’aderenza.");
        double fbd=ConcreteBond.Strength(p.Fctk05,p.Diameter,p.GoodBond?1:.7,1,p.GammaC);
        double basic=p.Diameter*p.Stress/(4*fbd), alpha6=p.Lap?Math.Clamp(Math.Sqrt(p.LapPercent/25),1,1.5):1;
        // Straight bars, no favourable confinement/shape reductions. NTC floor governs both modes.
        double required=p.Lap ? Math.Max(alpha6*basic,Math.Max(.3*alpha6*basic,Math.Max(20*p.Diameter,200)))
            : Math.Max(basic,Math.Max(20*p.Diameter,150));
        return new(fbd,basic,required,p.AvailableLength>=required && (!p.Lap || p.LapClearDistance<=4*p.Diameter),
            p.Lap?"l0 = max[α6·lb,rqd; 0,3·α6·lb,rqd; 20Ø; 200 mm]; α6 = clamp(√(%/25),1,1,5). Interferro ≤ 4Ø. Barre rettilinee, α1…α5 = 1."
            :"lb,rqd = Ø·σsd/(4fbd); lbd = max(lb,rqd; 20Ø; 150 mm). Barra rettilinea, nessuna riduzione favorevole.")
        { Eta1 = p.GoodBond ? 1 : .7, Eta2 = eta2, Alpha6 = alpha6, MaximumLapClearDistance = 4*p.Diameter,
            LengthPassed = p.AvailableLength >= required, LapClearDistancePassed = !p.Lap || p.LapClearDistance <= 4*p.Diameter };
    }
}
public sealed record ConcreteDetailingInput(ConcreteMemberKind Kind, SezioneCA Section, double CompressionKn,
    bool HasStirrups, double StirrupDiameter, double StirrupSpacing, int StirrupLegs,
    double Aggregate, double MinimumDurabilityCover, double CoverDeviation, bool LapZone,
    double SecondarySteelPerMetre, double SecondarySpacing, bool CriticalSlabRegion,
    bool CompressionBarsRestrained, bool EndSupportAnchorageConfirmed);
public interface IConcreteDetailingCalculator { IReadOnlyList<DetailingCheck> Calculate(ConcreteDetailingInput input); }
public sealed class ConcreteDetailingCalculator : IConcreteDetailingCalculator
{
    public IReadOnlyList<DetailingCheck> Calculate(ConcreteDetailingInput p)
    {
        var s=p.Section; var r=new List<DetailingCheck>();
        if(p.Kind==ConcreteMemberKind.Slab&&(s.Shape!="Rettangolare"||s.Holes.Count>0))throw new ArgumentException("Soletta piena: usare una striscia rettangolare senza foro, b = larghezza e h = spessore.");
        if(new[]{p.CompressionKn,p.StirrupDiameter,p.StirrupSpacing,p.Aggregate,p.CoverDeviation,p.SecondarySteelPerMetre,p.SecondarySpacing}.Any(v=>!double.IsFinite(v)||v<0))
            throw new ArgumentException("Dettagli: i parametri numerici devono essere finiti e non negativi.");
        void Min(string name,double actual,double limit,string unit,string reference,string expression) => r.Add(new(name,double.IsFinite(actual)?actual:null,limit,unit,actual>=limit,reference,expression));
        void Max(string name,double actual,double limit,string unit,string reference,string expression) => r.Add(new(name,double.IsFinite(actual)?actual:null,limit,unit,actual<=limit,reference,expression));
        void Pending(string name,string reference,string explanation) => r.Add(new(name,null,null,"",null,reference,explanation));
        const string ntc="NTC 2018 §4.1.6.1";
        double fck=s.Input.D("fck_mpa"),fyk=s.Input.D("fyk_mpa"),fctm=fck<=50?.3*Math.Pow(fck,2d/3):2.12*Math.Log(1+(fck+8)/10);
        double minPhi=s.Bars.Min(b=>b.Diametro),maxPhi=s.Bars.Max(b=>b.Diametro);
        double clear=double.PositiveInfinity;
        for(int i=0;i<s.Bars.Count;i++)for(int j=i+1;j<s.Bars.Count;j++)
            clear=Math.Min(clear,double.Hypot(s.Bars[i].X-s.Bars[j].X,s.Bars[i].Y-s.Bars[j].Y)-(s.Bars[i].Diametro+s.Bars[j].Diametro)/2);
        Min("Interferro minimo",clear,Math.Max(20,Math.Max(maxPhi,p.Aggregate+5)),"mm",ntc+".3 / EC2 §8.2","max(20 mm; Ømax; dg + 5 mm), controllo conservativo su tutte le coppie.");
        double nominal=s.Input.D("cover_mm");
        if(double.IsFinite(p.MinimumDurabilityCover)&&p.MinimumDurabilityCover>=0)
        {
            Min("Copriferro nominale",nominal,Math.Max(10,Math.Max(p.MinimumDurabilityCover,(p.HasStirrups?p.StirrupDiameter:maxPhi)+(p.Aggregate>32?5:0)))+p.CoverDeviation,"mm",ntc+".3 / EC2 §4.4","max(10; cmin,dur; cmin,b) + Δcdev. cmin,dur proviene dal progetto di durabilità.");
            double margin=double.PositiveInfinity;
            foreach(var bar in s.Bars)
            {
                double distance=SectionRegions.BarCover(s,bar);
                double required=Math.Max(10,Math.Max(p.MinimumDurabilityCover,bar.Diametro+(p.Aggregate>32?5:0)))+p.CoverDeviation;
                margin=Math.Min(margin,distance-required);
            }
            Min("Margine copriferro barre longitudinali",margin,0,"mm",ntc+".3 / EC2 §4.4","Minimo (copriferro geometrico della barra − copriferro richiesto), su contorno esterno e foro. Stessa classe di esposizione per tutte le superfici.");
        }
        else Pending("Copriferro nominale",ntc+".3","Completare esposizione SLE e parametri di durabilità per calcolare cmin,dur.");
        if(p.Kind==ConcreteMemberKind.Column)
        {
            Min("Diametro longitudinale",minPhi,12,"mm",ntc+".2","Ømin ≥ 12 mm");
            var spacing=new TensionBarSpacing().Maximum(s,Enumerable.Range(0,s.Bars.Count).ToArray());
            if(spacing is double sp) Max("Interasse longitudinale",sp,300,"mm",ntc+".2","Interasse sulle facce / sul perimetro ≤ 300 mm."); else Pending("Interasse longitudinale",ntc+".2","Disposizione non riconosciuta automaticamente.");
            Min("Armatura longitudinale minima",s.AreaSteel,Math.Max(.1*p.CompressionKn*1000/s.Fyd,.003*s.AreaCls),"mm²",ntc+".2","max(0,10 NEd/fyd; 0,003 Ac), NEd massimo di compressione fra le combinazioni.");
            if(!p.LapZone)Max("Armatura longitudinale massima",s.AreaSteel,.04*s.AreaCls,"mm²",ntc+".2","As ≤ 0,04 Ac fuori dalle sovrapposizioni.");
            Min("Diametro staffe",p.HasStirrups?p.StirrupDiameter:0,Math.Max(6,maxPhi/4),"mm",ntc+".2","max(6 mm; Ølong,max/4)");
            Max("Passo staffe",p.HasStirrups?p.StirrupSpacing:double.PositiveInfinity,Math.Min(250,12*minPhi),"mm",ntc+".2","min(250 mm; 12 Ølong,min)");
        }
        else if(p.Kind is ConcreteMemberKind.Beam or ConcreteMemberKind.Slab)
        {
            // Both faces are checked so reversal of bending is not silently omitted.
            foreach(bool top in new[]{false,true})
            {
                double middle=(s.Outline.Min(v=>v[1])+s.Outline.Max(v=>v[1]))/2;
                var bars=s.Bars.Where(b=>top?b.Y>=middle:b.Y<middle).ToArray();
                if(bars.Length==0){Min("As,min "+(top?"superiore":"inferiore"),0,Math.Max(.26*fctm/fyk,.0013)*s.Width*s.Height,"mm²",ntc+".1","Nessuna barra nella metà di sezione considerata.");continue;}
                double area=bars.Sum(b=>b.Area),ys=bars.Sum(b=>b.Area*b.Y)/area;
                double d=top?ys-s.Outline.Min(v=>v[1]):s.Outline.Max(v=>v[1])-ys;
                double bt=s.Shape=="A T"?s.Input.D(top?"flange_width_mm":"web_width_mm"):s.Width;
                string face=top?"superiore":"inferiore";
                Min("As,min "+face,area,Math.Max(.26*fctm/fyk,.0013)*bt*d,"mm²",ntc+".1 / EC2 §9.3.1.1","max(0,26 fctm/fyk; 0,0013) bt d; entrambe le facce potenzialmente tese.");
                if(!p.LapZone)Max("As,max "+face,area,.04*s.AreaCls,"mm²",ntc+".1","As della singola zona ≤ 0,04 Ac.");
                if(p.Kind==ConcreteMemberKind.Beam)
                {
                    double bw=s.Shape=="A T"?s.Input.D("web_width_mm"):s.Width;
                    Min("Staffe minime · "+face,p.HasStirrups&&p.StirrupSpacing>0?p.StirrupLegs*Math.PI*p.StirrupDiameter*p.StirrupDiameter/4*1000/p.StirrupSpacing:0,1.5*bw,"mm²/m",ntc+".1","Ast/s ≥ 1,5 b mm²/m");
                    Max("Passo staffe · "+face,p.HasStirrups?p.StirrupSpacing:double.PositiveInfinity,Math.Min(1000d/3,.8*d),"mm",ntc+".1","Almeno 3 staffe/m; passo ≤ 0,8 d.");
                }
            }
            if(p.Kind==ConcreteMemberKind.Slab)
            {
                var spacing=new TensionBarSpacing().Maximum(s,Enumerable.Range(0,s.Bars.Count).ToArray());
                if(spacing is double sp)Max("Interasse armatura principale",sp,Math.Min((p.CriticalSlabRegion?2:3)*s.Height,p.CriticalSlabRegion?250:400),"mm","EC2 §9.3.1.1","Regione critica: min(2h;250); altre zone: min(3h;400).");
                double principal=s.AreaSteel*1000/s.Width;
                Min("Armatura secondaria",p.SecondarySteelPerMetre,.2*principal,"mm²/m","EC2 §9.3.1.1","As,secondaria ≥ 20% As,principale; dati totali delle due facce per metro di larghezza.");
                Pending("Ripartizione armatura secondaria sulle facce","EC2 §9.3.1.1","Il dato totale non verifica da solo il 20% su ciascuna faccia: controllare la distribuzione effettiva.");
                if(p.SecondarySpacing>0)Max("Interasse armatura secondaria",p.SecondarySpacing,Math.Min((p.CriticalSlabRegion?3:3.5)*s.Height,p.CriticalSlabRegion?400:450),"mm","EC2 §9.3.1.1","Regione critica: min(3h;400); altre zone: min(3,5h;450).");else Pending("Interasse armatura secondaria","EC2 §9.3.1.1","Inserire la spaziatura nella direzione ortogonale.");
                Pending("Bordi, appoggi e punzonamento","EC2 §§9.3–9.4 / NTC §4.1.2.3.5.4","Verifiche locali richiedono geometria dell’elemento e degli appoggi; non desumibili dalla striscia di sezione.");
            }
            else
            {
                Max("Trattenimento barre compresse",p.HasStirrups?p.StirrupSpacing:double.PositiveInfinity,15*minPhi,"mm",ntc+".1","Passo ≤ 15Ø delle barre compresse considerate resistenti.");
                if(!p.EndSupportAnchorageConfirmed)Pending("Ancoraggio negli appoggi di estremità",ntc+".1","Confermare la verifica della traslazione delle trazioni e dell’armatura inferiore ancorata.");
            }
        }
        else
        {
            Min("Armatura verticale parete",s.AreaSteel,.002*s.AreaCls,"mm²","EC2 §9.6.2","As,v ≥ 0,002 Ac, da distribuire sulle due facce.");
            if(!p.LapZone)Max("Armatura verticale massima",s.AreaSteel,.04*s.AreaCls,"mm²","EC2 §9.6.2","As,v ≤ 0,04 Ac fuori dalle sovrapposizioni.");
            double thickness=Math.Min(s.Width,s.Height),length=Math.Max(s.Width,s.Height);
            var verticalSpacing=new TensionBarSpacing().Maximum(s,Enumerable.Range(0,s.Bars.Count).ToArray());
            if(verticalSpacing is double sv)Max("Interasse verticale sulla sezione",sv,Math.Min(3*thickness,400),"mm","EC2 §9.6.2","s ≤ min(3 volte lo spessore; 400 mm).");else Pending("Interasse verticale sulla sezione","EC2 §9.6.2","Disposizione non riconosciuta automaticamente.");
            Min("Armatura orizzontale per metro",p.SecondarySteelPerMetre,Math.Max(.25*s.AreaSteel*1000/length,.001*thickness*1000),"mm²/m","EC2 §9.6.3","max(0,25 As,v; 0,001 Ac), riportato a 1 m di parete.");
            if(p.SecondarySpacing>0)Max("Interasse orizzontale",p.SecondarySpacing,400,"mm","EC2 §9.6.3","s ≤ 400 mm");else Pending("Interasse orizzontale","EC2 §9.6.3","Inserire la spaziatura delle barre orizzontali.");
            Pending("Distribuzione e collegamenti fra facce","EC2 §9.6","Verificare distribuzione su entrambe le facce e legature trasversali; non descritti dalle sole barre longitudinali.");
        }
        if(p.HasStirrups&&!p.CompressionBarsRestrained)Pending("Barre trattenute dalle staffe",ntc,"Confermare la disposizione effettiva di staffe e legature sulle barre compresse.");
        if(p.LapZone && p.Kind is ConcreteMemberKind.Column or ConcreteMemberKind.Wall)
            Max("Armatura massima nella giunzione",s.AreaSteel,.08*s.AreaCls,"mm²","EC2 §§9.5.2 / 9.6.2","As ≤ 0,08 Ac. Includere nella geometria tutte le barre presenti nella giunzione, comprese quelle sovrapposte.");
        return r;
    }
}
