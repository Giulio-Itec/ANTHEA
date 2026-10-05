using GPC.Checkers.Geotechnics.Piles;
using GPC.Checkers.Concrete.Piles;
using GPC.Checkers.Concrete.Detailing;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Sections.Rebar;
using GPC.Model.Standards;
static class ReinforcementChecks
{
    internal static int Run()
    {
        int count=0;void Check(bool value,string label){if(!value)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}void Near(double a,double b,double tol,string label)=>Check(Math.Abs(a-b)<=tol*Math.Max(1,Math.Abs(b)),label);
        void Bad(Action action,string label){try{action();throw new Exception("Accepted "+label);}catch(ArgumentException){Check(true,label);}}
        Near(PileSegments.ConcreteWeight(2,25),78.5398163397448,1e-12,"gross RC weight includes steel once");
        Near(PileSegments.TubeWeight(.2,100,10,78.5,20),.793723383014476,2e-6,"tube and grout disjoint material areas");
        Near(PileSegments.TubeWeight(.2,100,10,0,20),.571769862953343,1e-12,"grout excludes steel annulus");
        var p=new ElasticPileInput{Length=10,Diameter=1,EI=50000,EISource="test",Step=.5,Force=100,Layers=new[]{new ElasticPileLayer{Thickness=10,Value=10000}}};
        var lateral=ElasticPile.Calculate(p);p.AxialHeadForce=-100;p.WeightPerLength=20;p.AdditionalNodes=new[]{3.17};var axial=ElasticPile.Calculate(p);
        Near(axial.Points[0].AxialForce,-100,1e-12,"axial tension at head kept signed");Near(axial.Points.Last().AxialForce,100,1e-12,"self weight cumulative to toe");Check(axial.Nodes.Any(n=>n.Depth==3.17),"verification boundary belongs to FEM mesh");Near(axial.HeadDisplacement,lateral.HeadDisplacement,1e-5,"axial force has no implicit P-delta effect");
        Check(axial.SectionDemands.Zip(axial.Points).All(x=>x.First.AxialForce==x.Second.AxialForce&&x.First.Shear==x.Second.Shear&&x.First.Moment==x.Second.Moment),"concomitant NVM and side in section demands");
        p.AxialHeadForce=p.WeightPerLength=0;Check(ElasticPile.Calculate(p).Points.All(x=>x.AxialForce==0),"zero axial force and zero weight");p.WeightPerLength=-1;Bad(()=>ElasticPile.Calculate(p),"negative weight rejected");
        var segments=new[]{new PileSegment{Id="A",End=10}};PileSegments.Validate(segments,10);var split=PileSegments.Split(segments,"A",3.17);PileSegments.Validate(split,10);Check(split.Count==2&&split[1].Inherited,"split conserves coverage and inheritance");Bad(()=>PileSegments.Split(segments,"A",10),"split at end rejected");Bad(()=>PileSegments.Validate(new[]{new PileSegment{Id="A",End=3},new PileSegment{Id="B",Start=4,End=10}},10),"gap rejected");Bad(()=>PileSegments.Validate(new[]{new PileSegment{Id="A",End=10,Inherited=false}},10),"first segment always linked");
        var anchor=AnchorageCalculator.Calculate(DetailingProfile.Ntc2018,new AnchorageInput(16,400,2,1.5,true,534));Near(anchor.RequiredLength,533.333333333333,1e-12,"independent bond example 16mm 400MPa fbd3MPa");Check(anchor.Passed,"anchorage length threshold");var lap=AnchorageCalculator.Calculate(DetailingProfile.Ntc2018,new AnchorageInput(16,400,2,1.5,true,800,true,100,0));Near(lap.RequiredLength,800,1e-12,"100 percent lap independent reference 800mm");Check(lap.Passed,"lap at required length passes");
        var parameters=Parameters();var service=new PileReinforcement(parameters);var zero=service.Check(new PileAction());Check(zero.MRdPositive>0&&zero.MRdNegative<0,"native resistance two bending branches");Near(zero.MRdPositive!.Value,-zero.MRdNegative!.Value,1e-4,"symmetric ring symmetric resistance");
        var compressed=service.Check(new PileAction{N=1000,M=100,V=100,Depth=2});Check(Math.Abs(compressed.MRdPositive!.Value-zero.MRdPositive.Value)>1,"MRd depends on concomitant N");Check(compressed.Action.N==1000&&compressed.Action.M==100&&compressed.Action.V==100,"section check keeps concomitant actions");Check(compressed.Status.StartsWith("Soddisfatto"),"N-M-V fulfilled for low demand");
        var reverse=service.Check(new PileAction{N=1000,M=-100,V=-100});Near(reverse.BendingRatio!.Value,compressed.BendingRatio!.Value,1e-4,"bending reversal");Near(reverse.ShearRatio!.Value,compressed.ShearRatio!.Value,1e-12,"shear reversal");
        var outside=service.Check(new PileAction{N=1e9,M=1});Check(outside.MRdPositive==null&&outside.Status=="Non verificabile","outside axial domain has no apparent resistance");
        var bars=service.Detail("A",0,10,10,new[]{compressed});Check(!bars.EndDevelopmentAvailable&&bars.Start==0&&bars.End==10,"missing external development not credited");Near(bars.TotalLength,160,1e-12,"bill derives from actual bar endpoints");
        var head=service.Check(new PileAction{Depth=0});var middle=service.Check(new PileAction{Depth=5});PileReinforcement.ApplyDevelopment(bars,new[]{head,middle});Check(head.UsableMRdPositive==null&&head.Status=="Parziale"&&head.MRdPositive>0,"undeveloped bars keep nominal resistance separate and unusable");Check(middle.UsableMRdPositive==middle.MRdPositive&&middle.DevelopmentAvailable==true,"developed interior retains local section resistance");
        parameters.HeadAnchorage=parameters.ToeAnchorage=3;var anchored=new PileReinforcement(parameters).Detail("A",0,10,10,new[]{compressed});Check(!anchored.EndDevelopmentAvailable&&anchored.TotalLength==bars.TotalLength,"external availability does not silently extend user cutting lengths");
        parameters.LinkSpacing=500;parameters.PileMinimumRequirements=true;Check(new PileReinforcement(parameters).Check(new PileAction()).MinimumReinforcementPassed==false,"documented pile link spacing minimum");parameters.LinkSpacing=150;
        Check(PileReinforcement.SelectCandidate(new[]{parameters},new[]{new PileAction{N=1000,M=100,V=100}})==0,"discrete design reuses section checker");
        var boundary=PileReinforcement.SuggestBoundaries(new[]{compressed},10,4,1);Check(boundary.SequenceEqual(new[]{0d,4d,8d,10d}),"proposal respects explicit maximum segment length");
        var pieces=PileReinforcement.StockPieces("B1",0,20,12,1,16,24);Check(pieces.Count==2&&pieces.All(b=>b.End-b.Start<=12),"stock cutting length constraint");Near(pieces[0].End-pieces[1].Start,1,1e-12,"actual overlap length");Near(pieces.Sum(b=>b.Count*(b.End-b.Start)),336,1e-12,"cutting bill includes physical overlap once");
        var runs=PileReinforcement.ContinuousRuns(new[]{new PileDetailSegment{Id="T1",Start=0,End=4,Service=service,Checks=new[]{compressed}},new PileDetailSegment{Id="T2",Start=4,End=10,Service=service,Checks=new[]{compressed}}},10);Check(runs.Count==2&&runs[0].End==4&&runs[1].End==10,"identical reinforcement keeps user cutting partitions");
        var development=PileReinforcement.SuggestWithDevelopment(new[]{compressed},new[]{new PileBarRun{Anchorage=1,Lap=2,Shift=.5}},10,8,1);Check(development.Zip(development.Skip(1)).All(x=>x.Second-x.First<=5.5),"proposal reserves development and lap allowance");
        var axialValues=new[]{-100d,0,250,1000,2000,1e9,250};var serial=new PileReinforcement(Parameters());var parallel=new PileReinforcement(Parameters());var resistanceTable=new PileResistanceTable();
        Check(parallel.PrepareResistance(axialValues,()=>new PileReinforcement(Parameters()),resistanceTable,maximumParallelism:4)==6&&resistanceTable.Count==6,"parallel batch deduplicates exact axial forces");
        foreach(double n in axialValues.Distinct())
        {
            var expected=serial.Check(new PileAction{N=n,M=100,V=80});var actual=parallel.Check(new PileAction{N=n,M=100,V=80});
            Check(expected.Status==actual.Status&&expected.MRdPositive.HasValue==actual.MRdPositive.HasValue,"parallel domain validity at N="+n);
            if(expected.MRdPositive.HasValue){Near(actual.MRdPositive!.Value,expected.MRdPositive.Value,1e-8,"parallel MRd+ at N="+n);Near(actual.MRdNegative!.Value,expected.MRdNegative!.Value,1e-8,"parallel MRd- at N="+n);}
        }
        Check(new PileReinforcement(Parameters()).PrepareResistance(axialValues,()=>throw new Exception("Cache missed"),resistanceTable)==0,"completed resistance cache reuses all N without creating solvers");
        using(var stop=new CancellationTokenSource())
        {
            var cancelledTable=new PileResistanceTable();stop.Cancel();try{parallel.PrepareResistance(axialValues,()=>new PileReinforcement(Parameters()),cancelledTable,stop.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(cancelledTable.Count==0,"cancelled batch publishes no resistances");}
        }
        var reused=new PileReinforcement(Parameters());Bad(()=>parallel.PrepareResistance(new[]{10d,20d},()=>reused,maximumParallelism:2),"parallel workers reject shared solver state");
        Near(PileReinforcement.InitialLap(24),1.5,1e-12,"60 phi24 is 1.44m rounded up to 1.50m");Near(PileReinforcement.InitialLap(20),1.2,1e-12,"exact decimetre does not receive spurious extra rounding");Bad(()=>PileReinforcement.InitialLap(-1),"invalid lap diameter rejected");
        Check(bars.Lap>=bars.RequiredLap&&bars.Lap>=bars.InitialLap,"software lap never shortens required lap");
        var buildable=PileReinforcement.SuggestBuildable(new[]{compressed},new[]{new PileBarRun{Lap=1.5,Anchorage=1,Shift=.5}},20.3,12,1);
        Check(buildable[0]==0&&buildable[^1]==20.3&&buildable.Skip(1).SkipLast(1).All(x=>x*2==Math.Floor(x*2)),"proposal half metre grid with exact toe");
        Check(buildable.Zip(buildable.Skip(1)).All(x=>x.Second-x.First>=3&&x.Second-x.First+(x.First==0?0:1.5)<=12+1e-9),"proposal min3 reserves one incoming lap except at head");
        Bad(()=>PileReinforcement.SuggestBuildable(new[]{compressed},new[]{new PileBarRun{Lap=10}},20,12,3),"incompatible stock and lap cannot introduce short segments");
        var preferred=PileReinforcement.PreferredStockPieces("P",0,20.3,12,1.5,16,24);Check(preferred.All(b=>new[]{6d,8,10,12}.Contains(b.StockLength)&&b.CuttingLength<=b.StockLength),"preferred stock catalogue includes the overlap");
        Check(preferred[0].Start==0&&preferred[^1].End==20.3&&preferred.Zip(preferred.Skip(1)).All(x=>Math.Abs(x.First.End-x.Second.Start-1.5)<1e-9),"preferred bill covers the bar and retains each physical lap");
        var layouts=PileReinforcement.Layouts(new[]{6,8},new[]{16d,24},new[]{8d,10},new[]{100d,200});Check(layouts.Count==16&&layouts.Zip(layouts.Skip(1)).All(x=>x.First.SteelArea<=x.Second.SteelArea),"design varies counts diameters and links ordered by steel area");
        var detailing=service.ConstructionChecks();var spacingCheck=detailing.Single(c=>c.Key=="ClearSpacing");Near(spacingCheck.Actual!.Value,2*408*Math.Sin(Math.PI/16)-24,1e-10,"independent chord reference for ring clear spacing");Check(spacingCheck.Limit==25&&spacingCheck.Passed==true,"aggregate20 spacing limit25 from native detailing checker");
        Check(detailing.Single(c=>c.Key=="NominalCover").Passed==null,"missing durability input remains explicitly pending");
        parameters.Aggregate=200;Check(new PileReinforcement(parameters).ConstructionChecks().Single(c=>c.Key=="ClearSpacing").Passed==false,"aggregate requirement can fail an otherwise resistant section");
        var links1=PileReinforcement.LinkStations(0,5.1,200,false);var links2=PileReinforcement.LinkStations(5.1,10,150,true);var linkPositions=links1.Concat(links2).ToArray();Check(linkPositions.Distinct().Count()==linkPositions.Length&&linkPositions[0]==0&&linkPositions[^1]==10,"link bill covers both ends without duplicate boundary hoop");
        Check(links1.Append(5.1).Zip(links1.Append(5.1).Skip(1)).All(x=>x.Second-x.First<=.2+1e-9)&&links2.Zip(links2.Skip(1)).All(x=>x.Second-x.First<=.15+1e-9),"scheduled link spacing never exceeds assigned spacing");
        var columnParameters=Parameters();var column=new PileReinforcement(columnParameters);
        Check(column.ConstructionChecks().Any(c=>c.Key=="MinimumLongitudinal"),"pile default includes native column rules");
        var compressionCheck=column.ConstructionChecks(20000).Single(c=>c.Key=="MinimumLongitudinal");
        Near(compressionCheck.Limit!.Value,.1*20000000/columnParameters.Fyd,1e-12,"column minimum uses maximum compression converted kN to N");
        Check(compressionCheck.Passed==true,"column minimum with sufficient steel");
        columnParameters.DetailingMode=PileDetailingMode.CommonOnly;
        Check(!new PileReinforcement(columnParameters).ConstructionChecks().Any(c=>c.Key=="MinimumLongitudinal")&&new PileReinforcement(columnParameters).ConstructionChecks().Any(c=>c.Key=="MemberRulesExcluded"&&c.Passed==null),"explicit common-only override records omitted rules");
        columnParameters.DetailingMode=PileDetailingMode.Beam;Check(new PileReinforcement(columnParameters).ConstructionChecks().Any(c=>c.Key.StartsWith("MinimumTension")),"beam override uses existing beam rules");
        Near(column.LinkCentreRadius,425,1e-12,"drawn stirrup centreline accounts for cover and half diameter");
        var updates=new List<(int Done,int Total)>();var reported=new PileResistanceTable();
        new PileReinforcement(Parameters()).PrepareResistance(new[]{25d,50,75,25},()=>new PileReinforcement(Parameters()),reported,maximumParallelism:2,progress:(done,total)=>updates.Add((done,total)));
        Check(updates.Select(x=>x.Done).SequenceEqual(new[]{0,1,2,3})&&updates.All(x=>x.Total==3),"parallel progress counts completed exact N pairs monotonically");
        updates.Clear();new PileReinforcement(Parameters()).PrepareResistance(new[]{25d,50,75},()=>throw new Exception("Unexpected factory"),reported,progress:(done,total)=>updates.Add((done,total)));
        Check(updates.Count==1&&updates[0]==(3,3),"cached progress reports batch complete");
        var momentShape=new[]{(0d,0d),(2d,100d),(4d,70d),(6d,30d),(12d,0d)}.Select(a=>new PileSectionCheck{Action=new PileAction{Depth=a.Item1,M=a.Item2}}).ToArray();
        var half=PileReinforcement.ProposeBuildable(momentShape,Array.Empty<PileBarRun>(),12,12,3);
        Near(half.HalfMomentDepth!.Value,5,1e-12,"independent piecewise linear Mmax half crossing at five metres");Near(half.ChangeDepth!.Value,5,1e-12,"feasible half moment boundary retained");Check(half.Boundaries.Contains(5),"proposal contains selected change of section");
        foreach(var c in momentShape)c.Action.M=-c.Action.M;
        Check(PileReinforcement.ProposeBuildable(momentShape,Array.Empty<PileBarRun>(),12,12,3).Boundaries.SequenceEqual(half.Boundaries),"proposal invariant to moment sign");
        var zeroProposal=PileReinforcement.ProposeBuildable(new[]{new PileSectionCheck{Action=new PileAction()}},Array.Empty<PileBarRun>(),12,12,3);Check(zeroProposal.ChangeDepth==null,"zero moment does not invent an armature reduction");
        var constrained=PileReinforcement.ProposeBuildable(momentShape,new[]{new PileBarRun{Lap=1,Shift=.5}},12,8,3);
        Check(constrained.Boundaries.Zip(constrained.Boundaries.Skip(1)).All(v=>v.Second-v.First>=3&&v.Second-v.First+(v.First==0?0:1)<=8),"M half proposal satisfies stock and incoming lap constraints");
        return count;
    }
    internal static PileRcParameters Parameters(int barCount=16,double phi=24,double angle=0)
    {
        var concrete=new ConcreteMaterialEN1992("C35",35,ConcreteMaterial.CompressionStressStrainDiagrams.ParabolaRectangle);var steel=new SteelMaterial("B450",200000,450,450,.1,SteelMaterial.StressStrainCurveType.ElasticPerfectPlastic,SteelMaterial.SteelTypes.Rebar);
        var shape=new Shape2d(new Polygon2d(Enumerable.Range(0,64).Select(i=>new Point2d(500*Math.Cos(i*Math.PI/32),500*Math.Sin(i*Math.PI/32)))));var section=new ReinforcedConcreteSection(shape,concrete);
        section.AddRebars(Enumerable.Range(0,barCount).Select(i=>new ReinforcedConcreteRebar(new RebarSectionCircular(phi,steel),new Point2d((420-phi/2)*Math.Cos(i*2*Math.PI/barCount+angle),(420-phi/2)*Math.Sin(i*2*Math.PI/barCount+angle)))).ToArray());var axes=new CoordinateSystem(section.Centroid,new Vector3d(-1,0,0),new Vector3d(0,-1,0));var standard=new StandardNTC2018Concrete();
        var options=new SectionCheckerModelCode2010.SectionOptionsModelCode2010(axes,SectionSolver.FailureAnalysisTypes.ConstantN,SectionSolver.FailureDomainTypes.Plastic,SectionSolver.StressAnalysisTypes.NonLinear,0,0,false,32);var checker=new SectionCheckerModelCode2010(new SectionCheckerAttribute(section,null,null),options,standard,false);checker.SetDomainPointStrategy(SectionSolver.DomainPointStrategyTypes.Iterative);
        return new PileRcParameters{Solver=checker.SectionSolver,Axes=axes,Section=section,Standard=standard,Diameter=1000,Fck=35,Fctk05=concrete.Fctk05,Fcd=Math.Abs(concrete.CalculateFcd(standard)),Fyd=Math.Abs(steel.CalculateFyd(standard)),Es=200000,Cover=70,LinkDiameter=10,LinkSpacing=150,ShearModelConfirmed=true,DesignActionsConfirmed=true,GoodBond=true};
    }
}
