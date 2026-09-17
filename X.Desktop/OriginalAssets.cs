namespace X.Desktop;

internal static class OriginalAssets
{
    public static readonly Image Logo=Load("logo.png"),Modules=Load("moduli.png");
    private static Image Load(string name)
    {
        var assembly=typeof(OriginalAssets).Assembly;using var stream=assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.EndsWith(".Assets."+name)))!;
        using var source=Image.FromStream(stream);return new Bitmap(source);
    }
    public static PictureBox LogoBox(int size)=>new(){Image=Logo,SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(size,size),BackColor=Color.White,Margin=new Padding(18,0,18,12)};
}

internal sealed class OriginalModuleIcon:Control
{
    public string Module{get;set;}="";
    public OriginalModuleIcon(){DoubleBuffered=true;BackColor=Color.White;}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;int size=Math.Min(80,Math.Min(Width,Height));
        if(Module=="str_palo")
        {
            using var navy=new SolidBrush(Ui.Navy);using var gold=new SolidBrush(Color.FromArgb(199,149,62));g.FillRectangle(navy,12,12,56,56);g.FillRectangle(Brushes.WhiteSmoke,15,15,50,50);
            foreach(int x in new[]{21,53})foreach(int y in new[]{21,37,53})g.FillRectangle(gold,x,y,6,6);
        }
        else{var (column,row)=Module switch{"geo_palo_orizzontale"=>(1,0),"geo_micropalo_verticale"=>(2,0),"geo_micropalo_orizzontale"=>(0,1),"str_micropalo"=>(2,1),_=>(0,0)};g.DrawImage(OriginalAssets.Modules,new Rectangle(0,0,size,size),new Rectangle(column*160,row*160,160,160),GraphicsUnit.Pixel);}
    }
}
