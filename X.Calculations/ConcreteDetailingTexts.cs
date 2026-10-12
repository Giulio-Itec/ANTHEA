using NativeCheck = GPC.Checkers.Concrete.Detailing.DetailingCheck;

namespace Anthea.Calculations;

/// <summary>Italian presentation of stable Checker keys; all numbers and verdicts come from the library.</summary>
internal static class ConcreteDetailingTexts
{
    const string Ntc = "NTC 2018 §4.1.6.1";
    public static DetailingCheck Translate(NativeCheck r)
    {
        string key = r.Key.Split(':')[0], face = r.Key.EndsWith(":Top", StringComparison.Ordinal) ? "superiore" : "inferiore";
        bool pending = r.Passed is null;
        var (name, reference, text) = key switch
        {
            "ClearSpacing" => ("Interferro minimo", Ntc+".3 / EC2 §8.2", "max(20 mm; Ømax; dg + 5 mm), controllo conservativo su tutte le coppie."),
            "NominalCover" => ("Copriferro nominale", Ntc+(pending ? ".3" : ".3 / EC2 §4.4"), pending ? "Completare esposizione SLE e parametri di durabilità per calcolare cmin,dur." : "max(10; cmin,dur; cmin,b) + Δcdev. cmin,dur proviene dal progetto di durabilità."),
            "BarCoverMargin" => ("Margine copriferro barre longitudinali", Ntc+".3 / EC2 §4.4", "Minimo (copriferro geometrico della barra − copriferro richiesto), su contorno esterno e foro. Stessa classe di esposizione per tutte le superfici."),
            "LongitudinalDiameter" => ("Diametro longitudinale", Ntc+".2", "Ømin ≥ 12 mm"),
            "LongitudinalSpacing" => ("Interasse longitudinale", Ntc+".2", pending ? "Disposizione non riconosciuta automaticamente." : "Interasse sulle facce / sul perimetro ≤ 300 mm."),
            "MinimumLongitudinal" => ("Armatura longitudinale minima", Ntc+".2", "max(0,10 NEd/fyd; 0,003 Ac), NEd massimo di compressione fra le combinazioni."),
            "MaximumLongitudinal" => ("Armatura longitudinale massima", Ntc+".2", "As ≤ 0,04 Ac fuori dalle sovrapposizioni."),
            "LinkDiameter" => ("Diametro staffe", Ntc+".2", "max(6 mm; Ølong,max/4)"),
            "LinkSpacing" when !r.Key.Contains(':') => ("Passo staffe", Ntc+".2", "min(250 mm; 12 Ølong,min)"),
            "MinimumTension" when r.Explanation == "No bar in this half of the section." => ("As,min "+face, Ntc+".1", "Nessuna barra nella metà di sezione considerata."),
            "MinimumTension" => ("As,min "+face, Ntc+".1 / EC2 §9.3.1.1", "max(0,26 fctm/fyk; 0,0013) bt d; entrambe le facce potenzialmente tese."),
            "MaximumTension" => ("As,max "+face, Ntc+".1", "As della singola zona ≤ 0,04 Ac."),
            "MinimumLinks" => ("Staffe minime · "+face, Ntc+".1", "Ast/s ≥ 1,5 b mm²/m"),
            "LinkSpacing" => ("Passo staffe · "+face, Ntc+".1", "Almeno 3 staffe/m; passo ≤ 0,8 d."),
            "MainSpacing" => ("Interasse armatura principale", "EC2 §9.3.1.1", "Regione critica: min(2h;250); altre zone: min(3h;400)."),
            "SecondaryReinforcement" => ("Armatura secondaria", "EC2 §9.3.1.1", "As,secondaria ≥ 20% As,principale; dati totali delle due facce per metro di larghezza."),
            "SecondaryFaceDistribution" => ("Ripartizione armatura secondaria sulle facce", "EC2 §9.3.1.1", "Il dato totale non verifica da solo il 20% su ciascuna faccia: controllare la distribuzione effettiva."),
            "SecondarySpacing" => ("Interasse armatura secondaria", "EC2 §9.3.1.1", pending ? "Inserire la spaziatura nella direzione ortogonale." : "Regione critica: min(3h;400); altre zone: min(3,5h;450)."),
            "EdgesSupportsPunching" => ("Bordi, appoggi e punzonamento", "EC2 §§9.3–9.4 / NTC §4.1.2.3.5.4", "Verifiche locali richiedono geometria dell’elemento e degli appoggi; non desumibili dalla striscia di sezione."),
            "CompressionBarRestraint" => ("Trattenimento barre compresse", Ntc+".1", "Passo ≤ 15Ø delle barre compresse considerate resistenti."),
            "EndSupportAnchorage" => ("Ancoraggio negli appoggi di estremità", Ntc+".1", "Confermare la verifica della traslazione delle trazioni e dell’armatura inferiore ancorata."),
            "WallVertical" => ("Armatura verticale parete", "EC2 §9.6.2", "As,v ≥ 0,002 Ac, da distribuire sulle due facce."),
            "WallVerticalMaximum" => ("Armatura verticale massima", "EC2 §9.6.2", "As,v ≤ 0,04 Ac fuori dalle sovrapposizioni."),
            "WallVerticalSpacing" => ("Interasse verticale sulla sezione", "EC2 §9.6.2", pending ? "Disposizione non riconosciuta automaticamente." : "s ≤ min(3 volte lo spessore; 400 mm)."),
            "WallHorizontal" => ("Armatura orizzontale per metro", "EC2 §9.6.3", "max(0,25 As,v; 0,001 Ac), riportato a 1 m di parete."),
            "WallHorizontalSpacing" => ("Interasse orizzontale", "EC2 §9.6.3", pending ? "Inserire la spaziatura delle barre orizzontali." : "s ≤ 400 mm"),
            "WallFacesAndTies" => ("Distribuzione e collegamenti fra facce", "EC2 §9.6", "Verificare distribuzione su entrambe le facce e legature trasversali; non descritti dalle sole barre longitudinali."),
            "BarsHeldByLinks" => ("Barre trattenute dalle staffe", Ntc, "Confermare la disposizione effettiva di staffe e legature sulle barre compresse."),
            "MaximumAtLap" => ("Armatura massima nella giunzione", "EC2 §§9.5.2 / 9.6.2", "As ≤ 0,08 Ac. Includere nella geometria tutte le barre presenti nella giunzione, comprese quelle sovrapposte."),
            _ => throw new NotSupportedException("Controllo dei dettagli non mappato: " + r.Key)
        };
        return new(name, r.Actual, r.Limit, r.Unit.Replace("mm2", "mm²"), r.Passed, reference, text);
    }
}
