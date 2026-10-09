using System.Collections.Immutable;

namespace Materiali;

/// <summary>
/// Nucleo legacy della durabilità (refactoring F2.9, passo E1): i corpi di Durability, NtcCover, MinimumConcrete e AtecapMix spostati
/// qui invariati, in classi annidate con gli stessi nomi, così le chiamate interne si risolvono come prima. Il catalogo delle esposizioni
/// è una copia privata immutabile (prima un array pubblico). Le facciate con i nomi pubblici (Materials/Durability.cs, NtcCover.cs,
/// MinimumConcrete.cs, MixAutomation.cs) non contengono calcoli. Il nucleo resta raggiungibile fino a F2.11 [CP]; fuori dall'adattatore
/// e dall'elenco ammesso (tests/ConcreteLibraryAdapter.Checks/legacy-allowlist-durabilita.json) non si usa (prova 11i).
/// </summary>
public static class DurabilityLegacy
{
    public static class Durability
    {
        // UNI EN 206-1:2006, prospetti 1 e F.1. F.1 è informativo.
        static readonly ImmutableArray<Exposure> Catalog =
        [
            new("X0", "Assenza di rischio; per armature: ambiente molto asciutto", null,12,0,0,0),
            new("XC1", "Carbonatazione · asciutto o sempre bagnato", .65,20,260,0,1),
            new("XC2", "Carbonatazione · bagnato, raramente asciutto", .60,25,280,0,2),
            new("XC3", "Carbonatazione · umidità moderata", .55,30,280,0,2),
            new("XC4", "Carbonatazione · alternanza bagnato/asciutto", .50,30,300,0,3),
            new("XD1", "Cloruri non marini · umidità moderata", .55,30,300,0,4),
            new("XD2", "Cloruri non marini · bagnato, raramente asciutto", .55,30,300,0,5),
            new("XD3", "Cloruri non marini · alternanza bagnato/asciutto", .45,35,320,0,6),
            new("XS1", "Cloruri marini · aerosol, senza contatto diretto", .50,30,300,0,4),
            new("XS2", "Cloruri marini · immersione permanente", .45,35,320,0,5),
            new("XS3", "Cloruri marini · maree, spruzzi e onde", .45,35,340,0,6),
            new("XF1", "Gelo · saturazione moderata, senza disgelanti", .55,30,300,0,-1),
            new("XF2", "Gelo · saturazione moderata, con disgelanti", .55,25,300,4,-1),
            new("XF3", "Gelo · saturazione elevata, senza disgelanti", .50,30,320,4,-1),
            new("XF4", "Gelo · saturazione elevata, disgelanti o mare", .45,30,340,4,-1),
            new("XA1", "Attacco chimico · debole", .55,30,300,0,-1),
            new("XA2", "Attacco chimico · moderato", .50,30,320,0,-1),
            new("XA3", "Attacco chimico · forte", .45,35,360,0,-1)
        ];
        public static ImmutableArray<Exposure> Exposures => Catalog;
        // EN 1992-1-1:2004, prospetto 4.4N: armatura ordinaria. Tabella privata, eccezione dichiarata della prova 11g fino a F2.11.
        static readonly int[,] Covers = {
            {10,10,10,15,20,25,30}, {10,10,15,20,25,30,35},
            {10,10,20,25,30,35,40}, {10,15,25,30,35,40,45},
            {15,20,30,35,40,45,50}, {20,25,35,40,45,50,55}
        };
        public static string Strength(int fck) => fck switch {12=>"C12/15",20=>"C20/25",25=>"C25/30",30=>"C30/37",35=>"C35/45",_=>"C"+fck};
        public static void ValidateExposure(Exposure[] values)
        {
            if(values.Length==0) throw new ArgumentException("Selezionare almeno una classe di esposizione.");
            if(values.Length>1 && values.Any(x=>x.Code=="X0")) throw new ArgumentException("X0 non è combinabile con altre esposizioni.");
        }
        public static int StructuralClass(Exposure e, double fck, CoverInput p)
        {
            // La soglia XD2 è C40/50, XS2 C45/55 (tabella 4.3N).
            int threshold=e.Code switch {"X0" or "XC1"=>30,"XC2" or "XC3"=>35,"XC4" or "XD1" or "XD2" or "XS1"=>40,_=>45};
            return Math.Max(1,4+(p.Life==100?2:0)-(p.StrengthReduction && fck>=threshold?1:0)-(p.Slab?1:0)-(p.Quality?1:0));
        }
        public static CoverResult Cover(Exposure[] values, double fck, CoverInput p)
        {
            ValidateExposure(values);
            if(!double.IsFinite(fck) || fck<12 || fck>90) throw new ArgumentException("Resistenza del calcestruzzo non valida.");
            if(p.Life is not (50 or 100) || !double.IsFinite(p.Diameter) || p.Diameter<=0 || !double.IsFinite(p.Aggregate) || p.Aggregate<=0 || !double.IsFinite(p.Deviation) || p.Deviation<0 || p.Abrasion is not (0 or 5 or 10 or 15) || p.Ground is not (0 or 40 or 75))
                throw new ArgumentException("Controllare diametro, Dmax, tolleranza e condizioni di getto.");
            var lines=values.Where(e=>e.CoverColumn>=0).Select(e=>new CoverLine(e.Code,StructuralClass(e,fck,p),Covers[StructuralClass(e,fck,p)-1,e.CoverColumn])).ToArray();
            if(lines.Length==0) throw new ArgumentException("Per il copriferro aggiungere l'esposizione alla corrosione (XC, XD o XS); XF/XA da sole non definiscono cmin,dur.");
            double bond=p.Diameter+(p.Aggregate>32?5:0),dur=lines.Max(x=>x.Durability);
            double min=Math.Max(10,Math.Max(bond,dur))+(p.Rough?5:0)+p.Abrasion;
            return new(bond,dur,min,Math.Max(min+p.Deviation,p.Ground),lines);
        }
        public static double EffectiveWater(double total,double absorbed)
        {
            if(!double.IsFinite(total)||!double.IsFinite(absorbed)||total<0||absorbed<0||absorbed>total) throw new ArgumentException("Acqua: richiedere 0 ≤ acqua assorbita ≤ acqua totale.");
            return total-absorbed;
        }
    }

    public static class NtcCover
    {
        public static int Severity(string code)=>code switch
        {
            "X0" or "XC1" or "XC2" or "XC3" or "XF1"=>0,
            "XC4" or "XD1" or "XS1" or "XA1" or "XA2" or "XF2" or "XF3"=>1,
            "XD2" or "XD3" or "XS2" or "XS3" or "XA3" or "XF4"=>2,
            _=>throw new ArgumentException("Esposizione non riconosciuta.")
        };
        public static double DefaultCmin(Exposure[] values)
        {
            Durability.ValidateExposure(values);
            return 25+5*values.Max(e=>Severity(e.Code));
        }
        public static NtcCoverResult Calculate(Exposure[] values,double fck,CoverInput p,bool plate,bool coverQuality,double? pertinentCmin=null)
        {
            Durability.ValidateExposure(values);
            // Convalida geometria e parametri tramite il ramo EC2, senza usarne la durabilità.
            _=Durability.Cover([Durability.Exposures[0]],fck,p);
            int severity=values.Max(e=>Severity(e.Code));
            double c0=35+5*severity,cmin=pertinentCmin??DefaultCmin(values);
            if(!double.IsFinite(cmin)||cmin<12||cmin>c0) throw new ArgumentException("Cmin pertinente: indicare fck tra 12 MPa e C0 = "+c0+" MPa.");
            double table=(plate?15:20)+10*severity+(fck<c0?5:0);
            double life=p.Life==100?10:0,low=fck<cmin?5:0,quality=coverQuality?5:0;
            double dur=table+life+low-quality;
            double bond=p.Diameter+(p.Aggregate>32?5:0);
            double minimum=Math.Max(10,Math.Max(bond,dur))+(p.Rough?5:0)+p.Abrasion;
            var result=new CoverResult(bond,dur,minimum,Math.Max(minimum+p.Deviation,p.Ground),[]);
            return new(new[]{"Ordinario","Aggressivo","Molto aggressivo"}[severity],severity,cmin,c0,table,life,low,quality,result);
        }
    }

    public static class MinimumConcrete
    {
        // Classe di resistenza minima per esposizione: UNI 11104:2025 (in vigore dal 24/07/2025), prospetto 6, riga «Classe di
        // resistenza minima». Valori trascritti dalla UNI 11104:2016, prospetto 5 (riportato in ATECAP 2020, p. 19), uguali a quelli
        // della 2025 salvo XF1: qui C32/40 dell'edizione 2016, nel prospetto 6 della 2025 C30/37 (registro D7-e, da decidere).
        public static int Fck(string code)=>code switch
        {
            "X0"=>12,
            "XC1" or "XC2" or "XF2" or "XF3"=>25,
            "XC3" or "XD1" or "XF4" or "XA1"=>30,
            "XC4" or "XS1" or "XD2" or "XF1" or "XA2"=>32,
            "XS2" or "XS3" or "XD3" or "XA3"=>35,
            _=>throw new ArgumentException("Classe di esposizione non riconosciuta.")
        };
        public static int Required(Exposure[] exposures)
        {
            Durability.ValidateExposure(exposures);return exposures.Max(e=>Fck(e.Code));
        }
        public static string Label(int fck)=>fck switch {12=>"C12/15",25=>"C25/30",30=>"C30/37",32=>"C32/40",35=>"C35/45",_=>throw new ArgumentException("Classe minima non riconosciuta.")};
    }

    public static class AtecapMix
    {
        // A/C massimo e dosaggio minimo di cemento: UNI 11104:2016, prospetto 5 (riportato in ATECAP 2020, p. 19), celle unite trascritte
        // per ogni esposizione. Il prospetto 6 della UNI 11104:2025 in vigore ha dosaggi minimi inferiori in tutte le classi e per XF1
        // A/C 0,55 e 300 kg/m³: i valori qui sono più restrittivi (registro D7-e, da decidere).
        public static (double? Ratio,int? Cement) Limits(string code)=>code switch
        {
            "X0" => (null,null),
            "XC1" or "XC2" => (.60,300),
            "XC3" or "XD1" or "XA1" => (.55,320),
            "XC4" or "XS1" or "XD2" or "XA2" or "XF2" or "XF3" => (.50,340),
            "XF1" => (.50,320),
            "XS2" or "XS3" or "XD3" or "XF4" or "XA3" => (.45,360),
            _ => throw new ArgumentException("Esposizione non riconosciuta.")
        };
        public static (double? Ratio,int? Cement) Required(Exposure[] active)
        {
            Durability.ValidateExposure(active);
            return (active.Min(e=>Limits(e.Code).Ratio),active.Max(e=>Limits(e.Code).Cement));
        }
        public static double? Air(Exposure[] active,double dmax)
        {
            Durability.ValidateExposure(active);
            if(!double.IsFinite(dmax)||dmax<=0)throw new ArgumentException("Dmax non valido.");
            if(!active.Any(e=>e.Code is "XF2" or "XF3" or "XF4"))return null;
            return dmax>20?4:dmax>=12&&dmax<=16?5:null;
        }
    }
}
