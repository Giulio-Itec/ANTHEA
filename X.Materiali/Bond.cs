using System.Windows;
using System.Windows.Controls;

namespace Materiali;
public sealed partial class MaterialView
{
    readonly TextBlock bondValue=Text("",22,true),bondDetails=Text("",12);
    bool bondReady;
    UIElement BuildBond()
    {
        bondValue.TextAlignment=TextAlignment.Center;
        bondDetails.TextAlignment=TextAlignment.Center;
        var content=Stack(Text("Resistenza di aderenza",15,true),
            Select("bondCondition","Condizioni di aderenza",["Buone · η₁ = 1,0","Altre · η₁ = 0,7"],1),
            Numeric("bondAlpha","Coefficiente di trazione · αct","1,0","—"),
            Numeric("bondGamma","Coefficiente parziale · γc","1,5","—"),bondValue,bondDetails,
            new Expander { Header="Formula e campo di applicazione",Content=Text("EN 1992-1-1:2004, §8.4.2, eq. (8.2): fbd = 2,25 η₁ η₂ fctd; fctd = αct fctk,0.05 / γc (§3.1.6). Diametro condiviso con il copriferro. η₂ = 1 per φ ≤32 mm, altrimenti (132−φ)/100. Per l'aderenza, fctk,0.05 è limitata al valore C60/75. Barre nervate, calcestruzzo normale, carichi prevalentemente statici. Le condizioni buone vanno accertate secondo figura 8.2; in altri casi η₁ = 0,7 (anche casseri scorrevoli salvo dimostrazione). αct = 1 e γc = 1,5 sono i valori iniziali per situazioni persistenti/transitorie; verificare annesso nazionale e situazione di progetto. Non è un calcolo della lunghezza di ancoraggio.",12) });
        bondReady=true; return Paper(content);
    }
    static (double Fct,double Fctd,double Eta2,double Fbd) Bond(double fck,double diameter,double eta1,double alpha,double gamma)
    {
        if(!double.IsFinite(diameter)||diameter<=0||diameter>=132||!double.IsFinite(alpha)||alpha<=0||alpha>1||!double.IsFinite(gamma)||gamma<1)
            throw new ArgumentException("Controllare φ (0 < φ < 132 mm), αct (0 < αct ≤1) e γc (≥1).");
        double fct=Math.Abs(Material(Math.Min(fck,60)).Fctk05),eta2=diameter<=32?1:(132-diameter)/100;
        double fctd=alpha*fct/gamma;
        return(fct,fctd,eta2,2.25*eta1*eta2*fctd);
    }
    void RefreshBond()
    {
        if(!bondReady || choice.SelectedIndex<0) return;
        bondValue.Text="Da completare";bondDetails.Text="";
        try
        {
            double diameter=Read("diameter","Diametro",.1,131.999),alpha=Read("bondAlpha","αct",.001,1),gamma=Read("bondGamma","γc",1,100);
            double fck=Classes[choice.SelectedIndex].Fck,eta1=choices["bondCondition"].SelectedIndex==0?1:.7;
            var b=Bond(fck,diameter,eta1,alpha,gamma);
            bondValue.Text=$"fbd = {b.Fbd:0.00} MPa";
            bondDetails.Text=$"φ = {diameter:0.##} mm  ·  η₁ = {eta1:0.00}  ·  η₂ = {b.Eta2:0.00}\nfctk,0.05 per aderenza = {b.Fct:0.00} MPa\nfctd = {b.Fctd:0.00} MPa"+(fck>60?"\nLimite C60/75 applicato.":"");
        }
        catch(ArgumentException ex) {bondDetails.Text=ex.Message;}
    }
    void CheckBond()
    {
        var good=Bond(30,16,1,1,1.5);
        if(Math.Abs(good.Fbd-3.041291)>1e-5) throw new Exception("Benchmark aderenza C30/37 errato.");
        if(Math.Abs(Bond(30,40,.7,1,1.5).Fbd-good.Fbd*.7*.92)>1e-10) throw new Exception("Coefficienti aderenza errati.");
        if(Bond(90,16,1,1,1.5).Fbd!=Bond(60,16,1,1,1.5).Fbd) throw new Exception("Limite C60/75 mancante.");
        choices["bondCondition"].SelectedIndex=0;
        if(!bondValue.Text.Contains("3,04")) throw new Exception("Aggiornamento aderenza errato.");
        numbers["bondGamma"].Text="0";
        if(bondValue.Text!="Da completare") throw new Exception("Risultato aderenza obsoleto.");
        numbers["bondGamma"].Text="1,5";
    }
}
