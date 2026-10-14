using GPC.Model.Checking.Contracts;
using GPC.Model.Core.Diagnostics;
using System.Text.Json;
using System.Diagnostics;
using System.Text.Json.Nodes;
using GPC.Checkers.Geotechnics.Piles;
using GPC.Checkers.Concrete.Piles;
using GPC.Checkers.Concrete.Durability;
namespace Anthea.Calculations;
public sealed record PileCalculationProgress(string Phase,int Completed=0,int Total=0);
public static partial class ElasticHorizontalPile
{
    public const string SeismicSourceUrl=PileReinforcement.SeismicUrl;
    public static readonly string[] ReinforcementKeys=["longitudinal_bar_count","longitudinal_bar_diameter_mm","transverse_bar_diameter_mm","transverse_spacing_mm"];
    /// <summary>Second ring of bars inside the first, with the keys of the c.a. section («Secondo anello interno»). A segment without
    /// them takes those of the main section.</summary>
    public static readonly string[] InnerRingKeys=["second_inner_enabled","second_inner_count","second_inner_diameter","second_inner_gap"];
    /// <summary>Keys that a segment with its own reinforcement can assign instead of the main section.</summary>
    public static readonly string[] SegmentKeys=[..ReinforcementKeys,..InnerRingKeys];
    public static void PrepareReinforcement(JsonObject root)
    {
        var e=root["elastico"]!.AsObject();
        if(e["tratti"] is not JsonArray)e["tratti"]=new JsonArray(NewSegment("T1",null));
        if(e["dettagli"] is not JsonObject)e["dettagli"]=J.Obj(("azioni_progetto",false),("taglio_confermato",false),("z_d",.9),("aderenza_buona",false),("percentuale_sovrapposta",100),("distanza_barre",0),("ancoraggio_testa",0),("ancoraggio_punta",0),("lunghezza_gabbia",12),("lunghezza_minima",3));
        if(e["dettagli"]!["lunghezza_barra"]==null)e["dettagli"]!["lunghezza_barra"]=12;
        foreach(var (key,value) in new[]{("fattore_sovrapposizione",60d),("aggregato",20d),("delta_c",10d)})if(e["dettagli"]![key]==null)e["dettagli"]![key]=value;
        if(e["dettagli"]!["elemento"]==null)e["dettagli"]!["elemento"]="Pilastro";
        if(e["dettagli"]!["copriferro_override"]==null)e["dettagli"]!["copriferro_override"]=!string.IsNullOrWhiteSpace(e["dettagli"].S("cmin_dur"));
        if(e["dettagli"]!["minimi_pali"]==null)e["dettagli"]!["minimi_pali"]=true;
        if(e["dettagli"]!["sisma_testa"]==null)e["dettagli"]!["sisma_testa"]=false;
        if(e["dettagli"]!["sisma_staffe"]==null)e["dettagli"]!["sisma_staffe"]="Staffe singole";
    }
    public static JsonObject NewSegment(string id,double? end)=>J.Obj(("id",id),("fine",end),("collegato",true));
    public static PileSegment[] ReadSegments(JsonObject root)
    {
        double length=ElasticPileSection.TotalLength(root["generali"]!.Required("lunghezza",strict:true),root["generali"].D("tratto_libero")),start=0;
        return root["elastico"]!.Array("tratti").Select((r,i)=>{double end=r!["fine"]==null?length:J.Number(r["fine"])??throw new ArgumentException("Quota finale del tratto non valida.");var s=new PileSegment{Id=r.S("id"),Start=start,End=end,Inherited=r.B("collegato")};start=end;return s;}).ToArray();
    }
    public static JsonObject SegmentSection(JsonObject root,JsonObject segment)
    {
        var input=(JsonObject)root["sezione"]!.DeepClone();input["shape"]="Circolare";input["diameter_mm"]=ConcreteLibraryMapping.MillimetresFromMetres(root["generali"].D("diametro"));
        if(!segment.B("collegato"))foreach(string key in SegmentKeys)if(segment[key]!=null)input[key]=segment[key]!.DeepClone();input["tipo_trasversale"]=TransverseKind(root,segment);return input;
    }
    static JsonObject CalculateReinforcement(JsonObject root,JsonObject result)=>CalculateReinforcementCore(root,result,new(),default,0);
    static JsonObject CalculateReinforcementCore(JsonObject root,JsonObject result,ElasticPileVerificationCache resistanceCache,CancellationToken cancellation,int maximumParallelism,IProgress<PileCalculationProgress>? progress=null)
    {
        if(root.S("tipo_sezione")=="CHS")return J.Obj(("stato","Non applicabile: il verificatore c.a. non si applica ai CHS."));
        var output=new JsonArray();var rows=root["elastico"]!.Array("tratti");var segments=ReadSegments(root);var details=root["elastico"]!["dettagli"]!;
        var workspace=ConcreteStandards.PileWorkspace(root["sezione"]!.AsObject());var options=J.Obj(("criterio","N costante"),("assi","Locali"),("strategia","Iterativo"),("modello","Non lineare"));
        var services=new Dictionary<string,(PileReinforcement Service,JsonObject Input,CheckerSection Engine)>();
        var detailSegments=new List<PileDetailSegment>();var satisfiedSegments=new HashSet<string>(StringComparer.Ordinal);
        for(int i=0;i<segments.Length;i++)
        {
            cancellation.ThrowIfCancellationRequested();
            progress?.Report(new($"Sezione {i+1}/{segments.Length}: preparazione geometria e materiali"));
            var s=segments[i];var input=SegmentSection(root,rows[i]!.AsObject());var record=J.Obj(("id",s.Id),("inizio",s.Start),("fine",s.End),("lunghezza",s.Length),("collegato",s.Inherited),("sezione",input.DeepClone()));output.Add(record);
            try
            {
                var timing=Stopwatch.StartNew();var times=new JsonObject();record["tempi_ms"]=times;
                string signature=input.ToJsonString();if(!services.TryGetValue(signature,out var cache))
                {
                    var engine=new CheckerSection(input,workspace,options);var p=RcParameters(input,details,workspace,engine);
                    cache=(new PileReinforcement(p),input,engine);services.Add(signature,cache);
                    var relevant=segments.Where((_,j)=>SegmentSection(root,rows[j]!.AsObject()).ToJsonString()==signature).ToArray();
                    var axialForces=result["risposta"]!.Array("Points").Where(point=>relevant.Any(seg=>point.D("Depth")>=seg.Start-1e-9&&point.D("Depth")<=seg.End+1e-9)).Select(point=>point.D("AxialForce"));
                    times["preparazione_sezione"]=timing.Elapsed.TotalMilliseconds;timing.Restart();
                    var table=resistanceCache.For(ResistanceKey(input,workspace,options));
                    record["nuovi_valori_N"]=cache.Service.PrepareResistance(axialForces,()=>
                    {
                        var workerInput=(JsonObject)input.DeepClone();var workerWorkspace=(JsonObject)workspace.DeepClone();var workerOptions=(JsonObject)options.DeepClone();
                        var workerEngine=new CheckerSection(workerInput,workerWorkspace,workerOptions);
                        return new PileReinforcement(RcParameters(workerInput,details,workerWorkspace,workerEngine));
                    },table,cancellation,maximumParallelism,(done,total)=>progress?.Report(new($"Sezione {i+1}/{segments.Length} · MRd +/− ai valori di N locali: {done}/{total}",done,total)));
                    times["resistenze"]=JsonSerializer.SerializeToNode(cache.Service.ResistanceTimings);times["batch_mrd_totale"]=timing.Elapsed.TotalMilliseconds;
                }
                timing.Restart();var checks=result["risposta"]!.Array("Points").Where(p=>p.D("Depth")>=s.Start-1e-9&&p.D("Depth")<=s.End+1e-9).Select(p=>{cancellation.ThrowIfCancellationRequested();return cache.Service.Check(new PileAction{Depth=p.D("Depth"),Side=p.S("Side"),N=p.D("AxialForce"),V=p.D("Shear"),M=p.D("Moment")});}).ToArray();
                times["verifiche_nmv"]=timing.Elapsed.TotalMilliseconds;timing.Restart();
                if(input.S("tipo_trasversale")!="Staffe singole")foreach(var check in checks)
                    check.Message=check.Message.Replace("Confermare il modello di taglio circolare e z/d.",input.S("tipo_trasversale")=="Spirale"?"Taglio con spirale non verificato: modello resistente non implementato.":"Taglio non verificato: tipologia di armatura trasversale da definire.");
                record["verifiche"]=JsonSerializer.SerializeToNode(checks);record["critica"]=JsonSerializer.SerializeToNode(checks.OrderByDescending(c=>c.GoverningRatio??c.BendingRatio??double.PositiveInfinity).First());
                detailSegments.Add(new PileDetailSegment{Id=s.Id,Start=s.Start,End=s.End,Service=cache.Service,Checks=checks});
                record["stato"]=checks.Any(c=>c.Outcome==EngineeringOutcome.NotSatisfied)?"Non soddisfatto":checks.Any(c=>c.Execution==ExecutionStatus.Error)?"Non verificabile":checks.All(c=>c.Outcome==EngineeringOutcome.Satisfied)?"Soddisfatto N–M–V; dettagli da completare":"Parziale";
                if(checks.All(c=>c.Outcome==EngineeringOutcome.Satisfied))satisfiedSegments.Add(s.Id);
                record["barre_sezione"]=J.Node(cache.Engine.Geometry.Bars.Select(b=>new[]{b.X,b.Y,b.Area,b.Diametro}).ToArray());
                record["staffa_raggio_mm"]=cache.Service.LinkCentreRadius;
                record["contorno_sezione"]=J.Node(cache.Engine.Geometry.Outline);
                record["durabilita"]=PileDurability(input,details);record["comportamento_dettagli"]=details.S("elemento");
                progress?.Report(new($"Tratto {s.Id}: controlli N–M–V, dettagli e distinta"));
                record["normativa"]=ConcreteStandards.ResultName(workspace);
                var construction=cache.Service.ConstructionChecks(checks.Max(c=>c.Action.N));record["controlli_costruttivi"]=JsonSerializer.SerializeToNode(construction);
                if(construction.Any(c=>c.Passed==false)){record["stato"]="Non soddisfatto: dettaglio della sezione";satisfiedSegments.Remove(s.Id);}
                else if(construction.Any(c=>c.Passed==null)&&satisfiedSegments.Remove(s.Id))record["stato"]="Parziale: dettagli da completare";
                var linkStations=PileReinforcement.LinkStations(s.Start,s.End,input.Required("transverse_spacing_mm"),i==segments.Length-1);
                record["distinta_staffe"]=J.Obj(("diametro_mm",input["transverse_bar_diameter_mm"]!.DeepClone()),("passo_massimo_mm",input["transverse_spacing_mm"]!.DeepClone()),("quantita",linkStations.Length),("quote_m",J.Node(linkStations)),("stato","Posizioni nominali a passo uniforme non maggiore di quello assegnato. Confine interno assegnato al tratto successivo; sagomatura, ganci e confinamento dei giunti da completare."));
                var transverse=PileReinforcement.TransverseDetail(LinkKind(input.S("tipo_trasversale")),s.Start,s.End,input.Required("diameter_mm"),input.Required("cover_mm"),input.Required("transverse_bar_diameter_mm"),input.Required("transverse_spacing_mm"),i==segments.Length-1);
                record["armatura_trasversale"]=JsonSerializer.SerializeToNode(transverse);
                record["distinta_staffe"]!["tipo"]=input.S("tipo_trasversale");
                record["distinta_staffe"]!["quantita"]=transverse.Count;
                record["distinta_staffe"]!["quote_m"]=J.Node(transverse.Stations);
                record["distinta_staffe"]!["spire"]=transverse.Turns;
                record["distinta_staffe"]!["passo_effettivo_mm"]=transverse.ActualPitch;
                record["distinta_staffe"]!["lunghezza_geometrica_m"]=transverse.GeometricLength;
                record["distinta_staffe"]!["stato"]=transverse.Note;
                times["dettagli_distinta"]=timing.Elapsed.TotalMilliseconds;
            }
            catch(OperationCanceledException){throw;}
            catch(Exception ex){record["stato"]="Non verificabile";record["errore"]=ex.Message;}
        }
        var runs=PileReinforcement.SegmentRuns(detailSegments,result["input"].D("lunghezza"));foreach(var record in output)
        {
            var segmentRuns=runs.Where(r=>r.Segment.Split(" / ").Contains(record.S("id"))).ToArray();if(segmentRuns.Length==0)continue;
            record!["dettagli_barre"]=JsonSerializer.SerializeToNode(segmentRuns);
            var checks=detailSegments.Single(s=>s.Id==record.S("id")).Checks;
            PileReinforcement.ApplyDevelopment(segmentRuns,checks);
            record["verifiche"]=JsonSerializer.SerializeToNode(checks);
            record["critica"]=JsonSerializer.SerializeToNode(checks.OrderByDescending(c=>c.DevelopmentAvailable==false?double.PositiveInfinity:c.GoverningRatio??c.BendingRatio??double.PositiveInfinity).First());
            if(checks.Any(c=>c.DevelopmentAvailable==false)&&satisfiedSegments.Remove(record.S("id")))record["stato"]="Parziale: sviluppo barre insufficiente";
        }
        foreach(var record in output.OfType<JsonObject>())
        {
            var segment=detailSegments.FirstOrDefault(s=>s.Id==record.S("id"));
            if(segment!=null)
            {
                var seismic=SeismicSettings(root);seismic.Links=LinkKind(record["sezione"].S("tipo_trasversale"));
                var seismicResult=segment.Service.CheckHeadSeismic(seismic,segment.Start,segment.End,result["input"].D("lunghezza"),segment.Checks);
                record["sisma_testa"]=JsonSerializer.SerializeToNode(seismicResult);
                if(seismicResult.Active&&seismicResult.Checks.Any(c=>c.Passed==false)){record["stato"]="Non soddisfatto: controlli sismici §7.2.5";satisfiedSegments.Remove(record.S("id"));}
                else if(seismicResult.Active&&seismicResult.Checks.Any(c=>c.Passed==null)&&satisfiedSegments.Remove(record.S("id")))record["stato"]="Parziale: controlli sismici da completare";
            }
            var issues=PendingIssues(record,details);
            record["da_completare_tipizzati"]=JsonSerializer.SerializeToNode(issues);
            record["da_completare"]=J.Node(issues.Select(i=>i.Text).ToArray());
            record["riepilogo"]=ReinforcementSummary(record);
        }
        progress?.Report(new("Verifiche e distinta aggiornate",1,1));
        return J.Obj(("versione_distinta",3),("stato","Verifiche di sezione N–M e taglio; dettagli costruttivi preliminari"),("tratti",output),("distinta",JsonSerializer.SerializeToNode(runs)),("giunti",JsonSerializer.SerializeToNode(runs.SelectMany(r=>r.Joints).Distinct())),("impostazioni",details.DeepClone()),("ipotesi","NTC 2018; azioni concomitanti. MRd nominale a N locale, nei due versi. Taglio: sezione circolare equivalente, bw=D, d dai baricentri delle barre nei semicerchi, z/d assegnato e confermato. Staffe chiuse a 90°, due bracci; nessuna equivalenza automatica della spirale. Dettagli sismici di testa opzionali: risultati nel relativo riepilogo NTC §7.2.5. SLE, sisma globale, duttilità esplicita, confinamento dei giunti e progetto esecutivo esclusi. Barre sovrapposte non sommate alla resistenza nominale. Quote dei tratti come quote finali dei tagli: primo gruppo da 0 alla fine assegnata, successivi da inizio tratto−l0 alla fine assegnata. Nessuna fusione di tratti uguali né prolungamenti automatici oltre testa, confini o punta. Suddivisione interna solo per il limite commerciale. Sviluppo, abbinamento e resistenza restano verifiche distinte; le quote non certificano il dettaglio. Giunti generati al 100%, senza sfalsamento automatico."));
    }
    public static JsonArray ProposeSegments(JsonObject root,JsonObject result)
    {
        var checks=result["armature"]!.Array("tratti").SelectMany(t=>t!.Array("verifiche")).Select(c=>c!.Deserialize<PileSectionCheck>()!).ToArray();var d=root["elastico"]!["dettagli"]!;
        var runs=result["armature"]!.Array("distinta").Select(r=>r!.Deserialize<PileBarRun>()!).ToArray();
        var estimate=PileReinforcement.ProposeBuildable(checks,runs,result["input"].D("lunghezza"),Math.Min(d.Required("lunghezza_gabbia",strict:true),d.Required("lunghezza_barra",strict:true)),d.Required("lunghezza_minima",strict:true));
        var bounds=estimate.Boundaries;var proposal=new JsonArray();for(int i=1;i<bounds.Length;i++)
        {
            var row=NewSegment("T"+i,i==bounds.Length-1?null:bounds[i]);
            if(estimate.ChangeDepth.HasValue&&bounds[i-1]>=estimate.ChangeDepth.Value-1e-9){row["collegato"]=false;foreach(string key in ReinforcementKeys)row[key]=root["sezione"]![key]?.DeepClone();foreach(string key in InnerRingKeys)if(root["sezione"]![key] is JsonNode ring)row[key]=ring.DeepClone();row["origine_armatura"]="Sezione personalizzabile oltre Mmax/2; armatura iniziale principale, da dimensionare";}
            row["criterio_proposta"]=estimate.Explanation;row["quota_teorica_mmeta"]=estimate.HalfMomentDepth;row["quota_cambio_proposta"]=estimate.ChangeDepth;proposal.Add(row);
        }return proposal;
    }
    static string ResistanceKey(JsonObject input,JsonObject workspace,JsonObject options)
    {
        var key=(JsonObject)input.DeepClone();
        // These values do not enter geometry, constitutive laws or the N–M resistance solver.
        foreach(string name in new[]{"tipo_trasversale","transverse_spacing_mm","esposizione","vita_durabilita","qualita_copriferro","gamma_ca","gamma_acciaio","gamma_iniezione","ej_override","ej_assegnato","ej_motivo"})key.Remove(name);
        return key.ToJsonString()+workspace.ToJsonString()+options.ToJsonString();
    }
    public static JsonObject PileDurability(JsonObject input,JsonNode details)
    {
        var result=J.Obj(("esposizione",input.S("esposizione")),("origine","Esposizione, fck e vita utile dalla sezione principale"),("override",details.B("copriferro_override")));
        try
        {
            if(details.B("copriferro_override")){result["cmin_dur"]=details.Required("cmin_dur");result["origine"]="cmin,dur assegnato esplicitamente dall'utente";return result;}
            var exposure=input.S("esposizione");
            if(string.IsNullOrWhiteSpace(exposure))throw new ArgumentException("Classe di esposizione non assegnata.");
            var value=CoverRequirements.Calculate(DurabilityProfile.Ntc2018,new CoverInput([exposure],input.Required("fck_mpa"),(int)input.D("vita_durabilita",50),false,false,false,input.Required("longitudinal_bar_diameter_mm"),details.Required("aggregato"),details.Required("delta_c"),ntcQualityReduction:input.B("qualita_copriferro"),pertinentCmin:ExposureClasses.Uni11104MinimumStrength([exposure])));
            result["cmin_dur"]=value.Durability;result["fonte"]=value.Reference;result["vita_anni"]=input.D("vita_durabilita",50);result["qualita"]=input.B("qualita_copriferro");
        }
        catch(ArgumentException ex){result["errore"]="Copriferro non verificato: completare esposizione/durabilità nella sezione principale. "+ex.Message;}
        return result;
    }
    public static string ConstructionLabel(string key)=>key switch
    {
        "ClearSpacing"=>"Interferro", "NominalCover"=>"Copriferro nominale", "BarCoverMargin"=>"Copriferro barre longitudinali",
        "PileSteelArea"=>"Minimo armatura pali", "PileLinkDiameter"=>"Diametro staffe pali", "PileLinkSpacing"=>"Passo staffe pali",
        "LongitudinalDiameter"=>"Diametro longitudinale", "LongitudinalSpacing"=>"Interasse longitudinale", "MinimumLongitudinal"=>"Armatura minima pilastro (NEd locale)",
        "MaximumLongitudinal"=>"Armatura massima", "LinkDiameter"=>"Diametro staffe pilastro", "LinkSpacing"=>"Passo staffe pilastro",
        "BarsHeldByLinks"=>"Trattenimento barre compresse: disposizione staffe da confermare", "LinkSpacingNearBeams"=>"Staffe nelle zone di estremità",
        "MemberRulesExcluded"=>"Regole di trave/pilastro escluse", _=>key
    };
    private enum PileIssueKind { Pending, Failure, Excluded }
    private sealed record PileIssue(PileIssueKind Kind,string Text,string Summary);
    private static PileIssue[] PendingIssues(JsonObject record,JsonNode settings)
    {
        var items=new List<PileIssue>();
        void Add(string text)=>items.Add(new(PileIssueKind.Pending,text,text));
        void Failure(string text,string? summary=null)=>items.Add(new(PileIssueKind.Failure,text,summary??text));
        if(record["errore"]!=null)Add("Non verificabile: "+record.S("errore"));
        if(!settings.B("azioni_progetto"))Add("Da confermare: natura di progetto delle azioni H, N e del peso proprio.");
        if(!settings.B("taglio_confermato"))Add("Taglio non verificato: confermare il modello circolare equivalente e z/d.");
        if(record["sezione"].S("tipo_trasversale")=="Spirale")Add("Taglio con spirale non verificato: il motore corrente verifica soltanto staffe singole chiuse. Il disegno della spirale non costituisce verifica resistente.");
        if(record["sezione"].S("tipo_trasversale")=="Da definire")Add("Armatura trasversale da definire: scegliere staffe singole o spirale.");
        foreach(var c in record.Array("controlli_costruttivi").Where(c=>c?["Passed"]==null||!c.B("Passed")))
        { string label=ConstructionLabel(c.S("Key"));if(c?["Passed"]==null)Add("Da completare: "+label);else Failure("NON SODDISFATTO: "+label,label); }
        if(record["durabilita"]?["errore"]!=null)Add(record["durabilita"].S("errore"));
        if(record.Array("verifiche").Any(c=>c?["MRdPositive"]==null||c?["MRdNegative"]==null))Add("N–M non verificabile ad alcune quote: sforzo normale fuori dominio o mancata convergenza.");
        if(record.Array("verifiche").Any(c=>J.Number(c?["BendingRatio"])>1))Failure("NON SODDISFATTO: pressoflessione N–M.","pressoflessione N–M.");
        if(record.Array("verifiche").Any(c=>J.Number(c?["ShearRatio"])>1))Failure("NON SODDISFATTO: taglio.","taglio.");
        if(record.Array("verifiche").Any(c=>c?["DevelopmentAvailable"]!=null&&!c.B("DevelopmentAvailable")))Failure("Sviluppo barre insufficiente ad alcune quote: MRd nominale non utilizzabile.");
        foreach(var run in record.Array("dettagli_barre"))
        {
            if(run!.Array("Joints").Any(j=>!j.B("LengthPassed")||!j.B("ArrangementPassed")))Failure("Giunti non utilizzabili nel gruppo "+run.S("Id")+": lunghezza, sovrapposizione fra giunti o distanza trasversale da correggere.");
            if(run.Array("Joints").Count>0&&settings.Required("percentuale_sovrapposta")<100)Add("Giunti calcolati al 100%; lo sfalsamento richiesto non è disposto automaticamente.");
        }
        foreach(var c in record["sisma_testa"].Array("Checks").Where(c=>c?["Passed"]==null||!c.B("Passed")))
        { string text=c.S("Title")+". "+c.S("Criterion");if(c?["Passed"]==null)Add("Da completare: "+text);else Failure("NON SODDISFATTO: "+text,text); }
        string excluded=settings.B("sisma_testa")?"combinazioni sismiche automatiche, interazione cinematica, zone dissipative profonde, duttilità esplicita, nodo palo-plinto, SLE, confinamento giunti e ganci esecutivi. I controlli sismici di testa §7.2.5 sono riportati separatamente.":"SLE, sisma/zone dissipative, confinamento giunti, ganci e dettagli esecutivi delle staffe.";
        items.Add(new(PileIssueKind.Excluded,"Non eseguiti: "+excluded,excluded));return items.Distinct().ToArray();
    }
    public static PileSeismicSettings SeismicSettings(JsonObject root)
    {
        var d=root["elastico"]!["dettagli"]!;
        if(!d.B("sisma_testa"))return new PileSeismicSettings();
        double? length=string.IsNullOrWhiteSpace(d.S("sisma_lunghezza"))?null:J.Number(d["sisma_lunghezza"])??throw new ArgumentException("Lunghezza zona sismica non valida.");
        return new PileSeismicSettings{Enabled=d.B("sisma_testa"),HeadLength=length,SeismicActionsConfirmed=d.B("sisma_azioni"),ElasticMomentConfirmed=d.B("sisma_elastico"),StandardNtc2018=!root["sezione"].B("coefficienti_unitari"),Links=d.S("sisma_staffe","Da definire") switch{"Da definire"=>PileSeismicLinks.Unspecified,"Staffe singole"=>PileSeismicLinks.SingleHoops,"Spirale"=>PileSeismicLinks.Spiral,_=>throw new ArgumentException("Tipologia di staffe sismiche non riconosciuta.")}};
    }
    static PileSeismicLinks LinkKind(string kind)=>kind switch{"Staffe singole"=>PileSeismicLinks.SingleHoops,"Spirale"=>PileSeismicLinks.Spiral,_=>PileSeismicLinks.Unspecified};
    static PileRcParameters RcParameters(JsonObject input,JsonNode details,JsonObject workspace,CheckerSection engine)
    {
        var standard=ConcreteStandards.Effective(input,workspace);var strengths=ConcreteMaterials.DesignValues(input,workspace);
        return new PileRcParameters{Solver=engine.Checker.SectionSolver,Section=engine.Section,Axes=engine.Local,Standard=standard,Diameter=input.Required("diameter_mm"),Fck=input.Required("fck_mpa"),Fctm=ConcreteMaterials.Concrete(input).Fctm,Fyk=input.Required("fyk_mpa"),Fctk05=GPC.Checkers.Concrete.Detailing.AnchorageCalculator.BondFctk05(input.D("fck_mpa")),Fcd=strengths.Fcd,Fyd=strengths.Fyd,Es=input.Required("steel_modulus_mpa"),Cover=input.Required("cover_mm"),LinkDiameter=input.Required("transverse_bar_diameter_mm"),LinkSpacing=input.Required("transverse_spacing_mm"),LeverFactor=details.Required("z_d",strict:true),ShearModelConfirmed=details.B("taglio_confermato")&&input.S("tipo_trasversale","Staffe singole")=="Staffe singole",DesignActionsConfirmed=details.B("azioni_progetto"),PileMinimumRequirements=details.B("minimi_pali"),DetailingMode=details.S("elemento","Pilastro") switch{"Pilastro"=>PileDetailingMode.Column,"Trave"=>PileDetailingMode.Beam,"Solo controlli comuni"=>PileDetailingMode.CommonOnly,_=>throw new ArgumentException("Comportamento dettagli non riconosciuto")},CompressionBarsRestrained=details.B("barre_trattenute"),EndZonesConfirmed=details.B("zone_estremita"),GoodBond=details.B("aderenza_buona"),LapPercent=details.Required("percentuale_sovrapposta",strict:true),LapClearDistance=details.Required("distanza_barre"),HeadAnchorage=details.Required("ancoraggio_testa"),ToeAnchorage=details.Required("ancoraggio_punta"),StockLength=details.Required("lunghezza_barra",strict:true),LapDiameterFactor=details.Required("fattore_sovrapposizione",strict:true),Aggregate=details.Required("aggregato"),CoverDeviation=details.Required("delta_c"),MinimumDurabilityCover=J.Number(PileDurability(input,details)["cmin_dur"])};
    }
    public static JsonObject DesignSegment(JsonObject root,JsonObject result,int index,int[] counts,double[] spacings)
        =>DesignSegment(root,result,index,counts,[SegmentSection(root,root["elastico"]!.Array("tratti")[index]!.AsObject()).D("longitudinal_bar_diameter_mm")],[SegmentSection(root,root["elastico"]!.Array("tratti")[index]!.AsObject()).D("transverse_bar_diameter_mm")],spacings);
    public static JsonObject DesignSegment(JsonObject root,JsonObject result,int index,int[] counts,double[] diameters,double[] linkDiameters,double[] spacings,CancellationToken cancellation=default,IProgress<PileCalculationProgress>? progress=null,bool legacy=false,int maximumParallelism=0)
    {
        if(!legacy)return DesignSegmentWithLibrary(root,result,index,counts,diameters,linkDiameters,spacings,cancellation,progress,maximumParallelism);
        var source=root["elastico"]!.Array("tratti")[index]!.AsObject();var segment=ReadSegments(root)[index];var settings=root["elastico"]!["dettagli"]!;
        if(!settings.B("azioni_progetto")||!settings.B("taglio_confermato"))throw new ArgumentException("Confermare azioni di progetto e modello di taglio nelle ipotesi.");
        var seismicSettings=SeismicSettings(root);seismicSettings.Links=LinkKind(TransverseKind(root,source));
        if(TransverseKind(root,source)!="Staffe singole")throw new ArgumentException("Il dimensionamento a taglio richiede staffe singole: modello resistente della spirale non implementato.");
        if(seismicSettings.Enabled&&(!seismicSettings.SeismicActionsConfirmed||!seismicSettings.ElasticMomentConfirmed||seismicSettings.Links==PileSeismicLinks.Unspecified||!seismicSettings.StandardNtc2018))throw new ArgumentException("Dimensionamento sismico: confermare azioni sismiche, momento elastico q=1, tipologia staffe e coefficienti ordinari NTC.");
        var workspace=ConcreteStandards.PileWorkspace(root["sezione"]!.AsObject());var options=J.Obj(("criterio","N costante"),("assi","Locali"),("strategia","Iterativo"),("modello","Non lineare"));
        var actions=result["risposta"]!.Array("Points").Where(p=>p.D("Depth")>=segment.Start-1e-9&&p.D("Depth")<=segment.End+1e-9).Select(p=>new PileAction{Depth=p.D("Depth"),Side=p.S("Side"),N=p.D("AxialForce"),V=p.D("Shear"),M=p.D("Moment")}).ToArray();
        if(actions.Length==0)throw new ArgumentException("Nessuna azione disponibile sul tratto.");
        var screen=new[]{actions.MaxBy(a=>Math.Abs(a.M))!,actions.MaxBy(a=>Math.Abs(a.V))!,actions.MinBy(a=>a.N)!,actions.MaxBy(a=>a.N)!}.Distinct().ToArray();
        var exclusions=new List<string>();var tables=new ElasticPileVerificationCache();int evaluated=0;
        var layouts=PileReinforcement.Layouts(counts,diameters,linkDiameters,spacings);
        foreach(var layout in layouts)
        {
            cancellation.ThrowIfCancellationRequested();evaluated++;progress?.Report(new($"Disposizione {evaluated}/{layouts.Count}: {layout.Count}φ{layout.Diameter}, staffe φ{layout.LinkDiameter}/{layout.LinkSpacing}",evaluated-1,layouts.Count));
            var input=SegmentSection(root,source);input["longitudinal_bar_count"]=layout.Count;input["longitudinal_bar_diameter_mm"]=layout.Diameter;input["transverse_bar_diameter_mm"]=layout.LinkDiameter;input["transverse_spacing_mm"]=layout.LinkSpacing;
            try
            {
                PileReinforcement Create(){var c=(JsonObject)input.DeepClone();return new PileReinforcement(RcParameters(c,settings,workspace,new CheckerSection(c,(JsonObject)workspace.DeepClone(),(JsonObject)options.DeepClone())));}
                var service=Create();if(service.ConstructionChecks(actions.Max(a=>a.N)).Any(c=>c.Passed==false))continue;
                if(seismicSettings.Enabled&&service.CheckHeadSeismic(seismicSettings,segment.Start,segment.End,result["input"].D("lunghezza"),[]).Checks.Any(c=>c.Passed==false))continue;
                if(screen.Any(a=>service.Check(a).Outcome!=EngineeringOutcome.Satisfied))continue;
                service.PrepareResistance(actions.Select(a=>a.N),Create,tables.For(input.ToJsonString()),cancellation,progress:(done,total)=>progress?.Report(new($"Disposizione {evaluated}/{layouts.Count} · MRd: {done}/{total} valori N",done,total)));
                var verified=actions.Select(service.Check).ToArray();if(verified.Any(c=>c.Outcome!=EngineeringOutcome.Satisfied))continue;
                if(seismicSettings.Enabled&&service.CheckHeadSeismic(seismicSettings,segment.Start,segment.End,result["input"].D("lunghezza"),verified).Checks.Any(c=>c.Passed!=true))continue;
                var proposal=new JsonObject();foreach(string key in ReinforcementKeys)proposal[key]=input[key]!.DeepClone();proposal["candidati_esaminati"]=evaluated;proposal["esclusioni_geometriche"]=J.Node(exclusions);proposal["criterio"]="Ricerca discreta Checker: As crescente, poi quantità di staffe per metro crescente. N–M–V concomitanti a tutte le ascisse e controlli geometrici/minimi disponibili. Copriferro senza cmin,dur e dettagli di sviluppo restano da completare.";return proposal;
            }
            catch(ArgumentException ex){exclusions.Add($"{layout.Count}φ{layout.Diameter}, staffe φ{layout.LinkDiameter}/{layout.LinkSpacing}: {ex.Message}");}
        }
        throw new ArgumentException("Nessuna disposizione del catalogo soddisfa N–M–V, interferro e minimi selezionati. Ampliare il catalogo.");
    }
}
