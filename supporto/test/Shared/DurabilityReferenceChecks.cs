using Anthea.Calculations;

namespace Materiali;

/// <summary>
/// Reference checks of durability and NTC cover, formerly Durability.Check and NtcCover.Check in
/// X.Calculations/Materials (refactoring F1.3). The bodies are unchanged; only the calls are qualified.
/// Linked into CalculationLibrary.Checks, X.Verifiche (--project-calculations), tests/ConcreteLibraryAdapter.Checks (11h) and, in the
/// UiTests configuration, into X.Materiali (MaterialView.Check, --smoke-materials).
/// Refactoring F2.9 (E3): the checks go through the facades with the given durability engine of ConcreteDurabilityAdapter (null = the
/// default engine); the expected values (by hand) are unchanged.
/// </summary>
internal static class DurabilityReferenceChecks
{
    internal static void CheckDurability(DurabilityEngine? engine = null)
    {
        static void Equal(double a,double b) { if(Math.Abs(a-b)>1e-8) throw new Exception($"Atteso {b}, ottenuto {a}"); }
        static void Reject(Action action) { try { action(); } catch(ArgumentException) {return;} throw new Exception("Input non valido accettato."); }
        Exposure E(string code)=>ConcreteDurabilityAdapter.Exposures(engine).Single(x=>x.Code==code);
        var p=new CoverInput(50,false,false,false,16,20,10,false,0,0);
        Equal(Durability.Cover([E("XC1")],30,p,engine).Nominal,26);
        Equal(Durability.Cover([E("XC4")],30,p,engine).Nominal,40);
        Equal(Durability.Cover([E("XC4"),E("XS3"),E("XF4")],35,p,engine).Nominal,55);
        Equal(Durability.Cover([E("XC1")],30,p with{Diameter=32,Aggregate=32},engine).Bond,32);
        Equal(Durability.Cover([E("XC1")],30,p with{Diameter=32,Aggregate=40},engine).Bond,37);
        Equal(Durability.Cover([E("XC1")],30,p with{Ground=75},engine).Nominal,75);
        Equal(Durability.Cover([E("XC4")],30,p with{Life=100},engine).Nominal,50);
        Equal(Durability.Cover([E("XC4")],30,p with{Rough=true,Abrasion=10},engine).Nominal,55);
        Equal(Durability.StructuralClass(E("XD2"),40,p with{StrengthReduction=true},engine),3);
        Equal(Durability.StructuralClass(E("XS2"),40,p with{StrengthReduction=true},engine),4);
        Equal(Durability.StructuralClass(E("XC1"),30,p with{StrengthReduction=true,Slab=true,Quality=true},engine),1);
        Equal(Durability.EffectiveWater(190,10)/360,.5);
        Reject(()=>Durability.Cover([E("XF4")],30,p,engine)); Reject(()=>Durability.Cover([E("X0"),E("XC1")],30,p,engine));
        Reject(()=>Durability.Cover([],30,p,engine)); Reject(()=>Durability.EffectiveWater(10,20)); Reject(()=>Durability.EffectiveWater(double.NaN,0));
        Reject(()=>Durability.Cover([E("XC1")],30,p with{Diameter=-1},engine));
        Equal(ConcreteDurabilityAdapter.Exposures(engine).Length,18); Equal(E("XS3").MinCement,340); Equal(E("XD2").MaxRatio!.Value,.55); Equal(E("XA3").MinCement,360);
    }

    internal static void CheckNtcCover(DurabilityEngine? engine = null)
    {
        Exposure E(string code)=>ConcreteDurabilityAdapter.Exposures(engine).Single(e=>e.Code==code);
        var p=new CoverInput(50,false,false,false,16,20,10,false,0,0);
        static void Equal(double actual,double expected){if(Math.Abs(actual-expected)>1e-9)throw new Exception($"NTC copriferro: {actual}, atteso {expected}");}
        Equal(NtcCover.Calculate([E("XF2")],30,p,true,false,null,engine).Cover.Nominal,40);
        Equal(NtcCover.Calculate([E("XF2")],30,p,false,false,null,engine).Cover.Nominal,45);
        Equal(NtcCover.Calculate([E("XF2")],40,p,false,false,null,engine).Cover.Nominal,40);
        Equal(NtcCover.Calculate([E("XF2")],25,p,false,false,null,engine).Cover.Nominal,50);
        Equal(NtcCover.Calculate([E("XF2")],25,p,false,false,25,engine).Cover.Nominal,45);
        Equal(NtcCover.Calculate([E("XF2")],30,p with{Life=100},false,false,null,engine).Cover.Nominal,55);
        Equal(NtcCover.Calculate([E("XF2")],30,p,false,true,null,engine).Cover.Nominal,40);
        Equal(NtcCover.Calculate([E("XF2"),E("XS3")],35,p,false,false,null,engine).Cover.Nominal,55);
        Equal(NtcCover.Calculate([E("XA3")],45,p,true,false,null,engine).Cover.Nominal,45);
        Equal(NtcCover.Calculate([E("XF1")],35,p,true,false,null,engine).Cover.Nominal,26);
        Equal(NtcCover.Calculate([E("XF2")],30,p with{Diameter=50},false,false,null,engine).Cover.Nominal,60);
        Equal(NtcCover.Calculate([E("XF2")],30,p with{Ground=75},false,false,null,engine).Cover.Nominal,75);
        Equal(NtcCover.Calculate([E("XF2")],30,p with{StrengthReduction=true,Slab=true,Quality=true},false,false,null,engine).Cover.Nominal,45);
        foreach(var e in ConcreteDurabilityAdapter.Exposures(engine)) _=NtcCover.Calculate([e],30,p,false,false,null,engine);
        try{NtcCover.Calculate([],30,p,false,false,null,engine);throw new Exception("Esposizione vuota accettata");}catch(ArgumentException){}
    }
}
