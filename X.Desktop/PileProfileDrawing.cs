using X.Core;

namespace X.Desktop;

public sealed partial class StratigraphyDrawing
{
    internal (double Top,double Thickness,double Bottom)[] PileDimensions(int index)
    {
        double z=0;return Data!.Array("stratigrafie")[index]!.AsArray().Where(r=>r.D("spessore")>0).Select(r=>{double top=z,h=r.D("spessore");z+=h;return(top,h,z);}).ToArray();
    }
    private void DrawPileProfile(Graphics g)
    {
        g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var indices=VisibleIndices;var gen=Data!["generali"]!;
        double maximum=indices.SelectMany(PileDimensions).Select(d=>d.Bottom).DefaultIfEmpty(0).Max();
        using var font=new Font("Segoe UI",14,FontStyle.Regular,GraphicsUnit.Pixel);using var bold=new Font("Segoe UI",14,FontStyle.Bold,GraphicsUnit.Pixel);
        if(maximum<=0){g.DrawString("Inserire la stratigrafia",font,Brushes.Gray,10,30);return;}
        float top=34,bottom=Height-54,band=Width/(float)Math.Max(1,indices.Length);if(bottom<=top)return;
        float scale=(bottom-top)/(float)maximum;Color[] colors=[Color.FromArgb(244,201,93),Color.FromArgb(223,160,110),Color.FromArgb(168,198,134),Color.FromArgb(143,184,222),Color.FromArgb(198,160,213)];
        using var pen=new Pen(Color.FromArgb(110,120,130));using var center=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};using var right=new StringFormat{Alignment=StringAlignment.Far,LineAlignment=StringAlignment.Center};
        for(int p=0;p<indices.Length;p++)
        {
            int index=indices[p];float left=p*band,x=left+54,width=Math.Max(24,band-136),dim=x+width+7;
            g.DrawString($"Stratigrafia {index+1}",bold,Brushes.DimGray,new RectangleF(left,3,band,26),center);
            var dimensions=PileDimensions(index);var rows=Data.Array("stratigrafie")[index]!.AsArray().Where(r=>r.D("spessore")>0).ToArray();
            for(int i=0;i<dimensions.Length;i++)
            {
                var d=dimensions[i];float y=top+(float)d.Top*scale,h=(float)d.Thickness*scale;using var fill=new SolidBrush(colors[i%colors.Length]);
                g.FillRectangle(fill,x,y,width,h);g.DrawRectangle(pen,x,y,width,h);
                g.DrawString($"{d.Top:0.##}",font,Brushes.DimGray,new RectangleF(left,y-10,49,20),right);
                g.DrawLine(pen,dim,y,dim,y+h);g.DrawLine(pen,dim-3,y,dim+3,y);g.DrawLine(pen,dim-3,y+h,dim+3,y+h);
                g.DrawString($"{d.Thickness:0.##} m",font,Brushes.DimGray,new RectangleF(dim+4,y,Math.Max(30,band-(dim-left)-5),h),center);
                string name=rows[i].S("strato",((char)('A'+i%26)).ToString());
                string text=h>=140?$"Strato {name}\n{rows[i].S("tipologia")} · {rows[i].S("addensamento")}\nφ′ = {rows[i].S("angolo_attrito")}°\nγ = {rows[i].S("peso_specifico")} · γsat = {rows[i].S("peso_specifico_saturo")} kN/m³\nc′ = {rows[i].S("coesione_efficace")} · Cu = {rows[i].S("coesione_non_drenata")} kPa":h>=70?$"Strato {name}\nφ′ = {rows[i].S("angolo_attrito")}°":$"Strato {name}";
                if(h>=18)g.DrawString(text,font,Brushes.Black,new RectangleF(x+2,y+1,width-4,h-2),center);
            }
            double total=dimensions.LastOrDefault().Bottom;float end=top+(float)total*scale;
            g.DrawString($"{total:0.##}",font,Brushes.DimGray,new RectangleF(left,end-10,49,20),right);
            g.DrawString($"Totale: {total:0.##} m",bold,Brushes.DimGray,new RectangleF(left,Height-45,band,22),center);
            if(gen.B("presenza_falda")){float water=top+(float)gen.D("profondita_falda")*scale;if(water>=top&&water<=end){using var wp=new Pen(Color.DodgerBlue,2){DashStyle=System.Drawing.Drawing2D.DashStyle.Dash};g.DrawLine(wp,x,water,x+width,water);g.DrawString("Falda",font,Brushes.DodgerBlue,x+3,water-18);}}
        }
        g.DrawString("Quote z [m] · positive verso il basso",font,Brushes.DimGray,3,Height-22);
    }
}
