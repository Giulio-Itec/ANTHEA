using System.IO;
using System.Globalization;
using System.Text.Json;

namespace Anthea.Muro;

public static class Tests
{
    public static string Run()
    {
        var log=new List<string>();
        void Near(string label,double a,double b,double tol=1e-8){if(!double.IsFinite(a)||Math.Abs(a-b)>tol*Math.Max(1,Math.Abs(b)))throw new Exception($"{label}: {a:R} != {b:R}");log.Add("OK "+label);}
        void True(string label,bool ok){if(!ok)throw new Exception(label);log.Add("OK "+label);}
        Input Edit(Input input,string key,string value){var copy=input with{Values=new(input.Values)};copy.Values[key]=value;return copy;}
        void Reject(string label,Input input){try{Engine.Calculate(input);}catch(ArgumentException){log.Add("OK "+label);return;}throw new Exception("Accettato input non valido: "+label);}
        var input=Input.Example();var r=Engine.Calculate(input);var k=r.Characteristic;
        Near("Rankine phi 30",r.Ka,1.0/3);
        Near("Larghezza base",r.Width,3);
        Near("Peso caratteristico",k.Vertical,174.1);
        Near("Spinta terre + q sul piano H+t",k.Horizontal,46.01333333333333);
        Near("Momento stabilizzante",k.Stabilizing,315.655);
        Near("Momento ribaltante",k.Overturning,58.57066666666666);
        Near("Momento fusto H",k.StemMoment,42);
        Near("Taglio fusto H",k.StemShear,37);
        True("8 combinazioni distinte",r.Cases.Select(c=>(c.ConcreteFactor,c.SoilFactor,c.LiveFactor)).Distinct().Count()==8);
        Near("Nq phi 34",r.Nq,29.4397923696435,1e-10);
        foreach(var c in r.Cases)
        {
            Near(c.Name+" equilibrio momenti",c.Vertical*c.X+c.Overturning,c.Stabilizing);
            Near(c.Name+" coefficiente scorrimento",c.SlidingResistance*1.1,c.Vertical*Math.Tan(26*Math.PI/180));
            Near(c.Name+" coefficiente ribaltamento",c.OverturningResistance*1.15,c.Stabilizing);
            double n=0,m=0,dt=r.Width/20000;
            for(int j=0;j<20000;j++){double x=(j+.5)*dt,p=Engine.Pressure(r.Width,c.Vertical,c.X,x);n+=p*dt;m+=p*x*dt;}
            Near(c.Name+" integrale pressione = N",n,c.Vertical,1e-7);Near(c.Name+" integrale momento = Nx",m,c.Vertical*c.X,1e-7);
            // Independent numerical integration of base cantilevers.
            double toeM=0,heelM=0;
            for(int j=0;j<20000;j++)
            {
                double x=.8*(j+.5)/20000,p=Engine.Pressure(r.Width,c.Vertical,c.X,x)-.4*25*c.ConcreteFactor;
                toeM+=p*(.8-x)*.8/20000;
                x=1.1+1.9*(j+.5)/20000;p=Engine.Pressure(r.Width,c.Vertical,c.X,x)-.4*25*c.ConcreteFactor-3*18*c.SoilFactor-10*c.LiveFactor;
                heelM+=p*(x-1.1)*1.9/20000;
            }
            Near(c.Name+" flessione mensola valle",c.ToeMoment,toeM,1e-7);Near(c.Name+" flessione mensola monte",c.HeelMoment,heelM,1e-7);
        }
        True("Tallone teso superiormente nell’esempio",k.HeelMoment<0);
        foreach(double x in new[]{.1,.8,1d,1.5,2d,2.2,2.9})
        {
            double force=0,moment=0,dx=3d/100000;
            for(int j=0;j<100000;j++){double at=(j+.5)*dx,p=Engine.Pressure(3,100,x,at);force+=p*dx;moment+=p*at*dx;}
            Near($"Contatto x={x}: forza",force,100,1e-7);Near($"Contatto x={x}: momento",moment,100*x,1e-7);
        }
        Near("Transizione nocciolo",Engine.Pressure(3,100,1,3),0);
        Near("Risultante fuori valle",Engine.Contact(3,100,-.1).Peak,0);
        Near("Risultante fuori monte",Engine.Contact(3,100,3.1).Peak,0);
        Reject("Dato mancante",Edit(input,"H",""));Reject("Testo",Edit(input,"H","abc"));Reject("NaN",Edit(input,"H","NaN"));
        Reject("Infinito",Edit(input,"H","Infinity"));Reject("Quota negativa",Edit(input,"H","-1"));Reject("Sovraccarico negativo",Edit(input,"Q","-1"));
        Reject("Attrito eccessivo",Edit(input,"Delta","40"));Reject("Base liscia fuori campo",Edit(input,"Delta","10"));
        Near("Virgola decimale italiana",Engine.Calculate(Edit(input,"H","3,0")).Ka,r.Ka);
        Near("Q=0 valido",Engine.Calculate(Edit(input,"Q","0")).Characteristic.Horizontal,34.68);
        True("Aumento q aumenta spinta",Engine.Calculate(Edit(input,"Q","20")).Characteristic.Horizontal>k.Horizontal);
        var unstable=Engine.Calculate(Edit(input,"H","20"));True("Instabilità non nascosta",unstable.Checks.Any(c=>c.Status!="Soddisfatta"));
        True("Portanza nulla con perdita di contatto",unstable.Cases.Where(c=>c.ContactWidth==0).All(c=>c.BearingResistance==0));
        var json=JsonSerializer.Serialize(r,Engine.JsonOptions);True("Risultati instabili serializzabili",JsonSerializer.Serialize(unstable,Engine.JsonOptions).Length>0);
        var copy=JsonSerializer.Deserialize<Input>(JsonSerializer.Serialize(input))!;Near("Round trip input",Engine.Calculate(copy).Characteristic.Horizontal,k.Horizontal);
        True("Esportazione HTML include limiti",Report.Html(r).Contains("Non comprende sisma"));
        return string.Join(Environment.NewLine,log)+$"\n{log.Count} controlli superati.\n";
    }
}
